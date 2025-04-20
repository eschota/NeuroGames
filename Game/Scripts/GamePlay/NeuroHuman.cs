using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Needed for LINQ queries later
using System;
using System.Text;

namespace Game.Scripts.GamePlay
{
    // Making sure this namespace has access to the Neural Network classes
    // If NeuralNetwork is in another namespace, add a 'using' statement here.

    public class NeuroHuman : MonoBehaviour
    {
        [Header("Neural Network")]
        [SerializeField] private bool use_neural_control = true;
        [SerializeField] private Transform target_transform; // GO_Aim
        [SerializeField] private float max_lifetime = 30f;
        
        [Header("HingeJoint Settings")]
        [SerializeField] private float max_motor_force = 500f;
        [Tooltip("Максимальная скорость мотора для суставов")]
        [SerializeField] private float max_velocity = 20f; // Увеличиваем с 5f до 20f для более выраженных движений
        [Tooltip("Множитель для лимитов углов суставов (1.0 = стандартные лимиты)")]
        [SerializeField] private float angle_limit_multiplier = 1.0f;
        
        // Добавляем новые параметры для более тонкой настройки моторов
        [Tooltip("Минимальная подаваемая скорость мотора (чтобы избежать микродвижений)")]
        [SerializeField] private float min_motor_velocity_threshold = 0.01f; // Радикально снижаем с 0.05f до 0.01f
        [Tooltip("Усиление сигнала для моторов ног (множитель)")]
        [SerializeField] private float leg_motor_multiplier = 20.0f; // Было меньше
        [Tooltip("Усиление сигнала для моторов рук (множитель)")]
        [SerializeField] private float arm_motor_multiplier = 8.0f;
        [Tooltip("Применять разное усиление сигнала для разных групп суставов")]
        [SerializeField] private bool use_differential_motor_control = true;
        [Tooltip("Задержка между обновлениями моторов (сек)")]
        [SerializeField] private float motor_update_interval = 0.01f; // Было 0.1f или больше
        
        [Header("Body Parts")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform r_bot; // Right foot
        [SerializeField] private Transform l_bot; // Left foot
        
        [Header("Rewards")]
        [SerializeField] private float head_height_reward = 1f;
        [SerializeField] private float required_head_height = 0.5f;
        [SerializeField] private float target_reward = 100f;
        [SerializeField] private float movement_reward = 0.1f;
        [SerializeField] private float fall_penalty = 50f;
        
        // Новые параметры для усиленного обучения движению
        [Header("Enhanced Movement Rewards")]
        [Tooltip("Множитель награды за движение в сторону цели")]
        [SerializeField] private float target_direction_multiplier = 5.0f;
        [Tooltip("Множитель награды за стабильное положение стоя")]
        [SerializeField] private float standing_bonus_multiplier = 2.0f;
        [Tooltip("Минимальное время стояния для получения бонуса (сек)")]
        [SerializeField] private float min_standing_time = 1.0f;
        [Tooltip("Бонус за каждый метр смещения от начальной позиции")]
        [SerializeField] private float distance_from_start_reward = 2.0f;
        [Tooltip("Штраф за отсутствие движения")]
        [SerializeField] private float no_movement_penalty = 0.5f;
        [Tooltip("Награда за общую активность всех частей тела")]
        [SerializeField] private float global_activity_reward = 0.2f;
        [Tooltip("Минимальная скорость для учета активности")]
        [SerializeField] private float min_activity_threshold = 0.1f;
        
        // Список всех суставов
        [SerializeField] private List<HingeJoint> joints = new List<HingeJoint>();
        
        // Сохраняем автоматически определённую структуру сети
        [Header("Auto-Generated Network Structure")]
        [SerializeField] private int input_size = 0;
        [SerializeField] private int output_size = 0;
        [SerializeField] private int[] network_structure;
        
        // Приватные переменные
        private NeuralNetwork neural_network;
        private SimulationManager simulation_manager;
        private float lifetime = 0f;
        private float fitness = 0f;
        private Vector3 last_position;
        private Vector3 initial_position; // Сохраняем начальную позицию
        private float standing_time = 0f; // Время, которое агент стоит
        private float last_moved_time = 0f; // Последнее время, когда агент двигался
        private bool is_training = true;
        private bool is_disabled = false;
        private float generation_start_time;
        private int instance_id;
        private bool success_reported = false;
        private Transform spawn_point; // Точка спавна для возврата при аномалиях физики
        
        // Историю предыдущих действий будем хранить для включения в входы нейросети
        private float[] last_actions;
        
        // Новые переменные для отслеживания работы моторов
        private float last_motor_update_time = 0f;
        private Dictionary<HingeJoint, string> joint_types = new Dictionary<HingeJoint, string>();
        
        // Новые переменные для расчета фитнеса
        [Header("Basic Rewards")]
        [Tooltip("Базовая награда за каждый тик существования")]
        [SerializeField] private float survival_reward = 0.0001f;
        [Tooltip("Бонус за движение вперед (умножитель)")]
        [SerializeField] private float forward_movement_reward = 5.0f;
        [Tooltip("Минимальная высота головы для получения награды")]
        [SerializeField] private float min_head_height = 0.7f;

        [Header("Anti-Fall Rewards")]
        [Tooltip("Максимальный штраф за падение (когда агент лежит на земле)")]
        [SerializeField] private float max_fall_penalty = -2.0f;
        [Tooltip("Высота, ниже которой считается что агент упал")]
        [SerializeField] private float fall_height_threshold = 0.3f;

        [Header("Early Success Rewards")]
        [Tooltip("Бонус за первые шаги (временно)")]
        [SerializeField] private float early_steps_bonus = 0.2f;
        [Tooltip("Число поколений, в течение которых действует бонус за первые шаги")]
        [SerializeField] private int early_bonus_generations = 10;
        [Tooltip("Минимальное расстояние для получения бонуса за первые шаги")]
        [SerializeField] private float min_distance_for_early_bonus = 0.5f;

        [Header("Balance Rewards")]
        [Tooltip("Награда за равновесие (умножитель)")]
        [SerializeField] private float balance_reward = 0.3f;
        [Tooltip("Штраф за отклонение от вертикали (умножитель)")]
        [SerializeField] private float tilt_penalty = 0.2f;

        // Сохраненные позиции для измерения прогресса
        private float total_distance_moved = 0f;
        private float best_distance = 0f;
        private float time_upright = 0f;
        private bool has_fallen = false;
        private float consecutive_upright_time = 0f;
        
        // Метод для автоматического поиска всех суставов и настройки нейросети
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Собираем все суставы (HingeJoint) из всех дочерних объектов
            HingeJoint[] foundJoints = GetComponentsInChildren<HingeJoint>(true);
            
            // Убираем излишнее логгирование для каждого агента
            // Debug.Log($"🦾 Найдено {foundJoints.Length} суставов! Автоматически настраиваю нейросетевой скелет...");
            
            // Очищаем старый список и добавляем новые суставы
            joints.Clear();
            joint_types.Clear(); // Очищаем классификацию суставов
            
            foreach (HingeJoint joint in foundJoints)
            {
                joints.Add(joint);
                
                // Классифицируем сустав по имени
                string jointName = joint.name.ToLower();
                string jointType = "torso"; // По умолчанию
                
                if (jointName.Contains("leg") || jointName.Contains("foot") || 
                    jointName.Contains("knee") || jointName.Contains("noga") || 
                    jointName.Contains("hip") || jointName.Contains("ankle"))
                {
                    jointType = "leg";
                }
                else if (jointName.Contains("arm") || jointName.Contains("hand") || 
                         jointName.Contains("wrist") || jointName.Contains("elbow") || 
                         jointName.Contains("finger") || jointName.Contains("ruka"))
                {
                    jointType = "arm";
                }
                else if (jointName.Contains("head") || jointName.Contains("neck") || 
                         jointName.Contains("spine") || jointName.Contains("back"))
                {
                    jointType = "spine";
                }
                
                joint_types[joint] = jointType;
                
                // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Принудительно устанавливаем правильные параметры мотора
                JointMotor motor = joint.motor;
                motor.force = max_motor_force; // ЯВНО устанавливаем максимальную силу
                motor.targetVelocity = 0;
                motor.freeSpin = false;
                joint.motor = motor;
                
                // Включаем использование мотора
                joint.useMotor = true;
                
                // Устанавливаем лимиты, если их ещё нет
                JointLimits limits = joint.limits;
                if (limits.min == 0 && limits.max == 0)
                {
                    limits.min = -45 * angle_limit_multiplier;
                    limits.max = 45 * angle_limit_multiplier;
                    joint.limits = limits;
                }
                else
                {
                    // Для существующих лимитов применяем множитель
                    limits.min *= angle_limit_multiplier;
                    limits.max *= angle_limit_multiplier;
                    joint.limits = limits;
                }
                
                // Включаем использование лимитов
                joint.useLimits = true;
            }
            
            // Автоматически определяем структуру нейросети
            DetermineNetworkStructureAdvanced();
        }
        
        // Улучшенное определение структуры нейросети
        private void DetermineNetworkStructureAdvanced()
        {
            // Размер выходного слоя = количество суставов
            output_size = joints.Count;
            
            // Рассчитываем точный размер входного слоя на основе логических групп данных
            int bodyPositionInputsCount = 4; // Положение головы + ног + расстояние между ногами
            int velocityInputsCount = 4;     // Линейная скорость (xyz) + угловая скорость
            int jointAngleInputsCount = joints.Count; // По одному входу на каждый сустав
            int jointPositionInputsCount = joints.Count * 4; // По 4 входа на каждый сустав (x, y, z, magnitude)
            int targetInputsCount = 3;       // Направление (x, z) + дистанция
            int memoryInputsCount = joints.Count + 1; // Предыдущие действия + время жизни
            
            // Считаем общее количество входов
            input_size = bodyPositionInputsCount + 
                         velocityInputsCount + 
                         jointAngleInputsCount + 
                         jointPositionInputsCount + 
                         targetInputsCount + 
                         memoryInputsCount;
            
            // Создаем более глубокую структуру сети с несколькими скрытыми слоями
            int layer1Size = Mathf.Max(32, input_size); // Первый скрытый слой - не меньше 32 нейронов
            int layer2Size = output_size * 4; // Второй скрытый слой - в 4 раза больше выходного
            
            // Создаем структуру сети с четырьмя слоями: входной, два скрытых, выходной
            network_structure = new int[4];
            network_structure[0] = input_size;
            network_structure[1] = layer1Size;
            network_structure[2] = layer2Size;
            network_structure[3] = output_size;
            
            // Выводим подробности о структуре входного слоя
            Debug.Log($"🔢 СТРУКТУРА ВХОДОВ НЕЙРОСЕТИ:" +
                $"\n- Позиции тела: {bodyPositionInputsCount}" +
                $"\n- Скорости: {velocityInputsCount}" +
                $"\n- Углы суставов: {jointAngleInputsCount}" +
                $"\n- Относительные позиции суставов: {jointPositionInputsCount}" +
                $"\n- Цель: {targetInputsCount}" +
                $"\n- Память: {memoryInputsCount}" +
                $"\n= ВСЕГО ВХОДОВ: {input_size}" +
                $"\n= ВСЕГО ВЫХОДОВ: {output_size}");
            
            // Добавляем информацию о получившейся структуре сети
            Debug.Log($"🧠 СТРУКТУРА СЕТИ: {input_size}-{layer1Size}-{layer2Size}-{output_size}");
            
            // Если где-то есть SimulationManager, обновляем его структуру нейросети
            SimulationManager sim = FindObjectOfType<SimulationManager>();
            if (sim != null)
            {
                // Мы не можем напрямую обновить SimulationManager из OnValidate,
                // но можно вывести сообщение для пользователя
                Debug.Log($"⚠️ Обновлена структура сети. Для применения обновите SimulationManager со слоями: [{string.Join(", ", network_structure.Select(x => x.ToString()))}]");
            }
        }
        
        // Кнопка для обновления SimulationManager с новой структурой сети
        [UnityEngine.ContextMenu("Обновить структуру сети в SimulationManager")]
        public void UpdateSimulationManagerNetwork()
        {
            if (network_structure == null || network_structure.Length < 2)
            {
                Debug.LogError("❌ Структура сети не определена! Сначала запустите OnValidate!");
                return;
            }
            
            SimulationManager sim = FindObjectOfType<SimulationManager>();
            if (sim == null)
            {
                Debug.LogError("❌ SimulationManager не найден на сцене! Добавьте его сначала!");
                return;
            }
            
            // Обновляем структуру сети в SimulationManager
            sim.UpdateNetworkStructure(network_structure);
            Debug.Log($"🤘 Заебись! Структура нейросети успешно обновлена в SimulationManager!");
        }
        #endif

        void Start()
        {
            // CRITICAL: Принудительно сбросить все моторы при старте
            ResetAllMotors();
            
            // Генерируем уникальный ID для этого экземпляра
            instance_id = GetInstanceID();
            
            // Получаем ссылку на SimulationManager
            if (simulation_manager == null)
            {
                simulation_manager = FindObjectOfType<SimulationManager>();
                if (simulation_manager == null)
                {
                    Debug.LogError("❌ SimulationManager не найден на сцене!");
                    enabled = false;
                    return;
                }
            }
            
            // Запоминаем время начала работы
            generation_start_time = Time.time;
            
            // Поиск частей тела, если они не заданы
            if (head == null)
            {
                Transform foundHead = transform.Find("Head");
                if (foundHead != null)
                {
                    head = foundHead;
                    // Удаляем лишний лог
                    // Debug.Log($"✅ Автоматически найдена голова: {head.name}");
                }
                else
                {
                    // Поиск по имени
                    foreach (Transform child in GetComponentsInChildren<Transform>())
                    {
                        if (child.name.ToLower().Contains("head") || 
                            child.name.ToLower().Contains("golova"))
                        {
                            head = child;
                            // Debug.Log($"✅ Автоматически найдена голова по имени: {head.name}");
                            break;
                        }
                    }
                    
                    if (head == null)
                    {
                        Debug.LogWarning("⚠️ Голова не найдена! Многие функции будут недоступны!");
                    }
                }
            }
            
            // Автоматический поиск ног, если они не заданы
            if (r_bot == null || l_bot == null)
            {
                List<Transform> feet = new List<Transform>();
                
                // Ищем объекты с именами, содержащими "foot", "leg", "нога" и т.д.
                foreach (Transform child in GetComponentsInChildren<Transform>())
                {
                    string name = child.name.ToLower();
                    if (name.Contains("foot") || name.Contains("leg") || 
                        name.Contains("нога") || name.Contains("ступня"))
                    {
                        feet.Add(child);
                    }
                }
                
                // Если нашли хотя бы одну ногу
                if (feet.Count > 0)
                {
                    // Debug.Log($"✅ Автоматически найдено {feet.Count} нижних конечностей");
                    
                    // Если нашли две ноги - распределяем правую и левую
                    if (feet.Count >= 2)
                    {
                        // Сортируем по X-координате (считаем, что правая нога имеет большую X-координату)
                        feet.Sort((a, b) => a.position.x.CompareTo(b.position.x));
                        
                        l_bot = feet[0]; // Левая (меньшая X)
                        r_bot = feet[feet.Count - 1]; // Правая (большая X)
                        
                        // Debug.Log($"✅ Ноги распределены: L={l_bot.name}, R={r_bot.name}");
                    }
                    else
                    {
                        // Если нашли только одну ногу - используем её для обеих ссылок
                        r_bot = l_bot = feet[0];
                        Debug.LogWarning("⚠️ Найдена только одна нога! Используем её для обеих ссылок.");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️ Ноги не найдены! Многие функции будут недоступны!");
                }
            }
            
            // Если суставы ещё не были найдены, делаем это автоматически
            if (joints == null || joints.Count == 0)
            {
                HingeJoint[] foundJoints = GetComponentsInChildren<HingeJoint>();
                if (foundJoints != null && foundJoints.Length > 0)
                {
                    foreach (HingeJoint joint in foundJoints)
                    {
                        if (joint != null)
                        {
                            joints.Add(joint);
                            
                            // Классифицируем сустав по имени
                            string jointName = joint.name.ToLower();
                            string jointType = "torso"; // По умолчанию
                            
                            if (jointName.Contains("leg") || jointName.Contains("foot") || 
                                jointName.Contains("knee") || jointName.Contains("noga") || 
                                jointName.Contains("hip") || jointName.Contains("ankle"))
                            {
                                jointType = "leg";
                            }
                            else if (jointName.Contains("arm") || jointName.Contains("hand") || 
                                     jointName.Contains("wrist") || jointName.Contains("elbow") || 
                                     jointName.Contains("finger") || jointName.Contains("ruka"))
                            {
                                jointType = "arm";
                            }
                            else if (jointName.Contains("head") || jointName.Contains("neck") || 
                                     jointName.Contains("spine") || jointName.Contains("back"))
                            {
                                jointType = "spine";
                            }
                            
                            joint_types[joint] = jointType;
                            
                            // Настраиваем сустав
                            JointMotor motor = joint.motor;
                            motor.force = max_motor_force;
                            motor.targetVelocity = 0;
                            joint.motor = motor;
                            joint.useMotor = true;
                            
                            // Применяем множитель к лимитам суставов
                            JointLimits limits = joint.limits;
                            limits.min *= angle_limit_multiplier;
                            limits.max *= angle_limit_multiplier;
                            joint.limits = limits;
                            joint.useLimits = true;
                        }
                    }

                    Debug.Log($"✅ Автоматически найдено и настроено {joints.Count} суставов!");
                }
            }
            
            // Сохраняем начальную позицию для расчёта смещения
            initial_position = transform.position;
            last_position = transform.position;
            
            // Пересчитываем структуру сети в соответствии с текущими суставами
            if (network_structure == null || network_structure.Length < 2) 
            {
                DetermineNetworkStructureAdvanced();
                Debug.Log($"🔄 Пересчитана структура сети в Start(): [{string.Join(", ", network_structure.Select(x => x.ToString()))}]");
            }
            
            // Получаем конфигурацию слоев из SimulationManager
             int[] layers = simulation_manager.GetNeuralLayers();
            if (layers == null || layers.Length < 2)
            {
                Debug.LogError("❌ Некорректная конфигурация слоев нейросети!");
                 enabled = false;
                return;
            }
            
            // Инициализируем массив предыдущих действий
            if (last_actions == null || last_actions.Length != joints.Count)
            {
                last_actions = new float[joints.Count];
                for (int i = 0; i < last_actions.Length; i++)
                {
                    last_actions[i] = 0f;
                }
                Debug.Log($"✅ Инициализирован массив last_actions с размером {joints.Count}");
            }
            
            // Создаем нейросеть, если она еще не создана
            if (neural_network == null)
            {
                try
                {
                    // Создаем нейросеть напрямую вместо использования GeneticAlgorithm
                    neural_network = new NeuralNetwork(layers);
                    // neural_network.InitializeWeights(); // Этого метода нет, конструктор уже всё инициализирует!
                    
                    // Принудительная инициализация ненулевыми весами для гарантии движения
                    ForceNonZeroInitialization();
                    
                    is_training = true;
                    Debug.Log($"✅ Создана новая случайная сеть для человека {instance_id}!");
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка создания сети: {e.Message}");
                    enabled = false;
                    return;
                }
            }
            
            // Запоминаем начальную позицию
            last_position = transform.position;
            
            // Ищем цель, если не задана
            if (target_transform == null)
            {
                GameObject target = GameObject.FindGameObjectWithTag("AIM");
                if (target != null)
                {
                    target_transform = target.transform;
                    Debug.Log("🎯 Автоматически найдена цель с тегом AIM");
                }
                else
                {
                    Debug.LogWarning("⚠️ Цель не найдена! Агент не сможет отслеживать цель.");
                }
            }
        }

        // Обновленный метод для расчета фитнеса
        public float GetFitness()
        {
            if (neural_network == null) 
                return 0f;
            
            // Получаем текущую позицию и высоту головы
            Vector3 current_position = transform.position;
            float head_height = head != null ? head.position.y - transform.position.y : 0f;
            
            // Расчет награды за выживание и высоту головы
            float fitness = survival_reward * Time.time;
            
            // 1. Награда за высоту головы (базовое стояние)
            if (head_height > min_head_height)
            {
                fitness += head_height * head_height_reward;
                consecutive_upright_time += Time.deltaTime;
                
                // Дополнительная награда за продолжительное удержание равновесия
                if (consecutive_upright_time > 3.0f)
                {
                    fitness += balance_reward * consecutive_upright_time * 0.1f;
                }
            }
            else
            {
                consecutive_upright_time = 0f;
                
                // Штраф за падение, пропорциональный тому, насколько низко опустилась голова
                float fall_ratio = Mathf.Clamp01((min_head_height - head_height) / min_head_height);
                float current_fall_penalty = Mathf.Lerp(fall_penalty, max_fall_penalty, fall_ratio);
                
                fitness += current_fall_penalty;
                
                if (!has_fallen && head_height < fall_height_threshold)
                {
                    has_fallen = true;
                    Debug.Log($"👎 Агент {name} упал! Высота головы: {head_height:F2}");
                }
            }
            
            // 2. Награда за движение вперед
            float distance_moved = Vector3.Distance(current_position, last_position);
            
            // Учитываем только движение вперед (вдоль локальной оси Z)
            Vector3 local_movement = transform.InverseTransformDirection(current_position - last_position);
            float forward_distance = local_movement.z;
            
            if (forward_distance > 0)
            {
                // Награда за движение вперед
                fitness += forward_distance * forward_movement_reward;
                
                // Общее пройденное расстояние
                total_distance_moved += forward_distance;
                
                // Обновляем лучшее расстояние
                if (total_distance_moved > best_distance)
                {
                    best_distance = total_distance_moved;
                }
                
                // Бонус за первые шаги в ранних поколениях
                if (simulation_manager != null && 
                    simulation_manager.GetCurrentGeneration() < early_bonus_generations && 
                    total_distance_moved > min_distance_for_early_bonus)
                {
                    fitness += early_steps_bonus;
                }
            }
            
            // 3. Штраф за наклон (отклонение от вертикали)
            float upright_dot = Vector3.Dot(transform.up, Vector3.up);
            float tilt_factor = 1f - upright_dot; // 0 = вертикально, 1 = лежит
            
            fitness -= tilt_factor * tilt_penalty;
            
            // 4. Бонус за оставление следов (означает что агент идет)
            // Если это важно, добавь здесь логику
            
            // Обновляем последнюю позицию для следующего кадра
            last_position = current_position;
            
            // КРИТИЧЕСКИ ВАЖНО: защита от магического фитнеса
            if (Math.Abs(fitness - 200.02f) < 0.1f)
            {
                Debug.LogWarning($"⚠️ ПЕРЕХВАЧЕН МАГИЧЕСКИЙ ФИТНЕС {fitness} в GetFitness! Сбрасываем до нуля.");
                fitness = 0f;
            }
            
            // Сохраняем фитнес в нейросеть
            if (neural_network != null)
            {
                neural_network.fitness = fitness;
            }
            
            return fitness;
        }

        // Сбрасываем все расчеты при респавне
        public void ResetFitness()
        {
            // Сохраняем начальное положение для расчетов движения
            initial_position = transform.position;
            last_position = transform.position;
            
            // Сбрасываем все счетчики
            total_distance_moved = 0f;
            best_distance = 0f;
            time_upright = 0f;
            has_fallen = false;
            consecutive_upright_time = 0f;
            
            // Сбрасываем фитнес в нейросети
            if (neural_network != null)
            {
                neural_network.fitness = 0f;
            }
            
            Debug.Log($"🔄 Сброшен фитнес для агента {name}");
        }

        // Метод для установки нейросети с дополнительными проверками
        public void SetNeuralNetwork(NeuralNetwork network)
        {
            // ДИАГНОСТИКА ФИТНЕСА ПЕРЕД УСТАНОВКОЙ
            float oldFitness = neural_network != null ? neural_network.fitness : 0f;
            Debug.Log($"👉 SetNeuralNetwork для {name}: Старый фитнес = {oldFitness}");
            
            neural_network = network;
            
            // ПРОВЕРКА НА МАГИЧЕСКИЙ ФИТНЕС (еще один барьер)
            if (network != null && Math.Abs(network.fitness - 200.02f) < 0.1f)
            {
                Debug.LogWarning($"⚠️ ОБНАРУЖЕН МАГИЧЕСКИЙ ФИТНЕС {network.fitness} в SetNeuralNetwork! Сбрасываем в 0.");
                network.fitness = 0f;
            }
            
            // ВСЕГДА сбрасываем фитнес при установке новой сети
            if (network != null)
            {
                network.fitness = 0f;
            }
            
            // Сбрасываем все данные о фитнесе
            ResetFitness();
            
            // ДИАГНОСТИКА ФИТНЕСА ПОСЛЕ УСТАНОВКИ
            float newFitness = neural_network != null ? neural_network.fitness : 0f;
            Debug.Log($"👌 SetNeuralNetwork для {name}: Новый фитнес = {newFitness}");
        }

        void Update()
        {
            if (use_neural_control && is_training && !is_disabled)
            {
                // Обновляем время жизни
            lifetime = Time.time - generation_start_time;

                // Проверяем физику на аномалии каждые 0.5 секунд
                if (Time.frameCount % 30 == 0) // ~0.5 сек при 60 FPS
                {
                    CheckForPhysicsAnomalies();
                }
                
                // Проверяем положение головы для награды
                CheckHeadHeight();
                
                // Проверяем достижение цели
                CheckTargetReached();
            }
        }

        void FixedUpdate()
        {
            if (is_disabled || neural_network == null || !use_neural_control)
                return;
                
            // Гарантируем, что все моторы включены
            EnsureMotorsEnabled();
            
            // Проверяем и ограничиваем физику каждый физический кадр
            FixedUpdatePhysicsChecks();
                
            // Обновляем время жизни
            lifetime += Time.fixedDeltaTime;
            
            // Проверяем, не истекло ли время жизни
            if (lifetime >= max_lifetime)
            {
                DisableAgent("Истекло время жизни");
                return;
            }
            
            // Используем нейросеть для управления
            UseNeuralNetworkControl();
            
            // Проверяем высоту головы и другие условия
            CheckHeadHeight();
            CheckTargetReached();
            CheckFallen();
            
            // Проверяем физические аномалии реже
            if (Time.frameCount % 30 == 0)
            {
                CheckForPhysicsAnomalies();
            }
        }
        
        private void UseNeuralNetworkControl()
        {
            if (neural_network == null) return;
            
            try
            {
                // Проверяем, нужно ли обновлять моторы
                if (Time.time - last_motor_update_time < motor_update_interval)
                {
                    return; // Пропускаем этот апдейт
                }
                
                // Запоминаем время последнего обновления моторов
                last_motor_update_time = Time.time;
                
                // Убедимся, что last_actions инициализирован правильно
                if (last_actions == null || last_actions.Length != joints.Count) 
                {
                    last_actions = new float[joints.Count];
                    // Выводим только действительно важные сообщения, с указанием ID агента
                    // для возможности отслеживания конкретного экземпляра
                    Debug.LogWarning($"🔄 [ID:{instance_id}] Переинициализирован массив last_actions с размером {joints.Count}");
                }
                
                // ЧЕТКАЯ СТРУКТУРА: Для лучшего понимания разбиваем входы на логические группы
                List<float> bodyPositionInputs = new List<float>();
                List<float> velocityInputs = new List<float>();
                List<float> jointAngleInputs = new List<float>();
                List<float> jointPositionInputs = new List<float>();
                List<float> targetInputs = new List<float>();
                List<float> memoryInputs = new List<float>();
                
                // Получаем сылки на необходимые компоненты
                Rigidbody rb = GetComponent<Rigidbody>();
                
                // === ГРУППА 1: ДАННЫЕ О ПОЛОЖЕНИИ ОСНОВНЫХ ЧАСТЕЙ ТЕЛА ===
                // Положение головы относительно центра масс
                bodyPositionInputs.Add(head.position.y - transform.position.y);
                
                // Положение ног относительно центра масс
                bodyPositionInputs.Add(r_bot.position.y - transform.position.y);
                bodyPositionInputs.Add(l_bot.position.y - transform.position.y);
                
                // Расстояние между ногами
                bodyPositionInputs.Add(Vector3.Distance(r_bot.position, l_bot.position));
                
                // === ГРУППА 2: ДАННЫЕ О СКОРОСТИ И НАПРАВЛЕНИИ ===
                if (rb != null)
                {
                    velocityInputs.Add(rb.linearVelocity.x);
                    velocityInputs.Add(rb.linearVelocity.z);
                    velocityInputs.Add(rb.linearVelocity.y); // Вертикальная скорость
                    velocityInputs.Add(rb.angularVelocity.magnitude);
            }
            else
            {
                    velocityInputs.Add(0f); velocityInputs.Add(0f); velocityInputs.Add(0f); velocityInputs.Add(0f);
                }
                
                // === ГРУППА 3: ДАННЫЕ О СУСТАВАХ ===
                // Углы суставов
                if (joints != null)
                {
                    foreach (HingeJoint joint in joints)
                    {
                        if (joint != null)
                        {
                            // Защита от ошибок с HingeJoint лимитами
                            float min = joint.limits.min;
                            float max = joint.limits.max;
                            if (min == max) // Избегаем деления на ноль
                            {
                                jointAngleInputs.Add(0f);
                            }
                            else
                            {
                                // Нормализуем угол сустава от -1 до 1, с защитой от ошибок
                                float normalizedAngle = Mathf.Clamp01(Mathf.InverseLerp(min, max, joint.angle)) * 2f - 1f;
                                jointAngleInputs.Add(normalizedAngle);
                            }
                        }
                        else
                        {
                            jointAngleInputs.Add(0f);
                        }
                    }
                }
                
                // === ГРУППА 4: ПОЛОЖЕНИЯ СУСТАВОВ ОТНОСИТЕЛЬНО ГОЛОВЫ ===
                if (head != null && joints != null)
                {
                    foreach (HingeJoint joint in joints)
                    {
                        if (joint != null)
                        {
                            // Получаем позицию сустава
                            Vector3 jointPosition = joint.transform.position;
                            
                            // Вычисляем разницу между суставом и головой
                            Vector3 relativePosition = jointPosition - head.position;
                            
                            // Нормализуем значения и добавляем в входные данные
                            jointPositionInputs.Add(relativePosition.x / 2.0f); // Делим на 2, чтобы нормализовать в диапазоне примерно [-1, 1]
                            jointPositionInputs.Add(relativePosition.y / 2.0f);
                            jointPositionInputs.Add(relativePosition.z / 2.0f);
                            
                            // Также добавляем дистанцию (для упрощения понимания пространства)
                            jointPositionInputs.Add(relativePosition.magnitude / 3.0f); // Нормализуем величину
                        }
                        else
                        {
                            // Заполнители для отсутствующих данных
                            jointPositionInputs.Add(0f); jointPositionInputs.Add(0f); 
                            jointPositionInputs.Add(0f); jointPositionInputs.Add(0f);
                        }
                    }
                }
                
                // === ГРУППА 5: ИНФОРМАЦИЯ О ЦЕЛИ ===
                if (target_transform != null)
                {
                    // Направление к цели в локальных координатах
                    Vector3 dirToTarget = target_transform.position - transform.position;
                    Vector3 localDir = transform.InverseTransformDirection(dirToTarget.normalized);
                    
                    targetInputs.Add(localDir.x);
                    targetInputs.Add(localDir.z);
                    
                    // Дистанция до цели (нормализованная)
                    float distToTarget = dirToTarget.magnitude;
                    targetInputs.Add(Mathf.Clamp01(distToTarget / 10f));
            }
            else
            {
                    targetInputs.Add(0f); targetInputs.Add(0f); targetInputs.Add(0f);
                }
                
                // === ГРУППА 6: ПАМЯТЬ О ПРЕДЫДУЩИХ ДЕЙСТВИЯХ И ТЕКУЩЕЕ СОСТОЯНИЕ ===
                // Добавляем предыдущие действия
                if (last_actions != null)
                {
                    for (int i = 0; i < last_actions.Length; i++)
                    {
                        memoryInputs.Add(last_actions[i]);
                    }
                }
                
                // Добавляем время жизни (нормализованное)
                memoryInputs.Add(Mathf.Clamp01(lifetime / max_lifetime));
                
                // Собираем все группы входов в один общий список
                List<float> inputs = new List<float>();
                inputs.AddRange(bodyPositionInputs);    // ~4 входа
                inputs.AddRange(velocityInputs);        // ~4 входа
                inputs.AddRange(jointAngleInputs);      // ~joints.Count входов
                inputs.AddRange(jointPositionInputs);   // ~joints.Count * 4 входов
                inputs.AddRange(targetInputs);          // 3 входа
                inputs.AddRange(memoryInputs);          // ~joints.Count + 1 входов
                
                // Проверка на бесконечные или NaN значения во входных данных
                for (int i = 0; i < inputs.Count; i++)
                {
                    if (float.IsInfinity(inputs[i]) || float.IsNaN(inputs[i]))
                    {
                        Debug.LogWarning($"⚠️ Обнаружено бесконечное/NaN значение во входных данных нейросети (индекс {i}): {inputs[i]}. Заменяем на 0.");
                        inputs[i] = 0f;
                    }
                    
                    // Жесткое ограничение входных значений для предотвращения взрыва градиентов
                    if (Mathf.Abs(inputs[i]) > 10f)
                    {
                        Debug.LogWarning($"⚠️ Слишком большое значение во входных данных нейросети (индекс {i}): {inputs[i]}. Ограничиваем.");
                        inputs[i] = Mathf.Clamp(inputs[i], -10f, 10f);
                    }
                }
                
                // Запомним размер каждой группы для отладки - ОГРАНИЧИВАЕМ ЧАСТОТУ ЛОГОВ
                if (Time.frameCount % 5000 == 0 && instance_id % 10 == 0) // Логируем очень редко и только для 10% агентов
                {
                    string inputStructure = $"[ID:{instance_id}] СТРУКТУРА ВХОДОВ: " +
                        $"Позиции тела: {bodyPositionInputs.Count}, " +
                        $"Скорости: {velocityInputs.Count}, " +
                        $"Углы суставов: {jointAngleInputs.Count}, " +
                        $"Относительные позиции суставов: {jointPositionInputs.Count}, " +
                        $"Цель: {targetInputs.Count}, " +
                        $"Память: {memoryInputs.Count}, " +
                        $"ВСЕГО: {inputs.Count}";
                    Debug.Log(inputStructure);
                }
                
                // Защитная проверка для neural_network и его слоев
                if (neural_network == null || neural_network.layers == null || neural_network.layers.Length < 2)
                {
                    Debug.LogError("❌ Некорректная структура нейросети!");
                    return;
                }
                
                // АДАПТИВНЫЙ ПОДХОД: Подстраиваем входной вектор под размер сети
                float[] inputArray = inputs.ToArray();
                float[] adjustedInputs;
                
                if (neural_network.layers[0] < inputArray.Length)
                {
                    // Если входной слой меньше, чем наши данные - обрезаем лишнее
                    adjustedInputs = new float[neural_network.layers[0]];
                    Array.Copy(inputArray, adjustedInputs, neural_network.layers[0]);
                    if (Time.frameCount % 5000 == 0 && instance_id % 10 == 0) // Логируем очень редко
                    {
                        Debug.LogWarning($"⚠️ [ID:{instance_id}] Обрезаны входные данные: {inputArray.Length} -> {neural_network.layers[0]}");
                    }
                }
                else if (neural_network.layers[0] > inputArray.Length)
                {
                    // Если входной слой больше наших данных - дополняем нулями
                    adjustedInputs = new float[neural_network.layers[0]];
                    Array.Copy(inputArray, adjustedInputs, inputArray.Length);
                    if (Time.frameCount % 5000 == 0 && instance_id % 10 == 0) // Логируем очень редко
                    {
                        Debug.LogWarning($"⚠️ [ID:{instance_id}] Дополнены входные данные нулями: {inputArray.Length} -> {neural_network.layers[0]}");
                    }
                }
                else
                {
                    // Размеры совпадают, используем как есть
                    adjustedInputs = inputArray;
                }
                
                try
                {
                    // Получаем выходные сигналы нейросети, используя адаптированный размер
                    float[] outputs = neural_network.FeedForward(adjustedInputs);
                    
                    // Защитная проверка размерности выходного массива
                    if (outputs == null)
                    {
                        Debug.LogError("❌ FeedForward вернул null вместо массива выходов!");
                        return;
                    }

                    if (outputs.Length != neural_network.layers[neural_network.layers.Length - 1])
                    {
                        Debug.LogError($"❌ Некорректный размер выходного слоя! Ожидается: {neural_network.layers[neural_network.layers.Length - 1]}, Получено: {outputs.Length}");
                        return;
                    }
                    
                    // ДИАГНОСТИКА ДЛЯ ЛУЧШЕГО АГЕНТА: Сравним все выходы нейросети
                    bool isTopAgent = fitness > 5.0f; // Если фитнес значительный - это вероятно хороший агент
                    
                    if (isTopAgent && Time.frameCount % 200 == 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"\n🔍 ДИАГНОСТИКА ВЫХОДОВ НЕЙРОСЕТИ [ID:{instance_id}, Фитнес:{fitness:F2}]:");
                        float maxOutput = 0f;
                        int nonZeroOutputs = 0;
                        
                        for (int i = 0; i < outputs.Length && i < 10; i++) // Показываем максимум 10 выходов
                        {
                            sb.AppendLine($"  Выход {i}: {outputs[i]:F6}");
                            if (!Mathf.Approximately(outputs[i], 0f))
                            {
                                nonZeroOutputs++;
                                maxOutput = Mathf.Max(maxOutput, Mathf.Abs(outputs[i]));
                            }
                        }
                        
                        if (nonZeroOutputs == 0)
                        {
                            sb.AppendLine("  ⚠️ ВСЕ ВЫХОДЫ НЕЙРОСЕТИ НУЛЕВЫЕ!");
                            
                            // Логируем в файл для последующего анализа
                            SimulationManager manager = FindObjectOfType<SimulationManager>();
                            if (manager != null)
                            {
                                manager.WriteToLogFile($"ДИАГНОСТИКА - ОБНАРУЖЕНЫ НУЛЕВЫЕ ВЫХОДЫ [ID:{instance_id}, Время:{Time.time:F1}]\n");
                                manager.WriteToLogFile($"Фитнес: {fitness:F2}, Входов: {adjustedInputs.Length}, Выходов: {outputs.Length}\n");
                                manager.WriteToLogFile("Первые 5 входов: ");
                                
                                for (int i = 0; i < Math.Min(5, adjustedInputs.Length); i++)
                                {
                                    manager.WriteToLogFile($"{adjustedInputs[i]:F4} ");
                                }
                                
                                manager.WriteToLogFile("\nПервые 5 выходов: ");
                                for (int i = 0; i < Math.Min(5, outputs.Length); i++)
                                {
                                    manager.WriteToLogFile($"{outputs[i]:F6} ");
                                }
                                
                                manager.WriteToLogFile("\n\n");
                            }
                        }
                        else
                        {
                            sb.AppendLine($"  ✓ Ненулевых выходов: {nonZeroOutputs}/{outputs.Length}, Макс: {maxOutput:F6}");
                        }
                        
                        Debug.Log(sb.ToString());
                    }
                    
                    // Перепроверим, существует ли массив суставов и last_actions перед использованием
                    if (joints == null || last_actions == null)
                    {
                        Debug.LogError("❌ Массив суставов или действий оказался null!");
                        return;
                    }
                    
                    // Проверяем размер выходных данных и списка суставов
                    int actionCount = Math.Min(joints.Count, outputs.Length);
                    
                    // Применяем выходные сигналы к суставам
                    for (int i = 0; i < actionCount; i++)
                    {
                        if (i >= joints.Count || joints[i] == null)
                            continue;
                            
                        // Применяем сигнал от нейросети к суставу
                        float action = outputs[i]; // Сигнал уже должен быть в диапазоне [-1, 1]
                        
                        // Усиливаем сигнал в зависимости от типа сустава
                        if (use_differential_motor_control && joint_types.ContainsKey(joints[i]))
                        {
                            string jointType = joint_types[joints[i]];
                            
                            // Усиливаем сигнал для ног
                            if (jointType == "leg")
                            {
                                action *= leg_motor_multiplier;
                            }
                            // Для позвоночника ограничиваем силу сигнала для стабильности
                            else if (jointType == "spine")
                            {
                                action *= 0.7f;
                            }
                            // Для рук ограничиваем силу сигнала для стабильности
                            else if (jointType == "arm")
                            {
                                action *= arm_motor_multiplier;
                            }
                        }
                        
                        // Убедимся, что значение в диапазоне [-1, 1] после всех модификаций
                        action = Mathf.Clamp(action, -1f, 1f);
                        
                        last_actions[i] = action; // Сохраняем для следующего шага
                        
                        // Устанавливаем требуемую скорость мотора на основе выхода сети
                        try
                        {
                            // Меняем скорость мотора на основе выхода нейросети
                            JointMotor motor = joints[i].motor;
                            
                            // КРИТИЧЕСКИЙ БАГИ: Force равен 1, а должен быть равен max_motor_force
                            // Принудительно устанавливаем правильное значение силы
                            if (motor.force < max_motor_force * 0.9f)
                            {
                                motor.force = max_motor_force;
                                if (Time.frameCount % 1000 == 0 && instance_id % 10 == 0)
                                {
                                    Debug.LogWarning($"⚠️ [ID:{instance_id}] Обнаружена неправильная сила мотора: {joints[i].name} ({motor.force} вместо {max_motor_force}). Исправлено!");
                                    
                                    // Логируем в файл критическую ошибку
                                    SimulationManager manager = FindObjectOfType<SimulationManager>();
                                    if (manager != null)
                                    {
                                        manager.WriteToLogFile($"КРИТИЧЕСКАЯ ОШИБКА - Неправильная сила мотора [ID:{instance_id}, Время:{Time.time:F1}]\n");
                                        manager.WriteToLogFile($"Сустав: {joints[i].name}, Сила: {motor.force} вместо {max_motor_force}\n\n");
                                    }
                                }
                            }
                            
                            // Устанавливаем выходные данные нейросети в targetVelocity
                            motor.targetVelocity = action * max_velocity;
                            
                            // Дополнительное логирование для очень малых значений, которые могут быть причиной проблемы
                            if (isTopAgent && Math.Abs(action) < 0.001f && Time.frameCount % 200 == i)
                            {
                                Debug.LogWarning($"⚠️ [ID:{instance_id}] Очень малое значение action={action} для сустава {i} ({joints[i].name})");
                                Debug.LogWarning($"   Исходный выход нейросети: {outputs[i]:F8}, Множитель: {max_velocity}, Итог: {action * max_velocity:F8}");
                            }
                            
                            // Применяем настройки мотора
                            joints[i].motor = motor;
                            
                            // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Обязательно включаем useMotor
                            if (!joints[i].useMotor)
                            {
                                joints[i].useMotor = true;
                                Debug.LogWarning($"⚠️ [ID:{instance_id}] Обнаружен отключенный мотор {joints[i].name} во время выполнения! Принудительно включен.");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"❌ Ошибка при установке параметров мотора для сустава {i}: {e.Message}");
                        }
                    }
                    
                    // Проверяем, что агент делает какие-то движения - ВАЖНО ДЛЯ ОТЛАДКИ!
                    if (Time.frameCount % 100 == 0 && instance_id % 5 == 0) // Чаще проверяем - каждые 100 кадров вместо 500
                    {
                        bool anyMovement = false;
                        bool anyLegMovement = false;
                        float maxMotorOutput = 0f;
                        
                        for (int i = 0; i < actionCount; i++)
                        {
                            if (i < last_actions.Length && Mathf.Abs(last_actions[i]) > min_motor_velocity_threshold)
                            {
                                anyMovement = true;
                                maxMotorOutput = Mathf.Max(maxMotorOutput, Mathf.Abs(last_actions[i]));
                                
                                // Проверяем, движутся ли ноги
                                if (joints[i] != null && joint_types.ContainsKey(joints[i]) && joint_types[joints[i]] == "leg")
                                {
                                    anyLegMovement = true;
                                }
                            }
                        }
                        
                        // Добавляем глобальную статистику для лучшего понимания обучения
                        if (!anyMovement)
                        {
                            Debug.LogWarning($"❌ [ID:{instance_id}] Агент {name} НЕ ДВИГАЕТСЯ! Все скорости моторов ниже порога {min_motor_velocity_threshold}");
                        }
                        else if (!anyLegMovement)
                        {
                            Debug.LogWarning($"⚠️ [ID:{instance_id}] Агент {name} не двигает ногами! Макс. выход: {maxMotorOutput:F2}, Фитнес: {fitness:F2}, Время: {lifetime:F1}с");
                        }
                        else
                        {
                            // Важная информация о прогрессе обучения
                            Vector3 displacement = transform.position - initial_position;
                            float distanceMoved = displacement.magnitude;
                            Debug.Log($"✅ [ID:{instance_id}] Агент активен: Макс.мотор={maxMotorOutput:F2}, " +
                                      $"Смещение={distanceMoved:F2}м, Фитнес={fitness:F2}, Время={lifetime:F1}с, " + 
                                      $"Ноги={anyLegMovement}");
                        }
                    }
                    
                    // Рассчитываем награду за движение
                    CalculateMovementReward();
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка при обработке выходных данных нейросети: {e.Message}\n{e.StackTrace}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка в UseNeuralNetworkControl: {e.Message}\n{e.StackTrace}");
            }
        }
        
        private void CheckHeadHeight()
        {
            if (head == null || is_disabled) return;

            // Проверяем высоту головы
            float currentHeadHeight = head.position.y;
            
            if (currentHeadHeight < required_head_height)
            {
                // Сбрасываем счётчик стояния
                standing_time = 0f;
                // Бонусы не добавляем, но и не штрафуем - пусть учится
            }
            else
            {
                // Увеличиваем счётчик времени стояния
                standing_time += Time.fixedDeltaTime;
                
                // Даем бонус за хорошую высоту головы с усилением за длительное стояние
                float heightBonus = head_height_reward;
                
                // Увеличиваем бонус при длительном стоянии
                if (standing_time > min_standing_time)
                {
                    heightBonus *= Mathf.Min(2.0f, 1.0f + (standing_time - min_standing_time) * 0.1f);
                }
                
                fitness += heightBonus * Time.fixedDeltaTime;
            }
        }
        
        private void EnsureMotorsEnabled()
        {
            // Проверяем и принудительно включаем все моторы суставов
            foreach (HingeJoint joint in joints)
            {
                if (joint != null && !joint.useMotor)
                {
                    // Настраиваем параметры мотора
                    JointMotor motor = joint.motor;
                    motor.force = max_motor_force; // Устанавливаем силу мотора
                    joint.motor = motor;
                    
                    // Включаем мотор
                    joint.useMotor = true;
                    Debug.LogWarning($"⚠️ [ID:{instance_id}] Принудительно включен мотор для {joint.name}!");
                }
                
                // Проверяем, что сила мотора установлена правильно
                JointMotor currentMotor = joint.motor;
                if (currentMotor.force < max_motor_force * 0.9f)
                {
                    currentMotor.force = max_motor_force;
                    joint.motor = currentMotor;
                    Debug.LogWarning($"⚠️ [ID:{instance_id}] Скорректирована сила мотора для {joint.name}: {currentMotor.force} -> {max_motor_force}");
                }
            }
        }
        
        private void CheckTargetReached()
        {
            if (target_transform == null) return;
            
            // Расстояние до цели
            float distanceToTarget = Vector3.Distance(transform.position, target_transform.position);
            
            // Если достигли цели (подошли достаточно близко)
            if (distanceToTarget < 1.5f && !success_reported)
            {
                success_reported = true;
                
                // Добавляем большую награду за достижение цели
                fitness += target_reward;
                if (neural_network != null)
                {
                    neural_network.fitness = fitness;
                }
                
                // Сообщаем менеджеру симуляции об успехе - ВАЖНОЕ СОБЫТИЕ, ВСЕГДА ЛОГИРУЕМ
                if (simulation_manager != null)
                {
                    simulation_manager.ReportSuccess(this);
                    Debug.Log($"🎯 УСПЕХ! Агент {instance_id} достиг цели! Дистанция: {distanceToTarget:F2}м, Фитнес: {fitness:F2}, Время: {lifetime:F1}с");
                }
                
                // Отключаем агента после успеха
                DisableAgent("Достигнута цель");
            }
        }
        
        private void CheckFallen()
        {
            if (head == null) return;
            
            // Если голова слишком низко - агент упал
            if (head.position.y < 0.3f)
            {
                // Применяем штраф, но не слишком большой, чтобы был стимул продолжать двигаться
                fitness -= fall_penalty * 0.7f;
                
                // Даже если упал, не обнуляем прогресс полностью
                if (neural_network != null)
                {
                    neural_network.fitness = fitness;
                }
                
                // Важное событие - падение, но логируем только для небольшого числа агентов
                if (instance_id % 20 == 0)
                {
                    Debug.Log($"👇 Агент {instance_id} упал! Штраф: -{fall_penalty * 0.7f:F2}, Общий фитнес: {fitness:F2}");
                }
                
                // Отключаем агента только если он лежит совсем неподвижно
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null && rb.linearVelocity.magnitude < 0.1f)
                {
                    DisableAgent("Упал и не двигается");
                }
                // Иначе даём шанс продолжить движение, но не логируем каждый случай
            }
        }
        
        private void DisableAgent(string reason)
        {
            if (!is_disabled)
            {
                is_disabled = true;
                // Логируем отключение только для небольшого процента агентов
                if (instance_id % 10 == 0)
                {
                    // Добавляем важную статистику
                    Vector3 displacement = transform.position - initial_position;
                    float distanceX = Mathf.Abs(displacement.x);
                    float distanceZ = displacement.z;
                    
                    Debug.Log($"⛔ Агент {instance_id} отключен: {reason}, " +
                              $"Пройдено: {distanceZ:F2}м вперед, {distanceX:F2}м вбок, " +
                              $"Время жизни: {lifetime:F1}с, Фитнес: {fitness:F2}");
                }
            }
        }
        
        // Методы для SimulationManager
        public void SetFitness(float value) { fitness = value; }
        public NeuralNetwork GetBrain() { return neural_network; }
        public bool IsSuccessful() { return success_reported; }
        public float GetStartTime() { return generation_start_time; }
        public float GetLifetime() { return lifetime; }
        
        // Добавляем метод для получения начальной позиции
        public Vector3 GetInitialPosition() { return initial_position; }
        
        // Сброс агента для нового поколения
        public void ResetAgent()
        {
            try
            {
                fitness = 0f;
                is_disabled = false;
                success_reported = false;
                lifetime = 0f;
                standing_time = 0f; // Сбрасываем время стояния
                last_moved_time = Time.time; // Обновляем время последнего движения
                last_position = transform.position;
                initial_position = transform.position; // Обновляем начальную позицию
                
                // СБРОС ФИЗИКИ: Исправляем проблему с бесконечными значениями Rigidbody
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Сбрасываем скорости
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    
                    // Убедимся, что трансформ в нормальном состоянии
                    if (float.IsInfinity(transform.position.x) || float.IsInfinity(transform.position.y) || float.IsInfinity(transform.position.z))
                    {
                        Debug.LogError("❌ Обнаружена бесконечная позиция! Сбрасываем к нулю.");
                        transform.position = Vector3.zero;
                    }
                    
                    // Сбрасываем все накопленные силы
                    rb.ResetCenterOfMass();
                    rb.ResetInertiaTensor();
                    rb.Sleep(); // Временно отключаем для стабилизации
                    rb.WakeUp(); // Включаем обратно
                }
                
                // Сбрасываем и все дочерние Rigidbody
                foreach (Rigidbody childRb in GetComponentsInChildren<Rigidbody>())
                {
                    if (childRb != null && childRb != rb)
                    {
                        childRb.linearVelocity = Vector3.zero;
                        childRb.angularVelocity = Vector3.zero;
                        childRb.Sleep();
                        childRb.WakeUp();
                    }
                }
                
                // Проверяем, нужно ли обновить размер last_actions после изменения структуры сети
                if (last_actions == null || last_actions.Length != joints.Count)
                {
                    last_actions = new float[joints.Count];
                    Debug.Log($"🔄 ResetAgent: Переинициализирован массив last_actions с размером {joints.Count}");
                }
                
                // Сбрасываем историю действий
                for (int i = 0; i < last_actions.Length; i++)
                {
                    last_actions[i] = 0f;
                }
                
                // Убедимся, что joints не null
                if (joints == null)
                {
                    Debug.LogError("❌ Массив суставов оказался null при сбросе агента!");
                    return;
                }
                
                // Убедимся, что после сброса суставы в рабочем состоянии
                foreach (HingeJoint joint in joints)
                {
                    if (joint != null)
                    {
                        try
                        {
                            // Сбрасываем только скорость, силу оставляем неизменной
                            JointMotor motor = joint.motor;
                            motor.targetVelocity = 0f; // Сбрасываем скорость
                            joint.motor = motor;
                            // Не меняем force и useMotor
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"❌ Ошибка при сбросе мотора сустава: {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Общая ошибка в ResetAgent: {e.Message}\n{e.StackTrace}");
            }
        }
        
        // Метод для проверки и исправления аномалий в физике
        private void CheckForPhysicsAnomalies()
        {
            try
            {
                // Проверяем главный Rigidbody
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Проверка на бесконечные значения скорости
                    if (float.IsInfinity(rb.linearVelocity.x) || float.IsNaN(rb.linearVelocity.x) ||
                        float.IsInfinity(rb.linearVelocity.y) || float.IsNaN(rb.linearVelocity.y) ||
                        float.IsInfinity(rb.linearVelocity.z) || float.IsNaN(rb.linearVelocity.z) ||
                        rb.linearVelocity.magnitude > 20f) // Снижаем с 50 до 20
                    {
                        Debug.LogWarning($"⚠️ Обнаружена аномальная скорость: {rb.linearVelocity}. Сбрасываем физику.");
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.Sleep(); // Замораживаем физику на момент
                        rb.WakeUp(); // И снова активируем
                    }
                    
                    // Проверка на бесконечные значения в позиции
                    if (float.IsInfinity(transform.position.x) || float.IsInfinity(transform.position.y) || 
                        float.IsInfinity(transform.position.z) || float.IsNaN(transform.position.x) ||
                        float.IsNaN(transform.position.y) || float.IsNaN(transform.position.z))
                    {
                        Debug.LogError("❌ Обнаружена бесконечная позиция! Отключаем агента.");
                        DisableAgent("Обнаружена бесконечная позиция");
                        
                        // Пытаемся восстановить разумную позицию
                        if (spawn_point != null) 
                        {
                            transform.position = spawn_point.position;
                        }
                        else 
                        {
                            transform.position = Vector3.zero + new Vector3(0, 1, 0); // Небольшой подъем над землей
                        }
                        
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        return;
                    }
                    
                    // Проверка на телепортацию (очень большое смещение)
                    float distanceMoved = Vector3.Distance(transform.position, last_position);
                    if (distanceMoved > 3f) // Снижаем с 5 до 3
                    {
                        Debug.LogWarning($"⚠️ Обнаружено аномальное перемещение: {distanceMoved}м. Возвращаем на прежнюю позицию.");
                        transform.position = last_position;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }

                    // Жесткое ограничение на абсолютную позицию для предотвращения ухода далеко от начала координат
                    if (Mathf.Abs(transform.position.x) > 100f || 
                        Mathf.Abs(transform.position.y) > 100f || 
                        Mathf.Abs(transform.position.z) > 100f)
                    {
                        Debug.LogWarning($"⚠️ Агент слишком далеко от начала координат: {transform.position}. Возвращаем ближе.");
                        Vector3 clampedPosition = new Vector3(
                            Mathf.Clamp(transform.position.x, -100f, 100f),
                            Mathf.Clamp(transform.position.y, 0f, 100f), // Не ниже земли
                            Mathf.Clamp(transform.position.z, -100f, 100f)
                        );
                        transform.position = clampedPosition;
                        rb.linearVelocity = Vector3.zero;
                    }
                }
                
                // Проверяем все дочерние Rigidbody
                foreach (Rigidbody childRb in GetComponentsInChildren<Rigidbody>())
                {
                    if (childRb != null && childRb != rb)
                    {
                        // Проверка на бесконечные значения скорости
                        if (float.IsInfinity(childRb.linearVelocity.x) || float.IsNaN(childRb.linearVelocity.x) ||
                            float.IsInfinity(childRb.linearVelocity.y) || float.IsNaN(childRb.linearVelocity.y) ||
                            float.IsInfinity(childRb.linearVelocity.z) || float.IsNaN(childRb.linearVelocity.z) ||
                            childRb.linearVelocity.magnitude > 20f) // Снижаем с 50 до 20
                        {
                            Debug.LogWarning($"⚠️ Обнаружена аномальная скорость в дочернем объекте: {childRb.name}. Сбрасываем физику.");
                            childRb.linearVelocity = Vector3.zero;
                            childRb.angularVelocity = Vector3.zero;
                            childRb.Sleep(); // Замораживаем на момент
                            childRb.WakeUp(); // И снова активируем
                        }
                        
                        // Проверка на бесконечные значения в позиции
                        if (float.IsInfinity(childRb.transform.position.x) || float.IsInfinity(childRb.transform.position.y) || 
                            float.IsInfinity(childRb.transform.position.z) || float.IsNaN(childRb.transform.position.x) ||
                            float.IsNaN(childRb.transform.position.y) || float.IsNaN(childRb.transform.position.z))
                        {
                            Debug.LogError($"❌ Обнаружена бесконечная позиция в дочернем объекте: {childRb.name}. Отключаем агента.");
                            DisableAgent("Обнаружена бесконечная позиция в дочернем объекте");
                            
                            // Сбрасываем всю физику агента
                            ResetAgent();
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка в CheckForPhysicsAnomalies: {e.Message}");
            }
        }
        
        // Добавим метод для проверки физики в FixedUpdate
        private void FixedUpdatePhysicsChecks()
        {
            // Проверяем главный Rigidbody
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Принудительно ограничиваем скорость для предотвращения аномалий
                if (rb.linearVelocity.magnitude > 10f)
                {
                    rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, 10f);
                }
                
                // Ограничиваем угловую скорость
                if (rb.angularVelocity.magnitude > 5f)
                {
                    rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 5f);
                }
            }
            
            // Проверяем дочерние Rigidbody
            foreach (Rigidbody childRb in GetComponentsInChildren<Rigidbody>())
            {
                if (childRb != null && childRb != rb)
                {
                    // Ограничиваем линейную скорость
                    if (childRb.linearVelocity.magnitude > 10f)
                    {
                        childRb.linearVelocity = Vector3.ClampMagnitude(childRb.linearVelocity, 10f);
                    }
                    
                    // Ограничиваем угловую скорость
                    if (childRb.angularVelocity.magnitude > 5f)
                    {
                        childRb.angularVelocity = Vector3.ClampMagnitude(childRb.angularVelocity, 5f);
                    }
                }
            }
        }
        
        // Добавляем пропущенный метод CalculateMovementReward
        private void CalculateMovementReward()
        {
            if (neural_network == null || is_disabled) return;
            
            // Рассчитываем смещение с момента последнего вызова
            float distanceMoved = Vector3.Distance(transform.position, last_position);
            
            // Если есть движение
            if (distanceMoved > 0.001f)
            {
                // Рассчитываем только движение вперед
                Vector3 localDirection = transform.InverseTransformDirection(transform.position - last_position);
                float forwardMovement = localDirection.z;
                
                // Даем награду только за движение вперед
                if (forwardMovement > 0)
                {
                    // Награда за движение, пропорциональная расстоянию
                    float reward = forwardMovement * movement_reward;
                    
                    // Если цель указана, добавляем бонус за движение в направлении цели
                    if (target_transform != null)
                    {
                        Vector3 dirToTarget = target_transform.position - transform.position;
                        dirToTarget.y = 0; // Игнорируем высоту
                        dirToTarget.Normalize();
                        
                        Vector3 moveDir = transform.position - last_position;
                        moveDir.y = 0; // Игнорируем высоту
                        if (moveDir.magnitude > 0.001f)
                        {
                            moveDir.Normalize();
                            
                            // Скалярное произведение для определения схожести направлений
                            float dotProduct = Vector3.Dot(moveDir, dirToTarget);
                            
                            // Если движение в сторону цели (угол < 90°)
                            if (dotProduct > 0)
                            {
                                reward *= (1f + dotProduct * target_direction_multiplier);
                            }
                        }
                    }
                    
                    // Добавляем награду
                    fitness += reward;
                    if (neural_network != null)
                    {
                        neural_network.fitness = fitness;
                    }
                    
                    // Обновляем время последнего движения
                    last_moved_time = Time.time;
                }
            }
            else if (Time.time - last_moved_time > 2.0f)
            {
                // Штраф за отсутствие движения более 2 секунд
                fitness -= no_movement_penalty * Time.deltaTime;
                
                // Убедимся, что фитнес не станет отрицательным из-за штрафа за бездействие
                if (fitness < 0)
                {
                    fitness = 0;
                }
                
                if (neural_network != null)
                {
                    neural_network.fitness = fitness;
                }
            }
            
            // Обновляем последнюю позицию
            last_position = transform.position;
        }

#if UNITY_EDITOR
        // Оставляю только функцию для проверки моторов, без попыток их включить
        [UnityEngine.ContextMenu("ПРОВЕРКА: Статус моторов")]
        public void CheckMotorStatus()
        {
            if (joints == null || joints.Count == 0)
            {
                Debug.LogError("❌ Суставы не найдены! Выполните автоматический поиск сначала.");
                return;
            }
            
            int enabled = 0;
            int disabled = 0;
            List<string> disabledJoints = new List<string>();
            
            foreach (HingeJoint joint in joints)
            {
                if (joint == null) continue;
                
                if (joint.useMotor)
                {
                    enabled++;
                }
                else
                {
                    disabled++;
                    disabledJoints.Add(joint.name);
                }
            }
            
            if (disabled > 0)
            {
                Debug.LogWarning($"⚠️ НАЙДЕНЫ ОТКЛЮЧЕННЫЕ МОТОРЫ: {disabled} из {joints.Count}");
                Debug.LogWarning($"⚠️ Отключенные суставы: {string.Join(", ", disabledJoints)}");
                Debug.Log("🔧 Необходимо вручную включить моторы в инспекторе Unity.");
            }
            else
            {
                Debug.Log($"✅ ЗАЕБИСЬ! Все {enabled} моторов включены и готовы к работе!");
            }
        }
#endif

        // Метод для изменения множителя лимитов углов во время выполнения
        public void SetAngleLimitMultiplier(float multiplier)
        {
            angle_limit_multiplier = multiplier;
            
            // Применяем новый множитель ко всем суставам
            foreach (HingeJoint joint in joints)
            {
                if (joint != null)
                {
                    JointLimits limits = joint.limits;
                    
                    // Сначала восстанавливаем оригинальные значения (приблизительно)
                    float originalMin = limits.min / angle_limit_multiplier;
                    float originalMax = limits.max / angle_limit_multiplier;
                    
                    // Затем применяем новый множитель
                    limits.min = originalMin * multiplier;
                    limits.max = originalMax * multiplier;
                    
                    joint.limits = limits;
                }
            }
            
            Debug.Log($"🔄 Множитель лимитов углов изменен на {multiplier}. Применено к {joints.Count} суставам.");
        }

        // Получение текущего множителя лимитов углов
        public float GetAngleLimitMultiplier()
        {
            return angle_limit_multiplier;
        }

        [UnityEngine.ContextMenu("Увеличить лимиты углов в 1.5 раза")]
        public void IncreaseLimits()
        {
            SetAngleLimitMultiplier(1.5f);
        }

        [UnityEngine.ContextMenu("Уменьшить лимиты углов в 0.5 раза")]
        public void DecreaseLimits()
        {
            SetAngleLimitMultiplier(0.5f);
        }

        [UnityEngine.ContextMenu("Сбросить лимиты углов к стандартным")]
        public void ResetLimits()
        {
            SetAngleLimitMultiplier(1.0f);
         }

        // ДОБАВЛЯЕМ ГЛОБАЛЬНЫЙ МЕТОД СТАТИСТИКИ ДЛЯ ВЫЗОВА ИЗ SimulationManager
        public string GetAgentStats()
        {
            if (neural_network == null) return "Нет данных (нейросеть не создана)";
            
            Vector3 displacement = transform.position - initial_position;
            float totalDistance = displacement.magnitude;
            float forwardDistance = displacement.z;
            float sideDistance = Mathf.Abs(displacement.x);
            
            // Подсчитываем активность суставов
            int activeJoints = 0;
            int activeLegJoints = 0;
            
            if (last_actions != null)
            {
                for (int i = 0; i < last_actions.Length; i++)
                {
                    if (Mathf.Abs(last_actions[i]) > min_motor_velocity_threshold)
                    {
                        activeJoints++;
                        
                        if (i < joints.Count && joint_types.ContainsKey(joints[i]) && joint_types[joints[i]] == "leg")
                        {
                            activeLegJoints++;
                        }
                    }
                }
            }
            
            // Возвращаем компактную строку с информацией
            return $"[ID:{instance_id}] Фитнес:{fitness:F2} " +
                   $"Время:{lifetime:F1}с " +
                   $"Вперед:{forwardDistance:F2}м " +
                   $"Вбок:{sideDistance:F2}м " +
                   $"Активность:{activeJoints}/{joints.Count} " +
                   $"Ноги:{activeLegJoints}/{joints.Count(j => joint_types.ContainsKey(j) && joint_types[j] == "leg")}";
        }

        // Новый метод для сброса всех моторов
        private void ResetAllMotors()
        {
            if (joints == null || joints.Count == 0)
            {
                // Найдем суставы, если еще не нашли
                HingeJoint[] foundJoints = GetComponentsInChildren<HingeJoint>(true);
                if (foundJoints.Length > 0)
                {
                    joints = new List<HingeJoint>(foundJoints);
                }
                else
                {
                    Debug.LogError($"❌ Не найдены суставы (HingeJoint) для {name}!");
                    return;
                }
            }
            
            // Сбрасываем все моторы
            foreach (HingeJoint joint in joints)
            {
                if (joint == null) continue;
                
                JointMotor motor = joint.motor;
                motor.force = max_motor_force; // ВАЖНО: устанавливаем правильную силу мотора
                motor.targetVelocity = 0;
                joint.motor = motor;
                joint.useMotor = true;
            }
            
            Debug.Log($"✅ [ID:{instance_id}] Все моторы сброшены! Установлена сила = {max_motor_force}");
        }

        // Новый метод для детального дампа весов и выходных сигналов нейросети
        public void DumpNeuralDebugInfo(bool isBestAgent = false)
        {
            if (neural_network == null) return;
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"========== ДАМП НЕЙРОСЕТИ АГЕНТА {instance_id} ==========");
            sb.AppendLine($"Структура сети: [{string.Join("-", neural_network.layers)}]");
            sb.AppendLine($"Фитнес: {neural_network.fitness:F4}");
            sb.AppendLine($"Lучший агент?: {(isBestAgent ? "ДА" : "нет")}");
            
            // Сгенерируем тестовый вход и посмотрим выход
            if (last_actions != null)
            {
                sb.AppendLine("\nПОСЛЕДНИЕ ДЕЙСТВИЯ:");
                for (int i = 0; i < last_actions.Length && i < joints.Count; i++)
                {
                    string jointName = joints[i] != null ? joints[i].name : "unknown";
                    string jointType = joints[i] != null && joint_types.ContainsKey(joints[i]) ? joint_types[joints[i]] : "unknown";
                    sb.AppendLine($"  Сустав {i} ({jointName}, тип: {jointType}): action={last_actions[i]:F6}, velocity={last_actions[i] * max_velocity:F2}");
                    
                    // Проверка на ноль
                    if (Mathf.Approximately(last_actions[i], 0f))
                    {
                        sb.AppendLine($"    ⚠️ СИГНАЛ РАВЕН НУЛЮ!");
                    }
                }
            }
            
            // Проверим веса выходного слоя
            int outputLayerIndex = neural_network.weights.Length - 1;
            float totalAbsWeightSum = 0;
            int zeroWeights = 0;
            int totalWeights = 0;
            
            sb.AppendLine("\nСТАТИСТИКА ВЕСОВ ВЫХОДНОГО СЛОЯ:");
            for (int j = 0; j < neural_network.weights[outputLayerIndex].Length; j++)
            {
                float absSum = 0;
                for (int k = 0; k < neural_network.weights[outputLayerIndex][j].Length; k++)
                {
                    float w = neural_network.weights[outputLayerIndex][j][k];
                    absSum += Mathf.Abs(w);
                    totalAbsWeightSum += Mathf.Abs(w);
                    totalWeights++;
                    if (Mathf.Approximately(w, 0f)) zeroWeights++;
                }
                
                sb.AppendLine($"  Нейрон {j}: AbsSum={absSum:F4}, Bias={neural_network.biases[outputLayerIndex][j]:F4}");
                
                // Предупреждения
                if (absSum < 0.1f)
                {
                    sb.AppendLine($"    ⚠️ КРИТИЧЕСКИ МАЛЫЕ ВЕСА! AbsSum={absSum:F4}");
                }
                
                if (Mathf.Approximately(neural_network.biases[outputLayerIndex][j], 0f) && absSum < 0.5f)
                {
                    sb.AppendLine($"    ⚠️ НУЛЕВОЕ СМЕЩЕНИЕ И МАЛЫЕ ВЕСА! Высокий риск полного нуля на выходе.");
                }
            }
            
            sb.AppendLine($"\nСТАТИСТИКА ПО ВСЕМ ВЕСАМ ВЫХОДНОГО СЛОЯ:");
            sb.AppendLine($"  Общая сумма модулей весов: {totalAbsWeightSum:F4}");
            sb.AppendLine($"  Нулевых весов: {zeroWeights}/{totalWeights} ({(float)zeroWeights/totalWeights*100:F1}%)");
            
            if (totalAbsWeightSum < totalWeights * 0.1f)
            {
                sb.AppendLine($"  ⚠️ КРИТИЧЕСКИ МАЛЫЕ ВЕСА В ВЫХОДНОМ СЛОЕ! Среднее значение модуля весов: {totalAbsWeightSum/totalWeights:F4}");
            }
            
            // Информация о моторах
            sb.AppendLine("\nСТАТУС МОТОРОВ:");
            int enabledMotors = 0;
            int totalMotors = joints.Count;
            
            foreach (HingeJoint joint in joints)
            {
                if (joint != null && joint.useMotor)
                {
                    enabledMotors++;
                }
            }
            
            sb.AppendLine($"  Включено моторов: {enabledMotors}/{totalMotors}");
            
            // Некоторые ключевые переменные
            sb.AppendLine("\nКЛЮЧЕВЫЕ ПАРАМЕТРЫ:");
            sb.AppendLine($"  max_velocity: {max_velocity:F2}");
            sb.AppendLine($"  max_motor_force: {max_motor_force:F2}");
            sb.AppendLine($"  leg_motor_multiplier: {leg_motor_multiplier:F2}");
            sb.AppendLine($"  arm_motor_multiplier: {arm_motor_multiplier:F2}");
            sb.AppendLine($"  motor_update_interval: {motor_update_interval:F4}");
            sb.AppendLine("=========================================");
            
            Debug.Log(sb.ToString());
        }

        // Метод для принудительной инициализации весов нейросети для генерации ненулевых движений
        [UnityEngine.ContextMenu("ФОРСИРОВАТЬ НЕНУЛЕВЫЕ ВЕСА")]
        public void ForceNonZeroInitialization()
        {
            if (neural_network == null)
            {
                Debug.LogError("❌ Нейросеть не инициализирована!");
                return;
            }
            
            Debug.Log($"🔥 Принудительная инициализация ненулевых весов для агента {instance_id}...");
            
            // Инициализируем выходной слой
            int outputLayerIndex = neural_network.weights.Length - 1;
            
            // Для каждого нейрона в выходном слое
            for (int j = 0; j < neural_network.weights[outputLayerIndex].Length; j++)
            {
                // Устанавливаем небольшое ненулевое смещение
                neural_network.biases[outputLayerIndex][j] = UnityEngine.Random.Range(0.1f, 0.3f) * 
                    (UnityEngine.Random.value > 0.5f ? 1f : -1f);
                
                // Устанавливаем хотя бы некоторые веса ненулевыми
                for (int k = 0; k < neural_network.weights[outputLayerIndex][j].Length; k++)
                {
                    // С вероятностью 30% усиливаем вес
                    if (UnityEngine.Random.value < 0.3f)
                    {
                        neural_network.weights[outputLayerIndex][j][k] = UnityEngine.Random.Range(0.1f, 0.5f) * 
                            (UnityEngine.Random.value > 0.5f ? 1f : -1f);
                    }
                }
            }
            
            Debug.Log($"✅ Веса принудительно инициализированы! Агент {instance_id} должен начать двигаться.");
            
            // Дампим информацию о полученной сети
            DumpNeuralDebugInfo();
        }

        // Метод для получения подробной информации о нейросети в виде строки (для записи в файл)
        public string GetDetailedDebugInfo(bool isBestAgent = false)
        {
            if (neural_network == null) return "Нейросеть не инициализирована!\n";
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Структура сети: [{string.Join("-", neural_network.layers)}]");
            sb.AppendLine($"Фитнес: {neural_network.fitness:F4}");
            sb.AppendLine($"Лучший агент?: {(isBestAgent ? "ДА" : "нет")}");
            
            // Информация о последних действиях
            if (last_actions != null)
            {
                sb.AppendLine("\nПОСЛЕДНИЕ ДЕЙСТВИЯ:");
                for (int i = 0; i < last_actions.Length && i < joints.Count; i++)
                {
                    string jointName = joints[i] != null ? joints[i].name : "unknown";
                    string jointType = joints[i] != null && joint_types.ContainsKey(joints[i]) ? joint_types[joints[i]] : "unknown";
                    sb.AppendLine($"  Сустав {i} ({jointName}, тип: {jointType}): action={last_actions[i]:F6}, velocity={last_actions[i] * max_velocity:F2}");
                    
                    // Проверка на ноль
                    if (Mathf.Approximately(last_actions[i], 0f))
                    {
                        sb.AppendLine($"    СИГНАЛ РАВЕН НУЛЮ!");
                    }
                }
            }
            
            // Проверим веса выходного слоя
            int outputLayerIndex = neural_network.weights.Length - 1;
            float totalAbsWeightSum = 0;
            int zeroWeights = 0;
            int totalWeights = 0;
            
            sb.AppendLine("\nСТАТИСТИКА ВЕСОВ ВЫХОДНОГО СЛОЯ:");
            for (int j = 0; j < neural_network.weights[outputLayerIndex].Length; j++)
            {
                float absSum = 0;
                for (int k = 0; k < neural_network.weights[outputLayerIndex][j].Length; k++)
                {
                    float w = neural_network.weights[outputLayerIndex][j][k];
                    absSum += Mathf.Abs(w);
                    totalAbsWeightSum += Mathf.Abs(w);
                    totalWeights++;
                    if (Mathf.Approximately(w, 0f)) zeroWeights++;
                }
                
                sb.AppendLine($"  Нейрон {j}: AbsSum={absSum:F4}, Bias={neural_network.biases[outputLayerIndex][j]:F4}");
                
                // Предупреждения
                if (absSum < 0.1f)
                {
                    sb.AppendLine($"    КРИТИЧЕСКИ МАЛЫЕ ВЕСА! AbsSum={absSum:F4}");
                }
                
                if (Mathf.Approximately(neural_network.biases[outputLayerIndex][j], 0f) && absSum < 0.5f)
                {
                    sb.AppendLine($"    НУЛЕВОЕ СМЕЩЕНИЕ И МАЛЫЕ ВЕСА! Высокий риск полного нуля на выходе.");
                }
            }
            
            sb.AppendLine($"\nСТАТИСТИКА ПО ВСЕМ ВЕСАМ ВЫХОДНОГО СЛОЯ:");
            sb.AppendLine($"  Общая сумма модулей весов: {totalAbsWeightSum:F4}");
            sb.AppendLine($"  Нулевых весов: {zeroWeights}/{totalWeights} ({(float)zeroWeights/totalWeights*100:F1}%)");
            
            if (totalAbsWeightSum < totalWeights * 0.1f)
            {
                sb.AppendLine($"  КРИТИЧЕСКИ МАЛЫЕ ВЕСА В ВЫХОДНОМ СЛОЕ! Среднее значение модуля весов: {totalAbsWeightSum/totalWeights:F4}");
            }
            
            // Информация о моторах
            sb.AppendLine("\nСТАТУС МОТОРОВ:");
            int enabledMotors = 0;
            int totalMotors = joints.Count;
            
            foreach (HingeJoint joint in joints)
            {
                if (joint != null && joint.useMotor)
                {
                    enabledMotors++;
                }
            }
            
            sb.AppendLine($"  Включено моторов: {enabledMotors}/{totalMotors}");
            
            // Некоторые ключевые переменные
            sb.AppendLine("\nКЛЮЧЕВЫЕ ПАРАМЕТРЫ:");
            sb.AppendLine($"  max_velocity: {max_velocity:F2}");
            sb.AppendLine($"  max_motor_force: {max_motor_force:F2}");
            sb.AppendLine($"  leg_motor_multiplier: {leg_motor_multiplier:F2}");
            sb.AppendLine($"  arm_motor_multiplier: {arm_motor_multiplier:F2}");
            sb.AppendLine($"  motor_update_interval: {motor_update_interval:F4}");
            
            return sb.ToString();
        }
    }
} 