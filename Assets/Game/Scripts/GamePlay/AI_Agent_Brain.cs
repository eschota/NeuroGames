using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AI_Agent))]
public class AI_Agent_Brain : MonoBehaviour
{
    private AI_Agent agent;
    private NeuralNetwork neuralNetwork;
    private Dictionary<HingeJoint, float> jointTargets = new Dictionary<HingeJoint, float>();
    private List<HingeJoint> jointsList = new List<HingeJoint>();
    private List<AI_Agent_Input> inputComponents = new List<AI_Agent_Input>();
    
    [SerializeField] private bool isActive = false; // Start as inactive
    [SerializeField] private int inputLayerSize = 0;
    [SerializeField] private int outputLayerSize = 0;
    
    // Public accessors for layer sizes
    public int InputLayerSize => inputLayerSize;
    public int OutputLayerSize => outputLayerSize;
    
    // For tracking problematic inputs
    private bool loggedInputWarning = false;
    
    private void Awake()
    {
        // Get the agent component
        agent = GetComponent<AI_Agent>();
        
        // Initialize the joint components - moved from Initialize to ensure this happens early
        FindAndSetupJoints();
    }
    
    private void FindAndSetupJoints()
    {
        // Clear existing lists
        inputComponents.Clear();
        jointsList.Clear();
        
        // First wait for AI_Agent to setup its joints and input components
        if (agent != null)
        {
            // Wait one frame to ensure AI_Agent has initialized its components
            StartCoroutine(DelayedJointSetup());
        }
    }
    
    private System.Collections.IEnumerator DelayedJointSetup()
    {
        // Wait for end of frame to ensure AI_Agent has created AI_Agent_Input components
        yield return new WaitForEndOfFrame();
        
        // Find all AI_Agent_Input components in children
        GetComponentsInChildren(inputComponents);
        
        Debug.Log($"Found {inputComponents.Count} AI_Agent_Input components");
        
        // Get all joints from input components
        foreach (var input in inputComponents)
        {
            if (input != null && input.Joint != null)
            {
                jointsList.Add(input.Joint);
            }
        }
        
        Debug.Log($"Added {jointsList.Count} joints to the list");
        
        // Set input and output sizes
        inputLayerSize = inputComponents.Count * 2; // Current angle + velocity for each joint
        outputLayerSize = jointsList.Count; // Target position for each joint
        
        Debug.Log($"Set inputLayerSize={inputLayerSize}, outputLayerSize={outputLayerSize}");
        
        // Initialize joint targets dictionary
        jointTargets.Clear();
        foreach (var joint in jointsList)
        {
            jointTargets[joint] = 0f;
        }
    }
    
    // Called by SimulationManager
    public void Initialize(NeuralNetwork network)
    {
        // Store the provided neural network
        neuralNetwork = network;
        
        // Debug current setup
        Debug.Log($"Initializing AI_Agent_Brain with network: {(network != null ? "Valid" : "Null")}, " +
                 $"inputSize={inputLayerSize}, outputSize={outputLayerSize}, joints={jointsList.Count}");
        
        // Reset the fitness - let's access this via the agent to make sure it's valid
        if (agent != null)
        {
            // Just access fitness to ensure agent is working properly
            float currentFitness = agent.GetFitness();
        }
        else
        {
            Debug.LogError("AI_Agent is null in Initialize method");
        }
        
        // Reset tracking
        loggedInputWarning = false;
    }
    
    private void FixedUpdate()
    {
        if (!isActive || neuralNetwork == null || agent == null) return;
        
        // Additional check to ensure we have valid joint configuration
        if (jointsList.Count == 0 || inputComponents.Count == 0)
        {
            Debug.LogWarning("No joints or input components found, trying to set up joints again");
            FindAndSetupJoints();
            return;
        }
        
        // Get inputs from agent
        float[] inputs = GetNeuralNetworkInputs();
        
        // Process through neural network
        float[] outputs = neuralNetwork.FeedForward(inputs);
        
        // Set outputs to agent
        ProcessNeuralNetworkOutputs(outputs);
    }
    
    private float[] GetNeuralNetworkInputs()
    {
        // Create input array for neural network
        float[] inputs = new float[inputLayerSize];
        int index = 0;
        bool hasInvalidInputs = false;
        
        // Add current angle and velocity for each joint
        foreach (var input in inputComponents)
        {
            if (input != null)
            {
                try
                {
                    // Get the normalized angle, safely handling NaN
                    float normalizedAngle = input.NormalizedAngle;
                    float angleVelocity = 0f;
                    
                    // Check for NaN/Infinity and replace with safe values
                    if (float.IsNaN(normalizedAngle) || float.IsInfinity(normalizedAngle))
                    {
                        normalizedAngle = 0f;
                        hasInvalidInputs = true;
                    }
                    
                    // Try to calculate the velocity safely
                    try 
                    {
                        // Angular velocity (normalized) - we'll use the difference between current and target angle as a simple approximation
                        angleVelocity = (input.TargetAngle - input.CurrentAngle) / 360f;
                        
                        // Check for NaN/Infinity and replace with safe values
                        if (float.IsNaN(angleVelocity) || float.IsInfinity(angleVelocity))
                        {
                            angleVelocity = 0f;
                            hasInvalidInputs = true;
                        }
                    }
                    catch
                    {
                        // If calculation fails, use a safe default
                        angleVelocity = 0f;
                        hasInvalidInputs = true;
                    }
                    
                    // Add normalized angle (-1 to 1) to inputs
                    inputs[index++] = normalizedAngle;
                    
                    // Add velocity to inputs
                    inputs[index++] = angleVelocity;
                }
                catch 
                {
                    // If anything goes wrong, use zeros
                    inputs[index++] = 0f;
                    inputs[index++] = 0f;
                    hasInvalidInputs = true;
                }
            }
            else
            {
                // Use zeros for missing inputs
                inputs[index++] = 0f;
                inputs[index++] = 0f;
                hasInvalidInputs = true;
            }
        }
        
        // Log a warning if we encountered invalid inputs, but only once
        if (hasInvalidInputs && !loggedInputWarning)
        {
            Debug.LogWarning("AI_Agent_Brain encountered invalid inputs (NaN or Infinity). Using default values instead.");
            loggedInputWarning = true;
        }
        
        return inputs;
    }
    
    private void ProcessNeuralNetworkOutputs(float[] outputs)
    {
        // Check if outputs match our expected size
        if (outputLayerSize == 0)
        {
            Debug.LogError($"Output layer size is 0, joints were not properly initialized");
            return;
        }
        
        if (outputs == null || outputs.Length == 0)
        {
            Debug.LogError("Neural network returned null or empty outputs");
            return;
        }
        
        if (outputs.Length != outputLayerSize)
        {
            Debug.LogError($"Neural network output size mismatch! Expected {outputLayerSize}, got {outputs.Length}");
            return;
        }
        
        // Set target positions for each joint
        for (int i = 0; i < jointsList.Count && i < outputs.Length; i++)
        {
            // Skip null joints that might have been destroyed
            if (jointsList[i] == null) continue;
            
            // Ensure the neural network output is within [-1, 1] range
            float clampedOutput = Mathf.Clamp(outputs[i], -1f, 1f);
            
            // Convert neural network output (-1 to 1) to joint target position (-180 to 180)
            float targetPosition = clampedOutput * 180f;
            
            // Assign target position
            jointTargets[jointsList[i]] = targetPosition;
        }
        
        // Apply to agent
        agent.SetNeuralNetworkOutputs(jointTargets);
    }
    
    public void SetActive(bool active)
    {
        // Only do something if the state actually changes
        //if (active != isActive) - Убрал эту проверку, чтобы UI всегда обновлялся
        {
            isActive = active;
            
            // Toggle kinematic mode on all rigidbodies
            if (agent != null)
            {
                // Set kinematic to !active (kinematic = true means physics are disabled)
                agent.SetKinematic(!active);
                
                Debug.Log($"AI_Agent_Brain activated: {active}, set kinematic to {!active}");
            }
            
            if (active)
            {
                // Создаем текстовый элемент UI для отображения фитнеса
                AgentFitnessUI.CreateAgentFitnessText(this);
            }
            else
            {
                // Удаляем текстовый элемент UI при деактивации
                AgentFitnessUI.RemoveAgentFitnessText(this);
            }
        }
    }
    
    public float GetFitness()
    {
        return agent != null ? agent.GetFitness() : 0f;
    }
    
    public AI_Agent GetAgent()
    {
        return agent;
    }
    
    public void SetNeuralNetwork(NeuralNetwork network)
    {
        neuralNetwork = network;
    }
    
    public NeuralNetwork GetNeuralNetwork()
    {
        return neuralNetwork;
    }
    
    // При уничтожении мозга, удаляем его текстовый элемент
    private void OnDestroy()
    {
        AgentFitnessUI.RemoveAgentFitnessText(this);
    }
} 