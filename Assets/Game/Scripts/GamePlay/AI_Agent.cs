using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AI_Agent : MonoBehaviour
{
    [SerializeField] private List<HingeJoint> joints = new List<HingeJoint>();
    [SerializeField] private Transform headTransform;
    [SerializeField] private Vector3 aimTarget = new Vector3(0, 0, 10);
    
    [Header("Body Parts")]
    [SerializeField] private List<Transform> legParts = new List<Transform>(); // Части, которые могут касаться земли
    [SerializeField] private List<Transform> bodyParts = new List<Transform>(); // Части, которые НЕ должны касаться землю
    
    [Header("Fall Detection")]
    [SerializeField] private float headHeightThreshold = 0.5f; // Минимальная высота головы над телом
    [SerializeField] private float bodyFallPenalty = 50.0f; // Штраф за касание земли телом
    [SerializeField] private float headLowPenalty = 30.0f; // Штраф за низкое положение головы
    [SerializeField] private float fallDetectionInterval = 0.2f; // Интервал проверки падения
    [SerializeField] private float groundCheckDistance = 0.1f; // Расстояние для проверки касания земли
    [SerializeField] private LayerMask groundLayer = 1; // Слой земли, по умолчанию Default
    
    private Vector3 centerOfMass;
    private Vector3 previousCenterOfMass;
    private float fitness = 0f;
    private Vector3 absoluteAimTarget;
    private Dictionary<HingeJoint, AI_Agent_Input> jointInputs = new Dictionary<HingeJoint, AI_Agent_Input>();
    private bool inputComponentsInitialized = false;
    private List<Rigidbody> rigidbodies = new List<Rigidbody>();
    private float nextFallCheckTime = 0f;
    private bool hasFallen = false; // Флаг, что агент уже упал (предотвращает накопление штрафов)
    
    // Clamp values for joint target position
    [Header("Joint Constraints")]
    [SerializeField] private float minTargetAngle = -180f;
    [SerializeField] private float maxTargetAngle = 180f;
    [SerializeField] private bool useJointLimitsForClamping = true;
    
    private void Awake()
    {
        // Set absolute aim target at the start
        absoluteAimTarget = transform.TransformPoint(aimTarget);
        
        // Find all joints on validate
        OnValidate();
        
        // Find all rigidbodies and make them kinematic
        CollectRigidbodies();
        SetKinematic(true);
    }

    private void OnValidate()
    {
        // Find all HingeJoint components in the hierarchy
        joints.Clear();
        GetComponentsInChildren(true, joints);
        
        if (joints.Count > 0)
        {
            if (headTransform == null)
            {
                Debug.LogWarning("AI_Agent: Head transform is not assigned, using first joint as reference");
                headTransform = joints[0].transform;
            }
            
            Debug.Log($"Found {joints.Count} joints in AI_Agent hierarchy");
        }
        else
        {
            Debug.LogError("AI_Agent: No HingeJoint components found in hierarchy!");
        }
        
        // Автоматически собрать части тела, если не указаны
        if (legParts.Count == 0 || bodyParts.Count == 0)
        {
            CollectBodyParts();
        }
    }
    
    // Автоматически собирает части тела на основе имен
    private void CollectBodyParts()
    {
        // Очищаем списки перед заполнением
        if (legParts.Count == 0) legParts.Clear();
        if (bodyParts.Count == 0) bodyParts.Clear();
        
        // Собираем все коллайдеры
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        
        foreach (var collider in allColliders)
        {
            // Скипаем неактивные объекты
            if (!collider.gameObject.activeInHierarchy) continue;
            
            string objName = collider.gameObject.name.ToLower();
            
            // Определяем какие части относятся к ногам
            if (objName.Contains("foot") || objName.Contains("leg") || 
                objName.Contains("нога") || objName.Contains("ступня") ||
                objName.Contains("ankle") || objName.Contains("knee"))
            {
                if (!legParts.Contains(collider.transform))
                {
                    legParts.Add(collider.transform);
                }
            }
            // Если это не нога и не голова, то это часть тела
            else if (!objName.Contains("head") && !objName.Contains("голова") &&
                     collider.transform != headTransform)
            {
                if (!bodyParts.Contains(collider.transform))
                {
                    bodyParts.Add(collider.transform);
                }
            }
        }
        
        Debug.Log($"Автоматически определены части тела: {legParts.Count} частей ног, {bodyParts.Count} частей тела");
    }
    
    private void CollectRigidbodies()
    {
        rigidbodies.Clear();
        GetComponentsInChildren(rigidbodies);
        Debug.Log($"Found {rigidbodies.Count} rigidbodies in AI_Agent hierarchy");
    }
    
    public void SetKinematic(bool kinematic)
    {
        // Avoid unnecessary work if no rigidbodies
        if (rigidbodies.Count == 0) return;
        
        // Set kinematic mode on bodies more efficiently
        foreach (var rb in rigidbodies)
        {
            if (rb != null)
            {
                // When disabling kinematic mode, clear velocities AFTER setting kinematic to false
                if (!kinematic) 
                {
                    // First set kinematic to false
                    rb.isKinematic = false;
                    
                    // Then reset velocities - this is valid because the body is no longer kinematic
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                else
                {
                    // When enabling kinematic, just set the flag - don't touch velocities
                    rb.isKinematic = true;
                }
            }
        }
        
        Debug.Log($"Set {rigidbodies.Count} rigidbodies to kinematic: {kinematic}");
    }
    
    private void Start()
    {
        // Create input components for each joint
        CreateInputComponents();
        
        // Calculate initial center of mass
        CalculateCenterOfMass();
        previousCenterOfMass = centerOfMass;
        
        // Сбрасываем флаг падения
        hasFallen = false;
        
        // Если части тела не заданы вручную, соберем их автоматически
        if (legParts.Count == 0 || bodyParts.Count == 0)
        {
            CollectBodyParts();
        }
    }
    
    private void CreateInputComponents()
    {
        if (inputComponentsInitialized) return;
        
        jointInputs.Clear();
        
        foreach (var joint in joints)
        {
            if (joint == null) continue;
            
            if (joint.connectedBody != null)
            {
                // Check if the joint already has an AI_Agent_Input component
                AI_Agent_Input existingInput = joint.gameObject.GetComponent<AI_Agent_Input>();
                
                if (existingInput == null)
                {
                    var input = joint.gameObject.AddComponent<AI_Agent_Input>();
                    input.Joint = joint;
                    jointInputs.Add(joint, input);
                    Debug.Log($"Created AI_Agent_Input for joint: {joint.name}");
                }
                else
                {
                    existingInput.Joint = joint;
                    jointInputs.Add(joint, existingInput);
                    Debug.Log($"Using existing AI_Agent_Input for joint: {joint.name}");
                }
            }
            else
            {
                Debug.LogError($"Joint {joint.name} has no connected body!");
            }
        }
        
        inputComponentsInitialized = true;
        Debug.Log($"Created {jointInputs.Count} input components for AI_Agent");
    }

    private void FixedUpdate()
    {
        // Make sure input components are created
        if (!inputComponentsInitialized)
        {
            CreateInputComponents();
        }
        
        // Calculate center of mass
        previousCenterOfMass = centerOfMass;
        CalculateCenterOfMass();
        
        // Проверяем падение с определенным интервалом для оптимизации
        if (Time.time >= nextFallCheckTime)
        {
            nextFallCheckTime = Time.time + fallDetectionInterval;
            CheckForFalling();
        }
        
        // Calculate reward for head position
        CalculateHeadPositionReward();
        
        // Calculate reward for distance to aim target
        CalculateAimTargetReward();
        
        // Update neural network inputs and outputs
        UpdateJointAngles();
    }
    
    // Проверяет условия падения - НЕ добавляет штраф, только визуально отмечает упавших
    private void CheckForFalling()
    {
        // Если уже упал, то не проверяем снова
        if (hasFallen) return;
        
        bool bodyTouchingGround = IsBodyTouchingGround();
        bool headTooLow = IsHeadTooLow();
        
        // Если какое-то из условий падения выполнено
        if (bodyTouchingGround || headTooLow)
        {
            // Отмечаем агента как упавшего, чтобы не проверять повторно
            hasFallen = true;
            
            // Только визуальная индикация падения без штрафов
            ApplyFallVisualEffect();
            
            string fallReason = bodyTouchingGround ? "тело касается земли" : "голова слишком низко";
            Debug.Log($"Агент {gameObject.name} упал ({fallReason}), но штраф не применяется");
        }
    }
    
    // Проверяет, касается ли тело (не ноги) земли
    private bool IsBodyTouchingGround()
    {
        foreach (var part in bodyParts)
        {
            if (part == null) continue;
            
            // Проверяем касание земли с помощью Raycast вниз
            RaycastHit hit;
            if (Physics.Raycast(part.position, Vector3.down, out hit, groundCheckDistance, groundLayer))
            {
                Debug.DrawLine(part.position, hit.point, Color.red, fallDetectionInterval);
                return true;
            }
            
            // Дополнительная проверка с SphereCast для лучшего определения
            if (Physics.SphereCast(part.position, 0.05f, Vector3.down, out hit, groundCheckDistance, groundLayer))
            {
                Debug.DrawLine(part.position, hit.point, Color.red, fallDetectionInterval);
                return true;
            }
        }
        
        return false;
    }
    
    // Проверяет, не слишком ли низко находится голова
    private bool IsHeadTooLow()
    {
        if (headTransform == null) return false;
        
        // Проверяем высоту головы относительно центра масс
        float headHeight = headTransform.position.y - centerOfMass.y;
        return headHeight < headHeightThreshold;
    }
    
    // Убираем штрафы, оставляем только визуальный эффект
    private void ApplyFallPenalty(bool bodyTouching, bool headLow)
    {
        // Метод оставлен для обратной совместимости, но штрафы не применяются
        
        // Отмечаем агента как упавшего
        hasFallen = true;
        
        // Для лучшей визуализации, меняем цвет агента на красный
        ApplyFallVisualEffect();
        
        string fallReason = bodyTouching ? "тело касается земли" : "голова слишком низко";
        Debug.Log($"Агент {gameObject.name} упал ({fallReason}), но штраф не применяется");
    }
    
    // Применяет визуальный эффект падения
    private void ApplyFallVisualEffect()
    {
        // Найти все рендереры в иерархии
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        foreach (var renderer in renderers)
        {
            // Сохраняем все материалы рендерера
            Material[] materials = renderer.materials;
            
            // Для каждого материала устанавливаем красный цвет
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].color = Color.red;
                
                // Если у материала есть эмиссия, тоже устанавливаем красный
                if (materials[i].HasProperty("_EmissionColor"))
                {
                    materials[i].SetColor("_EmissionColor", Color.red * 0.5f);
                    materials[i].EnableKeyword("_EMISSION");
                }
            }
            
            // Применяем изменённые материалы
            renderer.materials = materials;
        }
    }

    private void CalculateCenterOfMass()
    {
        if (joints.Count == 0) return;
        
        Vector3 sum = Vector3.zero;
        int validJoints = 0;
        
        foreach (var joint in joints)
        {
            if (joint != null)
            {
                sum += joint.transform.position;
                validJoints++;
            }
        }
        
        if (validJoints > 0)
        {
            centerOfMass = sum / validJoints;
        }
    }
    
    private void CalculateHeadPositionReward()
    {
        if (headTransform == null) return;
        
        float headHeight = headTransform.position.y - centerOfMass.y;
        
        // Даем награду только если голова достаточно высоко
        if (headHeight > headHeightThreshold)
        {
            // Увеличенная награда за высокое положение головы, т.к. штрафы убраны
            fitness += 2f * Time.fixedDeltaTime;
        }
        
        // Даем дополнительную награду, если голова очень высоко
        if (headHeight > headHeightThreshold * 2)
        {
            fitness += 1.5f * Time.fixedDeltaTime;
        }
        
        // Если голова чуть выше порога, тоже даем небольшую награду
        else if (headHeight > headHeightThreshold * 0.5f)
        {
            fitness += 0.5f * Time.fixedDeltaTime;
        }
    }
    
    private void CalculateAimTargetReward()
    {
        // Убираем проверку на падение, чтобы агенты могли получать награду за движение в любом случае
        
        // Calculate distance to aim target
        float currentDistance = Vector3.Distance(centerOfMass, absoluteAimTarget);
        float previousDistance = Vector3.Distance(previousCenterOfMass, absoluteAimTarget);
        
        // Reward for getting closer to the target
        float distanceDelta = previousDistance - currentDistance;
        if (distanceDelta > 0)
        {
            // Scale reward to max 100 based on aimTarget magnitude
            float maxDistance = aimTarget.magnitude;
            float rewardScale = 100f / maxDistance;
            fitness += distanceDelta * rewardScale;
        }
    }
    
    private void UpdateJointAngles()
    {
        foreach (var joint in joints)
        {
            if (joint == null) continue;
            
            if (jointInputs.TryGetValue(joint, out var input))
            {
                input.UpdateValues();
            }
        }
    }

    public float GetFitness()
    {
        return fitness;
    }
    
    // Clamp angle to joint limits or global limits
    private float ClampTargetAngle(HingeJoint joint, float angle)
    {
        // First, clamp to the global min/max range
        float clampedAngle = Mathf.Clamp(angle, minTargetAngle, maxTargetAngle);
        
        // If useJointLimitsForClamping is true and joint has limits enabled,
        // further restrict the value to the joint's own limits
        if (useJointLimitsForClamping && joint.useLimits)
        {
            JointLimits limits = joint.limits;
            clampedAngle = Mathf.Clamp(clampedAngle, limits.min, limits.max);
        }
        
        return clampedAngle;
    }
    
    public void SetNeuralNetworkOutputs(Dictionary<HingeJoint, float> targetPositions)
    {
        if (targetPositions == null) return;
        
        foreach (var kvp in targetPositions)
        {
            if (kvp.Key == null) continue;
            
            if (joints.Contains(kvp.Key))
            {
                // Clamp target position to valid range
                float clampedTargetPosition = ClampTargetAngle(kvp.Key, kvp.Value);
                
                // Check if the value changed significantly
                if (Mathf.Abs(clampedTargetPosition - kvp.Value) > 1f)
                {
                    Debug.LogWarning($"Target position clamped from {kvp.Value} to {clampedTargetPosition} for joint {kvp.Key.name}");
                }
                
                // Apply the neural network output to the joint's target position
                JointSpring spring = kvp.Key.spring;
                spring.targetPosition = clampedTargetPosition;
                kvp.Key.spring = spring;
                
                // Update the input component to show the target
                if (jointInputs.TryGetValue(kvp.Key, out var input))
                {
                    input.TargetAngle = clampedTargetPosition;
                }
            }
        }
    }
    
    // Сбрасывает состояние падения (вызывается при сбросе симуляции)
    public void ResetFallState()
    {
        hasFallen = false;
    }
    
    private void OnDrawGizmos()
    {
        // Draw current center of mass
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(centerOfMass, 0.1f);
        
        // Draw previous center of mass
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(previousCenterOfMass, 0.08f);
        
        // Draw aim target
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(absoluteAimTarget, 0.1f);
        
        // Рисуем лучи проверки земли в режиме редактора
        if (!Application.isPlaying && bodyParts.Count > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Оранжевый
            foreach (var part in bodyParts)
            {
                if (part != null)
                {
                    Gizmos.DrawLine(part.position, part.position + Vector3.down * groundCheckDistance);
                    Gizmos.DrawWireSphere(part.position + Vector3.down * groundCheckDistance, 0.05f);
                }
            }
            
            // Рисуем ноги
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f); // Зеленый
            foreach (var part in legParts)
            {
                if (part != null)
                {
                    Gizmos.DrawLine(part.position, part.position + Vector3.down * groundCheckDistance);
                    Gizmos.DrawWireSphere(part.position + Vector3.down * groundCheckDistance, 0.05f);
                }
            }
            
            // Рисуем линию высоты головы
            if (headTransform != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 headProjection = new Vector3(headTransform.position.x, centerOfMass.y, headTransform.position.z);
                Gizmos.DrawLine(headTransform.position, headProjection);
                Gizmos.DrawLine(headProjection, centerOfMass);
                
                // Минимальная высота головы
                Gizmos.color = Color.red;
                Vector3 minHeadPosition = new Vector3(headTransform.position.x, centerOfMass.y + headHeightThreshold, headTransform.position.z);
                Gizmos.DrawLine(minHeadPosition, headProjection);
            }
        }
    }
} 