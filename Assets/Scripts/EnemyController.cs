using UnityEngine;

/// <summary>
/// Simple Enemy Controller - 3 Frame Animation mit AOE Attack
/// Unity 6 LTS - Event-basiert, Performance optimiert
/// </summary>
[RequireComponent(typeof(Damageable))]
public class EnemyController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float idleFrameDuration = 2f;
    [SerializeField] private float windupFrameDuration = 1f;
    [SerializeField] private float attackFrameDuration = 0.5f;
    [SerializeField] private bool loopAnimation = true;
    
    [Header("Attack Settings")]
    [SerializeField] private float attackRadius = 2f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private LayerMask playerLayer = -1;
    
    [Header("Resources")]
    [SerializeField] private int resourceValue = 10;
    [SerializeField] private ResourceType resourceType = ResourceType.Energy;
    
    [Header("Visual")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color windupColor = Color.yellow;
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private GameObject attackEffect;
    
    private Damageable damageableComponent;
    private Renderer enemyRenderer;
    private EnemyState currentState = EnemyState.Idle;
    private float stateTimer;
    private bool hasAttacked = false;
    
    // Events
    public static event System.Action<EnemyController, float, int> OnEnemyAttacked; // enemy, damage, targetsHit
    public static event System.Action<EnemyController, int, ResourceType> OnEnemyDestroyed; // enemy, value, type
    
    void Awake()
    {
        damageableComponent = GetComponent<Damageable>();
        enemyRenderer = GetComponent<Renderer>();
        
        SetStateVisual(EnemyState.Idle);
        stateTimer = 0f;
    }
    
    void OnEnable()
    {
        Damageable.OnDestroyed += OnDamageableDestroyed;
    }
    
    void OnDisable()
    {
        Damageable.OnDestroyed -= OnDamageableDestroyed;
    }
    
    void Update()
    {
        if (!damageableComponent.IsAlive()) return;
        
        UpdateState();
    }
    
    void UpdateState()
    {
        stateTimer += Time.deltaTime;
        
        switch (currentState)
        {
            case EnemyState.Idle:
                if (stateTimer >= idleFrameDuration)
                {
                    ChangeState(EnemyState.Windup);
                }
                break;
                
            case EnemyState.Windup:
                if (stateTimer >= windupFrameDuration)
                {
                    ChangeState(EnemyState.Attack);
                }
                break;
                
            case EnemyState.Attack:
                // AOE Attack Frame
                if (!hasAttacked)
                {
                    PerformAttack();
                    hasAttacked = true;
                }
                
                if (stateTimer >= attackFrameDuration)
                {
                    if (loopAnimation)
                    {
                        ChangeState(EnemyState.Idle);
                    }
                    else
                    {
                        // Stop at attack frame if not looping
                        hasAttacked = false;
                    }
                }
                break;
        }
    }
    
    void ChangeState(EnemyState newState)
    {
        currentState = newState;
        stateTimer = 0f;
        
        if (newState != EnemyState.Attack)
        {
            hasAttacked = false;
        }
        
        SetStateVisual(newState);
    }
    
    void SetStateVisual(EnemyState state)
    {
        if (!enemyRenderer) return;
        
        Color targetColor = state switch
        {
            EnemyState.Idle => idleColor,
            EnemyState.Windup => windupColor,
            EnemyState.Attack => attackColor,
            _ => idleColor
        };
        
        enemyRenderer.material.color = targetColor;
    }
    
    void PerformAttack()
    {
        // AOE Damage
        Collider[] targets = Physics.OverlapSphere(transform.position, attackRadius, playerLayer);
        int targetsHit = 0;
        
        foreach (var target in targets)
        {
            var playerDamageable = target.GetComponent<IDamageable>();
            if (playerDamageable != null && playerDamageable.IsAlive())
            {
                playerDamageable.TakeDamage(attackDamage);
                targetsHit++;
            }
        }
        
        // Visual Effect
        if (attackEffect)
        {
            GameObject effect = Instantiate(attackEffect, transform.position, Quaternion.identity);
            
            // Auto-destroy effect after 2 seconds
            Destroy(effect, 2f);
        }
        
        // Event
        OnEnemyAttacked?.Invoke(this, attackDamage, targetsHit);
        
        Debug.Log($"Enemy {name} attacked! Targets hit: {targetsHit}, Damage: {attackDamage}");
    }
    
    void OnDamageableDestroyed(Damageable destroyed)
    {
        if (destroyed == damageableComponent)
        {
            // Give resource to player
            OnEnemyDestroyed?.Invoke(this, resourceValue, resourceType);
            
            Debug.Log($"Enemy destroyed! Gave {resourceValue} {resourceType} to player");
        }
    }
    
    // PUBLIC API
    public void SetAttackDamage(float damage) => attackDamage = damage;
    public void SetAttackRadius(float radius) => attackRadius = radius;
    public void SetResourceValue(int value) => resourceValue = value;
    public void SetLooping(bool loop) => loopAnimation = loop;
    
    public EnemyState GetCurrentState() => currentState;
    public float GetStateTimer() => stateTimer;
    public float GetAttackRadius() => attackRadius;
    public int GetResourceValue() => resourceValue;
    
    // Force state change (für externe Systeme)
    public void ForceState(EnemyState state) => ChangeState(state);
    
    void OnDrawGizmosSelected()
    {
        // Attack Radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
        
        // State visualization
        Gizmos.color = currentState switch
        {
            EnemyState.Idle => Color.white,
            EnemyState.Windup => Color.yellow,
            EnemyState.Attack => Color.red,
            _ => Color.gray
        };
        
        Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.2f);
    }
}

/// <summary>
/// Enemy Animation States
/// </summary>
public enum EnemyState
{
    Idle,
    Windup,
    Attack
}

/// <summary>
/// Resource Types für Player Economy
/// </summary>
public enum ResourceType
{
    Energy,
    Gold,
    Experience,
    Mana
}