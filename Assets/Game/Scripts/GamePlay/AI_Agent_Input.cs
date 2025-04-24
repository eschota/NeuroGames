using UnityEngine;

public class AI_Agent_Input : MonoBehaviour
{
    public HingeJoint Joint { get; set; }
    
    [Header("Current Values")]
    [SerializeField] private float currentAngle;
    [SerializeField] private float normalizedAngle; // -1 to 1 based on joint limits
    
    [Header("Neural Network Input/Output")]
    [SerializeField] private float targetAngle; // Set by neural network (-180 to 180)
    
    // Use properties with safety checks
    public float CurrentAngle 
    { 
        get 
        {
            // Make sure we don't return NaN values
            return float.IsNaN(currentAngle) ? 0f : currentAngle;
        }
    }
    
    public float NormalizedAngle 
    { 
        get 
        {
            // Make sure we don't return NaN values
            return float.IsNaN(normalizedAngle) ? 0f : normalizedAngle;
        }
    }
    
    public float TargetAngle 
    { 
        get => float.IsNaN(targetAngle) ? 0f : targetAngle;
        set => targetAngle = float.IsNaN(value) ? 0f : value;
    }
    
    public void UpdateValues()
    {
        if (Joint == null) return;
        
        try
        {
            // Get current joint angle
            currentAngle = GetJointAngle();
            
            // Normalize angle based on joint limits
            if (Joint.useLimits)
            {
                JointLimits limits = Joint.limits;
                float range = limits.max - limits.min;
                if (range > 0)
                {
                    // Map to -1 to 1, with safety check
                    float normalized = 2f * (currentAngle - limits.min) / range - 1f;
                    normalizedAngle = Mathf.Clamp(normalized, -1f, 1f);
                }
                else
                {
                    // If range is 0 or negative, just use 0
                    normalizedAngle = 0;
                }
            }
            else
            {
                // If no limits are used, normalize to -1 to 1 based on full 360° range
                normalizedAngle = Mathf.Clamp(currentAngle / 180f, -1f, 1f);
            }
            
            // Safety check for NaN values
            if (float.IsNaN(normalizedAngle) || float.IsInfinity(normalizedAngle))
            {
                normalizedAngle = 0f;
            }
        }
        catch
        {
            // If any error occurs, reset to safe values
            currentAngle = 0f;
            normalizedAngle = 0f;
        }
    }
    
    private float GetJointAngle()
    {
        if (Joint == null) return 0f;
        
        try
        {
            // In Unity, joint.angle gives us the current angle
            return Joint.angle;
        }
        catch
        {
            // Return 0 if anything goes wrong
            return 0f;
        }
    }
} 