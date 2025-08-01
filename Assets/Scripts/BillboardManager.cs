using UnityEngine;
using System.Collections.Generic;

// Einfaches, sehr performantes Billboard-System
public class BillboardManager : MonoBehaviour
{
    public static BillboardManager Instance { get; private set; }
    
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool constrainY = false; // Y-Achse fixieren für 2.5D
    
    // OPTIMIERUNG: Kapazität vordefiniert
    private static readonly List<Billboard> activeBillboards = new List<Billboard>(100);
    private Transform cameraTransform;
    
    // OPTIMIERUNG: Batch-Cleanup Variablen
    [SerializeField, Range(30, 300)] private int cleanupInterval = 60; // Frames zwischen Cleanups
    private int frameCounter = 0;
    private bool needsCleanup = false;
    
    // OPTIMIERUNG: Performance Monitoring
    [Header("Performance Debug")]
    [SerializeField] private bool showDebugInfo = false;
    private int lastBillboardCount = 0;
    
    public static event System.Action<Camera> OnCameraChanged;
    
    // OPTIMIERUNG: Public Property für Monitoring
    public static int ActiveCount => activeBillboards.Count;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            targetCamera = targetCamera ?? Camera.main;
            cacheCamera();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void cacheCamera()
    {
        if (targetCamera)
        {
            cameraTransform = targetCamera.transform;
            OnCameraChanged?.Invoke(targetCamera);
        }
    }
    
    void LateUpdate()
    {
        if (!cameraTransform || activeBillboards.Count == 0) return;
        
        // OPTIMIERUNG: Batch-Cleanup alle N Frames
        frameCounter++;
        if (frameCounter >= cleanupInterval || needsCleanup)
        {
            CleanupNullBillboards();
            frameCounter = 0;
            needsCleanup = false;
        }
        
        UpdateAllBillboards();
        
        // Debug Info
        if (showDebugInfo && lastBillboardCount != activeBillboards.Count)
        {
            Debug.Log($"Billboards: {activeBillboards.Count}");
            lastBillboardCount = activeBillboards.Count;
        }
    }
    
    // OPTIMIERUNG: Performante Update-Schleife ohne Null-Checks
    private void UpdateAllBillboards()
    {
        Vector3 cameraPos = cameraTransform.position;
        
        // Optimierte Schleife - keine Null-Checks im Hot Path
        for (int i = 0; i < activeBillboards.Count; i++)
        {
            var billboard = activeBillboards[i];
            var billboardTransform = billboard.transform;
            
            Vector3 direction = (cameraPos - billboardTransform.position).normalized;
            
            if (constrainY)
            {
                direction.y = 0;
                direction.Normalize();
            }
            
            billboardTransform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    // OPTIMIERUNG: Separate Cleanup-Methode
    private void CleanupNullBillboards()
    {
        int originalCount = activeBillboards.Count;
        
        // Rückwärts iterieren für sicheres Entfernen
        for (int i = activeBillboards.Count - 1; i >= 0; i--)
        {
            if (!activeBillboards[i] || !activeBillboards[i].transform)
            {
                activeBillboards.RemoveAt(i);
            }
        }
        
        // Debug Info bei Cleanup
        if (showDebugInfo && originalCount != activeBillboards.Count)
        {
            Debug.Log($"Billboard Cleanup: {originalCount - activeBillboards.Count} null references removed");
        }
    }
    
    // OPTIMIERUNG: Intelligente Registrierung
    public static void RegisterBillboard(Billboard billboard)
    {
        if (billboard == null) return;
        
        // Doppelte Registrierung verhindern - schnellere Contains-Alternative
        for (int i = 0; i < activeBillboards.Count; i++)
        {
            if (activeBillboards[i] == billboard)
                return; // Bereits registriert
        }
        
        activeBillboards.Add(billboard);
        
        // Debug
        if (Instance && Instance.showDebugInfo)
        {
            Debug.Log($"Billboard registered: {billboard.name} (Total: {activeBillboards.Count})");
        }
    }
    
    // OPTIMIERUNG: Schnellere Deregistrierung
    public static void UnregisterBillboard(Billboard billboard)
    {
        if (billboard == null) return;
        
        // Schnelle Entfernung ohne Contains-Check
        for (int i = 0; i < activeBillboards.Count; i++)
        {
            if (activeBillboards[i] == billboard)
            {
                activeBillboards.RemoveAt(i);
                
                // Debug
                if (Instance && Instance.showDebugInfo)
                {
                    Debug.Log($"Billboard unregistered: {billboard.name} (Total: {activeBillboards.Count})");
                }
                return;
            }
        }
    }
    
    // OPTIMIERUNG: Force Cleanup für kritische Situationen
    [ContextMenu("Force Cleanup")]
    public void ForceCleanup()
    {
        CleanupNullBillboards();
        Debug.Log($"Force cleanup completed. Active billboards: {activeBillboards.Count}");
    }
    
    // OPTIMIERUNG: Immediate Cleanup Flag
    public static void RequestCleanup()
    {
        if (Instance)
            Instance.needsCleanup = true;
    }
    
    public void SetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
        cacheCamera();
    }
    
    // OPTIMIERUNG: Bulk Operations für bessere Performance
    public static void RegisterBillboards(Billboard[] billboards)
    {
        if (billboards == null || billboards.Length == 0) return;
        
        // Kapazität vorab erweitern falls nötig
        if (activeBillboards.Capacity < activeBillboards.Count + billboards.Length)
        {
            activeBillboards.Capacity = activeBillboards.Count + billboards.Length + 10;
        }
        
        for (int i = 0; i < billboards.Length; i++)
        {
            if (billboards[i] != null)
                RegisterBillboard(billboards[i]);
        }
    }
    
    public static void UnregisterBillboards(Billboard[] billboards)
    {
        if (billboards == null || billboards.Length == 0) return;
        
        for (int i = 0; i < billboards.Length; i++)
        {
            if (billboards[i] != null)
                UnregisterBillboard(billboards[i]);
        }
        
        // Nach Bulk-Unregister sofortiges Cleanup
        RequestCleanup();
    }
    
    // OPTIMIERUNG: Memory-freundliches Clear
    public static void ClearAllBillboards()
    {
        activeBillboards.Clear();
        
        if (Instance && Instance.showDebugInfo)
        {
            Debug.Log("All billboards cleared");
        }
    }
    
    void OnDestroy()
    {
        ClearAllBillboards();
    }
    
    // Performance Stats für Debugging
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.Label($"Billboards: {activeBillboards.Count}");
        GUILayout.Label($"Cleanup Interval: {cleanupInterval} frames");
        GUILayout.Label($"Next Cleanup: {cleanupInterval - frameCounter} frames");
        if (GUILayout.Button("Force Cleanup"))
        {
            ForceCleanup();
        }
        GUILayout.EndArea();
    }
}