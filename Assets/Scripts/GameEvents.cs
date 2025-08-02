using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Production-Ready Event System - Unity 6 LTS
/// Langzeit-stabil, Performance-optimiert, Memory-safe
/// </summary>
public static class GameEvents
{
    // Circle System Events
    public static event Action<Vector3, float, Vector3> OnCircleConfirmed;
    public static event Action<Vector3, float, float> OnCircleConfirmedWithQuality;
    public static event Action<Vector3[]> OnPathUpdated;
    public static event Action OnDrawingCancelled;
    
    // Target System Events  
    public static event Action<CircleTarget, Vector3, float> OnTargetSelected;
    public static event Action<Vector3, float, int> OnAreaDamageDealt;
    
    // Player Events
    public static event Action<Vector3> OnDashStarted;
    public static event Action<Vector3> OnDashCompleted;
    public static event Action<int, float> OnCircleStatsUpdated;
    public static event Action<float, int> OnDamageDealt;
    public static event Action<int, int, int> OnExperienceGained;
    public static event Action<int> OnPlayerLevelUp;
    
    // Enemy Events
    public static event Action<EnemyController, float, int> OnEnemyAttacked;
    public static event Action<EnemyController, int, ResourceType> OnEnemyDestroyed;
    
    // Visual Events
    public static event Action<float> OnRadiusChanged;
    public static event Action<int> OnSegmentsChanged;
    public static event Action<Vector3[]> OnPointsUpdated;
    
    #region Trigger Methods - Type-Safe
    
    // Circle Events
    public static void TriggerCircleConfirmed(Vector3 center, float radius, Vector3 normal)
        => OnCircleConfirmed?.Invoke(center, radius, normal);
    
    public static void TriggerCircleConfirmedWithQuality(Vector3 center, float radius, float quality)
        => OnCircleConfirmedWithQuality?.Invoke(center, radius, quality);
    
    public static void TriggerPathUpdated(Vector3[] points)
        => OnPathUpdated?.Invoke(points);
    
    public static void TriggerDrawingCancelled()
        => OnDrawingCancelled?.Invoke();
    
    // Target Events
    public static void TriggerTargetSelected(CircleTarget target, Vector3 center, float speedBonus)
        => OnTargetSelected?.Invoke(target, center, speedBonus);
    
    public static void TriggerAreaDamageDealt(Vector3 position, float radius, int targetCount)
        => OnAreaDamageDealt?.Invoke(position, radius, targetCount);
    
    // Player Events
    public static void TriggerDashStarted(Vector3 targetPos)
        => OnDashStarted?.Invoke(targetPos);
    
    public static void TriggerDashCompleted(Vector3 targetPos)
        => OnDashCompleted?.Invoke(targetPos);
    
    public static void TriggerCircleStatsUpdated(int count, float avgQuality)
        => OnCircleStatsUpdated?.Invoke(count, avgQuality);
    
    public static void TriggerDamageDealt(float damage, int targets)
        => OnDamageDealt?.Invoke(damage, targets);
    
    public static void TriggerExperienceGained(int gained, int current, int toNext)
        => OnExperienceGained?.Invoke(gained, current, toNext);
    
    public static void TriggerPlayerLevelUp(int newLevel)
        => OnPlayerLevelUp?.Invoke(newLevel);
    
    // Enemy Events
    public static void TriggerEnemyAttacked(EnemyController enemy, float damage, int targetsHit)
        => OnEnemyAttacked?.Invoke(enemy, damage, targetsHit);
    
    public static void TriggerEnemyDestroyed(EnemyController enemy, int value, ResourceType type)
        => OnEnemyDestroyed?.Invoke(enemy, value, type);
    
    // Visual Events
    public static void TriggerRadiusChanged(float radius)
        => OnRadiusChanged?.Invoke(radius);
    
    public static void TriggerSegmentsChanged(int segments)
        => OnSegmentsChanged?.Invoke(segments);
    
    public static void TriggerPointsUpdated(Vector3[] points)
        => OnPointsUpdated?.Invoke(points);
    
    #endregion
    
    #region Memory Management - Langzeit-Safe
    
    /// <summary>
    /// Cleanup für Scene-Wechsel - Verhindert Memory Leaks
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        // Auto-cleanup bei Scene Load
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => {
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
            {
                ClearAllEvents();
            }
        };
    }
    
    /// <summary>
    /// Cleant alle Events - Memory-safe Scene-Wechsel
    /// </summary>
    public static void ClearAllEvents()
    {
        OnCircleConfirmed = null;
        OnCircleConfirmedWithQuality = null;
        OnPathUpdated = null;
        OnDrawingCancelled = null;
        OnTargetSelected = null;
        OnAreaDamageDealt = null;
        OnDashStarted = null;
        OnDashCompleted = null;
        OnCircleStatsUpdated = null;
        OnDamageDealt = null;
        OnExperienceGained = null;
        OnPlayerLevelUp = null;
        OnEnemyAttacked = null;
        OnEnemyDestroyed = null;
        OnRadiusChanged = null;
        OnSegmentsChanged = null;
        OnPointsUpdated = null;
        
        Debug.Log("GameEvents: All events cleared");
    }
    
    /// <summary>
    /// Event-Statistiken für Debugging
    /// </summary>
    public static void PrintEventStats()
    {
        var stats = new Dictionary<string, int>
        {
            ["CircleConfirmed"] = OnCircleConfirmed?.GetInvocationList().Length ?? 0,
            ["CircleWithQuality"] = OnCircleConfirmedWithQuality?.GetInvocationList().Length ?? 0,
            ["PathUpdated"] = OnPathUpdated?.GetInvocationList().Length ?? 0,
            ["TargetSelected"] = OnTargetSelected?.GetInvocationList().Length ?? 0,
            ["DashStarted"] = OnDashStarted?.GetInvocationList().Length ?? 0,
            ["PlayerLevelUp"] = OnPlayerLevelUp?.GetInvocationList().Length ?? 0
        };
        
        foreach (var stat in stats)
        {
            if (stat.Value > 0)
                Debug.Log($"Event {stat.Key}: {stat.Value} subscribers");
        }
    }
    
    #endregion
}

/// <summary>
/// Event Manager Component - Optional für Inspector-Control
/// </summary>
public class GameEventManager : MonoBehaviour
{
    [Header("Event Management")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private bool clearOnDestroy = true;
    [SerializeField] private bool debugMode = false;
    
    void Start()
    {
        if (autoInitialize)
        {
            GameEvents.Initialize();
        }
        
        if (debugMode)
        {
            InvokeRepeating(nameof(LogEventStats), 5f, 5f);
        }
    }
    
    void OnDestroy()
    {
        if (clearOnDestroy)
        {
            GameEvents.ClearAllEvents();
        }
    }
    
    void LogEventStats()
    {
        GameEvents.PrintEventStats();
    }
    
    [ContextMenu("Clear All Events")]
    public void ClearEvents() => GameEvents.ClearAllEvents();
    
    [ContextMenu("Print Event Stats")]
    public void PrintStats() => GameEvents.PrintEventStats();
}

/// <summary>
/// Beispiel Usage - Ersetzt alle bestehenden Event-Calls
/// </summary>
public class ExampleUsage : MonoBehaviour
{
    void Start()
    {
        // Subscribe
        GameEvents.OnCircleConfirmed += HandleCircleConfirmed;
        GameEvents.OnPlayerLevelUp += HandleLevelUp;
    }
    
    void OnDestroy()
    {
        // Unsubscribe (wichtig für Memory-Safety)
        GameEvents.OnCircleConfirmed -= HandleCircleConfirmed;
        GameEvents.OnPlayerLevelUp -= HandleLevelUp;
    }
    
    void HandleCircleConfirmed(Vector3 center, float radius, Vector3 normal)
    {
        Debug.Log($"Circle at {center} with radius {radius}");
    }
    
    void HandleLevelUp(int newLevel)
    {
        Debug.Log($"Player reached level {newLevel}!");
    }
    
    void TestTriggerEvents()
    {
        // Trigger Events
        GameEvents.TriggerCircleConfirmed(Vector3.zero, 5f, Vector3.up);
        GameEvents.TriggerPlayerLevelUp(2);
    }
}