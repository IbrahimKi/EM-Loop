using UnityEngine;

/// <summary>
/// Circle Analyzer - Erkennt Kreise und berechnet Quality
/// Generous aber fair, verhindert Line-Exploits
/// </summary>
public class CircleAnalyzer : MonoBehaviour
{
    [Header("Circle Detection")]
    [SerializeField] private float minCircleRadius = 0.5f;
    [SerializeField] private float maxCircleRadius = 5f;
    [SerializeField] private float closureThreshold = 0.5f; // Generous
    
    [Header("Quality Calculation")]
    [SerializeField] private float minCoverageAngle = 180f; // Mindestens halber Kreis
    [SerializeField] private AnimationCurve qualityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Line Prevention")]
    [SerializeField] private float lineDetectionThreshold = 0.7f; // Wie gerade darf es sein
    [SerializeField] private float minCircularity = 0.3f; // Wie rund muss es mindestens sein
    
    // Events
    public static event System.Action<Vector3, float, float> OnCircleDetected;
    public static event System.Action<string> OnDrawingRejected;
    
    void OnEnable()
    {
        DrawingInputHandler.OnDrawingCompleted += AnalyzeDrawing;
    }
    
    void OnDisable()
    {
        DrawingInputHandler.OnDrawingCompleted -= AnalyzeDrawing;
    }
    
    void AnalyzeDrawing(Vector3[] points)
    {
        if (points == null || points.Length < 3)
        {
            OnDrawingRejected?.Invoke("Too few points");
            return;
        }
        
        // Step 1: Check if it's a line
        if (IsLine(points))
        {
            OnDrawingRejected?.Invoke("Drawing is too linear");
            return;
        }
        
        // Step 2: Calculate center and radius
        Vector3 center = CalculateCenter(points);
        float avgRadius = CalculateAverageRadius(points, center);
        
        // Step 3: Validate radius
        if (avgRadius < minCircleRadius || avgRadius > maxCircleRadius)
        {
            OnDrawingRejected?.Invoke($"Circle too {(avgRadius < minCircleRadius ? "small" : "large")}");
            return;
        }
        
        // Step 4: Check closure
        float closureDistance = Vector3.Distance(points[0], points[points.Length - 1]);
        if (closureDistance > closureThreshold * avgRadius)
        {
            OnDrawingRejected?.Invoke("Circle not closed");
            return;
        }
        
        // Step 5: Check coverage
        float coverage = CalculateCoverage(points, center);
        if (coverage < minCoverageAngle)
        {
            OnDrawingRejected?.Invoke("Insufficient circle coverage");
            return;
        }
        
        // Step 6: Calculate quality
        float quality = CalculateQuality(points, center, avgRadius, closureDistance, coverage);
        
        // Success!
        OnCircleDetected?.Invoke(center, avgRadius, quality);
    }
    
    bool IsLine(Vector3[] points)
    {
        if (points.Length < 3) return true;
        
        // Get start and end
        Vector3 start = points[0];
        Vector3 end = points[points.Length - 1];
        Vector3 lineDirection = (end - start).normalized;
        
        // Count points that are close to the line
        int pointsOnLine = 0;
        float lineLength = Vector3.Distance(start, end);
        
        for (int i = 1; i < points.Length - 1; i++)
        {
            // Project point onto line
            Vector3 toPoint = points[i] - start;
            float projection = Vector3.Dot(toPoint, lineDirection);
            
            // Skip if outside line segment
            if (projection < 0 || projection > lineLength) continue;
            
            // Get closest point on line
            Vector3 closestOnLine = start + lineDirection * projection;
            float distanceToLine = Vector3.Distance(points[i], closestOnLine);
            
            // Check if point is close to line
            if (distanceToLine < lineLength * 0.1f) // 10% of line length
            {
                pointsOnLine++;
            }
        }
        
        // If most points are on the line, it's a line
        float lineRatio = (float)pointsOnLine / (points.Length - 2);
        return lineRatio > lineDetectionThreshold;
    }
    
    Vector3 CalculateCenter(Vector3[] points)
    {
        Vector3 sum = Vector3.zero;
        foreach (var point in points)
        {
            sum += point;
        }
        return sum / points.Length;
    }
    
    float CalculateAverageRadius(Vector3[] points, Vector3 center)
    {
        float sum = 0;
        foreach (var point in points)
        {
            sum += Vector3.Distance(point, center);
        }
        return sum / points.Length;
    }
    
    float CalculateCoverage(Vector3[] points, Vector3 center)
    {
        if (points.Length < 3) return 0;
        
        // Find min and max angles
        Vector3 reference = (points[0] - center).normalized;
        float minAngle = 0;
        float maxAngle = 0;
        
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 direction = (points[i] - center).normalized;
            float angle = Vector3.SignedAngle(reference, direction, Vector3.up);
            
            if (angle < minAngle) minAngle = angle;
            if (angle > maxAngle) maxAngle = angle;
        }
        
        return maxAngle - minAngle;
    }
    
    float CalculateQuality(Vector3[] points, Vector3 center, float avgRadius, float closureDistance, float coverage)
    {
        // 1. Closure quality (0-1)
        float closureQuality = 1f - Mathf.Clamp01(closureDistance / (avgRadius * 0.5f));
        
        // 2. Roundness quality (0-1)
        float radiusVariance = 0;
        foreach (var point in points)
        {
            float distance = Vector3.Distance(point, center);
            float diff = Mathf.Abs(distance - avgRadius);
            radiusVariance += diff;
        }
        radiusVariance /= points.Length;
        float roundnessQuality = 1f - Mathf.Clamp01(radiusVariance / avgRadius);
        
        // 3. Coverage quality (0-1)
        float coverageQuality = Mathf.Clamp01(coverage / 360f);
        
        // 4. Smoothness quality (0-1)
        float smoothness = CalculateSmoothness(points);
        
        // Combine qualities with generous weights
        float rawQuality = closureQuality * 0.3f +
                          roundnessQuality * 0.25f +
                          coverageQuality * 0.25f +
                          smoothness * 0.2f;
        
        // Apply curve for generous feel
        return qualityCurve.Evaluate(rawQuality);
    }
    
    float CalculateSmoothness(Vector3[] points)
    {
        if (points.Length < 3) return 0;
        
        float totalAngleChange = 0;
        
        for (int i = 1; i < points.Length - 1; i++)
        {
            Vector3 prev = points[i - 1];
            Vector3 curr = points[i];
            Vector3 next = points[i + 1];
            
            Vector3 dir1 = (curr - prev).normalized;
            Vector3 dir2 = (next - curr).normalized;
            
            float angle = Vector3.Angle(dir1, dir2);
            totalAngleChange += angle;
        }
        
        // Less angle change = smoother
        float avgAngleChange = totalAngleChange / (points.Length - 2);
        return 1f - Mathf.Clamp01(avgAngleChange / 45f); // 45 degrees as threshold
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        // Could visualize last analyzed circle here
    }
}