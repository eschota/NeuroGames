using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Simulation Settings")]
    [SerializeField] private int agentsCount = 30;
    [SerializeField] private float simulationTimer = 10f;
    [SerializeField] private int simulationsCountPerPrefab = 100;
    [SerializeField] private Transform startTransform;
    
    [Header("Genetic Algorithm Settings")]
    [SerializeField, Range(0.01f, 1f)] private float mutationRate = 0.1f;
    [SerializeField, Range(0.01f, 1f)] private float mutationStrength = 0.2f;
    [SerializeField, Range(0.01f, 0.5f)] private float elitePercentage = 0.1f;
    [SerializeField] private int[] neuralNetworkLayers = new int[] { 10, 20, 10 }; // This will be auto-updated based on agent
    
    [Header("Visual Settings")]
    [SerializeField] private Material defaultMaterial; // Reference to a URP material
    [SerializeField] private Color eliteColor = new Color(1f, 0.8f, 0f); // Gold
    [SerializeField] private Color bestColor = new Color(0.8f, 0f, 1f); // Purple
    [SerializeField] private Color worstColor = new Color(0.5f, 0.5f, 0.5f); // Gray
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private bool useEmission = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool validateNetworks = true;
    
    private List<GameObject> agentPrefabs = new List<GameObject>();
    private List<AI_Agent_Brain> currentGenerationBrains = new List<AI_Agent_Brain>();
    private List<NeuralNetwork> nextGenerationNetworks = new List<NeuralNetwork>();
    private Dictionary<int, Material> agentMaterials = new Dictionary<int, Material>();
    
    private int currentPrefabIndex = 0;
    private int currentGeneration = 0;
    private float currentSimulationTime = 0f;
    private bool isSimulationRunning = false;
    private GameObject currentPrefab;
    private string currentLogBuffer = "";
    
    private float bestFitnessCurrentGen = 0f;
    private float bestFitnessPreviousGen = 0f;
    private string savedNetworkPath = "";
    
    private void Start()
    {
        Application.runInBackground = true;
        
        // Создаем менеджер UI для отображения фитнеса агентов
        GameObject fitnessUIObj = new GameObject("Agent Fitness UI Manager");
        fitnessUIObj.AddComponent<AgentFitnessUI>();
        
        // Load agent prefabs from Resources
        LoadAgentPrefabs();
        
        if (agentPrefabs.Count > 0)
        {
            // Start simulation with a delay
            StartCoroutine(StartSimulationWithDelay());
        }
        else
        {
            Debug.LogError("No agent prefabs found in Resources/Agents folder!");
        }
    }
    
    private IEnumerator StartSimulationWithDelay()
    {
        LogToBuffer("Starting simulation with delay...");
        
        // Wait for half a second before starting
        yield return new WaitForSeconds(0.5f);
        
        // Start with the first prefab
        StartNextPrefabSimulation();
    }
    
    private void LoadAgentPrefabs()
    {
        agentPrefabs.Clear();
        GameObject[] prefabs = Resources.LoadAll<GameObject>("Agents");
        
        foreach (var prefab in prefabs)
        {
            if (prefab.GetComponent<AI_Agent>() != null)
            {
                agentPrefabs.Add(prefab);
                LogToBuffer($"Loaded agent prefab: {prefab.name}");
            }
        }
        
        // Create default material if not assigned
        if (defaultMaterial == null)
        {
            // Try to find Universal Render Pipeline shader
            string shaderName = "Universal Render Pipeline/Lit";
            Shader shader = Shader.Find(shaderName);
            
            // Fall back to Standard shader if URP not found
            if (shader == null)
            {
                shaderName = "Standard";
                shader = Shader.Find(shaderName);
            }
            
            if (shader != null)
            {
                defaultMaterial = new Material(shader);
                defaultMaterial.name = "Agent_Default";
                LogToBuffer($"Created default material using {shaderName} shader");
            }
            else
            {
                LogToBuffer("Warning: Could not find appropriate shader for agent materials");
            }
        }
        
        FlushLog();
    }
    
    private void StartNextPrefabSimulation()
    {
        if (currentPrefabIndex >= agentPrefabs.Count)
        {
            LogToBuffer("All prefabs processed. Simulation complete!");
            FlushLog();
            return;
        }
        
        // Set current prefab and reset generation counter
        currentPrefab = agentPrefabs[currentPrefabIndex];
        currentGeneration = 0;
        
        LogToBuffer($"Starting simulation for prefab: {currentPrefab.name}");
        
        // Always clear previous generation networks before loading
        nextGenerationNetworks.Clear();
        
        // Try to load a pre-trained network before starting the first generation
        if (!LoadBestNetwork())
        {
            LogToBuffer($"No existing network found for {currentPrefab.name}, starting fresh");
        }
        else
        {
            LogToBuffer($"Successfully loaded existing network for {currentPrefab.name}");
            
            // Validate the loaded network
            if (validateNetworks && nextGenerationNetworks.Count > 0 && !nextGenerationNetworks[0].Validate())
            {
                LogToBuffer("WARNING: Loaded network failed validation, creating fresh network instead");
                nextGenerationNetworks.Clear();
            }
        }
        
        // Start first generation
        StartNextGeneration();
    }
    
    private void StartNextGeneration()
    {
        if (currentGeneration >= simulationsCountPerPrefab)
        {
            // Move to next prefab
            currentPrefabIndex++;
            StartNextPrefabSimulation();
            return;
        }
        
        // Сбрасываем лучший фитнес для UI отображения
        AgentFitnessUI.ResetBestFitness();
        
        bestFitnessCurrentGen = 0f;
        currentSimulationTime = 0f;
        
        // Clear existing agents
        CleanupCurrentGeneration();
        
        LogToBuffer($"Starting generation {currentGeneration} for {currentPrefab.name}");
        
        // Spawn new generation with a slight delay
        StartCoroutine(SpawnAgentsWithDelay());
        
        isSimulationRunning = true;
    }
    
    private IEnumerator SpawnAgentsWithDelay()
    {
        // Spawn agents in a grid pattern
        const float agentSpacing = 3.5f;
        const int agentsPerRow = 10;
        
        Vector3 startPosition = startTransform != null ? startTransform.position : Vector3.zero;
        
        // Clear materials cache for this generation
        agentMaterials.Clear();
        
        // Создаем или находим контейнер для всех агентов
        GameObject agentsContainer = GameObject.Find("Agents_pool");
        if (agentsContainer == null)
        {
            agentsContainer = new GameObject("Agents_pool");
            LogToBuffer("Created new Agents_pool container");
        }
        else
        {
            // Очищаем контейнер, если он уже есть
            foreach (Transform child in agentsContainer.transform)
            {
                Destroy(child.gameObject);
            }
            LogToBuffer("Cleared existing Agents_pool container");
        }
        
        LogToBuffer($"Spawning {agentsCount} agents in batches to reduce lag");
        
        // Spawn in smaller batches with yield between each batch
        const int batchSize = 3; // Create just a few agents per batch
        for (int i = 0; i < agentsCount; i += batchSize)
        {
            // Create a batch of agents
            for (int j = 0; j < batchSize && (i + j) < agentsCount; j++)
            {
                int agentIndex = i + j;
                int row = agentIndex / agentsPerRow;
                int col = agentIndex % agentsPerRow;
                
                Vector3 spawnPosition = startPosition + new Vector3(col * agentSpacing, 0, row * agentSpacing);
                GameObject agentObj = Instantiate(currentPrefab, spawnPosition, Quaternion.identity);
                
                // Устанавливаем родителя для агента - наш новый контейнер
                agentObj.transform.SetParent(agentsContainer.transform);
                agentObj.name = $"Agent_{agentIndex}";
                
                // Add brain if not already present
                AI_Agent_Brain brain = agentObj.GetComponent<AI_Agent_Brain>();
                if (brain == null)
                {
                    brain = agentObj.AddComponent<AI_Agent_Brain>();
                }
                
                // Make sure the brain is inactive
                brain.SetActive(false);
                currentGenerationBrains.Add(brain);
                
                // Set agent color based on previous generation performance
                if (agentIndex < nextGenerationNetworks.Count)
                {
                    SetAgentColor(agentObj, agentIndex);
                }
            }
            
            // Yield after each batch to let the engine breathe
            yield return new WaitForSeconds(0.05f);
        }
        
        LogToBuffer($"All {agentsCount} agents spawned successfully in Agents_pool");
        
        // Wait for all agents to properly initialize with physics disabled
        yield return new WaitForSeconds(0.5f);
        
        // Сбрасываем состояние падения для всех агентов
        foreach (var brain in currentGenerationBrains)
        {
            if (brain != null && brain.GetAgent() != null)
            {
                brain.GetAgent().ResetFallState();
            }
        }
        
        // Initialize neural networks for this generation
        yield return StartCoroutine(InitializeGeneration());
    }
    
    private void SetAgentColor(GameObject agentObj, int index)
    {
        MeshRenderer[] renderers = agentObj.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;
        
        // Create or get cached material
        Material material;
        if (!agentMaterials.TryGetValue(index, out material))
        {
            // Create a new material based on the default
            material = defaultMaterial != null ? 
                new Material(defaultMaterial) : 
                new Material(Shader.Find("Universal Render Pipeline/Lit"));
            
            material.name = $"Agent_Material_{index}";
            
            // Choose color based on rank
            Color mainColor;
            float metallicValue = 0.0f;
            float smoothnessValue = 0.5f;

            if (index == 0)
            {
                // Elite agent (gold)
                mainColor = eliteColor;
                metallicValue = 1.0f;
                smoothnessValue = 0.8f;
            }
            else if (index < agentsCount * 0.1f) // Top 10%
            {
                // Gradient from purple to green for top performers
                float t = index / (agentsCount * 0.1f);
                mainColor = Color.Lerp(bestColor, Color.green, t);
                metallicValue = 0.7f - (t * 0.4f);
                smoothnessValue = 0.7f - (t * 0.2f);
            }
            else
            {
                // Default gray
                mainColor = worstColor;
            }
            
            // Set material properties
            material.SetColor("_BaseColor", mainColor); // URP
            material.SetColor("_Color", mainColor); // Fallback for Standard
            
            // Set emission (if shader supports it)
            if (useEmission && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", mainColor * emissionIntensity);
            }
            
            // Set metallic and smoothness for URP
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallicValue);
            }
            
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothnessValue);
            }
            
            // Cache the material
            agentMaterials[index] = material;
        }
        
        // Apply material to all renderers
        foreach (var renderer in renderers)
        {
            renderer.material = material;
        }
    }
    
    private IEnumerator InitializeGeneration()
    {
        if (currentGenerationBrains.Count == 0)
        {
            Debug.LogError("No agent brains in current generation!");
            yield break;
        }
        
        LogToBuffer("Creating neural networks...");
        
        // If this is the first generation and we don't have networks loaded, create random networks
        if (nextGenerationNetworks.Count == 0)
        {
            // Create neural networks with the right size based on the first agent
            AI_Agent_Brain firstBrain = currentGenerationBrains[0];
            
            // Wait for brain initialization to complete
            yield return new WaitForSeconds(0.2f);
            
            int inputSize = firstBrain.InputLayerSize;
            int outputSize = firstBrain.OutputLayerSize;
            
            // Validate sizes
            if (inputSize == 0 || outputSize == 0)
            {
                LogToBuffer($"Warning: Invalid network sizes from first brain: inputSize={inputSize}, outputSize={outputSize}. Using default architecture.");
                // Use a default architecture
                neuralNetworkLayers = new int[] { 10, 20, 10 }; 
            }
            else
            {
                // Create network architecture: [inputSize, hidden1, hidden2, outputSize]
                neuralNetworkLayers = new int[] { inputSize, inputSize * 2, outputSize * 2, outputSize };
                LogToBuffer($"Neural network architecture: [{string.Join(", ", neuralNetworkLayers)}]");
            }
            
            // Create random networks for each agent
            for (int i = 0; i < agentsCount; i++)
            {
                // Initialize with more randomness for first generation
                NeuralNetwork network = new NeuralNetwork(neuralNetworkLayers, i + UnityEngine.Random.Range(0, 10000));
                
                // Apply strong initial mutations
                network.Mutate(1.0f, 0.5f);
                
                nextGenerationNetworks.Add(network);
                
                // Yield occasionally during network creation to prevent lag
                if (i % 10 == 0) 
                {
                    yield return null;
                }
            }
        }
        else
        {
            Debug.Log($"===> APPLYING PRE-TRAINED MODEL: {Path.GetFileName(savedNetworkPath)} <===");
        }
        
        LogToBuffer("Applying networks to agents...");
        
        // Apply networks in batches to reduce lag
        const int batchSize = 5;
        for (int i = 0; i < currentGenerationBrains.Count; i += batchSize)
        {
            // Initialize a batch of agents
            for (int j = 0; j < batchSize && (i + j) < currentGenerationBrains.Count; j++)
            {
                int agentIndex = i + j;
                
                // Apply an existing network or create a new one
                if (agentIndex < nextGenerationNetworks.Count)
                {
                    NeuralNetwork network = nextGenerationNetworks[agentIndex];
                    
                    // Validate network before applying
                    if (validateNetworks && !network.Validate())
                    {
                        LogToBuffer($"Warning: Network at index {agentIndex} failed validation, creating new network");
                        network = new NeuralNetwork(neuralNetworkLayers, agentIndex + UnityEngine.Random.Range(0, 10000));
                        network.Mutate(1.0f, 0.5f);
                        nextGenerationNetworks[agentIndex] = network;
                    }
                    
                    currentGenerationBrains[agentIndex].Initialize(network);
                    
                    // Highlight if this is the best pre-trained model being applied
                    if (agentIndex == 0 && !string.IsNullOrEmpty(savedNetworkPath))
                    {
                        Debug.Log($"===> APPLYING NETWORK TO AGENT {agentIndex}: Using pre-trained model <===");
                    }
                }
                else
                {
                    // Create a new network if needed
                    NeuralNetwork network = new NeuralNetwork(neuralNetworkLayers, agentIndex + 100 + UnityEngine.Random.Range(0, 10000));
                    network.Mutate(1.0f, 0.5f); // Add strong mutations
                    currentGenerationBrains[agentIndex].Initialize(network);
                    
                    // Add to nextGenerationNetworks if there's room
                    if (nextGenerationNetworks.Count < agentsCount)
                    {
                        nextGenerationNetworks.Add(network);
                    }
                }
            }
            
            // Yield after each batch
            yield return null;
        }
        
        // Wait a moment before enabling physics
        LogToBuffer("Networks initialized, preparing to enable physics...");
        yield return new WaitForSeconds(0.2f);
        
        // Enable agents in small batches to reduce physics computation spikes
        LogToBuffer("Enabling physics simulation and neural networks...");
        for (int i = 0; i < currentGenerationBrains.Count; i += batchSize)
        {
            // Enable a batch of brains
            for (int j = 0; j < batchSize && (i + j) < currentGenerationBrains.Count; j++)
            {
                currentGenerationBrains[i + j].SetActive(true);
            }
            
            // Small delay between batches
            yield return new WaitForSeconds(0.05f);
        }
        
        if (!string.IsNullOrEmpty(savedNetworkPath))
        {
            Debug.Log($"===> SIMULATION STARTED WITH MODEL {Path.GetFileName(savedNetworkPath)} <===");
        }
        
        LogToBuffer($"Generation {currentGeneration} started with {currentGenerationBrains.Count} active agents");
        FlushLog();
    }
    
    private void Update()
    {
        if (!isSimulationRunning) return;
        
        // Update simulation timer
        currentSimulationTime += Time.deltaTime;
        
        // Check for best fitness in current generation
        UpdateBestFitness();
        
        // Check if simulation time is up
        if (currentSimulationTime >= simulationTimer)
        {
            isSimulationRunning = false;
            ProcessGenerationResults();
        }
    }
    
    private void UpdateBestFitness()
    {
        foreach (var brain in currentGenerationBrains)
        {
            if (brain == null) continue;
            
            float fitness = brain.GetFitness();
            if (fitness > bestFitnessCurrentGen)
            {
                bestFitnessCurrentGen = fitness;
            }
        }
    }
    
    private void ProcessGenerationResults()
    {
        LogToBuffer($"Generation {currentGeneration} finished. Best fitness: {bestFitnessCurrentGen:F2}");
        
        // Sort brains by fitness
        List<AI_Agent_Brain> sortedBrains = currentGenerationBrains
            .Where(brain => brain != null) // Filter out any null brains
            .OrderByDescending(brain => brain.GetFitness())
            .ToList();
        
        if (sortedBrains.Count > 0)
        {
            // Get best brain
            AI_Agent_Brain bestBrain = sortedBrains[0];
            float bestFitness = bestBrain.GetFitness();
            
            // Save best network if it's better than previous
            if (bestFitness > bestFitnessPreviousGen)
            {
                SaveBestNetwork(bestBrain.GetNeuralNetwork(), bestFitness);
            }
            
            // Create next generation networks
            CreateNextGeneration(sortedBrains);
            
            bestFitnessPreviousGen = bestFitness;
        }
        
        currentGeneration++;
        FlushLog();
        
        // Start next generation
        StartNextGeneration();
    }
    
    private void CreateNextGeneration(List<AI_Agent_Brain> sortedBrains)
    {
        nextGenerationNetworks.Clear();
        
        int eliteCount = Mathf.Max(1, Mathf.FloorToInt(agentsCount * elitePercentage));
        LogToBuffer($"Keeping {eliteCount} elite agents for next generation");
        
        // Keep elite networks unchanged
        for (int i = 0; i < eliteCount && i < sortedBrains.Count; i++)
        {
            NeuralNetwork eliteNetwork = sortedBrains[i].GetNeuralNetwork();
            if (eliteNetwork != null)
            {
                // Even elite networks can accumulate numerical issues over time, let's regularize periodically
                if (currentGeneration % 5 == 0)
                {
                    eliteNetwork.Regularize(0.0001f);
                }
                
                nextGenerationNetworks.Add(eliteNetwork);
            }
        }
        
        // Fill the rest with crossovers and mutations
        while (nextGenerationNetworks.Count < agentsCount && sortedBrains.Count >= 2)
        {
            // Select two parents using tournament selection
            AI_Agent_Brain parent1 = SelectParentTournament(sortedBrains);
            AI_Agent_Brain parent2 = SelectParentTournament(sortedBrains);
            
            if (parent1 != null && parent2 != null && 
                parent1.GetNeuralNetwork() != null && parent2.GetNeuralNetwork() != null)
            {
                // Create child through crossover
                NeuralNetwork child = NeuralNetwork.Crossover(
                    parent1.GetNeuralNetwork(),
                    parent2.GetNeuralNetwork()
                );
                
                // Apply mutation with increasing rate for generations with similar fitness
                float adaptiveMutationRate = mutationRate;
                float adaptiveMutationStrength = mutationStrength;
                
                // If fitness hasn't improved much, increase mutation
                if (bestFitnessCurrentGen <= bestFitnessPreviousGen * 1.01f)
                {
                    adaptiveMutationRate *= 1.5f;
                    adaptiveMutationStrength *= 1.2f;
                }
                
                // Apply mutation
                child.Mutate(adaptiveMutationRate, adaptiveMutationStrength);
                
                // Regularize occasionally to prevent numerical instability
                if (currentGeneration % 5 == 0)
                {
                    child.Regularize(0.0001f);
                }
                
                // Validate before adding to make sure we don't have NaN values
                if (validateNetworks && !child.Validate())
                {
                    // If validation fails, recreate the network
                    LogToBuffer("Warning: Created child network failed validation, creating fresh network instead");
                    child = new NeuralNetwork(neuralNetworkLayers, UnityEngine.Random.Range(0, 10000));
                    child.Mutate(1.0f, 0.2f);
                }
                
                // Add to next generation
                nextGenerationNetworks.Add(child);
            }
        }
        
        // Fill any remaining slots with completely new random networks with significant mutations
        while (nextGenerationNetworks.Count < agentsCount)
        {
            NeuralNetwork randomNetwork = new NeuralNetwork(neuralNetworkLayers, Random.Range(0, 10000));
            randomNetwork.Mutate(0.8f, 0.3f); // Slightly less extreme mutations for stability
            nextGenerationNetworks.Add(randomNetwork);
        }
        
        LogToBuffer($"Created {nextGenerationNetworks.Count} networks for next generation");
    }
    
    private AI_Agent_Brain SelectParentTournament(List<AI_Agent_Brain> sortedBrains)
    {
        // Tournament selection
        int tournamentSize = Mathf.Max(2, sortedBrains.Count / 5);
        List<AI_Agent_Brain> tournament = new List<AI_Agent_Brain>();
        
        for (int i = 0; i < tournamentSize; i++)
        {
            int randomIndex = Random.Range(0, sortedBrains.Count);
            tournament.Add(sortedBrains[randomIndex]);
        }
        
        // Return the best from tournament
        return tournament.OrderByDescending(b => b.GetFitness()).FirstOrDefault();
    }
    
    private void SaveBestNetwork(NeuralNetwork network, float fitness)
    {
        if (network == null) return;
        
        // Create directory if it doesn't exist
        string savePath = "Assets/Game/AI_Agents";
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
        
        // Create save file path
        string fileName = $"{currentPrefab.name}_{currentGeneration}_{fitness:F2}.json";
        string filePath = Path.Combine(savePath, fileName);
        
        try
        {
            // Validate network before saving
            if (validateNetworks && !network.Validate())
            {
                LogToBuffer("WARNING: Not saving network that failed validation");
                return;
            }
            
            // Save network to file
            string json = network.ToJson();
            File.WriteAllText(filePath, json);
            
            savedNetworkPath = filePath;
            LogToBuffer($"Saved best network to {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error saving network: {ex.Message}");
        }
    }
    
    private bool LoadBestNetwork()
    {
        string savePath = "Assets/Game/AI_Agents";
        if (!Directory.Exists(savePath)) return false;
        
        try
        {
            string[] files = Directory.GetFiles(savePath, $"{currentPrefab.name}_*.json");
            
            if (files.Length == 0)
            {
                LogToBuffer($"No saved networks found for {currentPrefab.name}");
                return false;
            }
            
            // Sort by fitness (which is at the end of the filename)
            System.Array.Sort(files, (a, b) => {
                float fitnessA = ExtractFitnessFromFilename(a);
                float fitnessB = ExtractFitnessFromFilename(b);
                return fitnessB.CompareTo(fitnessA); // Descending order
            });
            
            string bestFile = files[0];
            Debug.Log($"===> LOADING BEST MODEL: {Path.GetFileName(bestFile)} <===");
            LogToBuffer($"Found best network file: {Path.GetFileName(bestFile)}");
            
            string json = File.ReadAllText(bestFile);
            if (string.IsNullOrEmpty(json))
            {
                LogToBuffer("Network file is empty, skipping");
                return false;
            }
            
            NeuralNetwork loadedNetwork = NeuralNetwork.FromJson(json);
            if (loadedNetwork == null)
            {
                LogToBuffer("Failed to deserialize network from JSON");
                return false;
            }
            
            // Create a list of networks with the loaded one as the first
            nextGenerationNetworks.Clear();
            nextGenerationNetworks.Add(loadedNetwork);
            
            // Create slightly mutated variants for the rest of the population
            for (int i = 1; i < agentsCount; i++)
            {
                NeuralNetwork mutatedNetwork = new NeuralNetwork(loadedNetwork);
                
                // Apply stronger mutations to later copies
                float mutationIntensity = (float)i / agentsCount;
                mutatedNetwork.Mutate(0.1f + mutationIntensity * 0.4f, 0.1f + mutationIntensity * 0.3f);
                
                nextGenerationNetworks.Add(mutatedNetwork);
            }
            
            float fitness = ExtractFitnessFromFilename(bestFile);
            bestFitnessPreviousGen = fitness;
            savedNetworkPath = bestFile;
            
            Debug.Log($"===> MODEL LOADED SUCCESSFULLY: {Path.GetFileName(bestFile)} (Fitness: {fitness:F2}) <===");
            LogToBuffer($"Loaded network from {bestFile} with fitness {fitness:F2} and created {nextGenerationNetworks.Count} variants");
            return true;
        }
        catch (System.Exception ex)
        {
            LogToBuffer($"Error loading network: {ex.Message}");
            return false;
        }
    }
    
    private float ExtractFitnessFromFilename(string filename)
    {
        try
        {
            string fitnessStr = Path.GetFileNameWithoutExtension(filename);
            int lastUnderscore = fitnessStr.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < fitnessStr.Length - 1)
            {
                fitnessStr = fitnessStr.Substring(lastUnderscore + 1);
                return float.Parse(fitnessStr);
            }
        }
        catch { }
        
        return 0f;
    }
    
    private void CleanupCurrentGeneration()
    {
        // Очищаем список текущих мозгов
        foreach (var brain in currentGenerationBrains)
        {
            if (brain != null && brain.gameObject != null)
            {
                Destroy(brain.gameObject);
            }
        }
        currentGenerationBrains.Clear();
        
        // Альтернативный подход - находим и очищаем контейнер
        GameObject agentsContainer = GameObject.Find("Agents_pool");
        if (agentsContainer != null)
        {
            LogToBuffer("Cleaning up Agents_pool container");
            
            // Можно либо уничтожить сам контейнер (если в SpawnAgentsWithDelay мы его создаем заново)
            // Destroy(agentsContainer);
            
            // Либо уничтожить всех его детей (выбрал этот вариант, чтобы сохранять тот же объект в сцене)
            foreach (Transform child in agentsContainer.transform)
            {
                Destroy(child.gameObject);
            }
        }
    }
    
    private void LogToBuffer(string message)
    {
        if (!showDebugLogs) return;
        
        currentLogBuffer += message + "\n";
    }
    
    private void FlushLog()
    {
        if (!showDebugLogs || string.IsNullOrEmpty(currentLogBuffer)) return;
        
        Debug.Log(currentLogBuffer);
        currentLogBuffer = "";
    }
    
    // Public accessor methods
    public int GetCurrentGeneration() => currentGeneration;
    public float GetSimulationTime() => currentSimulationTime;
    public float GetTotalSimulationTime() => simulationTimer;
    public float GetBestFitnessCurrentGen() => bestFitnessCurrentGen;
    public float GetBestFitnessPreviousGen() => bestFitnessPreviousGen;
    public string GetCurrentPrefabName() => currentPrefab != null ? currentPrefab.name : "";
    public string GetSavedNetworkPath() => savedNetworkPath;

    // Добавляем методы для получения информации об агентах для графика
    public List<AI_Agent_Brain> GetAgentBrains() => currentGenerationBrains;
    public int GetAgentCount() => currentGenerationBrains.Count;
} 