using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color drawColor = Color.yellow;
    [SerializeField] private Color confirmColor = Color.green;
    [SerializeField] private float heightOffset = 0.1f;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxPathPoints = 50;
    [SerializeField] private int circleSegments = 32;
    [SerializeField] private bool enableDebug = false;
    
    private LineRenderer lineRenderer;
    private Coroutine confirmRoutine;
    
    // OPTIMIERUNG: Gecachte Arrays - keine Allokationen zur Laufzeit
    private Vector3[] pathPositionsCache;
    private Vector3[] circlePositionsCache;
    
    // OPTIMIERUNG: Basis-Vektoren Cache für Tangentialebene
    private Vector3 cachedTangent1;
    private Vector3 cachedTangent2;
    private Vector3 lastNormal;
    
    // OPTIMIERUNG: Performance Monitoring
    private int lastPathLength = 0;
    private float lastUpdateTime = 0f;
    
    void Awake()
    {
        SetupLineRenderer();
        InitializeCaches();
        
        if (sphereCenter == null)
            sphereCenter = transform.parent;
    }
    
    void InitializeCaches()
    {
        // Pre-allocate arrays basierend auf maxPathPoints
        pathPositionsCache = new Vector3[maxPathPoints];
        circlePositionsCache = new Vector3[circleSegments + 1];
        
        if (enableDebug)
        {
            Debug.Log($"CircleVisualizer: Initialized caches - Path: {maxPathPoints}, Circle: {circleSegments + 1}");
        }
    }
    
    void OnEnable()
    {
        CircleSelector.OnPathUpdated += UpdatePath;
        CircleSelector.OnCircleConfirmed += ShowConfirmation;
        CircleSelector.OnDrawingCancelled += ClearPath;
    }
    
    void OnDisable()
    {
        CircleSelector.OnPathUpdated -= UpdatePath;
        CircleSelector.OnCircleConfirmed -= ShowConfirmation;
        CircleSelector.OnDrawingCancelled -= ClearPath;
    }
    
    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startColor = drawColor;
        lineRenderer.endColor = drawColor;
        lineRenderer.positionCount = 0;
        
        // OPTIMIERUNG: Weitere LineRenderer Einstellungen für Performance
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.allowOcclusionWhenDynamic = false;
    }
    
    // OPTIMIERUNG: Cached Array Update ohne Neuallokation
    void UpdatePath(List<Vector3> path, Vector3 normal)
    {
        if (path == null || path.Count == 0) 
        {
            ClearPath();
            return;
        }
        
        // Performance-Check: Nur bei Änderungen updaten
        if (path.Count == lastPathLength && Time.time - lastUpdateTime < 0.016f)
            return; // Skip wenn < 60 FPS Updates
        
        lineRenderer.startColor = drawColor;
        lineRenderer.endColor = drawColor;
        
        int pointCount = Mathf.Min(path.Count, pathPositionsCache.Length);
        
        // OPTIMIERUNG: Direkt in gecachten Array schreiben
        for (int i = 0; i < pointCount; i++)
        {
            pathPositionsCache[i] = path[i] + normal * heightOffset;
        }
        
        // OPTIMIERUNG: Nur nötige Positionen setzen
        lineRenderer.positionCount = pointCount;
        lineRenderer.SetPositions(pathPositionsCache);
        
        // Cache Update
        lastPathLength = path.Count;
        lastUpdateTime = Time.time;
        
        if (enableDebug && pointCount != path.Count)
        {
            Debug.LogWarning($"Path truncated: {path.Count} -> {pointCount} points");
        }
    }
    
    void ShowConfirmation(Vector3 center, float radius, Vector3 normal)
    {
        if (confirmRoutine != null)
            StopCoroutine(confirmRoutine);
        
        confirmRoutine = StartCoroutine(ConfirmationEffect(center, radius, normal));
    }
    
    // OPTIMIERUNG: Cached Basis-Vektoren für Tangentialebene
    void CalculateTangentBasis(Vector3 normal)
    {
        // Nur neu berechnen wenn sich Normal ändert
        if (Vector3.Dot(normal, lastNormal) > 0.999f) return;
        
        cachedTangent1 = Vector3.Cross(normal, Vector3.up);
        if (cachedTangent1.magnitude < 0.1f) 
            cachedTangent1 = Vector3.Cross(normal, Vector3.forward);
        cachedTangent1.Normalize();
        
        cachedTangent2 = Vector3.Cross(normal, cachedTangent1).normalized;
        lastNormal = normal;
        
        if (enableDebug)
        {
            Debug.Log($"Tangent basis recalculated for normal: {normal}");
        }
    }
    
    System.Collections.IEnumerator ConfirmationEffect(Vector3 center, float radius, Vector3 normal)
    {
        // OPTIMIERUNG: Cached Tangent-Basis verwenden
        CalculateTangentBasis(normal);
        
        // Perfect circle in tangent plane - verwendet gecachten Array
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            
            Vector3 circlePoint = center + 
                (cachedTangent1 * cos + cachedTangent2 * sin) * radius +
                normal * heightOffset;
            
            circlePositionsCache[i] = circlePoint;
        }
        
        // OPTIMIERUNG: Farbe nur bei Änderung setzen
        if (lineRenderer.startColor != confirmColor)
        {
            lineRenderer.startColor = confirmColor;
            lineRenderer.endColor = confirmColor;
        }
        
        lineRenderer.positionCount = circlePositionsCache.Length;
        lineRenderer.SetPositions(circlePositionsCache);
        
        if (enableDebug)
        {
            Debug.Log($"Confirmation circle: {circleSegments} segments, radius: {radius:F2}");
        }
        
        yield return new WaitForSeconds(1f);
        ClearPath();
    }
    
    void ClearPath()
    {
        if (confirmRoutine != null)
        {
            StopCoroutine(confirmRoutine);
            confirmRoutine = null;
        }
        
        lineRenderer.positionCount = 0;
        lastPathLength = 0;
        
        if (enableDebug)
        {
            Debug.Log("Path cleared");
        }
    }
    
    // OPTIMIERUNG: Runtime-Konfiguration
    public void SetMaxPathPoints(int newMax)
    {
        if (newMax <= 0 || newMax == maxPathPoints) return;
        
        maxPathPoints = newMax;
        pathPositionsCache = new Vector3[maxPathPoints];
        
        if (enableDebug)
        {
            Debug.Log($"Path cache resized to {maxPathPoints} points");
        }
    }
    
    public void SetCircleSegments(int newSegments)
    {
        if (newSegments < 8 || newSegments == circleSegments) return;
        
        circleSegments = newSegments;
        circlePositionsCache = new Vector3[circleSegments + 1];
        
        if (enableDebug)
        {
            Debug.Log($"Circle cache resized to {circleSegments + 1} points");
        }
    }
    
    // OPTIMIERUNG: Bulk-Update für bessere Performance bei vielen Änderungen
    public void UpdateLineRendererSettings(float newWidth, Color newDrawColor, Color newConfirmColor)
    {
        bool changed = false;
        
        if (Mathf.Abs(lineRenderer.startWidth - newWidth) > 0.001f)
        {
            lineRenderer.startWidth = newWidth;
            lineRenderer.endWidth = newWidth;
            changed = true;
        }
        
        if (drawColor != newDrawColor)
        {
            drawColor = newDrawColor;
            changed = true;
        }
        
        if (confirmColor != newConfirmColor)
        {
            confirmColor = newConfirmColor;
            changed = true;
        }
        
        if (changed && enableDebug)
        {
            Debug.Log($"LineRenderer settings updated - Width: {newWidth}, DrawColor: {newDrawColor}");
        }
    }
    
    // Performance-Monitoring
    void OnGUI()
    {
        if (!enableDebug) return;
        
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(320, 10, 250, 120));
        GUILayout.Label("CircleVisualizer Debug:");
        GUILayout.Label($"Path Cache: {pathPositionsCache?.Length ?? 0}");
        GUILayout.Label($"Circle Cache: {circlePositionsCache?.Length ?? 0}");
        GUILayout.Label($"Last Path Length: {lastPathLength}");
        GUILayout.Label($"LineRenderer Points: {lineRenderer.positionCount}");
        GUILayout.EndArea();
    }
    
    // OPTIMIERUNG: Cleanup bei Destroy
    void OnDestroy()
    {
        if (confirmRoutine != null)
        {
            StopCoroutine(confirmRoutine);
        }
        
        // Arrays werden automatisch von GC aufgeräumt
        pathPositionsCache = null;
        circlePositionsCache = null;
    }
}