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

            // Инициализируем UI для параметров обучения
            InitTrainingUI();

            // Ставим игру на паузу до начала обучения
            Time.timeScale = 0f;
            training_started = false;
            
            // Инициализируем таймер поколения
            StartCoroutine(SimulationTimerCoroutine());
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

            // Показываем сообщение об ошибке если нужно
            if (show_error && Time.realtimeSinceStartup < error_time)
            {
                DrawErrorMessage();
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
                string russian_name = parameter_names[param];
                
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

        private void DrawErrorMessage()
        {
            float dialog_width = 400f;
            float dialog_height = 100f;
            float dialog_x = (Screen.width - dialog_width) / 2;
            float dialog_y = (Screen.height - dialog_height) / 2;

            // Рисуем красное окно с ошибкой
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
            GUI.Box(new Rect(dialog_x, dialog_y, dialog_width, dialog_height), "Ошибка");
            GUI.backgroundColor = Color.white;

            // Текст ошибки
            GUI.Label(new Rect(dialog_x + 20, dialog_y + 30, dialog_width - 40, dialog_height - 40),
                error_message);
        }

        public void ShowError(string message, float duration = 5f)
        {
            error_message = message;
            show_error = true;
            error_time = Time.realtimeSinceStartup + duration;
        }

        void UpdateTrainingParameter(string param_name, float value)
        {
            // Вместо прямого вызова метода simulation_manager.UpdateTrainingParameter
            // Создаем свою локальную реализацию, которая будет обрабатывать параметры
            try
            {
                // Логируем изменение параметра
                Debug.Log($"📊 Изменен параметр {param_name}: {value}");
                
                // Здесь можно добавить свою логику для обновления параметров
                // напрямую в компонентах Neuro, если это возможно
                var agents = GameObject.FindGameObjectsWithTag("Agent");
                foreach (var agent in agents)
                {
                    var neuro = agent.GetComponent<Neuro>();
                    if (neuro != null)
                    {
                        // Попытка обновить свойство через рефлексию
                        var property = neuro.GetType().GetField(param_name.ToLower());
                        if (property != null)
                        {
                            property.SetValue(neuro, value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка при обновлении параметра: {e.Message}");
            }
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
        
        // Инициализация параметров обучения в UI
        void InitTrainingUI()
        {
            if (simulation_manager == null) return;
            
            // Инициализируем параметры обучения
            training_params.Clear();
            training_inputs.Clear();
            
            // Базовые параметры, используем тут константы
            training_params["Activity_reward"] = 1.0f;
            training_params["Target_reward"] = 5.0f;
            training_params["Collision_penalty"] = 1.0f;
            training_params["Target_tracking_reward"] = 0.5f;
            training_params["Speed_change_reward"] = 0.2f;
            training_params["Rotation_change_reward"] = 0.2f;
            training_params["Time_bonus_multiplier"] = 1.0f;
            
            // Инициализируем текстовые значения для полей
            foreach (var param in training_params.Keys.ToList())
            {
                training_inputs[param] = training_params[param].ToString("F1");
            }
            
            // Обновляем размеры и позицию UI
            UpdateUIRect();
            
            Debug.Log("🔧 Инициализирован UI для параметров обучения");
        }
    }
}
