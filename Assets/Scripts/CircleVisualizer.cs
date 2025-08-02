using UnityEngine;
using UnityEngine.Events;

using System;

[System.Serializable]
public class CircleEvent : UnityEvent<float> { }

// Alternative: C# Events für Unity 6
[System.Serializable]
public class CircleEventSystem
{
    public event Action<float> OnRadiusChanged;
    public void InvokeRadiusChanged(float radius) => OnRadiusChanged?.Invoke(radius);
}

public class OptimizedCircleVisualizer : MonoBehaviour
{
    [Header("Circle Settings")]
    [SerializeField] private float radius = 5f;
    [SerializeField] private int segments = 64;
    [SerializeField] private float lineWidth = 0.1f;
    
    [Header("Performance")]
    [SerializeField] private bool useObjectPool = true;
    [SerializeField] private bool cacheMesh = true;
    
    [Header("Events")]  
    public CircleEventSystem Events = new CircleEventSystem();
    
    // Components
    private LineRenderer lineRenderer;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    
    // Cache
    private Vector3[] cachedPoints;
    private Mesh cachedMesh;
    private float lastRadius;
    private int lastSegments;
    
    void Awake()
    {
        CacheReferences();
        InitializeCircle();
    }
    
    void CacheReferences()
    {
        // LineRenderer Component sicherstellen
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        // Mesh Components für alternative Darstellung
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        // LineRenderer Setup
        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            
            // Material zuweisen falls nicht vorhanden
            if (lineRenderer.material == null)
            {
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
    }
    
    void InitializeCircle()
    {
        cachedPoints = new Vector3[segments];
        UpdateCirclePoints();
        
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = segments;
            lineRenderer.SetPositions(cachedPoints);
        }
    }
    
    void UpdateCirclePoints()
    {
        if (cachedPoints == null || cachedPoints.Length != segments)
        {
            cachedPoints = new Vector3[segments];
        }
        
        float angleStep = 2f * Mathf.PI / segments;
        
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            cachedPoints[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );
        }
        
        lastRadius = radius;
        lastSegments = segments;
    }
    
    void Update()
    {
        // Nur updaten wenn sich was geändert hat
        if (radius != lastRadius || segments != lastSegments)
        {
            UpdateCircle();
        }
    }
    
    public void UpdateCircle()
    {
        UpdateCirclePoints();
        
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = segments;
            lineRenderer.SetPositions(cachedPoints);
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
        }
        
        Events.InvokeRadiusChanged(radius);
    }
    
    public void SetRadius(float newRadius)
    {
        radius = Mathf.Max(0.1f, newRadius);
        UpdateCircle();
    }
    
    public void SetSegments(int newSegments)
    {
        segments = Mathf.Clamp(newSegments, 8, 256);
        UpdateCircle();
    }
    
    public void SetLineWidth(float width)
    {
        lineWidth = Mathf.Max(0.01f, width);
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
        }
    }
    
    // Public Getters
    public float Radius => radius;
    public int Segments => segments;
    public Vector3[] Points => cachedPoints;
    
    void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        segments = Mathf.Clamp(segments, 8, 256);
        lineWidth = Mathf.Max(0.01f, lineWidth);
        
        if (Application.isPlaying)
        {
            UpdateCircle();
        }
    }
}