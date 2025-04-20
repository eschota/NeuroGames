using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Game.Scripts.GamePlay
{
    public class SimulationManager : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [SerializeField] private float generation_time = 20f;
        [SerializeField] private int population_size = 20;
        [SerializeField] private float mutation_rate = 0.2f; // Увеличиваем базовую скорость мутации
        [SerializeField] private float mutation_strength = 0.7f; // Увеличиваем силу мутации
        [SerializeField] private bool elite_selection = true;
        [SerializeField] private int elite_count = 2;
        [SerializeField] private float max_lifetime = 30f; // Максимальное время жизни агента в секундах
        
        // Улучшенные настройки генетического алгоритма
        [Header("Enhanced Genetic Settings")]
        [Tooltip("Начальный размер турнира для селекции")]
        [SerializeField] private int initial_tournament_size = 3;
        [Tooltip("Увеличивать размер турнира каждые N поколений")]
        [SerializeField] private int tournament_increase_generation = 10;
        [Tooltip("Максимальный размер турнира")]
        [SerializeField] private int max_tournament_size = 7;
        [Tooltip("Шанс радикальной мутации (полностью случайный вес)")]
        [SerializeField] private float radical_mutation_chance = 0.1f; // Увеличено до 10% для лучшего выхода из локальных минимумов
        [Tooltip("Увеличивать мутацию при застое прогресса")]
        [SerializeField] private bool adaptive_mutation = true;
        [Tooltip("Количество поколений застоя для увеличения мутации")]
        [SerializeField] private int stagnation_threshold = 3; // Уменьшено для быстрой реакции
        
        [Header("Population Settings")]
        [Tooltip("Путь к префабу агента в Resources")]
        [SerializeField] private string agent_prefab_path = "Agents/Human";
        [Tooltip("Точка спавна агентов")]
        public Transform spawn_point;
        [Tooltip("Радиус случайного разброса агентов от точки спавна")]
        public float spawn_radius = 5f;
        [Tooltip("Использовать случайный поворот при спавне")]
        public bool useRandomRotation = true;
        [Tooltip("Расстояние между агентами при построении в форме сетки (метры)")]
        [SerializeField] private float agent_spacing = 2.0f;
        [Tooltip("Количество агентов в одном ряду сетки")]
        [SerializeField] private int agents_per_row = 5;
        
        [Header("Agent Appearance")]
        [Tooltip("Подсвечивать цветом ТОП-5 агентов из предыдущего поколения")]
        public bool highlight_top_agents = true;
        [Tooltip("Имя материала/шейдера агента для изменения цвета")]
        public string agent_material_property = "_Color";
        [Tooltip("Максимальная сила (мощность) агента")]
        public float max_agent_force = 500000f;
        
        [Header("Time Control")]
        [Tooltip("Предустановленные значения скорости симуляции")]
        public float[] time_speed_presets = new float[] { 1f, 2f, 3f, 4f, 5f };
        [Tooltip("Текущий индекс скорости симуляции")]
        private int current_speed_index = 0;
        
        [Header("Simulation Settings")]
        [SerializeField] private float time_scale = 1f;
        [Tooltip("Сила мотора для новых агентов")]
        public float default_motor_force = 500f;
        
        [Header("Debug")]
        public bool draw_gizmos = true;
        
        [Header("Neural Network Configuration")]
        [SerializeField] private int[] neural_layers = new int[] { 10, 16, 8 };
        
        [Header("File Management")]
        [Tooltip("Имя файла для сохранения лучшей модели")]
        public string best_model_filename = "best_model.json";
        [Tooltip("Путь к директории для сохранения моделей")]
        public string models_directory = "Game/Game/snapshots";
        
        [Header("Debug Statistics")]
        [SerializeField] private bool show_detailed_logs = true;
        [SerializeField] private string stats_log_prefix = "🧠👣";
        
        [Header("Log Files")]
        [Tooltip("Сохранять важные логи в файл для последующего анализа")]
        [SerializeField] private bool save_logs_to_file = true;
        [Tooltip("Имя файла для логов лучшего агента")]
        [SerializeField] private string best_agent_log_filename = "best_agent_log.txt";
        
        // Путь к файлу логов
        private string logFilePath;
        
        // Приватные переменные
        private List<NeuroHuman> agents = new List<NeuroHuman>();
        private List<NeuralNetwork> population = new List<NeuralNetwork>();
        private int current_generation = 0;
        private float generation_timer = 0f;
        private bool simulation_running = false;
        private int successful_agents = 0;
        private float best_fitness_ever = 0f;
        private NeuralNetwork best_network;
        
        // Статистика обучения
        private List<int> success_history = new List<int>();
        private List<float> fitness_history = new List<float>();
        
        // Цвета для ТОП-5 агентов
        private Color[] top_agent_colors = new Color[] {
            Color.green,  // 1-й - зеленый
            Color.blue,   // 2-й - синий
            Color.red,    // 3-й - красный
            Color.yellow, // 4-й - желтый
            new Color(1f, 0.5f, 0.7f)  // 5-й - розовый
        };
        
        // ТОП-5 агентов предыдущего поколения
        private List<int> previous_top_agents = new List<int>();
        
        // Флаги состояния спавна
        private bool isCurrentlySpawning = false;
        private int nextAgentToSpawn = 0;
        private Vector3[] spawnPositions;
        
        // Anti-Magic Fitness Fix
        private const float MAGIC_FITNESS = 200.02f;
        private const float MAGIC_FITNESS_THRESHOLD = 0.1f;
        private bool isMagicFitnessDetected = false;
        
        // Переменная для минимального приемлемого времени жизни агента
        private float minimum_acceptable_lifetime = 2f;
        
        // Добавляем недостающие переменные для логирования и статистики
        private float last_status_time = 0f;
        
        // Start is called before the first frame update
        void Start()
        {
            Debug.Log("🧠 SimulationManager запущен! Готов к обучению нейросетей, епта!");
            
            // Проверяем корректность структуры слоёв нейросети
            if (neural_layers == null || neural_layers.Length < 2)
            {
                Debug.LogWarning("⚠️ Некорректная структура слоёв нейросети. Устанавливаем стандартную (10-16-8).");
                neural_layers = new int[] { 10, 16, 8 };
            }
            
            // Создаем директорию для моделей, если её нет
            string fullSnapshotPath = Path.Combine(Application.dataPath, models_directory);
            if (!Directory.Exists(fullSnapshotPath))
            {
                try
                {
                    Directory.CreateDirectory(fullSnapshotPath);
                    Debug.Log($"📁 Создана директория для моделей: {fullSnapshotPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Не удалось создать директорию для моделей: {e.Message}");
                }
            }
            else
            {
                Debug.Log($"📁 Директория для снапшотов существует: {fullSnapshotPath}");
            }
            
            // Важное изначальное значение
            network_loaded_at_start = false;
            best_fitness_ever = 0f;
            
            // *********************************************************
            // ВРЕМЕННОЕ РЕШЕНИЕ: ПОЛНОСТЬЮ ОТКЛЮЧАЕМ ЗАГРУЗКУ ИЗ ФАЙЛА
            // *********************************************************
            Debug.Log("⚠️ ВНИМАНИЕ! ЗАГРУЗКА НЕЙРОСЕТЕЙ ИЗ ФАЙЛА ВРЕМЕННО ОТКЛЮЧЕНА!");
            /*
            // Пытаемся загрузить лучшую модель из файла
            if (!LoadBestNetworkFromFile())
            {
                Debug.Log("⚠️ Не удалось загрузить сохраненную модель. Начинаем обучение с нуля.");
            }
            else
            {
                Debug.Log("✅ Успешно загружена лучшая модель из файла!");
            }
            */
            
            // Находим всех агентов на сцене
            FindAllAgents();
            
            // Инициализируем популяцию
            InitializePopulation();
            
            // ПРИНУДИТЕЛЬНО сбрасываем фитнес всех сетей для первого поколения
            foreach (var net in population)
            {
                if (net != null) net.fitness = 0f;
            }
            
            // Диагностика: проверим фитнесы после инициализации
            for (int i = 0; i < Mathf.Min(5, population.Count); i++)
            {
                Debug.Log($"🔍 ДИАГНОСТИКА Start(): фитнес сети #{i}: {population[i].fitness}");
            }
            
            // Установим временной масштаб симуляции
            Time.timeScale = time_scale;
            
            // Создадим начальную популяцию агентов
            SpawnAgents();
            
            // Автоматически запускаем симуляцию при старте
            Debug.Log("🚀 Автоматический запуск симуляции при старте!");
            StartSimulation();
            
            // Инициализируем путь к файлу логов
            if (save_logs_to_file)
            {
                string directoryPath = Path.Combine(Application.dataPath, models_directory);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                logFilePath = Path.Combine(directoryPath, best_agent_log_filename);
                
                // Очищаем файл логов при старте
                if (File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, "");
                }
                
                WriteToLogFile("=== НАЧАЛО СЕССИИ ЛОГОВ ===\n");
                WriteToLogFile($"Дата и время: {DateTime.Now}\n");
                WriteToLogFile($"Конфигурация сети: [{string.Join("-", neural_layers)}]\n");
                WriteToLogFile($"Параметры: Мутация={mutation_rate}, Сила={mutation_strength}, Радикальная={radical_mutation_chance}\n\n");
            }
        }
        
        void Update()
        {
            if (!simulation_running)
                return;
                
            // Обновляем таймер поколения
            generation_timer += Time.deltaTime;
            
            // Проверяем, не истекло ли время поколения
            if (generation_timer >= generation_time)
            {
                EndGeneration();
                StartNextGeneration();
            }
            
            // НОВАЯ ФИЧА: Проверяем, не упали ли все агенты (отрицательный фитнес)
            // Проверяем каждую секунду, а не каждый кадр, чтобы снизить нагрузку
            if (Time.frameCount % 30 == 0 && generation_timer > minimum_generation_time)
            {
                CheckForEarlyTermination();
            }
            
            // Устанавливаем временной масштаб симуляции (на случай, если он был изменен)
            Time.timeScale = time_scale;
            
            // Счетчик неактивных агентов
            int inactive_count = 0;
            
            // Обновляем состояние симуляции каждые 2 секунды
            if (Time.time - last_status_time > 2.0f)
            {
                // Обновляем время последнего обновления статуса
                last_status_time = Time.time;
                
                // Собираем статистику по всем агентам
                if (agents.Count > 0)
                {
                    LogGlobalStatistics();
                }
            }
            
            // Проверяем активность всех агентов
            foreach (var agent in agents)
            {
                // Подсчитываем неактивных агентов
                if (agent.IsSuccessful() || agent.GetLifetime() > max_lifetime)
                {
                    inactive_count++;
                }
            }
        }
        
        [Header("Early Termination")]
        [Tooltip("Минимальное время работы поколения до проверки на досрочное завершение (сек)")]
        [SerializeField] private float minimum_generation_time = 5f;
        [Tooltip("Завершать поколение досрочно, если все агенты упали (отрицательный фитнес)")]
        [SerializeField] private bool enable_early_termination = true;
        [Tooltip("Максимальное разрешённое количество упавших агентов (в %)")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float max_fallen_percentage = 0.8f;
        
        // Метод для проверки досрочного завершения поколения
        private void CheckForEarlyTermination()
        {
            if (!enable_early_termination) return;
            
            int fallenAgents = 0;
            int totalAgents = 0;
            
            float firstFitnessValue = float.MinValue;
            bool allSameFitness = true;
            
            // Подсчитываем количество упавших агентов и проверяем одинаковость фитнеса
            foreach (var agent in agents)
            {
                if (agent == null) continue;
                
                totalAgents++;
                float fitness = agent.GetFitness();
                
                // Запоминаем первое значение фитнеса для сравнения
                if (firstFitnessValue == float.MinValue)
                {
                    firstFitnessValue = fitness;
                }
                // Проверяем, отличается ли значение от первого
                else if (Math.Abs(fitness - firstFitnessValue) > 0.01f)
                {
                    allSameFitness = false;
                }
                
                // Считаем агента упавшим, если его фитнес отрицательный или очень низкий
                if (fitness < 0f)
                {
                    fallenAgents++;
                }
            }
            
            // Если агентов нет, ничего не делаем
            if (totalAgents == 0) return;
            
            // Рассчитываем процент упавших агентов
            float fallenPercentage = (float)fallenAgents / totalAgents;
            
            // Если достаточное количество агентов упали, завершаем поколение досрочно
            if (fallenPercentage >= max_fallen_percentage)
            {
                Debug.Log($"🚨 ДОСРОЧНОЕ ЗАВЕРШЕНИЕ! Упало {fallenAgents}/{totalAgents} агентов ({fallenPercentage:P2})");
                EndGeneration();
                StartNextGeneration();
            }
            
            // НОВАЯ ПРОВЕРКА: Если у всех агентов одинаковый фитнес, возможно что-то не так
            if (allSameFitness && totalAgents > 1 && generation_timer > 5.0f)
            {
                // Проверяем, что это похоже на тот самый проблемный фитнес 200.02
                if (Math.Abs(firstFitnessValue - 200.02f) < 0.1f)
                {
                    Debug.LogWarning($"⚠️ ВНИМАНИЕ! Фитнес у всех агентов одинаковый: {firstFitnessValue:F2}! Возможно, что-то сломано!");
                    
                    // Насильно сбрасываем фитнес всех нейросетей
                    Debug.Log("🔄 Принудительно обнуляем фитнес всех нейросетей и начинаем следующее поколение!");
                    foreach (var net in population)
                    {
                        if (net != null) net.fitness = 0f;
                    }
                    
                EndGeneration();
                    StartNextGeneration();
                }
            }
        }
        
        // Поиск всех агентов на сцене
        private void FindAllAgents()
        {
            agents.Clear();
            var foundAgents = FindObjectsOfType<NeuroHuman>();
            
            if (foundAgents != null && foundAgents.Length > 0)
            {
                agents.AddRange(foundAgents);
                Debug.Log($"🔍 Найдено {agents.Count} агентов на сцене.");
            }
            else
            {
                Debug.Log("⚠️ На сцене не найдено ни одного агента.");
            }
        }
        
        // Создание популяции агентов
        private void SpawnAgents()
        {
            // Если спавн уже в процессе, игнорируем запрос
            if (isCurrentlySpawning)
            {
                Debug.Log("⚠️ Спавн агентов уже в процессе! Дождитесь завершения.");
                return;
            }
            
            // Проверка наличия точки спавна
            if (spawn_point == null)
            {
                spawn_point = transform;
                Debug.LogWarning("⚠️ Не задана точка спавна агентов. Используем текущий объект.");
            }
            
            // Предварительно удаляем всех существующих агентов
            foreach (var agent in agents)
            {
                if (agent != null)
                {
                    Destroy(agent.gameObject);
                }
            }
            
            agents.Clear();
            
            // Вычисляем все позиции заранее
            Vector3 centerPosition = spawn_point.position;
            int total_rows = Mathf.CeilToInt((float)population_size / agents_per_row);
            
            spawnPositions = new Vector3[population_size];
            
            for (int i = 0; i < population_size; i++)
            {
                // Вычисляем позицию в сетке
                int row = i / agents_per_row;
                int col = i % agents_per_row;
                
                // Смещение от центральной точки
                Vector3 offset = new Vector3(
                    col * agent_spacing - (agents_per_row-1) * agent_spacing / 2f,
                    0f,
                    row * agent_spacing - (total_rows-1) * agent_spacing / 2f
                );
                
                spawnPositions[i] = centerPosition + offset;
            }
            
            // Инициируем последовательный спавн
            isCurrentlySpawning = true;
            nextAgentToSpawn = 0;
            
            // Регистрируем метод FixedUpdate, если он еще не зарегистрирован
            Debug.Log($"🏭 НАЧИНАЕМ последовательный спавн {population_size} агентов...");
        }
        
        // Обработка последовательного спавна в FixedUpdate
        void FixedUpdate()
        {
            // Если идет процесс спавна - создаем по одному агенту за раз
            if (isCurrentlySpawning && nextAgentToSpawn < population_size)
            {
                // Создаем одного агента
                NeuroHuman agent = SpawnSingleAgent(nextAgentToSpawn, spawnPositions[nextAgentToSpawn]);
                
                if (agent != null)
                {
                    agents.Add(agent);
                    Debug.Log($"🤖 Создан агент {nextAgentToSpawn + 1}/{population_size}");
                }
                
                // Увеличиваем счетчик для следующего агента
                nextAgentToSpawn++;
                
                // Если это был последний агент, завершаем процесс
                if (nextAgentToSpawn >= population_size)
                {
                    isCurrentlySpawning = false;
                    Debug.Log($"✅ Последовательный спавн завершен! Создано {agents.Count} агентов.");
                    
                    // После завершения спавна всех агентов, назначаем им нейросети
                    if (population.Count > 0)
                    {
                        AssignNetworksToAgents();
                    }
                }
            }
            
            // Если симуляция запущена - проверяем на досрочное завершение
            if (simulation_running && Time.time - last_early_check > 0.5f && generation_timer > minimum_generation_time)
            {
                CheckForEarlyTermination();
                last_early_check = Time.time;
            }
        }
        
        private float last_early_check = 0f;

        // Метод для инициализации начальной популяции нейросетей
        private void InitializePopulation() {
            Debug.Log($"🧬 Инициализация начальной популяции из {population_size} сетей");
            
            // Очищаем существующую популяцию
            population.Clear();
            
            // Создаем новую популяцию
            for (int i = 0; i < population_size; i++) {
                try {
                    // Создаем новую сеть напрямую, без использования GeneticAlgorithm класса
                    NeuralNetwork network = new NeuralNetwork(neural_layers);
                    
                    // ОЧЕНЬ ВАЖНО: всегда устанавливаем начальный фитнес в 0
                    network.fitness = 0f;
                    
                    // Добавляем в популяцию
                    population.Add(network);
                    
                } catch (Exception e) {
                    Debug.LogError($"❌ Ошибка при создании сети #{i}: {e.Message}");
                }
            }
            
            Debug.Log($"✅ Популяция инициализирована. Создано {population.Count} сетей с улучшенной структурой");
            
            // Устанавливаем генерацию в 0 при инициализации
            current_generation = 0;
            
            // Очищаем историю
            success_history.Clear();
            fitness_history.Clear();
        }

        // Проверка на магический фитнес для любого числа
        private bool IsMagicFitness(float fitness) {
            // Проверяем на слишком большие значения или очень специфические значения
            if (Math.Abs(fitness - MAGIC_FITNESS) < MAGIC_FITNESS_THRESHOLD || 
                fitness > 10000f || float.IsNaN(fitness) || float.IsInfinity(fitness)) {
                if (!isMagicFitnessDetected) {
                    Debug.LogError($"❌ ОБНАРУЖЕН ПОДОЗРИТЕЛЬНЫЙ ФИТНЕС {fitness}! Это значение будет сброшено.");
                    isMagicFitnessDetected = true;
                }
                return true;
            }
            return false;
        }
        
        // Назначаем сети агентам
        private void AssignNetworksToAgents()
        {
            try
            {
                if (agents.Count == 0)
                {
                    Debug.LogError("❌ Нет агентов для назначения нейросетей!");
                    return;
                }
                
                if (population == null || population.Count == 0)
                {
                    Debug.LogError("❌ Популяция пуста! Создаём новую популяцию...");
                    InitializePopulation();
                }
                
                // Проверка на минимальные допустимые размеры
                int minRequiredSize = Mathf.Min(agents.Count, population_size);
                
                if (population.Count < minRequiredSize)
                {
                    Debug.LogWarning($"⚠️ Размер популяции ({population.Count}) меньше, чем нужно ({minRequiredSize})! Дополняем случайными сетями.");
                    int toAdd = minRequiredSize - population.Count;
                    
                    for (int i = 0; i < toAdd; i++)
                    {
                        NeuralNetwork newNetwork = new NeuralNetwork(neural_layers);
                        population.Add(newNetwork);
                    }
                }
                
                // Проверка агентов и моторов
                for (int i = 0; i < Mathf.Min(agents.Count, population.Count); i++)
                {
                    if (agents[i] == null) continue;
                    
                    // Проверяем суставы моторы агента перед назначением сети
                    NeuroHuman agent = agents[i];
                    bool hasActiveMotors = false;
                    
                    // Пытаемся получить HingeJoint компоненты и проверить их состояние
                    HingeJoint[] joints = agent.GetComponentsInChildren<HingeJoint>();
                    if (joints != null && joints.Length > 0)
                    {
                        foreach (var joint in joints)
                        {
                            if (joint != null && joint.useMotor)
                            {
                                hasActiveMotors = true;
                                break;
                            }
                        }
                        
                        if (!hasActiveMotors)
                        {
                            Debug.LogError($"❌ У агента {agent.name} нет активных моторов! Включаем вручную.");
                            
                            // Пытаемся активировать моторы
                            foreach (var joint in joints)
                            {
                                if (joint != null)
                                {
                                    joint.useMotor = true;
                                    
                                    // Установим начальные параметры мотора
                                    JointMotor motor = joint.motor;
                                    motor.force = default_motor_force;
                                    joint.motor = motor;
                                }
                            }
                        }
                    }
                    
                    // Назначаем сеть и выводим диагностику
                    agent.SetNeuralNetwork(population[i]);
                    
                    if (i < 3 || i == agents.Count - 1) // Выводим данные только для первых 3 и последнего агента
                    {
                        Debug.Log($"🧠 Агент #{i} ({agent.name}): назначена сеть с фитнесом {population[i].fitness:F2}, " +
                                  $"моторы активны: {(hasActiveMotors ? "✅" : "❌")}");
                    }
                }
                
                Debug.Log($"✅ Назначены сети {Mathf.Min(agents.Count, population.Count)} агентам из {agents.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка при назначении сетей агентам: {e.Message}\n{e.StackTrace}");
            }
        }
        
        // Запуск симуляции
        public void StartSimulation()
        {
            if (isCurrentlySpawning)
            {
                Debug.LogWarning("⚠️ Спавн агентов в процессе! Дождитесь завершения перед запуском симуляции.");
                return;
            }
            
            if (agents.Count == 0)
            {
                FindAllAgents();
                
                // Если агентов все равно нет, запускаем спавн
                if (agents.Count == 0)
                {
                    SpawnAgents();
                    // Симуляция запустится автоматически после завершения спавна
                    StartCoroutine(StartSimulationAfterSpawn());
                    return;
                }
            }
            
            if (population.Count == 0)
            {
                InitializePopulation();
            }
            
            // Если популяция нейросетей меньше количества агентов, увеличиваем её
            if (population.Count < agents.Count)
            {
                Debug.Log($"⚠️ Популяция нейросетей ({population.Count}) меньше количества агентов ({agents.Count}). Увеличиваем размер популяции.");
                
                int initialCount = population.Count;
                for (int i = 0; i < agents.Count - initialCount; i++)
                {
                    GeneticAlgorithm genetic = new GeneticAlgorithm();
                    genetic.neural_layers = neural_layers;
                    NeuralNetwork network = genetic.CreateRandomNetwork();
                    network.fitness = 0f; // ЯВНО устанавливаем фитнес в 0
                    population.Add(network);
                }
                
                Debug.Log($"✅ Популяция нейросетей увеличена до {population.Count}.");
            }
            
            current_generation = 0;
            generation_timer = 0f; // Сбрасываем таймер поколения при запуске симуляции
            simulation_running = true;
            successful_agents = 0;
            
            // НЕ сбрасываем best_fitness_ever, чтобы сохранить прогресс между запусками
            // Но сбрасываем историю для нового запуска
            success_history.Clear();
            fitness_history.Clear();
            
            // ЯВНОЕ исправление фитнеса если нужно
            foreach (var net in population)
            {
                // Исправление проблемного фитнеса 200.02
                if (Math.Abs(net.fitness - 200.02f) < 0.01f)
                {
                    Debug.LogWarning($"⚠️ Принудительно сбрасываем проблемный фитнес 200.02 -> 0");
                    net.fitness = 0f;
                }
            }
            
            // Назначаем сети агентам
            AssignNetworksToAgents();
            
            // Логируем запуск симуляции
            Debug.Log($"▶️ Симуляция запущена! Поколение {current_generation}, временной масштаб {time_scale}x");
        }
        
        // Остановка симуляции
        public void StopSimulation()
        {
            simulation_running = false;
            Debug.Log("⛔ Симуляция остановлена.");
        }
        
        // Начало следующего поколения
        private void StartNextGeneration()
        {
            // Сохраняем идентификаторы топ-5 агентов для визуализации
            previous_top_agents.Clear();
            
            // Сортируем популяцию по фитнесу и сохраняем индексы лучших
            var sortedIndices = Enumerable.Range(0, population.Count)
                .OrderByDescending(i => population[i].fitness)
                .Take(5)
                .ToList();
            
            previous_top_agents.AddRange(sortedIndices);
            
            current_generation++;
            generation_timer = 0f;
            successful_agents = 0;
            
            // Если это не первое поколение, то применяем селекцию и генетические операторы
            if (current_generation > 1)
            {
                population = EvolvePoplation();
            }
            
            // Каждые 10 поколений делаем слепок текущего состояния
            if (current_generation % 10 == 0)
            {
                // Сохраняем лучшую сеть с префиксом поколения
                int roundedFitness = Mathf.RoundToInt(best_fitness_ever);
                string snapshotFilename = $"snapshot_gen{current_generation}_fit{roundedFitness}.json";
                SaveBestNetwork(snapshotFilename);
                Debug.Log($"📊 Создан слепок поколения {current_generation} с фитнесом {roundedFitness}");
            }
            
            // Назначаем сети агентам
            AssignNetworksToAgents();
            
            // Сбрасываем агентов в начальное состояние
            ResetAgents();
            
            // Выводим информацию о новом поколении
            Debug.Log($"{stats_log_prefix} 🚀 Поколение {current_generation} запущено! Агентов: {agents.Count}");
        }
        
        // Завершение текущего поколения
        private void EndGeneration()
        {
            // Проверяем, не все ли фитнесы одинаковые (возможно, проблема)
            bool allSameFitness = true;
            float firstFitness = float.MinValue;
            
            // Собираем фитнес-функции со всех агентов
            foreach (var agent in agents)
            {
                if (agent == null) continue;
                
                float fitness = agent.GetFitness();
                
                // Проверка на одинаковые фитнесы
                if (firstFitness == float.MinValue)
                {
                    firstFitness = fitness;
                }
                else if (Math.Abs(fitness - firstFitness) > 0.01f)
                {
                    allSameFitness = false;
                }
                
                // Логируем фитнес каждого агента для диагностики
                Debug.Log($"🔍 Агент {agent.name}: фитнес = {fitness:F2}");
                
                int agent_index = agents.IndexOf(agent);
                
                if (agent_index < population.Count)
                {
                    // Дополнительная проверка на подозрительный фитнес 200.02
                    if (Math.Abs(fitness - 200.02f) < 0.01f)
                    {
                        Debug.LogWarning($"⚠️ ОБНАРУЖЕН ПОДОЗРИТЕЛЬНЫЙ ФИТНЕС 200.02 у агента {agent.name}!");
                        
                        // Попробуем получить фитнес напрямую из компонента агента
                        try 
                        {
                            // Пробуем получить нормальный фитнес
                            float realFitness = agent.transform.position.magnitude; // Какой-то примитивный, но рабочий фитнес
                            Debug.Log($"🔧 Пробуем использовать альтернативный фитнес: {realFitness:F2}");
                            population[agent_index].fitness = realFitness;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"❌ Ошибка при расчете альтернативного фитнеса: {e.Message}");
                            // Если всё совсем плохо, просто используем случайный фитнес
                            population[agent_index].fitness = UnityEngine.Random.Range(0f, 100f);
                        }
                    }
                    else
                    {
                        // Если фитнес нормальный, используем его
                        population[agent_index].fitness = fitness;
                    }
                }
            }
            
            // Логируем предупреждение, если все фитнесы одинаковые
            if (allSameFitness && agents.Count > 1)
            {
                Debug.LogWarning($"⚠️ ВНИМАНИЕ! Все агенты имеют одинаковый фитнес {firstFitness:F2}! Возможно, это ошибка!");
                
                // Если все фитнесы равны 200.02, принудительно рандомизируем их
                if (Math.Abs(firstFitness - 200.02f) < 0.01f)
                {
                    Debug.LogWarning("⚠️ ОБНАРУЖЕН МАГИЧЕСКИЙ ФИТНЕС 200.02! Принудительно рандомизируем фитнесы!");
                    for (int i = 0; i < population.Count; i++)
                    {
                        if (population[i] != null)
                        {
                            population[i].fitness = UnityEngine.Random.Range(0f, 100f);
                        }
                    }
                }
            }
            
            // Сортируем популяцию по фитнесу
            population = population.OrderByDescending(n => n.fitness).ToList();
            
            // Диагностика: выводим фитнесы первых 5 сетей после сортировки
            for (int i = 0; i < Mathf.Min(5, population.Count); i++)
            {
                Debug.Log($"🔍 После сортировки, сеть #{i} имеет фитнес: {population[i].fitness:F2}");
            }
            
            // Проверяем, есть ли хотя бы один элемент в популяции
            if (population.Count > 0)
            {
                // Проверяем валидность структуры лучшей сети перед сохранением
                if (population[0] != null && population[0].layers != null && population[0].layers.Length >= 2)
                {
                    // Сохраняем лучшую сеть, если она лучше предыдущей
                    if (population[0].fitness > best_fitness_ever)
                    {
                        float oldBest = best_fitness_ever;
                        best_fitness_ever = population[0].fitness;
                        // Создаем глубокую копию с проверкой структуры
                        best_network = population[0].Clone();
                        
                        // Дополнительная проверка на валидность клонированной сети
                        if (best_network != null && best_network.layers != null && best_network.layers.Length >= 2)
                        {
                            Debug.Log($"{stats_log_prefix} 🏆 Новый рекорд! Фитнес улучшен с {oldBest:F2} до {best_fitness_ever:F2}");
                            
                            // Сохраняем только при новом рекорде
                            int roundedFitness = Mathf.RoundToInt(best_fitness_ever);
                            string bestFilename = $"best_gen{current_generation}_fit{roundedFitness}.json";
                            SaveBestNetwork(bestFilename);
                            Debug.Log($"💾 Сохранена лучшая сеть с фитнесом {best_fitness_ever:F2}");
                        }
                        else
                        {
                            Debug.LogError("❌ Ошибка при клонировании лучшей сети! Пересоздаем с нужной структурой.");
                            best_network = new NeuralNetwork(neural_layers);
                            best_network.fitness = best_fitness_ever;
                        }
                    }
                    
                    // Если у нас нет лучшей сети, создаем копию лучшей из текущего поколения
                    if (best_network == null)
                    {
                        if (population[0] != null && population[0].layers != null && population[0].layers.Length >= 2)
                        {
                            best_network = population[0].Clone();
                        }
                        else
                        {
                            // Если даже в текущем поколении нет валидной сети, создаем новую
                            Debug.LogWarning("⚠️ В популяции нет валидной сети! Создаем новую с нужной структурой.");
                            best_network = new NeuralNetwork(neural_layers);
                        }
                    }
                }
                else
                {
                    Debug.LogError("❌ Лучшая сеть в популяции имеет некорректную структуру! Создаем новую.");
                    best_network = new NeuralNetwork(neural_layers);
                }
            }
            else
            {
                Debug.LogError("❌ Популяция пуста! Невозможно найти лучшую сеть.");
                // Создаем новую сеть, если популяция пуста
                best_network = new NeuralNetwork(neural_layers);
            }
            
            // Сохраняем статистику
            float avgFitness = population.Count > 0 ? population.Average(n => n.fitness) : 0;
            fitness_history.Add(avgFitness);
            success_history.Add(successful_agents);
            
            // Выводим подробную статистику о поколении
            if (show_detailed_logs)
            {
                // Собираем топ-3 фитнес-значения
                string topFitnesses = "";
                for (int i = 0; i < Mathf.Min(3, population.Count); i++)
                {
                    topFitnesses += $"#{i+1}: {population[i].fitness:F2}; ";
                }
                
                // Собираем информацию о производительности
                float fitnessImprovement = fitness_history.Count > 1 
                    ? avgFitness - fitness_history[fitness_history.Count - 2] 
                    : 0;
                    
                string fitnessChange = fitnessImprovement > 0 
                    ? $"↗️ +{fitnessImprovement:F2}" 
                    : fitnessImprovement < 0 
                        ? $"↘️ {fitnessImprovement:F2}" 
                        : "→ 0.00";
                
                Debug.Log($"{stats_log_prefix} 📊 ИТОГИ ПОКОЛЕНИЯ {current_generation}" +
                    $"\n✅ Успешных агентов: {successful_agents}/{agents.Count} ({(float)successful_agents/agents.Count:P1})" +
                    $"\n📈 Средний фитнес: {avgFitness:F2} {fitnessChange}" + 
                    $"\n🥇 Топ фитнес: {topFitnesses}" +
                    $"\n🏆 Рекорд за всё время: {best_fitness_ever:F2}" +
                    $"\n⏱️ Длительность поколения: {generation_timer:F1} сек" +
                    $"\n💾 Путь сохранения: {Path.Combine(Application.dataPath, models_directory)}");
            }
            else
            {
                Debug.Log($"{stats_log_prefix} Поколение {current_generation} завершено. Успехов: {successful_agents}, Средний фитнес: {fitness_history.Last():F2}");
            }
        }
        
        // Эволюция популяции с помощью генетического алгоритма
        private List<NeuralNetwork> EvolvePoplation()
        {
            if (population == null || population.Count == 0)
            {
                Debug.LogError("❌ Невозможно эволюционировать пустую популяцию!");
                return new List<NeuralNetwork>();
            }
            
            // Находим лучшего агента в текущем поколении для логирования
            NeuroHuman bestAgent = null;
            float bestFitnessInCurrentPopulation = float.MinValue;
            
            foreach (var agent in agents)
            {
                if (agent == null || agent.GetBrain() == null) continue;
                
                float agentFitness = agent.GetBrain().fitness;
                if (agentFitness > bestFitnessInCurrentPopulation)
                {
                    bestFitnessInCurrentPopulation = agentFitness;
                    bestAgent = agent;
                }
            }
            
            // Логируем информацию о лучшем агенте перед эволюцией
            if (bestAgent != null)
            {
                Debug.Log($"🏆 АНАЛИЗ ЛУЧШЕГО АГЕНТА ПОКОЛЕНИЯ {current_generation}");
                
                // Сохраняем в файл вместо вывода в консоль
                if (save_logs_to_file)
                {
                    string logText = $"=== ЛУЧШИЙ АГЕНТ ПОКОЛЕНИЯ {current_generation} ===\n";
                    logText += $"ID: {bestAgent.GetInstanceID()}, Фитнес: {bestFitnessInCurrentPopulation:F2}\n";
                    logText += bestAgent.GetDetailedDebugInfo(true);
                    logText += $"=== КОНЕЦ ДАННЫХ ПОКОЛЕНИЯ {current_generation} ===\n\n";
                    
                    WriteToLogFile(logText);
                }
                
                bestAgent.DumpNeuralDebugInfo(true);
            }
            
            List<NeuralNetwork> newPopulation = new List<NeuralNetwork>();
            
            // Сортируем популяцию по фитнесу (лучшие сети в начале списка)
            population.Sort((a, b) => b.fitness.CompareTo(a.fitness));
            
            // Отображаем информацию о текущей популяции
            float avgFitness = 0;
            float worstFitness = float.MaxValue;
            float bestFitness = float.MinValue;
            
            // Добавляем детальное логирование для диагностики проблем с фитнесом
            Debug.Log($"🔍 ДЕТАЛЬНАЯ ДИАГНОСТИКА ПЕРЕД ЭВОЛЮЦИЕЙ:");
            Debug.Log($"📊 Размер популяции: {population.Count}");
            
            int zeroFitnessCount = 0;
            int negativeFitnessCount = 0;
            int positiveFitnessCount = 0;
            
            foreach (NeuralNetwork network in population)
            {
                // ИСПРАВЛЕНИЕ МАГИЧЕСКОГО ФИТНЕСА
                if (IsMagicFitness(network.fitness))
                {
                    Debug.LogWarning($"⚠️ Обнаружен магический фитнес {network.fitness}, сбрасываем в 0");
                    network.fitness = 0f;
                }
                
                // Подсчет разных типов фитнеса
                if (network.fitness == 0) zeroFitnessCount++;
                else if (network.fitness < 0) negativeFitnessCount++;
                else positiveFitnessCount++;
                
                avgFitness += network.fitness;
                if (network.fitness < worstFitness) worstFitness = network.fitness;
                if (network.fitness > bestFitness) bestFitness = network.fitness;
            }
            
            avgFitness /= population.Count;
            
            // Выводим подробную статистику
            Debug.Log($"📈 Статистика фитнеса: " +
                      $"\n • Лучший: {bestFitness:F2}" +
                      $"\n • Средний: {avgFitness:F2}" +
                      $"\n • Худший: {worstFitness:F2}" +
                      $"\n • Нулевой фитнес: {zeroFitnessCount} сетей" +
                      $"\n • Отрицательный фитнес: {negativeFitnessCount} сетей" +
                      $"\n • Положительный фитнес: {positiveFitnessCount} сетей");
            
            // Проверяем наличие проблемного фитнеса 200.02
            int count200 = 0;
            foreach (NeuralNetwork net in population)
            {
                if (IsMagicFitness(net.fitness))
                {
                    count200++;
                    // Принудительно сбрасываем опять
                    net.fitness = 0f;
                }
            }
            
            if (count200 > 0)
            {
                Debug.LogError($"❌❌❌ КРИТИЧЕСКАЯ ОШИБКА: Обнаружено {count200} сетей с магическим фитнесом 200.02!");
            }
            
            // НОВОЕ: если все сети имеют фитнес <= 0, полностью перезапускаем пару лучших
            bool allBad = true;
            foreach (var network in population)
            {
                if (network.fitness > 0.1f) {
                    allBad = false;
                    break;
                }
            }
            
            if (allBad && current_generation > 5)
            {
                Debug.LogWarning("⚠️ ВСЕ СЕТИ ИМЕЮТ НУЛЕВОЙ ИЛИ ОТРИЦАТЕЛЬНЫЙ ФИТНЕС! Добавляем полностью новые случайные сети.");
                
                // Добавляем 4 полностью новые случайные сети
                for (int i = 0; i < 4; i++)
                {
                    GeneticAlgorithm genetic = new GeneticAlgorithm();
                    genetic.neural_layers = neural_layers;
                    NeuralNetwork fresh = genetic.CreateRandomNetwork();
                    fresh.fitness = 0.01f; // Небольшой положительный фитнес чтобы гарантировать выбор
                    
                    // Заменяем худшие сети в популяции
                    if (population.Count > 4 + i)
                    {
                        population[population.Count - 1 - i] = fresh;
                    }
                    else if (population.Count > 0)
                    {
                        population[0] = fresh;
                    }
                }
                
                // Пересортируем
                population.Sort((a, b) => b.fitness.CompareTo(a.fitness));
            }
            
            // Проверяем на застой эволюции для адаптивной мутации
            bool isStagnating = false;
            if (adaptive_mutation && fitness_history.Count >= stagnation_threshold)
            {
                // Берем предыдущие N лучших фитнесов
                float sumLastN = 0;
                for (int i = 1; i <= stagnation_threshold; i++)
                {
                    if (fitness_history.Count >= i)
                    {
                        sumLastN += fitness_history[fitness_history.Count - i];
                    }
                }
                float avgLastN = sumLastN / stagnation_threshold;
                
                // Если текущий лучший фитнес не сильно отличается от среднего за последние N поколений
                if (population[0].fitness < avgLastN * 1.05f)
                {
                    isStagnating = true;
                    Debug.LogWarning($"🔄 Обнаружен застой эволюции! Увеличиваем силу мутации временно!");
                    
                    // Временно увеличиваем параметры мутации
                    mutation_rate *= 2f;
                    mutation_strength *= 2f;
                    radical_mutation_chance *= 3f;
                }
            }
            
            // НОВОЕ: Более детальная диагностика
            string fitnessDetails = "";
            for (int i = 0; i < Math.Min(5, population.Count); i++)
            {
                fitnessDetails += $"\n   #{i+1}: {population[i].fitness:F2}";
            }
            
            Debug.Log($"🧬 Эволюция поколения {current_generation}:\n" +
                $"   Лучший фитнес: {bestFitness:F2}\n" +
                $"   Средний фитнес: {avgFitness:F2}\n" +
                $"   Худший фитнес: {worstFitness:F2}\n" +
                $"   Режим мутации: {(isStagnating ? "УСИЛЕННЫЙ 🔥" : "Стандартный")}\n" +
                $"   Мутация: {mutation_rate:F2} / Сила: {mutation_strength:F2} / Радикальная: {radical_mutation_chance:F3}\n" +
                $"   Топ-5 сетей:" + fitnessDetails);
            
            // Сохраняем элитные особи (лучшие сети) без изменений, если включено
            if (elite_selection && elite_count > 0)
            {
                int elitesToKeep = Math.Min(elite_count, population.Count);
                for (int i = 0; i < elitesToKeep; i++)
                {
                    // Пропускаем сети с магическим фитнесом
                    if (IsMagicFitness(population[i].fitness))
                    {
                        Debug.LogWarning($"⚠️ Элитная особь #{i} имеет магический фитнес! Пропускаем.");
                        continue;
                    }
                    
                    NeuralNetwork elite = population[i].Clone();
                    elite.fitness = 0; // Сбрасываем фитнес для нового поколения
                    newPopulation.Add(elite);
                    Debug.Log($"🏆 Элитная особь {i+1} сохранена: предыдущий фитнес = {population[i].fitness:F2}");
                }
            }
            
            // Обновляем размер турнира
            int adaptive_tournament_size = initial_tournament_size;
            if (tournament_increase_generation > 0)
            {
                adaptive_tournament_size += current_generation / tournament_increase_generation;
                adaptive_tournament_size = Mathf.Min(adaptive_tournament_size, max_tournament_size);
            }
            tournament_selection_size = adaptive_tournament_size;
            
            // НОВОЕ: более разнообразные способы создания детей
            while (newPopulation.Count < population_size)
            {
                NeuralNetwork child;
                
                // 70% шанс на обычный кроссовер
                if (UnityEngine.Random.value < 0.7f)
                {
                    // Стандартный кроссовер
                    NeuralNetwork parent1 = TournamentSelection(tournament_selection_size);
                    NeuralNetwork parent2 = TournamentSelection(tournament_selection_size);
                    
                    // Предотвращаем скрещивание сети с самой собой
                    int attempt = 0;
                    while (parent1 == parent2 && attempt < 5)
                    {
                        parent2 = TournamentSelection(tournament_selection_size);
                        attempt++;
                    }
                    
                    // Создаём потомка через кроссовер
                    child = NeuralNetwork.Crossover(parent1, parent2);
                }
                else if (UnityEngine.Random.value < 0.5f)
                {
                    // 15% шанс на клонирование с сильной мутацией
                    NeuralNetwork parent = TournamentSelection(tournament_selection_size);
                    child = parent.Clone();
                    // Сильная мутация для разнообразия
                    child.Mutate(mutation_rate * 2f, mutation_strength * 2f);
                }
                else
                {
                    // 15% шанс на полностью новую сеть
                    child = new NeuralNetwork(neural_layers);
                }
                
                // Стандартная мутация для всех (кроме полностью новых)
                if (!(UnityEngine.Random.value < 0.15f))
                {
                    child.Mutate(mutation_rate, mutation_strength);
                }
                
                // Шанс радикальной мутации (полностью случайные веса для некоторых связей)
                if (UnityEngine.Random.value < radical_mutation_chance)
                {
                    // Используем встроенный метод мутации с повышенной силой вместо ручного перебора весов
                    child.Mutate(radical_mutation_chance * 3f, mutation_strength * 3f);
                    Debug.Log("💥 Применена радикальная мутация к потомку!");
                }
                
                // Проверяем на магический фитнес
                if (IsMagicFitness(child.fitness))
                {
                    child.fitness = 0f;
                }
                else
                {
                    // Сбрасываем фитнес ребёнка всегда
                    child.fitness = 0;
                }
                
                // Добавляем в новую популяцию
                newPopulation.Add(child);
            }
            
            // Если мы временно увеличивали силу мутации из-за застоя, возвращаем нормальные значения
            if (isStagnating)
            {
                mutation_rate /= 2f;
                mutation_strength /= 2f;
                radical_mutation_chance /= 3f;
                Debug.Log("🔄 Параметры мутации возвращены к нормальным значениям");
            }
            
            // Диверсификация популяции - если популяция стала слишком однородной
            float diversity = CalculatePopulationDiversity();
            if (diversity < 0.1f) // Если разнообразие меньше 10%
            {
                Debug.LogWarning($"⚠️ Низкое разнообразие популяции ({diversity:P2})! Добавляем случайные сети.");
                
                // Заменяем до 20% худших сетей случайными
                int replacementCount = Mathf.CeilToInt(population_size * 0.2f);
                for (int i = 0; i < replacementCount && newPopulation.Count > elite_count + 2; i++)
                {
                    // Удаляем худшую сеть (но не трогаем элитные!)
                    newPopulation.RemoveAt(newPopulation.Count - 1);
                    
                    // Создаём новую случайную сеть
                    int[] layers = population[0].layers; // Берём структуру из существующей сети
                    NeuralNetwork randomNet = new NeuralNetwork(layers);
                    randomNet.Randomize(); // Случайные веса
                    
                    newPopulation.Add(randomNet);
                }
                
                Debug.Log($"🔀 Добавлено {replacementCount} случайных сетей для увеличения разнообразия");
            }
            
            // Повторная проверка на магический фитнес у всех сетей
            foreach (var net in newPopulation)
            {
                if (IsMagicFitness(net.fitness))
                {
                    net.fitness = 0f;
                }
            }
            
            return newPopulation;
        }
        
        // Расчет разнообразия популяции
        private float CalculatePopulationDiversity()
        {
            if (population.Count <= 1) return 0;
            
            float totalDifference = 0;
            int comparisons = 0;
            
            // Сравниваем каждую сеть с некоторыми другими (не со всеми для оптимизации)
            for (int i = 0; i < population.Count; i++)
            {
                for (int j = i + 1; j < Mathf.Min(i + 5, population.Count); j++)
                {
                    totalDifference += CalculateNetworkDifference(population[i], population[j]);
                    comparisons++;
                }
            }
            
            if (comparisons == 0) return 0;
            return totalDifference / comparisons;
        }
        
        // Расчет различия между двумя нейросетями
        private float CalculateNetworkDifference(NeuralNetwork net1, NeuralNetwork net2)
        {
            if (net1 == null || net2 == null) return 1.0f; // Максимальное различие
            
            // Простая метрика различия - доля различающихся весов
            int totalWeights = 0;
            int differentWeights = 0;
            float differenceThreshold = 0.1f; // Порог, при котором веса считаются различными
            
            // Проходим по всем слоям и проверяем веса (трехмерный массив [слой][нейрон][вес])
            for (int i = 0; i < net1.weights.Length && i < net2.weights.Length; i++)
            {
                for (int j = 0; j < net1.weights[i].Length && j < net2.weights[i].Length; j++)
                {
                    for (int k = 0; k < net1.weights[i][j].Length && k < net2.weights[i][j].Length; k++)
                    {
                        totalWeights++;
                        if (Mathf.Abs(net1.weights[i][j][k] - net2.weights[i][j][k]) > differenceThreshold)
                        {
                            differentWeights++;
                        }
                    }
                }
            }
            
            if (totalWeights == 0) return 1.0f;
            return (float)differentWeights / totalWeights;
        }
        
        // Турнирная селекция
        private int tournament_selection_size = 3; // Переменная для хранения размера турнира
        
        private NeuralNetwork TournamentSelection(int tournament_size = -1)
        {
            // Используем переопределенный размер или базовый
            if (tournament_size <= 0)
            {
                tournament_size = tournament_selection_size;
            }
            
            NeuralNetwork best = null;
            float best_fitness = float.MinValue;
            
            // Выбираем случайную подгруппу сетей и находим лучшую
            for (int i = 0; i < tournament_size; i++)
            {
                int random_index = UnityEngine.Random.Range(0, population.Count);
                if (best == null || population[random_index].fitness > best_fitness)
                {
                    best = population[random_index];
                    best_fitness = best.fitness;
                }
            }
            
            return best;
        }
        
        // Сброс агентов в начальное состояние - полное пересоздание
        private void ResetAgents()
        {
            // Если идет процесс спавна, не начинаем новый
            if (isCurrentlySpawning)
            {
                Debug.LogWarning("⚠️ Спавн агентов в процессе! Нельзя сбросить агентов.");
                return;
            }
            
            Vector3 spawnPosition = spawn_point != null ? spawn_point.position : Vector3.zero;
            
            Debug.Log($"🔄 Полный сброс агентов для поколения {current_generation}. Уничтожаем и создаем заново...");
            
            // Перед началом обязательно проверяем, доступен ли префаб
            GameObject prefab = Resources.Load<GameObject>(agent_prefab_path);
            if (prefab == null)
            {
                Debug.LogError($"❌ Критическая ошибка! Не удалось загрузить префаб по пути 'Resources/{agent_prefab_path}'");
                StopSimulation();
                return;
            }
            
            // Уничтожаем всех текущих агентов
            var allAgentsInScene = FindObjectsOfType<NeuroHuman>();
            Debug.Log($"⚠️ Найдено {allAgentsInScene.Length} агентов для уничтожения");
            
            foreach (var agent in allAgentsInScene)
            {
                if (agent != null)
                {
                    Destroy(agent.gameObject);
                }
            }
            
            // Запускаем спавн новых агентов
            agents.Clear();
            SpawnAgents();
            
            // Подсвечиваем ТОП агентов после завершения спавна
            StartCoroutine(HighlightTopAgentsAfterSpawn());
        }
        
        // Корутина для подсветки топ агентов после завершения спавна
        private IEnumerator HighlightTopAgentsAfterSpawn()
        {
            while (isCurrentlySpawning)
            {
                yield return new WaitForFixedUpdate();
            }
            
            // Когда спавн завершен, подсвечиваем топ агентов
            HighlightTopAgents();
        }
        
        // Создание одного агента
        private NeuroHuman SpawnSingleAgent(int index, Vector3 centerPosition)
        {
            // Загружаем префаб из Resources
            GameObject prefab = Resources.Load<GameObject>(agent_prefab_path);
            
            // Проверка наличия префаба
            if (prefab == null)
            {
                Debug.LogError($"❌ Не удалось загрузить префаб агента по пути 'Resources/{agent_prefab_path}'!");
                return null;
            }
            
            // Вместо случайного расположения, организуем строй агентов в сетку
            int agentsPerRow = agents_per_row; // Используем настраиваемый параметр
            
            // Вычисляем строку и столбец для текущего индекса
            int row = index / agentsPerRow;
            int col = index % agentsPerRow;
            
            // Расстояние между агентами (настраиваемый параметр)
            float spacing = agent_spacing;
            
            // Вычисляем смещение от центра, чтобы строй был центрирован
            float totalWidth = (agentsPerRow - 1) * spacing;
            float startX = -totalWidth / 2; // Смещение для центрирования по X
            
            // Вычисляем позицию в строю
            Vector3 formationOffset = new Vector3(
                startX + col * spacing,  // X позиция (слева направо)
                0,                       // Y позиция (высота)
                row * spacing            // Z позиция (спереди назад)
            );
            
            // Итоговая позиция = центр + смещение в строю
            Vector3 spawnPosition = centerPosition + formationOffset;
            
            // Фиксированный поворот для всех агентов - лицом в одном направлении
            Quaternion spawnRotation = Quaternion.Euler(0, 0, 0); // Смотрят вперед по Z
            
            // Создаем агента
            GameObject agentObject = Instantiate(prefab, spawnPosition, spawnRotation);
            agentObject.name = $"Agent_{index:D3}";
            
            // Получаем компонент NeuroHuman
            NeuroHuman neuroHuman = agentObject.GetComponent<NeuroHuman>();
            if (neuroHuman == null)
            {
                Debug.LogWarning($"⚠️ У префаба {prefab.name} отсутствует компонент NeuroHuman!");
            }
            
            return neuroHuman;
        }
        
        // Публичные методы, используемые извне
        
        // Получить слои нейросети
        public int[] GetNeuralLayers()
        {
            return neural_layers;
        }
        
        // Обновить структуру нейросети из NeuroHuman
        public void UpdateNetworkStructure(int[] newLayers)
        {
            if (newLayers == null || newLayers.Length < 2)
            {
                Debug.LogError("❌ Некорректная структура нейросети! Нужно как минимум входной и выходной слой!");
                return;
            }

            neural_layers = new int[newLayers.Length];
            for (int i = 0; i < newLayers.Length; i++)
            {
                neural_layers[i] = newLayers[i];
            }
            
            Debug.Log($"✅ Структура нейросети обновлена: {string.Join(", ", neural_layers)}");
            
            // Если симуляция уже запущена, пересоздаем популяцию с новой структурой
            if (simulation_running)
            {
                Debug.Log("⚠️ Симуляция уже запущена, пересоздаем популяцию с новой структурой...");
                InitializePopulation();
                ResetAgents();
            }
        }
        
        // Отчет об успехе от агента
        public void ReportSuccess(NeuroHuman agent)
        {
            if (agent == null) return;
            
            try 
            {
                successful_agents++;
                
                // Получаем время жизни агента
                float lifetime = Time.time - agent.GetStartTime();
                
                // Только если агент прожил достаточно долго, считаем это настоящим успехом
                if (lifetime < minimum_acceptable_lifetime) {
                    Debug.LogWarning($"⚠️ Агент {agent.name} слишком быстро отчитался об успехе ({lifetime:F2} сек). Игнорируем.");
                    return;
                }
                
                // Увеличиваем фитнес агента, который достиг цели
                float currentFitness = agent.GetFitness();
                currentFitness += 100f; // Бонус за успех
                
                // Дополнительный бонус за скорость достижения цели
                float speedBonus = Mathf.Max(0, 200f - lifetime * 5f);
                currentFitness += speedBonus;
                
                agent.SetFitness(currentFitness);
                
                // Обновляем фитнес в нейросети агента
                NeuralNetwork agentBrain = agent.GetBrain();
                if (agentBrain != null) 
                {
                    agentBrain.fitness = currentFitness;
                    
                    // Находим индекс агента в списке
                    int agentIndex = agents.IndexOf(agent);
                    if (agentIndex >= 0 && agentIndex < population.Count)
                    {
                        population[agentIndex] = agentBrain; // Обновляем нейросеть в популяции
                    }
                }
                
                // Выводим подробную информацию об успешном агенте
                Debug.Log($"🏆 УСПЕХ #{successful_agents} В ПОКОЛЕНИИ {current_generation}!" +
                    $"\n👤 Агент: {agent.name}" +
                    $"\n⭐ Итоговый фитнес: {currentFitness:F2}" + 
                    $"\n🌟 Бонус за скорость: {speedBonus:F2}" +
                    $"\n⏱️ Время жизни: {lifetime:F2} сек" +
                    $"\n🧠 Структура сети: [{string.Join(", ", GetNeuralLayers())}]");
                
                // Если это первый успех в текущем поколении, сохраняем лучшую сеть
                if (successful_agents == 1)
                {
                    // Добавляем дополнительную проверку перед клонированием сети
                    if (agentBrain != null && agentBrain.layers != null && agentBrain.layers.Length >= 2)
                    {
                        best_network = agentBrain.Clone();
                        best_fitness_ever = currentFitness;
                        
                        // Сохраняем лучшую сеть автоматически
                        SaveBestNetwork($"best_network_gen{current_generation}.json");
                        
                        Debug.Log($"💾 Лучшая сеть автоматически сохранена (поколение {current_generation})");
                    }
                    else
                    {
                        Debug.LogError($"❌ Невозможно сохранить сеть от агента {agent.name}: некорректная структура нейросети!");
                    }
                }
                
                // Если успешных агентов больше половины, переходим к следующему поколению
                if (successful_agents >= agents.Count * 0.5f)
                {
                    Debug.Log($"🚀 Успешно более половины агентов ({successful_agents}/{agents.Count})! Досрочно завершаем поколение.");
                    EndGeneration();
                    StartNextGeneration();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка в ReportSuccess: {e.Message}\n{e.StackTrace}");
            }
        }
        
        // Поддержка старого типа для совместимости
        public void ReportSuccess(Neuro agent)
        {
            // Этот метод существует только для поддержки существующего кода
            Debug.Log("👴 Вызван устаревший метод ReportSuccess для Neuro");
            successful_agents++;
        }
        
        // Получить текущее поколение
        public int GetCurrentGeneration()
        {
            return current_generation;
        }
        
        // Получить лучшую сеть
        public NeuralNetwork GetBestNetwork()
        {
            return best_network?.Clone();
        }
        
        // Получить историю успехов
        public List<int> GetSuccessHistory()
        {
            return new List<int>(success_history);
        }
        
        // Получить историю фитнеса
        public List<float> GetFitnessHistory()
        {
            return new List<float>(fitness_history);
        }
        
        // Получить активных агентов для совместимости с TrainingUI
        public List<GameObject> GetActiveAgents()
        {
            List<GameObject> result = new List<GameObject>();
            foreach (var agent in agents)
            {
                if (agent != null)
                {
                    result.Add(agent.gameObject);
                }
            }
            return result;
        }
        
        // Сохранение лучшей сети в файл
        public void SaveBestNetwork(string filename = null)
        {
            if (best_network == null)
            {
                Debug.LogWarning("⚠️ Нет лучшей сети для сохранения!");
                return;
            }

            // Добавляем дополнительную проверку структуры сети перед сохранением
            if (best_network.layers == null || best_network.layers.Length < 2)
            {
                Debug.LogError($"⚠️ Лучшая сеть имеет некорректную структуру слоёв! Layers: {(best_network.layers == null ? "null" : best_network.layers.Length.ToString())}");
                
                // Пытаемся восстановить структуру
                if (neural_layers != null && neural_layers.Length >= 2)
                {
                    Debug.Log("🛠️ Пытаемся восстановить структуру сети из настроек менеджера...");
                    best_network = new NeuralNetwork(neural_layers);
                    best_network.Randomize();
                    best_network.fitness = 0;
                }
                else
                {
                    Debug.LogError("❌ Невозможно восстановить структуру сети! Отмена сохранения.");
                    return;
                }
            }

            if (string.IsNullOrEmpty(filename))
            {
                filename = best_model_filename;
            }

            try
            {
                // Проверяем и корректируем путь для сохранения
                string directoryPath = Path.Combine(Application.dataPath, models_directory);
                
                Debug.Log($"🔍 Попытка сохранить нейросеть в директорию: {directoryPath}");
                
                // Создаем все директории в пути, если они не существуют
                if (!Directory.Exists(directoryPath))
                {
                    Debug.Log($"📁 Создаем директорию: {directoryPath}");
                    Directory.CreateDirectory(directoryPath);
                }
                
                // Формируем полный путь к файлу
                string filePath = Path.Combine(directoryPath, filename);
                
                // Сериализуем нейросеть в JSON
                string jsonData = SerializeNetworkToJson(best_network);
                
                if (string.IsNullOrEmpty(jsonData))
                {
                    Debug.LogError($"❌ Сериализация сети вернула пустую строку! Отмена сохранения.");
                    return;
                }
                
                // Логируем размер JSON для диагностики
                Debug.Log($"📊 Размер сериализованной сети: {jsonData.Length} байт");
                
                // Записываем в файл
                File.WriteAllText(filePath, jsonData);
                
                // Проверяем, что файл действительно создан
                if (File.Exists(filePath))
                {
                    Debug.Log($"💾 Успешно сохранена нейросеть в: {filePath}");
                    // Запоминаем имя последнего сохраненного файла для отображения в GUI
                    last_saved_file = filename;
                    last_saved_time = Time.time;
                }
                else
                {
                    Debug.LogError($"❌ Файл не был создан после попытки записи: {filePath}");
                }
                
                // Дополнительно вызываем Flush, чтобы гарантировать запись на диск
                // (может быть полезно в некоторых случаях)
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Write);
                fileStream.Flush(true);
                fileStream.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка при сохранении сети: {e.Message}\n{e.StackTrace}");
            }
        }
        
        // Переменные для хранения информации о последнем сохранении/загрузке
        private string last_saved_file = "";
        private float last_saved_time = 0;
        private string last_loaded_file = "";
        private bool network_loaded_at_start = false;
        
        // Загрузка лучшей нейросети из файла (обертка для существующего метода)
        private bool LoadBestNetworkFromFile()
        {
            if (network_loaded_at_start)
            {
                // Если сеть уже была загружена, не загружаем её снова
                Debug.Log("⚠️ Нейросеть уже была загружена ранее, повторная загрузка пропущена.");
                return true;
            }
            
            // Попробуем загрузить сеть из нескольких возможных мест
            NeuralNetwork network = null;
            
            // 1. Сначала пробуем загрузить из директории snapshots
            string snapshotsPath = Path.Combine(Application.dataPath, models_directory);
            if (Directory.Exists(snapshotsPath))
            {
                // Ищем все JSON файлы в директории
                string[] files = Directory.GetFiles(snapshotsPath, "*.json");
                
                Debug.Log($"📁 Найдено {files.Length} файлов моделей в {snapshotsPath}");
                
                // Если есть файлы, пробуем загрузить последний (с наибольшей датой изменения)
                if (files.Length > 0)
                {
                    // Сортируем файлы по дате изменения (от новых к старым)
                    Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
                    
                    // Пробуем загрузить самый новый файл
                    string latestFile = files[0];
                    Debug.Log($"🔄 Пробуем загрузить последний файл: {Path.GetFileName(latestFile)}");
                    
                    try
                    {
                        string json = File.ReadAllText(latestFile);
                        network = DeserializeNetworkFromJson(json);
                        
                        if (network != null)
                        {
                            Debug.Log($"✅ Успешно загружена сеть из {Path.GetFileName(latestFile)}");
                            best_network = network;
                            last_loaded_file = Path.GetFileName(latestFile);
                            network_loaded_at_start = true;
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"❌ Ошибка при загрузке {latestFile}: {e.Message}");
                    }
                }
            }
            
            // 2. Если не удалось загрузить из snapshots, пробуем стандартный путь
            network = LoadBestNetwork();
            
            if (network != null)
            {
                best_network = network;
                last_loaded_file = "best_network.json";
                network_loaded_at_start = true;
                return true;
            }
            
            // Если не удалось загрузить ни из одного места
            network_loaded_at_start = false;
            return false;
        }
        
        // Загрузка лучшей нейросети из файла
        public NeuralNetwork LoadBestNetwork()
        {
            string filePath = Application.dataPath + "/Resources/Agents/best_network.json";
            
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"Файл с нейросетью не найден: {filePath}");
                return null;
            }

            try
            {
                // Читаем JSON из файла
                string json = File.ReadAllText(filePath);
                
                // Используем общий метод десериализации
                NeuralNetwork network = DeserializeNetworkFromJson(json);
                
                if (network != null)
                {
                    Debug.Log($"Лучшая сеть успешно загружена из {filePath}: {network}");
                    return network;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка при загрузке сети: {e.Message}\n{e.StackTrace}");
            }

            return null;
        }
        
        // Десериализация сети из JSON
        private NeuralNetwork DeserializeNetworkFromJson(string json)
            {
                try
                {
                // Парсим JSON
                Dictionary<string, object> data = ParseJSONObject(json);
                
                if (data == null)
                {
                    Debug.LogError("Ошибка при разборе JSON файла!");
                    return null;
                }

                // Извлекаем массив слоев
                List<object> layersObj = data["layers"] as List<object>;
                if (layersObj == null)
                {
                    Debug.LogError("Не найдена информация о слоях в JSON файле!");
                    return null;
                }

                // Проверяем количество слоев (минимум 2)
                if (layersObj.Count < 2)
                {
                    Debug.LogError("В загружаемой сети недостаточно слоев (минимум 2)!");
                    return null;
                }

                int[] layers = new int[layersObj.Count];
                for (int i = 0; i < layersObj.Count; i++)
                {
                    layers[i] = Convert.ToInt32(layersObj[i]);
                    if (layers[i] <= 0)
                    {
                        Debug.LogError($"Некорректный размер слоя {i}: {layers[i]} (должен быть положительным)");
                        return null;
                    }
                }

                // Создаем новую нейросеть
                NeuralNetwork network = new NeuralNetwork(layers);

                // Извлекаем веса
                List<object> weightsObj = data["weights"] as List<object>;
                if (weightsObj != null && weightsObj.Count == network.weights.Length)
                {
                    for (int i = 0; i < weightsObj.Count; i++)
                    {
                        List<object> layerWeights = weightsObj[i] as List<object>;
                        if (layerWeights != null && layerWeights.Count == network.weights[i].Length)
                        {
                            for (int j = 0; j < layerWeights.Count; j++)
                            {
                                List<object> neuronWeights = layerWeights[j] as List<object>;
                                if (neuronWeights != null && neuronWeights.Count == network.weights[i][j].Length)
                                {
                                    for (int k = 0; k < neuronWeights.Count; k++)
                                    {
                                        network.weights[i][j][k] = Convert.ToSingle(neuronWeights[k]);
                                    }
                }
                else
                {
                                    Debug.LogWarning($"Структура весов нейрона {j} в слое {i} не соответствует ожидаемой!");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Структура весов слоя {i} не соответствует ожидаемой!");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Структура весов в JSON не соответствует ожидаемой!");
                }

                // Извлекаем смещения
                List<object> biasesObj = data["biases"] as List<object>;
                if (biasesObj != null && biasesObj.Count == network.biases.Length)
                {
                    for (int i = 0; i < biasesObj.Count; i++)
                    {
                        List<object> layerBiases = biasesObj[i] as List<object>;
                        if (layerBiases != null && layerBiases.Count == network.biases[i].Length)
                        {
                            for (int j = 0; j < layerBiases.Count; j++)
                            {
                                network.biases[i][j] = Convert.ToSingle(layerBiases[j]);
                }
            }
            else
            {
                            Debug.LogWarning($"Структура смещений в слое {i} не соответствует ожидаемой!");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Структура смещений в JSON не соответствует ожидаемой!");
                }

                // Извлекаем фитнес
                if (data.ContainsKey("fitness"))
                {
                    network.fitness = Convert.ToSingle(data["fitness"]);
                }

                return network;
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка при десериализации сети: {e.Message}");
                return null;
            }
        }
        
        // Парсер JSON
        private Dictionary<string, object> ParseJSONObject(string json)
            {
                try
                {
                int index = 0;
                return ParseJSONObject(json, ref index);
                }
                catch (Exception e)
                {
                Debug.LogError($"Ошибка при разборе JSON: {e.Message}");
                return null;
            }
        }
        
        private Dictionary<string, object> ParseJSONObject(string json, ref int index)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            
            // Пропускаем пробелы и находим начало объекта
            SkipWhitespace(json, ref index);
            
            if (index >= json.Length || json[index] != '{')
            {
                throw new Exception($"Ожидалась открывающая фигурная скобка '{{', получено: {(index < json.Length ? json[index].ToString() : "конец строки")}");
            }
            
            index++; // Пропускаем {
            
            SkipWhitespace(json, ref index);
            
            // Пустой объект {}
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return result;
            }
            
            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                
                // Ожидаем строку в кавычках (ключ)
                if (json[index] != '"')
                {
                    throw new Exception($"Ожидалась открывающая кавычка для ключа, получено: {json[index]}");
                }
                
                index++; // Пропускаем "
                int startKey = index;
                
                // Ищем закрывающую кавычку
                while (index < json.Length && (json[index] != '"' || IsEscaped(json, index)))
                {
                    index++;
                }
                
                if (index >= json.Length)
                {
                    throw new Exception("Неожиданный конец строки при чтении ключа");
                }
                
                string key = json.Substring(startKey, index - startKey);
                index++; // Пропускаем "
                
                SkipWhitespace(json, ref index);
                
                // Ожидаем двоеточие
                if (index >= json.Length || json[index] != ':')
                {
                    throw new Exception($"Ожидалось двоеточие после ключа, получено: {(index < json.Length ? json[index].ToString() : "конец строки")}");
                }
                
                index++; // Пропускаем :
                
                // Чтение значения
                object value = ParseJSONValue(json, ref index);
                result[key] = value;
                
                SkipWhitespace(json, ref index);
                
                // После значения ожидаем запятую или закрывающую скобку
                if (index >= json.Length)
                {
                    throw new Exception("Неожиданный конец строки при чтении объекта");
                }
                
                if (json[index] == ',')
                {
                    index++; // Пропускаем ,
                    continue;
                }
                else if (json[index] == '}')
                {
                    index++; // Пропускаем }
                    break;
                }
                else
                {
                    throw new Exception($"Ожидалась запятая или закрывающая фигурная скобка, получено: {json[index]}");
                }
            }
            
            return result;
        }
        
        private List<object> ParseJSONArray(string json, ref int index)
        {
            List<object> result = new List<object>();
            
            // Пропускаем пробелы и находим начало массива
            SkipWhitespace(json, ref index);
            
            if (index >= json.Length || json[index] != '[')
            {
                throw new Exception($"Ожидалась открывающая квадратная скобка '[', получено: {(index < json.Length ? json[index].ToString() : "конец строки")}");
            }
            
            index++; // Пропускаем [
            
            SkipWhitespace(json, ref index);
            
            // Пустой массив []
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return result;
            }
            
            while (index < json.Length)
            {
                // Чтение элемента массива
                object value = ParseJSONValue(json, ref index);
                result.Add(value);
                
                SkipWhitespace(json, ref index);
                
                // После значения ожидаем запятую или закрывающую скобку
                if (index >= json.Length)
                {
                    throw new Exception("Неожиданный конец строки при чтении массива");
                }
                
                if (json[index] == ',')
                {
                    index++; // Пропускаем ,
                    continue;
                }
                else if (json[index] == ']')
                {
                    index++; // Пропускаем ]
                    break;
                }
                else
                {
                    throw new Exception($"Ожидалась запятая или закрывающая квадратная скобка, получено: {json[index]}");
                }
            }
            
            return result;
        }

        private object ParseJSONValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            
            if (index >= json.Length)
            {
                throw new Exception("Неожиданный конец строки при чтении значения");
            }
            
            char c = json[index];
            
            if (c == '{')
            {
                return ParseJSONObject(json, ref index);
            }
            else if (c == '[')
            {
                return ParseJSONArray(json, ref index);
            }
            else if (c == '"')
            {
                index++; // Пропускаем "
                int start = index;
                
                // Ищем закрывающую кавычку
                while (index < json.Length && (json[index] != '"' || IsEscaped(json, index)))
                {
                    index++;
                }
                
                if (index >= json.Length)
                {
                    throw new Exception("Неожиданный конец строки при чтении строки");
                }
                
                string value = json.Substring(start, index - start);
                index++; // Пропускаем "
                
                return value;
            }
            else if (char.IsDigit(c) || c == '-' || c == '+' || c == '.')
            {
                return ParseJSONNumber(json, ref index);
            }
            else if (json.Length - index >= 4 && json.Substring(index, 4) == "true")
            {
                index += 4;
                return true;
            }
            else if (json.Length - index >= 5 && json.Substring(index, 5) == "false")
            {
                index += 5;
                return false;
            }
            else if (json.Length - index >= 4 && json.Substring(index, 4) == "null")
            {
                index += 4;
                return null;
                }
                else
                {
                throw new Exception($"Неизвестный символ при чтении значения: {c}");
            }
        }

        private object ParseJSONNumber(string json, ref int index)
        {
            int start = index;
            bool isFloat = false;
            
            // Читаем число
            while (index < json.Length)
            {
                char c = json[index];
                
                if (char.IsDigit(c) || c == '-' || c == '+' || c == 'e' || c == 'E')
                {
                    index++;
                }
                else if (c == '.')
                {
                    isFloat = true;
                    index++;
                }
                else
                {
                    break;
                }
            }
            
            string numStr = json.Substring(start, index - start);
            
            // Преобразуем строку в число
            if (isFloat)
            {
                if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                {
                    return floatVal;
                }
            }
            else
            {
                if (int.TryParse(numStr, out int intVal))
                {
                    return intVal;
                }
            }
            
            throw new Exception($"Не удалось разобрать число: {numStr}");
        }

        private void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        private bool IsEscaped(string json, int index)
        {
            // Проверяем, что кавычка экранирована
            int count = 0;
            int i = index - 1;
            
            while (i >= 0 && json[i] == '\\')
            {
                count++;
                i--;
            }
            
            return count % 2 != 0; // Если количество слешей нечетное, кавычка экранирована
        }

        // Новый метод для корректной сериализации нейросети в JSON
        private string SerializeNetworkToJson(NeuralNetwork network)
        {
            if (network == null || network.layers == null || network.layers.Length < 2)
            {
                Debug.LogError($"❌ Невозможно сериализовать нейросеть: неверная структура слоёв. Network: {(network == null ? "null" : "not null")}, Layers: {(network?.layers == null ? "null" : network.layers.Length.ToString())}");
                return null;
            }

            try
            {
                // Создаем простую строковую структуру JSON вручную
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("{\n");
                
                // Добавляем слои
                sb.Append("\"layers\": [");
                for (int i = 0; i < network.layers.Length; i++)
                {
                    sb.Append(network.layers[i]);
                    if (i < network.layers.Length - 1)
                        sb.Append(", ");
                }
                sb.Append("],\n");
                
                // Добавляем фитнес
                sb.Append($"\"fitness\": {network.fitness.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n");
                
                // Сериализуем веса (трехмерный массив)
                sb.Append("\"weights\": [\n");
                for (int i = 0; i < network.weights.Length; i++)
                {
                    sb.Append("  [\n");
                    
                    for (int j = 0; j < network.weights[i].Length; j++)
                    {
                        sb.Append("    [");
                        
                        for (int k = 0; k < network.weights[i][j].Length; k++)
                        {
                            sb.Append(network.weights[i][j][k].ToString(System.Globalization.CultureInfo.InvariantCulture));
                            if (k < network.weights[i][j].Length - 1)
                                sb.Append(", ");
                        }
                        
                        sb.Append("]");
                        if (j < network.weights[i].Length - 1)
                            sb.Append(",");
                        sb.Append("\n");
                    }
                    
                    sb.Append("  ]");
                    if (i < network.weights.Length - 1)
                        sb.Append(",");
                    sb.Append("\n");
                }
                sb.Append("],\n");
                
                // Сериализуем смещения (двумерный массив)
                sb.Append("\"biases\": [\n");
                for (int i = 0; i < network.biases.Length; i++)
                {
                    sb.Append("  [");
                    
                    for (int j = 0; j < network.biases[i].Length; j++)
                    {
                        sb.Append(network.biases[i][j].ToString(System.Globalization.CultureInfo.InvariantCulture));
                        if (j < network.biases[i].Length - 1)
                            sb.Append(", ");
                    }
                    
                    sb.Append("]");
                    if (i < network.biases.Length - 1)
                        sb.Append(",");
                    sb.Append("\n");
                }
                sb.Append("]\n");
                
                sb.Append("}");
                
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка при сериализации сети: {e.Message}");
                return null;
            }
        }

        // Подсветка ТОП-5 агентов из предыдущего поколения
        private void HighlightTopAgents()
        {
            if (!highlight_top_agents || previous_top_agents.Count == 0)
                return;
                
            for (int i = 0; i < Mathf.Min(5, previous_top_agents.Count); i++)
            {
                int agentIndex = previous_top_agents[i];
                if (agentIndex < agents.Count && agents[agentIndex] != null)
                {
                    NeuroHuman agent = agents[agentIndex];
                    
                    // Подсвечиваем агента соответствующим цветом
                    SetAgentColor(agent, top_agent_colors[i]);
                    
                    Debug.Log($"🎨 Подсвечен ТОП-{i+1} агент #{agentIndex} цветом {top_agent_colors[i]}");
                }
            }
        }
        
        // Установка цвета агента
        private void SetAgentColor(NeuroHuman agent, Color color)
        {
            if (agent == null)
                return;
                
            try
            {
                // Получаем все рендереры в агенте и его дочерних объектах
                Renderer[] renderers = agent.GetComponentsInChildren<Renderer>();
                
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"⚠️ Агент {agent.name} не имеет Renderer компонентов!");
                    return;
                }
                
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || renderer.material == null) continue;
                    
                    // Создаем новый материал на основе существующего для каждого рендерера
                    Material newMaterial = new Material(renderer.material);
                    
                    // Устанавливаем базовый цвет и эмиссию для большей заметности
                    newMaterial.SetColor(agent_material_property, color);
                    
                    // Также попробуем установить эмиссионный цвет для большей заметности
                    if (newMaterial.HasProperty("_EmissionColor"))
                    {
                        newMaterial.EnableKeyword("_EMISSION");
                        newMaterial.SetColor("_EmissionColor", color * 0.5f); // Умножаем на 0.5 для менее интенсивного свечения
                    }
                    
                    // Для Стандартного шейдера увеличиваем металличность и сглаженность
                    if (newMaterial.HasProperty("_Metallic"))
                    {
                        newMaterial.SetFloat("_Metallic", 0.8f);
                    }
                    if (newMaterial.HasProperty("_Glossiness"))
                    {
                        newMaterial.SetFloat("_Glossiness", 0.9f);
                    }
                    
                    // Присваиваем новый материал
                    renderer.material = newMaterial;
                }
                
                // Пометим агент именем чтобы было понятно по номеру
                agent.gameObject.name = $"TOP_{previous_top_agents.IndexOf(agents.IndexOf(agent)) + 1}_AGENT";
                
                Debug.Log($"🎨 Изменен материал агента {agent.name} на цвет {color}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка при изменении материала: {e.Message}");
            }
        }

        // Установка максимальной силы для агента
        private void SetAgentMaxForce(NeuroHuman agent, float force)
        {
            if (agent == null)
                return;
                
            // Находим компоненты ConfigurableJoint и устанавливаем им силу
            ConfigurableJoint[] joints = agent.GetComponentsInChildren<ConfigurableJoint>();
            foreach (var joint in joints)
            {
                JointDrive drive = joint.angularXDrive;
                drive.maximumForce = force;
                joint.angularXDrive = drive;
                
                drive = joint.angularYZDrive;
                drive.maximumForce = force;
                joint.angularYZDrive = drive;
            }
        }

#if UNITY_EDITOR
    // Кнопка для спавна агентов из инспектора
    [UnityEngine.ContextMenu("Создать агентов")]
    public void EditorSpawnAgents()
    {
        Debug.Log($"🏭 Создаем {population_size} агентов из префаба (вызвано из редактора)...");
        SpawnAgents();
    }
    
    // Кнопка для удаления всех агентов из сцены
    [UnityEngine.ContextMenu("Удалить всех агентов")]
    public void EditorDeleteAllAgents()
    {
        int count = 0;
        foreach (var agent in FindObjectsOfType<NeuroHuman>())
        {
            if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("Удаление агентов", 
                $"Удаляем агента {agent.name}...", count / (float)agents.Count))
            {
                break;
            }
            
            UnityEditor.EditorApplication.delayCall += () => {
                DestroyImmediate(agent.gameObject);
            };
            count++;
        }
        
        UnityEditor.EditorUtility.ClearProgressBar();
        agents.Clear();
        Debug.Log($"🗑️ Удалено {count} агентов из сцены.");
    }
    
    // Тестовый метод для создания плотной кучи агентов в одной точке
    [UnityEngine.ContextMenu("ТЕСТ: Создать толпу агентов в одной точке")]
    public void TestSpawnHundredAgents()
    {
        // Сохраняем старые значения
        float oldRadius = spawn_radius;
        bool oldRandomRotation = useRandomRotation;
        
        // Устанавливаем тестовые параметры
        spawn_radius = 0.5f; // Очень маленький радиус
        useRandomRotation = false; // Без случайного поворота
        
        Debug.Log($"🧪 ТЕСТ: Создаем толпу из {population_size} агентов в плотной куче...");
        SpawnAgents();
        
        // Восстанавливаем старые значения
        spawn_radius = oldRadius;
        useRandomRotation = oldRandomRotation;
        
        Debug.Log("✅ Тестовая толпа создана! Все агенты в одной точке.");
    }

    // Полный сброс состояния эволюции - ЭКСТРЕННАЯ МЕРА
    [UnityEngine.ContextMenu("!!! ЭКСТРЕННЫЙ СБРОС ЭВОЛЮЦИИ !!!")]
    public void EmergencyReset()
    {
        // Останавливаем текущую симуляцию
        StopSimulation();
        
        // Сбрасываем все ключевые переменные
        current_generation = 0;
        best_fitness_ever = 0f;
        best_network = null;
        isMagicFitnessDetected = false;
        
        // Очищаем историю и популяцию
        fitness_history.Clear();
        success_history.Clear();
        population.Clear();
        
        // Удаляем всех существующих агентов
        EditorDeleteAllAgents();
        
        // Пересоздаем популяцию с нуля
        InitializePopulation();
        
        // Спавним новых агентов
        SpawnAgents();
        
        Debug.LogWarning("⚠️ ВЫПОЛНЕН ЭКСТРЕННЫЙ СБРОС ЭВОЛЮЦИИ! Весь прогресс обнулен.");
    }
#endif

    // GUI для отображения статистики симуляции
    private void OnGUI()
    {
        // Стиль для заголовка
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        // Стиль для значений
        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.fontSize = 16;
        valueStyle.fontStyle = FontStyle.Normal;
        valueStyle.normal.textColor = Color.yellow;

        // Стиль для кнопок
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        
        // Основная панель
        GUI.Box(new Rect(10, 10, 300, 470), "");
        
        // Заголовок
        GUI.Label(new Rect(20, 15, 280, 30), "Эволюционная симуляция", titleStyle);
        
        // Информация о поколении
        GUI.Label(new Rect(20, 50, 150, 25), "Поколение:", titleStyle);
        GUI.Label(new Rect(180, 50, 120, 25), $"{current_generation}", valueStyle);
        
        // Информация о времени поколения
        GUI.Label(new Rect(20, 75, 150, 25), "Время поколения:", titleStyle);
        GUI.Label(new Rect(180, 75, 120, 25), $"{generation_timer:F1} / {generation_time:F1}", valueStyle);
        
        // Прогресс поколения
        float progress = generation_timer / generation_time;
        GUI.Box(new Rect(20, 105, 260, 20), "");
        if (simulation_running)
        {
            GUI.color = Color.green;
        }
        else
        {
            GUI.color = Color.gray;
        }
        GUI.Box(new Rect(20, 105, 260 * progress, 20), "");
        GUI.color = Color.white;
        
        // Информация о лучшем фитнесе
        GUI.Label(new Rect(20, 130, 150, 25), "Лучший фитнес:", titleStyle);
        GUI.Label(new Rect(180, 130, 120, 25), $"{best_fitness_ever:F2}", valueStyle);
        
        // Исправляем расчет среднего фитнеса - используем всю популяцию
        float avgPopulationFitness = 0;
        if (population != null && population.Count > 0)
        {
            float sum = 0;
            int validNetworks = 0;
            
            // Считаем сумму всех фитнесов
            foreach (var net in population)
            {
                if (net != null)
                {
                    sum += net.fitness;
                    validNetworks++;
                }
            }
            
            // Вычисляем среднее значение только если есть валидные сети
            if (validNetworks > 0)
            {
                avgPopulationFitness = sum / validNetworks;
            }
            
            // Отображаем средний фитнес всей популяции
            GUI.Label(new Rect(20, 155, 150, 25), "Средний фитнес:", titleStyle);
            GUI.Label(new Rect(180, 155, 120, 25), $"{avgPopulationFitness:F2}", valueStyle);
            
            // Отображаем информацию о количестве сетей
            GUI.Label(new Rect(20, 180, 280, 25), $"(по всей популяции, {validNetworks} сетей)", new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.gray }});
        }
        
        // Информация о файле сохранения
        GUI.Label(new Rect(20, 205, 280, 25), "Последний сохраненный файл:", titleStyle);
        GUI.Label(new Rect(20, 230, 280, 40), $"{last_saved_file}", valueStyle);
        
        // Кнопки управления
        if (simulation_running)
        {
            if (GUI.Button(new Rect(20, 280, 130, 40), "⏹️ Стоп", buttonStyle))
            {
                StopSimulation();
            }
            
            if (GUI.Button(new Rect(160, 280, 130, 40), "⏭️ Следующее", buttonStyle))
            {
                EndGeneration();
            }
        }
        else
        {
            if (GUI.Button(new Rect(20, 280, 130, 40), "▶️ Старт", buttonStyle))
            {
                StartSimulation();
            }
            
            if (GUI.Button(new Rect(160, 280, 130, 40), "🔄 Рестарт", buttonStyle))
            {
                current_generation = 0;
                StartSimulation();
            }
        }
        
        // Кнопка сохранения
        if (GUI.Button(new Rect(20, 330, 270, 35), "💾 Сохранить лучшую сеть", buttonStyle))
        {
            SaveBestNetwork();
        }
        
        // Метка управления скоростью
        GUI.Label(new Rect(20, 375, 270, 20), "⏱️ Скорость симуляции:", titleStyle);
        
        // Делаем 5 кнопок для разных скоростей
        for (int i = 0; i < time_speed_presets.Length; i++)
        {
            float speed = time_speed_presets[i];
            
            // Подсвечиваем текущую скорость
            GUI.backgroundColor = (i == current_speed_index) ? Color.yellow : Color.gray;
            
            if (GUI.Button(new Rect(20 + i * 52, 400, 52, 30), $"{speed}x", buttonStyle))
            {
                // Устанавливаем новую скорость
                time_scale = speed;
                Time.timeScale = speed;
                current_speed_index = i;
                Debug.Log($"⏩ Скорость симуляции изменена на {speed}x");
            }
        }
    }

    // Корутина для ожидания завершения спавна
    private IEnumerator StartSimulationAfterSpawn()
    {
        while (isCurrentlySpawning)
        {
            yield return new WaitForFixedUpdate();
        }
        
        // Когда спавн завершен, запускаем симуляцию
        if (agents.Count > 0)
        {
            Debug.Log("🚀 Спавн завершен, запускаем симуляцию...");
            StartSimulation();
        }
        else
        {
            Debug.LogError("❌ Не удалось создать агентов! Симуляция не запущена.");
        }
    }

    // Новый метод для глобального логирования статистики
    private void LogGlobalStatistics()
    {
        if (agents == null || agents.Count == 0) return;
        
        float avgFitness = 0f;
        float maxFitness = float.MinValue;
        float minFitness = float.MaxValue;
        int successCount = 0;
        int activeCount = 0;
        int movingCount = 0;
        float avgDistance = 0f;
        float avgForwardDistance = 0f;
        float maxDistance = 0f;
        float maxForwardDistance = 0f;
        NeuroHuman bestAgent = null;
        
        foreach (var agent in agents)
        {
            if (agent == null) continue;
            
            float fitness = agent.GetFitness();
            avgFitness += fitness;
            
            if (fitness > maxFitness)
            {
                maxFitness = fitness;
                bestAgent = agent;
            }
            
            if (fitness < minFitness)
            {
                minFitness = fitness;
            }
            
            if (agent.IsSuccessful())
            {
                successCount++;
            }
            
            // Используем уже существующую переменную max_lifetime
            if (agent.GetLifetime() <= max_lifetime && !agent.IsSuccessful())
            {
                activeCount++;
                
                // Рассчитываем расстояние от начальной позиции
                Vector3 initialPos = agent.GetInitialPosition();
                Vector3 displacement = agent.transform.position - initialPos;
                float distance = displacement.magnitude;
                float forwardDistance = displacement.z; // Расстояние по оси Z (вперед)
                
                avgDistance += distance;
                avgForwardDistance += forwardDistance;
                
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                }
                
                if (forwardDistance > maxForwardDistance)
                {
                    maxForwardDistance = forwardDistance;
                }
                
                // Если агент переместился хотя бы на 0.2m
                if (distance > 0.2f)
                {
                    movingCount++;
                }
            }
        }
        
        // Вычисляем средние значения
        if (agents.Count > 0)
        {
            avgFitness /= agents.Count;
            
            if (activeCount > 0)
            {
                avgDistance /= activeCount;
                avgForwardDistance /= activeCount;
            }
        }
        
        // Вывод общей статистики поколения
        Debug.Log($"=============== СТАТИСТИКА ПОКОЛЕНИЯ {current_generation} (t={generation_timer:F1}с) ===============");
        Debug.Log($"Всего агентов: {agents.Count}, Активных: {activeCount}, Успехов: {successCount}");
        Debug.Log($"Фитнес: Средний={avgFitness:F2}, Макс={maxFitness:F2}, Мин={minFitness:F2}");
        Debug.Log($"Движение: Средний путь={avgDistance:F2}м, Макс путь={maxDistance:F2}м, Вперед(avg)={avgForwardDistance:F2}м, Вперед(max)={maxForwardDistance:F2}м");
        Debug.Log($"Прогресс: Двигаются: {movingCount}/{activeCount}, Процент: {(activeCount > 0 ? (float)movingCount/activeCount*100 : 0):F1}%");
        
        // Детали лучшего агента
        if (bestAgent != null)
        {
            Debug.Log($"ЛУЧШИЙ АГЕНТ: {bestAgent.GetAgentStats()}");
        }
        
        // Выводим случайных 3 агентов для мониторинга
        Debug.Log("ВЫБОРКА АГЕНТОВ:");
        int sampleSize = Math.Min(3, agents.Count);
        for (int i = 0; i < sampleSize; i++)
        {
            int index = UnityEngine.Random.Range(0, agents.Count);
            if (agents[index] != null)
            {
                Debug.Log($"- {agents[index].GetAgentStats()}");
            }
        }
        
        Debug.Log("===============================================================");
    }
    
    // Метод для подсчета активности агентов
    private int CountActiveAgents()
    {
        int count = 0;
        foreach (var agent in agents)
        {
            if (agent != null && agent.GetLifetime() <= max_lifetime && !agent.IsSuccessful())
            {
                count++;
            }
        }
        return count;
    }

    // Новый метод для записи в файл логов
    public void WriteToLogFile(string text)
    {
        if (!save_logs_to_file || string.IsNullOrEmpty(logFilePath)) return;
        
        try
        {
            File.AppendAllText(logFilePath, text);
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при записи в файл логов: {e.Message}");
        }
    }

    }
}
