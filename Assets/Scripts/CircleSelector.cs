using UnityEngine;
using System.Collections.Generic;

public class CircleSelector : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask sphereLayer = -1;
    [SerializeField] private LayerMask targetLayer = -1;
    [SerializeField] private float minRadius = 1f;
    [SerializeField] private int maxPathPoints = 50;
    [SerializeField] private Transform sphereCenter;
    
    private Camera cam;
    private List<Vector3> path = new List<Vector3>();
    private Vector3 tangentNormal;
    private Vector3 tangentCenter;
    private bool isDrawing;
    
    // Events
    public static event System.Action<Vector3, float, Vector3> OnCircleConfirmed; // center, radius, normal
    public static event System.Action<List<Vector3>, Vector3> OnPathUpdated; // path, normal
    public static event System.Action OnDrawingCancelled;
    public static event System.Action<CircleTarget, Vector3> OnTargetSelected; // target, center
    
    void Awake()
    {
        cam = Camera.main;
        if (sphereCenter == null)
            sphereCenter = transform.parent; // Fallback zu Parent
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
            StartDrawing();
        else if (Input.GetMouseButton(0) && isDrawing)
            UpdatePath();
        else if (Input.GetMouseButtonUp(0) && isDrawing)
            FinishDrawing();
    }
    
    void StartDrawing()
    {
        Vector3 spherePos = GetSpherePosition(out Vector3 normal);
        if (spherePos == Vector3.zero) return;
        
        isDrawing = true;
        tangentNormal = normal;
        tangentCenter = spherePos;
        path.Clear();
        path.Add(ProjectToTangentPlane(spherePos, spherePos));
    }
    
    void UpdatePath()
    {
        Vector3 spherePos = GetSpherePosition(out Vector3 normal);
        if (spherePos == Vector3.zero) return;
        
        Vector3 projectedPos = ProjectToTangentPlane(spherePos, tangentCenter);
        
        if (path.Count < maxPathPoints)
            path.Add(projectedPos);
        
        OnPathUpdated?.Invoke(path, tangentNormal);
    }
    
    void FinishDrawing()
    {
        if (path.Count < 3)
        {
            CancelDrawing();
            return;
        }
        
        Vector3 pathCenter = CalculateCenter();
        float radius = CalculateRadius(pathCenter);
        
        if (radius >= minRadius)
        {
            // Finde Target und nutze dessen Position als finales Center
            Vector3 finalCenter = FindBestTargetCenter(pathCenter, radius);
            OnCircleConfirmed?.Invoke(finalCenter, radius, tangentNormal);
            ProcessTargets(finalCenter, radius);
        }
        
        isDrawing = false;
        path.Clear();
    }
    
    void CancelDrawing()
    {
        isDrawing = false;
        path.Clear();
        OnDrawingCancelled?.Invoke();
    }
    
    Vector3 GetSpherePosition(out Vector3 normal)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sphereLayer))
        {
            normal = hit.normal;
            return hit.point;
        }
        
        normal = Vector3.up;
        return Vector3.zero;
    }
    
    Vector3 ProjectToTangentPlane(Vector3 worldPos, Vector3 planeCenter)
    {
        // Projiziere Position auf Tangentialebene
        Vector3 toPos = worldPos - planeCenter;
        Vector3 projected = toPos - Vector3.Dot(toPos, tangentNormal) * tangentNormal;
        return planeCenter + projected;
    }
    
    Vector3 CalculateCenter()
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 point in path)
            sum += point;
        return sum / path.Count;
    }
    
    float CalculateRadius(Vector3 center)
    {
        float maxDist = 0f;
        foreach (Vector3 point in path)
        {
            // 2D-Distanz in der Tangentialebene
            Vector3 toPoint = point - center;
            Vector3 projected = toPoint - Vector3.Dot(toPoint, tangentNormal) * tangentNormal;
            float dist = projected.magnitude;
            if (dist > maxDist) maxDist = dist;
        }
        return maxDist;
    }
    
    void ProcessTargets(Vector3 center, float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(center, radius, targetLayer);
        
        if (colliders.Length == 0)
        {
            Debug.Log($"No targets in circle - Center: {center}");
            return;
        }
        
        // Finde bestes Target
        CircleTarget bestTarget = null;
        float closestDist = float.MaxValue;
        int highestPriority = int.MinValue;
        
        foreach (var col in colliders)
        {
            var target = col.GetComponent<CircleTarget>();
            if (!target || !target.IsActive) continue;
            
            float dist = Vector3.Distance(center, col.transform.position);
            
            // Priorität zuerst, dann Distanz
            if (target.Priority > highestPriority || 
                (target.Priority == highestPriority && dist < closestDist))
            {
                highestPriority = target.Priority;
                closestDist = dist;
                bestTarget = target;
            }
        }
        
        if (bestTarget)
        {
            OnTargetSelected?.Invoke(bestTarget, center);
            bestTarget.SelectTarget(center);
        }
        else
        {
            Debug.Log($"No valid targets - using circle center: {center}");
        }
    }
    
    Vector3 FindBestTargetCenter(Vector3 pathCenter, float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(pathCenter, radius, targetLayer);
        
        if (colliders.Length == 0) return pathCenter;
        
        // Finde bestes Target
        CircleTarget bestTarget = null;
        float closestDist = float.MaxValue;
        int highestPriority = int.MinValue;
        
        foreach (var col in colliders)
        {
            var target = col.GetComponent<CircleTarget>();
            if (!target || !target.IsActive) continue;
            
            float dist = Vector3.Distance(pathCenter, col.transform.position);
            
            if (target.Priority > highestPriority || 
                (target.Priority == highestPriority && dist < closestDist))
            {
                highestPriority = target.Priority;
                closestDist = dist;
                bestTarget = target;
            }
        }
        
        if (bestTarget && sphereCenter)
        {
            // Raycast von Target zum Sphere-Center
            Vector3 targetPos = bestTarget.transform.position;
            Vector3 spherePos = sphereCenter.position;
            Vector3 direction = (spherePos - targetPos).normalized;
            
            // Finde Kontaktpunkt auf Sphere (ignoriere Targets)
            if (Physics.Raycast(targetPos, direction, out RaycastHit hit, Mathf.Infinity, sphereLayer))
            {
                // Neue Tangente am Kontaktpunkt
                tangentNormal = hit.normal;
                tangentCenter = hit.point;
                return hit.point;
            }
        }
        
        return pathCenter;
    }
}