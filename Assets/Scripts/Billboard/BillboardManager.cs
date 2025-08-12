using System.Collections.Generic;
using UnityEngine;

public class BillboardManager : MonoBehaviour
{
    public static BillboardManager Instance { get; private set; }
    
    [SerializeField] private Camera targetCameraOverride;
    [SerializeField] private bool constrainYOverride = false;
    [SerializeField] private bool useBulkUpdateOverride = true;
    
    // Config
    private GameConfig config;
    
    // Optimierte Listen für verschiedene Billboard-Typen
    private static readonly List<Billboard> allBillboards = new List<Billboard>(300);
    private static readonly List<AnimatedBillboard> animatedBillboards = new List<AnimatedBillboard>(200);
    
    private Transform cameraTransform;
    private Vector3 lastCameraPosition;
    private bool cameraHasMoved = true;
    
    // Performance Monitoring
    [SerializeField] private bool showDebugInfo = false;
    private int lastUpdateCount = 0;
    
    // Events
    public static System.Action<int> OnBillboardCountChanged;
    public static System.Action OnCameraMoved;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            config = GameConfig.Instance;
            SetupCamera();
            
            // Subscribe to billboard events
            Billboard.OnBillboardRegistered += OnBillboardRegistered;
            Billboard.OnBillboardUnregistered += OnBillboardUnregistered;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Billboard.OnBillboardRegistered -= OnBillboardRegistered;
            Billboard.OnBillboardUnregistered -= OnBillboardUnregistered;
        }
    }
    
    void SetupCamera()
    {
        Camera targetCamera = targetCameraOverride ?? GameReferences.MainCamera;
        if (targetCamera)
        {
            cameraTransform = targetCamera.transform;
            lastCameraPosition = cameraTransform.position;
        }
    }
    
    void LateUpdate()
    {
        if (!cameraTransform) return;
        
        CheckCameraMovement();
        
        bool shouldUseBulkUpdate = GetBulkUpdateSetting();
        if (shouldUseBulkUpdate && cameraHasMoved)
        {
            BulkUpdateBillboards();
        }
        
        UpdateDebugInfo();
    }
    
    bool GetBulkUpdateSetting()
    {
        if (useBulkUpdateOverride != true) return useBulkUpdateOverride;
        return config?.useBulkBillboardUpdate ?? true;
    }
    
    bool GetConstrainYSetting()
    {
        if (constrainYOverride != false) return constrainYOverride;
        return config?.billboardConstrainY ?? false;
    }
    
    void CheckCameraMovement()
    {
        Vector3 currentPos = cameraTransform.position;
        float threshold = config?.billboardUpdateThreshold ?? 0.001f;
        cameraHasMoved = Vector3.SqrMagnitude(currentPos - lastCameraPosition) > threshold;
        
        if (cameraHasMoved)
        {
            lastCameraPosition = currentPos;
            OnCameraMoved?.Invoke();
        }
    }
    
    void BulkUpdateBillboards()
    {
        Vector3 cameraPos = cameraTransform.position;
        bool constrainY = GetConstrainYSetting();
        
        // Update alle Billboards (außer AnimatedBillboards, die sich selbst updaten)
        for (int i = allBillboards.Count - 1; i >= 0; i--)
        {
            var billboard = allBillboards[i];
            
            if (!billboard || !billboard.transform)
            {
                allBillboards.RemoveAt(i);
                continue;
            }
            
            // Skip AnimatedBillboards - sie handhaben ihr eigenes Update
            if (billboard is AnimatedBillboard) continue;
            
            UpdateBillboardRotation(billboard.transform, cameraPos, constrainY);
        }
        
        // Cleanup null references in animated list
        for (int i = animatedBillboards.Count - 1; i >= 0; i--)
        {
            if (!animatedBillboards[i])
            {
                animatedBillboards.RemoveAt(i);
            }
        }
    }
    
    void UpdateBillboardRotation(Transform billboardTransform, Vector3 cameraPos, bool constrainY)
    {
        Vector3 direction = (cameraPos - billboardTransform.position).normalized;
        
        if (constrainY)
        {
            direction.y = 0;
            direction.Normalize();
        }
        
        billboardTransform.rotation = Quaternion.LookRotation(direction);
    }
    
    void UpdateDebugInfo()
    {
        if (showDebugInfo)
        {
            int totalBillboards = allBillboards.Count;
            if (lastUpdateCount != totalBillboards)
            {
                Debug.Log($"Billboards: {totalBillboards} (Animated: {animatedBillboards.Count}, Static: {totalBillboards - animatedBillboards.Count})");
                lastUpdateCount = totalBillboards;
                OnBillboardCountChanged?.Invoke(totalBillboards);
            }
        }
    }
    
    #region Event Handlers
    
    void OnBillboardRegistered(Billboard billboard)
    {
        if (billboard == null) return;
        
        if (!allBillboards.Contains(billboard))
        {
            allBillboards.Add(billboard);
        }
        
        // Track animated billboards separately
        if (billboard is AnimatedBillboard animatedBillboard)
        {
            if (!animatedBillboards.Contains(animatedBillboard))
            {
                animatedBillboards.Add(animatedBillboard);
            }
        }
        
        OnBillboardCountChanged?.Invoke(allBillboards.Count);
    }
    
    void OnBillboardUnregistered(Billboard billboard)
    {
        if (billboard != null)
        {
            allBillboards.Remove(billboard);
            
            if (billboard is AnimatedBillboard animatedBillboard)
            {
                animatedBillboards.Remove(animatedBillboard);
            }
            
            OnBillboardCountChanged?.Invoke(allBillboards.Count);
        }
    }
    
    #endregion
    
    #region Registration System (Legacy Support)
    
    // Legacy methods für AnimatedBillboard Kompatibilität
    public static void RegisterAnimatedBillboard(AnimatedBillboard billboard)
    {
        if (billboard != null)
        {
            billboard.Register();
        }
    }
    
    public static void UnregisterAnimatedBillboard(AnimatedBillboard billboard)
    {
        if (billboard != null)
        {
            billboard.Unregister();
        }
    }
    
    // Generic Registration
    public static void RegisterBillboard(Billboard billboard)
    {
        if (billboard != null)
        {
            billboard.Register();
        }
    }
    
    public static void UnregisterBillboard(Billboard billboard)
    {
        if (billboard != null)
        {
            billboard.Unregister();
        }
    }
    
    #endregion
    
    #region Public API
    
    public void SetCamera(Camera newCamera)
    {
        targetCameraOverride = newCamera;
        SetupCamera();
    }
    
    public static int GetTotalBillboardCount()
    {
        return allBillboards.Count;
    }
    
    public static int GetAnimatedBillboardCount()
    {
        return animatedBillboards.Count;
    }
    
    public static int GetStaticBillboardCount()
    {
        return allBillboards.Count - animatedBillboards.Count;
    }
    
    public static List<Billboard> GetAllBillboards()
    {
        return new List<Billboard>(allBillboards);
    }
    
    public static List<AnimatedBillboard> GetAnimatedBillboards()
    {
        return new List<AnimatedBillboard>(animatedBillboards);
    }
    
    public static void ClearAllBillboards()
    {
        allBillboards.Clear();
        animatedBillboards.Clear();
    }
    
    public static void SetBulkUpdate(bool enabled)
    {
        if (Instance != null)
        {
            Instance.useBulkUpdateOverride = enabled;
        }
    }
    
    public void RefreshCamera()
    {
        SetupCamera();
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Refresh Camera")]
    void DebugRefreshCamera() => RefreshCamera();
    
    [ContextMenu("Debug Config Info")]
    void DebugConfigInfo()
    {
        Debug.Log($"[BillboardManager] Config Info:\n" +
                 $"  Config Found: {config != null}\n" +
                 $"  Bulk Update: {GetBulkUpdateSetting()}\n" +
                 $"  Constrain Y: {GetConstrainYSetting()}\n" +
                 $"  Update Threshold: {config?.billboardUpdateThreshold ?? 0.001f}\n" +
                 $"  Camera: {cameraTransform?.name ?? "NULL"}");
    }
    
    #endregion
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 300, 180));
        GUILayout.Label("Billboard Manager", GUI.skin.box);
        
        GUILayout.Label($"Total: {GetTotalBillboardCount()}");
        GUILayout.Label($"Animated: {animatedBillboards.Count}");
        GUILayout.Label($"Static: {GetStaticBillboardCount()}");
        GUILayout.Label($"Camera Moved: {cameraHasMoved}");
        GUILayout.Label($"Bulk Update: {GetBulkUpdateSetting()}");
        GUILayout.Label($"Constrain Y: {GetConstrainYSetting()}");
        GUILayout.Label($"Config: {(config != null ? "✅" : "❌")}");
        
        if (GUILayout.Button("Clear All"))
        {
            ClearAllBillboards();
        }
        
        if (GUILayout.Button("Toggle Bulk Update"))
        {
            useBulkUpdateOverride = !useBulkUpdateOverride;
        }
        
        if (GUILayout.Button("Refresh Camera"))
        {
            RefreshCamera();
        }
        
        GUILayout.EndArea();
    }
}