using UnityEngine;

/// <summary>
/// Circle Detection System - Pure Logic, No Visuals
/// Unity 6 LTS optimiert - Event-basiert, Performance fokussiert
/// </summary>
public class CircleManager : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask sphereLayer = -1;
    [SerializeField] private LayerMask targetLayer = -1;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Drawing Parameters")]
    [SerializeField] private float minDistance = 0.03f;
    [SerializeField] private float maxDistance = 0.15f;
    [SerializeField] private int maxPoints = 120;
    [SerializeField] private int cleanupStackSize = 20;
    
    [Header("Line Length System")]
    [SerializeField] private float maxLineLength = 8f;
    [SerializeField] private bool enableLengthLimit = true;
    [SerializeField] private float fadeZoneLength = 2f;
    [SerializeField] private float fadeSpeed = 3f;
    
    [Header("Circle Quality System")]
    [SerializeField] private float minCircleRadius = 0.5f;
    [SerializeField] private float qualityBonusMultiplier = 1.5f;
    [SerializeField] private float speedBonusMultiplier = 2f;
    [SerializeField] private float maxSpeedBonusTime = 1f;
    [SerializeField] private bool enableQualityScaling = true;
    
    [Header("Quality Thresholds")]
    [SerializeField] private float poorQualityPenalty = 0.2f;
    [SerializeField] private float baseRadiusMultiplier = 0.3f;
    [SerializeField] private float mediumQualityThreshold = 0.4f;
    [SerializeField] private float goodQualityThreshold = 0.7f;
    [SerializeField] private float excellentQualityThreshold = 0.85f;
    [SerializeField] private float excellentBonusMultiplier = 1.5f;
    
    // Core State
    private Camera cam;
    private bool isDrawing;
    private float drawStartTime;
    
    // Point Data
    private Vector3[] points;
    private bool[] isInterpolated;
    private float[] pointFadeFactors;
    private int pointCount;
    private float currentLineLength;
    private int cleanupThreshold;
    
    // Circle Context
    private Vector3 tangentNormal, tangentCenter;
    
    // Last Confirmed Circle
    private float lastConfirmedRadius;
    private Vector3 lastConfirmedCenter;
    private float lastConfirmedQuality;
    
    // Events - Clean Interface
    public static event System.Action<Vector3, float, Vector3> OnCircleConfirmed;
    public static event System.Action<Vector3, float, float> OnCircleConfirmedWithQuality;
    public static event System.Action<Vector3[]> OnPathUpdated;
    public static event System.Action OnDrawingCancelled;
    public static event System.Action<CircleTarget, Vector3, float> OnTargetSelected;
    public static event System.Action<Vector3, float, int> OnAreaDamageDealt;
    
    void Awake()
    {
        InitializeComponents();
        InitializeArrays();
    }
    
    void InitializeComponents()
    {
        cam = Camera.main;
        if (!sphereCenter) sphereCenter = transform.parent;
    }
    
    void InitializeArrays()
    {
        points = new Vector3[maxPoints];
        isInterpolated = new bool[maxPoints];
        pointFadeFactors = new float[maxPoints];
        cleanupThreshold = maxPoints - cleanupStackSize;
        
        // Initialize fade factors
        for (int i = 0; i < maxPoints; i++)
        {
            pointFadeFactors[i] = 1f;
        }
    }
    
    void Update()
    {
        HandleInput();
    }
    
    #region Input Handling
    
    void HandleInput()
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
        pointCount = 0;
        currentLineLength = 0f;
        
        AddPoint(ProjectToTangentPlane(hitPoint), false);
        TriggerPathUpdate();
    }
    
    void UpdateDrawing()
    {
        if (!GetSphereHit(out Vector3 hitPoint, out Vector3 normal)) return;
        
        Vector3 worldPos = ProjectToTangentPlane(hitPoint);
        
        if (pointCount == 0)
        {
            AddPoint(worldPos, false);
            TriggerPathUpdate();
            return;
        }
        
        float distance = Vector3.Distance(worldPos, points[pointCount - 1]);
        bool pointAdded = false;
        
        if (distance > maxDistance)
        {
            Vector3 midPoint = Vector3.Lerp(points[pointCount - 1], worldPos, 0.5f);
            if (AddPoint(midPoint, true)) pointAdded = true;
            if (AddPoint(worldPos, false)) pointAdded = true;
        }
        else if (distance > minDistance)
        {
            if (AddPoint(worldPos, false)) pointAdded = true;
        }
        
        if (pointAdded)
        {
            if (enableLengthLimit) CleanupByLength();
            if (pointCount >= cleanupThreshold) CleanupInterpolationStack();
            TriggerPathUpdate();
        }
    }
    
    void FinishDrawing()
    {
        if (pointCount < 3)
        {
            CancelDrawing();
            return;
        }
        
        Vector3 center = CalculateCenter();
        float drawnRadius = CalculateRadius(center);
        float quality = CalculateAdvancedQuality(center);
        float finalRadius = CalculateFinalRadius(drawnRadius, quality);
        
        float drawTime = Time.time - drawStartTime;
        float speedBonus = Mathf.Clamp01(maxSpeedBonusTime / drawTime) * speedBonusMultiplier;
        
        lastConfirmedRadius = finalRadius;
        lastConfirmedCenter = center;
        lastConfirmedQuality = quality;
        
        OnCircleConfirmed?.Invoke(center, finalRadius, tangentNormal);
        OnCircleConfirmedWithQuality?.Invoke(center, finalRadius, quality);
        ProcessTargets(center, finalRadius, speedBonus);
        
        string qualityText = GetQualityText(quality);
        Debug.Log($"Circle: {qualityText} ({quality:F2}) | {drawnRadius:F2} → {finalRadius:F2} | Speed: {speedBonus:F2}");
        
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
        pointCount = 0;
        currentLineLength = 0f;
        
        System.Array.Fill(pointFadeFactors, 1f);
    }
    
    void TriggerPathUpdate()
    {
        OnPathUpdated?.Invoke(points);
    }
    
    #endregion
    
    #region Point Management
    
    bool AddPoint(Vector3 point, bool interpolated)
    {
        if (pointCount >= maxPoints)
        {
            CleanupInterpolationStack();
            if (pointCount >= maxPoints) return false;
        }
        
        if (pointCount > 0)
        {
            currentLineLength += Vector3.Distance(point, points[pointCount - 1]);
        }
        
        points[pointCount] = point;
        isInterpolated[pointCount] = interpolated;
        pointFadeFactors[pointCount] = 1f;
        pointCount++;
        
        return true;
    }
    
    void CleanupByLength()
    {
        if (currentLineLength <= maxLineLength)
        {
            for (int i = 0; i < pointCount; i++)
            {
                pointFadeFactors[i] = 1f;
            }
            return;
        }
        
        float runningLength = 0f;
        bool anyFaded = false;
        
        for (int i = 0; i < pointCount; i++)
        {
            if (i > 0)
            {
                runningLength += Vector3.Distance(points[i], points[i - 1]);
            }
            
            float distanceFromEnd = currentLineLength - runningLength;
            
            if (distanceFromEnd > maxLineLength)
            {
                float excessDistance = distanceFromEnd - maxLineLength;
                float fadeStrength = Mathf.Clamp01(excessDistance / fadeZoneLength);
                float targetFade = 1f - fadeStrength;
                
                pointFadeFactors[i] = Mathf.MoveTowards(pointFadeFactors[i], targetFade, fadeSpeed * Time.deltaTime);
                anyFaded = true;
            }
            else if (distanceFromEnd > maxLineLength - fadeZoneLength)
            {
                float fadePosition = (maxLineLength - distanceFromEnd) / fadeZoneLength;
                float targetFade = Mathf.Lerp(1f, 0.1f, fadePosition);
                
                pointFadeFactors[i] = Mathf.MoveTowards(pointFadeFactors[i], targetFade, fadeSpeed * Time.deltaTime);
                anyFaded = true;
            }
            else
            {
                pointFadeFactors[i] = 1f;
            }
        }
        
        if (anyFaded)
        {
            RemoveInvisiblePoints();
        }
    }
    
    void RemoveInvisiblePoints()
    {
        int writeIndex = 0;
        
        for (int readIndex = 0; readIndex < pointCount; readIndex++)
        {
            if (pointFadeFactors[readIndex] > 0.05f)
            {
                if (writeIndex != readIndex)
                {
                    points[writeIndex] = points[readIndex];
                    isInterpolated[writeIndex] = isInterpolated[readIndex];
                    pointFadeFactors[writeIndex] = pointFadeFactors[readIndex];
                }
                writeIndex++;
            }
        }
        
        if (writeIndex != pointCount)
        {
            pointCount = writeIndex;
            RecalculateLineLength();
        }
    }
    
    void CleanupInterpolationStack()
    {
        int removeCount = Mathf.Min(cleanupStackSize, pointCount);
        
        if (removeCount > 0)
        {
            for (int i = 0; i < pointCount - removeCount; i++)
            {
                points[i] = points[i + removeCount];
                isInterpolated[i] = isInterpolated[i + removeCount];
                pointFadeFactors[i] = pointFadeFactors[i + removeCount];
            }
            
            pointCount -= removeCount;
            RecalculateLineLength();
        }
    }
    
    void RecalculateLineLength()
    {
        currentLineLength = 0f;
        for (int i = 1; i < pointCount; i++)
        {
            currentLineLength += Vector3.Distance(points[i], points[i - 1]);
        }
    }
    
    #endregion
    
    #region Circle Mathematics
    
    Vector3 CalculateCenter()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < pointCount; i++)
        {
            sum += points[i];
        }
        return sum / pointCount;
    }
    
    float CalculateRadius(Vector3 center)
    {
        float sum = 0f;
        for (int i = 0; i < pointCount; i++)
        {
            sum += Vector3.Distance(points[i], center);
        }
        return sum / pointCount;
    }
    
    float CalculateAdvancedQuality(Vector3 center)
    {
        if (pointCount < 4) return 0.3f;
        
        float avgRadius = CalculateRadius(center);
        
        float startEndDistance = Vector3.Distance(points[0], points[pointCount - 1]);
        float closureScore = Mathf.Clamp01(1f - (startEndDistance / (avgRadius * 0.8f)));
        
        float radiusVariance = 0f;
        for (int i = 0; i < pointCount; i++)
        {
            float distance = Vector3.Distance(points[i], center);
            float diff = distance - avgRadius;
            radiusVariance += diff * diff;
        }
        radiusVariance = Mathf.Sqrt(radiusVariance / pointCount);
        float roundnessScore = Mathf.Clamp01(1f - (radiusVariance / avgRadius));
        
        float angleSpread = CalculateAngleSpread(center);
        float coverageScore = Mathf.Clamp01(angleSpread / 270f);
        
        return (closureScore * 0.4f + roundnessScore * 0.3f + coverageScore * 0.3f);
    }
    
    float CalculateAngleSpread(Vector3 center)
    {
        if (pointCount < 4) return 180f;
        
        Vector3 referenceDir = (points[0] - center).normalized;
        float minAngle = 0f, maxAngle = 0f;
        
        for (int i = 1; i < pointCount; i++)
        {
            Vector3 currentDir = (points[i] - center).normalized;
            float angle = Vector3.SignedAngle(referenceDir, currentDir, tangentNormal);
            
            if (angle < minAngle) minAngle = angle;
            if (angle > maxAngle) maxAngle = angle;
        }
        
        return maxAngle - minAngle;
    }
    
    float CalculateFinalRadius(float drawnRadius, float quality)
    {
        if (!enableQualityScaling)
        {
            return Mathf.Max(drawnRadius, minCircleRadius);
        }
        
        float multiplier;
        
        if (quality >= excellentQualityThreshold)
        {
            multiplier = excellentBonusMultiplier;
        }
        else if (quality >= goodQualityThreshold)
        {
            float t = (quality - goodQualityThreshold) / (excellentQualityThreshold - goodQualityThreshold);
            multiplier = Mathf.Lerp(1.0f, excellentBonusMultiplier, t);
        }
        else if (quality >= mediumQualityThreshold)
        {
            float t = (quality - mediumQualityThreshold) / (goodQualityThreshold - mediumQualityThreshold);
            multiplier = Mathf.Lerp(0.7f, 1.0f, t);
        }
        else
        {
            float t = quality / mediumQualityThreshold;
            multiplier = Mathf.Lerp(poorQualityPenalty, baseRadiusMultiplier, t);
            
            if (IsLinearPath())
            {
                multiplier *= 0.5f;
            }
        }
        
        return Mathf.Max(drawnRadius * multiplier, minCircleRadius);
    }
    
    string GetQualityText(float quality)
    {
        if (quality >= excellentQualityThreshold) return "EXCELLENT";
        if (quality >= goodQualityThreshold) return "GOOD";  
        if (quality >= mediumQualityThreshold) return "MEDIUM";
        return "POOR";
    }
    
    bool IsLinearPath()
    {
        if (pointCount < 6) return false;
        
        Vector3 start = points[0];
        Vector3 end = points[pointCount - 1];
        Vector3 lineDirection = (end - start).normalized;
        
        int linearPoints = 0;
        const float threshold = 0.5f;
        
        for (int i = 1; i < pointCount - 1; i++)
        {
            Vector3 toPoint = points[i] - start;
            Vector3 projectedPoint = start + Vector3.Project(toPoint, lineDirection);
            float distanceFromLine = Vector3.Distance(points[i], projectedPoint);
            
            if (distanceFromLine < threshold)
            {
                linearPoints++;
            }
        }
        
        float linearRatio = (float)linearPoints / (pointCount - 2);
        return linearRatio > 0.8f;
    }
    
    #endregion
    
    #region Target Processing
    
    void ProcessTargets(Vector3 center, float radius, float speedBonus)
    {
        Collider[] targets = Physics.OverlapSphere(center, radius, targetLayer);
        
        CircleTarget bestTarget = null;
        float closestDistance = float.MaxValue;
        int highestPriority = int.MinValue;
        
        foreach (var collider in targets)
        {
            var target = collider.GetComponent<CircleTarget>();
            if (target?.IsActive != true) continue;
            
            float distance = Vector3.Distance(center, collider.transform.position);
            
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
    
    #endregion
    
    #region Utility Methods
    
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
    
    #endregion
    
    #region Public API
    
    public int GetPointCount() => pointCount;
    public Vector3[] GetPoints() => points;
    public float[] GetPointFadeFactors() => pointFadeFactors;
    public float GetCurrentLineLength() => currentLineLength;
    public float GetMaxLineLength() => maxLineLength;
    public float GetLastRadius() => lastConfirmedRadius;
    public float GetLastQuality() => lastConfirmedQuality;
    public Vector3 GetLastCenter() => lastConfirmedCenter;
    public void SetMaxLineLength(float length) => maxLineLength = length;
    public void SetQualityScaling(bool enabled) => enableQualityScaling = enabled;
    public void SetPoorQualityPenalty(float penalty) => poorQualityPenalty = penalty;
    public void TriggerAreaDamageEvent(Vector3 position, float radius, int targetCount)
    {
        OnAreaDamageDealt?.Invoke(position, radius, targetCount);
    }
    public bool IsCurrentlyDrawing() => isDrawing;
    public bool HasValidCircle() => lastConfirmedRadius > 0f;
    
    #endregion
    
    #region Debug Tools
    
    [ContextMenu("Analyze Current Path")]
    void AnalyzeCurrentPath()
    {
        if (pointCount <= 0)
        {
            Debug.Log("No active path to analyze");
            return;
        }
        
        Vector3 center = CalculateCenter();
        float radius = CalculateRadius(center);
        float quality = CalculateAdvancedQuality(center);
        float finalRadius = CalculateFinalRadius(radius, quality);
        bool isLinear = IsLinearPath();
        string qualityText = GetQualityText(quality);
        
        Debug.Log($"Path Analysis:\n" +
                 $"Points: {pointCount} | Line Length: {currentLineLength:F2}/{maxLineLength:F2}\n" +
                 $"Drawn Radius: {radius:F2} | Final Radius: {finalRadius:F2}\n" +
                 $"Quality: {qualityText} ({quality:F2}) | Linear: {isLinear}");
    }
    
    [ContextMenu("Toggle Quality Scaling")]
    void ToggleQualityScaling()
    {
        enableQualityScaling = !enableQualityScaling;
        Debug.Log($"Quality Scaling: {(enableQualityScaling ? "ON" : "OFF")}");
    }
    
    [ContextMenu("Reset Circle Data")]
    void ResetCircleData()
    {
        lastConfirmedRadius = 0f;
        lastConfirmedCenter = Vector3.zero;
        lastConfirmedQuality = 0f;
        Debug.Log("Circle data reset");
    }
    
    #endregion
}