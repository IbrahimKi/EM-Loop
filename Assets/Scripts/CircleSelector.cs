using UnityEngine;
using System.Collections.Generic;

public class CircleSelector : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask sphereLayer = -1;
    [SerializeField] private LayerMask targetLayer = -1;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Drawing Limits")]
    [SerializeField] private float maxDrawDistance = 10f;
    [SerializeField] private float minPointDistance = 0.1f;
    [SerializeField] private float minCircleRadius = 0.5f;
    
    [Header("Quality & Speed")]
    [SerializeField] private float qualityBonusMultiplier = 1.5f;
    [SerializeField] private float speedBonusMultiplier = 2f;
    [SerializeField] private float maxSpeedBonusTime = 1f;
    
    [Header("Circle Quality Balancing")]
    [SerializeField] private float closureThreshold = 0.4f;
    [SerializeField] private float excellentThreshold = 0.8f;
    [SerializeField] private float perfectCircleBonus = 2.0f;
    
    private Camera cam;
    private bool isDrawing;
    private float drawStartTime;
    
    // OPTIMIERUNG: Single path list with distance tracking
    private List<Vector3> activePath;
    private List<float> pathDistances;
    private float totalPathDistance;
    
    // OPTIMIERUNG: Tangent plane cache
    private Vector3 tangentNormal;
    private Vector3 tangentCenter;
    
    // OPTIMIERUNG: Raycast cache
    private Vector2 lastMousePos;
    private Vector3 lastSphereHit;
    private Vector3 lastSphereNormal;
    private int lastRaycastFrame;
    
    // Events
    public static event System.Action<Vector3, float, Vector3> OnCircleConfirmed;
    public static event System.Action<List<Vector3>, Vector3> OnPathUpdated;
    public static event System.Action OnDrawingCancelled;
    public static event System.Action<CircleTarget, Vector3, float> OnTargetSelected;
    
    void Awake()
    {
        cam = Camera.main;
        if (!sphereCenter) sphereCenter = transform.parent;
        
        activePath = new List<Vector3>(Mathf.RoundToInt(maxDrawDistance / minPointDistance));
        pathDistances = new List<float>(activePath.Capacity);
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
        
        activePath.Clear();
        pathDistances.Clear();
        
        activePath.Add(ProjectToTangentPlane(hitPoint));
        pathDistances.Add(0f);
        
        OnPathUpdated?.Invoke(activePath, tangentNormal);
    }
    
    void UpdateDrawing()
    {
        if (!GetSphereHit(out Vector3 hitPoint, out Vector3 normal)) return;
        
        Vector3 projectedPoint = ProjectToTangentPlane(hitPoint);
        
        // OPTIMIERUNG: Distance check before adding
        if (activePath.Count > 0)
        {
            float segmentDistance = Vector3.Distance(projectedPoint, activePath[activePath.Count - 1]);
            if (segmentDistance < minPointDistance) return;
            
            totalPathDistance += segmentDistance;
            
            // OPTIMIERUNG: Remove tail points if over limit
            while (totalPathDistance > maxDrawDistance && activePath.Count > 1)
            {
                RemoveOldestPoint();
            }
            
            activePath.Add(projectedPoint);
            pathDistances.Add(totalPathDistance);
        }
        
        OnPathUpdated?.Invoke(activePath, tangentNormal);
    }
    
    void FinishDrawing()
    {
        if (activePath.Count < 3)
        {
            CancelDrawing();
            return;
        }
        
        // NEUE BERECHNUNG: Kreis am Cursor-Ende basierend auf umschlossener Fläche
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
    
    // NEUE METHODE: Berechnet Radius basierend auf umschlossener Fläche
    float CalculateEnclosedRadius(Vector3 center)
    {
        if (activePath.Count < 3) return minCircleRadius;
        
        // Prüfe erst ob es wirklich eine geschlossene Form ist
        float closureQuality = CalculateClosureQuality(center);
        
        // QUALITY TIERS:
        if (closureQuality < closureThreshold) 
            return minCircleRadius; // Schlechte Formen = sehr klein
        
        // Berechne Basis-Radius
        float avgDistance = CalculateAverageDistance(center);
        float densityFactor = CalculatePathDensity(center, avgDistance);
        float baseRadius = avgDistance * densityFactor * closureQuality;
        
        // QUALITY MULTIPLIERS:
        if (closureQuality >= excellentThreshold)
        {
            // Excellent circles get bigger than drawn
            return baseRadius * (1f + perfectCircleBonus * (closureQuality - excellentThreshold) / (1f - excellentThreshold));
        }
        else if (closureQuality >= 0.6f)
        {
            // Good circles get small bonus
            return baseRadius * (1f + qualityBonusMultiplier * 0.3f);
        }
        else
        {
            // Medium circles stay normal size
            return baseRadius;
        }
    }
    
    // HELPER: Berechnet durchschnittliche Distanz zum Center
    float CalculateAverageDistance(Vector3 center)
    {
        float totalDistance = 0f;
        int validPoints = 0;
        
        for (int i = 0; i < activePath.Count; i++)
        {
            float distance = Vector3.Distance(activePath[i], center);
            
            if (distance <= maxDrawDistance * 0.8f)
            {
                totalDistance += distance;
                validPoints++;
            }
        }
        
        return validPoints > 0 ? totalDistance / validPoints : minCircleRadius;
    }
    
    // NEUE METHODE: Prüft ob der Pfad wirklich geschlossen/kreisförmig ist
    float CalculateClosureQuality(Vector3 center)
    {
        if (activePath.Count < 4) return 0f;
        
        // 1. Start-End Nähe prüfen
        float startEndDistance = Vector3.Distance(activePath[0], activePath[activePath.Count - 1]);
        float avgDistanceToCenter = 0f;
        
        for (int i = 0; i < activePath.Count; i++)
        {
            avgDistanceToCenter += Vector3.Distance(activePath[i], center);
        }
        avgDistanceToCenter /= activePath.Count;
        
        // Start-End sollten nah beieinander sein für echten Kreis
        float closureScore = Mathf.Clamp01(1f - (startEndDistance / (avgDistanceToCenter * 0.6f)));
        
        // 2. Winkelabdeckung prüfen - echter Kreis sollte ~360° abdecken
        float angleSpread = CalculateAngleSpread(center);
        float angleScore = Mathf.Clamp01(angleSpread / 300f); // 300° als "gut genug"
        
        // 3. Richtungsänderungen prüfen - Bögen haben wenig Richtungsänderung
        float directionChangeScore = CalculateDirectionChanges();
        
        // Kombiniere alle Faktoren
        return (closureScore * 0.4f + angleScore * 0.4f + directionChangeScore * 0.2f);
    }
    
    // NEUE METHODE: Berechnet Winkelabdeckung um Center
    float CalculateAngleSpread(Vector3 center)
    {
        if (activePath.Count < 4) return 0f;
        
        Vector3 referenceDir = (activePath[0] - center).normalized;
        float minAngle = 0f;
        float maxAngle = 0f;
        
        for (int i = 1; i < activePath.Count; i++)
        {
            Vector3 currentDir = (activePath[i] - center).normalized;
            float angle = Vector3.SignedAngle(referenceDir, currentDir, tangentNormal);
            
            if (angle < minAngle) minAngle = angle;
            if (angle > maxAngle) maxAngle = angle;
        }
        
        return maxAngle - minAngle;
    }
    
    // NEUE METHODE: Zählt signifikante Richtungsänderungen
    float CalculateDirectionChanges()
    {
        if (activePath.Count < 4) return 0f;
        
        int significantChanges = 0;
        float totalAngleChange = 0f;
        
        for (int i = 2; i < activePath.Count; i++)
        {
            Vector3 dir1 = (activePath[i-1] - activePath[i-2]).normalized;
            Vector3 dir2 = (activePath[i] - activePath[i-1]).normalized;
            
            if (dir1.sqrMagnitude > 0.01f && dir2.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.Angle(dir1, dir2);
                totalAngleChange += angle;
                
                if (angle > 30f) // Signifikante Richtungsänderung
                {
                    significantChanges++;
                }
            }
        }
        
        // Echter Kreis sollte viele kleine Richtungsänderungen haben
        float expectedChanges = activePath.Count * 0.7f;
        float changeRatio = Mathf.Clamp01(significantChanges / expectedChanges);
        
        // Auch Gesamtwinkeländerung berücksichtigen (sollte ~360° sein)
        float totalAngleScore = Mathf.Clamp01(totalAngleChange / 300f);
        
        return (changeRatio + totalAngleScore) * 0.5f;
    }
    
    // NEUE METHODE: Berechnet Pfad-Dichte um Center
    float CalculatePathDensity(Vector3 center, float avgDistance)
    {
        if (activePath.Count < 4) return 0.8f;
        
        float radiusVariance = 0f;
        int validPoints = 0;
        
        // Berechne Varianz der Distanzen zum Center
        for (int i = 0; i < activePath.Count; i++)
        {
            float distance = Vector3.Distance(activePath[i], center);
            if (distance <= maxDrawDistance * 0.8f)
            {
                float diff = distance - avgDistance;
                radiusVariance += diff * diff;
                validPoints++;
            }
        }
        
        if (validPoints <= 1) return 0.8f;
        
        radiusVariance = Mathf.Sqrt(radiusVariance / validPoints);
        
        // Niedrige Varianz = hohe Dichte = größerer effektiver Radius
        // Hohe Varianz = niedrige Dichte = kleinerer effektiver Radius
        float normalizedVariance = Mathf.Clamp01(radiusVariance / avgDistance);
        float densityFactor = Mathf.Lerp(1.2f, 0.6f, normalizedVariance);
        
        return densityFactor;
    }
    
    // NEUE METHODE: Aktuelle Cursor-Position in Weltkoordinaten
    Vector3 GetCursorWorldPosition()
    {
        if (GetSphereHit(out Vector3 hitPoint, out Vector3 normal))
        {
            return ProjectToTangentPlane(hitPoint);
        }
        
        // Fallback: Letzter Pfadpunkt
        return activePath.Count > 0 ? activePath[activePath.Count - 1] : tangentCenter;
    }
    
    void CancelDrawing()
    {
        OnDrawingCancelled?.Invoke();
        ResetDrawing();
    }
    
    void ResetDrawing()
    {
        isDrawing = false;
        activePath.Clear();
        pathDistances.Clear();
        totalPathDistance = 0f;
    }
    
    // OPTIMIERUNG: Remove oldest point and update distances
    void RemoveOldestPoint()
    {
        if (activePath.Count <= 1) return;
        
        float removedDistance = pathDistances[1]; // Distance to second point
        activePath.RemoveAt(0);
        pathDistances.RemoveAt(0);
        
        // Shift all distances
        for (int i = 0; i < pathDistances.Count; i++)
        {
            pathDistances[i] -= removedDistance;
        }
        
        totalPathDistance -= removedDistance;
    }
    
    // OPTIMIERUNG: Cached sphere raycast
    bool GetSphereHit(out Vector3 hitPoint, out Vector3 normal)
    {
        Vector2 mousePos = Input.mousePosition;
        
        // Cache hit if mouse barely moved
        if (Time.frameCount - lastRaycastFrame <= 2 && 
            Vector2.Distance(mousePos, lastMousePos) < 2f)
        {
            hitPoint = lastSphereHit;
            normal = lastSphereNormal;
            return lastSphereHit != Vector3.zero;
        }
        
        Ray ray = cam.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sphereLayer))
        {
            lastMousePos = mousePos;
            lastSphereHit = hit.point;
            lastSphereNormal = hit.normal;
            lastRaycastFrame = Time.frameCount;
            
            hitPoint = hit.point;
            normal = hit.normal;
            return true;
        }
        
        lastSphereHit = Vector3.zero;
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
    
    Vector3 CalculatePathCenter()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < activePath.Count; i++)
            sum += activePath[i];
        return sum / activePath.Count;
    }
    
    float CalculatePathRadius(Vector3 center)
    {
        float maxDist = 0f;
        for (int i = 0; i < activePath.Count; i++)
        {
            float dist = Vector3.Distance(activePath[i], center);
            if (dist > maxDist) maxDist = dist;
        }
        return maxDist;
    }
    
    float CalculateDrawQuality()
    {
        if (activePath.Count < 4) return 0f;
        
        Vector3 center = CalculatePathCenter();
        float avgRadius = 0f;
        
        // Calculate average radius
        for (int i = 0; i < activePath.Count; i++)
        {
            avgRadius += Vector3.Distance(activePath[i], center);
        }
        avgRadius /= activePath.Count;
        
        // Calculate circularity (radius variance)
        float radiusVariance = 0f;
        for (int i = 0; i < activePath.Count; i++)
        {
            float diff = Vector3.Distance(activePath[i], center) - avgRadius;
            radiusVariance += diff * diff;
        }
        radiusVariance = Mathf.Sqrt(radiusVariance / activePath.Count);
        
        // Calculate closeness (start/end proximity)
        float closeness = Vector3.Distance(activePath[0], activePath[activePath.Count - 1]);
        float maxExpectedDistance = avgRadius * 0.5f;
        float closenessFactor = Mathf.Clamp01(1f - closeness / maxExpectedDistance);
        
        // Calculate smoothness
        float smoothnessFactor = CalculatePathSmoothness(avgRadius);
        
        return (closenessFactor * 0.5f + smoothnessFactor * 0.3f + 
                Mathf.Clamp01(1f - radiusVariance / (avgRadius * 0.1f)) * 0.2f);
    }
    
    float CalculatePathSmoothness(float avgRadius)
    {
        if (activePath.Count < 4) return 1f;
        
        float totalAngleChange = 0f;
        int validSegments = 0;
        
        for (int i = 2; i < activePath.Count; i++)
        {
            Vector3 dir1 = (activePath[i-1] - activePath[i-2]).normalized;
            Vector3 dir2 = (activePath[i] - activePath[i-1]).normalized;
            
            if (dir1.sqrMagnitude > 0.01f && dir2.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.Angle(dir1, dir2);
                totalAngleChange += angle;
                validSegments++;
            }
        }
        
        if (validSegments == 0) return 1f;
        
        float avgAngleChange = totalAngleChange / validSegments;
        float expectedAngleChange = 360f / activePath.Count;
        
        return Mathf.Clamp01(1f - Mathf.Abs(avgAngleChange - expectedAngleChange) / 90f);
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