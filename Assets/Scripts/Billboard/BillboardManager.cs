using System.Collections.Generic;
using UnityEngine;

public class BillboardManager : MonoBehaviour
{
    public static BillboardManager Instance { get; private set; }
    
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool constrainY = false;
    [SerializeField] private bool useBulkUpdate = true;
    
    // Optimierte Listen für verschiedene Billboard-Typen
    private static readonly List<AnimatedBillboard> animatedBillboards = new List<AnimatedBillboard>(200);
    private static readonly List<AnimatedBillboard> staticBillboards = new List<AnimatedBillboard>(100);
    
    private Transform cameraTransform;
    private Vector3 lastCameraPosition;
    private bool cameraHasMoved = true;
    
    // Performance Monitoring
    [SerializeField] private bool showDebugInfo = false;
    private int lastUpdateCount = 0;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            targetCamera = targetCamera ?? Camera.main;
            CacheCamera();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void CacheCamera()
    {
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
        
        if (useBulkUpdate && cameraHasMoved)
        {
            BulkUpdateBillboards();
        }
        
        UpdateDebugInfo();
    }
    
    void CheckCameraMovement()
    {
        Vector3 currentPos = cameraTransform.position;
        cameraHasMoved = Vector3.SqrMagnitude(currentPos - lastCameraPosition) > 0.001f;
        
        if (cameraHasMoved)
        {
            lastCameraPosition = currentPos;
        }
    }
    
    void BulkUpdateBillboards()
    {
        Vector3 cameraPos = cameraTransform.position;
        
        // Update static billboards
        for (int i = staticBillboards.Count - 1; i >= 0; i--)
        {
            var billboard = staticBillboards[i];
            
            if (!billboard || !billboard.transform)
            {
                staticBillboards.RemoveAt(i);
                continue;
            }
            
            UpdateBillboardRotation(billboard.transform, cameraPos);
        }
        
        // AnimatedBillboards handhaben ihr eigenes Update
        // Cleanup null references
        for (int i = animatedBillboards.Count - 1; i >= 0; i--)
        {
            if (!animatedBillboards[i])
            {
                animatedBillboards.RemoveAt(i);
            }
        }
    }
    
    void UpdateBillboardRotation(Transform billboardTransform, Vector3 cameraPos)
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
            int totalBillboards = animatedBillboards.Count + staticBillboards.Count;
            if (lastUpdateCount != totalBillboards)
            {
                Debug.Log($"Billboards: {totalBillboards} (Animated: {animatedBillboards.Count}, Static: {staticBillboards.Count})");
                lastUpdateCount = totalBillboards;
            }
        }
    }
    
    #region Registration System
    
    // Für AnimatedBillboard (bevorzugt)
    public static void RegisterAnimatedBillboard(AnimatedBillboard billboard)
    {
        if (billboard == null) return;
        
        if (!animatedBillboards.Contains(billboard))
        {
            animatedBillboards.Add(billboard);
        }
    }
    
    public static void UnregisterAnimatedBillboard(AnimatedBillboard billboard)
    {
        if (billboard != null)
        {
            animatedBillboards.Remove(billboard);
        }
    }
    
    
    // Generic Registration für AnimatedBillboard Kompatibilität
    public static void RegisterBillboard(AnimatedBillboard billboard)
    {
        RegisterAnimatedBillboard(billboard);
    }
    
    public static void UnregisterBillboard(AnimatedBillboard billboard)
    {
        UnregisterAnimatedBillboard(billboard);
    }
    
    #endregion
    
    #region Public API
    
    public void SetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
        CacheCamera();
    }
    
    public static int GetTotalBillboardCount()
    {
        return animatedBillboards.Count + staticBillboards.Count;
    }
    
    public static int GetAnimatedBillboardCount()
    {
        return animatedBillboards.Count;
    }
    
    public static void ClearAllBillboards()
    {
        animatedBillboards.Clear();
        staticBillboards.Clear();
    }
    
    #endregion
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 300, 120));
        GUILayout.Label("Billboard Manager", GUI.skin.box);
        
        GUILayout.Label($"Total: {GetTotalBillboardCount()}");
        GUILayout.Label($"Animated: {animatedBillboards.Count}");
        GUILayout.Label($"Static: {staticBillboards.Count}");
        GUILayout.Label($"Camera Moved: {cameraHasMoved}");
        GUILayout.Label($"Bulk Update: {useBulkUpdate}");
        
        if (GUILayout.Button("Clear All"))
        {
            ClearAllBillboards();
        }
        
        GUILayout.EndArea();
    }
}
    
    

