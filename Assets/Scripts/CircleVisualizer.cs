using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color drawColor = Color.yellow;
    [SerializeField] private Color confirmColor = Color.green;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Drawing Mode")]
    [SerializeField] private DrawMode drawMode = DrawMode.WorldSpace;
    [SerializeField] private float worldSpaceHeight = 0.1f;
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private LayerMask drawingSurface = -1;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxPathPoints = 50;
    [SerializeField] private int circleSegments = 32;
    [SerializeField] private bool enableDebug = false;
    
    public enum DrawMode
    {
        WorldSpace,      // Mit heightOffset
        CursorProjection // Genau auf Cursor-Position projiziert
    }
    
    private LineRenderer lineRenderer;
    private Camera cam;
    private Coroutine confirmRoutine;
    
    // OPTIMIERUNG: Gecachte Arrays
    private Vector3[] pathPositionsCache;
    private Vector3[] circlePositionsCache;
    
    // OPTIMIERUNG: Basis-Vektoren Cache
    private Vector3 cachedTangent1;
    private Vector3 cachedTangent2;
    private Vector3 lastNormal;
    
    // Performance Monitoring
    private int lastPathLength = 0;
    private float lastUpdateTime = 0f;
    
    void Awake()
    {
        SetupLineRenderer();
        InitializeCaches();
        cam = Camera.main;
        
        if (sphereCenter == null)
            sphereCenter = transform.parent;
    }
    
    void InitializeCaches()
    {
        pathPositionsCache = new Vector3[maxPathPoints];
        circlePositionsCache = new Vector3[circleSegments + 1];
        
        if (enableDebug)
            Debug.Log($"CircleVisualizer: Caches initialized - Path: {maxPathPoints}, Circle: {circleSegments + 1}");
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
        
        // Performance-Optimierungen
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.allowOcclusionWhenDynamic = false;
    }
    
    void UpdatePath(List<Vector3> path, Vector3 normal)
    {
        if (path == null || path.Count == 0) 
        {
            ClearPath();
            return;
        }
        
        // Performance-Check
        if (path.Count == lastPathLength && Time.time - lastUpdateTime < 0.016f)
            return;
        
        lineRenderer.startColor = drawColor;
        lineRenderer.endColor = drawColor;
        
        int pointCount = Mathf.Min(path.Count, pathPositionsCache.Length);
        
        // Modus-abhängige Position-Berechnung
        for (int i = 0; i < pointCount; i++)
        {
            pathPositionsCache[i] = CalculateDrawPosition(path[i], normal);
        }
        
        lineRenderer.positionCount = pointCount;
        lineRenderer.SetPositions(pathPositionsCache);
        
        lastPathLength = path.Count;
        lastUpdateTime = Time.time;
        
        if (enableDebug && pointCount != path.Count)
            Debug.LogWarning($"Path truncated: {path.Count} -> {pointCount} points");
    }
    
    // Neue Methode: Berechnet Zeichenposition basierend auf Modus
    Vector3 CalculateDrawPosition(Vector3 spherePoint, Vector3 normal)
    {
        switch (drawMode)
        {
            case DrawMode.WorldSpace:
                return spherePoint + normal * worldSpaceHeight;
                
            case DrawMode.CursorProjection:
                return ProjectToCursorRay(spherePoint);
                
            default:
                return spherePoint + normal * worldSpaceHeight;
        }
    }
    
    // Neue Methode: Projiziert Punkt auf Cursor-Ray
    Vector3 ProjectToCursorRay(Vector3 spherePoint)
    {
        // Ray vom Cursor
        Ray cursorRay = cam.ScreenPointToRay(Input.mousePosition);
        
        // Finde nächsten Punkt auf Ray zu spherePoint
        Vector3 toSphere = spherePoint - cursorRay.origin;
        float projLength = Vector3.Dot(toSphere, cursorRay.direction);
        Vector3 projectedPoint = cursorRay.origin + cursorRay.direction * projLength;
        
        // Raycast auf Zeichenfläche
        if (Physics.Raycast(cursorRay, out RaycastHit hit, raycastDistance, drawingSurface))
        {
            return hit.point;
        }
        
        // Fallback: Projektion auf Z=0 Ebene
        Plane drawPlane = new Plane(Vector3.forward, Vector3.zero);
        if (drawPlane.Raycast(cursorRay, out float distance))
        {
            return cursorRay.GetPoint(distance);
        }
        
        // Letzter Fallback
        return projectedPoint;
    }
    
    void ShowConfirmation(Vector3 center, float radius, Vector3 normal)
    {
        if (confirmRoutine != null)
            StopCoroutine(confirmRoutine);
        
        confirmRoutine = StartCoroutine(ConfirmationEffect(center, radius, normal));
    }
    
    void CalculateTangentBasis(Vector3 normal)
    {
        if (Vector3.Dot(normal, lastNormal) > 0.999f) return;
        
        cachedTangent1 = Vector3.Cross(normal, Vector3.up);
        if (cachedTangent1.magnitude < 0.1f) 
            cachedTangent1 = Vector3.Cross(normal, Vector3.forward);
        cachedTangent1.Normalize();
        
        cachedTangent2 = Vector3.Cross(normal, cachedTangent1).normalized;
        lastNormal = normal;
        
        if (enableDebug)
            Debug.Log($"Tangent basis recalculated for normal: {normal}");
    }
    
    System.Collections.IEnumerator ConfirmationEffect(Vector3 center, float radius, Vector3 normal)
    {
        CalculateTangentBasis(normal);
        
        // Kreis-Punkte berechnen - berücksichtigt Modus
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            
            Vector3 sphereCirclePoint = center + (cachedTangent1 * cos + cachedTangent2 * sin) * radius;
            Vector3 drawPosition = CalculateDrawPosition(sphereCirclePoint, normal);
            
            circlePositionsCache[i] = drawPosition;
        }
        
        if (lineRenderer.startColor != confirmColor)
        {
            lineRenderer.startColor = confirmColor;
            lineRenderer.endColor = confirmColor;
        }
        
        lineRenderer.positionCount = circlePositionsCache.Length;
        lineRenderer.SetPositions(circlePositionsCache);
        
        if (enableDebug)
            Debug.Log($"Confirmation circle: {circleSegments} segments, radius: {radius:F2}, mode: {drawMode}");
        
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
            Debug.Log("Path cleared");
    }
    
    // Neue Utility-Methoden für Modus-Wechsel
    public void SetDrawMode(DrawMode mode)
    {
        drawMode = mode;
        if (enableDebug)
            Debug.Log($"Draw mode changed to: {mode}");
    }
    
    public void SetWorldSpaceHeight(float height)
    {
        worldSpaceHeight = height;
    }
    
    public void SetRaycastDistance(float distance)
    {
        raycastDistance = distance;
    }
    
    public void SetDrawingSurface(LayerMask mask)
    {
        drawingSurface = mask;
    }
    
    // Erweiterte Performance-Settings
    public void SetMaxPathPoints(int newMax)
    {
        if (newMax <= 0 || newMax == maxPathPoints) return;
        
        maxPathPoints = newMax;
        pathPositionsCache = new Vector3[maxPathPoints];
        
        if (enableDebug)
            Debug.Log($"Path cache resized to {maxPathPoints} points");
    }
    
    public void SetCircleSegments(int newSegments)
    {
        if (newSegments < 8 || newSegments == circleSegments) return;
        
        circleSegments = newSegments;
        circlePositionsCache = new Vector3[circleSegments + 1];
        
        if (enableDebug)
            Debug.Log($"Circle cache resized to {circleSegments + 1} points");
    }
    
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
            Debug.Log($"Settings updated - Width: {newWidth}, Mode: {drawMode}");
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!enableDebug) return;
        
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(320, 10, 250, 140));
        GUILayout.Label("CircleVisualizer Debug:");
        GUILayout.Label($"Draw Mode: {drawMode}");
        GUILayout.Label($"Path Cache: {pathPositionsCache?.Length ?? 0}");
        GUILayout.Label($"Circle Cache: {circlePositionsCache?.Length ?? 0}");
        GUILayout.Label($"Last Path Length: {lastPathLength}");
        GUILayout.Label($"LineRenderer Points: {lineRenderer.positionCount}");
        
        // Mode-Toggle Buttons
        if (GUILayout.Button($"Toggle Mode (Current: {drawMode})"))
        {
            drawMode = drawMode == DrawMode.WorldSpace ? DrawMode.CursorProjection : DrawMode.WorldSpace;
        }
        
        GUILayout.EndArea();
    }
    
    void OnDestroy()
    {
        if (confirmRoutine != null)
            StopCoroutine(confirmRoutine);
        
        pathPositionsCache = null;
        circlePositionsCache = null;
    }
}