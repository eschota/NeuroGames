using UnityEngine;
using System.Collections.Generic;

public class SimulationManager_UI : MonoBehaviour
{
    [SerializeField] private SimulationManager simulationManager;
    [SerializeField] private bool showUI = true;
    [SerializeField] private float groundCheckDistance = 0.1f; // Слишком маленькое!
    [SerializeField] private float bodyFallPenalty = 50.0f; // Слишком жестко!
    [SerializeField] private float headLowPenalty = 30.0f; // Слишком жестко!
    
    [Header("Фитнес график")]
    [SerializeField] private bool showFitnessGraph = true;
    [SerializeField] private int maxGenerationsToShow = 50; // Максимальное количество поколений на графике
    [SerializeField] private Color graphLineColor = new Color(0f, 1f, 0.5f); // Зеленый
    [SerializeField] private Color graphBackgroundColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private Vector2 graphSize = new Vector2(400, 200);
    [SerializeField] private Vector2 graphPosition = new Vector2(20, 250);
    
    private string loadedModelStatus = "No model loaded";
    private Color modelStatusColor = Color.red;
    
    // Исторические данные для графика
    private List<float> avgFitnessByGeneration = new List<float>();
    private List<float> bestFitnessByGeneration = new List<float>();
    private int lastRecordedGeneration = -1;
    private float currentGenerationAvgFitness = 0f;
    private int lastGenerationAgentCount = 0;
    
    private void Start()
    {
        Application.runInBackground = true;
        if (simulationManager == null)
        {
            simulationManager = FindObjectOfType<SimulationManager>();
            
            if (simulationManager == null)
            {
                Debug.LogError("SimulationManager_UI: No SimulationManager found in scene!");
                enabled = false;
            }
        }
    }
    
    private void Update()
    {
        if (simulationManager == null) return;
        
        // Обновляем данные для графика
        UpdateFitnessData();
        
        // Update model status
        string networkPath = simulationManager.GetSavedNetworkPath();
        if (!string.IsNullOrEmpty(networkPath))
        {
            string filename = System.IO.Path.GetFileName(networkPath);
            loadedModelStatus = $"✓ Model loaded: {filename}";
            modelStatusColor = Color.green;
        }
        else
        {
            loadedModelStatus = "▢ No pre-trained model";
            modelStatusColor = Color.yellow;
        }
    }
    
    private void UpdateFitnessData()
    {
        // Получаем текущее поколение
        int currentGeneration = simulationManager.GetCurrentGeneration();
        
        // Если поколение изменилось, сохраняем данные предыдущего
        if (currentGeneration > lastRecordedGeneration && lastRecordedGeneration >= 0)
        {
            // Добавляем средний фитнес прошлого поколения в историю
            avgFitnessByGeneration.Add(currentGenerationAvgFitness);
            bestFitnessByGeneration.Add(simulationManager.GetBestFitnessPreviousGen());
            
            // Ограничиваем размер истории
            if (avgFitnessByGeneration.Count > maxGenerationsToShow)
            {
                avgFitnessByGeneration.RemoveAt(0);
                bestFitnessByGeneration.RemoveAt(0);
            }
            
            // Сбрасываем счетчик для нового поколения
            currentGenerationAvgFitness = 0f;
            lastGenerationAgentCount = 0;
        }
        
        // Обновляем средний фитнес текущего поколения
        if (simulationManager.GetAgentCount() > 0)
        {
            // Получаем средний фитнес всех агентов
            float totalFitness = 0f;
            int agentCount = simulationManager.GetAgentBrains().Count;
            
            foreach (var brain in simulationManager.GetAgentBrains())
            {
                if (brain != null)
                {
                    totalFitness += brain.GetFitness();
                }
            }
            
            // Если есть агенты, обновляем средний фитнес
            if (agentCount > 0)
            {
                currentGenerationAvgFitness = totalFitness / agentCount;
                lastGenerationAgentCount = agentCount;
            }
        }
        
        // Запоминаем текущее поколение
        lastRecordedGeneration = currentGeneration;
    }
    
    private void OnGUI()
    {
        if (!showUI || simulationManager == null) return;
        
        // Define UI box size and position
        int width = 350;
        int height = 220;
        int margin = 10;
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.fontSize = 14;
        boxStyle.normal.textColor = Color.white;
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 16;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        
        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.fontSize = 14;
        valueStyle.normal.textColor = Color.white;
        
        GUIStyle modelStatusStyle = new GUIStyle(GUI.skin.label);
        modelStatusStyle.fontSize = 14;
        modelStatusStyle.fontStyle = FontStyle.Bold;
        modelStatusStyle.normal.textColor = modelStatusColor;
        
        // Draw background box
        GUI.Box(new Rect(margin, margin, width, height), "", boxStyle);
        
        int y = margin + 10;
        int labelWidth = 150;
        
        // Title
        GUI.Label(new Rect(margin + 10, y, width - 20, 20), "AI Training Simulation", titleStyle);
        y += 30;
        
        // Prefab name
        GUI.Label(new Rect(margin + 10, y, labelWidth, 20), "Prefab:", valueStyle);
        GUI.Label(new Rect(margin + 10 + labelWidth, y, width - 20 - labelWidth, 20), simulationManager.GetCurrentPrefabName(), valueStyle);
        y += 20;
        
        // Generation
        GUI.Label(new Rect(margin + 10, y, labelWidth, 20), "Generation:", valueStyle);
        GUI.Label(new Rect(margin + 10 + labelWidth, y, width - 20 - labelWidth, 20), simulationManager.GetCurrentGeneration().ToString(), valueStyle);
        y += 20;
        
        // Timer
        GUI.Label(new Rect(margin + 10, y, labelWidth, 20), "Timer:", valueStyle);
        GUI.Label(new Rect(margin + 10 + labelWidth, y, width - 20 - labelWidth, 20), 
            $"{simulationManager.GetSimulationTime():F1} / {simulationManager.GetTotalSimulationTime():F1}", valueStyle);
        y += 20;
        
        // Fitness
        string fitnessText = $"{simulationManager.GetBestFitnessCurrentGen():F2}";
        if (simulationManager.GetBestFitnessPreviousGen() > 0)
        {
            fitnessText += $" ({simulationManager.GetBestFitnessPreviousGen():F2})";
        }
        
        GUI.Label(new Rect(margin + 10, y, labelWidth, 20), "Best Fitness:", valueStyle);
        GUI.Label(new Rect(margin + 10 + labelWidth, y, width - 20 - labelWidth, 20), fitnessText, valueStyle);
        y += 20;
        
        // Средний фитнес текущего поколения
        GUI.Label(new Rect(margin + 10, y, labelWidth, 20), "Avg Fitness:", valueStyle);
        GUI.Label(new Rect(margin + 10 + labelWidth, y, width - 20 - labelWidth, 20), 
            $"{currentGenerationAvgFitness:F2} ({lastGenerationAgentCount} agents)", valueStyle);
        y += 20;
        
        // Network path
        string path = simulationManager.GetSavedNetworkPath();
        if (!string.IsNullOrEmpty(path))
        {
            string filename = System.IO.Path.GetFileName(path);
            GUI.Label(new Rect(margin + 10, y, labelWidth, 20), "Network File:", valueStyle);
            GUI.Label(new Rect(margin + 10 + labelWidth, y, width - 20 - labelWidth, 20), filename, valueStyle);
            y += 20;
        }
        
        // Model status indicator
        GUI.Label(new Rect(margin + 10, y, width - 20, 20), loadedModelStatus, modelStatusStyle);
        
        // Рисуем график фитнеса
        if (showFitnessGraph)
        {
            DrawFitnessGraph();
        }
    }
    
    private void DrawFitnessGraph()
    {
        if (avgFitnessByGeneration.Count < 2) return; // Нужно минимум 2 поколения для графика
        
        // Параметры графика
        float graphWidth = graphSize.x;
        float graphHeight = graphSize.y;
        float graphX = graphPosition.x;
        float graphY = graphPosition.y;
        
        // Рисуем фон графика
        GUI.color = graphBackgroundColor;
        GUI.DrawTexture(new Rect(graphX, graphY, graphWidth, graphHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        // Стиль для заголовка и осей
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        
        // Заголовок
        GUI.Label(new Rect(graphX, graphY - 20, graphWidth, 20), "Прогресс обучения по поколениям", labelStyle);
        
        // Находим мин/макс значения для масштабирования
        float minFitness = float.MaxValue;
        float maxFitness = float.MinValue;
        
        for (int i = 0; i < avgFitnessByGeneration.Count; i++)
        {
            minFitness = Mathf.Min(minFitness, avgFitnessByGeneration[i]);
            maxFitness = Mathf.Max(maxFitness, avgFitnessByGeneration[i]);
            
            // Также учитываем лучший фитнес
            minFitness = Mathf.Min(minFitness, bestFitnessByGeneration[i]);
            maxFitness = Mathf.Max(maxFitness, bestFitnessByGeneration[i]);
        }
        
        // Добавляем немного отступа сверху и снизу
        float fitnessPadding = Mathf.Max(1f, (maxFitness - minFitness) * 0.1f);
        maxFitness += fitnessPadding;
        minFitness -= fitnessPadding;
        
        // Если все значения равны, добавляем диапазон
        if (Mathf.Approximately(minFitness, maxFitness))
        {
            minFitness = minFitness - 1;
            maxFitness = maxFitness + 1;
        }
        
        // Рисуем оси
        DrawGraphAxes(graphX, graphY, graphWidth, graphHeight, minFitness, maxFitness);
        
        // Рисуем линию среднего фитнеса
        DrawGraphLine(graphX, graphY, graphWidth, graphHeight, avgFitnessByGeneration, minFitness, maxFitness, graphLineColor);
        
        // Рисуем линию лучшего фитнеса
        DrawGraphLine(graphX, graphY, graphWidth, graphHeight, bestFitnessByGeneration, minFitness, maxFitness, Color.yellow);
        
        // Добавляем легенду
        float legendY = graphY + graphHeight + 10;
        
        // Средний фитнес
        GUI.color = graphLineColor;
        GUI.DrawTexture(new Rect(graphX, legendY, 20, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(graphX + 25, legendY - 10, 100, 20), "Средний", labelStyle);
        
        // Лучший фитнес
        GUI.color = Color.yellow;
        GUI.DrawTexture(new Rect(graphX + 100, legendY, 20, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(graphX + 125, legendY - 10, 100, 20), "Лучший", labelStyle);
    }
    
    private void DrawGraphAxes(float x, float y, float width, float height, float minValue, float maxValue)
    {
        // Стиль для подписей осей
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleRight;
        
        // Ось Y
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, y, 1, height), Texture2D.whiteTexture);
        
        // Подписи для оси Y
        int yLabels = 5; // Количество подписей на оси Y
        for (int i = 0; i <= yLabels; i++)
        {
            float valuePercent = (float)i / yLabels;
            float value = minValue + (maxValue - minValue) * valuePercent;
            float labelY = y + height - height * valuePercent;
            
            // Линия сетки
            GUI.color = new Color(1, 1, 1, 0.2f);
            GUI.DrawTexture(new Rect(x, labelY, width, 1), Texture2D.whiteTexture);
            
            // Подпись значения
            GUI.color = Color.white;
            GUI.Label(new Rect(x - 55, labelY - 10, 50, 20), value.ToString("F1"), labelStyle);
        }
        
        // Ось X
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, y + height, width, 1), Texture2D.whiteTexture);
        
        // Подписи для оси X
        int generations = avgFitnessByGeneration.Count;
        int xLabels = Mathf.Min(5, generations - 1); // Максимум 5 подписей по X
        
        labelStyle.alignment = TextAnchor.UpperCenter;
        
        for (int i = 0; i <= xLabels; i++)
        {
            float valuePercent = (float)i / xLabels;
            int genIndex = Mathf.RoundToInt(valuePercent * (generations - 1));
            int genNumber = lastRecordedGeneration - (generations - 1) + genIndex;
            float labelX = x + width * valuePercent;
            
            // Линия сетки
            GUI.color = new Color(1, 1, 1, 0.2f);
            GUI.DrawTexture(new Rect(labelX, y, 1, height), Texture2D.whiteTexture);
            
            // Подпись значения
            GUI.color = Color.white;
            GUI.Label(new Rect(labelX - 15, y + height + 5, 30, 20), genNumber.ToString(), labelStyle);
        }
    }
    
    private void DrawGraphLine(float x, float y, float width, float height, List<float> values, float minValue, float maxValue, Color lineColor)
    {
        if (values.Count < 2) return;
        
        GUI.color = lineColor;
        float prevX = x;
        float prevY = 0;
        
        for (int i = 0; i < values.Count; i++)
        {
            float normalizedX = (float)i / (values.Count - 1);
            float normalizedY = (values[i] - minValue) / (maxValue - minValue);
            
            float pointX = x + width * normalizedX;
            float pointY = y + height - height * normalizedY;
            
            if (i > 0)
            {
                DrawLine(prevX, prevY, pointX, pointY, lineColor, 2);
            }
            
            prevX = pointX;
            prevY = pointY;
        }
        
        GUI.color = Color.white;
    }
    
    private void DrawLine(float x1, float y1, float x2, float y2, Color color, float thickness)
    {
        Matrix4x4 matrix = GUI.matrix;
        
        // Расстояние между точками и угол
        float dx = x2 - x1;
        float dy = y2 - y1;
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(dx * dx + dy * dy);
        
        GUIUtility.RotateAroundPivot(angle, new Vector2(x1, y1));
        GUI.color = color;
        GUI.DrawTexture(new Rect(x1, y1 - thickness / 2, length, thickness), Texture2D.whiteTexture);
        
        GUI.matrix = matrix;
        GUI.color = Color.white;
    }
} 