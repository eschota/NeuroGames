using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Game.Scripts.GamePlay
{
    public class TrainingUI : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private bool show_training_ui = true;
        [SerializeField] private float ui_width = 300f;
        [SerializeField] private float slider_height = 20f;
        [SerializeField] private float input_width = 60f;
        [SerializeField] private float param_spacing = 5f;
        
        private Rect training_ui_rect;
        private Vector2 training_ui_scroll;
        private Dictionary<string, float> training_params = new Dictionary<string, float>();
        private Dictionary<string, string> training_inputs = new Dictionary<string, string>();
        private GUIStyle label_style;
        private GUIStyle input_style;
        
        // Ссылка на менеджер симуляции
        private SimulationManager sim_manager;
        
        void Start()
        {
            // Получаем ссылку на SimulationManager
            sim_manager = FindObjectOfType<SimulationManager>();
            if (sim_manager == null)
            {
                Debug.LogError("❌ TrainingUI: Не найден SimulationManager!");
                enabled = false;
                return;
            }
            
            InitUI();
        }
        
        void InitUI()
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
            
            // Настраиваем размер и позицию окна
            float window_height = (slider_height + param_spacing) * training_params.Count + 40f;
            training_ui_rect = new Rect(Screen.width - ui_width - 20f, 480f, ui_width, window_height);
            
            // Создаём стили
            label_style = new GUIStyle(GUI.skin.label);
            label_style.fontSize = 12;
            label_style.normal.textColor = Color.white;
            
            input_style = new GUIStyle(GUI.skin.textField);
            input_style.fontSize = 12;
            input_style.alignment = TextAnchor.MiddleRight;
        }
        
        void OnGUI()
        {
            if (!show_training_ui) return;
            
            // Рисуем окно с параметрами
            GUI.Box(training_ui_rect, "Training Parameters");
            
            // Начинаем скролл область
            training_ui_scroll = GUI.BeginScrollView(
                new Rect(training_ui_rect.x, training_ui_rect.y + 20, training_ui_rect.width, training_ui_rect.height - 20),
                training_ui_scroll,
                new Rect(0, 0, training_ui_rect.width - 20, (slider_height + param_spacing) * training_params.Count)
            );
            
            float y_pos = 0f;
            
            // Отрисовываем каждый параметр
            foreach (var param in training_params.Keys.ToList())
            {
                // Рисуем лейбл
                GUI.Label(new Rect(10, y_pos, ui_width - input_width - 20, slider_height), param, label_style);
                
                // Рисуем слайдер
                float new_value = GUI.HorizontalSlider(
                    new Rect(10, y_pos + slider_height, ui_width - input_width - 30, slider_height),
                    training_params[param],
                    0f,
                    100f
                );
                
                // Если значение изменилось через слайдер
                if (new_value != training_params[param])
                {
                    training_params[param] = new_value;
                    training_inputs[param] = new_value.ToString("F3");
                    UpdateParameter(param, new_value);
                }
                
                // Рисуем поле ввода
                string new_input = GUI.TextField(
                    new Rect(ui_width - input_width - 10, y_pos + slider_height, input_width, slider_height),
                    training_inputs[param],
                    input_style
                );
                
                // Если значение изменилось через ввод
                if (new_input != training_inputs[param])
                {
                    training_inputs[param] = new_input;
                    if (float.TryParse(new_input, out float parsed_value))
                    {
                        training_params[param] = parsed_value;
                        UpdateParameter(param, parsed_value);
                    }
                }
                
                y_pos += slider_height * 2 + param_spacing;
            }
            
            GUI.EndScrollView();
        }
        
        void UpdateParameter(string param_name, float value)
        {
            if (sim_manager == null) return;
            
            // Получаем всех активных агентов
            var agents = sim_manager.GetActiveAgents();
            if (agents == null) return;
            
            // Обновляем параметр у каждого агента
            foreach (var agent in agents)
            {
                if (agent == null) continue;
                
                var neuro = agent.GetComponent<Neuro>();
                if (neuro == null) continue;
                
                switch (param_name)
                {
                    case "Activity_reward":
                        neuro.activity_reward = value;
                        break;
                    case "Target_reward":
                        neuro.target_reward = value;
                        break;
                    case "Collision_penalty":
                        neuro.collision_penalty = value;
                        break;
                    case "Target_tracking_reward":
                        neuro.target_tracking_reward = value;
                        break;
                    case "Speed_change_reward":
                        neuro.speed_change_reward = value;
                        break;
                    case "Rotation_change_reward":
                        neuro.rotation_change_reward = value;
                        break;
                    case "Time_bonus_multiplier":
                        neuro.time_bonus_multiplier = value;
                        break;
                }
            }
        }
    }
} 