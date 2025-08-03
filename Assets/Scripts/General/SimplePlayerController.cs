using UnityEngine;

/// <summary>
/// Simple One-Hit Player Controller - Energy-basiert
/// </summary>
public class SimplePlayerController : MonoBehaviour
{
    [Header("Energy System")]
    [SerializeField] private int startingEnergy = 50;
    [SerializeField] private int maxEnergy = 100;
    [SerializeField] private int minEnergy = 0;
    
    [Header("Respawn")]
    [SerializeField] private Vector3 respawnPosition = Vector3.zero;
    [SerializeField] private float respawnDelay = 1f;
    
    [Header("Energy Decay")]
    [SerializeField] private float idleDecayRate = 5f; // Energy per second when idle
    [SerializeField] private float idleThreshold = 2f; // Seconds before decay starts
    [SerializeField] private int killEnergyReward = 15; // Energy gained per kill
    
    [Header("Current State")]
    [SerializeField, ReadOnly] private int currentEnergy;
    [SerializeField, ReadOnly] private bool isAlive = true;
    [SerializeField, ReadOnly] private float timeSinceLastAction = 0f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    void Start()
    {
        currentEnergy = startingEnergy;
        respawnPosition = transform.position;
        
        // Subscribe to events
        GameEvents.OnEnemyDestroyed += OnEnemyDestroyed;
        GameEvents.OnDashStarted += OnDashStarted;
        
        LogDebug($"Player initialized with {currentEnergy} energy");
    }
    
    void Update()
    {
        if (!isAlive) return;
        
        UpdateEnergyDecay();
    }
    
    void UpdateEnergyDecay()
    {
        timeSinceLastAction += Time.deltaTime;
        
        // Start decay after idle threshold
        if (timeSinceLastAction >= idleThreshold)
        {
            float decayAmount = idleDecayRate * Time.deltaTime;
            LoseEnergy(Mathf.RoundToInt(decayAmount));
        }
    }
    
    void ResetActionTimer()
    {
        timeSinceLastAction = 0f;
    }
    
    void OnDestroy()
    {
        GameEvents.OnEnemyDestroyed -= OnEnemyDestroyed;
        GameEvents.OnDashStarted -= OnDashStarted;
    }
    
    #region Public API
    
    public void TakeHit()
    {
        if (!isAlive) return;
        
        isAlive = false;
        
        LogDebug("Player hit - respawning...");
        
        // Trigger player hit event
        GameEvents.TriggerPlayerHit();
        
        // Disable player temporarily
        SetPlayerActive(false);
        
        // Respawn after delay
        Invoke(nameof(Respawn), respawnDelay);
    }
    
    public void GainEnergy(int amount)
    {
        if (!isAlive) return;
        
        int oldEnergy = currentEnergy;
        currentEnergy = Mathf.Clamp(currentEnergy + amount, minEnergy, maxEnergy);
        
        if (currentEnergy != oldEnergy)
        {
            GameEvents.TriggerPlayerEnergyChanged(currentEnergy);
            LogDebug($"Energy: {oldEnergy} → {currentEnergy} (+{amount})");
        }
        
        ResetActionTimer(); // Reset decay on energy gain
    }
    
    public void LoseEnergy(int amount)
    {
        if (!isAlive || amount <= 0) return;
        
        int oldEnergy = currentEnergy;
        currentEnergy = Mathf.Clamp(currentEnergy - amount, minEnergy, maxEnergy);
        
        if (currentEnergy != oldEnergy)
        {
            GameEvents.TriggerPlayerEnergyChanged(currentEnergy);
            LogDebug($"Energy: {oldEnergy} → {currentEnergy} (-{amount})");
        }
        
        // Check for death by energy depletion
        if (currentEnergy <= minEnergy)
        {
            TakeHit(); // Energy depletion = death
        }
    }
    
    public int GetCurrentEnergy() => currentEnergy;
    
    public bool IsAlive() => isAlive;
    
    #endregion
    
    #region Respawn System
    
    void Respawn()
    {
        // Reset position
        transform.position = respawnPosition;
        
        // Reset state
        isAlive = true;
        timeSinceLastAction = 0f; // Reset action timer
        
        // Re-enable player
        SetPlayerActive(true);
        
        LogDebug("Player respawned");
    }
    
    void SetPlayerActive(bool active)
    {
        // Disable/enable components as needed
        var playerCollider = GetComponent<Collider>();
        if (playerCollider != null)
            playerCollider.enabled = active;
        
        var playerRenderer = GetComponent<Renderer>();
        if (playerRenderer != null)
            playerRenderer.enabled = active;
        
        // Note: No movement component since player only moves via dash
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnEnemyDestroyed(EnemyController enemy, int energyValue)
    {
        // Gain energy from kills + bonus reward
        int totalReward = energyValue + killEnergyReward;
        GainEnergy(totalReward);
        ResetActionTimer(); // Reset decay timer on kill
        
        LogDebug($"Kill reward: +{totalReward} energy (base: {energyValue}, bonus: {killEnergyReward})");
    }
    
    void OnDashStarted(Vector3 targetPos)
    {
        // Reset action timer when dashing (no energy cost, but resets decay)
        ResetActionTimer();
        LogDebug("Dash started - action timer reset");
    }
    
    #endregion
    
    #region Energy Management
    
    public float GetTimeSinceLastAction() => timeSinceLastAction;
    public float GetIdleDecayRate() => idleDecayRate;
    public void ForceResetActionTimer() => ResetActionTimer();
    
    public float GetEnergyPercentage()
    {
        return (float)currentEnergy / maxEnergy;
    }
    
    public bool HasEnoughEnergy(int required)
    {
        return currentEnergy >= required;
    }
    
    public bool TrySpendEnergy(int amount)
    {
        if (HasEnoughEnergy(amount))
        {
            LoseEnergy(amount);
            return true;
        }
        return false;
    }
    
    #endregion
    
    #region Utility
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Player] {message}");
        }
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Take Hit")]
    void DebugTakeHit() => TakeHit();
    
    [ContextMenu("Gain Energy +10")]
    void DebugGainEnergy() => GainEnergy(10);
    
    [ContextMenu("Lose Energy -10")]
    void DebugLoseEnergy() => LoseEnergy(10);
    
    [ContextMenu("Reset Action Timer")]
    void DebugResetTimer() => ResetActionTimer();
    
    #endregion
}