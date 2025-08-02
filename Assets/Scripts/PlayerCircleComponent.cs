using UnityEngine;

/// <summary>
/// Optimized Player Circle Component - Clean Integration
/// Unity 6 LTS - Focused on player stats and progression
/// </summary>
[RequireComponent(typeof(PlayerDash))]
public class OptimizedPlayerCircleComponent : MonoBehaviour
{
    [Header("Player Circle Stats")]
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float radiusMultiplier = 1f;
    [SerializeField] private float speedBonus = 1f;
    [SerializeField] private int circleLevel = 1;
    
    [Header("Circle Combat")]
    [SerializeField] private bool enableAreaDamage = true;
    [SerializeField] private bool enableVisualEffect = true;
    [SerializeField] private float baseDamage = 25f;
    
    [Header("Progression")]
    [SerializeField] private int experiencePerTarget = 10;
    [SerializeField] private float qualityBonusXP = 5f;
    [SerializeField] private int currentExperience = 0;
    [SerializeField] private int experienceToNextLevel = 100;
    
    // References
    private PlayerDash playerDash;
    private CircleManager circleManager;
    
    // Cached Data
    private float lastCircleQuality = 1f;
    private int circlesPerformed = 0;
    private int totalTargetsHit = 0;
    private float totalDamageDealt = 0f;
    
    // Events for UI/Stats
    public static event System.Action<int, float> OnCircleStatsUpdated; // count, avgQuality
    public static event System.Action<float, int> OnDamageDealt; // damage, targets
    public static event System.Action<int, int, int> OnExperienceGained; // gained, current, toNext
    public static event System.Action<int> OnPlayerLevelUp; // newLevel
    
    void Awake()
    {
        InitializeComponents();
    }
    
    void InitializeComponents()
    {
        playerDash = GetComponent<PlayerDash>();
        circleManager = FindObjectOfType<CircleManager>();
        
        if (!circleManager)
        {
            Debug.LogWarning("PlayerCircleComponent: No CircleManager found!");
        }
    }
    
    void OnEnable()
    {
        SubscribeToEvents();
    }
    
    void OnDisable()
    {
        UnsubscribeFromEvents();
    }
    
    void SubscribeToEvents()
    {
        // Circle System Events
        CircleManager.OnCircleConfirmedWithQuality += OnCircleConfirmed;
        CircleManager.OnAreaDamageDealt += OnAreaDamageDealt;
        
        // Player Events
        PlayerDash.OnDashStarted += OnDashStarted;
        PlayerDash.OnDashCompleted += OnDashCompleted;
    }
    
    void UnsubscribeFromEvents()
    {
        CircleManager.OnCircleConfirmedWithQuality -= OnCircleConfirmed;
        CircleManager.OnAreaDamageDealt -= OnAreaDamageDealt;
        PlayerDash.OnDashStarted -= OnDashStarted;
        PlayerDash.OnDashCompleted -= OnDashCompleted;
    }
    
    #region Event Handlers
    
    void OnCircleConfirmed(Vector3 center, float radius, float quality)
    {
        circlesPerformed++;
        lastCircleQuality = quality;
        
        // Update stats event
        OnCircleStatsUpdated?.Invoke(circlesPerformed, quality);
        
        Debug.Log($"Circle #{circlesPerformed} - Quality: {GetQualityText(quality)} ({quality:F2})");
    }
    
    void OnDashStarted(Vector3 targetPos)
    {
        // Player feedback for dash start
        // Could trigger effects, sounds, etc.
    }
    
    void OnDashCompleted(Vector3 targetPos)
    {
        // Area effects are handled by external systems
        // Player-specific post-dash logic here
    }
    
    void OnAreaDamageDealt(Vector3 position, float radius, int targetCount)
    {
        if (!enableAreaDamage) return;
        
        float finalDamage = CalculateFinalDamage();
        float totalDamage = finalDamage * targetCount;
        
        // Update stats
        totalTargetsHit += targetCount;
        totalDamageDealt += totalDamage;
        
        // Gain experience
        GainExperience(targetCount, lastCircleQuality);
        
        // Fire events
        OnDamageDealt?.Invoke(totalDamage, targetCount);
        
        Debug.Log($"Dealt {totalDamage:F0} damage to {targetCount} targets");
    }
    
    #endregion
    
    #region Calculations
    
    float CalculateFinalDamage()
    {
        float damage = baseDamage * damageMultiplier;
        
        // Quality bonus (0-50% extra)
        damage *= (1f + lastCircleQuality * 0.5f);
        
        // Speed bonus
        damage *= speedBonus;
        
        // Level scaling (20% per level)
        damage *= (1f + (circleLevel - 1) * 0.2f);
        
        return damage;
    }
    
    void GainExperience(int targetsHit, float quality)
    {
        int baseXP = targetsHit * experiencePerTarget;
        float qualityBonus = quality * qualityBonusXP;
        int totalXP = Mathf.RoundToInt(baseXP + qualityBonus);
        
        currentExperience += totalXP;
        
        // Check for level up
        CheckLevelUp();
        
        // Fire event
        OnExperienceGained?.Invoke(totalXP, currentExperience, experienceToNextLevel);
        
        Debug.Log($"Gained {totalXP} XP (Targets: {targetsHit}, Quality Bonus: {qualityBonus:F0})");
    }
    
    void CheckLevelUp()
    {
        while (currentExperience >= experienceToNextLevel)
        {
            currentExperience -= experienceToNextLevel;
            circleLevel++;
            
            // Increase XP requirement for next level
            experienceToNextLevel = Mathf.RoundToInt(experienceToNextLevel * 1.5f);
            
            // Auto-upgrades on level up
            ApplyLevelUpBonuses();
            
            // Fire event
            OnPlayerLevelUp?.Invoke(circleLevel);
            
            Debug.Log($"LEVEL UP! Now level {circleLevel}");
        }
    }
    
    void ApplyLevelUpBonuses()
    {
        // Automatic stat increases per level
        damageMultiplier += 0.1f;   // +10% damage
        radiusMultiplier += 0.05f;  // +5% radius
        speedBonus += 0.05f;        // +5% speed
    }
    
    string GetQualityText(float quality)
    {
        if (quality >= 0.85f) return "EXCELLENT";
        if (quality >= 0.7f) return "GOOD";
        if (quality >= 0.4f) return "MEDIUM";
        return "POOR";
    }
    
    #endregion
    
    #region Upgrade System
    
    public void UpgradeDamage(float amount)
    {
        damageMultiplier += amount;
        Debug.Log($"Damage upgraded! New multiplier: {damageMultiplier:F2}x");
    }
    
    public void UpgradeRadius(float amount)
    {
        radiusMultiplier += amount;
        Debug.Log($"Radius upgraded! New multiplier: {radiusMultiplier:F2}x");
    }
    
    public void UpgradeSpeed(float amount)
    {
        speedBonus += amount;
        Debug.Log($"Speed upgraded! New bonus: {speedBonus:F2}x");
    }
    
    public void SetLevel(int level)
    {
        if (level < 1) return;
        
        circleLevel = level;
        
        // Recalculate XP requirement
        experienceToNextLevel = 100;
        for (int i = 1; i < level; i++)
        {
            experienceToNextLevel = Mathf.RoundToInt(experienceToNextLevel * 1.5f);
        }
        
        Debug.Log($"Level set to {circleLevel}");
    }
    
    public void AddExperience(int amount)
    {
        if (amount <= 0) return;
        
        currentExperience += amount;
        CheckLevelUp();
        
        OnExperienceGained?.Invoke(amount, currentExperience, experienceToNextLevel);
    }
    
    #endregion
    
    #region Public API - Getters
    
    public float GetDamageMultiplier() => damageMultiplier;
    public float GetRadiusMultiplier() => radiusMultiplier;
    public float GetSpeedBonus() => speedBonus;
    public int GetCircleLevel() => circleLevel;
    public int GetCirclesPerformed() => circlesPerformed;
    public float GetLastCircleQuality() => lastCircleQuality;
    public float GetCurrentDamage() => CalculateFinalDamage();
    public int GetTotalTargetsHit() => totalTargetsHit;
    public float GetTotalDamageDealt() => totalDamageDealt;
    
    // Experience System
    public int GetCurrentExperience() => currentExperience;
    public int GetExperienceToNextLevel() => experienceToNextLevel;
    public float GetLevelProgress() => (float)currentExperience / experienceToNextLevel;
    
    // Combat Settings
    public bool IsAreaDamageEnabled() => enableAreaDamage;
    public bool IsVisualEffectEnabled() => enableVisualEffect;
    public float GetBaseDamage() => baseDamage;
    
    #endregion
    
    #region Public API - Setters
    
    public void SetAreaDamageEnabled(bool enabled) => enableAreaDamage = enabled;
    public void SetVisualEffectEnabled(bool enabled) => enableVisualEffect = enabled;
    public void SetBaseDamage(float damage) => baseDamage = Mathf.Max(1f, damage);
    public void SetExperiencePerTarget(int xp) => experiencePerTarget = Mathf.Max(1, xp);
    public void SetQualityBonusXP(float bonus) => qualityBonusXP = Mathf.Max(0f, bonus);
    
    #endregion
    
    #region Statistics
    
    public PlayerCircleStats GetStats()
    {
        return new PlayerCircleStats
        {
            level = circleLevel,
            circlesPerformed = circlesPerformed,
            totalTargetsHit = totalTargetsHit,
            totalDamageDealt = totalDamageDealt,
            currentExperience = currentExperience,
            experienceToNextLevel = experienceToNextLevel,
            damageMultiplier = damageMultiplier,
            radiusMultiplier = radiusMultiplier,
            speedBonus = speedBonus,
            lastCircleQuality = lastCircleQuality
        };
    }
    
    public void ResetStats()
    {
        circlesPerformed = 0;
        totalTargetsHit = 0;
        totalDamageDealt = 0f;
        lastCircleQuality = 1f;
        
        Debug.Log("Player circle stats reset");
    }
    
    #endregion
    
    #region Debug Tools
    
    [ContextMenu("Debug Player Stats")]
    void DebugStats()
    {
        var stats = GetStats();
        Debug.Log($"Player Circle Stats:\n" +
                 $"Level: {stats.level} | XP: {stats.currentExperience}/{stats.experienceToNextLevel}\n" +
                 $"Circles: {stats.circlesPerformed} | Targets Hit: {stats.totalTargetsHit}\n" +
                 $"Total Damage: {stats.totalDamageDealt:F0} | Current DPS: {GetCurrentDamage():F1}\n" +
                 $"Multipliers: Dmg {stats.damageMultiplier:F2}x | Radius {stats.radiusMultiplier:F2}x | Speed {stats.speedBonus:F2}x");
    }
    
    [ContextMenu("Level Up")]
    void DebugLevelUp()
    {
        AddExperience(experienceToNextLevel);
    }
    
    [ContextMenu("Reset All Stats")]
    void DebugResetAll()
    {
        ResetStats();
        circleLevel = 1;
        currentExperience = 0;
        experienceToNextLevel = 100;
        damageMultiplier = 1f;
        radiusMultiplier = 1f;
        speedBonus = 1f;
    }
    
    #endregion
}

/// <summary>
/// Data structure for player statistics
/// </summary>
[System.Serializable]
public struct PlayerCircleStats
{
    public int level;
    public int circlesPerformed;
    public int totalTargetsHit;
    public float totalDamageDealt;
    public int currentExperience;
    public int experienceToNextLevel;
    public float damageMultiplier;
    public float radiusMultiplier;
    public float speedBonus;
    public float lastCircleQuality;
}