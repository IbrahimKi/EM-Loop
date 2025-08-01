using UnityEngine;
using System.Collections.Generic;

public class CircleSelector : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask sphereLayer = -1;
    [SerializeField] private LayerMask targetLayer = -1;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Drawing - Simple")]
    [SerializeField] private float minDistance = 0.03f; // Mindestabstand zwischen Punkten
    [SerializeField] private float maxDistance = 0.15f; // Ab hier wird interpoliert
    [SerializeField] private int maxPoints = 120; // Größer für mehr Puffer
    [SerializeField] private int cleanupStackSize = 20; // Lösche immer X Punkte auf einmal
    
    [Header("Line Length System - UPGRADEABLE")]
    [SerializeField] private float maxLineLength = 8f; // STAT: Maximale Linienlänge
    [SerializeField] private bool enableLengthLimit = true; // Toggle für Testing
    [SerializeField] private float fadeZoneLength = 2f; // Länge der Fade-Zone am Ende
    [SerializeField] private float fadeSpeed = 3f; // Geschwindigkeit des Fadings
    
    [Header("Circle Detection")]
    [SerializeField] private float minCircleRadius = 0.5f;
    [SerializeField] private float closureThreshold = 0.5f;
    [SerializeField] private float qualityBonusMultiplier = 1.5f;
    [SerializeField] private float speedBonusMultiplier = 2f;
    [SerializeField] private float maxSpeedBonusTime = 1f;
    
    [Header("Circle Size Balancing")]
    [SerializeField] private float baseRadiusMultiplier = 0.8f; // Basis-Multiplikator für schlechte Kreise
    [SerializeField] private float mediumQualityThreshold = 0.4f; // Ab hier wird's medium
    [SerializeField] private float goodQualityThreshold = 0.7f; // Ab hier wird's groß
    [SerializeField] private float excellentQualityThreshold = 0.85f; // Ab hier größer als gezeichnet
    [SerializeField] private float excellentBonusMultiplier = 1.8f; // Bonus für exzellente Kreise
    
    private Camera cam;
    private bool isDrawing;
    private float drawStartTime; // Für Speed Bonus
    
    // MINIMAL: Nur das Nötigste
    private Vector3[] points;
    private int pointCount;
    private Vector3 tangentNormal, tangentCenter;
    
    // INTERPOLATION STACK TRACKING
    private bool[] isInterpolated; // Markiert welche Punkte interpoliert sind
    private int cleanupThreshold; // Ab wann Cleanup starten
    
    // LINE LENGTH SYSTEM
    private float currentLineLength; // Aktuelle Gesamtlänge
    private float[] pointFadeFactors; // Fade-Werte pro Punkt (0 = unsichtbar, 1 = voll sichtbar)
    
    // Events - Nur die wichtigsten
    public static event System.Action<Vector3, float, Vector3> OnCircleConfirmed;
    public static event System.Action<Vector3[]> OnPathUpdated; // Direkt Array für Performance
    public static event System.Action OnDrawingCancelled;
    public static event System.Action<CircleTarget, Vector3, float> OnTargetSelected; // Für CircleTarget Kompatibilität
    
    void Awake()
    {
        cam = Camera.main;
        if (!sphereCenter) sphereCenter = transform.parent;
        points = new Vector3[maxPoints];
        isInterpolated = new bool[maxPoints];
        pointFadeFactors = new float[maxPoints];
        cleanupThreshold = maxPoints - cleanupStackSize; // Cleanup bei 80% voll
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
        drawStartTime = Time.time; // Speed Bonus Timer
        tangentNormal = normal;
        tangentCenter = hitPoint;
        pointCount = 0;
        
        AddPoint(ProjectToTangentPlane(hitPoint), false); // Erster Punkt nie interpoliert
        TriggerUpdate();
    }
    
    void UpdateDrawing()
    {
        if (!GetSphereHit(out Vector3 hitPoint, out Vector3 normal)) return;
        
        Vector3 worldPos = ProjectToTangentPlane(hitPoint);
        
        if (pointCount == 0)
        {
            AddPoint(worldPos, false);
            TriggerUpdate();
            return;
        }
        
        float distance = Vector3.Distance(worldPos, points[pointCount - 1]);
        
        // CONTINUOUS DRAWING: Immer Punkte hinzufügen, Cleanup läuft parallel
        bool pointAdded = false;
        
        // SIMPLE SMOOTHING: Bei großen Sprüngen einen Zwischenpunkt
        if (distance > maxDistance)
        {
            Vector3 midPoint = Vector3.Lerp(points[pointCount - 1], worldPos, 0.5f);
            if (AddPoint(midPoint, true)) pointAdded = true; // Interpolierter Punkt
            if (AddPoint(worldPos, false)) pointAdded = true; // Original Punkt
        }
        else if (distance > minDistance)
        {
            if (AddPoint(worldPos, false)) pointAdded = true; // Original Punkt
        }
        
        // CONTINUOUS FADE: Update Fade-Faktoren nach dem Hinzufügen
        if (enableLengthLimit && pointAdded)
        {
            CleanupByLength();
        }
        
        // STACK-BASED CLEANUP: Fallback bei zu vielen Punkten  
        if (pointCount >= cleanupThreshold)
        {
            CleanupInterpolationStack();
        }
        
        if (pointAdded)
        {
            TriggerUpdate();
        }
        
        // Kleinere Bewegungen ignorieren
    }
    
    bool AddPoint(Vector3 point, bool interpolated)
    {
        if (pointCount >= maxPoints)
        {
            // EMERGENCY CLEANUP: Mache Platz für neuen Punkt
            CleanupInterpolationStack();
            
            if (pointCount >= maxPoints)
            {
                Debug.LogWarning("Array still full after cleanup!");
                return false;
            }
        }
        
        // LINE LENGTH TRACKING
        if (pointCount > 0)
        {
            float segmentLength = Vector3.Distance(point, points[pointCount - 1]);
            currentLineLength += segmentLength;
        }
        
        points[pointCount] = point;
        isInterpolated[pointCount] = interpolated;
        pointFadeFactors[pointCount] = 1f; // Neue Punkte sind voll sichtbar
        pointCount++;
        return true;
    }
    
    void CleanupByLength()
    {
        // CONTINUOUS FADE: Fade ältere Punkte aus, blockiere nie das Zeichnen
        if (currentLineLength > maxLineLength)
        {
            float fadeStartLength = maxLineLength - fadeZoneLength;
            
            // Berechne Fade-Faktoren basierend auf Position in der Linie
            float runningLength = 0f;
            bool anyFaded = false;
            
            for (int i = 0; i < pointCount; i++)
            {
                if (i > 0)
                {
                    runningLength += Vector3.Distance(points[i], points[i - 1]);
                }
                
                // FADE LOGIC: Je weiter vom Ende, desto mehr Fade
                float distanceFromEnd = currentLineLength - runningLength;
                
                if (distanceFromEnd > maxLineLength)
                {
                    // Punkt ist außerhalb der erlaubten Länge -> Strong Fade
                    float excessDistance = distanceFromEnd - maxLineLength;
                    float fadeStrength = Mathf.Clamp01(excessDistance / fadeZoneLength);
                    float targetFade = 1f - fadeStrength;
                    
                    pointFadeFactors[i] = Mathf.MoveTowards(pointFadeFactors[i], targetFade, fadeSpeed * Time.deltaTime);
                    anyFaded = true;
                }
                else if (distanceFromEnd > maxLineLength - fadeZoneLength)
                {
                    // Punkt ist in Fade-Zone
                    float fadePosition = (maxLineLength - distanceFromEnd) / fadeZoneLength;
                    float targetFade = Mathf.Lerp(1f, 0.1f, fadePosition);
                    
                    pointFadeFactors[i] = Mathf.MoveTowards(pointFadeFactors[i], targetFade, fadeSpeed * Time.deltaTime);
                    anyFaded = true;
                }
                else
                {
                    // Punkt ist in sicherer Zone: Voll sichtbar
                    pointFadeFactors[i] = 1f;
                }
            }
            
            // GENTLE CLEANUP: Entferne nur stark gefadete Punkte
            if (anyFaded)
            {
                RemoveInvisiblePoints();
            }
        }
        else
        {
            // Alle Punkte voll sichtbar wenn unter Limit
            for (int i = 0; i < pointCount; i++)
            {
                pointFadeFactors[i] = 1f;
            }
        }
    }
    
    void RemoveInvisiblePoints()
    {
        // GENTLE CLEANUP: Entferne nur sehr stark gefadete Punkte (< 0.05f)
        int writeIndex = 0;
        
        for (int readIndex = 0; readIndex < pointCount; readIndex++)
        {
            if (pointFadeFactors[readIndex] > 0.05f) // Punkt noch ausreichend sichtbar
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
    
    void RemoveFirstPoint()
    {
        if (pointCount <= 1) return;
        
        // Verschiebe alle Punkte um 1 nach vorne
        for (int i = 0; i < pointCount - 1; i++)
        {
            points[i] = points[i + 1];
            isInterpolated[i] = isInterpolated[i + 1];
            pointFadeFactors[i] = pointFadeFactors[i + 1];
        }
        pointCount--;
    }
    
    void RecalculateLineLength()
    {
        currentLineLength = 0f;
        for (int i = 1; i < pointCount; i++)
        {
            currentLineLength += Vector3.Distance(points[i], points[i - 1]);
        }
    }
    
    void CleanupInterpolationStack()
    {
        // STACK CLEANUP: Entferne älteste Interpolations-Stacks
        int removeCount = cleanupStackSize;
        int removed = 0;
        
        // Finde zusammenhängende Interpolations-Blöcke vom Anfang
        for (int i = 0; i < pointCount && removed < removeCount; i++)
        {
            removed++;
        }
        
        // Verschiebe Rest nach vorne
        if (removed > 0)
        {
            for (int i = 0; i < pointCount - removed; i++)
            {
                points[i] = points[i + removed];
                isInterpolated[i] = isInterpolated[i + removed];
                pointFadeFactors[i] = pointFadeFactors[i + removed];
            }
            pointCount -= removed;
            RecalculateLineLength(); // Länge neu berechnen
        }
    }
    
    void TriggerUpdate()
    {
        // PERFORMANCE: Direkt Array senden, kein List-Copy
        OnPathUpdated?.Invoke(points);
    }
    
    void FinishDrawing()
    {
        if (pointCount < 3)
        {
            CancelDrawing();
            return;
        }
        
        Vector3 center = CalculateCenter();
        float drawnRadius = CalculateRadius(center); // Was tatsächlich gezeichnet wurde
        float quality = CalculateQuality(center);
        
        // FALLBACK: Immer mindestens Basis-Kreis erstellen
        if (!IsValidCircle(center, drawnRadius, quality))
        {
            // Fallback: Nutze letzten Mausposition als Center mit Basis-Radius
            Vector3 mousePos = Input.mousePosition;
            center = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, Camera.main.nearClipPlane));
            drawnRadius = minCircleRadius;
            quality = 0.2f; // Schlechte Qualität für Fallback
        }
        
        // CIRCLE SIZE BALANCING basierend auf Quality
        float finalRadius = CalculateFinalRadius(drawnRadius, quality);
        
        // Speed Bonus berechnen
        float drawTime = Time.time - drawStartTime;
        float speedBonus = Mathf.Clamp01(maxSpeedBonusTime / drawTime) * speedBonusMultiplier;
        
        // IMMER einen Kreis erstellen
        OnCircleConfirmed?.Invoke(center, finalRadius, tangentNormal);
        ProcessTargets(center, finalRadius, speedBonus);
        
        ResetDrawing();
    }
    
    float CalculateFinalRadius(float drawnRadius, float quality)
    {
        float finalRadius;
        
        if (quality >= excellentQualityThreshold)
        {
            // EXZELLENT: Größer als gezeichnet (1.8x)
            finalRadius = drawnRadius * excellentBonusMultiplier;
        }
        else if (quality >= goodQualityThreshold)
        {
            // GUT: Große Kreise (1.2x - 1.5x basierend auf Qualität)
            float bonusRange = Mathf.Lerp(1.2f, 1.5f, (quality - goodQualityThreshold) / (excellentQualityThreshold - goodQualityThreshold));
            finalRadius = drawnRadius * bonusRange;
        }
        else if (quality >= mediumQualityThreshold)
        {
            // MEDIUM: Hauptsächlich wie gezeichnet (0.9x - 1.1x)
            float bonusRange = Mathf.Lerp(0.9f, 1.1f, (quality - mediumQualityThreshold) / (goodQualityThreshold - mediumQualityThreshold));
            finalRadius = drawnRadius * bonusRange;
        }
        else
        {
            // SCHLECHT: Kleine Kreise (0.6x - 0.8x)
            float penalty = Mathf.Lerp(0.6f, baseRadiusMultiplier, quality / mediumQualityThreshold);
            finalRadius = drawnRadius * penalty;
        }
        
        // Mindestgröße garantieren
        return Mathf.Max(finalRadius, minCircleRadius);
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
        currentLineLength = 0f; // Reset Line Length
        
        // Reset alle Fade-Faktoren
        for (int i = 0; i < pointFadeFactors.Length; i++)
        {
            pointFadeFactors[i] = 1f;
        }
    }
    
    // SIMPLE CIRCLE DETECTION
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
    
    float CalculateQuality(Vector3 center)
    {
        if (pointCount < 4) return 0.3f; // Basis-Qualität für wenige Punkte
        
        // ERWEITERTE QUALITY: Closure + Rundheit
        
        // 1. Closure Quality: Start und End Punkt Distanz
        float startEndDistance = Vector3.Distance(points[0], points[pointCount - 1]);
        float avgRadius = CalculateRadius(center);
        float closureScore = Mathf.Clamp01(1f - (startEndDistance / (avgRadius * 0.8f)));
        
        // 2. Roundness Quality: Wie gleichmäßig sind die Abstände zum Center?
        float radiusVariance = 0f;
        for (int i = 0; i < pointCount; i++)
        {
            float distance = Vector3.Distance(points[i], center);
            float diff = distance - avgRadius;
            radiusVariance += diff * diff;
        }
        radiusVariance = Mathf.Sqrt(radiusVariance / pointCount);
        float roundnessScore = Mathf.Clamp01(1f - (radiusVariance / avgRadius));
        
        // 3. Coverage Quality: Wie viel vom Kreis ist abgedeckt?
        float angleSpread = CalculateAngleSpread(center);
        float coverageScore = Mathf.Clamp01(angleSpread / 270f); // 270° = gute Abdeckung
        
        // Kombiniere alle Scores
        return (closureScore * 0.4f + roundnessScore * 0.3f + coverageScore * 0.3f);
    }
    
    float CalculateAngleSpread(Vector3 center)
    {
        if (pointCount < 4) return 180f; // Fallback für wenige Punkte
        
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
    
    bool IsValidCircle(Vector3 center, float radius, float quality)
    {
        // LOCKERE VALIDATION: Erlaube mehr Kreise
        if (radius < minCircleRadius * 0.5f) return false; // Sehr lockere Radius-Check
        
        // Quality-basierte Closure Check - lockerer
        return quality >= (closureThreshold * 0.7f); // 70% des normalen Thresholds
    }
    
    void ProcessTargets(Vector3 center, float radius, float speedBonus)
    {
        Collider[] targets = Physics.OverlapSphere(center, radius, targetLayer);
        
        CircleTarget bestTarget = null;
        float closestDistance = float.MaxValue;
        int highestPriority = int.MinValue;
        
        foreach (var collider in targets)
        {
            var target = collider.GetComponent<CircleTarget>();
            if (target == null || !target.IsActive) continue;
            
            float distance = Vector3.Distance(center, collider.transform.position);
            
            // Priority-basierte Auswahl wie im Original
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
    
    // UTILITY METHODS
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
    
    // DEBUG & UPGRADE SYSTEM
    public int GetPointCount() => pointCount;
    public Vector3[] GetPoints() => points;
    public float[] GetPointFadeFactors() => pointFadeFactors; // Für CircleVisualizer
    public float GetCurrentLineLength() => currentLineLength;
    public float GetMaxLineLength() => maxLineLength;
    
    // UPGRADE SYSTEM - Called by external upgrade manager
    public void UpgradeMaxLineLength(float newMaxLength)
    {
        maxLineLength = newMaxLength;
        Debug.Log($"Line Length upgraded to: {maxLineLength:F1}");
    }
    
    public void SetMaxLineLength(float length) => maxLineLength = length;
    
    [ContextMenu("Debug Line Stats")]
    void DebugLineStats()
    {
        Debug.Log($"Points: {pointCount}/{maxPoints} | Length: {currentLineLength:F2}/{maxLineLength:F2}");
    }
}