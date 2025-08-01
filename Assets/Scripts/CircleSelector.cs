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
    [SerializeField] private float minRadiusModifier = 0.5f;
    [SerializeField] private float baseRadiusQuality = 0.6f;
    [SerializeField] private float absoluteMinRadius = 0.5f;
    
    [Header("Speed Bonus")]
    [SerializeField] private float speedBonusMultiplier = 2f;
    [SerializeField] private float maxSpeedBonusTime = 1f;
    
    [Header("Raycast Cache Settings")]
    [SerializeField] private float cachePixelTolerance = 2f; // Screen-pixel Toleranz
    [SerializeField] private int maxCacheAge = 2; // Max Frames für Cache-Gültigkeit
    [SerializeField] private bool enableRaycastDebug = false;
    
    private Camera cam;
    
    // Array Pooling
    private Vector3[] spherePathPool;
    private Vector3[] rayHitsPool;
    private int pathLength = 0;
    
    private Vector3[] pathEventCache;
    private List<Vector3> pathEventList;
    
    private Vector3 tangentNormal;
    private Vector3 tangentCenter;
    private bool isDrawing;
    private float drawStartTime;
    
    // OPTIMIERUNG: Raycast Cache System
    private struct RaycastCache
    {
        public Vector2 screenPos;
        public Vector3 sphereHitPoint;
        public Vector3 sphereNormal;
        public Vector3 generalHitPoint;
        public int frameCount;
        public bool sphereHitValid;
        public bool generalHitValid;
        
        public bool IsValid(int currentFrame, int maxAge)
        {
            return currentFrame - frameCount <= maxAge;
        }
        
        public bool IsScreenPosMatch(Vector2 testPos, float tolerance)
        {
            return Vector2.Distance(screenPos, testPos) <= tolerance;
        }
    }
    
    private RaycastCache lastSphereRaycast;
    private RaycastCache lastGeneralRaycast;
    
    // Performance Stats
    private int raycastCacheHits = 0;
    private int raycastCacheMisses = 0;
    private int totalRaycastQueries = 0;
    
    // Events
    public static event System.Action<Vector3, float, Vector3> OnCircleConfirmed;
    public static event System.Action<List<Vector3>, Vector3> OnPathUpdated;
    public static event System.Action OnDrawingCancelled;
    public static event System.Action<CircleTarget, Vector3, float> OnTargetSelected;
    
    void Awake()
    {
        cam = Camera.main;
        if (sphereCenter == null)
            sphereCenter = transform.parent;
        
        InitializePools();
    }
    
    void InitializePools()
    {
        spherePathPool = new Vector3[maxPathPoints];
        rayHitsPool = new Vector3[maxPathPoints];
        pathEventCache = new Vector3[maxPathPoints];
        pathEventList = new List<Vector3>(maxPathPoints);
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
        Vector3 spherePos = GetSpherePositionCached(out Vector3 normal);
        if (spherePos == Vector3.zero) return;
        
        isDrawing = true;
        drawStartTime = Time.time;
        tangentNormal = normal;
        tangentCenter = spherePos;
        
        pathLength = 0;
        
        spherePathPool[0] = ProjectToTangentPlane(spherePos, spherePos);
        rayHitsPool[0] = GetRaycastHitCached();
        pathLength = 1;
    }
    
    void UpdatePath()
    {
        Vector3 spherePos = GetSpherePositionCached(out Vector3 normal);
        if (spherePos == Vector3.zero) return;
        
        Vector3 projectedPos = ProjectToTangentPlane(spherePos, tangentCenter);
        Vector3 rayHit = GetRaycastHitCached();
        
        if (pathLength < maxPathPoints)
        {
            spherePathPool[pathLength] = projectedPos;
            rayHitsPool[pathLength] = rayHit;
            pathLength++;
        }
        
        TriggerPathUpdatedEvent();
    }
    
    void TriggerPathUpdatedEvent()
    {
        pathEventList.Clear();
        for (int i = 0; i < pathLength; i++)
        {
            pathEventList.Add(spherePathPool[i]);
        }
        
        OnPathUpdated?.Invoke(pathEventList, tangentNormal);
    }
    
    void FinishDrawing()
    {
        if (pathLength < 3)
        {
            CancelDrawing();
            return;
        }
        
        Vector3 spherePathCenter = CalculateCenter(spherePathPool, pathLength);
        float baseRadius = CalculateRadius(spherePathCenter, spherePathPool, pathLength);
        float quality = CalculateDrawingQuality();
        float radiusMultiplier = CalculateRadiusMultiplier(quality);
        float finalRadius = Mathf.Max(baseRadius * radiusMultiplier, absoluteMinRadius);
        
        float drawTime = Time.time - drawStartTime;
        float speedBonus = CalculateSpeedBonus(drawTime);
        
        Vector3 rayCenter = CalculateCenter(rayHitsPool, pathLength);
        float rayRadius = Mathf.Max(CalculateRadius(rayCenter, rayHitsPool, pathLength) * radiusMultiplier, absoluteMinRadius);
        
        if (finalRadius >= absoluteMinRadius)
        {
            OnCircleConfirmed?.Invoke(spherePathCenter, finalRadius, tangentNormal);
            ProcessTargetsFromRaycast(rayCenter, rayRadius, speedBonus);
        }
        
        isDrawing = false;
        pathLength = 0;
    }
    
    void CancelDrawing()
    {
        isDrawing = false;
        pathLength = 0;
        OnDrawingCancelled?.Invoke();
    }
    
    // OPTIMIERUNG: Cached Sphere Raycast
    Vector3 GetSpherePositionCached(out Vector3 normal)
    {
        Vector2 mousePos = Input.mousePosition;
        int currentFrame = Time.frameCount;
        totalRaycastQueries++;
        
        // Cache-Hit Check
        if (lastSphereRaycast.IsValid(currentFrame, maxCacheAge) && 
            lastSphereRaycast.IsScreenPosMatch(mousePos, cachePixelTolerance))
        {
            raycastCacheHits++;
            normal = lastSphereRaycast.sphereNormal;
            
            if (enableRaycastDebug)
            {
                Debug.Log($"Sphere raycast cache HIT - Frame: {currentFrame}");
            }
            
            return lastSphereRaycast.sphereHitValid ? lastSphereRaycast.sphereHitPoint : Vector3.zero;
        }
        
        // Cache Miss - Neuer Raycast
        raycastCacheMisses++;
        Ray ray = cam.ScreenPointToRay(mousePos);
        
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sphereLayer))
        {
            // Cache Update
            lastSphereRaycast = new RaycastCache
            {
                screenPos = mousePos,
                sphereHitPoint = hit.point,
                sphereNormal = hit.normal,
                frameCount = currentFrame,
                sphereHitValid = true
            };
            
            normal = hit.normal;
            
            if (enableRaycastDebug)
            {
                Debug.Log($"Sphere raycast cache MISS - New hit at {hit.point}");
            }
            
            return hit.point;
        }
        
        // Kein Hit - Cache mit Invalid-Flag
        lastSphereRaycast = new RaycastCache
        {
            screenPos = mousePos,
            frameCount = currentFrame,
            sphereHitValid = false
        };
        
        normal = Vector3.up;
        return Vector3.zero;
    }
    
    // OPTIMIERUNG: Cached General Raycast
    Vector3 GetRaycastHitCached()
    {
        Vector2 mousePos = Input.mousePosition;
        int currentFrame = Time.frameCount;
        
        // Cache-Hit Check
        if (lastGeneralRaycast.IsValid(currentFrame, maxCacheAge) && 
            lastGeneralRaycast.IsScreenPosMatch(mousePos, cachePixelTolerance))
        {
            if (enableRaycastDebug)
            {
                Debug.Log($"General raycast cache HIT - Frame: {currentFrame}");
            }
            
            return lastGeneralRaycast.generalHitValid ? lastGeneralRaycast.generalHitPoint : Vector3.zero;
        }
        
        // Cache Miss - Neuer Raycast
        Ray ray = cam.ScreenPointToRay(mousePos);
        int layerMask = ~sphereLayer;
        
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            // Cache Update
            lastGeneralRaycast = new RaycastCache
            {
                screenPos = mousePos,
                generalHitPoint = hit.point,
                frameCount = currentFrame,
                generalHitValid = true
            };
            
            if (enableRaycastDebug)
            {
                Debug.Log($"General raycast cache MISS - New hit at {hit.point}");
            }
            
            return hit.point;
        }
        
        // Fallback: Projektion auf Z=0 Ebene
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 fallbackPoint = ray.GetPoint(distance);
            
            lastGeneralRaycast = new RaycastCache
            {
                screenPos = mousePos,
                generalHitPoint = fallbackPoint,
                frameCount = currentFrame,
                generalHitValid = true
            };
            
            return fallbackPoint;
        }
        
        // Total Fallback
        lastGeneralRaycast = new RaycastCache
        {
            screenPos = mousePos,
            frameCount = currentFrame,
            generalHitValid = false
        };
        
        return Vector3.zero;
    }
    
    Vector3 ProjectToTangentPlane(Vector3 worldPos, Vector3 planeCenter)
    {
        Vector3 toPos = worldPos - planeCenter;
        Vector3 projected = toPos - Vector3.Dot(toPos, tangentNormal) * tangentNormal;
        return planeCenter + projected;
    }
    
    Vector3 CalculateCenter(Vector3[] points, int length)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < length; i++)
            sum += points[i];
        return sum / length;
    }
    
    float CalculateRadius(Vector3 center, Vector3[] points, int length)
    {
        float maxDist = 0f;
        for (int i = 0; i < length; i++)
        {
            float dist = Vector3.Distance(points[i], center);
            if (dist > maxDist) maxDist = dist;
        }
        return maxDist;
    }
    
    float CalculateDrawingQuality()
    {
        if (pathLength < 4) return 0f;
        
        Vector3 center = CalculateCenter(spherePathPool, pathLength);
        float avgRadius = 0f;
        
        for (int i = 0; i < pathLength; i++)
        {
            avgRadius += Vector3.Distance(spherePathPool[i], center);
        }
        avgRadius /= pathLength;
        
        float radiusVariance = 0f;
        for (int i = 0; i < pathLength; i++)
        {
            float diff = Vector3.Distance(spherePathPool[i], center) - avgRadius;
            radiusVariance += diff * diff;
        }
        radiusVariance = Mathf.Sqrt(radiusVariance / pathLength);
        
        float closeness = Vector3.Distance(spherePathPool[0], spherePathPool[pathLength - 1]);
        float maxExpectedDistance = avgRadius * 0.5f;
        float closenessFactor = Mathf.Clamp01(1f - closeness / maxExpectedDistance);
        
        float expectedVariance = avgRadius * 0.1f;
        float circularityScore = Mathf.Clamp01(1f - radiusVariance / expectedVariance);
        
        float smoothnessFactor = CalculatePathSmoothness();
        
        return (circularityScore * 0.4f + closenessFactor * 0.4f + smoothnessFactor * 0.2f);
    }
    
    float CalculatePathSmoothness()
    {
        if (pathLength < 4) return 1f;
        
        float totalAngleChange = 0f;
        int validSegments = 0;
        
        for (int i = 2; i < pathLength; i++)
        {
            Vector3 dir1 = (spherePathPool[i-1] - spherePathPool[i-2]).normalized;
            Vector3 dir2 = (spherePathPool[i] - spherePathPool[i-1]).normalized;
            
            if (dir1.magnitude > 0.1f && dir2.magnitude > 0.1f)
            {
                float angle = Vector3.Angle(dir1, dir2);
                totalAngleChange += angle;
                validSegments++;
            }
        }
        
        if (validSegments == 0) return 1f;
        
        float avgAngleChange = totalAngleChange / validSegments;
        float expectedAngleChange = 360f / pathLength;
        
        return Mathf.Clamp01(1f - Mathf.Abs(avgAngleChange - expectedAngleChange) / 90f);
    }
    
    float CalculateRadiusMultiplier(float quality)
    {
        if (quality <= baseRadiusQuality)
        {
            return minRadiusModifier + (1f - minRadiusModifier) * (quality / baseRadiusQuality);
        }
        else
        {
            float overQuality = (quality - baseRadiusQuality) / (1f - baseRadiusQuality);
            return 1f + qualityBonus * overQuality;
        }
    }
    
    void ProcessTargetsFromRaycast(Vector3 rayCenter, float rayRadius, float speedBonus)
    {
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
        float speedFactor = Mathf.Clamp01(maxSpeedBonusTime / drawTime);
        return speedBonusMultiplier * speedFactor;
    }
    
    // OPTIMIERUNG: Cache-Statistiken und Management
    [ContextMenu("Clear Raycast Cache")]
    public void ClearRaycastCache()
    {
        lastSphereRaycast = default;
        lastGeneralRaycast = default;
        Debug.Log("Raycast cache cleared");
    }
    
    [ContextMenu("Print Cache Stats")]
    public void PrintCacheStats()
    {
        float hitRatio = totalRaycastQueries > 0 ? (float)raycastCacheHits / totalRaycastQueries * 100f : 0f;
        Debug.Log($"Raycast Cache Stats - Hits: {raycastCacheHits}, Misses: {raycastCacheMisses}, Hit Ratio: {hitRatio:F1}%");
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!enableRaycastDebug) return;
        
        GUI.color = Color.yellow;
        GUILayout.BeginArea(new Rect(10, 120, 300, 120));
        GUILayout.Label("Raycast Cache Debug:");
        GUILayout.Label($"Cache Hits: {raycastCacheHits}");
        GUILayout.Label($"Cache Misses: {raycastCacheMisses}");
        GUILayout.Label($"Total Queries: {totalRaycastQueries}");
        
        if (totalRaycastQueries > 0)
        {
            float hitRatio = (float)raycastCacheHits / totalRaycastQueries * 100f;
            GUILayout.Label($"Hit Ratio: {hitRatio:F1}%");
        }
        
        if (GUILayout.Button("Clear Cache"))
        {
            ClearRaycastCache();
        }
        GUILayout.EndArea();
    }
}