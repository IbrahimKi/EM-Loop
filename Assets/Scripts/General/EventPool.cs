using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// High-Performance Event Pooling System für Unity 6 LTS
/// Verhindert GC-Allokationen durch Event-Wiederverwendung
/// </summary>
public static class EventPool
{
    [Header("Pool Configuration")]
    private const int DEFAULT_POOL_SIZE = 32;
    private const int MAX_POOL_SIZE = 128;
    private const bool ENABLE_DEBUG = false;
    
    // Action Pools für verschiedene Signatures
    private static readonly Queue<System.Action> actionPool = new Queue<System.Action>(DEFAULT_POOL_SIZE);
    private static readonly Queue<System.Action<Vector3, float, Vector3>> vector3ActionPool = new Queue<System.Action<Vector3, float, Vector3>>(DEFAULT_POOL_SIZE);
    private static readonly Queue<System.Action<List<Vector3>, Vector3>> listVector3ActionPool = new Queue<System.Action<List<Vector3>, Vector3>>(DEFAULT_POOL_SIZE);
    private static readonly Queue<System.Action<CircleTarget, Vector3, float>> targetActionPool = new Queue<System.Action<CircleTarget, Vector3, float>>(DEFAULT_POOL_SIZE);
    private static readonly Queue<System.Action<int>> intActionPool = new Queue<System.Action<int>>(DEFAULT_POOL_SIZE);
    private static readonly Queue<System.Action<RotationCommand>> rotationActionPool = new Queue<System.Action<RotationCommand>>(DEFAULT_POOL_SIZE);
    private static readonly Queue<System.Action<Camera>> cameraActionPool = new Queue<System.Action<Camera>>(DEFAULT_POOL_SIZE);
    
    // Performance Statistics
    private static int totalGets = 0;
    private static int totalReturns = 0;
    private static int poolHits = 0;
    private static int poolMisses = 0;
    
    #region Action Pool
    
    /// <summary>
    /// Holt eine Action aus dem Pool oder erstellt eine neue
    /// </summary>
    public static System.Action GetAction()
    {
        totalGets++;
        
        if (actionPool.Count > 0)
        {
            poolHits++;
            var action = actionPool.Dequeue();
            
            if (ENABLE_DEBUG)
                Debug.Log($"EventPool: Action retrieved from pool (remaining: {actionPool.Count})");
            
            return action;
        }
        
        poolMisses++;
        if (ENABLE_DEBUG)
            Debug.Log("EventPool: New Action created");
        
        return new System.Action(() => {});
    }
    
    /// <summary>
    /// Gibt eine Action zurück in den Pool
    /// </summary>
    public static void ReturnAction(System.Action action)
    {
        if (action == null || actionPool.Count >= MAX_POOL_SIZE) return;
        
        totalReturns++;
        actionPool.Enqueue(action);
        
        if (ENABLE_DEBUG)
            Debug.Log($"EventPool: Action returned to pool (total: {actionPool.Count})");
    }
    
    #endregion
    
    #region Vector3 Action Pool
    
    public static System.Action<Vector3, float, Vector3> GetVector3Action()
    {
        totalGets++;
        
        if (vector3ActionPool.Count > 0)
        {
            poolHits++;
            return vector3ActionPool.Dequeue();
        }
        
        poolMisses++;
        return new System.Action<Vector3, float, Vector3>((v1, f, v2) => {});
    }
    
    public static void ReturnVector3Action(System.Action<Vector3, float, Vector3> action)
    {
        if (action == null || vector3ActionPool.Count >= MAX_POOL_SIZE) return;
        
        totalReturns++;
        vector3ActionPool.Enqueue(action);
    }
    
    #endregion
    
    #region List<Vector3> Action Pool
    
    public static System.Action<List<Vector3>, Vector3> GetListVector3Action()
    {
        totalGets++;
        
        if (listVector3ActionPool.Count > 0)
        {
            poolHits++;
            return listVector3ActionPool.Dequeue();
        }
        
        poolMisses++;
        return new System.Action<List<Vector3>, Vector3>((list, v) => {});
    }
    
    public static void ReturnListVector3Action(System.Action<List<Vector3>, Vector3> action)
    {
        if (action == null || listVector3ActionPool.Count >= MAX_POOL_SIZE) return;
        
        totalReturns++;
        listVector3ActionPool.Enqueue(action);
    }
    
    #endregion
    
    #region Target Action Pool
    
    public static System.Action<CircleTarget, Vector3, float> GetTargetAction()
    {
        totalGets++;
        
        if (targetActionPool.Count > 0)
        {
            poolHits++;
            return targetActionPool.Dequeue();
        }
        
        poolMisses++;
        return new System.Action<CircleTarget, Vector3, float>((target, pos, bonus) => {});
    }
    
    public static void ReturnTargetAction(System.Action<CircleTarget, Vector3, float> action)
    {
        if (action == null || targetActionPool.Count >= MAX_POOL_SIZE) return;
        
        totalReturns++;
        targetActionPool.Enqueue(action);
    }
    
    #endregion
    
    #region Int Action Pool
    
    public static System.Action<int> GetIntAction()
    {
        totalGets++;
        
        if (intActionPool.Count > 0)
        {
            poolHits++;
            return intActionPool.Dequeue();
        }
        
        poolMisses++;
        return new System.Action<int>(i => {});
    }
    
    public static void ReturnIntAction(System.Action<int> action)
    {
        if (action == null || intActionPool.Count >= MAX_POOL_SIZE) return;
        
        totalReturns++;
        intActionPool.Enqueue(action);
    }
    
    #endregion
    
    #region Rotation Action Pool
    
    public static System.Action<RotationCommand> GetRotationAction()
    {
        totalGets++;
        
        if (rotationActionPool.Count > 0)
        {
            poolHits++;
            return rotationActionPool.Dequeue();
        }
        
        poolMisses++;
        return new System.Action<RotationCommand>(cmd => {});
    }
    
    public static void ReturnRotationAction(System.Action<RotationCommand> action)
    {
        if (action == null || rotationActionPool.Count >= MAX_POOL_SIZE) return;
        
        totalReturns++;
        rotationActionPool.Enqueue(action);
    }
    
    #endregion
    
    #region Camera Action Pool
    
    public static System.Action<Camera> GetCameraAction()
    {
        totalGets++;
        
        if (cameraActionPool.Count > 0)
        {
            poolHits++;
            return cameraActionPool.Dequeue();
        }
        
        poolMisses++;
        return new System.Action<Camera>(cam => {});
    }
    
    public static void ReturnCameraAction(System.Action<Camera> action)
    {
        if (action == null || cameraActionPool.Count >= MAX_POOL_SIZE) return;
        
        totalReturns++;
        cameraActionPool.Enqueue(action);
    }
    
    #endregion
    
    #region Pool Management
    
    /// <summary>
    /// Initialisiert alle Pools mit Standardgröße
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitializePools()
    {
        // Pre-populate pools für bessere Performance
        for (int i = 0; i < DEFAULT_POOL_SIZE / 4; i++)
        {
            actionPool.Enqueue(new System.Action(() => {}));
            vector3ActionPool.Enqueue(new System.Action<Vector3, float, Vector3>((v1, f, v2) => {}));
            listVector3ActionPool.Enqueue(new System.Action<List<Vector3>, Vector3>((list, v) => {}));
            targetActionPool.Enqueue(new System.Action<CircleTarget, Vector3, float>((target, pos, bonus) => {}));
            intActionPool.Enqueue(new System.Action<int>(i => {}));
            rotationActionPool.Enqueue(new System.Action<RotationCommand>(cmd => {}));
            cameraActionPool.Enqueue(new System.Action<Camera>(cam => {}));
        }
        
        if (ENABLE_DEBUG)
            Debug.Log($"EventPool: Initialized with {DEFAULT_POOL_SIZE / 4} items per pool");
    }
    
    /// <summary>
    /// Leert alle Pools - nützlich bei Level-Wechseln
    /// </summary>
    public static void ClearAllPools()
    {
        actionPool.Clear();
        vector3ActionPool.Clear();
        listVector3ActionPool.Clear();
        targetActionPool.Clear();
        intActionPool.Clear();
        rotationActionPool.Clear();
        cameraActionPool.Clear();
        
        // Reset statistics
        totalGets = 0;
        totalReturns = 0;
        poolHits = 0;
        poolMisses = 0;
        
        Debug.Log("EventPool: All pools cleared");
    }
    
    /// <summary>
    /// Gibt Pool-Statistiken aus
    /// </summary>
    public static void PrintStatistics()
    {
        float hitRatio = totalGets > 0 ? (float)poolHits / totalGets * 100f : 0f;
        
        Debug.Log($"EventPool Statistics:" +
                 $"\nTotal Gets: {totalGets}" +
                 $"\nTotal Returns: {totalReturns}" +
                 $"\nPool Hits: {poolHits}" +
                 $"\nPool Misses: {poolMisses}" +
                 $"\nHit Ratio: {hitRatio:F1}%" +
                 $"\nAction Pool: {actionPool.Count}" +
                 $"\nVector3 Pool: {vector3ActionPool.Count}" +
                 $"\nListVector3 Pool: {listVector3ActionPool.Count}" +
                 $"\nTarget Pool: {targetActionPool.Count}" +
                 $"\nInt Pool: {intActionPool.Count}" +
                 $"\nRotation Pool: {rotationActionPool.Count}" +
                 $"\nCamera Pool: {cameraActionPool.Count}");
    }
    
    /// <summary>
    /// Gibt Pool-Auslastung zurück
    /// </summary>
    public static PoolStats GetPoolStats()
    {
        return new PoolStats
        {
            actionPoolCount = actionPool.Count,
            vector3PoolCount = vector3ActionPool.Count,
            listVector3PoolCount = listVector3ActionPool.Count,
            targetPoolCount = targetActionPool.Count,
            intPoolCount = intActionPool.Count,
            rotationPoolCount = rotationActionPool.Count,
            cameraPoolCount = cameraActionPool.Count,
            totalGets = totalGets,
            totalReturns = totalReturns,
            poolHits = poolHits,
            poolMisses = poolMisses,
            hitRatio = totalGets > 0 ? (float)poolHits / totalGets : 0f
        };
    }
    
    #endregion
}

/// <summary>
/// Pool-Statistiken Struct
/// </summary>
public struct PoolStats
{
    public int actionPoolCount;
    public int vector3PoolCount;
    public int listVector3PoolCount;
    public int targetPoolCount;
    public int intPoolCount;
    public int rotationPoolCount;
    public int cameraPoolCount;
    public int totalGets;
    public int totalReturns;
    public int poolHits;
    public int poolMisses;
    public float hitRatio;
}

/// <summary>
/// MonoBehaviour Component für Event Pool Management im Inspector
/// </summary>
public class EventPoolManager : MonoBehaviour
{
    [Header("Event Pool Settings")]
    [SerializeField] private bool showDebugGUI = false;
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private bool clearOnLevelLoad = true;
    
    [Header("Statistics")]
    [SerializeField, ReadOnly] private PoolStats currentStats;
    
    void Start()
    {
        if (autoInitialize)
        {
            EventPool.InitializePools();
        }
    }
    
    void Update()
    {
        // Update stats every second
        if (Time.frameCount % 60 == 0)
        {
            currentStats = EventPool.GetPoolStats();
        }
    }
    
    void OnLevelWasLoaded(int level)
    {
        if (clearOnLevelLoad)
        {
            EventPool.ClearAllPools();
            if (autoInitialize)
            {
                EventPool.InitializePools();
            }
        }
    }
    
    void OnGUI()
    {
        if (!showDebugGUI) return;
        
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 300, 200));
        GUILayout.Label("Event Pool Manager", GUI.skin.box);
        
        currentStats = EventPool.GetPoolStats();
        
        GUILayout.Label($"Hit Ratio: {currentStats.hitRatio:F1}%");
        GUILayout.Label($"Total Gets: {currentStats.totalGets}");
        GUILayout.Label($"Pool Hits: {currentStats.poolHits}");
        
        GUILayout.Space(5);
        GUILayout.Label("Pool Counts:");
        GUILayout.Label($"Action: {currentStats.actionPoolCount}");
        GUILayout.Label($"Vector3: {currentStats.vector3PoolCount}");
        GUILayout.Label($"Target: {currentStats.targetPoolCount}");
        
        GUILayout.Space(5);
        if (GUILayout.Button("Print Full Stats"))
        {
            EventPool.PrintStatistics();
        }
        
        if (GUILayout.Button("Clear All Pools"))
        {
            EventPool.ClearAllPools();
        }
        
        GUILayout.EndArea();
    }
    
    [ContextMenu("Initialize Pools")]
    public void InitializePools() => EventPool.InitializePools();
    
    [ContextMenu("Clear All Pools")]
    public void ClearAllPools() => EventPool.ClearAllPools();
    
    [ContextMenu("Print Statistics")]
    public void PrintStatistics() => EventPool.PrintStatistics();
}

/// <summary>
/// ReadOnly Attribute für Inspector
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }