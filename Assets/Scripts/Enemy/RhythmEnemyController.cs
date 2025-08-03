using UnityEngine;

/// <summary>
/// Simpler Rhythm-basierter Enemy Controller
/// 3 States: Inactive → Preparing → Attacking (im globalen Takt)
/// </summary>
public class RhythmEnemyController : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int rewardValue = 10;
    [SerializeField] private ResourceType rewardType = ResourceType.Experience;
    [SerializeField] private EnemyType enemyType = EnemyType.Basic;
    
    [Header("Attack Settings")]
    [SerializeField] private float attackRadius = 3f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private LayerMask playerLayer = -1;
    [SerializeField] private bool showAttackRadius = true;
    
    [Header("Visual")]
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private Color inactiveColor = Color.gray;
    [SerializeField] private Color preparingColor = Color.yellow;
    [SerializeField] private Color attackingColor = Color.red;
    
    [Header("Current State")]
    [SerializeField, ReadOnly] private EnemyRhythmState currentState = EnemyRhythmState.Inactive;
    [SerializeField, ReadOnly] private float currentHealth;
    [SerializeField, ReadOnly] private bool isAlive = true;
    [SerializeField, ReadOnly] private int currentFrame = 0;
    
    // Animation
    private Animator animator;
    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int FrameHash = Animator.StringToHash("Frame");
    
    // Attack detection
    private Collider attackCollider;
    private bool hasAttackedThisCycle = false;
    
    void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        
        if (enemyRenderer == null)
            enemyRenderer = GetComponent<Renderer>();
        
        // Setup attack collider
        SetupAttackCollider();
        
        // Subscribe to rhythm system
        RhythmManager.OnBeatTick += OnBeatTick;
        RhythmManager.OnStateChanged += OnGlobalStateChanged;
    }
    
    void OnDestroy()
    {
        RhythmManager.OnBeatTick -= OnBeatTick;
        RhythmManager.OnStateChanged -= OnGlobalStateChanged;
    }
    
    void SetupAttackCollider()
    {
        // Create attack range collider
        GameObject attackTrigger = new GameObject("AttackTrigger");
        attackTrigger.transform.SetParent(transform);
        attackTrigger.transform.localPosition = Vector3.zero;
        
        attackCollider = attackTrigger.AddComponent<SphereCollider>();
        attackCollider.isTrigger = true;
        attackCollider.radius = attackRadius;
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
            case EnemyRhythmState.Inactive:
                // Nothing to do
                break;
                
            case EnemyRhythmState.Preparing:
                // Visual preparation (pulsing, etc.)
                break;
                
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
        
        ChangeState(newState);
    }
    
    #endregion
    
    #region State Management
    
    void ChangeState(EnemyRhythmState newState)
    {
        if (currentState == newState) return;
        
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
                SetVisualState(inactiveColor);
                break;
                
            case EnemyRhythmState.Preparing:
                SetVisualState(preparingColor);
                break;
                
            case EnemyRhythmState.Attacking:
                SetVisualState(attackingColor);
                attackCollider.enabled = true;
                hasAttackedThisCycle = false;
                break;
        }
        
        // Update animator
        if (animator != null)
        {
            animator.SetInteger(StateHash, (int)newState);
        }
    }
    
    void SetVisualState(Color color)
    {
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = color;
        }
    }
    
    #endregion
    
    #region Combat
    
    void ExecuteAttack()
    {
        hasAttackedThisCycle = true;
        
        // Find players in attack range
        Collider[] hitTargets = Physics.OverlapSphere(transform.position, attackRadius, playerLayer);
        
        foreach (var target in hitTargets)
        {
            var playerHealth = target.GetComponent<IPlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
                
                // Trigger damage event
                GameEvents.TriggerDamageDealt(attackDamage, 1);
                
                // Visual feedback
                CreateAttackEffect(target.transform.position);
                
                break; // Only hit one target per attack
            }
        }
    }
    
    void CreateAttackEffect(Vector3 hitPosition)
    {
        // Simple attack effect - später durch Particle System ersetzen
        Debug.Log($"Enemy {name} attacked at {hitPosition} for {attackDamage} damage");
        
        // Trigger attack visual event
        GameEvents.TriggerAreaDamageDealt(transform.position, attackRadius, 1);
    }
    
    public void TakeDamage(float damage)
    {
        if (!isAlive) return;
        
        currentHealth -= damage;
        
        // Visual damage feedback
        StartCoroutine(DamageFlash());
        
        if (currentHealth <= 0)
        {
            Die();
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
        
        // Trigger death event
        GameEvents.TriggerEnemyDestroyed(this, rewardValue, rewardType);
        
        // Disable components
        if (attackCollider != null)
            attackCollider.enabled = false;
        
        // Death visual
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = Color.black;
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
    public EnemyType GetEnemyType() => enemyType;
    public int GetRewardValue() => rewardValue;
    public ResourceType GetRewardType() => rewardType;
    
    #endregion
    
    #region Gizmos
    
    void OnDrawGizmosSelected()
    {
        if (!showAttackRadius) return;
        
        // Attack radius
        Gizmos.color = currentState == EnemyRhythmState.Attacking ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
        
        // State indicator
        Vector3 offset = Vector3.up * 2f;
        Gizmos.color = GetStateColor();
        Gizmos.DrawCube(transform.position + offset, Vector3.one * 0.3f);
    }
    
    Color GetStateColor()
    {
        return currentState switch
        {
            EnemyRhythmState.Inactive => inactiveColor,
            EnemyRhythmState.Preparing => preparingColor,
            EnemyRhythmState.Attacking => attackingColor,
            _ => Color.white
        };
    }
    
    #endregion
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
    private RhythmEnemyController owner;
    
    public void SetOwner(RhythmEnemyController enemy)
    {
        owner = enemy;
    }
    
    void OnTriggerStay(Collider other)
    {
        // Continuous trigger während attack state
        if (owner != null && owner.GetCurrentState() == EnemyRhythmState.Attacking)
        {
            var playerHealth = other.GetComponent<IPlayerHealth>();
            if (playerHealth != null)
            {
                // Additional continuous damage könnte hier implementiert werden
            }
        }
    }
}

