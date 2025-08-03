using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Vereinfachter Planet-Umlauf Manager - 8 Bereiche Loop
/// Simplified für Rhythm-basierte Enemies
/// </summary>
public class SphereLevelManager : MonoBehaviour
{
    [Header("Planet Configuration")]
    [SerializeField] private PlanetAreaData[] areaData = new PlanetAreaData[8];
    [SerializeField] private Transform enemyParent;
    [SerializeField] private float rotationTransitionTime = 0.8f;
    [SerializeField] private float areaSetupTime = 0.3f;
    
    [Header("Current State")]
    [SerializeField, ReadOnly] private int currentAreaIndex = 0;
    [SerializeField, ReadOnly] private AreaState currentState = AreaState.Inactive;
    [SerializeField, ReadOnly] private int totalRotations = 0;
    [SerializeField, ReadOnly] private int enemiesRemaining = 0;
    
    [Header("Progression Settings")]
    [SerializeField] private bool enableProgressionScaling = true;
    [SerializeField] private int rotationsForDangerEnemies = 3; // Nach X Rotationen = Danger Enemies
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showAreaGizmos = true;
    
    // Components
    private SphereLevelController sphereController;
    private Coroutine areaTransitionCoroutine;
    
    // Simple enemy tracking
    private List<EnemyController> activeEnemies = new List<EnemyController>();
    
    // Planet Loop Events
    public static System.Action<int, PlanetAreaData> OnAreaLoaded;
    public static System.Action<int> OnAreaStarted;
    public static System.Action<int, float> OnAreaCleared;
    public static System.Action<int, int> OnAreaTransition; // from, to
    public static System.Action<int> OnPlanetRotationComplete; // total rotations
    public static System.Action<int> OnEnemyCountChanged;
    public static System.Action OnAllEnemiesCleared;
    
    void Start()
    {
        InitializeSystem();
        StartPlanetLoop();
    }
    
    void InitializeSystem()
    {
        sphereController = GetComponent<SphereLevelController>();
        if (sphereController == null)
        {
            Debug.LogError("SphereLevelController required!");
            return;
        }
        
        // Subscribe to enemy death events
        GameEvents.OnEnemyDestroyed += OnEnemyDestroyed;
        
        if (enemyParent == null)
        {
            GameObject enemyContainer = new GameObject("EnemyContainer");
            enemyContainer.transform.SetParent(transform);
            enemyParent = enemyContainer.transform;
        }
        
        LogDebug("Planet Manager initialized");
    }
    
    void OnDestroy()
    {
        GameEvents.OnEnemyDestroyed -= OnEnemyDestroyed;
        
        if (areaTransitionCoroutine != null)
        {
            StopCoroutine(areaTransitionCoroutine);
        }
    }
    
    #region Planet Loop Management
    
    public void StartPlanetLoop()
    {
        LogDebug("Starting planet loop");
        LoadArea(0);
    }
    
    void LoadArea(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= areaData.Length) return;
        if (currentState == AreaState.Transitioning) return;
        
        currentAreaIndex = areaIndex;
        currentState = AreaState.Loading;
        
        var area = areaData[areaIndex];
        if (area == null)
        {
            Debug.LogError($"Area {areaIndex} data missing!");
            return;
        }
        
        LogDebug($"Loading area {areaIndex}: {area.areaName}");
        
        // Cleanup previous enemies
        ClearAllEnemies();
        
        // Rotate sphere to area
        sphereController.LoadState(areaIndex);
        
        OnAreaLoaded?.Invoke(areaIndex, area);
        
        // Setup enemies after rotation
        StartCoroutine(SetupAreaAfterRotation());
    }
    
    IEnumerator SetupAreaAfterRotation()
    {
        // Wait for sphere rotation
        yield return new WaitForSeconds(rotationTransitionTime);
        
        // Setup area
        yield return new WaitForSeconds(areaSetupTime);
        
        SpawnEnemiesForArea();
        
        currentState = AreaState.Active;
        OnAreaStarted?.Invoke(currentAreaIndex);
        
        LogDebug($"Area {currentAreaIndex} active with {enemiesRemaining} enemies");
    }
    
    void ClearArea()
    {
        if (currentState != AreaState.Active) return;
        
        currentState = AreaState.Cleared;
        OnAreaCleared?.Invoke(currentAreaIndex, Time.time);
        OnAllEnemiesCleared?.Invoke();
        
        LogDebug($"Area {currentAreaIndex} cleared!");
        
        // Immediate transition to next area
        TransitionToNextArea();
    }
    
    void TransitionToNextArea()
    {
        if (areaTransitionCoroutine != null) return;
        
        int nextIndex = (currentAreaIndex + 1) % areaData.Length;
        
        // Check if full planet rotation completed
        if (nextIndex == 0)
        {
            totalRotations++;
            OnPlanetRotationComplete?.Invoke(totalRotations);
            LogDebug($"Planet rotation #{totalRotations} completed!");
        }
        
        areaTransitionCoroutine = StartCoroutine(TransitionToArea(nextIndex));
    }
    
    IEnumerator TransitionToArea(int nextIndex)
    {
        currentState = AreaState.Transitioning;
        OnAreaTransition?.Invoke(currentAreaIndex, nextIndex);
        
        LogDebug($"Transitioning: {currentAreaIndex} → {nextIndex}");
        
        // Minimale Pause für smooth transition
        yield return new WaitForSeconds(0.1f);
        
        LoadArea(nextIndex);
        areaTransitionCoroutine = null;
    }
    
    #endregion
    
    #region Simple Enemy Management
    
    void SpawnEnemiesForArea()
    {
        var area = areaData[currentAreaIndex];
        if (area.enemyPrefabs == null || area.enemyPrefabs.Length == 0)
        {
            LogDebug($"No enemies for area {currentAreaIndex}");
            // Auto-clear if no enemies
            Invoke(nameof(ClearArea), 0.5f);
            return;
        }
        
        // Check if enemies should start in danger state based on progression
        bool shouldStartInDanger = ShouldEnemiesStartInDanger();
        
        // Simple direct spawning
        for (int i = 0; i < area.enemyPrefabs.Length; i++)
        {
            if (area.enemyPrefabs[i] == null) continue;
            
            Vector3 spawnPos = GetSpawnPosition(i, area);
            GameObject enemyGO = Instantiate(area.enemyPrefabs[i], spawnPos, Quaternion.identity, enemyParent);
            
            // Get enemy controller
            var enemyController = enemyGO.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                // Apply progression scaling
                if (shouldStartInDanger && enemyController.GetEnemySize() == EnemySize.Large)
                {
                    enemyController.SetStartInDangerState(true);
                    LogDebug($"Large enemy spawned in DANGER STATE (rotation {totalRotations})");
                }
                
                activeEnemies.Add(enemyController);
            }
        }
        
        enemiesRemaining = activeEnemies.Count;
        OnEnemyCountChanged?.Invoke(enemiesRemaining);
        
        LogDebug($"Spawned {activeEnemies.Count} enemies for area {currentAreaIndex}" + 
                (shouldStartInDanger ? " (DANGER MODE)" : ""));
    }
    
    bool ShouldEnemiesStartInDanger()
    {
        if (!enableProgressionScaling) return false;
        
        return totalRotations >= rotationsForDangerEnemies;
    }
    
    Vector3 GetSpawnPosition(int enemyIndex, PlanetAreaData area)
    {
        // Use custom positions if available
        if (area.spawnPositions != null && enemyIndex < area.spawnPositions.Length)
        {
            return transform.position + area.spawnPositions[enemyIndex];
        }
        
        // Default: circle around sphere
        float angle = (360f / area.enemyPrefabs.Length) * enemyIndex;
        float radius = area.spawnRadius;
        
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);
        
        return transform.position + offset;
    }
    
    void ClearAllEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null && enemy.gameObject != null)
            {
                DestroyImmediate(enemy.gameObject);
            }
        }
        
        activeEnemies.Clear();
        enemiesRemaining = 0;
        OnEnemyCountChanged?.Invoke(0);
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnEnemyDestroyed(EnemyController enemy, int energyValue)
    {
        if (currentState != AreaState.Active) return;
        
        // Handle enemy controller specifically
        var _enemy = enemy as EnemyController;
        if (_enemy != null && activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            enemiesRemaining = activeEnemies.Count;
            OnEnemyCountChanged?.Invoke(enemiesRemaining);
            
            LogDebug($"Enemy destroyed. Energy: +{energyValue}. Remaining: {enemiesRemaining}");
            
            // Check if area cleared
            if (enemiesRemaining <= 0)
            {
                ClearArea();
            }
        }
    }
    
    #endregion
    
    #region Utility
    
    public int GetCurrentArea() => currentAreaIndex;
    public int GetTotalRotations() => totalRotations;
    public int GetEnemiesRemaining() => enemiesRemaining;
    public AreaState GetCurrentState() => currentState;
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlanetManager] {message}");
        }
    }
    
    #endregion
    
    #region Context Menu & Debug
    
    [ContextMenu("Next Area")]
    void ForceNextArea() => TransitionToNextArea();
    
    [ContextMenu("Clear Current Area")]
    void ForceClearArea() => ClearArea();
    
    [ContextMenu("Spawn Enemies")]
    void ForceSpawnEnemies() => SpawnEnemiesForArea();
    
    void OnDrawGizmos()
    {
        if (!showAreaGizmos) return;
        
        // Current area highlight
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 6f);
        
        // Area spawn positions
        if (areaData != null && currentAreaIndex < areaData.Length)
        {
            var area = areaData[currentAreaIndex];
            if (area?.spawnPositions != null)
            {
                Gizmos.color = Color.red;
                foreach (var pos in area.spawnPositions)
                {
                    Gizmos.DrawWireCube(transform.position + pos, Vector3.one * 0.5f);
                }
            }
        }
    }
    
    #endregion
}

/// <summary>
/// Area States für Planet-Loop
/// </summary>
public enum AreaState
{
    Inactive,
    Loading,
    Active,
    Cleared,
    Transitioning
}

/// <summary>
/// Vereinfachte Planet Area Data - nur Essential
/// </summary>
[System.Serializable]
public class PlanetAreaData
{
    [Header("Area Info")]
    public string areaName = "Area 1";
    
    [Header("Simple Enemy Setup")]
    public GameObject[] enemyPrefabs = new GameObject[0];
    public Vector3[] spawnPositions = new Vector3[0];
    public float spawnRadius = 5f;
    
    [Header("Visual")]
    public Color areaColor = Color.white;
}