using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class AgentFitnessUI : MonoBehaviour
{
    [Header("Positioning")]
    [SerializeField] private Vector3 offsetPosition = new Vector3(0, 1.5f, 0);
    [SerializeField] private float updateInterval = 0.2f; // Обновлять каждые 0.2 секунды для оптимизации
    
    [Header("Text Appearance")]
    [SerializeField] private Color defaultTextColor = Color.white;
    [SerializeField] private int defaultFontSize = 16;
    [SerializeField] private bool addTextShadow = true;
    
    [Header("Background")]
    [SerializeField] private bool showBackground = true;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Vector2 backgroundPadding = new Vector2(10f, 5f);
    
    [Header("Dynamic Text Size")]
    [SerializeField] private bool useDynamicSize = true;
    [SerializeField] private int minFontSize = 12;
    [SerializeField] private int maxFontSize = 24;
    [SerializeField] private float fontSizeScaleFactor = 0.5f; // На сколько увеличивать размер шрифта по мере роста фитнеса
    
    [Header("Dynamic Text Color")]
    [SerializeField] private bool useGradientColor = true;
    [SerializeField] private Color lowFitnessColor = Color.white;    // 0-10
    [SerializeField] private Color mediumFitnessColor = Color.yellow; // 10-20
    [SerializeField] private Color highFitnessColor = Color.green;   // 20+
    [SerializeField] private Color bestFitnessColor = new Color(1f, 0.6f, 0f); // Золотой цвет для лучшего фитнеса
    
    [Header("Thresholds")]
    [SerializeField] private float mediumFitnessThreshold = 10f;
    [SerializeField] private float highFitnessThreshold = 20f;
    
    // Храним лучший фитнес для выделения лучшего агента
    private static float bestFitnessSoFar = 0f;
    private static AI_Agent_Brain bestBrain = null;
    
    private static Canvas uiCanvas;
    private static Dictionary<AI_Agent_Brain, FitnessTextElements> fitnessBrainTexts = new Dictionary<AI_Agent_Brain, FitnessTextElements>();
    private static AgentFitnessUI instance;
    
    private float nextUpdateTime;

    // Класс для хранения всех UI элементов для фитнеса агента
    private class FitnessTextElements
    {
        public GameObject rootObject;
        public Image background;
        public Text text;
    }
    
    private void Awake()
    {
        // Синглтон паттерн
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Создаем канвас при старте, если его еще нет
        if (uiCanvas == null)
        {
            CreateCanvas();
        }
    }
    
    private void CreateCanvas()
    {
        // Создаем игровой объект для канваса
        GameObject canvasObj = new GameObject("Fitness UI Canvas");
        uiCanvas = canvasObj.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        // Добавляем канвас-скейлер и настраиваем его
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Добавляем рейкастер для обработки ввода (на всякий случай)
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Не уничтожать при смене сцены
        DontDestroyOnLoad(canvasObj);
    }
    
    public static Text CreateAgentFitnessText(AI_Agent_Brain brain)
    {
        if (instance == null)
        {
            // Если инстанс отсутствует, создаем его
            GameObject managerObj = new GameObject("Agent Fitness UI Manager");
            instance = managerObj.AddComponent<AgentFitnessUI>();
        }
        
        if (uiCanvas == null)
        {
            instance.CreateCanvas();
        }
        
        // Проверяем, есть ли уже текст для этого мозга
        if (fitnessBrainTexts.ContainsKey(brain))
        {
            return fitnessBrainTexts[brain].text;
        }
        
        // Создаем элементы UI для отображения фитнеса
        FitnessTextElements elements = new FitnessTextElements();
        
        // Создаем корневой объект
        elements.rootObject = new GameObject($"FitnessText_{brain.gameObject.name}");
        elements.rootObject.transform.SetParent(uiCanvas.transform, false);
        RectTransform rootRect = elements.rootObject.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(100, 30);
        
        // Если нужен фон, создаем его
        if (instance.showBackground)
        {
            // Создаем фон
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(elements.rootObject.transform, false);
            
            elements.background = bgObj.AddComponent<Image>();
            elements.background.color = instance.backgroundColor;
            elements.background.raycastTarget = false;
            
            // Настраиваем ректтрансформ фона
            RectTransform bgRect = elements.background.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = -instance.backgroundPadding / 2;
            bgRect.offsetMax = instance.backgroundPadding / 2;
        }
        
        // Создаем текст поверх фона
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(elements.rootObject.transform, false);
        
        // Настраиваем текстовый компонент
        elements.text = textObj.AddComponent<Text>();
        elements.text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        elements.text.alignment = TextAnchor.MiddleCenter;
        elements.text.color = instance.defaultTextColor;
        elements.text.fontSize = instance.defaultFontSize;
        elements.text.raycastTarget = false; // Отключаем рейкаст для производительности
        elements.text.text = "0.00";
        
        // Добавляем шедоу для лучшей видимости (если включено)
        if (instance.addTextShadow)
        {
            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(1, -1);
        }
        
        // Настраиваем ректтрансформ текста
        RectTransform textRect = elements.text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Добавляем в словарь
        fitnessBrainTexts.Add(brain, elements);
        
        return elements.text;
    }
    
    public static void RemoveAgentFitnessText(AI_Agent_Brain brain)
    {
        if (fitnessBrainTexts.TryGetValue(brain, out FitnessTextElements elements) && elements.rootObject != null)
        {
            Destroy(elements.rootObject);
            fitnessBrainTexts.Remove(brain);
        }
        
        // Если это был лучший мозг, сбрасываем его
        if (brain == bestBrain)
        {
            bestBrain = null;
        }
    }
    
    // Ресет для новой генерации
    public static void ResetBestFitness()
    {
        bestFitnessSoFar = 0f;
        bestBrain = null;
    }
    
    private void Update()
    {
        // Обновляем позиции и значения текста с определенным интервалом
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + updateInterval;
            UpdateAllTexts();
        }
    }
    
    private void UpdateAllTexts()
    {
        // Получаем камеру
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        
        // Временный список для удаления невалидных ключей
        List<AI_Agent_Brain> invalidBrains = new List<AI_Agent_Brain>();
        
        // Находим лучший фитнес в этом обновлении
        float currentBestFitness = bestFitnessSoFar;
        AI_Agent_Brain currentBestBrain = bestBrain;
        
        // Сначала найдем лучший фитнес
        foreach (var pair in fitnessBrainTexts)
        {
            AI_Agent_Brain brain = pair.Key;
            
            if (brain == null)
            {
                invalidBrains.Add(brain);
                continue;
            }
            
            float fitness = brain.GetFitness();
            if (fitness > currentBestFitness)
            {
                currentBestFitness = fitness;
                currentBestBrain = brain;
            }
        }
        
        // Обновляем глобальные лучшие значения
        bestFitnessSoFar = currentBestFitness;
        bestBrain = currentBestBrain;
        
        // Теперь обрабатываем все тексты
        foreach (var pair in fitnessBrainTexts)
        {
            AI_Agent_Brain brain = pair.Key;
            FitnessTextElements elements = pair.Value;
            
            if (brain == null || elements.rootObject == null)
            {
                invalidBrains.Add(brain);
                continue;
            }
            
            // Получаем позицию над головой агента
            Vector3 worldPos = brain.transform.position + offsetPosition;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
            
            // Если агент за камерой, скрываем текст
            if (screenPos.z < 0)
            {
                elements.rootObject.SetActive(false);
                continue;
            }
            
            // Включаем отображение и обновляем позицию
            elements.rootObject.SetActive(true);
            elements.rootObject.GetComponent<RectTransform>().position = screenPos;
            
            // Обновляем значение фитнеса
            float fitness = brain.GetFitness();
            elements.text.text = fitness.ToString("F2");
            
            // Настраиваем размер текста в зависимости от фитнеса, если включено
            if (useDynamicSize)
            {
                float sizeMultiplier = 1f + fitness * fontSizeScaleFactor / 100f;
                int dynamicSize = Mathf.Clamp(
                    Mathf.RoundToInt(defaultFontSize * sizeMultiplier),
                    minFontSize, 
                    maxFontSize
                );
                elements.text.fontSize = dynamicSize;
            }
            
            // Настраиваем цвет текста в зависимости от фитнеса, если включено
            if (useGradientColor)
            {
                if (brain == bestBrain)
                {
                    // Лучший агент получает золотой цвет
                    elements.text.color = bestFitnessColor;
                    
                    // Делаем текст жирным для лучшего агента
                    elements.text.fontStyle = FontStyle.Bold;
                }
                else
                {
                    // Обычный стиль для всех остальных
                    elements.text.fontStyle = FontStyle.Normal;
                    
                    // Градиентное изменение цвета в зависимости от фитнеса
                    if (fitness >= highFitnessThreshold)
                    {
                        elements.text.color = highFitnessColor;
                    }
                    else if (fitness >= mediumFitnessThreshold)
                    {
                        // Плавный переход между средним и высоким фитнесом
                        float t = (fitness - mediumFitnessThreshold) / (highFitnessThreshold - mediumFitnessThreshold);
                        elements.text.color = Color.Lerp(mediumFitnessColor, highFitnessColor, t);
                    }
                    else
                    {
                        // Плавный переход между низким и средним фитнесом
                        float t = Mathf.Clamp01(fitness / mediumFitnessThreshold);
                        elements.text.color = Color.Lerp(lowFitnessColor, mediumFitnessColor, t);
                    }
                }
            }
            else
            {
                // Если динамический цвет выключен, используем цвет по умолчанию
                elements.text.color = defaultTextColor;
            }
        }
        
        // Удаляем невалидные ключи
        foreach (var brain in invalidBrains)
        {
            if (fitnessBrainTexts.TryGetValue(brain, out FitnessTextElements elements) && elements.rootObject != null)
            {
                Destroy(elements.rootObject);
            }
            fitnessBrainTexts.Remove(brain);
        }
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
} 