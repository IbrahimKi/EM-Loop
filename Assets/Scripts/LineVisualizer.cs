using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

/// <summary>
/// Smooth Line Visualizer - Zeigt Drawing visuell an
/// Nutzt Job System für Performance
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LineVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private Color lineColor = Color.yellow;
    [SerializeField] private float hoverHeight = 0f; // No hover needed!
    [SerializeField] private AnimationCurve widthCurve = AnimationCurve.Linear(0, 1, 1, 1);
    [SerializeField] private int cornerVertices = 5; // Smooth corners
    [SerializeField] private int capVertices = 5; // Smooth ends
    
    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothingStrength = 0.3f;
    [SerializeField] private int smoothingIterations = 2;
    
    [Header("Fade")]
    [SerializeField] private bool enableEndFade = true;
    [SerializeField] private float fadeLength = 1f;
    
    [Header("Performance")]
    [SerializeField] private bool useJobSystem = true;
    
    // Components
    private LineRenderer lineRenderer;
    private Material lineMaterial;
    
    // State
    private Vector3[] currentPoints;
    private Vector3[] visualPoints;
    private bool isShowing;
    
    void Awake()
    {
        SetupLineRenderer();
    }
    
    void OnEnable()
    {
        DrawingInputHandler.OnPointsUpdated += UpdateVisualLine;
        DrawingInputHandler.OnDrawingCompleted += OnDrawingComplete;
        DrawingInputHandler.OnDrawingCancelled += HideLine;
    }
    
    void OnDisable()
    {
        DrawingInputHandler.OnPointsUpdated -= UpdateVisualLine;
        DrawingInputHandler.OnDrawingCompleted -= OnDrawingComplete;
        DrawingInputHandler.OnDrawingCancelled -= HideLine;
    }
    
    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        
        // Create simple unlit material
        var shader = Shader.Find("Sprites/Default");
        if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
        
        lineMaterial = new Material(shader);
        lineMaterial.color = lineColor;
        
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.widthCurve = widthCurve;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        
        // Smooth line settings
        lineRenderer.numCornerVertices = cornerVertices;
        lineRenderer.numCapVertices = capVertices;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        
        // Performance settings
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
    }
    
    void UpdateVisualLine(Vector3[] points)
    {
        if (points == null || points.Length < 2)
        {
            HideLine();
            return;
        }
        
        currentPoints = points;
        
        if (useJobSystem && points.Length > 10)
        {
            ProcessWithJobs(points);
        }
        else
        {
            ProcessSimple(points);
        }
        
        isShowing = true;
    }
    
    void ProcessSimple(Vector3[] points)
    {
        // For very smooth lines, we'll interpolate between points
        int totalPoints = points.Length;
        int interpolatedCount = totalPoints * 3; // 3x more points for smoothness
        
        if (visualPoints == null || visualPoints.Length != interpolatedCount)
        {
            visualPoints = new Vector3[interpolatedCount];
        }
        
        // First, copy original points with hover
        Vector3[] hoveredPoints = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            hoveredPoints[i] = points[i] + Vector3.up * hoverHeight;
        }
        
        // Apply smoothing to original points first
        if (enableSmoothing && points.Length > 3)
        {
            for (int iter = 0; iter < smoothingIterations; iter++)
            {
                SmoothPointsArray(ref hoveredPoints);
            }
        }
        
        // Now create interpolated curve
        int visualIndex = 0;
        for (int i = 0; i < hoveredPoints.Length - 1; i++)
        {
            Vector3 p0 = i > 0 ? hoveredPoints[i - 1] : hoveredPoints[i];
            Vector3 p1 = hoveredPoints[i];
            Vector3 p2 = hoveredPoints[i + 1];
            Vector3 p3 = i < hoveredPoints.Length - 2 ? hoveredPoints[i + 2] : hoveredPoints[i + 1];
            
            // Add interpolated points between p1 and p2
            for (int t = 0; t < 3; t++)
            {
                float factor = t / 3f;
                visualPoints[visualIndex++] = CatmullRom(p0, p1, p2, p3, factor);
            }
        }
        
        // Add last point
        visualPoints[visualIndex++] = hoveredPoints[hoveredPoints.Length - 1];
        
        // Update LineRenderer with exact count
        lineRenderer.positionCount = visualIndex;
        for (int i = 0; i < visualIndex; i++)
        {
            lineRenderer.SetPosition(i, visualPoints[i]);
        }
        
        // Apply fade
        if (enableEndFade)
        {
            ApplyFadeGradient();
        }
    }
    
    void SmoothPointsArray(ref Vector3[] pts)
    {
        Vector3[] smoothed = new Vector3[pts.Length];
        smoothed[0] = pts[0];
        smoothed[pts.Length - 1] = pts[pts.Length - 1];
        
        for (int i = 1; i < pts.Length - 1; i++)
        {
            Vector3 prev = pts[i - 1];
            Vector3 curr = pts[i];
            Vector3 next = pts[i + 1];
            
            Vector3 average = (prev + curr * 2f + next) / 4f;
            smoothed[i] = Vector3.Lerp(curr, average, smoothingStrength);
        }
        
        pts = smoothed;
    }
    
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
    
    void ProcessWithJobs(Vector3[] points)
    {
        // Native arrays for job
        NativeArray<float3> inputPoints = new NativeArray<float3>(points.Length, Allocator.TempJob);
        NativeArray<float3> outputPoints = new NativeArray<float3>(points.Length, Allocator.TempJob);
        
        // Convert to float3
        for (int i = 0; i < points.Length; i++)
        {
            inputPoints[i] = new float3(points[i].x, points[i].y, points[i].z);
        }
        
        // Create and schedule job
        var smoothJob = new SmoothLineJob
        {
            input = inputPoints,
            output = outputPoints,
            hoverHeight = hoverHeight,
            smoothingStrength = smoothingStrength
        };
        
        JobHandle handle = smoothJob.Schedule(points.Length, 64);
        handle.Complete();
        
        // Convert back and update visual points
        if (visualPoints == null || visualPoints.Length != points.Length)
        {
            visualPoints = new Vector3[points.Length];
        }
        
        for (int i = 0; i < points.Length; i++)
        {
            visualPoints[i] = new Vector3(outputPoints[i].x, outputPoints[i].y, outputPoints[i].z);
        }
        
        // Cleanup
        inputPoints.Dispose();
        outputPoints.Dispose();
        
        // Update LineRenderer
        lineRenderer.positionCount = visualPoints.Length;
        lineRenderer.SetPositions(visualPoints);
        
        if (enableEndFade)
        {
            ApplyFadeGradient();
        }
    }
    
    void SmoothPoints()
    {
        Vector3[] smoothed = new Vector3[visualPoints.Length];
        smoothed[0] = visualPoints[0];
        smoothed[visualPoints.Length - 1] = visualPoints[visualPoints.Length - 1];
        
        for (int i = 1; i < visualPoints.Length - 1; i++)
        {
            Vector3 prev = visualPoints[i - 1];
            Vector3 curr = visualPoints[i];
            Vector3 next = visualPoints[i + 1];
            
            Vector3 average = (prev + curr * 2f + next) / 4f;
            smoothed[i] = Vector3.Lerp(curr, average, smoothingStrength);
        }
        
        visualPoints = smoothed;
    }
    
    void ApplyFadeGradient()
    {
        if (visualPoints == null || visualPoints.Length < 2) return;
        
        // Calculate total length
        float totalLength = 0;
        for (int i = 1; i < visualPoints.Length; i++)
        {
            totalLength += Vector3.Distance(visualPoints[i], visualPoints[i - 1]);
        }
        
        // Create gradient
        Gradient gradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[2];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
        
        colorKeys[0] = new GradientColorKey(lineColor, 0);
        colorKeys[1] = new GradientColorKey(lineColor, 1);
        
        float fadeStart = 1f - (fadeLength / totalLength);
        alphaKeys[0] = new GradientAlphaKey(1f, 0);
        alphaKeys[1] = new GradientAlphaKey(1f, Mathf.Max(0, fadeStart));
        alphaKeys[2] = new GradientAlphaKey(0.2f, 1);
        
        gradient.SetKeys(colorKeys, alphaKeys);
        lineRenderer.colorGradient = gradient;
    }
    
    void OnDrawingComplete(Vector3[] points)
    {
        // Keep showing for a moment
        LeanTween.delayedCall(0.5f, HideLine);
    }
    
    void HideLine()
    {
        isShowing = false;
        lineRenderer.positionCount = 0;
    }
    
    // Job for smooth line processing
    [BurstCompile]
    struct SmoothLineJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> input;
        [WriteOnly] public NativeArray<float3> output;
        public float hoverHeight;
        public float smoothingStrength;
        
        public void Execute(int index)
        {
            float3 point = input[index];
            point.y += hoverHeight;
            
            // Simple smoothing in job
            if (index > 0 && index < input.Length - 1)
            {
                float3 prev = input[index - 1];
                float3 next = input[index + 1];
                prev.y += hoverHeight;
                next.y += hoverHeight;
                
                float3 average = (prev + point * 2f + next) / 4f;
                point = math.lerp(point, average, smoothingStrength);
            }
            
            output[index] = point;
        }
    }
    
    // Public API
    public void SetLineColor(Color color)
    {
        lineColor = color;
        if (lineMaterial) lineMaterial.color = color;
    }
    
    public void SetLineWidth(float width)
    {
        lineWidth = width;
        if (lineRenderer)
        {
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
        }
    }
    
    public void SetHoverHeight(float height) => hoverHeight = height;
    public bool IsShowing => isShowing;
}