using UnityEngine;
using System.Collections;

/// <summary>
/// Enemy Controller - Rhythm-basiertes Enemy System
/// Verwendet EnemyDataSO für Configuration
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Enemy Configuration")]
    [SerializeField] private EnemyDataSO enemyData;
    
    [Header("Runtime Overrides")]
    [SerializeField] private bool startInDangerState = false; // Set by level progression
    
    [Header("Current State")]
    [SerializeField, ReadOnly] private EnemyRhythmState currentState = EnemyRhythmState.Inactive;
    [SerializeField, ReadOnly] private float currentHealth;
    [SerializeField, ReadOnly] private bool isAlive = true;
    [SerializeField, ReadOnly] private int currentFrame = 0;
    [SerializeField, ReadOnly] private int attackCycleCounter = 0; // Für large enemy
    [SerializeField, ReadOnly] private bool isDangerState = false; // Current danger status
    
    [Header("Components")]
    [SerializeField] private LayerMask playerLayer = -1;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Runtime variables
    private float maxHealth;
    private int energyValue;
    private float attackRadius;
    private EnemySize enemySize;
    
    // Components
    private Renderer enemyRenderer;
    private Collider attackCollider;
    private Animator animator;
    private bool hasAttackedThisCycle = false;
    
    // Animation hashes
    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int FrameHash = Animator.StringToHash("Frame");
    
    void Awake()
    {
        InitializeFromData();
        SetupComponents();
        
        // Subscribe to global rhythm system
        GlobalRhythmManager.OnBeatTick += OnBeatTick;
        GlobalRhythmManager.OnStateChanged += OnGlobalStateChanged;
    }
    
    void OnDestroy()
    {
        GlobalRhythmManager.OnBeatTick -= OnBeatTick;
        GlobalRhythmManager.OnStateChanged -= OnGlobalStateChanged;
    }
    
    void InitializeFromData()
    {
        if (enemyData == null)
        {
            Debug.LogError($"EnemyData missing on {name}! Using defaults.");
            CreateDefaultData();
            return;
        }
        
        // Copy stats from ScriptableObject
        maxHealth = enemyData.maxHealth;
        energyValue = enemyData.energyValue;
        attackRadius = enemyData.attackRadius;
        enemySize = enemyData.enemySize;
        
        // Set initial values
        currentHealth = maxHealth;
        
        // Apply visual settings
        transform.localScale = enemyData.scale;
        
        // Setup danger state for Large enemies
        if (enemySize == EnemySize.Large)
        {
            isDangerState = startInDangerState;
            attackCycleCounter = 0;
        }
    }
    
    void CreateDefaultData()
    {
        // Fallback values
        maxHealth = 100f;
        energyValue = 10;
        attackRadius = 3f;
        enemySize = EnemySize.Medium;
        currentHealth = maxHealth;
    }
    
    void SetupComponents()
    {
        enemyRenderer = GetComponent<Renderer>();
        animator = GetComponent<Animator>();
        
        // Setup attack collider
        SetupAttackCollider();
    }
    
    void SetupAttackCollider()
    {
        // Create attack range collider
        GameObject attackTrigger = new GameObject("AttackTrigger");
        attackTrigger.transform.SetParent(transform);
        attackTrigger.transform.localPosition = Vector3.zero;
        
        attackCollider = attackTrigger.AddComponent<SphereCollider>();
        attackCollider.isTrigger = true;
        ((SphereCollider)attackCollider).radius = attackRadius;
        attackCollider.enabled = false; // Only active during attack
        
        // Add trigger component
        var trigger = attackTrigger.AddComponent<EnemyAttackTrigger>();
        trigger.SetOwner(this);
    }
    
    #region Rhythm System Events
    
    void OnBeatTick(int frame)
    {
        if (!isAlive) return;
        
        currentFrame = frame;
        
        // Update animation frame
        if (animator != null)
        {
            animator.SetInteger(FrameHash, frame);
        }
        
        // Handle state-specific frame logic
        switch (currentState)
        {
            case EnemyRhythmState.Attacking:
                // Execute attack on specific frame
                if (frame == 1 && !hasAttackedThisCycle) // Attack on frame 1 of attack state
                {
                    ExecuteAttack();
                }
                break;
        }
    }
    
    void OnGlobalStateChanged(EnemyRhythmState newState)
    {
        if (!isAlive) return;
        
        // Handle large enemy special logic
        if (enemySize == EnemySize.Large)
        {
            HandleLargeEnemyState(newState);
        }
        else
        {
            // Normal enemies follow global state
            ChangeState(newState);
        }
    }
    
    void HandleLargeEnemyState(EnemyRhythmState globalState)
    {
        if (isDangerState)
        {
            // Danger state: attack every cycle
            ChangeState(globalState);
        }
        else
        {
            // Normal state: attack every second cycle
            if (globalState == EnemyRhythmState.Attacking)
            {
                attackCycleCounter++;
                
                if (attackCycleCounter % 2 == 1)
                {
                    // Odd cycle: stay idle instead of attacking
                    ChangeState(EnemyRhythmState.Inactive, useIdleColor: true);
                }
                else
                {
                    // Even cycle: attack normally
                    ChangeState(EnemyRhythmState.Attacking);
                }
            }
            else
            {
                // Follow global state for non-attack states
                ChangeState(globalState);
            }
        }
    }
    
    #endregion
    
    #region State Management
    
    void ChangeState(EnemyRhythmState newState, bool useIdleColor = false)
    {
        if (currentState == newState && !useIdleColor) return;
        
        // Exit current state
        switch (currentState)
        {
            case EnemyRhythmState.Attacking:
                attackCollider.enabled = false;
                hasAttackedThisCycle = false;
                break;
        }
        
        currentState = newState;
        
        // Enter new state
        switch (newState)
        {
            case EnemyRhythmState.Inactive:
                SetVisualState(useIdleColor ? GetIdleColor() : GetInactiveColor());
                break;
                
            case EnemyRhythmState.Preparing:
                SetVisualState(GetPreparingColor());
                break;
                
            case EnemyRhythmState.Attacking:
                SetVisualState(GetAttackingColor());
                attackCollider.enabled = true;
                hasAttackedThisCycle = false;
                break;
        }
        
        // Update animator
        if (animator != null)
        {
            animator.SetInteger(StateHash, (int)newState);
            animator.SetBool("IsIdle", useIdleColor); // Extra parameter für idle animation
        }
    }
    
    void SetVisualState(Color color)
    {
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = color;
        }
    }
    
    // Color getters (use ScriptableObject data or defaults)
    Color GetInactiveColor() => enemyData ? enemyData.inactiveColor : Color.gray;
    Color GetPreparingColor() => enemyData ? enemyData.preparingColor : Color.yellow;
    Color GetAttackingColor() => enemyData ? enemyData.attackingColor : Color.red;
    Color GetIdleColor() => enemyData ? enemyData.idleColor : Color.blue;
    
    #endregion
    
    #region Combat
    
    void ExecuteAttack()
    {
        hasAttackedThisCycle = true;
        
        // Find players in attack range
        Collider[] hitTargets = Physics.OverlapSphere(transform.position, attackRadius, playerLayer);
        
        foreach (var target in hitTargets)
        {
            var playerController = target.GetComponent<SimplePlayerController>();
            if (playerController != null && playerController.IsAlive())
            {
                // One-hit kill player
                playerController.TakeHit();
                
                // Visual feedback
                CreateAttackEffect(target.transform.position);
                
                break; // Only hit one target per attack
            }
        }
        
        // Play attack sound
        if (enemyData && enemyData.attackSound)
        {
            AudioSource.PlayClipAtPoint(enemyData.attackSound, transform.position);
        }
    }
    
    void CreateAttackEffect(Vector3 hitPosition)
    {
        LogDebug($"Enemy {name} attacked at {hitPosition}");
        
        // Trigger attack visual event
        GameEvents.TriggerAreaDamageDealt(transform.position, attackRadius, 1);
    }
    
    public void TakeDamage(float damage)
    {
        if (!isAlive) return;
        
        currentHealth -= damage;
        
        // Check for danger state transition (only for Large enemies)
        if (enemySize == EnemySize.Large && !isDangerState)
        {
            float healthPercent = currentHealth / maxHealth;
            float threshold = enemyData ? enemyData.dangerHealthThreshold : 0.3f;
            
            if (healthPercent <= threshold)
            {
                EnterDangerState();
            }
        }
        
        // Visual damage feedback
        StartCoroutine(DamageFlash());
        
        // Play hit sound
        if (enemyData && enemyData.hitSound)
        {
            AudioSource.PlayClipAtPoint(enemyData.hitSound, transform.position);
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    void EnterDangerState()
    {
        isDangerState = true;
        attackCycleCounter = 0; // Reset cycle counter
        
        // Visual feedback für danger state
        if (enemyRenderer != null)
        {
            StartCoroutine(DangerStateFlash());
        }
        
        // Spawn danger effect
        if (enemyData && enemyData.dangerEffect)
        {
            Instantiate(enemyData.dangerEffect, transform.position, Quaternion.identity);
        }
        
        LogDebug("Large enemy entered DANGER STATE!");
    }
    
    System.Collections.IEnumerator DangerStateFlash()
    {
        // Danger state activation visual
        for (int i = 0; i < 3; i++)
        {
            if (enemyRenderer != null)
            {
                enemyRenderer.material.color = Color.red;
                yield return new WaitForSeconds(0.1f);
                enemyRenderer.material.color = Color.white;
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
    
    System.Collections.IEnumerator DamageFlash()
    {
        if (enemyRenderer != null)
        {
            Color originalColor = enemyRenderer.material.color;
            enemyRenderer.material.color = Color.white;
            
            yield return new WaitForSeconds(0.1f);
            
            enemyRenderer.material.color = originalColor;
        }
    }
    
    void Die()
    {
        if (!isAlive) return;
        
        isAlive = false;
        
        // Trigger death event - simplified signature
        GameEvents.TriggerEnemyDestroyed(this, energyValue);
        
        // Disable components
        if (attackCollider != null)
            attackCollider.enabled = false;
        
        // Death visual
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = Color.black;
        }
        
        // Spawn death effect
        if (enemyData && enemyData.deathEffect)
        {
            Instantiate(enemyData.deathEffect, transform.position, Quaternion.identity);
        }
        
        // Play death sound
        if (enemyData && enemyData.deathSound)
        {
            AudioSource.PlayClipAtPoint(enemyData.deathSound, transform.position);
        }
        
        // Remove after short delay
        Destroy(gameObject, 0.5f);
    }
    
    #endregion
    
    #region Public API
    
    public EnemyRhythmState GetCurrentState() => currentState;
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public bool IsAlive() => isAlive;
    public EnemySize GetEnemySize() => enemySize;
    public int GetEnergyValue() => energyValue;
    public bool GetIsDangerState() => isDangerState;
    public int GetAttackCycleCounter() => attackCycleCounter;
    
    public void SetDangerState(bool danger)
    {
        isDangerState = danger;
        attackCycleCounter = 0; // Reset counter when changing danger state
        
        if (danger)
        {
            LogDebug("Enemy forced into DANGER STATE!");
        }
    }
    
    public void SetStartInDangerState(bool startDanger)
    {
        startInDangerState = startDanger;
        if (enemySize == EnemySize.Large)
        {
            isDangerState = startDanger;
        }
    }
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{name}] {message}");
        }
    }
    
    #endregion
    
    #region Gizmos
    
    void OnDrawGizmosSelected()
    {
        // Attack radius
        float radius = enemyData ? enemyData.attackRadius : attackRadius;
        Gizmos.color = currentState == EnemyRhythmState.Attacking ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
        
        // State indicator
        Vector3 offset = Vector3.up * 2f;
        Gizmos.color = GetStateColor();
        Gizmos.DrawCube(transform.position + offset, Vector3.one * 0.3f);
    }
    
    Color GetStateColor()
    {
        return currentState switch
        {
            EnemyRhythmState.Inactive => isDangerState ? GetIdleColor() : GetInactiveColor(),
            EnemyRhythmState.Preparing => GetPreparingColor(),
            EnemyRhythmState.Attacking => GetAttackingColor(),
            _ => Color.white
        };
    }
    
    #endregion
}

/// <summary>
/// Enemy Sizes mit unterschiedlichen Eigenschaften
/// </summary>
public enum EnemySize
{
    Small,   // Klein, schnell, wenig Health/Energy
    Medium,  // Standard
    Large    // Groß, langsame Attacken (2 Zyklen), viel Health/Energy
}

/// <summary>
/// Enemy Rhythm States - Synchron mit globalem System
/// </summary>
public enum EnemyRhythmState
{
    Inactive = 0,   // Frame 0
    Preparing = 1,  // Frame 1  
    Attacking = 2   // Frame 2
}

/// <summary>
/// Attack Trigger Component für AOE Detection
/// </summary>
public class EnemyAttackTrigger : MonoBehaviour
{
    private EnemyController owner;
    
    public void SetOwner(EnemyController enemy)
    {
        owner = enemy;
    }
    
    void OnTriggerStay(Collider other)
    {
        // Continuous trigger während attack state
        if (owner != null && owner.GetCurrentState() == EnemyRhythmState.Attacking)
        {
            var playerController = other.GetComponent<SimplePlayerController>();
            if (playerController != null)
            {
                // Additional continuous damage könnte hier implementiert werden
            }
        }
    }
}

/// <summary>
/// Simplified Resource Type
/// </summary>
public enum ResourceType
{
    Energy
}