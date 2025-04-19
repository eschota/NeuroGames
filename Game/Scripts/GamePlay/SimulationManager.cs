using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEditor;
using System.Linq;

namespace Game.Scripts.GamePlay
{
    [System.Serializable]
    public class NetworkSaveData
    {
        public string network_json; // Теперь храним полную сериализованную сеть как строку
        public int saved_generation;
        public float best_fitness;
        public float best_distance;
        public int total_successes;
        public string save_date;
    }

    public class SimulationManager : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [SerializeField] private GameObject agent_prefab;
        [SerializeField] private string agent_prefab_path = "Prefabs/Agent"; // Путь к префабу в Resources
        [SerializeField] private Transform spawn_point;
        [SerializeField] private float generation_time = 10f;
        [SerializeField] private int min_fps_threshold = 60;
        [SerializeField] private int initial_agents_count = 1;
        [SerializeField] private string model_save_path = "best_neural_model.json";
        
        [Header("Victory Conditions")]
        [SerializeField] private int validation_interval = 10; // Каждые сколько поколений проводить проверку
        [SerializeField] private float victory_success_rate = 0.9f; // 90% успешных агентов для победы
        [SerializeField] private bool is_validation_round = false; // Флаг текущего раунда
        [SerializeField] private bool game_won = false; // Флаг победы
        
        [Header("Logging")]
        [SerializeField] private string log_filename = "gameplay_log.txt";
        private string log_path;
        private System.Text.StringBuilder log_buffer = new System.Text.StringBuilder();
        
        [Header("Genetic Algorithm")]
        [SerializeField] private float initial_mutation_rate = 0.1f;
        [SerializeField] private float min_mutation_rate = 0.01f;
        [SerializeField] private float mutation_decay = 0.995f;
        
        // Конфигурация нейросети
        [SerializeField] private int[] neural_layers = new int[] { 12, 16, 12, 8, 2 }; // Дефолтная конфигурация
        [SerializeField] private int[] hidden_layers = new int[] { 16, 12, 8 };
        
        [SerializeField] private int tournament_size = 5;
        [SerializeField] private float elite_percent = 0.15f;
        [SerializeField] private float crossover_rate = 0.8f;
        
        // Динамически сконфигурированные слои
        private string network_config_hash;
        
        [Header("Graph Settings")]
        [SerializeField] private int max_history_points = 30; // Количество точек для отображения
        [SerializeField] private Color graph_line_color = Color.green;
        [SerializeField] private Color graph_bg_color = new Color(0, 0, 0, 0.5f);
        [SerializeField] private Vector2 graph_size = new Vector2(300, 150);
        
        [Header("Save Settings")]
        [SerializeField] private string snapshots_folder = "snapshots";
        [SerializeField] private int snapshot_interval = 10; // Сохранять каждые 10 поколений
        [SerializeField] private int max_snapshots = 10; // Максимальное количество снапшотов
        
        // Stats tracking
        private int current_generation = 0;
        private float generation_timer = 0f;
        private int current_agents_count;
        private float current_fps = 0f;
        private bool increasing_agents = true;
        
        // FPS calculation
        private const float FPS_MEASURE_PERIOD = 0.5f;
        private int fps_accumulator = 0;
        private float fps_next_period = 0f;
        
        // Agent tracking
        private List<GameObject> active_agents = new List<GameObject>();
        private List<NeuralNetwork> successful_networks = new List<NeuralNetwork>();
        private NeuralNetwork best_network = null;
        
        // Enhanced statistics tracking
        private float best_distance_ever = float.MaxValue;
        private float best_distance_current_gen = float.MaxValue;
        private float avg_distance_last_gen = 0f;
        private int total_successes_ever = 0;
        private int successes_last_gen = 0;
        private GameObject best_agent_current = null;
        
        // Color definitions for agents
        private readonly Color NORMAL_AGENT_COLOR = new Color(0.7f, 0.7f, 0.7f); // Светло-серый для случайных
        private readonly Color BEST_AGENT_COLOR = new Color(1f, 0.8f, 0f);       // Золотой для элиты
        private readonly Color MUTATED_BEST_COLOR = new Color(0.3f, 0.8f, 1f);   // Голубой для кроссовера
        private readonly Color MUTATION_COLOR = new Color(0.8f, 0.4f, 1f);       // Фиолетовый для мутаций
        
        // История успехов для графика
        private List<int> success_history = new List<int>();
        private int max_success_count = 1; // Максимальное количество успехов (для масштабирования)
        
        // Добавляем переменные для отслеживания постепенного спавна
        private Queue<int> agents_to_spawn = new Queue<int>();
        private float generation_start_time;
        private bool is_spawning = false;

        // Добавляем информацию о сохранённой сети
        private NetworkSaveData current_save_data;
        private bool has_loaded_network = false;

        // Добавляем ссылку на пул агентов
        private Transform agents_pool;

        private float current_mutation_rate;
        private float best_fitness_ever = float.MinValue;
        private int generations_without_improvement = 0;
        private const int RESET_THRESHOLD = 10; // После скольки поколений без улучшений увеличивать мутацию

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
        [SerializeField] private int window_title_font_size = 24;
        [SerializeField] private int param_label_font_size = 22;
        [SerializeField] private int input_font_size = 22;
        [SerializeField] private int start_button_font_size = 28;
        [SerializeField] private int speed_button_font_size = 24;

        [Header("Speed Buttons")]
        [SerializeField] private float speed_button_height = 45f;
        [SerializeField] private float speed_button_spacing = 10f;

        // Training control
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
        private readonly Dictionary<string, string> param_names = new Dictionary<string, string>()
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

        void Awake()
        {
            // Конфигурируем нейросеть в Awake, до всего остального
            ConfigureNeuralNetwork();
            
            if (neural_layers == null || neural_layers.Length < 2)
            {
                Debug.LogError("❌ КРИТИЧЕСКАЯ ОШИБКА: Неверная конфигурация слоёв нейросети в Awake!");
                enabled = false;
            }
            else
            {
                Debug.Log($"✅ Нейросеть успешно сконфигурирована в Awake: {string.Join(" → ", neural_layers)}");
            }
        }

        void Start()
        {
            try
            {
                // Сбрасываем все флаги и состояния
                current_mutation_rate = initial_mutation_rate;
                game_won = false;
                is_validation_round = false;
                current_generation = 0;
                best_distance_ever = float.MaxValue;
                total_successes_ever = 0;
                has_loaded_network = false;
                best_network = null;
                current_save_data = null;
                success_history.Clear();
                successful_networks.Clear();
                
                // Инициализируем и очищаем лог
                log_path = Path.Combine(Application.dataPath, "Game", snapshots_folder, log_filename);
                try 
                {
                    // Очищаем старый лог
                    File.WriteAllText(log_path, "");
                    LogGameplayEvent($"=== Начало новой сессии обучения ===\n" +
                                   $"Сцена: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
                                   $"Время: {DateTime.Now}\n" +
                                   $"Конфигурация сети: {string.Join(" → ", neural_layers)}\n" +
                                   $"Начальное количество агентов: {initial_agents_count}\n" +
                                   $"Условие победы: {(victory_success_rate * 100):F0}% успешных агентов\n");
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка при очистке лога: {e.Message}");
                }

                // Создаём папку для снапшотов, если её нет
                string full_snapshots_path = Path.Combine(Application.dataPath, "Game", snapshots_folder);
                try
                {
                    if (!Directory.Exists(full_snapshots_path))
                    {
                        Directory.CreateDirectory(full_snapshots_path);
                        Debug.Log($"📁 Создана папка для снапшотов: {full_snapshots_path}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Не удалось создать папку снапшотов: {e.Message}");
                }

                // Создаём пул для агентов
                GameObject pool = new GameObject("Agents_Pool");
                agents_pool = pool.transform;
                Debug.Log("🏊 Создан пул для агентов");

                // Verify target setup
                VerifyTargetSetup();
                
                // Try to load the best network
                LoadBestNetwork();
                
                // Initialize generation
                current_agents_count = initial_agents_count;
                StartNewGeneration();

                // Инициализируем UI для параметров обучения
                InitTrainingUI();

                // Ставим игру на паузу до начала обучения
                Time.timeScale = 0f;
                training_started = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Критическая ошибка в Start(): {e.Message}\n{e.StackTrace}");
                enabled = false;
            }
        }
        
        void Update()
        {
            // Если обучение не начато - пропускаем обновление
            if (!training_started) return;
            
            // Update FPS counter
            UpdateFPS();
            
            // Update generation timer
            generation_timer -= Time.deltaTime;
            
            // Check if generation is complete
            if (generation_timer <= 0)
            {
                EndGeneration();
            }
        }

        void OnGUI()
        {
            int padding = 10;
            int width = 300;
            int height = 25;
            
            GUI.skin.label.fontSize = 16;
            GUI.skin.label.normal.textColor = Color.white;
            
            // Create background box for stats
            GUI.Box(new Rect(padding, padding, width + padding * 2, 460), ""); // Увеличили высоту для новой инфы
            
            // Display statistics
            int y = padding * 2;
            
            // Добавляем информацию о загруженной сети в начало
            if (has_loaded_network && current_save_data != null)
            {
                GUI.contentColor = Color.green;
                GUI.Label(new Rect(padding * 2, y, width, height), 
                    $"💾 Loaded Network Gen: {current_save_data.saved_generation}");
                y += height;
                GUI.Label(new Rect(padding * 2, y, width, height), 
                    $"📅 Save Date: {current_save_data.save_date}");
                y += height;
                GUI.Label(new Rect(padding * 2, y, width, height), 
                    $"🏆 Saved Best Fitness: {current_save_data.best_fitness:F2}");
                y += height + 5; // Дополнительный отступ после блока информации о сохранении
                GUI.contentColor = Color.white;
            }
            else
            {
                GUI.contentColor = Color.yellow;
                GUI.Label(new Rect(padding * 2, y, width, height), 
                    "🆕 Using New Network (No Save)");
                y += height + 5;
                GUI.contentColor = Color.white;
            }

            // Остальная статистика
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
                $"Best Distance Current: {best_distance_current_gen:F2}m");
                
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
            y += height + 10; // Дополнительный отступ для визуального разделения
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"🧠 Neural Network Info:");
                
            y += height;
            // Показываем структуру слоёв безопасно
            string layers_str = "Not configured";
            if (neural_layers != null && neural_layers.Length > 0)
            {
                layers_str = string.Join(" → ", neural_layers);
            }
            GUI.Label(new Rect(padding * 2, y, width, height), 
                $"Structure: {layers_str}");
                
            y += height;
            // Считаем общее количество параметров (весов) в сети
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

            // Рисуем UI параметров обучения
            DrawTrainingUI();
        }
        
        private void VerifyTargetSetup()
        {
            GameObject target = GameObject.FindGameObjectWithTag("AIM");
            if (target == null)
            {
                Debug.LogError("❌ КРИТИЧЕСКАЯ ОШИБКА: Цель с тегом 'AIM' не найдена в сцене!");
                return;
            }
            
            Debug.Log($"✅ Проверка цели успешна! Найдена на позиции: {target.transform.position}");
        }
        
        private void LoadBestNetwork()
        {
            // Сначала пробуем загрузить current_gen.json из папки снапшотов
            string current_gen_path = GetSnapshotPath("current_gen.json");
            
            if (File.Exists(current_gen_path))
            {
                try
                {
                    string json = File.ReadAllText(current_gen_path);
                    current_save_data = JsonUtility.FromJson<NetworkSaveData>(json);
                    
                    // Десериализуем полную нейросеть из JSON
                    best_network = NeuralNetwork.FromJson(current_save_data.network_json);
                    
                    best_distance_ever = current_save_data.best_distance;
                    total_successes_ever = current_save_data.total_successes;
                    has_loaded_network = true;
                    
                    Debug.Log($"🎯 Загружена лучшая нейросеть из поколения {current_save_data.saved_generation}!");
                    Debug.Log($"Параметры: Фитнес = {current_save_data.best_fitness:F2}, " +
                             $"Лучшая дистанция = {current_save_data.best_distance:F2}м");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка загрузки current_gen.json: {e.Message}");
                }
            }
            
            // Если не удалось загрузить из снапшотов, пробуем стандартный путь
            string filepath = Application.persistentDataPath + "/" + model_save_path;
            if (File.Exists(filepath))
            {
                try
                {
                    string json = File.ReadAllText(filepath);
                    current_save_data = JsonUtility.FromJson<NetworkSaveData>(json);
                    
                    // Десериализуем полную нейросеть из JSON
                    best_network = NeuralNetwork.FromJson(current_save_data.network_json);
                    
                    best_distance_ever = current_save_data.best_distance;
                    total_successes_ever = current_save_data.total_successes;
                    has_loaded_network = true;
                    
                    Debug.Log($"🎯 Загружена лучшая нейросеть из резервного файла, поколение {current_save_data.saved_generation}!");
                    
                    // Сразу сохраняем в папку снапшотов
                    File.WriteAllText(current_gen_path, json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка загрузки нейросети: {e.Message}");
                    best_network = null;
                    current_save_data = null;
                    has_loaded_network = false;
                }
            }
            else
            {
                Debug.Log("🆕 Файл сохранения не найден, начинаем с новой сети!");
                best_network = null;
                current_save_data = null;
                has_loaded_network = false;
            }
        }
        
        private void StartNewGeneration()
        {
            current_generation++;
            
            // Проверяем условия для валидационного раунда (каждые validation_interval поколений)
            is_validation_round = !game_won && ((current_generation % validation_interval) == 0);
            
            if (is_validation_round)
            {
                LogGameplayEvent($"\n🎯 ВАЛИДАЦИОННЫЙ РАУНД {current_generation} 🎯");
                G.CurrentState = G.State.Validation;
                
                // Сохраняем копию успешных сетей для валидации
                List<NeuralNetwork> validation_networks = new List<NeuralNetwork>();
                foreach (var network in successful_networks)
                {
                    validation_networks.Add(new NeuralNetwork(network));
                }
                
                if (validation_networks.Count == 0)
                {
                    LogGameplayEvent("❌ Нет успешных агентов для валидации, пропускаем раунд.");
                    is_validation_round = false;
                    G.CurrentState = G.State.Playing;
                }
                else
                {
                    LogGameplayEvent($"Найдено успешных сетей: {validation_networks.Count}");
                    LogGameplayEvent($"Будет создано агентов: {current_agents_count}");
                    LogGameplayEvent($"Каждая успешная сеть будет использована примерно {(float)current_agents_count / validation_networks.Count:F1} раз");
                    
                    // Заменяем список успешных сетей на валидационные копии
                    successful_networks = validation_networks;
                }
            }
            
            generation_timer = generation_time;
            generation_start_time = Time.time;
            
            // Clear any existing agents
            ClearAgents();
            
            // Подготавливаем очередь агентов для спавна
            agents_to_spawn.Clear();
            for (int i = 0; i < current_agents_count; i++)
            {
                agents_to_spawn.Enqueue(i);
            }
            
            is_spawning = true;
            string gen_type = is_validation_round ? "VALIDATION ROUND" : "обычное поколение";
            LogGameplayEvent($"Поколение {current_generation}: {current_agents_count} агентов ({gen_type})");
        }
        
        void FixedUpdate()
        {
            // Спавним по одному агенту каждый FixedUpdate, пока есть агенты в очереди
            if (is_spawning && agents_to_spawn.Count > 0)
            {
                int agent_id = agents_to_spawn.Dequeue();
                SpawnSingleAgent(agent_id);
                
                if (agents_to_spawn.Count == 0)
                {
                    is_spawning = false;
                    Debug.Log("All agents spawned sequentially!");
                }
            }
        }
        
        private void SpawnSingleAgent(int agent_id)
        {
            // Не спавним агентов, если обучение не начато
            if (!training_started) return;
            
            try
            {
                // Проверяем и при необходимости перезагружаем префаб
                ValidateAgentPrefab();
                
                if (agent_prefab == null)
                {
                    LogGameplayEvent($"❌ Невозможно создать агента {agent_id}: префаб не найден!");
                    return;
                }

                // ВСЕГДА спавним в нуле
                Vector3 spawn_position = Vector3.zero;
                Quaternion spawn_rotation = Quaternion.Euler(0, 0, 0);
                
                // Создаём агента
                GameObject agent = null;
                try
                {
                    agent = Instantiate(agent_prefab, spawn_position, spawn_rotation, agents_pool);
                }
                catch (Exception e)
                {
                    LogGameplayEvent($"❌ Ошибка при создании агента {agent_id}: {e.Message}");
                    return;
                }
                
                if (agent == null)
                {
                    LogGameplayEvent($"❌ Агент {agent_id} не был создан!");
                    return;
                }

                // Проверяем и форсируем позицию
                if (agent.transform.position != Vector3.zero)
                {
                    LogGameplayEvent($"🚨 Агент {agent_id} создался не в нуле! Позиция: {agent.transform.position}. Исправляем...");
                    agent.transform.position = Vector3.zero;
                }
                
                // Задаём имя с индексом поколения и ID
                agent.name = $"Agent_Gen{current_generation:D4}_ID{agent_id:D4}";
                
                // Получаем компоненты
                Rigidbody rb = agent.GetComponentInChildren<Rigidbody>();
                Neuro neuro = agent.GetComponent<Neuro>();
                
                // Ищем все рендереры в агенте и его детях
                Renderer[] renderers = agent.GetComponentsInChildren<Renderer>();
                
                if (rb != null)
                {
                    // Сбрасываем все физические параметры
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = Vector3.zero;
                    rb.rotation = spawn_rotation;
                    
                    // Временно отключаем и включаем физику для сброса всех сил
                    rb.isKinematic = true;
                    rb.isKinematic = false;
                }
                
                if (neuro != null)
                {
                    neuro.instance_id = agent_id;
                    
                    Color agent_color = Color.white; // Цвет по умолчанию
                    
                    if (is_validation_round && successful_networks.Count > 0)
                    {
                        // В валидационном раунде - используем только успешные сети
                        int network_idx = agent_id % successful_networks.Count;
                        NeuralNetwork copy = new NeuralNetwork(successful_networks[network_idx]);
                        // НЕ мутируем сети в валидационном раунде!
                        neuro.SetNeuralNetwork(copy);
                        
                        // Фиолетовый цвет для валидационных агентов
                        agent_color = new Color(0.8f, 0.2f, 0.8f);
                        LogGameplayEvent($"🔮 Валидационный агент {agent_id}: Использует сеть #{network_idx} из {successful_networks.Count} успешных");
                    }
                    else if (!is_validation_round)
                    {
                        // Стандартная логика для обычных раундов
                        int elite_count = Mathf.Max(1, Mathf.FloorToInt(current_agents_count * elite_percent));
                        if (agent_id < elite_count && best_network != null)
                        {
                            // Элитные особи
                            NeuralNetwork copy = new NeuralNetwork(best_network);
                            copy.Mutate(current_mutation_rate * 0.1f);
                            neuro.SetNeuralNetwork(copy);
                            agent_color = BEST_AGENT_COLOR;
                            if (agent_id == 0) best_agent_current = agent;
                        }
                        else if (best_network != null && successful_networks.Count > 0)
                        {
                            if (UnityEngine.Random.value < crossover_rate)
                            {
                                // Кроссовер
                                NeuralNetwork parent1 = TournamentSelection();
                                NeuralNetwork parent2 = TournamentSelection();
                                
                                NeuralNetwork child = CrossoverNetworks(parent1, parent2);
                                child.Mutate(current_mutation_rate);
                                neuro.SetNeuralNetwork(child);
                                agent_color = MUTATED_BEST_COLOR;
                            }
                            else
                            {
                                // Мутация
                                NeuralNetwork parent = TournamentSelection();
                                NeuralNetwork copy = new NeuralNetwork(parent);
                                copy.Mutate(current_mutation_rate * 1.5f);
                                neuro.SetNeuralNetwork(copy);
                                agent_color = MUTATION_COLOR;
                            }
                        }
                        else
                        {
                            // Случайная сеть
                            GeneticAlgorithm genetic = new GeneticAlgorithm();
                            genetic.neural_layers = neural_layers;
                            neuro.SetNeuralNetwork(genetic.CreateRandomNetwork());
                            agent_color = NORMAL_AGENT_COLOR;
                        }
                    }
                    
                    // Применяем цвет ко всем рендерерам
                    foreach (var renderer in renderers)
                    {
                        if (renderer != null && renderer.material != null)
                        {
                            renderer.material.color = agent_color;
                            // Убираем нахер спам из логов
                            //LogGameplayEvent($"Установлен цвет {agent_color} для рендерера {renderer.name} агента {agent_id}");
                        }
                    }
                    
                    // Устанавливаем время начала
                    neuro.SetStartTime(generation_start_time);
                }
                
                // Финальная проверка позиции
                if (agent.transform.position != Vector3.zero)
                {
                    LogGameplayEvent($"❌ КРИТИЧЕСКАЯ ОШИБКА: Агент {agent_id} всё ещё не в нуле после всех проверок! " +
                                   $"Позиция: {agent.transform.position}");
                    agent.transform.position = Vector3.zero;
                }
                
                active_agents.Add(agent);
            }
            catch (Exception e)
            {
                LogGameplayEvent($"❌ Ошибка при спавне агента {agent_id}: {e.Message}\n{e.StackTrace}");
            }
        }
        
        // Турнирный отбор
        private NeuralNetwork TournamentSelection()
        {
            if (successful_networks.Count == 0) return null;
            
            NeuralNetwork best = null;
            float best_fitness = float.MinValue;
            
            // Проводим турнир
            for (int i = 0; i < tournament_size; i++)
            {
                int idx = UnityEngine.Random.Range(0, successful_networks.Count);
                NeuralNetwork contestant = successful_networks[idx];
                
                if (contestant.fitness > best_fitness)
                {
                    best = contestant;
                    best_fitness = contestant.fitness;
                }
            }
            
            return best ?? successful_networks[0];
        }

        // Кроссовер двух нейросетей
        private NeuralNetwork CrossoverNetworks(NeuralNetwork parent1, NeuralNetwork parent2)
        {
            if (parent1 == null || parent2 == null) return null;
            
            // Создаём потомка с такой же структурой
            NeuralNetwork child = new NeuralNetwork(neural_layers);
            
            // Для каждого слоя весов
            for (int i = 0; i < child.weights.Length; i++)
            {
                for (int j = 0; j < child.weights[i].Length; j++)
                {
                    for (int k = 0; k < child.weights[i][j].Length; k++)
                    {
                        // Случайно выбираем вес от одного из родителей
                        if (UnityEngine.Random.value < 0.5f)
                        {
                            child.weights[i][j][k] = parent1.weights[i][j][k];
                        }
                        else
                        {
                            child.weights[i][j][k] = parent2.weights[i][j][k];
                        }
                    }
                }
            }
            
            return child;
        }
        
        private void EndGeneration()
        {
            // Не заканчиваем поколение, если обучение не начато
            if (!training_started) return;

            CalculateGenerationStats();
            
            float success_rate = (float)successful_networks.Count / current_agents_count;
            
            if (is_validation_round)
            {
                LogGameplayEvent($"\nРезультаты валидации (поколение {current_generation}):\n" +
                               $"Успешных агентов: {successful_networks.Count} из {current_agents_count} ({success_rate:P1})\n" +
                               $"Лучшая дистанция: {best_distance_current_gen:F2}м\n" +
                               $"Средняя дистанция: {avg_distance_last_gen:F2}м");
                
                if (success_rate >= victory_success_rate)
                {
                    game_won = true;
                    LogGameplayEvent($"\n🏆 ПОБЕДА! {success_rate:P1} агентов достигли цели!\n" +
                                   $"Обучение завершено на поколении {current_generation}");
                    
                    SaveVictoryNetwork();
                    G.CurrentState = G.State.Win;
                    return;
                }
                else
                {
                    LogGameplayEvent($"Валидация не пройдена. Нужно: {victory_success_rate:P0}, получено: {success_rate:P1}");
                }
            }
            
            // Сохраняем успешные сети независимо от типа раунда
            SaveSuccessfulNetworks();
            
            // Обновляем историю успехов
            success_history.Add(successful_networks.Count);
            if (success_history.Count > max_history_points)
            {
                success_history.RemoveAt(0);
            }
            
            // Обновляем максимальное количество успехов для масштабирования графика
            max_success_count = Mathf.Max(max_success_count, successful_networks.Count);
            
            // Обновляем количество успехов последнего поколения
            successes_last_gen = successful_networks.Count;
            
            // Сбрасываем лучшую дистанцию для следующего поколения
            best_distance_current_gen = float.MaxValue;
            best_agent_current = null;
            
            // Решаем, увеличивать ли количество агентов
            if (!is_validation_round) // Не меняем количество агентов в валидационном раунде
            {
                if (increasing_agents && current_fps >= min_fps_threshold)
                {
                    current_agents_count++;
                    Debug.Log($"Increasing agents to {current_agents_count} for next generation");
                }
                else if (current_fps < min_fps_threshold)
                {
                    increasing_agents = false;
                    Debug.Log($"FPS dropped below threshold ({current_fps:F1} FPS). Stopping agent increase.");
                }
            }
            
            // Начинаем новое поколение, если игра не выиграна
            if (!game_won)
            {
                StartNewGeneration();
            }
        }
        
        private void CalculateGenerationStats()
        {
            float total_distance = 0f;
            int valid_agents = 0;

            foreach (GameObject agent in active_agents)
            {
                if (agent != null)
                {
                    Neuro neuro = agent.GetComponent<Neuro>();
                    if (neuro != null)
                    {
                        float distance = neuro.GetDistanceToTarget();
                        if (distance >= 0) // Valid distance
                        {
                            total_distance += distance;
                            valid_agents++;
                            
                            // Update best distances
                            if (distance < best_distance_current_gen)
                            {
                                best_distance_current_gen = distance;
                                if (distance < best_distance_ever)
                                {
                                    best_distance_ever = distance;
                                }
                            }
                        }
                    }
                }
            }

            // Calculate average distance
            if (valid_agents > 0)
            {
                avg_distance_last_gen = total_distance / valid_agents;
            }
        }
        
        private void SaveSuccessfulNetworks()
        {
            // If there were successful agents, update the best network
            if (successful_networks.Count > 0)
            {
                // Find the network with the highest fitness
                NeuralNetwork highestFitness = successful_networks[0];
                foreach (NeuralNetwork network in successful_networks)
                {
                    if (network.fitness > highestFitness.fitness)
                    {
                        highestFitness = network;
                    }
                }
                
                // Update best network
                best_network = new NeuralNetwork(highestFitness);
                
                // Save to file
                SaveBestNetwork();
            }
        }
        
        private void SaveBestNetwork()
        {
            if (best_network == null) return;
            
            try
            {
                // Сериализуем всю нейросеть в JSON
                string network_json = best_network.ToJson();
                
                // Создаём или обновляем данные сохранения
                if (current_save_data == null)
                {
                    current_save_data = new NetworkSaveData();
                }
                
                // Сохраняем полную сериализованную сеть
                current_save_data.network_json = network_json;
                current_save_data.saved_generation = current_generation;
                current_save_data.best_fitness = best_network.fitness;
                current_save_data.best_distance = best_distance_ever;
                current_save_data.total_successes = total_successes_ever;
                current_save_data.save_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                string full_json = JsonUtility.ToJson(current_save_data);
                
                // Сохраняем текущую лучшую сеть в папку снапшотов
                string current_gen_path = GetSnapshotPath("current_gen.json");
                File.WriteAllText(current_gen_path, full_json);
                
                // Сохраняем снапшот каждые N поколений
                if (current_generation % snapshot_interval == 0)
                {
                    SaveGenerationSnapshot(full_json);
                }
                
                // Сохраняем также в стандартное место
                File.WriteAllText(Application.persistentDataPath + "/" + model_save_path, full_json);
                
                has_loaded_network = true;
                Debug.Log($"💾 Сохранена лучшая нейросеть поколения {current_generation} (размер JSON: {full_json.Length} байт)");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка сохранения нейросети: {e.Message}");
            }
        }

        private void SaveGenerationSnapshot(string json)
        {
            try
            {
                // Создаём имя файла для текущего поколения
                string snapshot_filename = $"gen_{current_generation.ToString("D6")}.json";
                string snapshot_path = GetSnapshotPath(snapshot_filename);
                
                // Сохраняем текущий снапшот
                File.WriteAllText(snapshot_path, json);
                
                // Получаем список всех снапшотов (кроме current_gen.json)
                var snapshots = Directory.GetFiles(GetSnapshotPath(), "gen_*.json")
                                       .OrderBy(f => f)
                                       .ToList();
                
                // Если снапшотов больше максимума, удаляем старые
                while (snapshots.Count > max_snapshots)
                {
                    string oldest = snapshots[0];
                    File.Delete(oldest);
                    snapshots.RemoveAt(0);
                    Debug.Log($"🗑️ Удалён старый снапшот: {Path.GetFileName(oldest)}");
                }
                
                Debug.Log($"📸 Создан снапшот поколения {current_generation}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка создания снапшота: {e.Message}");
            }
        }
        
        private void UpdateFPS()
        {
            // Increment counter each frame
            fps_accumulator++;
            
            if (Time.realtimeSinceStartup > fps_next_period)
            {
                // Calculate FPS for the period
                current_fps = fps_accumulator / FPS_MEASURE_PERIOD;
                
                // Reset for next period
                fps_accumulator = 0;
                fps_next_period = Time.realtimeSinceStartup + FPS_MEASURE_PERIOD;
            }
        }
        
        // Called when an agent successfully finds the target
        public void ReportSuccess(Neuro successful_agent)
        {
            if (successful_agent == null)
            {
                Debug.LogError("ReportSuccess called with null agent!");
                return;
            }

            Debug.Log($"Success reported by agent {successful_agent.instance_id}!");
            
            // Increment total successes
            total_successes_ever++;
            
            // Add this agent's network to the successful list
            NeuralNetwork brain = successful_agent.GetBrain();
            if (brain == null)
            {
                Debug.LogError($"Agent {successful_agent.instance_id} reported success but has no brain!");
                return;
            }

            try
            {
                // Make a copy of the network
                NeuralNetwork copy = new NeuralNetwork(brain);
                successful_networks.Add(copy);
                Debug.Log($"Successfully copied brain from agent {successful_agent.instance_id} with fitness {brain.fitness}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to copy brain from agent {successful_agent.instance_id}: {e.Message}\n{e.StackTrace}");
            }
        }

        private void DrawSuccessGraph()
        {
            // Вычисляем позицию графика в правом верхнем углу с отступом 10 пикселей
            float graphX = Screen.width - graph_size.x - 10;
            float graphY = 10; // Отступ сверху

            // Draw graph background
            GUI.color = graph_bg_color;
            GUI.Box(new Rect(graphX, graphY, graph_size.x, graph_size.y), "");
            GUI.color = Color.white;

            // Draw graph title
            GUI.Label(new Rect(graphX, graphY - 20, graph_size.x, 20), 
                $"Success History (Max: {max_success_count})");

            // Создаём текстуру для линий
            if (lineTex == null)
            {
                lineTex = new Texture2D(1, 1);
                lineTex.SetPixel(0, 0, Color.white);
                lineTex.Apply();
            }

            // Draw coordinate system
            DrawLine(new Vector2(graphX, graphY + graph_size.y),
                    new Vector2(graphX, graphY),
                    Color.gray); // Y axis
                    
            DrawLine(new Vector2(graphX, graphY + graph_size.y),
                    new Vector2(graphX + graph_size.x, graphY + graph_size.y),
                    Color.gray); // X axis

            // Draw grid lines
            Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            int gridLines = 5;
            for (int i = 1; i < gridLines; i++)
            {
                float y = graphY + (i * graph_size.y / gridLines);
                DrawLine(new Vector2(graphX, y),
                        new Vector2(graphX + graph_size.x, y),
                        gridColor);
                
                float x = graphX + (i * graph_size.x / gridLines);
                DrawLine(new Vector2(x, graphY),
                        new Vector2(x, graphY + graph_size.y),
                        gridColor);
                
                // Draw Y axis labels
                int value = (gridLines - i) * max_success_count / gridLines;
                GUI.Label(new Rect(graphX - 30, y - 10, 25, 20), value.ToString());
            }

            // Draw graph
            if (success_history.Count > 1)
            {
                for (int i = 0; i < success_history.Count - 1; i++)
                {
                    float x1 = graphX + (i * graph_size.x / (max_history_points - 1));
                    float x2 = graphX + ((i + 1) * graph_size.x / (max_history_points - 1));
                    
                    float y1 = graphY + graph_size.y - 
                              (success_history[i] * graph_size.y / Mathf.Max(1, max_success_count));
                    float y2 = graphY + graph_size.y - 
                              (success_history[i + 1] * graph_size.y / Mathf.Max(1, max_success_count));
                    
                    DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), graph_line_color);
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

        // Вспомогательный метод для подсчёта параметров сети
        private int CalculateTotalParameters()
        {
            int total = 0;
            if (neural_layers != null && neural_layers.Length > 1)
            {
                // Для каждого слоя (кроме входного) считаем количество весов
                for (int i = 1; i < neural_layers.Length; i++)
                {
                    // Количество весов = нейроны текущего слоя * нейроны предыдущего слоя
                    total += neural_layers[i] * neural_layers[i - 1];
                }
            }
            return total;
        }

        private void ClearAgents()
        {
            foreach (GameObject agent in active_agents)
            {
                if (agent != null)
                {
                    Destroy(agent);
                }
            }
            active_agents.Clear();
            
            // НЕ очищаем successful_networks если это валидационный раунд!
            if (!is_validation_round)
            {
                successful_networks.Clear();
            }

            // Очищаем имя пула, добавляя номер следующего поколения
            if (agents_pool != null)
            {
                agents_pool.name = $"Agents_Pool_Gen{(current_generation + 1):D4}";
            }
        }

        private string GetSnapshotPath(string filename = "")
        {
            return Path.Combine(Application.dataPath, "Game", snapshots_folder, filename);
        }

        void OnDestroy()
        {
            // Очищаем текстуру при уничтожении
            if (lineTex != null)
            {
                Destroy(lineTex);
                lineTex = null;
            }

            // Сохраняем оставшиеся логи
            if (log_buffer.Length > 0)
            {
                try
                {
                    File.AppendAllText(log_path, log_buffer.ToString());
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка сохранения финального лога: {e.Message}");
                }
            }
            
            // Подчищаем за собой при выходе
            if (agents_pool != null)
            {
                Destroy(agents_pool.gameObject);
            }
        }

        // Метод для получения текущей конфигурации слоёв
        public int[] GetNeuralLayers()
        {
            // Используем дефолтную конфигурацию если что-то пошло не так
            if (neural_layers == null || neural_layers.Length < 2)
            {
                Debug.LogWarning("⚠️ GetNeuralLayers: Используем дефолтную конфигурацию слоёв!");
                return new int[] { 12, 16, 12, 8, 2 };
            }

            // Возвращаем копию массива
            int[] layers_copy = new int[neural_layers.Length];
            Array.Copy(neural_layers, layers_copy, neural_layers.Length);
            return layers_copy;
        }

        private void ValidateAgentPrefab()
        {
            if (agent_prefab == null)
            {
                Debug.LogWarning("🔄 Префаб агента не задан или был уничтожен, пробуем загрузить из Resources...");
                
                // Пробуем загрузить из Resources
                agent_prefab = Resources.Load<GameObject>(agent_prefab_path);
                
                if (agent_prefab == null)
                {
                    Debug.LogError($"❌ КРИТИЧЕСКАЯ ОШИБКА: Не удалось загрузить префаб агента по пути {agent_prefab_path}!");
                    return;
                }
                
                Debug.Log("✅ Префаб агента успешно перезагружен!");
            }
        }

        private void SaveVictoryNetwork()
        {
            try
            {
                // Находим лучшую сеть среди успешных
                NeuralNetwork best_victory_network = null;
                float best_fitness = float.MinValue;
                
                foreach (NeuralNetwork network in successful_networks)
                {
                    if (network.fitness > best_fitness)
                    {
                        best_fitness = network.fitness;
                        best_victory_network = network;
                    }
                }
                
                if (best_victory_network != null)
                {
                    string victory_path = Path.Combine(Application.dataPath, "Game", "victory_network.json");
                    
                    // Создаём данные для сохранения
                    NetworkSaveData victory_data = new NetworkSaveData
                    {
                        network_json = best_victory_network.ToJson(),
                        saved_generation = current_generation,
                        best_fitness = best_fitness,
                        best_distance = best_distance_ever,
                        total_successes = total_successes_ever,
                        save_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    
                    string json = JsonUtility.ToJson(victory_data);
                    File.WriteAllText(victory_path, json);
                    Debug.Log($"💾 Сохранена победная нейросеть! (Поколение {current_generation}, Фитнес: {best_fitness:F2})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка сохранения победной сети: {e.Message}");
            }
        }

        private void LogGameplayEvent(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string log_entry = $"[{timestamp}] {message}\n";
            
            // Добавляем в буфер
            log_buffer.Append(log_entry);
            
            // Каждые 10 записей сохраняем на диск
            if (log_buffer.Length > 1000)
            {
                try
                {
                    File.AppendAllText(log_path, log_buffer.ToString());
                    log_buffer.Clear();
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка записи лога: {e.Message}");
                }
            }
            
            // Выводим в консоль
            Debug.Log(message);
        }

        void InitTrainingUI()
        {
            // Инициализируем параметры обучения
            training_params.Clear();
            training_inputs.Clear();
            
            // Добавляем все параметры со значением 0
            training_params["Activity_reward"] = 0f;
            training_params["Target_reward"] = 0f;
            training_params["Collision_penalty"] = 0f;
            training_params["Target_tracking_reward"] = 0f;
            training_params["Speed_change_reward"] = 0f;
            training_params["Rotation_change_reward"] = 0f;
            training_params["Time_bonus_multiplier"] = 0f;
            
            // Инициализируем строковые значения для инпутов
            foreach (var param in training_params.Keys.ToList())
            {
                training_inputs[param] = "0";
            }
            
            UpdateUIRect();
        }

        // Новый метод для обновления размеров и позиции UI
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

        void DrawTrainingUI()
        {
            if (!show_training_ui) return;

            // Обновляем размеры UI если размер экрана изменился
            if (Event.current.type == EventType.Layout)
            {
                UpdateUIRect();
            }
            
            // Применяем масштаб UI
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(ui_scale, ui_scale, 1));
            
            // Инициализируем стили при первом вызове
            if (label_style == null)
            {
                window_style = new GUIStyle(GUI.skin.window);
                window_style.fontSize = window_title_font_size;
                window_style.normal.textColor = Color.white;
                
                label_style = new GUIStyle(GUI.skin.label);
                label_style.fontSize = param_label_font_size;
                label_style.normal.textColor = Color.white;
                label_style.alignment = TextAnchor.MiddleLeft;
                
                input_style = new GUIStyle(GUI.skin.textField);
                input_style.fontSize = input_font_size;
                input_style.alignment = TextAnchor.MiddleCenter;
                input_style.normal.textColor = Color.white;
                
                button_style = new GUIStyle(GUI.skin.button);
                button_style.fontSize = start_button_font_size;
                button_style.fontStyle = FontStyle.Bold;
                button_style.normal.textColor = Color.white;

                speed_button_style = new GUIStyle(GUI.skin.button);
                speed_button_style.fontSize = speed_button_font_size;
                speed_button_style.fontStyle = FontStyle.Bold;
                speed_button_style.normal.textColor = Color.white;
                speed_button_style.padding = new RectOffset(8, 8, 8, 8);
            }
            
            // Рисуем панель управления скоростью
            DrawSpeedPanel();
            
            // Рисуем окно с параметрами
            GUI.Box(training_ui_rect, "Параметры обучения", window_style);
            
            // Начинаем скролл область
            float scroll_y = training_ui_rect.y + (30f / ui_scale);
            float scroll_height = training_ui_rect.height - (40f / ui_scale);
            
            training_ui_scroll = GUI.BeginScrollView(
                new Rect(training_ui_rect.x, scroll_y, training_ui_rect.width, scroll_height),
                training_ui_scroll,
                new Rect(0, 0, training_ui_rect.width - (25f / ui_scale),
                        ((slider_height * 2 + param_spacing) * training_params.Count) / ui_scale)
            );
            
            float y_pos = 0f;
            
            // Отрисовываем каждый параметр
            foreach (var param in training_params.Keys.ToList())
            {
                // Используем русское название из словаря
                string russian_name = param_names[param];
                
                // Рисуем лейбл
                GUI.Label(new Rect(10f / ui_scale, y_pos, 
                                 (ui_width - input_width - 30f) / ui_scale, 
                                 slider_height / ui_scale), 
                         russian_name, label_style);
                
                // Рисуем слайдер
                float new_value = GUI.HorizontalSlider(
                    new Rect(10f / ui_scale, y_pos + slider_height / ui_scale, 
                            (ui_width - input_width - 30f) / ui_scale, 
                            slider_height / ui_scale),
                    training_params[param],
                    0f,
                    100f
                );
                
                // Если значение изменилось через слайдер
                if (new_value != training_params[param])
                {
                    training_params[param] = new_value;
                    training_inputs[param] = new_value.ToString("F1");
                    UpdateTrainingParameter(param, new_value);
                }
                
                // Рисуем поле ввода с тёмным фоном
                GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                string new_input = GUI.TextField(
                    new Rect((ui_width - input_width - 10f) / ui_scale, 
                            y_pos + (5f / ui_scale), 
                            input_width / ui_scale, 
                            (slider_height - 5f) / ui_scale),
                    training_inputs[param],
                    input_style
                );
                GUI.backgroundColor = Color.white;
                
                // Если значение изменилось через ввод
                if (new_input != training_inputs[param])
                {
                    training_inputs[param] = new_input;
                    if (float.TryParse(new_input, out float parsed_value))
                    {
                        training_params[param] = Mathf.Clamp(parsed_value, 0f, 100f);
                        UpdateTrainingParameter(param, parsed_value);
                    }
                }
                
                y_pos += (slider_height * 2 + param_spacing) / ui_scale;
            }
            
            GUI.EndScrollView();
            
            // Сбрасываем масштаб UI для остальных элементов интерфейса
            GUI.matrix = Matrix4x4.identity;
        }
        
        private void DrawSpeedPanel()
        {
            GUI.Box(speed_panel_rect, "Скорость симуляции", window_style);

            // Вычисляем размеры для кнопок скорости и управления
            float total_width = speed_panel_rect.width - (2 * speed_button_spacing);
            float speed_section_width = total_width * 0.6f; // 60% под кнопки скорости
            float control_section_width = total_width * 0.4f;  // 40% под кнопки управления
            float control_button_width = (control_section_width - speed_button_spacing) / 2; // Делим на 2 кнопки

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
                    Debug.Log("⏸️ Обучение на паузе");
                }
            }

            // Кнопка СБРОС
            control_x += control_button_width + speed_button_spacing;
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // Красный для сброса
            if (GUI.Button(new Rect(control_x, button_y,
                                  control_button_width - speed_button_spacing,
                                  speed_button_height / ui_scale),
                         "СБРОС", button_style))
            {
                if (EditorUtility.DisplayDialog("Сброс обучения",
                    "Вы уверены, что хотите сбросить всё обучение?\n" +
                    "Это действие удалит все сохранённые нейросети и начнёт обучение заново.",
                    "Да, сбросить всё", "Отмена"))
                {
                    ResetTraining();
                }
            }

            GUI.backgroundColor = Color.white;
        }

        // Метод для полного сброса обучения
        private void ResetTraining()
        {
            try
            {
                // Останавливаем симуляцию
                training_started = false;
                Time.timeScale = 0f;

                // Удаляем все файлы нейросетей
                string snapshots_path = Path.Combine(Application.dataPath, "Game", snapshots_folder);
                if (Directory.Exists(snapshots_path))
                {
                    Directory.Delete(snapshots_path, true);
                    Directory.CreateDirectory(snapshots_path); // Создаём пустую папку
                    Debug.Log("🗑️ Папка снапшотов очищена");
                }

                // Удаляем файл текущей сети
                string current_model_path = Application.persistentDataPath + "/" + model_save_path;
                if (File.Exists(current_model_path))
                {
                    File.Delete(current_model_path);
                    Debug.Log("🗑️ Удалён файл текущей сети");
                }

                // Удаляем файл победной сети
                string victory_path = Path.Combine(Application.dataPath, "Game", "victory_network.json");
                if (File.Exists(victory_path))
                {
                    File.Delete(victory_path);
                    Debug.Log("🗑️ Удалён файл победной сети");
                }

                // Очищаем лог
                if (File.Exists(log_path))
                {
                    File.WriteAllText(log_path, "");
                    Debug.Log("🗑️ Лог очищен");
                }

                // Сбрасываем все параметры
                current_generation = 0;
                best_distance_ever = float.MaxValue;
                total_successes_ever = 0;
                has_loaded_network = false;
                best_network = null;
                current_save_data = null;
                success_history.Clear();
                successful_networks.Clear();
                current_agents_count = initial_agents_count;
                current_mutation_rate = initial_mutation_rate;
                game_won = false;
                is_validation_round = false;

                // Удаляем всех агентов
                ClearAgents();

                // Пересоздаём пул агентов
                if (agents_pool != null)
                {
                    Destroy(agents_pool.gameObject);
                }
                GameObject pool = new GameObject("Agents_Pool");
                agents_pool = pool.transform;

                // Сбрасываем параметры обучения в UI
                InitTrainingUI();

                Debug.Log("🔄 Обучение полностью сброшено! Можно начать заново.");
                
                // Перезапускаем сцену
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка при сбросе обучения: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("Ошибка",
                    $"Не удалось полностью сбросить обучение:\n{e.Message}",
                    "OK");
            }
        }

        void UpdateTrainingParameter(string param_name, float value)
        {
            // Обновляем соответствующий параметр в Neuro
            switch (param_name)
            {
                case "Activity_reward":
                    foreach (var agent in active_agents)
                    {
                        if (agent != null)
                        {
                            var neuro = agent.GetComponent<Neuro>();
                            if (neuro != null) neuro.activity_reward = value;
                        }
                    }
                    break;
                    
                case "Target_reward":
                    foreach (var agent in active_agents)
                    {
                        if (agent != null)
                        {
                            var neuro = agent.GetComponent<Neuro>();
                            if (neuro != null) neuro.target_reward = value;
                        }
                    }
                    break;
                    
                case "Collision_penalty":
                    foreach (var agent in active_agents)
                    {
                        if (agent != null)
                        {
                            var neuro = agent.GetComponent<Neuro>();
                            if (neuro != null) neuro.collision_penalty = value;
                        }
                    }
                    break;
                    
                case "Target_tracking_reward":
                    foreach (var agent in active_agents)
                    {
                        if (agent != null)
                        {
                            var neuro = agent.GetComponent<Neuro>();
                            if (neuro != null) neuro.target_tracking_reward = value;
                        }
                    }
                    break;
                    
                case "Speed_change_reward":
                    foreach (var agent in active_agents)
                    {
                        if (agent != null)
                        {
                            var neuro = agent.GetComponent<Neuro>();
                            if (neuro != null) neuro.speed_change_reward = value;
                        }
                    }
                    break;
                    
                case "Rotation_change_reward":
                    foreach (var agent in active_agents)
                    {
                        if (agent != null)
                        {
                            var neuro = agent.GetComponent<Neuro>();
                            if (neuro != null) neuro.rotation_change_reward = value;
                        }
                    }
                    break;
                    
                case "Time_bonus_multiplier":
                    foreach (var agent in active_agents)
                    {
                        if (agent != null)
                        {
                            var neuro = agent.GetComponent<Neuro>();
                            if (neuro != null) neuro.time_bonus_multiplier = value;
                        }
                    }
                    break;
            }
        }

        // Метод для доступа к активным агентам из TrainingUI
        public List<GameObject> GetActiveAgents()
        {
            return active_agents;
        }

        private void ConfigureNeuralNetwork()
        {
            try 
            {
                Debug.Log("🔄 Начинаем конфигурацию нейросети...");

                // Получаем агента-прототип для подсчёта входов
                if (agent_prefab == null)
                {
                    ValidateAgentPrefab();
                    if (agent_prefab == null)
                    {
                        Debug.LogError("❌ КРИТИЧЕСКАЯ ОШИБКА: Не удалось загрузить префаб агента!");
                        return;
                    }
                }

                var neuro = agent_prefab.GetComponent<Neuro>();
                if (neuro == null)
                {
                    Debug.LogError("❌ КРИТИЧЕСКАЯ ОШИБКА: В префабе агента нет компонента Neuro!");
                    return;
                }

                // Считаем входы
                int detector_inputs = neuro.GetDetectorsCount() * 2; // Каждый детектор даёт 2 значения
                int movement_inputs = 2;    // Скорость и поворот
                int target_inputs = 4;      // Дистанция до цели, угол к цели, направление X и Z
                int prev_control_inputs = 2; // Предыдущие команды
                int time_inputs = 1;        // Нормализованное время жизни
                
                int total_inputs = detector_inputs + movement_inputs + target_inputs + prev_control_inputs + time_inputs;
                int outputs = 2; // Движение и поворот

                Debug.Log($"📊 Расчёт входов: детекторы({detector_inputs}) + движение({movement_inputs}) + " +
                         $"цель({target_inputs}) + пред.команды({prev_control_inputs}) + время({time_inputs}) = {total_inputs}");

                // Проверяем что все значения валидны
                if (total_inputs <= 0 || outputs <= 0)
                {
                    Debug.LogError($"❌ КРИТИЧЕСКАЯ ОШИБКА: Неверное количество входов ({total_inputs}) или выходов ({outputs})!");
                    return;
                }

                // Конфигурируем слои
                List<int> layers = new List<int>();
                layers.Add(total_inputs);        // Входной слой
                
                // Проверяем hidden_layers перед добавлением
                if (hidden_layers != null && hidden_layers.Length > 0)
                {
                    bool valid_hidden = true;
                    foreach (int size in hidden_layers)
                    {
                        if (size <= 0)
                        {
                            Debug.LogError($"❌ КРИТИЧЕСКАЯ ОШИБКА: Неверный размер скрытого слоя: {size}!");
                            valid_hidden = false;
                            break;
                        }
                    }
                    if (valid_hidden)
                    {
                        layers.AddRange(hidden_layers);   // Скрытые слои
                        Debug.Log($"✅ Добавлены скрытые слои: {string.Join(" → ", hidden_layers)}");
                    }
                }
                
                layers.Add(outputs);             // Выходной слой
                
                // Явно присваиваем значение neural_layers
                neural_layers = layers.ToArray();

                Debug.Log($"✅ Итоговая структура сети: {string.Join(" → ", neural_layers)}");

                // Проверяем финальную конфигурацию
                if (neural_layers == null || neural_layers.Length < 2)
                {
                    Debug.LogError("❌ КРИТИЧЕСКАЯ ОШИБКА: Неверная итоговая конфигурация слоёв!");
                    return;
                }

                // Создаём хэш конфигурации для имени файла
                network_config_hash = $"i{total_inputs}_h{string.Join("-", hidden_layers)}_o{outputs}";
                
                // Обновляем путь к файлу сохранения
                model_save_path = $"best_neural_model_{network_config_hash}.json";
                
                Debug.Log($"🧠 Нейросеть успешно сконфигурирована!");
                Debug.Log($"💾 Файл сохранения: {model_save_path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка конфигурации нейросети: {e.Message}\n{e.StackTrace}");
                neural_layers = null; // Сбрасываем в случае ошибки
            }
        }
    }
} 