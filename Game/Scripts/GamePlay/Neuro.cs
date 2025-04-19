using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;
using System;

namespace Game.Scripts.GamePlay
{
    [System.Serializable]
    public class NeuralNetwork
    {
        public int[] layers;
        public float[][] neurons;
        public float[][][] weights;
        public float fitness;

        // Random generator
        private System.Random random;

        // Вспомогательные классы для сериализации
        [System.Serializable]
        private class SerializedWeights
        {
            public float[] data;
            public int[] dimensions;
        }

        [System.Serializable]
        private class SerializedNetwork
        {
            public int[] layers;
            public float[] flatNeurons;
            public SerializedWeights weights;
            public float fitness;
        }

        // Constructor
        public NeuralNetwork(int[] layers)
        {
            if (layers == null)
            {
                Debug.LogError("❌ Слои не заданы (null)!");
                throw new ArgumentException("Layers array is null");
            }

            if (layers.Length < 2)
            {
                Debug.LogError("❌ Нужно минимум 2 слоя (вход и выход)!");
                throw new ArgumentException("Need at least 2 layers");
            }

            foreach (int size in layers)
            {
                if (size <= 0)
                {
                    Debug.LogError($"❌ Некорректный размер слоя: {size}!");
                    throw new ArgumentException($"Invalid layer size: {size}");
                }
            }

            this.layers = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                this.layers[i] = layers[i];
            }

            // Generate random seed
            random = new System.Random(Guid.NewGuid().GetHashCode());

            try
            {
                InitNeurons();
                InitWeights();
                Debug.Log($"✅ Создана новая нейросеть с {this.layers.Length} слоями: {string.Join(" → ", this.layers)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка инициализации нейросети: {e.Message}");
                throw;
            }
        }

        // Deep copy constructor
        public NeuralNetwork(NeuralNetwork copyNetwork)
        {
            if (copyNetwork == null)
            {
                Debug.LogError("Attempting to copy null network!");
                throw new ArgumentNullException(nameof(copyNetwork));
            }

            if (copyNetwork.layers == null)
            {
                Debug.LogError("Source network has null layers array!");
                throw new ArgumentException("Source network is not properly initialized");
            }

            try
            {
                // Copy layers
                this.layers = new int[copyNetwork.layers.Length];
                for (int i = 0; i < copyNetwork.layers.Length; i++)
                {
                    this.layers[i] = copyNetwork.layers[i];
                }

                // Generate new random seed
                random = new System.Random(Guid.NewGuid().GetHashCode());

                // Initialize structures
                InitNeurons();
                InitWeights();

                // Copy neuron values
                if (copyNetwork.neurons != null)
                {
                    for (int i = 0; i < neurons.Length; i++)
                    {
                        if (copyNetwork.neurons[i] != null)
                        {
                            for (int j = 0; j < neurons[i].Length; j++)
                            {
                                neurons[i][j] = copyNetwork.neurons[i][j];
                            }
                        }
                    }
                }

                // Copy weight values
                if (copyNetwork.weights != null)
                {
                    for (int i = 0; i < weights.Length; i++)
                    {
                        if (copyNetwork.weights[i] != null)
                        {
                            for (int j = 0; j < weights[i].Length; j++)
                            {
                                if (copyNetwork.weights[i][j] != null)
                                {
                                    for (int k = 0; k < weights[i][j].Length; k++)
                                    {
                                        weights[i][j][k] = copyNetwork.weights[i][j][k];
                                    }
                                }
                            }
                        }
                    }
                }

                this.fitness = copyNetwork.fitness;
                
                Debug.Log($"Successfully copied neural network with {this.layers.Length} layers");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to copy neural network: {e.Message}\nStack trace: {e.StackTrace}");
                throw;
            }
        }

        // Initialize neurons
        private void InitNeurons()
        {
            List<float[]> neuronsList = new List<float[]>();
            for (int i = 0; i < layers.Length; i++)
            {
                neuronsList.Add(new float[layers[i]]);
            }
            neurons = neuronsList.ToArray();
        }

        // Initialize weights
        private void InitWeights()
        {
            try
            {
                if (layers == null)
                {
                    Debug.LogError("Cannot initialize weights: layers array is null!");
                    throw new InvalidOperationException("Layers array is null");
                }

                if (neurons == null)
                {
                    Debug.LogError("Cannot initialize weights: neurons array is null!");
                    throw new InvalidOperationException("Neurons array is null");
                }

                List<float[][]> weightsList = new List<float[][]>();

                // For each layer except the input layer
                for (int i = 1; i < layers.Length; i++)
                {
                    List<float[]> layerWeightList = new List<float[]>();
                    int neuronsInPreviousLayer = layers[i - 1];
                    
                    // For each neuron in current layer
                    for (int j = 0; j < neurons[i].Length; j++)
                    {
                        float[] neuronWeights = new float[neuronsInPreviousLayer];
                        
                        // For each connection from the previous layer
                        for (int k = 0; k < neuronsInPreviousLayer; k++)
                        {
                            // Initialize with random weights between -1 and 1
                            neuronWeights[k] = (random != null) 
                                ? (float)random.NextDouble() * 2 - 1
                                : UnityEngine.Random.Range(-1f, 1f);
                        }
                        
                        layerWeightList.Add(neuronWeights);
                    }
                    
                    weightsList.Add(layerWeightList.ToArray());
                }
                
                weights = weightsList.ToArray();
                
                // Verify weights initialization
                if (weights == null)
                {
                    Debug.LogError("Weights array is null after initialization!");
                    throw new InvalidOperationException("Failed to initialize weights");
                }
                
                Debug.Log($"Successfully initialized weights: {weights.Length} layers");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in InitWeights: {e.Message}\nStack trace: {e.StackTrace}");
                throw;
            }
        }

        // Feed forward
        public float[] FeedForward(float[] inputs)
        {
            // Set input layer values
            for (int i = 0; i < inputs.Length; i++)
            {
                neurons[0][i] = inputs[i];
            }

            // Feed forward
            for (int i = 1; i < layers.Length; i++)
            {
                for (int j = 0; j < neurons[i].Length; j++)
                {
                    float value = 0f;
                    for (int k = 0; k < neurons[i - 1].Length; k++)
                    {
                        value += weights[i - 1][j][k] * neurons[i - 1][k];
                    }
                    neurons[i][j] = Sigmoid(value);
                }
            }

            return neurons[neurons.Length - 1];
        }

        // Activation function (sigmoid)
        private float Sigmoid(float x)
        {
            return 1.0f / (1.0f + Mathf.Exp(-x));
        }

        // Mutate weights
        public void Mutate(float mutationRate)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights[i].Length; j++)
                {
                    for (int k = 0; k < weights[i][j].Length; k++)
                    {
                        if (random.NextDouble() < mutationRate)
                        {
                            weights[i][j][k] += (float)random.NextDouble() * 2 - 1;
                            
                            // Clamp weights between -1 and 1
                            weights[i][j][k] = Mathf.Clamp(weights[i][j][k], -1f, 1f);
                        }
                    }
                }
            }
        }

        // Convert to JSON for saving
        public string ToJson()
        {
            try 
            {
                SerializedNetwork data = new SerializedNetwork();
                
                // Сохраняем структуру сети
                data.layers = new int[this.layers.Length];
                Array.Copy(this.layers, data.layers, this.layers.Length);
                
                // Сохраняем значения нейронов
                List<float> neuronsList = new List<float>();
                for (int i = 0; i < neurons.Length; i++)
                {
                    neuronsList.AddRange(neurons[i]);
                }
                data.flatNeurons = neuronsList.ToArray();
                
                // Сохраняем веса - самое важное!
                data.weights = new SerializedWeights();
                
                // Считаем общее количество весов
                int totalWeights = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    for (int j = 0; j < weights[i].Length; j++)
                    {
                        totalWeights += weights[i][j].Length;
                    }
                }
                
                // Сохраняем размерности для каждого слоя
                List<int> dimensions = new List<int>();
                dimensions.Add(weights.Length); // Количество слоев весов
                
                // Сохраняем размеры каждого слоя
                for (int i = 0; i < weights.Length; i++)
                {
                    dimensions.Add(weights[i].Length); // Нейроны в текущем слое
                    dimensions.Add(weights[i][0].Length); // Веса для каждого нейрона
                }
                data.weights.dimensions = dimensions.ToArray();
                
                // Сохраняем сами веса в плоский массив
                List<float> weightsList = new List<float>();
                for (int i = 0; i < weights.Length; i++)
                {
                    for (int j = 0; j < weights[i].Length; j++)
                    {
                        weightsList.AddRange(weights[i][j]);
                    }
                }
                data.weights.data = weightsList.ToArray();
                
                // Сохраняем фитнес
                data.fitness = this.fitness;
                
                string json = JsonUtility.ToJson(data);
                Debug.Log($"Сохранено весов: {totalWeights}, размер JSON: {json.Length} байт");
                return json;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка сериализации нейросети: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        // Load from JSON
        public static NeuralNetwork FromJson(string json)
        {
            try
            {
                SerializedNetwork data = JsonUtility.FromJson<SerializedNetwork>(json);
                if (data == null) throw new Exception("Failed to deserialize JSON");
                
                // Создаём новую сеть с нужной структурой
                NeuralNetwork network = new NeuralNetwork(data.layers);
                
                // Восстанавливаем нейроны
                int neuronIndex = 0;
                for (int i = 0; i < network.neurons.Length; i++)
                {
                    for (int j = 0; j < network.neurons[i].Length; j++)
                    {
                        network.neurons[i][j] = data.flatNeurons[neuronIndex++];
                    }
                }
                
                // Восстанавливаем веса
                int weightIndex = 0;
                int layerCount = data.weights.dimensions[0];
                int dimIndex = 1;
                
                for (int i = 0; i < layerCount; i++)
                {
                    int neuronsInLayer = data.weights.dimensions[dimIndex++];
                    int weightsPerNeuron = data.weights.dimensions[dimIndex++];
                    
                    for (int j = 0; j < neuronsInLayer; j++)
                    {
                        for (int k = 0; k < weightsPerNeuron; k++)
                        {
                            network.weights[i][j][k] = data.weights.data[weightIndex++];
                        }
                    }
                }
                
                // Восстанавливаем фитнес
                network.fitness = data.fitness;
                
                Debug.Log($"Загружено весов: {weightIndex}, структура сети: {string.Join(",", data.layers)}");
                return network;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка десериализации нейросети: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }
        
        // Create a completely randomized network
        public void Randomize()
        {
            random = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights[i].Length; j++)
                {
                    for (int k = 0; k < weights[i][j].Length; k++)
                    {
                        weights[i][j][k] = (float)random.NextDouble() * 2 - 1; // Values between -1 and 1
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class GeneticAlgorithm
    {
        [Header("Genetic Algorithm Settings")]
        public int population_size = 50;
        public float mutation_rate = 0.1f;
        public int[] neural_layers = new int[] { 6, 8, 4, 2 }; // Input, Hidden layers, Output
        
        private List<NeuralNetwork> population = new List<NeuralNetwork>();
        private int generation = 0;
        
        public void InitializePopulation()
        {
            if (neural_layers == null || neural_layers.Length < 2)
            {
                Debug.LogError("❌ Неверная конфигурация слоёв в GeneticAlgorithm!");
                return;
            }

            population.Clear();
            for (int i = 0; i < population_size; i++)
            {
                try
                {
                    population.Add(new NeuralNetwork(neural_layers));
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Ошибка создания сети #{i}: {e.Message}");
                    throw;
                }
            }
            generation = 1;
            Debug.Log($"✅ Инициализирована популяция: {population_size} сетей");
        }
        
        public NeuralNetwork GetBestNetwork()
        {
            if (population.Count == 0) return null;
            
            NeuralNetwork bestNetwork = population[0];
            foreach (NeuralNetwork network in population)
            {
                if (network.fitness > bestNetwork.fitness)
                {
                    bestNetwork = network;
                }
            }
            return bestNetwork;
        }
        
        public void NextGeneration()
        {
            generation++;
            
            // Sort by fitness (descending)
            population.Sort((a, b) => b.fitness.CompareTo(a.fitness));
            
            // Keep top performers
            List<NeuralNetwork> newPopulation = new List<NeuralNetwork>();
            int eliteCount = population_size / 10; // Keep top 10%
            
            for (int i = 0; i < eliteCount; i++)
            {
                newPopulation.Add(new NeuralNetwork(population[i]));
            }
            
            // Fill rest with mutations of top performers
            while (newPopulation.Count < population_size)
            {
                // Get a random top performer
                int index = UnityEngine.Random.Range(0, eliteCount);
                NeuralNetwork network = new NeuralNetwork(population[index]);
                network.Mutate(mutation_rate);
                network.fitness = 0; // Reset fitness
                newPopulation.Add(network);
            }
            
            population = newPopulation;
        }
        
        public NeuralNetwork GetCurrentNetwork(int index)
        {
            if (index >= 0 && index < population.Count)
            {
                return population[index];
            }
            return null;
        }
        
        public int GetGeneration()
        {
            return generation;
        }
        
        public int GetPopulationCount()
        {
            return population.Count;
        }
        
        public void AddNetwork(NeuralNetwork network)
        {
            population.Add(network);
        }
        
        // Create a completely randomized network
        public NeuralNetwork CreateRandomNetwork()
        {
            if (neural_layers == null || neural_layers.Length < 2)
            {
                Debug.LogError("❌ Неверная конфигурация слоёв при создании случайной сети!");
                throw new ArgumentException("Invalid neural_layers configuration");
            }

            try
            {
                NeuralNetwork network = new NeuralNetwork(neural_layers);
                network.Randomize();
                return network;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка создания случайной сети: {e.Message}");
                throw;
            }
        }
    }

public class Neuro : MonoBehaviour
{
        [Header("Movement")]
        [SerializeField] private float move_speed = 5f;
        [SerializeField] private float rotation_speed = 120f;
        
        [Header("Neural Network")]
        [SerializeField] private List<Detector> detectors = new List<Detector>();
        [SerializeField] private string model_save_path = "best_neural_model.json";
        [SerializeField] private bool use_neural_control = true;
        public float activity_reward = 0.01f;
        public float target_reward = 100f;
        public float collision_penalty = 50f;
        public float target_tracking_reward = 0.1f; // Reward per second for keeping target in sight
        public float speed_change_reward = 0.05f; // Награда за изменение скорости
        public float rotation_change_reward = 0.05f; // Награда за изменение поворота
        public float time_bonus_multiplier = 0.5f; // Множитель бонуса за скорость (0.5 = до 50% от базовой награды)
        [SerializeField] [Tooltip("Минимальное время для получения максимального бонуса (в секундах)")]
        private float min_time_for_max_bonus = 3f; // Минимальное время для макс бонуса
        
        [Header("Target")]
        [SerializeField] private Transform target_transform; // Reference to the target
        [SerializeField] private float success_distance = 1f; // Distance to consider success
        
        [Header("Simulation")]
        [SerializeField] private float max_lifetime = 10f;
        [SerializeField] private bool auto_reset = false;
        
        [Header("Success Handling")]
        [SerializeField] private bool destroy_on_success = true; // If true, destroy agent on success, if false - teleport far away
        [SerializeField] private Vector3 teleport_position = new Vector3(0, -1000, 0); // Where to teleport if not destroying
        
        private Rigidbody rb;
        private Transform rb_transform;
        private float vertical_input;
        private float horizontal_input;
        private float generation_start_time;
        private float lifetime = 0f;
        private float distance_moved = 0f;
        private Vector3 last_position;
        private float fitness = 0f;
        private bool is_training = true;
        private NeuralNetwork brain;
        private SimulationManager simulation_manager;
        private bool success_reported = false;
        private bool is_disabled = false; // New flag for control disabling
        
        // Unique ID for this instance
        [HideInInspector] public int instance_id;

        private float last_vertical_input = 0f;
        private float last_horizontal_input = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponentInChildren<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("❌ No Rigidbody found in this object or its children! Please add one.");
            enabled = false;
            return;
        }
        
        rb_transform = rb.transform;
        last_position = rb_transform.position;
        
        // Try to find simulation manager
        simulation_manager = FindObjectOfType<SimulationManager>();
        if (simulation_manager == null)
        {
            Debug.LogError("❌ SimulationManager не найден!");
            enabled = false;
            return;
        }

        // Используем конфигурацию из SimulationManager
        int[] layers = simulation_manager.GetNeuralLayers();
        if (layers == null)
        {
            Debug.LogError("❌ Не удалось получить конфигурацию слоёв из SimulationManager!");
            enabled = false;
            return;
        }

        if (layers.Length < 2)
        {
            Debug.LogError($"❌ Неверная конфигурация слоёв: получено {layers.Length} слоёв, нужно минимум 2!");
            enabled = false;
            return;
        }

        if (brain == null)
        {
            try
            {
                GeneticAlgorithm genetic = new GeneticAlgorithm();
                genetic.neural_layers = layers;
                brain = genetic.CreateRandomNetwork();
                is_training = true;
                Debug.Log($"✅ Создана новая случайная сеть для агента {instance_id}: {string.Join(" → ", layers)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка создания сети для агента {instance_id}: {e.Message}");
                enabled = false;
                return;
            }
        }
        
        // Find target if not set
        if (target_transform == null)
        {
            GameObject target = GameObject.FindGameObjectWithTag("AIM");
            if (target != null)
            {
                target_transform = target.transform;
            }
        }
        
        // Set layer to "Agent"
        gameObject.layer = LayerMask.NameToLayer("Agent");
    }

        private void InitializeNeuralNetwork()
        {
            // Create a new random network
            if (brain == null)
            {
                GeneticAlgorithm genetic = new GeneticAlgorithm();
                brain = genetic.CreateRandomNetwork();
                is_training = true;
            }
        }
        
        // Method to set the neural network from the SimulationManager
        public void SetNeuralNetwork(NeuralNetwork network)
        {
            // Сохраняем старые значения параметров, если они существуют
            float[] original_params = null;
            if (brain != null && brain.neurons != null && brain.neurons.Length > 0)
            {
                // Копируем входные параметры (первый слой нейронов)
                original_params = new float[brain.neurons[0].Length];
                Array.Copy(brain.neurons[0], original_params, original_params.Length);
                Debug.Log($"🧠 Агент {instance_id}: Сохранены параметры нейросети перед заменой ({original_params.Length} значений)");
            }
            
            // Устанавливаем новую сеть
            brain = network;
            
            // Восстанавливаем параметры, если они были сохранены
            if (original_params != null && brain != null && brain.neurons != null && 
                brain.neurons.Length > 0 && original_params.Length == brain.neurons[0].Length)
            {
                Array.Copy(original_params, brain.neurons[0], original_params.Length);
                Debug.Log($"🧠 Агент {instance_id}: Восстановлены параметры после замены нейросети");
            }
        }

        public void SetStartTime(float start_time)
        {
            generation_start_time = start_time;
            lifetime = 0f;
    }

    // Update is called once per frame
    void Update()
    {
            if (use_neural_control && is_training)
            {
                // Update lifetime based on generation start time
                lifetime = Time.time - generation_start_time;
                
                // Check for success based on distance
                if (!success_reported && !is_disabled)
                {
                    float distance_to_target = GetDistanceToTarget();
                    if (distance_to_target >= 0 && distance_to_target <= success_distance)
                    {
                        HandleSuccess(distance_to_target);
                        return; // Exit early after success
                    }
                    
                    // Check if any detector sees the target and award tracking reward
                    bool targetInSight = false;
                    foreach (Detector detector in detectors)
                    {
                        if (detector != null && detector.OUTPUT[1] > 0) // OUTPUT[1] > 0 means target detected
                        {
                            targetInSight = true;
                            break;
                        }
                    }
                    
                    if (targetInSight)
                    {
                        // Award reward for keeping target in sight (scaled by time)
                        float trackingReward = target_tracking_reward * Time.deltaTime;
                        fitness += trackingReward;
                        if (brain != null)
                        {
                            brain.fitness = fitness;
                        }
                        Debug.Log($"Agent {instance_id}: Target in sight! +{trackingReward:F3} fitness");
                    }
                }
            }
            else if (!is_disabled)
            {
                // Get player input
                vertical_input = Input.GetAxis("Vertical");
                horizontal_input = Input.GetAxis("Horizontal");
            }
        }

        void FixedUpdate()
        {
            if (!is_disabled)
            {
                // Get neural network decision first
                if (use_neural_control && is_training)
                {
                    UseNeuralNetworkControl();
                }
                
                // Then apply movement based on current inputs
                Rotate();
                Move();
                
                // Calculate distance moved for fitness
                if (is_training)
                {
                    float distance = Vector3.Distance(rb_transform.position, last_position);
                    if (distance > 0.01f) // Only count significant movement
                    {
                        distance_moved += distance;
                        fitness += activity_reward; // Small reward for moving
                    }
                    last_position = rb_transform.position;
                }
            }
        }
        
        private void UseNeuralNetworkControl()
        {
            if (brain == null) return;
            
            // Gather inputs for neural network
            List<float> inputs = new List<float>();
            
            // Add detector inputs
            foreach (Detector detector in detectors)
            {
                if (detector != null && detector.OUTPUT.Length >= 2)
                {
                    inputs.Add(detector.OUTPUT[0]); // Distance
                    inputs.Add(detector.OUTPUT[1]); // Object type
                }
            }
            
            // Добавляем больше информации о движении
            inputs.Add(rb.linearVelocity.magnitude / move_speed); // Нормализованная скорость
            inputs.Add(rb.angularVelocity.y / rotation_speed); // Нормализованная скорость поворота
            
            // Информация о цели
            float distanceToTarget = GetDistanceToTarget();
            inputs.Add(distanceToTarget > 0 ? Mathf.Clamp01(distanceToTarget / 10f) : 1f); // Дистанция до цели
            
            // Направление к цели
            float angleToTarget = 0f;
            if (target_transform != null)
            {
                Vector3 directionToTarget = (target_transform.position - rb_transform.position).normalized;
                angleToTarget = Vector3.SignedAngle(rb_transform.forward, directionToTarget, Vector3.up) / 180f;
                inputs.Add(angleToTarget); // Угол к цели (-1 до 1)
                
                // Проекции направления на оси
                inputs.Add(directionToTarget.x); // Направление по X
                inputs.Add(directionToTarget.z); // Направление по Z
            }
            else
            {
                inputs.Add(0f);
                inputs.Add(0f);
                inputs.Add(0f);
            }
            
            // Предыдущие действия для лучшего принятия решений
            inputs.Add(last_vertical_input);
            inputs.Add(last_horizontal_input);
            
            // Время жизни (нормализованное)
            inputs.Add(Mathf.Clamp01(lifetime / max_lifetime));
            
            // Debug input layer size mismatch
            if (brain.layers[0] != inputs.Count)
            {
                Debug.LogError($"❌ Несоответствие размера входного слоя! Ожидается: {brain.layers[0]}, Получено: {inputs.Count}");
                Debug.Log($"Входы: Детектор({detectors.Count * 2}), Движение(2), Цель(4), Пред.действия(2), Время(1) = {inputs.Count}");
                return;
            }
            
            // Get outputs from neural network
            float[] outputs = brain.FeedForward(inputs.ToArray());
            
            // Set movement based on neural network output
            vertical_input = (outputs[0] * 2) - 1;  // Convert from [0,1] to [-1,1]
            horizontal_input = (outputs[1] * 2) - 1;
            
            // Улучшенная система наград
            float speed_change = Mathf.Abs(vertical_input - last_vertical_input);
            float rotation_change = Mathf.Abs(horizontal_input - last_horizontal_input);
            
            // Награда за движение к цели
            if (target_transform != null)
            {
                Vector3 moveDirection = rb_transform.forward * vertical_input;
                Vector3 targetDirection = (target_transform.position - rb_transform.position).normalized;
                float alignmentReward = Vector3.Dot(moveDirection, targetDirection) * 0.1f;
                
                if (alignmentReward > 0)
                {
                    fitness += alignmentReward * Time.fixedDeltaTime;
                    if (brain != null) brain.fitness = fitness;
                }
            }
            
            // Награда за эффективное использование поворотов
            if (rotation_change > 0.1f && Mathf.Abs(angleToTarget) > 0.1f)
            {
                float turnEfficiency = 1f - (Mathf.Abs(angleToTarget) / 180f);
                fitness += rotation_change_reward * turnEfficiency;
                if (brain != null) brain.fitness = fitness;
            }
            
            // Награда за поддержание скорости при правильном направлении
            if (Mathf.Abs(angleToTarget) < 30f && vertical_input > 0.5f)
            {
                fitness += 0.05f * Time.fixedDeltaTime;
                if (brain != null) brain.fitness = fitness;
            }
            
            // Сохраняем текущие значения для следующего кадра
            last_vertical_input = vertical_input;
            last_horizontal_input = horizontal_input;
        }
        
        private void DisableAgent(string reason)
        {
            if (!is_disabled)
            {
                is_disabled = true;
                Debug.Log($"Agent {instance_id} disabled: {reason}");
                
                // Stop all movement
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                
                // Reset inputs
                vertical_input = 0f;
                horizontal_input = 0f;
            }
        }

        private void Move()
        {
            if (!is_disabled)
            {
                // Calculate speed based on direction (slower for backward movement)
                float current_speed = move_speed * (vertical_input < 0 ? 0.5f : 1f);
                
                // Calculate velocity in the direction we're facing
                Vector3 velocity = rb_transform.forward * vertical_input * current_speed;
                
                // Apply the velocity directly to rigidbody (preserve Y velocity for gravity)
                rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
            }
            else
            {
                // Ensure stopped when disabled
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
        }
        
        private void Rotate()
        {
            if (!is_disabled && Mathf.Abs(horizontal_input) > 0.1f)
            {
                // Rotate left/right
                float angle = horizontal_input * rotation_speed * Time.fixedDeltaTime;
                
                // Apply rotation to the rigidbody directly
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0, angle, 0));
            }
        }
        
        void OnCollisionEnter(Collision collision)
        {
            // Ignore collisions with other agents
            if (collision.gameObject.layer == LayerMask.NameToLayer("Agent"))
                return;

            if (is_training)
            {
                // Collision with something - penalty and disable control
                Debug.Log($"Agent {instance_id}: Collision detected with {collision.gameObject.name}");
                fitness -= collision_penalty;
                
                if (simulation_manager != null)
                {
                    brain.fitness = fitness;
                }
                
                // Disable movement after collision
                DisableAgent("Collided with wall");
                
                if (auto_reset)
                {
                    ResetAgent();
                }
            }
        }
        
        private void ResetAgent()
        {
            // Reset all states
            fitness = 0f;
            is_disabled = false;
            success_reported = false;
            lifetime = 0f;
            distance_moved = 0f;
            last_position = rb_transform.position;
            last_vertical_input = 0f;
            last_horizontal_input = 0f;
        }
        
        // Getters/Setters for SimulationManager
        public float GetFitness() { return fitness; }
        public void SetFitness(float value) { fitness = value; }
        public NeuralNetwork GetBrain() { return brain; }
        public bool IsSuccessful() { return success_reported; }

        // Get distance to target
        public float GetDistanceToTarget()
        {
            try
            {
                if (target_transform == null)
                {
                    GameObject target = GameObject.FindGameObjectWithTag("AIM");
                    if (target != null)
                    {
                        target_transform = target.transform;
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Агент {instance_id}: Цель не найдена!");
                        return float.MaxValue; // Возвращаем максимальную дистанцию если цель не найдена
                    }
                }

                if (rb_transform == null)
                {
                    Debug.LogWarning($"⚠️ Агент {instance_id}: rb_transform не инициализирован!");
                    return float.MaxValue;
                }
                
                return Vector3.Distance(rb_transform.position, target_transform.position);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка в GetDistanceToTarget для агента {instance_id}: {e.Message}");
                return float.MaxValue;
            }
        }

        private void HandleSuccess(float distance_to_target)
        {
            if (success_reported) return; // Prevent multiple success reports
            
            // Found the target - big reward!
            Debug.Log($"🎯 УСПЕХ! Agent {instance_id} достиг цели! Дистанция: {distance_to_target:F2}м, Fitness: {fitness}");
            
            // Базовая награда за достижение цели
            fitness += target_reward;
            
            // Бонус за скорость прохождения!
            float time_taken = Time.time - generation_start_time;
            float max_time_bonus = target_reward * time_bonus_multiplier;
            
            // Нормализуем время относительно минимального времени и максимального
            float normalized_time = Mathf.Clamp01((time_taken - min_time_for_max_bonus) / (max_lifetime - min_time_for_max_bonus));
            float time_bonus = max_time_bonus * (1f - normalized_time);
            
            // Если прошёл быстрее минимального времени - даём максимальный бонус
            if (time_taken <= min_time_for_max_bonus)
            {
                time_bonus = max_time_bonus;
            }
            
            // Добавляем временной бонус
            if (time_bonus > 0)
            {
                fitness += time_bonus;
                Debug.Log($"⚡ Скоростной бонус для Agent {instance_id}: +{time_bonus:F2} " +
                         $"(Время: {time_taken:F2}с, {(time_bonus/max_time_bonus*100):F0}% от макс. бонуса)");
            }
            
            brain.fitness = fitness;
            success_reported = true;
            
            // Let simulation manager know about the success
            if (simulation_manager != null)
            {
                simulation_manager.ReportSuccess(this);
                Debug.Log($"Success reported to SimulationManager for agent {instance_id}");
            }
            else
            {
                Debug.LogError($"SimulationManager not found for agent {instance_id}!");
            }
            
            // Disable movement after success
            DisableAgent("Reached target");
            
            if (destroy_on_success)
            {
                // Destroy the agent
                Debug.Log($"Destroying successful agent {instance_id}");
                Destroy(gameObject);
            }
            else
            {
                // Teleport far away
                Debug.Log($"Teleporting successful agent {instance_id} to {teleport_position}");
                transform.position = teleport_position;
                if (rb != null)
                {
                    rb.position = teleport_position;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            
            if (auto_reset)
            {
                ResetAgent();
            }
        }

        // Метод для получения количества детекторов
        public int GetDetectorsCount()
        {
            return detectors.Count;
        }
    }
}
