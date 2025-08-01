using UnityEngine;
using System.Collections.Generic;

public class CircleSelector : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask sphereLayer = -1;
    [SerializeField] private LayerMask targetLayer = -1;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Drawing")]
    [SerializeField] private float maxDrawDistance = 10f;
    [SerializeField] private float minCircleRadius = 0.5f;
    [SerializeField] private int maxPathPoints = 200; // Fixed size für Performance
    
    [Header("Quality")]
    [SerializeField] private float qualityBonusMultiplier = 1.5f;
    [SerializeField] private float speedBonusMultiplier = 2f;
    [SerializeField] private float maxSpeedBonusTime = 1f;
    [SerializeField] private float closureThreshold = 0.4f;
    [SerializeField] private float excellentThreshold = 0.8f;
    [SerializeField] private float perfectCircleBonus = 2.0f;
    
    private Camera cam;
    private bool isDrawing;
    private float drawStartTime;
    
    // PERFORMANCE: Fixed arrays statt Lists
    private Vector3[] pathArray;
    private int pathCount;
    private float totalPathDistance;
    
    // PERFORMANCE: Tangent plane cache
    private Vector3 tangentNormal, tangentCenter;
    
    // PERFORMANCE: Mouse tracking für Smoothness
    private Vector3 lastMouseWorldPos;
    private bool hasLastMousePos;
    
    // Events
    public static event System.Action<Vector3, float, Vector3> OnCircleConfirmed;
    public static event System.Action<List<Vector3>, Vector3> OnPathUpdated;
    public static event System.Action OnDrawingCancelled;
    public static event System.Action<CircleTarget, Vector3, float> OnTargetSelected;
    
    // PERFORMANCE: Reusable list for events
    private List<Vector3> pathEventList;
    
    void Awake()
    {
        cam = Camera.main;
        if (!sphereCenter) sphereCenter = transform.parent;
        
        pathArray = new Vector3[maxPathPoints];
        pathEventList = new List<Vector3>(maxPathPoints);
    }
    
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) StartDrawing();
        else if (Input.GetMouseButton(0) && isDrawing) UpdateDrawing();
        else if (Input.GetMouseButtonUp(0) && isDrawing) FinishDrawing();
    }
    
    void StartDrawing()
    {
        if (!GetSphereHit(out Vector3 hitPoint, out Vector3 normal)) return;
        
        isDrawing = true;
        drawStartTime = Time.time;
        tangentNormal = normal;
        tangentCenter = hitPoint;
        totalPathDistance = 0f;
        pathCount = 0;
        hasLastMousePos = false;
        
        AddPoint(ProjectToTangentPlane(hitPoint));
        TriggerPathUpdate();
    }
    
    void UpdateDrawing()
    {
        if (!GetSphereHit(out Vector3 hitPoint, out Vector3 normal)) return;
        
        Vector3 currentWorldPos = ProjectToTangentPlane(hitPoint);
        
        // SMOOTHNESS: Immer hinzufügen, egal wie klein die Distanz
        // Performance: Direkte Array-Operation
        if (pathCount < maxPathPoints)
        {
            AddPoint(currentWorldPos);
            
            // PERFORMANCE: Distance-based removal nur wenn nötig
            if (totalPathDistance > maxDrawDistance)
            {
                RemoveOldestPoints();
            }
            
            TriggerPathUpdate();
        }
        
        lastMouseWorldPos = currentWorldPos;
        hasLastMousePos = true;
    }
    
    void AddPoint(Vector3 point)
    {
        if (pathCount > 0)
        {
            totalPathDistance += Vector3.Distance(point, pathArray[pathCount - 1]);
        }
        
        pathArray[pathCount] = point;
        pathCount++;
    }
    
    void RemoveOldestPoints()
    {
        int removeCount = pathCount / 4; // Entferne 25% der ältesten Punkte
        if (removeCount < 1) return;
        
        // PERFORMANCE: Array.Copy statt einzelne Shifts
        System.Array.Copy(pathArray, removeCount, pathArray, 0, pathCount - removeCount);
        pathCount -= removeCount;
        
        // Recalculate total distance
        totalPathDistance = 0f;
        for (int i = 1; i < pathCount; i++)
        {
            totalPathDistance += Vector3.Distance(pathArray[i], pathArray[i - 1]);
        }
    }
    
    void TriggerPathUpdate()
    {
        // PERFORMANCE: Reuse list, clear statt new
        pathEventList.Clear();
        for (int i = 0; i < pathCount; i++)
        {
            pathEventList.Add(pathArray[i]);
        }
        
        OnPathUpdated?.Invoke(pathEventList, tangentNormal);
    }
    
    void FinishDrawing()
    {
        if (pathCount < 3)
        {
            CancelDrawing();
            return;
        }
        
        Vector3 cursorCenter = GetCursorWorldPosition();
        float enclosedRadius = CalculateEnclosedRadius(cursorCenter);
        float quality = CalculateDrawQuality();
        float finalRadius = Mathf.Max(enclosedRadius * (1f + quality * qualityBonusMultiplier), minCircleRadius);
        
        float drawTime = Time.time - drawStartTime;
        float speedBonus = Mathf.Clamp01(maxSpeedBonusTime / drawTime) * speedBonusMultiplier;
        
        OnCircleConfirmed?.Invoke(cursorCenter, finalRadius, tangentNormal);
        ProcessTargetsInRadius(cursorCenter, finalRadius, speedBonus);
        
        ResetDrawing();
    }
    
    void CancelDrawing()
    {
        OnDrawingCancelled?.Invoke();
        ResetDrawing();
    }
    
    void ResetDrawing()
    {
        isDrawing = false;
        pathCount = 0;
        totalPathDistance = 0f;
        hasLastMousePos = false;
    }
    
    float CalculateEnclosedRadius(Vector3 center)
    {
        if (pathCount < 3) return minCircleRadius;
        
        float closureQuality = CalculateClosureQuality(center);
        if (closureQuality < closureThreshold) return minCircleRadius;
        
        float avgDistance = CalculateAverageDistance(center);
        float densityFactor = CalculatePathDensity(center, avgDistance);
        float baseRadius = avgDistance * densityFactor * closureQuality;
        
        if (closureQuality >= excellentThreshold)
        {
            return baseRadius * (1f + perfectCircleBonus * (closureQuality - excellentThreshold) / (1f - excellentThreshold));
        }
        else if (closureQuality >= 0.6f)
        {
            return baseRadius * (1f + qualityBonusMultiplier * 0.3f);
        }
        
        return baseRadius;
    }
    
    float CalculateAverageDistance(Vector3 center)
    {
        float totalDistance = 0f;
        for (int i = 0; i < pathCount; i++)
        {
            totalDistance += Vector3.Distance(pathArray[i], center);
        }
        return totalDistance / pathCount;
    }
    
    float CalculatePathDensity(Vector3 center, float avgDistance)
    {
        if (pathCount < 4) return 0.8f;
        
        float radiusVariance = 0f;
        for (int i = 0; i < pathCount; i++)
        {
            float diff = Vector3.Distance(pathArray[i], center) - avgDistance;
            radiusVariance += diff * diff;
        }
        
        radiusVariance = Mathf.Sqrt(radiusVariance / pathCount);
        float normalizedVariance = Mathf.Clamp01(radiusVariance / avgDistance);
        return Mathf.Lerp(1.2f, 0.6f, normalizedVariance);
    }
    
    float CalculateClosureQuality(Vector3 center)
    {
        if (pathCount < 4) return 0f;
        
        float startEndDistance = Vector3.Distance(pathArray[0], pathArray[pathCount - 1]);
        float avgDistanceToCenter = CalculateAverageDistance(center);
        float closureScore = Mathf.Clamp01(1f - (startEndDistance / (avgDistanceToCenter * 0.6f)));
        
        float angleSpread = CalculateAngleSpread(center);
        float angleScore = Mathf.Clamp01(angleSpread / 300f);
        
        return (closureScore * 0.6f + angleScore * 0.4f);
    }
    
    float CalculateAngleSpread(Vector3 center)
    {
        if (pathCount < 4) return 0f;
        
        Vector3 referenceDir = (pathArray[0] - center).normalized;
        float minAngle = 0f, maxAngle = 0f;
        
        for (int i = 1; i < pathCount; i++)
        {
            Vector3 currentDir = (pathArray[i] - center).normalized;
            float angle = Vector3.SignedAngle(referenceDir, currentDir, tangentNormal);
            
            if (angle < minAngle) minAngle = angle;
            if (angle > maxAngle) maxAngle = angle;
        }
        
        return maxAngle - minAngle;
    }
    
    float CalculateDrawQuality()
    {
        if (pathCount < 4) return 0f;
        
        Vector3 center = GetCursorWorldPosition();
        return CalculateClosureQuality(center);
    }
    
    Vector3 GetCursorWorldPosition()
    {
        if (GetSphereHit(out Vector3 hitPoint, out Vector3 normal))
        {
            return ProjectToTangentPlane(hitPoint);
        }
        return pathCount > 0 ? pathArray[pathCount - 1] : tangentCenter;
    }
    
    bool GetSphereHit(out Vector3 hitPoint, out Vector3 normal)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sphereLayer))
        {
            hitPoint = hit.point;
            normal = hit.normal;
            return true;
        }
        
        hitPoint = Vector3.zero;
        normal = Vector3.up;
        return false;
    }
    
    Vector3 ProjectToTangentPlane(Vector3 worldPos)
    {
        Vector3 toPos = worldPos - tangentCenter;
        Vector3 projected = toPos - Vector3.Dot(toPos, tangentNormal) * tangentNormal;
        return tangentCenter + projected;
    }
    
    void ProcessTargetsInRadius(Vector3 center, float radius, float speedBonus)
    {
        Collider[] targets = Physics.OverlapSphere(center, radius, targetLayer);
        if (targets.Length == 0) return;
        
        CircleTarget bestTarget = null;
        float closestDistance = float.MaxValue;
        int highestPriority = int.MinValue;
        
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i].GetComponent<CircleTarget>();
            if (!target?.IsActive == true) continue;
            
            float distance = Vector3.Distance(center, targets[i].transform.position);
            
            if (target.Priority > highestPriority || 
                (target.Priority == highestPriority && distance < closestDistance))
            {
                highestPriority = target.Priority;
                closestDistance = distance;
                bestTarget = target;
            }
        }
        
        if (bestTarget)
        {
            OnTargetSelected?.Invoke(bestTarget, center, speedBonus);
            bestTarget.SelectTarget(center, speedBonus);
        }
    }
}