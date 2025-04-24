using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NeuralNetwork
{
    // Max values to help avoid numerical instability
    private const float MAX_WEIGHT_VALUE = 10.0f;
    private const float MAX_BIAS_VALUE = 10.0f;
    private const float MAX_INPUT_VALUE = 10.0f;
    
    // Counter for logging NaN values (to reduce log spam)
    private static int nanErrorCounter = 0;
    private static float lastNanErrorTime = 0f;

    [Serializable]
    public class SerializableMatrix
    {
        public int rows;
        public int cols;
        public float[] data;

        public SerializableMatrix(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
            this.data = new float[rows * cols];
        }

        public SerializableMatrix(float[,] matrix)
        {
            rows = matrix.GetLength(0);
            cols = matrix.GetLength(1);
            data = new float[rows * cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    data[i * cols + j] = matrix[i, j];
                }
            }
        }

        public float[,] ToMatrix()
        {
            if (data == null)
            {
                Debug.LogError("SerializableMatrix data is null");
                return new float[1, 1];
            }

            float[,] matrix = new float[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (i * cols + j < data.Length)
                    {
                        matrix[i, j] = data[i * cols + j];
                    }
                }
            }

            return matrix;
        }
    }
    
    [Serializable]
    public class SerializableLayer
    {
        public float[] neurons;
        public SerializableMatrix weights;
        public float[] biases;
        
        public SerializableLayer(Layer layer)
        {
            if (layer == null)
            {
                Debug.LogError("Cannot serialize null layer");
                neurons = new float[1];
                weights = new SerializableMatrix(1, 1);
                biases = new float[1];
                return;
            }
            
            neurons = layer.neurons != null ? layer.neurons : new float[1];
            weights = layer.weights != null ? new SerializableMatrix(layer.weights) : new SerializableMatrix(1, 1);
            biases = layer.biases != null ? layer.biases : new float[1];
        }
        
        public Layer ToLayer()
        {
            Layer layer = new Layer(weights.cols, neurons.Length);
            layer.neurons = neurons;
            layer.weights = weights.ToMatrix();
            layer.biases = biases;
            return layer;
        }
    }
    
    [Serializable]
    public class SerializableNetwork
    {
        public List<SerializableLayer> layers = new List<SerializableLayer>();
        public int seed;
        
        public SerializableNetwork(NeuralNetwork network)
        {
            if (network == null || network.layers == null)
            {
                Debug.LogError("Cannot serialize null network");
                return;
            }
            
            foreach (var layer in network.layers)
            {
                layers.Add(new SerializableLayer(layer));
            }
            
            seed = network.random != null ? network.random.Next() : 0;
        }
        
        public NeuralNetwork ToNeuralNetwork()
        {
            NeuralNetwork network = new NeuralNetwork();
            network.random = new System.Random(seed);
            network.layers.Clear();
            
            foreach (var serLayer in layers)
            {
                network.layers.Add(serLayer.ToLayer());
            }
            
            return network;
        }
    }

    [Serializable]
    public class Layer
    {
        public float[] neurons;
        public float[,] weights;
        public float[] biases;

        public Layer(int inputSize, int outputSize)
        {
            neurons = new float[outputSize];
            weights = new float[outputSize, inputSize];
            biases = new float[outputSize];
        }
    }

    public List<Layer> layers = new List<Layer>();
    public System.Random random;

    // Default constructor for serialization
    public NeuralNetwork()
    {
        random = new System.Random(0);
    }

    public NeuralNetwork(int[] layerSizes, int seed = 0)
    {
        if (layerSizes == null || layerSizes.Length < 2)
        {
            Debug.LogError("Neural network needs at least 2 layers (input and output)");
            // Create a minimal default network to avoid null reference
            layerSizes = new int[] { 1, 1 };
        }
        
        random = new System.Random(seed);

        for (int i = 0; i < layerSizes.Length - 1; i++)
        {
            if (layerSizes[i] <= 0 || layerSizes[i+1] <= 0)
            {
                Debug.LogError($"Layer size must be positive. Found invalid size at index {i}: {layerSizes[i]}");
                // Use minimum size of 1 to avoid errors
                layerSizes[i] = Math.Max(1, layerSizes[i]);
                layerSizes[i+1] = Math.Max(1, layerSizes[i+1]);
            }
            
            Layer layer = new Layer(layerSizes[i], layerSizes[i + 1]);
            InitializeLayer(layer);
            layers.Add(layer);
        }
    }

    // Copy constructor for creating mutations
    public NeuralNetwork(NeuralNetwork parent)
    {
        if (parent == null || parent.layers == null || parent.layers.Count == 0)
        {
            Debug.LogError("Cannot create network from null or empty parent");
            // Create a minimal network
            random = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
            Layer defaultLayer = new Layer(1, 1);
            InitializeLayer(defaultLayer);
            layers.Add(defaultLayer);
            return;
        }
        
        random = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));

        foreach (var parentLayer in parent.layers)
        {
            if (parentLayer == null || parentLayer.weights == null)
            {
                Debug.LogError("Parent layer or weights are null");
                continue;
            }
            
            int inputSize = parentLayer.weights.GetLength(1);
            int outputSize = parentLayer.weights.GetLength(0);
            
            Layer childLayer = new Layer(inputSize, outputSize);
            
            // Copy weights and biases
            for (int i = 0; i < parentLayer.biases.Length; i++)
            {
                childLayer.biases[i] = parentLayer.biases[i];
                
                for (int j = 0; j < parentLayer.weights.GetLength(1); j++)
                {
                    childLayer.weights[i, j] = parentLayer.weights[i, j];
                }
            }
            
            layers.Add(childLayer);
        }
    }

    private void InitializeLayer(Layer layer)
    {
        if (layer == null || layer.weights == null || layer.biases == null || layer.neurons == null)
        {
            Debug.LogError("Cannot initialize null layer or components");
            return;
        }
        
        int inputSize = layer.weights.GetLength(1);
        int outputSize = layer.neurons.Length;
        
        // Xavier initialization with numerical stability
        float weightRange = Mathf.Min(0.5f, (float)Math.Sqrt(6f / (inputSize + outputSize)));
        
        for (int i = 0; i < layer.biases.Length; i++)
        {
            // Initialize biases with small values
            layer.biases[i] = Mathf.Clamp(((float)random.NextDouble() * 2 - 1) * 0.1f, -MAX_BIAS_VALUE, MAX_BIAS_VALUE);
            
            for (int j = 0; j < inputSize; j++)
            {
                // Initialize weights with Xavier but clamped
                layer.weights[i, j] = Mathf.Clamp(((float)random.NextDouble() * 2 - 1) * weightRange, -MAX_WEIGHT_VALUE, MAX_WEIGHT_VALUE);
            }
        }
    }

    public float[] FeedForward(float[] inputs)
    {
        // Check for null or empty inputs
        if (inputs == null)
        {
            LogRateLimited("Neural network received null inputs");
            return new float[0];
        }
        
        // Check for empty network
        if (layers == null || layers.Count == 0)
        {
            LogRateLimited("Neural network has no layers");
            return new float[0];
        }
        
        // Check input size matches first layer
        Layer firstLayer = layers[0];
        if (firstLayer == null || firstLayer.weights == null)
        {
            LogRateLimited("First layer or weights are null");
            return new float[0];
        }
        
        int expectedInputs = firstLayer.weights.GetLength(1);
        if (inputs.Length != expectedInputs)
        {
            LogRateLimited($"Input size mismatch: Expected {expectedInputs}, got {inputs.Length}");
            
            // Adjust inputs to match expected size
            float[] adjustedInputs = new float[expectedInputs];
            int copyLength = Math.Min(inputs.Length, expectedInputs);
            
            for (int i = 0; i < copyLength; i++)
            {
                adjustedInputs[i] = inputs[i];
            }
            
            inputs = adjustedInputs;
        }
        
        // Clamp inputs to prevent extreme values
        float[] clampedInputs = new float[inputs.Length];
        bool hasNaN = false;
        int nanCount = 0;
        for (int i = 0; i < inputs.Length; i++)
        {
            if (float.IsNaN(inputs[i]) || float.IsInfinity(inputs[i]))
            {
                clampedInputs[i] = 0f;
                hasNaN = true;
                nanCount++;
            }
            else
            {
                clampedInputs[i] = Mathf.Clamp(inputs[i], -MAX_INPUT_VALUE, MAX_INPUT_VALUE);
            }
        }
        
        // Only log NaN inputs periodically to avoid spamming the log
        if (hasNaN && ShouldLogNanError())
        {
            Debug.LogWarning($"Neural network received {nanCount} NaN inputs. Replacing with 0.");
        }
        
        float[] outputs = clampedInputs;
        
        try
        {
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                if (layer == null || layer.neurons == null || layer.weights == null || layer.biases == null)
                {
                    LogRateLimited($"Layer {layerIndex} or its components are null");
                    continue;
                }
                
                // Calculate outputs for this layer
                for (int i = 0; i < layer.neurons.Length; i++)
                {
                    // Start with bias
                    float sum = layer.biases[i];
                    bool hasLayerNaN = false;
                    
                    // Add weighted inputs
                    for (int j = 0; j < outputs.Length && j < layer.weights.GetLength(1); j++)
                    {
                        float weightedInput = outputs[j] * layer.weights[i, j];
                        
                        // Check for NaN before adding
                        if (float.IsNaN(weightedInput) || float.IsInfinity(weightedInput))
                        {
                            hasLayerNaN = true;
                            continue;
                        }
                        
                        sum += weightedInput;
                    }
                    
                    // Protect against NaN
                    if (hasLayerNaN || float.IsNaN(sum) || float.IsInfinity(sum))
                    {
                        if (ShouldLogNanError())
                        {
                            Debug.LogWarning($"NaN detected in layer {layerIndex}, neuron {i}. Resetting to 0.");
                        }
                        sum = 0;
                    }
                    
                    // Clamp sum to avoid extreme values before activation
                    sum = Mathf.Clamp(sum, -20f, 20f);
                    
                    // Apply tanh activation safely
                    layer.neurons[i] = (float)System.Math.Tanh(sum);
                }
                
                outputs = layer.neurons;
            }
        }
        catch (Exception e)
        {
            LogRateLimited($"Exception in neural network: {e.Message}\n{e.StackTrace}");
            return new float[layers[layers.Count - 1].neurons.Length];
        }
        
        // Ensure all outputs are in the correct range [-1, 1]
        bool hasNanOutput = false;
        for (int i = 0; i < outputs.Length; i++)
        {
            if (float.IsNaN(outputs[i]) || float.IsInfinity(outputs[i]))
            {
                outputs[i] = 0f;
                hasNanOutput = true;
            }
            else
            {
                // Tanh should already constrain to [-1, 1], but let's be extra sure
                outputs[i] = Mathf.Clamp(outputs[i], -1f, 1f);
            }
        }
        
        if (hasNanOutput && ShouldLogNanError())
        {
            Debug.LogWarning("Neural network produced invalid outputs. Replacing with 0.");
        }
        
        return outputs;
    }

    public void Mutate(float mutationRate, float mutationStrength)
    {
        if (layers == null)
        {
            Debug.LogError("Cannot mutate null layers");
            return;
        }
        
        foreach (var layer in layers)
        {
            if (layer == null || layer.weights == null || layer.biases == null)
            {
                Debug.LogError("Cannot mutate null layer or components");
                continue;
            }
            
            for (int i = 0; i < layer.biases.Length; i++)
            {
                // Mutate bias
                if (random.NextDouble() < mutationRate)
                {
                    // Create a safe mutation value
                    float mutation = (float)(random.NextDouble() * 2 - 1) * mutationStrength;
                    float newBias = layer.biases[i] + mutation;
                    
                    // Clamp to prevent extreme values
                    layer.biases[i] = Mathf.Clamp(newBias, -MAX_BIAS_VALUE, MAX_BIAS_VALUE);
                }
                
                // Mutate weights
                for (int j = 0; j < layer.weights.GetLength(1); j++)
                {
                    if (random.NextDouble() < mutationRate)
                    {
                        // Create a safe mutation value
                        float mutation = (float)(random.NextDouble() * 2 - 1) * mutationStrength;
                        float newWeight = layer.weights[i, j] + mutation;
                        
                        // Clamp to prevent extreme values
                        layer.weights[i, j] = Mathf.Clamp(newWeight, -MAX_WEIGHT_VALUE, MAX_WEIGHT_VALUE);
                    }
                }
            }
        }
    }

    // Crossover with another network to produce a child
    public static NeuralNetwork Crossover(NeuralNetwork parent1, NeuralNetwork parent2)
    {
        if (parent1 == null || parent2 == null)
        {
            Debug.LogError("Cannot crossover with null parents");
            return new NeuralNetwork(new int[] { 1, 1 });
        }
        
        // Check if network structures match
        if (parent1.layers.Count != parent2.layers.Count)
        {
            Debug.LogError("Parent networks have different structures");
            return new NeuralNetwork(parent1); // Just return a copy of parent1
        }
        
        NeuralNetwork child = new NeuralNetwork(parent1);
        System.Random rand = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
        
        for (int l = 0; l < child.layers.Count && l < parent2.layers.Count; l++)
        {
            var childLayer = child.layers[l];
            var parent2Layer = parent2.layers[l];
            
            if (childLayer == null || parent2Layer == null)
            {
                Debug.LogError("Layer is null during crossover");
                continue;
            }
            
            if (childLayer.biases.Length != parent2Layer.biases.Length ||
                childLayer.weights.GetLength(0) != parent2Layer.weights.GetLength(0) ||
                childLayer.weights.GetLength(1) != parent2Layer.weights.GetLength(1))
            {
                Debug.LogError("Layer dimensions mismatch during crossover");
                continue;
            }
            
            for (int i = 0; i < childLayer.biases.Length; i++)
            {
                // 50% chance to inherit from each parent
                if (rand.NextDouble() < 0.5)
                {
                    childLayer.biases[i] = parent2Layer.biases[i];
                }
                
                for (int j = 0; j < childLayer.weights.GetLength(1); j++)
                {
                    if (rand.NextDouble() < 0.5)
                    {
                        childLayer.weights[i, j] = parent2Layer.weights[i, j];
                    }
                    
                    // Add occasional small noise during crossover to avoid getting stuck
                    if (rand.NextDouble() < 0.05)
                    {
                        float noise = (float)(rand.NextDouble() * 0.1 - 0.05);
                        childLayer.weights[i, j] += noise;
                        childLayer.weights[i, j] = Mathf.Clamp(childLayer.weights[i, j], -MAX_WEIGHT_VALUE, MAX_WEIGHT_VALUE);
                    }
                }
            }
        }
        
        return child;
    }
    
    // Serialize to JSON for saving
    public string ToJson()
    {
        SerializableNetwork serNetwork = new SerializableNetwork(this);
        return JsonUtility.ToJson(serNetwork);
    }
    
    // Deserialize from JSON for loading
    public static NeuralNetwork FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("Cannot deserialize null or empty JSON");
            return new NeuralNetwork(new int[] { 1, 1 });
        }
        
        try
        {
            SerializableNetwork serNetwork = JsonUtility.FromJson<SerializableNetwork>(json);
            if (serNetwork == null)
            {
                Debug.LogError("Deserialized network is null");
                return new NeuralNetwork(new int[] { 1, 1 });
            }
            
            return serNetwork.ToNeuralNetwork();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error deserializing neural network: {e.Message}\n{e.StackTrace}");
            return new NeuralNetwork(new int[] { 1, 1 });
        }
    }
    
    // Debug method to validate network integrity
    public bool Validate()
    {
        if (layers == null || layers.Count == 0)
        {
            Debug.LogError("Network validation failed: No layers");
            return false;
        }
        
        for (int i = 0; i < layers.Count; i++)
        {
            Layer layer = layers[i];
            
            if (layer == null)
            {
                Debug.LogError($"Network validation failed: Layer {i} is null");
                return false;
            }
            
            if (layer.neurons == null)
            {
                Debug.LogError($"Network validation failed: Layer {i} neurons are null");
                return false;
            }
            
            if (layer.weights == null)
            {
                Debug.LogError($"Network validation failed: Layer {i} weights are null");
                return false;
            }
            
            if (layer.biases == null)
            {
                Debug.LogError($"Network validation failed: Layer {i} biases are null");
                return false;
            }
            
            if (layer.biases.Length != layer.neurons.Length)
            {
                Debug.LogError($"Network validation failed: Layer {i} biases length ({layer.biases.Length}) doesn't match neurons length ({layer.neurons.Length})");
                return false;
            }
            
            if (layer.weights.GetLength(0) != layer.neurons.Length)
            {
                Debug.LogError($"Network validation failed: Layer {i} weights rows ({layer.weights.GetLength(0)}) doesn't match neurons length ({layer.neurons.Length})");
                return false;
            }
            
            if (i > 0 && layer.weights.GetLength(1) != layers[i-1].neurons.Length)
            {
                Debug.LogError($"Network validation failed: Layer {i} weights columns ({layer.weights.GetLength(1)}) doesn't match previous layer neurons length ({layers[i-1].neurons.Length})");
                return false;
            }
            
            // Check for NaN values
            for (int j = 0; j < layer.neurons.Length; j++)
            {
                if (float.IsNaN(layer.neurons[j]) || float.IsInfinity(layer.neurons[j]))
                {
                    Debug.LogError($"Network validation failed: Layer {i} neuron {j} has invalid value: {layer.neurons[j]}");
                    layer.neurons[j] = 0f; // Fix it
                }
                
                if (float.IsNaN(layer.biases[j]) || float.IsInfinity(layer.biases[j]))
                {
                    Debug.LogError($"Network validation failed: Layer {i} bias {j} has invalid value: {layer.biases[j]}");
                    layer.biases[j] = 0f; // Fix it
                    return false;
                }
                
                for (int k = 0; k < layer.weights.GetLength(1); k++)
                {
                    if (float.IsNaN(layer.weights[j, k]) || float.IsInfinity(layer.weights[j, k]))
                    {
                        Debug.LogError($"Network validation failed: Layer {i} weight [{j},{k}] has invalid value: {layer.weights[j, k]}");
                        layer.weights[j, k] = 0f; // Fix it
                        return false;
                    }
                }
            }
        }
        
        return true;
    }
    
    // Add regularization to prevent weights from growing too large
    public void Regularize(float regularizationRate = 0.0001f)
    {
        foreach (var layer in layers)
        {
            for (int i = 0; i < layer.biases.Length; i++)
            {
                // Shrink biases slightly
                layer.biases[i] *= (1f - regularizationRate);
                
                // Shrink weights slightly
                for (int j = 0; j < layer.weights.GetLength(1); j++)
                {
                    layer.weights[i, j] *= (1f - regularizationRate);
                }
            }
        }
    }

    // Helper method to limit log rate and avoid spamming
    private static bool ShouldLogNanError()
    {
        float currentTime = Time.realtimeSinceStartup;
        
        // Only log once every 5 seconds, or every 100 errors
        if (currentTime - lastNanErrorTime > 5f || nanErrorCounter >= 100)
        {
            lastNanErrorTime = currentTime;
            nanErrorCounter = 0;
            return true;
        }
        
        nanErrorCounter++;
        return false;
    }
    
    // Log messages but with rate limiting to reduce spam
    private static void LogRateLimited(string message)
    {
        if (ShouldLogNanError())
        {
            Debug.LogWarning(message);
        }
    }
} 