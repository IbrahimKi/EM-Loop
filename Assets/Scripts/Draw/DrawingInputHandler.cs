using UnityEngine;

/// <summary>
/// Simple Drawing Input Handler - Sammelt Punkte vom Mouse Input
/// Keine komplexe Logik, nur Input → World Points
/// </summary>
public class DrawingInputHandler : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask sphereLayer = -1;
    [SerializeField] private float sphereRadius = 10f;
    
    [Header("Point Collection")]
    [SerializeField] private float minPointDistance = 0.02f; // Smaller for more points
    [SerializeField] private float maxPointDistance = 0.2f; // Smaller for smoother curves
    [SerializeField] private int maxPoints = 500; // More points allowed
    
    [Header("References")]
    [SerializeField] private Transform sphereCenter;
    [SerializeField] private Camera drawingCamera;
    
    // Point Storage
    private Vector3[] points;
    private int pointCount;
    private bool isDrawing;
    
    // Events
    public static event System.Action<Vector3[]> OnPointsUpdated;
    public static event System.Action<Vector3[]> OnDrawingCompleted;
    public static event System.Action OnDrawingCancelled;
    
    void Awake()
    {
        points = new Vector3[maxPoints];
        drawingCamera = drawingCamera ? drawingCamera : Camera.main;
        
        if (!sphereCenter)
        {
            // Finde Sphere automatisch
            GameObject sphere = GameObject.FindWithTag("Sphere");
            if (sphere) sphereCenter = sphere.transform;
        }
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartDrawing();
        }
        else if (Input.GetMouseButton(0) && isDrawing)
        {
            UpdateDrawing();
        }
        else if (Input.GetMouseButtonUp(0) && isDrawing)
        {
            FinishDrawing();
        }
    }
    
    void StartDrawing()
    {
        // Get point on sphere
        Vector3 worldPoint = GetWorldPointOnSphere();
        if (worldPoint == Vector3.zero) return;
        
        // Start drawing
        isDrawing = true;
        pointCount = 0;
        
        // Add first point
        points[pointCount++] = worldPoint;
        
        // Notify listeners
        OnPointsUpdated?.Invoke(GetCurrentPoints());
    }
    
    void UpdateDrawing()
    {
        if (pointCount >= maxPoints) return;
        
        // Get current point
        Vector3 worldPoint = GetWorldPointOnSphere();
        if (worldPoint == Vector3.zero) return;
        
        // Check distance to last point
        float distance = Vector3.Distance(worldPoint, points[pointCount - 1]);
        
        // Add point if within distance range
        if (distance >= minPointDistance && distance <= maxPointDistance)
        {
            points[pointCount++] = worldPoint;
            
            // Notify listeners
            OnPointsUpdated?.Invoke(GetCurrentPoints());
        }
        else if (distance > maxPointDistance)
        {
            // Add interpolated point if too far
            Vector3 midPoint = Vector3.Lerp(points[pointCount - 1], worldPoint, 0.5f);
            midPoint = ProjectToSphere(midPoint);
            
            if (pointCount < maxPoints)
            {
                points[pointCount++] = midPoint;
            }
            
            if (pointCount < maxPoints)
            {
                points[pointCount++] = worldPoint;
            }
            
            OnPointsUpdated?.Invoke(GetCurrentPoints());
        }
    }
    
    void FinishDrawing()
    {
        if (pointCount < 3)
        {
            CancelDrawing();
            return;
        }
        
        isDrawing = false;
        
        // Notify completion
        OnDrawingCompleted?.Invoke(GetCurrentPoints());
    }
    
    void CancelDrawing()
    {
        isDrawing = false;
        pointCount = 0;
        OnDrawingCancelled?.Invoke();
    }
    
    Vector3 GetWorldPointOnSphere()
    {
        Ray ray = drawingCamera.ScreenPointToRay(Input.mousePosition);
        
        // Simple raycast to drawing collider
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sphereLayer))
        {
            return hit.point;
        }
        
        return Vector3.zero;
    }
    
    Vector3 ProjectToSphere(Vector3 point)
    {
        if (!sphereCenter) return point;
        
        Vector3 direction = (point - sphereCenter.position).normalized;
        return sphereCenter.position + direction * sphereRadius;
    }
    
    Vector3[] GetCurrentPoints()
    {
        Vector3[] currentPoints = new Vector3[pointCount];
        System.Array.Copy(points, currentPoints, pointCount);
        return currentPoints;
    }
    
    // Public API
    public bool IsDrawing => isDrawing;
    public int PointCount => pointCount;
    public Vector3[] Points => GetCurrentPoints();
    public float GetTotalLength()
    {
        float length = 0;
        for (int i = 1; i < pointCount; i++)
        {
            length += Vector3.Distance(points[i], points[i - 1]);
        }
        return length;
    }
    
    // Test method for debugging
    public void SimulateDrawing(Vector3[] testPoints)
    {
        if (testPoints == null || testPoints.Length < 3) return;
        
        // Simulate drawing these points
        pointCount = Mathf.Min(testPoints.Length, maxPoints);
        System.Array.Copy(testPoints, points, pointCount);
        
        OnPointsUpdated?.Invoke(GetCurrentPoints());
        OnDrawingCompleted?.Invoke(GetCurrentPoints());
    }
}