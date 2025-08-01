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
    
    [Header("Performance")]
    [SerializeField] private int maxCacheSize = 100;
    [SerializeField] private int circleSegments = 32;
    
    private LineRenderer lineRenderer;
    private Camera cam;
    private Coroutine confirmRoutine;
    
    // OPTIMIERUNG: Pre-allocated arrays
    private Vector3[] pathCache;
    private Vector3[] circleCache;
    
    // OPTIMIERUNG: Cached tangent basis
    private Vector3 tangent1, tangent2, lastNormal;
    
    void Awake()
    {
        SetupComponents();
        pathCache = new Vector3[maxCacheSize];
        circleCache = new Vector3[circleSegments + 1];
    }
    
    void SetupComponents()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.allowOcclusionWhenDynamic = false;
        lineRenderer.positionCount = 0;
        
        cam = Camera.main;
        if (!sphereCenter) sphereCenter = transform.parent;
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
    
    void UpdatePath(List<Vector3> path, Vector3 normal)
    {
        if (path == null || path.Count == 0) 
        { 
            ClearPath(); 
            return; 
        }
        
        lineRenderer.startColor = drawColor;
        lineRenderer.endColor = drawColor;
        
        int count = Mathf.Min(path.Count, maxCacheSize);
        
        // HYBRID: Historical points + current cursor position
        for (int i = 0; i < count; i++)
        {
            if (i == count - 1)
            {
                pathCache[i] = GetCursorPosition();
            }
            else
            {
                pathCache[i] = path[i] + normal * heightOffset;
            }
        }
        
        lineRenderer.positionCount = count;
        lineRenderer.SetPositions(pathCache);
    }
    
    // OPTIMIERUNG: Inline cursor projection
    Vector3 GetCursorPosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 100f)) return hit.point;
        
        // Z=0 plane fallback
        float t = -ray.origin.z / ray.direction.z;
        return ray.origin + ray.direction * t;
    }
    
    void ShowConfirmation(Vector3 center, float radius, Vector3 normal)
    {
        if (confirmRoutine != null) StopCoroutine(confirmRoutine);
        confirmRoutine = StartCoroutine(ConfirmationEffect(center, radius, normal));
    }
    
    System.Collections.IEnumerator ConfirmationEffect(Vector3 center, float radius, Vector3 normal)
    {
        UpdateTangentBasis(normal);
        
        // OPTIMIERUNG: Direct circle calculation
        float angleStep = Mathf.PI * 2f / circleSegments;
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * angleStep;
            circleCache[i] = center + 
                (tangent1 * Mathf.Cos(angle) + tangent2 * Mathf.Sin(angle)) * radius +
                normal * heightOffset;
        }
        
        lineRenderer.startColor = confirmColor;
        lineRenderer.endColor = confirmColor;
        lineRenderer.positionCount = circleSegments + 1;
        lineRenderer.SetPositions(circleCache);
        
        yield return new WaitForSeconds(1f);
        ClearPath();
    }
    
    // OPTIMIERUNG: Only recalculate if normal changed
    void UpdateTangentBasis(Vector3 normal)
    {
        if (Vector3.Dot(normal, lastNormal) > 0.999f) return;
        
        tangent1 = Vector3.Cross(normal, Vector3.up);
        if (tangent1.sqrMagnitude < 0.01f) tangent1 = Vector3.Cross(normal, Vector3.forward);
        tangent1.Normalize();
        
        tangent2 = Vector3.Cross(normal, tangent1);
        lastNormal = normal;
    }
    
    void ClearPath()
    {
        if (confirmRoutine != null)
        {
            StopCoroutine(confirmRoutine);
            confirmRoutine = null;
        }
        lineRenderer.positionCount = 0;
    }
    
    void OnDestroy()
    {
        if (confirmRoutine != null) StopCoroutine(confirmRoutine);
    }
}