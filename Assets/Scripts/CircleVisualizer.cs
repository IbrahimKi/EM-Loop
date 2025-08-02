using UnityEngine;

/// <summary>
/// Simple Line Visualizer - Clean & Functional
/// Zeigt Drawing-Path und Confirmed Circles ohne Overengineering
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    [Header("Drawing Line")]
    [SerializeField] private Color drawingColor = Color.yellow;
    [SerializeField] private float drawingWidth = 0.05f;
    [SerializeField] private Material lineMaterial;
    
    [Header("Confirmed Circle")]
    [SerializeField] private Color circleColor = Color.green;
    [SerializeField] private float circleWidth = 0.08f;
    [SerializeField] private float circleDisplayTime = 2f;
    [SerializeField] private int circleSegments = 32;
    
    [Header("Fade Effects")]
    [SerializeField] private bool enableFade = true;
    [SerializeField] private float fadeMultiplier = 0.5f;
    
    [Header("References")]
    [SerializeField] private CircleManager circleManager; // FIXED: Correct type name
    [SerializeField] private Transform sphereCenter;
    
    // Components
    private LineRenderer drawingLine;
    private LineRenderer circleLineRenderer;
    private GameObject circleObject;
    
    // State
    private bool isShowingDrawing = false;
    private int circleHideId = -1;
    
    void Awake()
    {
        SetupComponents();
        
        if (!circleManager)
            circleManager = FindObjectOfType<CircleManager>(); // FIXED: Correct type name
        
        if (!sphereCenter)
            sphereCenter = transform.parent;
    }
    
    void OnEnable()
    {
        // Subscribe to GameEvents
        GameEvents.OnPathUpdated += ShowDrawingPath;
        GameEvents.OnCircleConfirmed += ShowConfirmedCircle;
        GameEvents.OnDrawingCancelled += HideDrawing;
    }
    
    void OnDisable()
    {
        // Unsubscribe from GameEvents
        GameEvents.OnPathUpdated -= ShowDrawingPath;
        GameEvents.OnCircleConfirmed -= ShowConfirmedCircle;
        GameEvents.OnDrawingCancelled -= HideDrawing;
        
        if (circleHideId >= 0)
            LeanTween.cancel(circleHideId);
    }
    
    void SetupComponents()
    {
        // Drawing Line Renderer
        drawingLine = GetComponent<LineRenderer>();
        SetupLineRenderer(drawingLine, drawingColor, drawingWidth);
        
        // Circle Line Renderer
        circleObject = new GameObject("Circle");
        circleObject.transform.SetParent(transform);
        circleLineRenderer = circleObject.AddComponent<LineRenderer>();
        SetupLineRenderer(circleLineRenderer, circleColor, circleWidth);
        circleLineRenderer.loop = true;
        
        // Initially hidden
        HideDrawing();
        HideCircle();
    }
    
    void SetupLineRenderer(LineRenderer lr, Color color, float width)
    {
        lr.material = lineMaterial ? lineMaterial : CreateSimpleMaterial();
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        
        // Simple settings
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View; // FIXED: View alignment, nicht TransformZ
    }
    
    Material CreateSimpleMaterial()
    {
        // Simple unlit material für bessere Performance
        var shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = Color.white;
        return mat;
    }
    
    void ShowDrawingPath(Vector3[] points)
    {
        if (!circleManager || points == null)
        {
            HideDrawing();
            return;
        }
        
        int pointCount = circleManager.GetPointCount();
        if (pointCount <= 1)
        {
            HideDrawing();
            return;
        }
        
        isShowingDrawing = true;
        
        // Simple path rendering
        drawingLine.positionCount = pointCount;
        
        // Get fade factors if available
        float[] fadeFactors = null;
        if (enableFade)
        {
            fadeFactors = circleManager.GetPointFadeFactors();
        }
        
        // Set positions and apply simple fade
        Vector3[] positions = new Vector3[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            positions[i] = points[i];
        }
        
        drawingLine.SetPositions(positions);
        
        // Simple fade effect durch Gradient
        if (enableFade && fadeFactors != null)
        {
            ApplySimpleFade(fadeFactors, pointCount);
        }
        else
        {
            drawingLine.startColor = drawingColor;
            drawingLine.endColor = drawingColor;
        }
    }
    
    void ApplySimpleFade(float[] fadeFactors, int pointCount)
    {
        // Einfacher Fade-Effekt: Start opaque, Ende transparent
        Color startColor = drawingColor;
        Color endColor = drawingColor;
        
        // Berechne durchschnittliche Fade am Ende
        float avgEndFade = 0f;
        int endSamples = Mathf.Min(5, pointCount);
        for (int i = pointCount - endSamples; i < pointCount; i++)
        {
            if (i >= 0 && i < fadeFactors.Length)
                avgEndFade += fadeFactors[i];
        }
        avgEndFade /= endSamples;
        
        endColor.a = avgEndFade * fadeMultiplier;
        
        drawingLine.startColor = startColor;
        drawingLine.endColor = endColor;
    }
    
    void ShowConfirmedCircle(Vector3 center, float radius, Vector3 normal)
    {
        // Hide drawing line
        HideDrawing();
        
        // Create circle on tangent plane
        CreateSimpleCircle(center, radius, normal);
        
        // Auto-hide after time
        if (circleHideId >= 0)
            LeanTween.cancel(circleHideId);
        
        circleHideId = LeanTween.delayedCall(circleDisplayTime, () => {
            HideCircle();
            circleHideId = -1;
        }).id;
    }
    
    void CreateSimpleCircle(Vector3 center, float radius, Vector3 normal)
    {
        Vector3[] circlePoints = new Vector3[circleSegments + 1];
        
        // Create tangent vectors for the plane
        Vector3 tangent1 = Vector3.Cross(normal, Vector3.up);
        if (tangent1.magnitude < 0.1f)
            tangent1 = Vector3.Cross(normal, Vector3.right);
        tangent1 = tangent1.normalized;
        
        Vector3 tangent2 = Vector3.Cross(normal, tangent1).normalized;
        
        // Create circle points in tangent plane
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            
            // Project to tangent plane
            circlePoints[i] = center + (tangent1 * x) + (tangent2 * y);
        }
        
        circleLineRenderer.positionCount = circlePoints.Length;
        circleLineRenderer.SetPositions(circlePoints);
        circleLineRenderer.startColor = circleColor;
        circleLineRenderer.endColor = circleColor;
        
        Debug.Log($"Simple circle created: Center={center}, Radius={radius:F2}");
    }
    
    void HideDrawing()
    {
        isShowingDrawing = false;
        if (drawingLine)
            drawingLine.positionCount = 0;
    }
    
    void HideCircle()
    {
        if (circleLineRenderer)
            circleLineRenderer.positionCount = 0;
    }
    
    // Public API
    public void SetDrawingColor(Color color)
    {
        drawingColor = color;
        if (drawingLine && isShowingDrawing)
            drawingLine.startColor = color;
    }
    
    public void SetCircleColor(Color color)
    {
        circleColor = color;
        if (circleLineRenderer)
        {
            circleLineRenderer.startColor = color;
            circleLineRenderer.endColor = color;
        }
    }
    
    public void SetDrawingWidth(float width)
    {
        drawingWidth = Mathf.Max(0.01f, width);
        if (drawingLine)
        {
            drawingLine.startWidth = drawingWidth;
            drawingLine.endWidth = drawingWidth;
        }
    }
    
    public void SetCircleWidth(float width)
    {
        circleWidth = Mathf.Max(0.01f, width);
        if (circleLineRenderer)
        {
            circleLineRenderer.startWidth = circleWidth;
            circleLineRenderer.endWidth = circleWidth;
        }
    }
    
    public void SetEnableFade(bool enabled)
    {
        enableFade = enabled;
    }
    
    // Getters
    public bool IsShowingDrawing => isShowingDrawing;
    public Color DrawingColor => drawingColor;
    public Color CircleColor => circleColor;
    
    #region Debug
    
    [ContextMenu("Test Simple Circle")]
    void TestSimpleCircle()
    {
        Vector3 testCenter = transform.position + Vector3.forward * 2f;
        Vector3 testNormal = Vector3.up;
        ShowConfirmedCircle(testCenter, 1.5f, testNormal);
    }
    
    [ContextMenu("Hide All")]
    void HideAll()
    {
        HideDrawing();
        HideCircle();
    }
    
    [ContextMenu("Toggle Fade")]
    void ToggleFade()
    {
        SetEnableFade(!enableFade);
        Debug.Log($"Simple Fade: {(enableFade ? "ON" : "OFF")}");
    }
    
    #endregion
}