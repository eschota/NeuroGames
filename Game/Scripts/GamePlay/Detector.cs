using UnityEngine;

public class Detector : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private float ray_length = 10f;
    [SerializeField] private float color_transition_time = 0.5f;
    [SerializeField] private LayerMask aim_layer; // Layer for the target
    private LayerMask raycast_mask; // Маска для рейкаста (все слои кроме Agent)
    
    [Header("Colors")]
    [SerializeField] private Color no_hit_color = Color.gray;
    [SerializeField] private Color far_hit_color = Color.green;
    [SerializeField] private Color close_hit_color = Color.red;
    [SerializeField] private Color aim_hit_color = Color.yellow; // Color when hit AIM
    
    private Renderer target_renderer;
    private Material target_material;
    private Color current_color;
    private Color target_color;
    private float color_lerp_timer = 0f;
    
    public float[] OUTPUT = new float[2]; // [0] = normalized distance, [1] = object type
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Get the renderer from this object
        target_renderer = GetComponent<Renderer>();
        if (target_renderer == null)
        {
            Debug.LogError("No Renderer found on this object! Add a renderer with a material.");
            enabled = false;
            return;
        }
        
        // Store reference to material
        target_material = target_renderer.material;
        
        // Initialize colors and output
        current_color = no_hit_color;
        target_color = no_hit_color;
        target_material.color = current_color;
        
        // Set up the aim layer mask
        aim_layer = LayerMask.GetMask("AIM");
        
        // Настраиваем маску рейкаста: все слои кроме Agent
        int allLayers = ~0; // Все слои (побитовое НЕ от нуля = все биты в 1)
        int agentLayer = 1 << LayerMask.NameToLayer("Agent"); // Слой Agent
        raycast_mask = allLayers & ~agentLayer; // Все слои кроме Agent
        
        // Initialize OUTPUT array
        OUTPUT[0] = -1f; // No hit initially
        OUTPUT[1] = 0f;  // No object
    }

    // Update is called once per frame
    void Update()
    {
        // Cast a ray forward along the Z axis and update OUTPUT
        DetectEnvironment();
        
        // Lerp the color over time
        UpdateMaterialColor();
    }
    
    private void DetectEnvironment()
    {
        // Create a ray from the object's position pointing forward
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;
        
        // Cast the ray and check for hits, используя маску для игнора агентов
        if (Physics.Raycast(ray, out hit, ray_length, raycast_mask))
        {
            // Get normalized distance (0 = very close, 1 = max distance)
            float normalized_distance = hit.distance / ray_length;
            OUTPUT[0] = normalized_distance;
            
            // Check if the hit object is the AIM (target) - either by layer or tag
            bool isTarget = hit.collider.gameObject.layer == LayerMask.NameToLayer("AIM") || 
                          hit.collider.CompareTag("AIM");
            
            if (isTarget)
            {
                OUTPUT[1] = 1f; // Target hit
                target_color = aim_hit_color;
                // Draw ray in green when hitting target
                Debug.DrawRay(transform.position, transform.forward * hit.distance, Color.green);
                // Draw remaining ray length in blue
                Debug.DrawRay(hit.point, transform.forward * (ray_length - hit.distance), Color.blue);
                
                Debug.Log($"Hit TARGET! Distance: {hit.distance:F2}"); // Debug log
            }
            else
            {
                OUTPUT[1] = -1f; // Non-target hit
                
                // Set color based on distance
                target_color = hit.distance > 1.0f ? far_hit_color : close_hit_color;
                // Draw ray in red when hitting obstacles
                Debug.DrawRay(transform.position, transform.forward * hit.distance, Color.red);
                // Draw remaining ray length in blue
                Debug.DrawRay(hit.point, transform.forward * (ray_length - hit.distance), Color.blue);
            }
        }
        else
        {
            // No hit
            OUTPUT[0] = -1f;
            OUTPUT[1] = 0f;
            target_color = no_hit_color;
            // Draw full ray in blue when no hit
            Debug.DrawRay(transform.position, transform.forward * ray_length, Color.blue);
        }
    }
    
    private void UpdateMaterialColor()
    {
        // Reset timer if color changed
        if (target_color != current_color && color_lerp_timer <= 0)
        {
            color_lerp_timer = color_transition_time;
        }
        
        // If we're in transition
        if (color_lerp_timer > 0)
        {
            // Calculate how far along the transition we are (1.0 = start, 0.0 = end)
            float t = color_lerp_timer / color_transition_time;
            
            // Invert for Lerp (0.0 = start, 1.0 = end)
            float lerp_factor = 1f - t;
            
            // Lerp the color
            current_color = Color.Lerp(current_color, target_color, lerp_factor);
            
            // Apply to material
            target_material.color = current_color;
            
            // Decrement timer
            color_lerp_timer -= Time.deltaTime;
        }
        else if (current_color != target_color)
        {
            // Ensure we reach the final color
            current_color = target_color;
            target_material.color = current_color;
        }
    }
}
