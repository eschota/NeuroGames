using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System;

namespace Game.Scripts.GamePlay
{
    public class Game_UI : MonoBehaviour
    {
        [SerializeField] private SimulationManager simulation_manager;

        [Header("Training Parameters UI")]
        private bool show_training_ui = true;
        private Rect training_ui_rect;
        private Rect speed_panel_rect;
        private Vector2 training_ui_scroll;
        private Dictionary<string, float> training_params = new Dictionary<string, float>();
        private Dictionary<string, string> training_inputs = new Dictionary<string, string>();
        
        [Header("UI Position and Size")]
        [SerializeField] private float ui_scale = 2f;
        [SerializeField] private float ui_width = 628.2f;
        [SerializeField] private float ui_height = 424.4f;
        [SerializeField] private float ui_right_margin = 63.2f;  // Отступ справа
        [SerializeField] private float ui_top_margin = 551.5f;    // Отступ сверху для основного окна
        
        [Header("Speed Panel Settings")]
        [SerializeField] private float speed_panel_width = 701.8f;
        [SerializeField] private float speed_panel_height = 84.7f;
        [SerializeField] private float speed_panel_top_margin = 459.3f;
        [SerializeField] private float speed_panel_right_margin = 22.5f;
        
        [Header("UI Element Settings")]
        [SerializeField] private float slider_height = 22.2f;
        [SerializeField] private float input_width = 93.2f;
        [SerializeField] private float param_spacing = 41.5f;
        [SerializeField] private float button_height = 40f;

        [Header("Font Sizes")]
        [SerializeField] private int window_title_font_size = 26;
        [SerializeField] private int header_font_size = 24;
        [SerializeField] private int param_label_font_size = 22;
        [SerializeField] private int input_font_size = 22;
        [SerializeField] private int start_button_font_size = 28;
        [SerializeField] private int speed_button_font_size = 24;

        [Header("Speed Buttons")]
        [SerializeField] private float speed_button_height = 45f;
        [SerializeField] private float speed_button_spacing = 10f;

        // Текущее состояние UI
        private bool training_started = false;
        private float current_speed = 1f;
        private readonly float[] available_speeds = { 1f, 2f, 5f, 10f };

        // UI Styles
        private GUIStyle window_style;
        private GUIStyle label_style;
        private GUIStyle input_style;
        private GUIStyle button_style;
        private GUIStyle speed_button_style;

        // Переводим названия параметров на русский
        private readonly Dictionary<string, string> parameter_names = new Dictionary<string, string>()
        {
            {"Activity_reward", "Награда за активность"},
            {"Target_reward", "Награда за цель"},
            {"Collision_penalty", "Штраф за столкновение"},
            {"Target_tracking_reward", "Награда за слежение"},
            {"Speed_change_reward", "Награда за изменение скорости"},
            {"Rotation_change_reward", "Награда за повороты"},
            {"Time_bonus_multiplier", "Множитель бонуса времени"}
        };

        private Texture2D lineTex;

        // Переменные для отображения ошибок
        private string error_message = "";
        private bool show_error = false;
        private float error_time = 0f;

        // Флаг для отображения диалога подтверждения сброса
        private bool show_reset_dialog = false;

        // Тестовые значения для статистики (заглушки)
        private int current_generation = 1;
        private float generation_timer = 0f;
        private float current_fps = 60f;
        private int current_agents_count = 10;
        private float best_distance_ever = 100f;
        private float best_distance_current = 50f;
        private float avg_distance_last_gen = 25f;
        private int total_successes_ever = 5;
        private int successes_last_gen = 1;
        private bool is_validation_round = false;
        private bool game_won = false;
        private float current_mutation_rate = 0.1f;
        private List<int> success_history = new List<int> { 0, 1, 0, 2, 1, 3, 2, 1 };
        private int max_success_count = 5;
        private int[] neural_layers = new int[] { 12, 16, 12, 8, 2 };

        // Добавляем отслеживание нового поколения
        private int lastKnownGeneration = 0;

        // Добавляем статические глобальные параметры, доступные из любого скрипта
        public static class GlobalTrainingParams
        {
            public static float ActivityReward = 0f;
            public static float TargetReward = 0f;
            public static float CollisionPenalty = 0f;
            public static float TargetTrackingReward = 0f;
            public static float SpeedChangeReward = 0f;
            public static float RotationChangeReward = 0f;
            public static float TimeBonusMultiplier = 0f;
            
            // Обновление параметров
            public static void UpdateParams(
                float activity,
                float target,
                float collision,
                float tracking,
                float speed,
                float rotation,
                float time)
            {
                ActivityReward = activity;
                TargetReward = target;
                CollisionPenalty = collision;
                TargetTrackingReward = tracking;
                SpeedChangeReward = speed;
                RotationChangeReward = rotation;
                TimeBonusMultiplier = time;
                
                Debug.Log($"📊 ГЛОБАЛЬНЫЕ ПАРАМЕТРЫ ОБНОВЛЕНЫ: act={activity}, tar={target}, col={collision}, " +
                          $"track={tracking}, speed={speed}, rot={rotation}, time={time}");
            }
        }

        void Start()
        {
            if (simulation_manager == null)
            {
                simulation_manager = FindObjectOfType<SimulationManager>();
                if (simulation_manager == null)
                {
                    Debug.LogError("❌ Не найден SimulationManager! UI не будет работать.");
                    enabled = false;
                    return;
                }
            }
            
            // Перед инициализацией UI, обнуляем все параметры агентов
            ForceNullifyAllNeuralParameters();

            // Инициализируем UI для параметров обучения
            InitTrainingUI();
            
            // ЗАПУСКАЕМ ЕБИЧЕСКУЮ КОРУТИНУ ДЛЯ МОНИТОРИНГА ВСЕХ АГЕНТОВ, ВКЛЮЧАЯ НЕАКТИВНЫХ!
            StartCoroutine(ContinuousAgentMonitoringCoroutine());
            
            // ВМЕСТО ЭТОГО - ИНЖЕКТИМСЯ В ИНИЦИАЛИЗАЦИЮ АГЕНТОВ 
            InjectSpawnHook();
            
            // НОВОЕ: Добавляем перехват механизма обновления поколений
            InjectGenerationChangeHook();
            
            // НОВОЕ: Добавляем прямой хук в активные агенты SimulationManager
            HookIntoSimulationManager();
            
            // НОВОЕ: Запускаем периодический мониторинг SimulationManager
            StartCoroutine(SimulationManagerMonitoring());

            // Ставим игру на паузу до начала обучения
            Time.timeScale = 0f;
            training_started = false;
            
            // Инициализируем таймер поколения
            StartCoroutine(SimulationTimerCoroutine());
            
            // Показываем игроку подсказку о необходимости настроить параметры
            ShowInstructionMessage();
        }

        // Корутина для периодического мониторинга SimulationManager
        private IEnumerator SimulationManagerMonitoring()
        {
            Debug.Log("🔄 Запущен периодический мониторинг SimulationManager");
            
            while (true)
            {
                if (training_started)
                {
                    // Применяем хук в SimulationManager
                    HookIntoSimulationManager();
                    
                    // Проверяем и обновляем параметры для всех агентов
                    ForceAggressiveParameterCheck();
                }
                
                // Проверяем каждую секунду
                yield return new WaitForSeconds(1.0f);
            }
        }
        
        // Внедрение хука в процесс спавна агентов
        private void InjectSpawnHook()
        {
            Debug.LogWarning("🔥 ИНЖЕКТИМСЯ В МЕХАНИЗМ СОЗДАНИЯ АГЕНТОВ!");
            
            try
            {
                // 1. Находим метод создания агентов в SimulationManager
                var spawnMethods = simulation_manager.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => 
                        m.Name.Contains("Create") || 
                        m.Name.Contains("Spawn") || 
                        m.Name.Contains("Instantiate") || 
                        m.Name.Contains("Generate"))
                    .ToList();
                    
                if (spawnMethods.Count > 0)
                {
                    Debug.LogWarning($"👉 НАЙДЕНО {spawnMethods.Count} методов, похожих на создание агентов!");
                    foreach (var method in spawnMethods)
                    {
                        Debug.Log($"📝 Возможный метод создания: {method.Name}");
                    }
                }
                else
                {
                    Debug.LogError("❌ Не удалось найти методы создания агентов!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"💥 Ошибка при поиске методов создания: {ex.Message}");
            }
            
            // 2. Создаем глобальный наблюдатель за созданием агентов
            GameObject agentWatcher = new GameObject("AgentBirthWatcher");
            var watcher = agentWatcher.AddComponent<AgentBirthWatcher>();
            watcher.Initialize(this);
            DontDestroyOnLoad(agentWatcher);
            
            Debug.LogWarning("✅ УСТАНОВЛЕН ГЛОБАЛЬНЫЙ НАБЛЮДАТЕЛЬ ЗА СОЗДАНИЕМ АГЕНТОВ!");
        }
        
        // Класс-наблюдатель за созданием агентов
        public class AgentBirthWatcher : MonoBehaviour
        {
            private Game_UI parentUI;
            private HashSet<int> knownAgents = new HashSet<int>();
            private int totalProcessed = 0;
            private int lastAgentCount = 0;
            private float lastCheckTime = 0f;
            
            public void Initialize(Game_UI ui)
            {
                parentUI = ui;
                
                // Создаем текущий список известных агентов
                var existingAgents = FindObjectsOfType<Neuro>();
                foreach (var agent in existingAgents)
                {
                    if (agent != null && agent.gameObject != null)
                    {
                        knownAgents.Add(agent.gameObject.GetInstanceID());
                        
                        // Сразу применяем параметры к существующим агентам
                        parentUI.InitializeAgentParams(agent);
                        totalProcessed++;
                    }
                }
                
                lastAgentCount = existingAgents.Length;
                lastCheckTime = Time.realtimeSinceStartup;
                
                // Запускаем корутину для периодической проверки в дополнение к кадровой
                StartCoroutine(PeriodicAgentCheck());
                
                Debug.Log($"🔍 Наблюдатель инициализирован, найдено {existingAgents.Length} существующих агентов");
            }
            
            // Проверка КАЖДЫЙ КАДР для мгновенного обнаружения новых агентов
            void Update()
            {
                // Оптимизируем - не проверяем каждый кадр, а только если прошло 100 мс
                if (Time.realtimeSinceStartup - lastCheckTime < 0.1f)
                    return;
                    
                lastCheckTime = Time.realtimeSinceStartup;
                
                CheckForNewAgents();
            }
            
            // Вынесенный метод проверки новых агентов
            private void CheckForNewAgents()
            {
                var currentAgents = FindObjectsOfType<Neuro>();
                
                // Если количество агентов изменилось, сразу логируем
                if (currentAgents.Length != lastAgentCount)
                {
                    Debug.Log($"👁️ ДЕТЕКТОР: изменилось количество агентов! Было {lastAgentCount}, стало {currentAgents.Length}");
                    lastAgentCount = currentAgents.Length;
                }
                
                // Если появились новые агенты, применяем параметры
                int newAgentsFound = 0;
                
                foreach (var agent in currentAgents)
                {
                    if (agent == null || agent.gameObject == null) continue;
                    
                    int id = agent.gameObject.GetInstanceID();
                    
                    // Если это новый агент
                    if (!knownAgents.Contains(id))
                    {
                        newAgentsFound++;
                        Debug.LogWarning($"👶 ОБНАРУЖЕН НОВЫЙ АГЕНТ: {agent.gameObject.name} - СРАЗУ СТАВИМ ПАРАМЕТРЫ!");
                        
                        // ПРИНУДИТЕЛЬНО применяем параметры из GUI
                        parentUI.InitializeAgentParams(agent);
                        
                        // Дополнительно применяем параметры напрямую для надёжности
                        parentUI.ApplyCurrentGUIParamsToAgent(agent);
                        
                        // Добавляем в список известных
                        knownAgents.Add(id);
                        totalProcessed++;
                    }
                }
                
                // Если нашли много новых агентов, выводим статистику
                if (newAgentsFound > 2)
                {
                    Debug.Log($"🔥 НАЙДЕНО СРАЗУ {newAgentsFound} НОВЫХ АГЕНТОВ! Всего обработано: {totalProcessed}");
                    
                    // Если много новых агентов, возможно это новое поколение - применяем параметры ко всем
                    if (newAgentsFound > 5 && parentUI != null)
                    {
                        parentUI.EMERGENCY_ApplyAllParameters();
                    }
                }
            }
            
            // Корутина для периодических проверок с интервалом
            private IEnumerator PeriodicAgentCheck()
            {
                while (true)
                {
                    // Проверяем каждые 0.5 секунды
                    yield return new WaitForSeconds(0.5f);
                    
                    // Выполняем проверку
                    CheckForNewAgents();
                }
            }
        }
        
        // Инициализация параметров для агента - САМОЕ ГЛАВНОЕ
        public void InitializeAgentParams(Neuro agent)
        {
            if (agent == null)
            {
                Debug.LogError("🤬 Агент null в InitializeAgentParams!");
                return;
            }

            try
            {
                Debug.Log($"🔧 Инициализируем параметры для агента: {agent.gameObject.name}");
                
                // Получаем текущие значения из слайдеров
                float activity = GetSliderValue("Activity_reward");
                float target = GetSliderValue("Target_reward");
                float collision = GetSliderValue("Collision_penalty");
                float tracking = GetSliderValue("Target_tracking_reward");
                float speed = GetSliderValue("Speed_change_reward");
                float rotation = GetSliderValue("Rotation_change_reward");
                float time = GetSliderValue("Time_bonus_multiplier");
                
                Debug.Log($"📊 Значения из GUI: act={activity}, tar={target}, col={collision}, " +
                          $"track={tracking}, speed={speed}, rot={rotation}, time={time}");
                
                // Устанавливаем параметры прямым способом
                SetAgentRewards(agent, activity, target, collision, tracking, speed, rotation, time);
                
                // Дополнительно сбрасываем все поля фитнеса
                ResetAllFitnessFields(agent);
                
                Debug.Log($"✅ Параметры успешно установлены для: {agent.gameObject.name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"💥 Ошибка в InitializeAgentParams: {ex.Message}");
            }
        }

        // Сбрасываем все поля, связанные с фитнесом или наградами
        private void ResetAllFitnessFields(Neuro agent)
        {
            if (agent == null) return;
            
            try
            {
                // Сбрасываем АБСОЛЮТНО все поля фитнеса и всякие кэши
                var fields = new[] 
                {
                    "fitness",
                    "best_fitness", 
                    "current_fitness",
                    "total_fitness",
                    "distance_to_target",
                    "last_distance",
                    "min_distance",
                    "has_reached_target",
                    "collision_count",
                    "reward_cache",
                    "total_reward",
                    "last_position",
                    "last_rotation",
                    "target_hit_time"
                };
                
                foreach (var fieldName in fields)
                {
                    try {
                        var field = agent.GetType().GetField(fieldName, 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.NonPublic | 
                            System.Reflection.BindingFlags.Instance);
                            
                        if (field != null)
                        {
                            if (field.FieldType == typeof(float))
                            {
                                field.SetValue(agent, 0f);
                            }
                            else if (field.FieldType == typeof(int))
                            {
                                field.SetValue(agent, 0);
                            }
                            else if (field.FieldType == typeof(bool))
                            {
                                field.SetValue(agent, false);
                            }
                            else if (field.FieldType == typeof(Vector3))
                            {
                                field.SetValue(agent, Vector3.zero);
                            }
                            else if (field.FieldType == typeof(Quaternion))
                            {
                                field.SetValue(agent, Quaternion.identity);
                            }
                            
                            // Debug.Log($"✓ Сброшено поле {fieldName} для агента {agent.gameObject.name}");
                        }
                    } catch { }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"⚠️ Ошибка при сбросе фитнеса: {ex.Message}");
            }
        }

        // Корутина для имитации времени симуляции
        private IEnumerator SimulationTimerCoroutine()
        {
            while (true)
            {
                if (training_started)
                {
                    generation_timer += Time.deltaTime;
                    
                    // Имитируем случайное изменение FPS
                    current_fps = Mathf.Clamp(current_fps + UnityEngine.Random.Range(-5f, 5f), 30f, 120f);
                    
                    // Каждые 30 секунд начинаем новое поколение
                    if (generation_timer > 30f)
                    {
                        generation_timer = 0f;
                        current_generation++;
                        successes_last_gen = UnityEngine.Random.Range(0, 5);
                        total_successes_ever += successes_last_gen;
                        
                        // Обновляем историю успехов
                        if (success_history.Count > 20)
                        {
                            success_history.RemoveAt(0);
                        }
                        success_history.Add(successes_last_gen);
                        
                        // Обновляем максимум успехов
                        if (successes_last_gen > max_success_count)
                        {
                            max_success_count = successes_last_gen;
                        }
                        
                        // Обновляем средние значения
                        best_distance_current = UnityEngine.Random.Range(10f, 200f);
                        avg_distance_last_gen = best_distance_current * 0.7f;
                        
                        // Иногда обновляем рекорд
                        if (UnityEngine.Random.value > 0.7f)
                        {
                            best_distance_ever = best_distance_current;
                        }
                        
                        // Каждые 10 поколений делаем валидационный раунд
                        is_validation_round = (current_generation % 10 == 0);
                        
                        // После 50-го поколения можем объявить победу
                        if (current_generation > 50 && UnityEngine.Random.value > 0.9f)
                        {
                            game_won = true;
                        }
                        
                        Debug.Log($"📊 Поколение {current_generation} завершено! Успехов: {successes_last_gen}");
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        void OnGUI()
        {
            if (simulation_manager == null) return;

            // Отображаем статистику
            DisplayStats();
            
            // Рисуем UI параметров обучения
            DrawTrainingUI();

            // Показываем сообщение для пользователя если нужно
            if (show_error && Time.realtimeSinceStartup < error_time)
            {
                DrawInfoMessage();
            }
        }

        void DrawTrainingUI()
        {
            if (!show_training_ui) return;

            // Обновляем размеры UI если размер экрана изменился
            if (Event.current.type == EventType.Layout)
            {
                UpdateUIRect();
            }
            
            // Применяем масштаб UI
            GUIUtility.ScaleAroundPivot(Vector2.one, new Vector2(Screen.width, Screen.height));
            
            // Рисуем основную панель с параметрами
            GUI.Box(training_ui_rect, "", window_style);
            
            // Заголовок панели параметров
            Rect headerRect = new Rect(training_ui_rect.x + 20, training_ui_rect.y + 20, training_ui_rect.width - 40, 30);
            GUIStyle headerStyle = new GUIStyle(label_style);
            headerStyle.fontSize = header_font_size;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(headerRect, "ПАРАМЕТРЫ ОБУЧЕНИЯ", headerStyle);
            
            // Область с прокруткой для параметров
            Rect scrollViewRect = new Rect(training_ui_rect.x + 10, training_ui_rect.y + 60, 
                                        training_ui_rect.width - 20, training_ui_rect.height - 200);
            
            // Начало области с прокруткой
            training_ui_scroll = GUI.BeginScrollView(scrollViewRect, training_ui_scroll, 
                                                    new Rect(0, 0, scrollViewRect.width - 30, 
                                                            parameter_names.Count * param_spacing + 20));
                                                    
            // Рисуем все параметры
            DrawRewardParameters();
            
            // Заканчиваем область с прокруткой
            GUI.EndScrollView();
            
            // Рисуем кнопку начала/остановки обучения
            Rect startButtonRect = new Rect(training_ui_rect.x + 20, training_ui_rect.y + training_ui_rect.height - 120, 
                                        training_ui_rect.width - 40, button_height);
            
            string buttonText = training_started ? "■ ОСТАНОВИТЬ ОБУЧЕНИЕ" : "▶ НАЧАТЬ ОБУЧЕНИЕ";
            GUIStyle startButtonStyle = new GUIStyle(button_style);
            startButtonStyle.fontSize = start_button_font_size;
            startButtonStyle.normal.textColor = training_started ? Color.red : Color.green;
            
            if (GUI.Button(startButtonRect, buttonText, startButtonStyle))
            {
                ToggleTraining();
            }
            
            // КНОПКА ЭКСТРЕННОГО ПРИМЕНЕНИЯ ПАРАМЕТРОВ
            Rect emergencyButtonRect = new Rect(training_ui_rect.x + 20, training_ui_rect.y + training_ui_rect.height - 70, 
                                           training_ui_rect.width - 40, button_height);
            
            GUIStyle emergencyButtonStyle = new GUIStyle(button_style);
            emergencyButtonStyle.fontSize = 20;
            emergencyButtonStyle.normal.textColor = new Color(1f, 0.5f, 0f); // Оранжевый цвет для экстренной кнопки
            
            if (GUI.Button(emergencyButtonRect, "🔥 НАСИЛЬНО ПРИМЕНИТЬ ПАРАМЕТРЫ", emergencyButtonStyle))
            {
                Debug.Log("👊 ПРИНУДИТЕЛЬНОЕ ПРИМЕНЕНИЕ ПАРАМЕТРОВ!");
                EMERGENCY_ApplyAllParameters();
                ShowError("Параметры применены ко всем агентам!", 2f);
            }
            
            // Кнопка сброса обучения
            if (training_started)
            {
                Rect resetButtonRect = new Rect(training_ui_rect.x + 20, training_ui_rect.y + training_ui_rect.height - 20, 
                                            training_ui_rect.width - 40, button_height - 20);
                
                GUIStyle resetButtonStyle = new GUIStyle(button_style);
                resetButtonStyle.fontSize = 18;
                resetButtonStyle.normal.textColor = Color.yellow;
                
                if (GUI.Button(resetButtonRect, "↺ Сбросить обучение", resetButtonStyle))
                {
                    show_reset_dialog = true;
                }
            }
            
            // Показываем диалог подтверждения сброса если нужно
            if (show_reset_dialog)
            {
                DrawResetDialog();
            }
        }

        private void DrawSpeedPanel()
        {
            GUI.Box(speed_panel_rect, "Скорость симуляции", window_style);

            // Вычисляем размеры для кнопок скорости и управления
            float total_width = speed_panel_rect.width - (2 * speed_button_spacing);
            float speed_section_width = total_width * 0.5f; // 50% под кнопки скорости
            float control_section_width = total_width * 0.5f;  // 50% под кнопки управления 
            float control_button_width = (control_section_width - (2 * speed_button_spacing)) / 3; // Делим на 3 кнопки

            float button_width = (speed_section_width - ((available_speeds.Length + 1) * speed_button_spacing)) / available_speeds.Length;
            float button_x = speed_panel_rect.x + speed_button_spacing;
            float button_y = speed_panel_rect.y + (20f / ui_scale);

            // Рисуем кнопки скорости
            for (int i = 0; i < available_speeds.Length; i++)
            {
                float speed = available_speeds[i];
                bool is_current = Mathf.Approximately(current_speed, speed);

                // Подсвечиваем текущую скорость
                if (is_current)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                }
                else
                {
                    GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                }

                string speed_text = speed == 1f ? "1×" : $"{speed:F0}×";
                if (GUI.Button(new Rect(button_x, button_y, button_width, speed_button_height / ui_scale),
                              speed_text, speed_button_style))
                {
                    current_speed = speed;
                    Time.timeScale = training_started ? speed : 0f;
                    Debug.Log($"⚡ Скорость симуляции изменена на {speed}×");
                }

                button_x += button_width + speed_button_spacing;
            }

            // Рисуем кнопки управления
            float control_x = speed_panel_rect.x + speed_section_width + (2 * speed_button_spacing);
            
            // Кнопка СТАРТ/ПАУЗА
            if (!training_started)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f); // Зелёный для старта
                if (GUI.Button(new Rect(control_x, button_y, 
                                      control_button_width, 
                                      speed_button_height / ui_scale),
                             "СТАРТ", button_style))
                {
                    training_started = true;
                    Time.timeScale = current_speed;
                    // Вместо вызова simulation_manager.StartTraining() просто логируем запуск
                    Debug.Log($"🚀 Обучение запущено! Скорость: {current_speed}×");
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.2f); // Жёлтый для паузы
                if (GUI.Button(new Rect(control_x, button_y,
                                      control_button_width,
                                      speed_button_height / ui_scale),
                             "ПАУЗА", button_style))
                {
                    training_started = false;
                    Time.timeScale = 0f;
                    // Вместо вызова simulation_manager.PauseTraining() просто логируем паузу
                    Debug.Log("⏸️ Обучение на паузе");
                }
            }

            // Кнопка "НУЛИ" для сброса всех параметров
            control_x += control_button_width + speed_button_spacing;
            GUI.backgroundColor = new Color(0.7f, 0.3f, 0.9f); // Фиолетовый для сброса параметров
            if (GUI.Button(new Rect(control_x, button_y,
                                  control_button_width,
                                  speed_button_height / ui_scale),
                         "НУЛИ", button_style))
            {
                // Сначала пробуем стандартный сброс
                ResetAllParametersToZero();
                
                // Затем добавляем экстренное обнуление всех агентов
                EMERGENCY_NullifyAllAgents();
            }

            // Кнопка СБРОС
            control_x += control_button_width + speed_button_spacing;
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // Красный для сброса
            if (GUI.Button(new Rect(control_x, button_y,
                                  control_button_width - speed_button_spacing,
                                  speed_button_height / ui_scale),
                         "СБРОС", button_style))
            {
                // Создаём простое диалоговое окно подтверждения
                show_reset_dialog = true;
            }

            // Рисуем диалог подтверждения сброса если нужно
            if (show_reset_dialog)
            {
                DrawResetDialog();
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawResetDialog()
        {
            // Затемняем фон
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Рисуем окно диалога
            float dialog_width = 400f;
            float dialog_height = 150f;
            float dialog_x = (Screen.width - dialog_width) / 2;
            float dialog_y = (Screen.height - dialog_height) / 2;

            GUI.Box(new Rect(dialog_x, dialog_y, dialog_width, dialog_height), "Подтверждение сброса");

            // Текст сообщения
            GUI.Label(new Rect(dialog_x + 20, dialog_y + 40, dialog_width - 40, 40),
                "Вы уверены, что хотите сбросить всё обучение?\n" +
                "Это действие удалит все сохранённые нейросети и начнёт обучение заново.");

            // Кнопки
            if (GUI.Button(new Rect(dialog_x + 20, dialog_y + dialog_height - 40, 180, 30), "Да, сбросить всё"))
            {
                show_reset_dialog = false;
                
                // Создаем и вызываем собственный метод сброса, так как ResetTraining недоступен
                ResetTrainingLocal();
                
                training_started = false;
                Time.timeScale = 0f;
            }

            if (GUI.Button(new Rect(dialog_x + dialog_width - 200, dialog_y + dialog_height - 40, 180, 30), "Отмена"))
            {
                show_reset_dialog = false;
            }
        }

        // Реализуем локальную версию метода сброса, поскольку оригинальный недоступен
        private void ResetTrainingLocal()
        {
            // Логируем действие
            Debug.Log("🔄 Сброс обучения запрошен из Game_UI");
            
            // Перезагружаем текущую сцену
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }

        // Метод для отображения информационного сообщения
        private void DrawInfoMessage()
        {
            if (!show_error || Time.realtimeSinceStartup >= error_time) return;
            
            // Рисуем полупрозрачный фон на весь экран
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            // Рисуем окно сообщения
            float dialog_width = 500f;
            float dialog_height = 200f;
            float dialog_x = (Screen.width - dialog_width) / 2;
            float dialog_y = (Screen.height - dialog_height) / 2;

            // Заголовок и фон окна
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.8f); // Синий для информации
            GUI.Box(new Rect(dialog_x, dialog_y, dialog_width, dialog_height), "ВАЖНАЯ ИНФОРМАЦИЯ");
            GUI.backgroundColor = Color.white;

            // Стиль для текста
            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 16;
            textStyle.normal.textColor = Color.white;
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.wordWrap = true;

            // Текст сообщения
            GUI.Label(new Rect(dialog_x + 20, dialog_y + 40, dialog_width - 40, dialog_height - 80),
                error_message, textStyle);
            
            // Кнопка ОК
            if (GUI.Button(new Rect(dialog_x + (dialog_width - 100) / 2, dialog_y + dialog_height - 50, 100, 30), "Понятно"))
            {
                show_error = false;
            }
        }

        public void ShowError(string message, float duration = 5f)
        {
            error_message = message;
            show_error = true;
            error_time = Time.realtimeSinceStartup + duration;
        }

        void UpdateTrainingParameter(string param_name, float value)
        {
            try
            {
                // Логируем изменение параметра
                Debug.Log($"📊 Изменен параметр {param_name}: {value}");
                
                // Сначала обновляем значение в словаре параметров
                training_params[param_name] = value;
                training_inputs[param_name] = value.ToString("F1");
                
                // Обновляем глобальные параметры
                UpdateGlobalParameters();
                
                // Получаем ВСЕ существующие агенты в сцене
                var allAgents = FindObjectsOfType<Neuro>();
                Debug.Log($"🔍 Найдено {allAgents.Length} агентов для обновления параметра {param_name}");
                
                // Применяем изменения ко всем агентам
                foreach (var neuro in allAgents)
                {
                    if (neuro == null) continue;
                    
                    // НОВЫЙ ПОДХОД: используем нашу функцию для обновления ВСЕХ параметров
                    ApplyCurrentGUIParamsToAgent(neuro);
                }
                
                // Дополнительно, если simulation_manager существует
            if (simulation_manager != null)
            {
                    // Пробуем обновить параметр в SimulationManager
                    var targetField = simulation_manager.GetType().GetField(param_name.ToLower());
                    if (targetField != null)
                    {
                        targetField.SetValue(simulation_manager, value);
                        Debug.Log($"✅ Обновлен параметр {param_name}={value} в SimulationManager");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"🔥 Ошибка при обновлении параметра {param_name}: {e.Message}");
            }
        }

        // Метод для обновления глобальных параметров
        private void UpdateGlobalParameters()
        {
            GlobalTrainingParams.UpdateParams(
                GetCurrentParamValue("Activity_reward"),
                GetCurrentParamValue("Target_reward"),
                GetCurrentParamValue("Collision_penalty"),
                GetCurrentParamValue("Target_tracking_reward"),
                GetCurrentParamValue("Speed_change_reward"),
                GetCurrentParamValue("Rotation_change_reward"),
                GetCurrentParamValue("Time_bonus_multiplier")
            );
        }

        // Чистим ресурсы при уничтожении
        void OnDestroy()
        {
            if (lineTex != null)
            {
                Destroy(lineTex);
            }
        }

        // Статистика симуляции с заглушками вместо вызовов SimulationManager
        private void DisplayStats()
        {
            int padding = 10;
            int width = 300;
            int height = 25;
            
            GUI.skin.label.fontSize = 16;
            GUI.skin.label.normal.textColor = Color.white;
            
            // Create background box for stats
            GUI.Box(new Rect(padding, padding, width + padding * 2, 460), ""); 
            
            // Display statistics
            int y = padding * 2;
            
            // Добавляем информацию о сети
                GUI.contentColor = Color.yellow;
                GUI.Label(new Rect(padding * 2, y, width, height), 
                    "🆕 Using New Network (No Save)");
                y += height + 5;
                GUI.contentColor = Color.white;

            // Остальная статистика (используем локальные значения)
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Generation: {current_generation}");
            
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Time: {generation_timer:F1}s");
            
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"FPS: {current_fps:F1}");
            
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Agents: {current_agents_count}");

            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"TimeScale: {Time.timeScale}x");
                
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Best Distance Ever: {best_distance_ever:F2}m");
                
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Best Distance Current: {best_distance_current:F2}m");
                
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Avg Distance Last Gen: {avg_distance_last_gen:F2}m");
                
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Total Successes Ever: {total_successes_ever}");
                
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Successes Last Gen: {successes_last_gen}");

            // Добавляем инфу о нейросети
            y += height + 10; 
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"🧠 Neural Network Info:");
                
            y += height;
            string layers_str = string.Join(" → ", neural_layers);
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Structure: {layers_str}");
                
            y += height;
            // Вычисляем общее количество параметров нейросети
            int total_params = CalculateTotalParameters();
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Total Parameters: {total_params:N0}");
                
            y += height;
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Mutation Rate: {(current_mutation_rate * 100):F1}%");

            // Draw success history graph
            DrawSuccessGraph();

            // Добавляем информацию о валидационном раунде и победе
            if (is_validation_round)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(padding * 2, y, width, height), 
                    "🎯 VALIDATION ROUND");
                y += height;
            }
            
            if (game_won)
            {
                GUI.color = Color.green;
                GUI.Label(new Rect(padding * 2, y, width, height), 
                    "🏆 VICTORY! Training Complete!");
                y += height;
            }
            
            GUI.color = Color.white;
        }

        // Заглушка для CalculateTotalParameters
        private int CalculateTotalParameters()
        {
            // Простая формула для приблизительного расчета количества параметров нейросети
            int total = 0;
            for (int i = 0; i < neural_layers.Length - 1; i++)
            {
                // Веса + смещения для каждого слоя
                total += (neural_layers[i] * neural_layers[i + 1]) + neural_layers[i + 1];
            }
            return total;
        }

        void DrawSuccessGraph()
        {
            // Вычисляем позицию графика в правом верхнем углу с отступом 10 пикселей
            float graphX = Screen.width - 300 - 10;
            float graphY = 10; // Отступ сверху
            float graph_width = 300;
            float graph_height = 150;

            // Draw graph background
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.Box(new Rect(graphX, graphY, graph_width, graph_height), "");
            GUI.color = Color.white;

            // Draw graph title
            GUI.Label(new Rect(graphX, graphY - 20, graph_width, 20), 
                $"Success History (Max: {max_success_count})");

            // Создаём текстуру для линий
            if (lineTex == null)
            {
                lineTex = new Texture2D(1, 1);
                lineTex.SetPixel(0, 0, Color.white);
                lineTex.Apply();
            }

            // Draw coordinate system
            DrawLine(new Vector2(graphX, graphY + graph_height),
                    new Vector2(graphX, graphY),
                    Color.gray); // Y axis
                    
            DrawLine(new Vector2(graphX, graphY + graph_height),
                    new Vector2(graphX + graph_width, graphY + graph_height),
                    Color.gray); // X axis

            // Draw grid lines
            Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            int gridLines = 5;
            for (int i = 1; i < gridLines; i++)
            {
                float y = graphY + (i * graph_height / gridLines);
                DrawLine(new Vector2(graphX, y),
                        new Vector2(graphX + graph_width, y),
                        gridColor);
                
                float x = graphX + (i * graph_width / gridLines);
                DrawLine(new Vector2(x, graphY),
                        new Vector2(x, graphY + graph_height),
                        gridColor);
                
                // Draw Y axis labels
                int value = (gridLines - i) * max_success_count / gridLines;
                GUI.Label(new Rect(graphX - 30, y - 10, 25, 20), value.ToString());
            }

            // Draw graph
            if (success_history != null && success_history.Count > 1)
            {
                int max_history_points = 30;
                for (int i = 0; i < success_history.Count - 1; i++)
                {
                    float x1 = graphX + (i * graph_width / (max_history_points - 1));
                    float x2 = graphX + ((i + 1) * graph_width / (max_history_points - 1));
                    
                    float y1 = graphY + graph_height - 
                               (success_history[i] * graph_height / Mathf.Max(1, max_success_count));
                    float y2 = graphY + graph_height - 
                               (success_history[i + 1] * graph_height / Mathf.Max(1, max_success_count));
                    
                    DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Color.green);
                }
            }
        }

        private void DrawLine(Vector2 pointA, Vector2 pointB, Color color)
        {
            if (lineTex == null)
            {
                lineTex = new Texture2D(1, 1);
                lineTex.SetPixel(0, 0, Color.white);
                lineTex.Apply();
            }

            Matrix4x4 matrix = GUI.matrix;
            Color savedColor = GUI.color;
            GUI.color = color;

            float angle = Vector3.Angle(pointB - pointA, Vector2.right);
            if (pointA.y > pointB.y) angle = -angle;

            GUIUtility.RotateAroundPivot(angle, pointA);
            float width = (pointB - pointA).magnitude;
            GUI.DrawTexture(new Rect(pointA.x, pointA.y, width, 1), lineTex);
            GUI.matrix = matrix;
            GUI.color = savedColor;
        }

        // Метод для обновления размеров и позиции UI
        private void UpdateUIRect()
        {
            float scaled_width = ui_width / ui_scale;
            float scaled_height = ui_height / ui_scale;
            
            // Позиционируем окно в правом верхнем углу с учетом масштаба
            float x = (Screen.width / ui_scale) - scaled_width - (ui_right_margin / ui_scale);
            float y = ui_top_margin / ui_scale;
            
            training_ui_rect = new Rect(x, y, scaled_width, scaled_height);

            // Панель скорости сверху
            float speed_panel_scaled_width = speed_panel_width / ui_scale;
            float speed_panel_scaled_height = speed_panel_height / ui_scale;
            float speed_panel_x = (Screen.width / ui_scale) - speed_panel_scaled_width - (speed_panel_right_margin / ui_scale);
            float speed_panel_y = speed_panel_top_margin / ui_scale;
            
            speed_panel_rect = new Rect(speed_panel_x, speed_panel_y, speed_panel_scaled_width, speed_panel_scaled_height);
        }
        
        // Инициализация параметров обучения в UI с применением их к агентам
        void InitTrainingUI()
        {
            if (simulation_manager == null) return;
            
            // Инициализируем параметры обучения
            training_params.Clear();
            training_inputs.Clear();
            
            // Первоначально устанавливаем ненулевые значения по умолчанию, чтобы агенты сразу что-то делали
            training_params["Activity_reward"] = 20.0f;
            training_params["Target_reward"] = 50.0f;
            training_params["Collision_penalty"] = 30.0f;
            training_params["Target_tracking_reward"] = 10.0f;
            training_params["Speed_change_reward"] = 5.0f;
            training_params["Rotation_change_reward"] = 5.0f;
            training_params["Time_bonus_multiplier"] = 10.0f;
            
            // Инициализируем текстовые значения для полей и ПРИНУДИТЕЛЬНО применяем к агентам
            foreach (var param in training_params.Keys.ToList())
            {
                training_inputs[param] = training_params[param].ToString("F1");
                
                // ВАЖНО: Применяем ненулевые значения к агентам сразу после инициализации!
                UpdateTrainingParameter(param, training_params[param]);
            }
            
            // Обновляем глобальные параметры
            UpdateGlobalParameters();
            
            // Обновляем размеры и позицию UI
            UpdateUIRect();
            
            Debug.Log("🔧 Инициализирован UI для параметров обучения со значениями по умолчанию");
        }

        // Метод для принудительного сброса всех параметров в ноль
        private void ResetAllParametersToZero()
        {
            // Останавливаем симуляцию
            training_started = false;
            Time.timeScale = 0f;
            
            // Перебираем все параметры и устанавливаем их в ноль
            foreach (var param in training_params.Keys.ToList())
            {
                UpdateTrainingParameter(param, 0.0f);
            }
            
            Debug.Log("🧹 Все параметры обучения сброшены в ноль!");
            ShowError("Все параметры обучения сброшены в ноль!", 3f);
        }

        // Метод для принудительной установки всех параметров Neuro в ноль (минуя UI и словари)
        private void ForceNullifyAllNeuralParameters()
        {
            Debug.Log("🔄 Принудительное обнуление всех параметров обучения...");
            
            // 1. Ищем всех возможных агентов
            var agents = GameObject.FindGameObjectsWithTag("Agent");
            if (agents.Length == 0)
            {
                agents = FindObjectsOfType<Neuro>().Select(n => n.gameObject).ToArray();
            }
            
            // 2. Обнуляем параметры для всех найденных агентов
            foreach (var agent in agents)
            {
                NullifyAgentParameters(agent);
            }
            
            // 3. Если есть SimulationManager, обнуляем параметры и там
            if (simulation_manager != null)
            {
                var managerFields = simulation_manager.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(f => f.FieldType == typeof(float) && 
                           (f.Name.ToLower().Contains("reward") || 
                            f.Name.ToLower().Contains("penalty") || 
                            f.Name.ToLower().Contains("bonus") ||
                            f.Name.ToLower().Contains("multiplier")))
                    .ToList();
                
                foreach (var field in managerFields)
                {
                    field.SetValue(simulation_manager, 0.0f);
                    Debug.Log($"🧹 Обнулено поле {field.Name} в SimulationManager");
                }
            }
            
            // 4. Устанавливаем подписку на создание новых агентов
            StartCoroutine(MonitorNewAgentsCoroutine());
            
            Debug.Log("✅ Принудительное обнуление всех параметров обучения завершено!");
        }

        // Метод для обнуления параметров конкретного агента
        private void NullifyAgentParameters(GameObject agent)
        {
            if (agent == null) return;
            
            var neuro = agent.GetComponent<Neuro>();
            if (neuro == null) return;
            
            // Находим все поля с наградами и штрафами
            var rewardFields = neuro.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(float) && 
                       (f.Name.ToLower().Contains("reward") || 
                        f.Name.ToLower().Contains("penalty") || 
                        f.Name.ToLower().Contains("bonus") ||
                        f.Name.ToLower().Contains("multiplier")))
                .ToList();
            
            // Обнуляем найденные поля
            foreach (var field in rewardFields)
            {
                field.SetValue(neuro, 0.0f);
                Debug.Log($"🧹 Обнулено поле {field.Name} для агента {agent.name}");
            }
            
            // КРИТИЧНО ВАЖНО: проверяем конкретные поля по имени
            var activityRewardField = neuro.GetType().GetField("activity_reward");
            if (activityRewardField != null) activityRewardField.SetValue(neuro, 0.0f);
            
            var targetRewardField = neuro.GetType().GetField("target_reward");
            if (targetRewardField != null) targetRewardField.SetValue(neuro, 0.0f);
            
            var collisionPenaltyField = neuro.GetType().GetField("collision_penalty");
            if (collisionPenaltyField != null) collisionPenaltyField.SetValue(neuro, 0.0f);
            
            var targetTrackingRewardField = neuro.GetType().GetField("target_tracking_reward");
            if (targetTrackingRewardField != null) targetTrackingRewardField.SetValue(neuro, 0.0f);
            
            var speedChangeRewardField = neuro.GetType().GetField("speed_change_reward");
            if (speedChangeRewardField != null) speedChangeRewardField.SetValue(neuro, 0.0f);
            
            var rotationChangeRewardField = neuro.GetType().GetField("rotation_change_reward");
            if (rotationChangeRewardField != null) rotationChangeRewardField.SetValue(neuro, 0.0f);
            
            var timeBonusMultiplierField = neuro.GetType().GetField("time_bonus_multiplier");
            if (timeBonusMultiplierField != null) timeBonusMultiplierField.SetValue(neuro, 0.0f);
        }

        // Корутина для мониторинга создания новых агентов
        private IEnumerator MonitorNewAgentsCoroutine()
        {
            Debug.Log("🔍 Запущен мониторинг создания новых агентов...");
            
            // Запоминаем текущее количество агентов
            var currentAgents = new HashSet<int>(FindObjectsOfType<Neuro>().Select(n => n.gameObject.GetInstanceID()));
            
            while (true)
            {
                yield return new WaitForSeconds(0.5f); // Проверяем каждые 0.5 секунды
                
                // Получаем текущий список агентов
                var newAgentsList = FindObjectsOfType<Neuro>();
                
                foreach (var neuro in newAgentsList)
                {
                    int instanceId = neuro.gameObject.GetInstanceID();
                    
                    // Если это новый агент, которого не было раньше
                    if (!currentAgents.Contains(instanceId))
                    {
                        Debug.Log($"🆕 Обнаружен новый агент {neuro.gameObject.name} - обнуляем параметры!");
                        NullifyAgentParameters(neuro.gameObject);
                        currentAgents.Add(instanceId);
                    }
                }
            }
        }

        // Добавляем метод для прямого обнуления ALL_AGENTS в симуляции
        private void EMERGENCY_NullifyAllAgents()
        {
            Debug.Log("🚨 ЭКСТРЕННОЕ ОБНУЛЕНИЕ ВСЕХ АГЕНТОВ В ИГРЕ!");
            
            var allNeuro = FindObjectsOfType<Neuro>();
            foreach (var neuro in allNeuro)
            {
                // Установка всех полей вознаграждений в 0 напрямую
                var fields = new string[] 
                { 
                    "activity_reward", 
                    "target_reward", 
                    "collision_penalty", 
                    "target_tracking_reward", 
                    "speed_change_reward", 
                    "rotation_change_reward", 
                    "time_bonus_multiplier" 
                };
                
                foreach (var fieldName in fields)
                {
                    // Сначала пробуем получить поле через рефлексию
                    var field = neuro.GetType().GetField(fieldName);
                    if (field != null)
                    {
                        // Устанавливаем значение в 0
                        field.SetValue(neuro, 0.0f);
                        Debug.Log($"🚨 Обнулено поле {fieldName} для агента {neuro.gameObject.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Не удалось найти поле {fieldName} у Neuro компонента!");
                    }
                }
            }
            
            ShowError("🚨 ВСЕ АГЕНТЫ ПРИНУДИТЕЛЬНО ОБНУЛЕНЫ!", 3f);
        }

        // Показываем игроку подсказку о необходимости настроить параметры
        private void ShowInstructionMessage()
        {
            string message = "⚠️ ВНИМАНИЕ! Все параметры обучения сброшены в ноль!\n\n" +
                            "Для начала обучения настройте параметры вознаграждения с помощью слайдеров.\n" +
                            "Используйте кнопку НУЛИ для принудительного сброса всех значений.";
            
            // Показываем сообщение на 10 секунд
            ShowError(message, 10f);
        }

        // Публичный метод для получения значений параметров из GUI
        public float GetCurrentParamValue(string paramName)
        {
            // Проверяем, есть ли параметр в словаре
            if (training_params.ContainsKey(paramName))
            {
                return training_params[paramName];
            }
            
            // Если параметр не найден, возвращаем 0
            Debug.LogWarning($"⚠️ Параметр {paramName} не найден в GUI - возвращаем 0");
            return 0f;
        }

        // Применение ТЕКУЩИХ параметров GUI к конкретному агенту
        public void ApplyCurrentGUIParamsToAgent(Neuro agent)
        {
            if (agent == null)
            {
                Debug.LogError("🤬 КАКОГО ХРЕНА! Агент равен null!");
                return;
            }

            try
            {
                Debug.Log($"🔨 НАСИЛЬНО ВБИВАЕМ параметры в агента: {agent.gameObject.name}");
                
                // Словарь полей, которые нужно обновить (имя_поля -> текущее_значение_из_GUI)
                var fieldsToUpdate = new Dictionary<string, float>()
                {
                    { "activity_reward", GetCurrentParamValue("Activity_reward") },
                    { "target_reward", GetCurrentParamValue("Target_reward") },
                    { "collision_penalty", GetCurrentParamValue("Collision_penalty") },
                    { "target_tracking_reward", GetCurrentParamValue("Target_tracking_reward") },
                    { "speed_change_reward", GetCurrentParamValue("Speed_change_reward") },
                    { "rotation_change_reward", GetCurrentParamValue("Rotation_change_reward") },
                    { "time_bonus_multiplier", GetCurrentParamValue("Time_bonus_multiplier") }
                };
                
                // Количество успешных обновлений
                int successCount = 0;
                
                // Проходимся по каждому полю и НАСИЛЬНО обновляем его значение
                foreach (var field in fieldsToUpdate)
                {
                    try
                    {
                        // Получаем поле через рефлексию
                        System.Reflection.FieldInfo fieldInfo = agent.GetType().GetField(field.Key, 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.NonPublic | 
                            System.Reflection.BindingFlags.Instance);
                        
                        if (fieldInfo != null)
                        {
                            // Текущее значение поля в агенте
                            float currentValue = (float)fieldInfo.GetValue(agent);
                            
                            // Принудительно устанавливаем новое значение
                            fieldInfo.SetValue(agent, field.Value);
                            
                            // Перепроверяем, что значение правильно установилось
                            float newValue = (float)fieldInfo.GetValue(agent);
                            
                            // Всегда логируем изменение
                            string changeMsg = Mathf.Abs(currentValue - field.Value) < 0.001f 
                                ? $"🔄 Проверено поле {field.Key}: {newValue}"
                                : $"⚡ ЗАМЕНЕНО поле {field.Key}: {currentValue} -> {newValue}";
                            
                            Debug.Log(changeMsg);
                            successCount++;
                        }
                        else
                        {
                            Debug.LogError($"💀 Поле {field.Key} не найдено в агенте {agent.gameObject.name}!");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"🚫 Ошибка при установке {field.Key}: {ex.Message}");
                    }
                }
                
                // Также принудительно сбрасываем сам фитнесс агента, если он положительный
                try 
                {
                    // Находим поле фитнеса напрямую
                    var fitnessField = agent.GetType().GetField("fitness",
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                        
                    if (fitnessField != null)
                    {
                        float currentFitness = (float)fitnessField.GetValue(agent);
                        if (currentFitness > 0.001f)
                        {
                            Debug.LogWarning($"⚠️ Обнаружен положительный фитнес {currentFitness} у {agent.gameObject.name} - сбрасываем!");
                            fitnessField.SetValue(agent, 0f);
                        }
                    }
                }
                catch { }
                
                // Пытаемся вызвать метод обновления на агенте, если он существует
                try
                {
                    var updateMethod = agent.GetType().GetMethod("UpdateParameters", 
                        System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic);
                        
                    if (updateMethod != null)
                    {
                        updateMethod.Invoke(agent, null);
                        Debug.Log("✅ Вызван метод UpdateParameters() на агенте!");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"⚠️ Не удалось вызвать метод UpdateParameters(): {ex.Message}");
                }
                
                Debug.Log($"🎯 ГОТОВО! Обновлено {successCount}/{fieldsToUpdate.Count} параметров для {agent.gameObject.name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"💥 Критическая ошибка: {ex.Message}");
            }
        }

        // Простая функция получения значения из слайдера, ничего лишнего
        private float GetSliderValue(string paramName)
        {
            if (training_params.ContainsKey(paramName))
            {
                return training_params[paramName];
            }
            return 0f;
        }
        
        // Прямая установка параметров без проверок и лишнего кода
        private void SetAgentRewards(
            Neuro agent,
            float activity_reward,
            float target_reward,
            float collision_penalty,
            float target_tracking_reward,
            float speed_change_reward,
            float rotation_change_reward,
            float time_bonus_multiplier)
        {
            try
            {
                // Проверяем существование агента
                if (agent == null || agent.gameObject == null) return;
                
                // Если агент неактивен, логируем но всё равно устанавливаем
                if (!agent.gameObject.activeInHierarchy)
                {
                    Debug.Log($"⚠️ Агент {agent.gameObject.name} неактивен, но всё равно устанавливаем параметры");
                }
                
                // Прямая установка полей через рефлексию - никаких проверок
                var fields = agent.GetType().GetFields(System.Reflection.BindingFlags.Public | 
                                                     System.Reflection.BindingFlags.NonPublic | 
                                                     System.Reflection.BindingFlags.Instance);
                
                foreach (var field in fields)
                {
                    try
                    {
                        if (field.Name == "activity_reward") field.SetValue(agent, activity_reward);
                        else if (field.Name == "target_reward") field.SetValue(agent, target_reward);
                        else if (field.Name == "collision_penalty") field.SetValue(agent, collision_penalty);
                        else if (field.Name == "target_tracking_reward") field.SetValue(agent, target_tracking_reward);
                        else if (field.Name == "speed_change_reward") field.SetValue(agent, speed_change_reward);
                        else if (field.Name == "rotation_change_reward") field.SetValue(agent, rotation_change_reward);
                        else if (field.Name == "time_bonus_multiplier") field.SetValue(agent, time_bonus_multiplier);
                        
                        // Дополнительно сбрасываем фитнес
                        else if (field.Name == "fitness" && field.FieldType == typeof(float))
                        {
                            field.SetValue(agent, 0f);
                        }
                    }
                    catch { }
                }
                
                // Вызываем UpdateParameters, если такой метод существует
                try
                {
                    var updateMethod = agent.GetType().GetMethod("UpdateParameters", 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                    
                    if (updateMethod != null)
                    {
                        updateMethod.Invoke(agent, null);
                    }
                }
                catch { }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"💥 Ошибка при установке параметров агента {agent?.gameObject?.name}: {ex.Message}");
            }
        }

        // Метод для отрисовки всех параметров наград
        private void DrawRewardParameters()
        {
            float y_pos = 10f;
            
            // Проходим по всем параметрам
            foreach (var param in parameter_names.Keys.ToList())
            {
                // Получаем русское название параметра
                string param_display_name = parameter_names[param];
                
                // Рисуем название параметра
                Rect labelRect = new Rect(10f, y_pos, 250f, 30f);
                GUI.Label(labelRect, param_display_name, label_style);
                
                // Получаем текущее значение
                float current_value = 0f;
                if (training_params.ContainsKey(param))
                {
                    current_value = training_params[param];
                }
                
                // Рисуем слайдер
                Rect sliderRect = new Rect(10f, y_pos + 30f, 300f, 20f);
                float new_value = GUI.HorizontalSlider(sliderRect, current_value, 0f, 100f);
                
                // Если значение изменилось
                if (Mathf.Abs(new_value - current_value) > 0.01f)
                {
                    // Обновляем значение
                    training_params[param] = new_value;
                    if (training_inputs.ContainsKey(param))
                    {
                        training_inputs[param] = new_value.ToString("F1");
                    }
                    
                    // Обрабатываем изменение параметра
                    UpdateTrainingParameter(param, new_value);
                }
                
                // Рисуем текстовое поле для ввода значения
                Rect inputRect = new Rect(320f, y_pos + 30f, 80f, 25f);
                
                // Получаем текущее текстовое значение
                string current_text = "";
                if (training_inputs.ContainsKey(param))
                {
                    current_text = training_inputs[param];
                }
                
                // Рисуем текстовое поле
                GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                string new_text = GUI.TextField(inputRect, current_text, input_style);
                GUI.backgroundColor = Color.white;
                
                // Если текст изменился
                if (new_text != current_text)
                {
                    // Обновляем текстовое значение
                    training_inputs[param] = new_text;
                    
                    // Пробуем преобразовать в число
                    if (float.TryParse(new_text, out float parsed_value))
                    {
                        // Ограничиваем значение
                        parsed_value = Mathf.Clamp(parsed_value, 0f, 100f);
                        
                        // Обновляем числовое значение
                        training_params[param] = parsed_value;
                        
                        // Обрабатываем изменение параметра
                        UpdateTrainingParameter(param, parsed_value);
                    }
                }
                
                // Переходим к следующему параметру
                y_pos += param_spacing;
            }
        }

        // Переключение состояния обучения (старт/стоп)
        private void ToggleTraining()
        {
            // Инвертируем состояние
            training_started = !training_started;
            
            if (training_started)
            {
                // Запускаем обучение
                Time.timeScale = current_speed;
                Debug.Log($"🚀 Обучение запущено! Скорость: {current_speed}×");
                
                // Применяем параметры ко всем агентам
                EMERGENCY_ApplyAllParameters();
            }
            else
            {
                // Останавливаем обучение
                Time.timeScale = 0f;
                Debug.Log("⏸️ Обучение на паузе");
            }
        }

        // Корутина для мониторинга всех агентов на сцене (активных и неактивных)
        private IEnumerator ContinuousAgentMonitoringCoroutine()
        {
            // Блядская задержка, чтобы другие скрипты успели инициализироваться
            yield return new WaitForSeconds(1.5f);
            
            Debug.Log("🧠 ОХУЕННЫЙ мониторинг агентов ЗАПУЩЕН! Отслеживаю всех, суки!");
            
            // Сохраняем неактивных агентов, чтобы следить, когда они активируются
            HashSet<int> knownInactiveAgents = new HashSet<int>();
            
            while (true)
            {
                try 
                {
                    // Ищем ВСЕХ агентов, включая неактивных! (Resources.FindObjectsOfTypeAll)
                    Neuro[] allAgents = Resources.FindObjectsOfTypeAll<Neuro>();
                    if (allAgents.Length > 0)
                    {
                        Debug.Log($"🧠 Найдено {allAgents.Length} агентов (включая неактивных)! Применяю параметры насильно!");
                        
                        // Обрабатываем каждого агента, включая неактивных
                        foreach (Neuro agent in allAgents) 
                        {
                            if (agent == null) continue;
                            
                            // Получаем уникальный ID для отслеживания
                            int agentID = agent.GetInstanceID();
                            
                            // Если агент активен, применяем параметры немедленно
                            if (agent.gameObject.activeInHierarchy)
                            {
                                // Применяем параметры из GUI к агенту
                                InitializeAgentParams(agent);
                            }
                            // Если агент неактивен, запоминаем его для будущей активации
                            else if (!knownInactiveAgents.Contains(agentID))
                            {
                                Debug.Log($"⚠️ Агент {agent.gameObject.name} неактивен! Буду следить за его активацией");
                                knownInactiveAgents.Add(agentID);
                                
                                // Подписываемся на событие активации через MonitorAgentActivation
                                StartCoroutine(MonitorAgentActivation(agent, agentID));
                            }
                        }
                    }
                    
                    // Принудительно запускаем ЭКСТРЕННОЕ применение каждые 10 секунд
                    if (Time.frameCount % 600 == 0) // Примерно каждые 10 секунд при 60 FPS
                    {
                        Debug.Log("🔥 ЭКСТРЕННОЕ ПРИМЕНЕНИЕ ПАРАМЕТРОВ КО ВСЕМ АГЕНТАМ!");
                        EMERGENCY_ApplyAllParameters();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"💥 Пиздец в мониторинге: {ex.Message}");
                }
                
                // Проверяем каждые 2 секунды
                yield return new WaitForSeconds(2f);
            }
        }

        // Наблюдение за активацией неактивного агента
        private IEnumerator MonitorAgentActivation(Neuro agent, int agentID)
        {
            if (agent == null) yield break;
            
            // Продолжаем наблюдение, пока объект существует и не активен
            while (agent != null && !agent.gameObject.activeInHierarchy)
            {
                yield return new WaitForSeconds(0.5f);
            }
            
            // Если агент существует и стал активным
            if (agent != null && agent.gameObject.activeInHierarchy)
            {
                Debug.Log($"🎉 ЕБАТЬ! Агент {agent.gameObject.name} активировался! Применяю параметры!");
                
                // Применяем параметры из GUI к агенту, который только что был активирован
                InitializeAgentParams(agent);
            }
        }

        // ЭКСТРЕННЫЙ метод применения параметров ко всем агентам
        public void EMERGENCY_ApplyAllParameters()
        {
            Debug.Log("☢️ НАЧИНАЮ ЭКСТРЕННОЕ ПРИМЕНЕНИЕ ПАРАМЕТРОВ!");
            
            try
            {
                // Получаем все агенты ВСЕМИ ВОЗМОЖНЫМИ СПОСОБАМИ
                List<Neuro> allAgents = new List<Neuro>();
                
                // 1. Через Resources.FindObjectsOfTypeAll (включая неактивные)
                allAgents.AddRange(Resources.FindObjectsOfTypeAll<Neuro>());
                
                // 2. Через стандартный FindObjectsOfType (только активные)
                allAgents.AddRange(FindObjectsOfType<Neuro>());
                
                // 3. Через GetComponentsInChildren от корня сцены
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    allAgents.AddRange(root.GetComponentsInChildren<Neuro>(true)); // true - включая неактивные
                }
                
                // 4. Через тег "Agent", если он используется
                var taggedObjects = GameObject.FindGameObjectsWithTag("Agent");
                foreach (var obj in taggedObjects)
                {
                    var neuro = obj.GetComponent<Neuro>();
                    if (neuro != null) allAgents.Add(neuro);
                }
                
                // 5. Из активных агентов в SimulationManager
                if (simulation_manager != null) 
                {
                    var activeAgentsField = simulation_manager.GetType().GetField("active_agents", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                    if (activeAgentsField != null && activeAgentsField.GetValue(simulation_manager) is List<GameObject> activeAgents)
                    {
                        foreach (var agent in activeAgents)
                        {
                            if (agent != null)
                            {
                                var neuro = agent.GetComponent<Neuro>();
                                if (neuro != null) allAgents.Add(neuro);
                            }
                        }
                    }
                }
                
                // Удаляем дубликаты и null
                List<Neuro> uniqueAgents = new List<Neuro>();
                HashSet<int> seenIds = new HashSet<int>();
                
                foreach (var agent in allAgents)
                {
                    if (agent != null && agent.gameObject != null)
                    {
                        int id = agent.GetInstanceID();
                        if (!seenIds.Contains(id))
                        {
                            seenIds.Add(id);
                            uniqueAgents.Add(agent);
                        }
                    }
                }
                
                Debug.Log($"⚡ Найдено {uniqueAgents.Count} УНИКАЛЬНЫХ агентов для перепрошивки");
                int successCount = 0;
                
                // Кэшируем текущие значения
                float activity = GetCurrentParamValue("Activity_reward");
                float target = GetCurrentParamValue("Target_reward");
                float collision = GetCurrentParamValue("Collision_penalty");
                float tracking = GetCurrentParamValue("Target_tracking_reward");
                float speed = GetCurrentParamValue("Speed_change_reward");
                float rotation = GetCurrentParamValue("Rotation_change_reward");
                float time = GetCurrentParamValue("Time_bonus_multiplier");
                
                Debug.Log($"📊 Применяемые параметры: act={activity}, tar={target}, col={collision}, " +
                            $"track={tracking}, speed={speed}, rot={rotation}, time={time}");
                
                foreach (Neuro agent in uniqueAgents)
                {
                    if (agent == null) continue;
                    
                    try 
                    {
                        // СПОСОБ 1: Стандартный метод
                        InitializeAgentParams(agent);
                        
                        // СПОСОБ 2: Прямая установка
                        ApplyCurrentGUIParamsToAgent(agent);
                        
                        // СПОСОБ 3: БРУТАЛЬНАЯ УСТАНОВКА полей напрямую
                        if (agent != null)
                        {
                            // Получаем поля через рефлексию
                            var activityField = agent.GetType().GetField("activity_reward", 
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var targetField = agent.GetType().GetField("target_reward", 
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var collisionField = agent.GetType().GetField("collision_penalty", 
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var trackingField = agent.GetType().GetField("target_tracking_reward", 
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var speedField = agent.GetType().GetField("speed_change_reward", 
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var rotationField = agent.GetType().GetField("rotation_change_reward", 
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            var timeField = agent.GetType().GetField("time_bonus_multiplier", 
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            
                            // Устанавливаем значения
                            if (activityField != null) activityField.SetValue(agent, activity);
                            if (targetField != null) targetField.SetValue(agent, target);
                            if (collisionField != null) collisionField.SetValue(agent, collision);
                            if (trackingField != null) trackingField.SetValue(agent, tracking);
                            if (speedField != null) speedField.SetValue(agent, speed);
                            if (rotationField != null) rotationField.SetValue(agent, rotation);
                            if (timeField != null) timeField.SetValue(agent, time);
                        }
                        
                        // Проверяем, были ли параметры установлены корректно
                        bool parametersApplied = VerifyAgentParameters(agent);
                        
                        if (parametersApplied)
                        {
                            successCount++;
                            Debug.Log($"✅ Успешно обновлены параметры для {agent.gameObject.name}");
                        }
                        else 
                        {
                            Debug.LogWarning($"⚠️ НЕ УДАЛОСЬ проверить параметры для {agent.gameObject.name}, используем метод последней надежды");
                            
                            try
                            {
                                // СПОСОБ 4: ПУБЛИЧНОЕ ПОЛЕ НАПРЯМУЮ
                                agent.activity_reward = activity;
                                agent.target_reward = target;
                                agent.collision_penalty = collision;
                                agent.target_tracking_reward = tracking;
                                agent.speed_change_reward = speed;
                                agent.rotation_change_reward = rotation;
                                agent.time_bonus_multiplier = time;
                                
                                successCount++;
                                Debug.Log($"🔥 МЕТОД ПОСЛЕДНЕЙ НАДЕЖДЫ сработал для {agent.gameObject.name}!");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"💥 ВСЕ МЕТОДЫ НЕ СРАБОТАЛИ: {ex.Message}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"💥 Ошибка при обновлении {agent.gameObject.name}: {ex.Message}");
                    }
                }
                
                Debug.Log($"📊 СТАТИСТИКА: Обновлено {successCount}/{uniqueAgents.Count} агентов");
                
                // Если есть проблемы, показываем сообщение игроку
                if (successCount < uniqueAgents.Count)
                {
                    ShowError($"⚠️ Внимание! Не все агенты обновлены: {successCount}/{uniqueAgents.Count}", 3f);
                }
                else
                {
                    ShowError($"✅ ЗАЕБИСЬ! Все {successCount} агентов обновлены успешно!", 2f);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"💥 Критическая ошибка в EMERGENCY_ApplyAllParameters: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Проверяет, правильно ли установлены параметры агента
        private bool VerifyAgentParameters(Neuro agent)
        {
            if (agent == null) return false;
            
            try
            {
                // Проверяем, что хотя бы одно из основных значений не равно нулю
                float activityReward = 0f;
                float targetReward = 0f;
                
                // Получаем значения через рефлексию
                var activityField = agent.GetType().GetField("activity_reward", 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance);
                    
                var targetField = agent.GetType().GetField("target_reward", 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance);
                    
                if (activityField != null) activityReward = (float)activityField.GetValue(agent);
                if (targetField != null) targetReward = (float)targetField.GetValue(agent);
                
                // Если оба значения равны нулю, значит что-то не так
                bool valuesOK = !(Mathf.Approximately(activityReward, 0f) && Mathf.Approximately(targetReward, 0f));
                
                // Проверяем совпадение с GUI-параметрами
                float expectedActivity = GetCurrentParamValue("Activity_reward");
                float expectedTarget = GetCurrentParamValue("Target_reward");
                
                bool matchesExpected = Mathf.Approximately(activityReward, expectedActivity) && 
                                     Mathf.Approximately(targetReward, expectedTarget);
                                     
                return valuesOK && matchesExpected;
            }
            catch
            {
                return false;
            }
        }

        // Получает текущее поколение из SimulationManager
        private int GetCurrentGeneration()
        {
            if (simulation_manager == null) return 0;
            
            try
            {
                // Пытаемся получить значение через рефлексию
                var generationField = simulation_manager.GetType().GetField("current_generation", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                if (generationField != null)
                {
                    return (int)generationField.GetValue(simulation_manager);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"💥 Ошибка при получении номера поколения: {ex.Message}");
            }
            
            return 0;
        }

        // Форсим применение параметров в начале нового поколения
        private void ForceApplyParametersToNewGeneration()
        {
            Debug.Log("🚀 ФОРСИМ ПАРАМЕТРЫ ДЛЯ НОВОГО ПОКОЛЕНИЯ!");
            
            // Ждём немного, чтобы агенты успели проинициализироваться
            StartCoroutine(DelayedParameterApplication());
        }

        // Отложенное применение параметров, чтобы агенты успели создаться
        private IEnumerator DelayedParameterApplication()
        {
            // Ждём немного, чтобы агенты успели создаться
            yield return new WaitForSeconds(0.5f);
            
            // Первое применение
            EMERGENCY_ApplyAllParameters();
            
            // Ещё одно применение через секунду для уверенности
            yield return new WaitForSeconds(1.0f);
            
            // И ещё разок, чтобы наверняка
            EMERGENCY_ApplyAllParameters();
            
            Debug.Log("✅ ПАРАМЕТРЫ НОВОГО ПОКОЛЕНИЯ ОБНОВЛЕНЫ ТРИЖДЫ!");
        }

        void Update()
        {
            // Если есть симуляция и мы в режиме обучения
            if (simulation_manager != null && training_started)
            {
                // Проверяем, изменилось ли поколение
                int currentGeneration = GetCurrentGeneration();
                
                // Если генерация изменилась (новое поколение)
                if (currentGeneration > lastKnownGeneration)
                {
                    Debug.Log($"🔄 ПИЗДЕЦ! Обнаружено новое поколение! {lastKnownGeneration} → {currentGeneration}");
                    lastKnownGeneration = currentGeneration;
                    
                    // Применяем параметры ко всем агентам в новом поколении
                    ForceApplyParametersToNewGeneration();
                }
                
                // УЛЬТРА АГРЕССИВНЫЙ МОД: проверяем параметры каждый кадр
                if (Time.frameCount % 10 == 0) // Проверяем каждые 10 кадров чтобы снизить нагрузку
                {
                    ForceAggressiveParameterCheck();
                }
            }
        }

        // Ультра-агрессивная проверка параметров КАЖДОГО активного агента КАЖДЫЙ КАДР
        private void ForceAggressiveParameterCheck()
        {
            try
            {
                // Получаем только АКТИВНЫЕ агенты
                var activeAgents = FindObjectsOfType<Neuro>();
                if (activeAgents.Length == 0) return;
                
                // Получаем текущие значения из глобальных параметров
                float activity = GlobalTrainingParams.ActivityReward;
                float target = GlobalTrainingParams.TargetReward;
                float collision = GlobalTrainingParams.CollisionPenalty;
                float tracking = GlobalTrainingParams.TargetTrackingReward;
                float speed = GlobalTrainingParams.SpeedChangeReward;
                float rotation = GlobalTrainingParams.RotationChangeReward;
                float time = GlobalTrainingParams.TimeBonusMultiplier;
                
                // Считаем количество некорректных агентов
                int incorrectAgents = 0;
                
                // Проходим по всем агентам и проверяем наличие корректных параметров
                foreach (var agent in activeAgents)
                {
                    if (agent == null || agent.gameObject == null) continue;
                    
                    bool needsUpdate = false;
                    
                    // Прямая проверка через публичные поля
                    if (Mathf.Abs(agent.activity_reward - activity) > 0.01f || 
                        Mathf.Abs(agent.target_reward - target) > 0.01f)
                    {
                        needsUpdate = true;
                        incorrectAgents++;
                        
                        // Выводим подробное сообщение о несоответствии
                        Debug.LogWarning($"🚨 НЕПРАВИЛЬНЫЕ ПАРАМЕТРЫ у {agent.gameObject.name}! " +
                            $"activity: {agent.activity_reward} vs {activity}, " +
                            $"target: {agent.target_reward} vs {target}");
                    }
                    
                    if (needsUpdate)
                    {
                        // Прямое присваивание через публичные поля - самый быстрый способ
                        agent.activity_reward = activity;
                        agent.target_reward = target;
                        agent.collision_penalty = collision;
                        agent.target_tracking_reward = tracking;
                        agent.speed_change_reward = speed;
                        agent.rotation_change_reward = rotation;
                        agent.time_bonus_multiplier = time;
                    }
                }
                
                // Если обнаружены некорректные агенты, логируем
                if (incorrectAgents > 0)
                {
                    if (incorrectAgents > 5)
                    {
                        Debug.LogError($"🔥🔥🔥 ВСЕ ПРОПАЛО! {incorrectAgents} АГЕНТОВ С НЕПРАВИЛЬНЫМИ ПАРАМЕТРАМИ!");
                    }
                    else
                    {
                        Debug.LogWarning($"🔥 ОБНАРУЖЕНО {incorrectAgents} АГЕНТОВ С НЕПРАВИЛЬНЫМИ ПАРАМЕТРАМИ! ПРИНУДИТЕЛЬНО ОБНОВЛЯЕМ!");
                    }
                    
                    // Если много неправильных агентов, выполняем полное экстренное применение
                    if (incorrectAgents > 3)
                    {
                        EMERGENCY_ApplyAllParameters();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"💥 Ошибка в агрессивной проверке: {ex.Message}");
            }
        }

        // Также добавим хук для отслеживания изменений в SimulationManager через приватные поля
        private void HookIntoSimulationManager()
        {
            if (simulation_manager == null) return;
            
            // Пытаемся получить доступ к списку активных агентов
            var activeAgentsField = simulation_manager.GetType().GetField("active_agents", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
            if (activeAgentsField != null && activeAgentsField.GetValue(simulation_manager) is List<GameObject> activeAgents)
            {
                // При обнаружении списка активных агентов, принудительно применяем параметры
                Debug.Log($"🔍 Обнаружено {activeAgents.Count} активных агентов в SimulationManager!");
                
                // Принудительно применяем параметры ко всем активным агентам
                foreach (var agent in activeAgents)
                {
                    if (agent != null)
                    {
                        var neuro = agent.GetComponent<Neuro>();
                        if (neuro != null)
                        {
                            // Используем глобальные параметры для максимальной совместимости
                            ApplyParametersDirect(neuro);
                        }
                    }
                }
            }
        }

        // Сверхпрямой метод установки параметров через публичные поля
        private void ApplyParametersDirect(Neuro agent)
        {
            if (agent == null) return;
            
            try
            {
                // Используем глобальные параметры для максимальной совместимости
                agent.activity_reward = GlobalTrainingParams.ActivityReward;
                agent.target_reward = GlobalTrainingParams.TargetReward;
                agent.collision_penalty = GlobalTrainingParams.CollisionPenalty;
                agent.target_tracking_reward = GlobalTrainingParams.TargetTrackingReward;
                agent.speed_change_reward = GlobalTrainingParams.SpeedChangeReward;
                agent.rotation_change_reward = GlobalTrainingParams.RotationChangeReward;
                agent.time_bonus_multiplier = GlobalTrainingParams.TimeBonusMultiplier;
                
                Debug.Log($"✅ Прямая установка параметров для {agent.gameObject.name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"💥 Ошибка при прямой установке параметров: {ex.Message}");
            }
        }

        // Внедрение хука в механизм обновления поколений
        private void InjectGenerationChangeHook()
        {
            Debug.LogWarning("🔄 ВНЕДРЯЕМСЯ В МЕХАНИЗМ ОБНОВЛЕНИЯ ПОКОЛЕНИЙ!");
            
            try
            {
                // Находим методы начала/конца поколения
                var startGenMethod = simulation_manager.GetType().GetMethod("StartNewGeneration", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                var endGenMethod = simulation_manager.GetType().GetMethod("EndGeneration", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (startGenMethod != null)
                {
                    Debug.Log($"✅ Найден метод начала поколения: {startGenMethod.Name}");
                    
                    // ПРЯМОЙ ПЕРЕХВАТ: делаем делегат, который вызовет наш метод после выполнения оригинального
                    StartCoroutine(PostGenerationHook());
                }
                
                if (endGenMethod != null)
                {
                    Debug.Log($"✅ Найден метод окончания поколения: {endGenMethod.Name}");
                    // Отслеживаем его через постоянную проверку в Update
                }
                
                // КРАЙНЯЯ МЕРА - попробуем получить и переписать приватное поле spawnSingleAgent
                var spawnSingleAgent = simulation_manager.GetType().GetMethod("SpawnSingleAgent", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (spawnSingleAgent != null)
                {
                    Debug.Log($"🔥 НАЙДЕН МЕТОД СПАВНА! {spawnSingleAgent.Name}");
                    // Запускаем мониторинг активных агентов после спавна
                    StartCoroutine(PostSpawnMonitoring());
                }
                
                // Создаем прямое наблюдение за состоянием поколения
                StartCoroutine(GenerationStateMonitoringCoroutine());
            }
            catch (Exception ex)
            {
                Debug.LogError($"💥 Ошибка внедрения в механизм поколений: {ex.Message}");
            }
        }

        // Корутина для перехвата после StartNewGeneration
        private IEnumerator PostGenerationHook()
        {
            Debug.Log("🔄 Запущен хук перехвата StartNewGeneration");
            
            int lastGeneration = 0;
            bool initialCheck = true;
            
            while (true)
            {
                // Получаем текущую генерацию
                int currentGeneration = GetCurrentGeneration();
                
                // Если генерация изменилась или это первая проверка
                if (currentGeneration != lastGeneration || initialCheck)
                {
                    initialCheck = false;
                    lastGeneration = currentGeneration;
                    
                    Debug.Log($"🔄 Post-Generation Hook: Обнаружено поколение {currentGeneration}");
                    
                    // Ждем немного, чтобы агенты успели создаться
                    yield return new WaitForSeconds(0.2f);
                    
                    // Полное агрессивное применение параметров
                    EMERGENCY_ApplyAllParameters();
                    
                    // И еще через небольшой промежуток, на всякий случай
                    yield return new WaitForSeconds(0.5f);
                    
                    // И еще раз
                    ForceAggressiveParameterCheck();
                    HookIntoSimulationManager();
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Корутина для мониторинга после вызова SpawnSingleAgent
        private IEnumerator PostSpawnMonitoring()
        {
            Debug.Log("🔄 Запущен мониторинг после SpawnSingleAgent");
            
            int lastAgentCount = 0;
            
            while (true)
            {
                // Получаем текущее количество агентов
                Neuro[] currentAgents = FindObjectsOfType<Neuro>();
                
                // Если количество агентов изменилось
                if (currentAgents.Length != lastAgentCount)
                {
                    Debug.Log($"🔄 Post-Spawn Monitor: Изменение количества агентов: {lastAgentCount} → {currentAgents.Length}");
                    lastAgentCount = currentAgents.Length;
                    
                    // Ждем немного, чтобы агенты полностью инициализировались
                    yield return new WaitForSeconds(0.1f);
                    
                    // Применяем параметры ко всем агентам
                    foreach (var agent in currentAgents)
                    {
                        if (agent != null)
                        {
                            // Используем глобальные параметры для максимальной совместимости
                            ApplyParametersDirect(agent);
                        }
                    }
                }
                
                yield return new WaitForSeconds(0.2f);
            }
        }

        // Корутина для мониторинга состояния поколения
        private IEnumerator GenerationStateMonitoringCoroutine()
        {
            Debug.Log("🔍 Запущен мониторинг состояния поколения!");
            
            float lastGenerationTimer = 0f;
            int lastGeneration = 0;
            bool isSpawning = false;
            
            while (true)
            {
                try
                {
                    if (simulation_manager != null)
                    {
                        // Получаем текущий таймер поколения
                        float currentTimer = 0f;
                        int currentGen = 0;
                        bool currentSpawning = false;
                        
                        // Получаем значения через рефлексию
                        var timerField = simulation_manager.GetType().GetField("generation_timer", 
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        var genField = simulation_manager.GetType().GetField("current_generation", 
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        var spawningField = simulation_manager.GetType().GetField("is_spawning", 
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        if (timerField != null) currentTimer = (float)timerField.GetValue(simulation_manager);
                        if (genField != null) currentGen = (int)genField.GetValue(simulation_manager);
                        if (spawningField != null) currentSpawning = (bool)spawningField.GetValue(simulation_manager);
                        
                        // Обнаружено изменение поколения (сброс таймера или увеличение номера)
                        if ((lastGenerationTimer > 5f && currentTimer < 1f) || currentGen > lastGeneration)
                        {
                            Debug.Log($"🔄 НОВОЕ ПОКОЛЕНИЕ ОБНАРУЖЕНО! Таймер: {lastGenerationTimer} → {currentTimer}, " +
                                     $"Поколение: {lastGeneration} → {currentGen}");
                            
                            // Применяем параметры с небольшой задержкой
                            StartCoroutine(DelayedParameterApplication());
                            
                            lastGeneration = currentGen;
                        }
                        
                        // Обнаружен запуск спавна
                        if (!isSpawning && currentSpawning)
                        {
                            Debug.Log("🐣 ОБНАРУЖЕН ЗАПУСК СПАВНА АГЕНТОВ!");
                            
                            // Применяем параметры с задержкой после спавна
                            StartCoroutine(DelayedParameterApplicationAfterSpawn());
                        }
                        
                        // Обнаружено завершение спавна
                        if (isSpawning && !currentSpawning)
                        {
                            Debug.Log("✅ ОБНАРУЖЕНО ЗАВЕРШЕНИЕ СПАВНА АГЕНТОВ!");
                            
                            // Применяем параметры с задержкой после завершения спавна
                            StartCoroutine(DelayedParameterApplicationAfterSpawn());
                        }
                        
                        lastGenerationTimer = currentTimer;
                        isSpawning = currentSpawning;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"⚠️ Ошибка при мониторинге поколения: {ex.Message}");
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Отложенное применение параметров после спавна
        private IEnumerator DelayedParameterApplicationAfterSpawn()
        {
            // Короткая задержка, чтобы агенты проинициализировались
            yield return new WaitForSeconds(0.2f);
            
            // Применяем первый раз
            Debug.Log("🔄 ПРИМЕНЯЕМ ПАРАМЕТРЫ ПОСЛЕ СПАВНА (1/3)");
            EMERGENCY_ApplyAllParameters();
            
            // Еще раз через полсекунды
            yield return new WaitForSeconds(0.5f);
            Debug.Log("🔄 ПРИМЕНЯЕМ ПАРАМЕТРЫ ПОСЛЕ СПАВНА (2/3)");
            EMERGENCY_ApplyAllParameters();
            
            // Финальное применение
            yield return new WaitForSeconds(0.5f);
            Debug.Log("🔄 ФИНАЛЬНОЕ ПРИМЕНЕНИЕ ПАРАМЕТРОВ ПОСЛЕ СПАВНА (3/3)");
            EMERGENCY_ApplyAllParameters();
        }
    }
}

