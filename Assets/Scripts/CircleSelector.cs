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
    
    [Header("Quality Bonus")]
    [SerializeField] private float qualityBonus = 0.5f;
    [SerializeField] private float closenessThreshold = 0.3f;
    [SerializeField] private float minRadiusModifier = 0.5f;  // Min Radius bei Quality 0
    [SerializeField] private float baseRadiusQuality = 0.6f;  // Quality für Base Radius
    [SerializeField] private float absoluteMinRadius = 0.5f;  // Absolute Mindestgröße
    
    [Header("Speed Bonus")]
    [SerializeField] private float speedBonusMultiplier = 2f;
    [SerializeField] private float maxSpeedBonusTime = 1f;  // Zeit für max Speed Bonus
    
    private Camera cam;
    private List<Vector3> spherePath = new List<Vector3>();  // Sphere-Positionen für Visuals
    private List<Vector3> rayHits = new List<Vector3>();     // Raycast-Hits für Target-Detection
    private Vector3 tangentNormal;
    private Vector3 tangentCenter;
    private bool isDrawing;
    private float drawStartTime;
    
    // Events
    public static event System.Action<Vector3, float, Vector3> OnCircleConfirmed;
    public static event System.Action<List<Vector3>, Vector3> OnPathUpdated;
    public static event System.Action OnDrawingCancelled;
    public static event System.Action<CircleTarget, Vector3, float> OnTargetSelected; // + speedBonus
    
    void Awake()
    {
        cam = Camera.main;
        if (sphereCenter == null)
            sphereCenter = transform.parent;
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
        drawStartTime = Time.time;
        tangentNormal = normal;
        tangentCenter = spherePos;
        spherePath.Clear();
        rayHits.Clear();
        
        spherePath.Add(ProjectToTangentPlane(spherePos, spherePos));
        rayHits.Add(GetRaycastHit());
    }
    
    void UpdatePath()
    {
        Vector3 spherePos = GetSpherePosition(out Vector3 normal);
        if (spherePos == Vector3.zero) return;
        
        Vector3 projectedPos = ProjectToTangentPlane(spherePos, tangentCenter);
        Vector3 rayHit = GetRaycastHit();
        
        if (spherePath.Count < maxPathPoints)
        {
            spherePath.Add(projectedPos);
            rayHits.Add(rayHit);
        }
        
        OnPathUpdated?.Invoke(spherePath, tangentNormal);
    }
    
    void FinishDrawing()
    {
        if (spherePath.Count < 3)
        {
            CancelDrawing();
            return;
        }
        
        // Quality-basierte Radius-Berechnung
        Vector3 spherePathCenter = CalculateCenter(spherePath);
        float baseRadius = CalculateRadius(spherePathCenter, spherePath);
        float quality = CalculateDrawingQuality();
        float radiusMultiplier = CalculateRadiusMultiplier(quality);
        float finalRadius = Mathf.Max(baseRadius * radiusMultiplier, absoluteMinRadius);
        
        // Speed Bonus berechnen
        float drawTime = Time.time - drawStartTime;
        float speedBonus = CalculateSpeedBonus(drawTime);
        
        // Raycast-basierte Target-Detection
        Vector3 rayCenter = CalculateCenter(rayHits);
        float rayRadius = Mathf.Max(CalculateRadius(rayCenter, rayHits) * radiusMultiplier, absoluteMinRadius);
        
        if (finalRadius >= absoluteMinRadius)
        {
            OnCircleConfirmed?.Invoke(spherePathCenter, finalRadius, tangentNormal);
            ProcessTargetsFromRaycast(rayCenter, rayRadius, speedBonus);
        }
        
        isDrawing = false;
        spherePath.Clear();
        rayHits.Clear();
    }
    
    void CancelDrawing()
    {
        isDrawing = false;
        spherePath.Clear();
        rayHits.Clear();
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
    
    Vector3 GetRaycastHit()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
        // Raycast durch alle Layer außer Sphere
        int layerMask = ~sphereLayer;
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            return hit.point;
        }
        
        // Fallback: Projektion auf Ebene bei Z=0
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        
        return Vector3.zero;
    }
    
    Vector3 ProjectToTangentPlane(Vector3 worldPos, Vector3 planeCenter)
    {
        Vector3 toPos = worldPos - planeCenter;
        Vector3 projected = toPos - Vector3.Dot(toPos, tangentNormal) * tangentNormal;
        return planeCenter + projected;
    }
    
    Vector3 CalculateCenter(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 point in points)
            sum += point;
        return sum / points.Count;
    }
    
    float CalculateRadius(Vector3 center, List<Vector3> points)
    {
        float maxDist = 0f;
        foreach (Vector3 point in points)
        {
            float dist = Vector3.Distance(point, center);
            if (dist > maxDist) maxDist = dist;
        }
        return maxDist;
    }
    
    float CalculateDrawingQuality()
    {
        if (spherePath.Count < 4) return 0f;
        
        Vector3 center = CalculateCenter(spherePath);
        float avgRadius = 0f;
        
        // Durchschnittsradius berechnen
        foreach (Vector3 point in spherePath)
        {
            avgRadius += Vector3.Distance(point, center);
        }
        avgRadius /= spherePath.Count;
        
        // Radiusvarianz für Kreisqualität (normalisiert)
        float radiusVariance = 0f;
        foreach (Vector3 point in spherePath)
        {
            float diff = Vector3.Distance(point, center) - avgRadius;
            radiusVariance += diff * diff;
        }
        radiusVariance = Mathf.Sqrt(radiusVariance / spherePath.Count);
        
        // Geschlossenheit (graduell statt threshold)
        float closeness = Vector3.Distance(spherePath[0], spherePath[spherePath.Count - 1]);
        float maxExpectedDistance = avgRadius * 0.5f; // Erwartet: halber Radius max
        float closenessFactor = Mathf.Clamp01(1f - closeness / maxExpectedDistance);
        
        // Kreisförmigkeit (graduell, basiert auf relativer Varianz)
        float expectedVariance = avgRadius * 0.1f; // 10% Varianz = akzeptabel
        float circularityScore = Mathf.Clamp01(1f - radiusVariance / expectedVariance);
        
        // Pfadglätte (weniger abrupte Richtungsänderungen = besser)
        float smoothnessFactor = CalculatePathSmoothness();
        
        // Gewichtete Kombination
        return (circularityScore * 0.4f + closenessFactor * 0.4f + smoothnessFactor * 0.2f);
    }
    
    float CalculatePathSmoothness()
    {
        if (spherePath.Count < 4) return 1f;
        
        float totalAngleChange = 0f;
        int validSegments = 0;
        
        for (int i = 2; i < spherePath.Count; i++)
        {
            Vector3 dir1 = (spherePath[i-1] - spherePath[i-2]).normalized;
            Vector3 dir2 = (spherePath[i] - spherePath[i-1]).normalized;
            
            if (dir1.magnitude > 0.1f && dir2.magnitude > 0.1f)
            {
                float angle = Vector3.Angle(dir1, dir2);
                totalAngleChange += angle;
                validSegments++;
            }
        }
        
        if (validSegments == 0) return 1f;
        
        float avgAngleChange = totalAngleChange / validSegments;
        float expectedAngleChange = 360f / spherePath.Count; // Perfekter Kreis
        
        return Mathf.Clamp01(1f - Mathf.Abs(avgAngleChange - expectedAngleChange) / 90f);
    }
    
    float CalculateRadiusMultiplier(float quality)
    {
        if (quality <= baseRadiusQuality)
        {
            // Von minRadiusModifier bis 1.0 (Base Radius)
            return minRadiusModifier + (1f - minRadiusModifier) * (quality / baseRadiusQuality);
        }
        else
        {
            // Von 1.0 bis 1.0 + qualityBonus
            float overQuality = (quality - baseRadiusQuality) / (1f - baseRadiusQuality);
            return 1f + qualityBonus * overQuality;
        }
    }
    
    void ProcessTargetsFromRaycast(Vector3 rayCenter, float rayRadius, float speedBonus)
    {
        // Finde Targets basierend auf Raycast-Bereich
        Collider[] colliders = Physics.OverlapSphere(rayCenter, rayRadius, targetLayer);
        
        if (colliders.Length == 0)
        {
            Debug.Log($"No targets in raycast circle - Center: {rayCenter}");
            return;
        }
        
        CircleTarget bestTarget = null;
        float closestDist = float.MaxValue;
        int highestPriority = int.MinValue;
        
        foreach (var col in colliders)
        {
            var target = col.GetComponent<CircleTarget>();
            if (!target || !target.IsActive) continue;
            
            float dist = Vector3.Distance(rayCenter, col.transform.position);
            
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
            OnTargetSelected?.Invoke(bestTarget, rayCenter, speedBonus);
            bestTarget.SelectTarget(rayCenter, speedBonus);
        }
    }
    
    float CalculateSpeedBonus(float drawTime)
    {
        // Schneller = mehr Bonus (umgekehrt proportional)
        float speedFactor = Mathf.Clamp01(maxSpeedBonusTime / drawTime);
        return speedBonusMultiplier * speedFactor;
    }
}