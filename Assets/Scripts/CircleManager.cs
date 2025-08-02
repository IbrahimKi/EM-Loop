using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Circle Manager - Fixed Visual Offset & Circle Positioning
/// Unity 6 LTS - Kamera-ausgerichteter Offset + intelligente Kreispositionierung
/// </summary>
public class CircleManager : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask sphereLayer = -1;
    [SerializeField] private LayerMask targetLayer = -1;
    [SerializeField] private LayerMask damageableLayer = -1;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Drawing")]
    [SerializeField] private float minDistance = 0.03f;
    [SerializeField] private float maxDistance = 0.15f;
    [SerializeField] private int maxPoints = 120;
    [SerializeField] private int cleanupStackSize = 20;
    
    [Header("Line Length System")]
    [SerializeField] private float maxLineLength = 8f;
    [SerializeField] private bool enableLengthLimit = true;
    [SerializeField] private float fadeZoneLength = 2f;
    [SerializeField] private float fadeSpeed = 3f;
    
    [Header("Screen Space Drawing")]
    [SerializeField] private bool useScreenSpaceCalculation = true;
    [SerializeField] private float screenToWorldScale = 0.01f;
    [SerializeField] private float drawingPlaneDistance = 10f;
    [SerializeField] private bool projectVisualsToSurface = true;
    [SerializeField] private float surfaceOffset = 0.1f;
    [SerializeField] private bool useCameraAlignedOffset = true; // NEW: Kamera-orientierter Offset
    [SerializeField] private bool enableCatmullRom = false;
    [SerializeField] private float curveTension = 0.5f;
    [SerializeField] private int smoothingSegments = 3;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useAnimationCurve = false;
    
    [Header("Performance Settings")]
    [SerializeField] private bool enableUpdateThrottling = true;
    [SerializeField] private float updateInterval = 0.033f;
    
    [Header("Circle Detection")]
    [SerializeField] private float minCircleRadius = 0.5f;
    [SerializeField] private float closureThreshold = 0.1f;
    [SerializeField] private float minClosureDistance = 3f;
    [SerializeField] private float qualityBonusMultiplier = 1.5f;
    [SerializeField] private float speedBonusMultiplier = 2f;
    [SerializeField] private float maxSpeedBonusTime = 1f;
    [SerializeField] private bool enableQualityScaling = true;
    
    [Header("Circle Quality Balancing")]
    [SerializeField] private float baseRadiusMultiplier = 0.3f;
    [SerializeField] private float mediumQualityThreshold = 0.4f;
    [SerializeField] private float goodQualityThreshold = 0.7f;
    [SerializeField] private float excellentQualityThreshold = 0.85f;
    [SerializeField] private float excellentBonusMultiplier = 1.5f;
    [SerializeField] private float poorQualityPenalty = 0.2f;
    
    [Header("Circle Positioning")]
    [SerializeField] private float linearPathThreshold = 0.6f; // Schwelle für lineare Pfade
    [SerializeField] private bool useIntelligentCirclePositioning = true; // NEW: Intelligente Positionierung
    
    [Header("Area Effect Settings")]
    [SerializeField] private int circleSegments = 32;
    
    [Header("FIXED: Improved Fade Balancing")]
    [SerializeField] private float minFadeThreshold = 0.15f; // Punkt-Removal Schwelle
    [SerializeField] private float fadeMinAlpha = 0.3f; // Minimum Alpha für Fade
    [SerializeField] private float fadeZoneMinAlpha = 0.5f; // Minimum Alpha in Fade-Zone
    [SerializeField] private int maxPointsToRemovePerFrame = 5; // Max Points pro Frame entfernen
    [SerializeField] private bool enableSmartFading = true; // Intelligentes Fading
    
    [Header("Advanced Features")]
    [SerializeField] private bool enableAdaptiveInterpolation = false;
    [SerializeField] private float speedThreshold = 5f;
    [SerializeField] private int maxInterpolationSteps = 4;
    [SerializeField] private bool enableSphereAwareCurves = false;
    [SerializeField] private float sphereRadius = 10f;
    
    // Private Variables
    private Camera cam;
    private bool isDrawing;
    private float drawStartTime;
    
    // Point Arrays
    private Vector3[] points;
    private Vector3[] visualPoints;
    private bool[] isInterpolated;
    private float[] pointFadeFactors;
    private float[] pointArcLengths;
    private int pointCount;
    private float currentLineLength;
    private int cleanupThreshold;
    
    // Screen Space Drawing
    private Vector2 screenStartPos;
    private Vector3 calculationCenter;
    private Plane drawingPlane;
    
    // Surface Projection
    private Vector3 tangentNormal;
    private Vector3 tangentCenter;
    
    // Performance
    private float lastUpdateTime;
    
    // Circle Data
    private float lastConfirmedRadius;
    private Vector3 lastConfirmedCenter;
    private float lastConfirmedQuality;
    
    void Awake()
    {
        cam = Camera.main;
        if (!sphereCenter) sphereCenter = transform.parent;
        
        points = new Vector3[maxPoints];
        visualPoints = new Vector3[maxPoints];
        isInterpolated = new bool[maxPoints];
        pointFadeFactors = new float[maxPoints];
        pointArcLengths = new float[maxPoints];
        cleanupThreshold = maxPoints - cleanupStackSize;
    }
    
    void Update()
    {
        HandleInput();
        
        if (enableUpdateThrottling && isDrawing && pointCount > 1 && 
            Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            if (enableLengthLimit) 
            {
                UpdateAdaptiveFadeParameters(); // NEW: Update adaptive parameters
                CleanupByLength();
            }
        }
        else if (!enableUpdateThrottling && isDrawing && enableLengthLimit)
        {
            UpdateAdaptiveFadeParameters(); // NEW: Update adaptive parameters
            CleanupByLength();
        }
    }
    
    #region Drawing System
    
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0)) StartDrawing();
        else if (Input.GetMouseButton(0) && isDrawing) UpdateDrawing();
        else if (Input.GetMouseButtonUp(0) && isDrawing) FinishDrawing();
    }
    
    void StartDrawing()
    {
        Vector3 hitPoint = Vector3.zero;
        Vector3 normal = Vector3.up;
        
        if (useScreenSpaceCalculation)
        {
            screenStartPos = Input.mousePosition;
            calculationCenter = cam.ScreenToWorldPoint(new Vector3(screenStartPos.x, screenStartPos.y, drawingPlaneDistance));
            drawingPlane = new Plane(cam.transform.forward, calculationCenter);
            
            if (projectVisualsToSurface && GetSphereHit(out hitPoint, out normal))
            {
                tangentNormal = normal;
                tangentCenter = hitPoint;
            }
            else
            {
                tangentNormal = cam.transform.forward;
                tangentCenter = calculationCenter;
            }
        }
        else
        {
            if (!GetSphereHit(out hitPoint, out normal)) return;
            tangentNormal = normal;
            tangentCenter = hitPoint;
            calculationCenter = hitPoint;
        }
        
        isDrawing = true;
        drawStartTime = Time.time;
        pointCount = 0;
        currentLineLength = 0f;
        
        Vector3 startPoint = useScreenSpaceCalculation ? 
            GetScreenSpaceWorldPoint() : ProjectToTangentPlane(hitPoint);
        
        AddPoint(startPoint, false);
        TriggerPathUpdate();
    }
    
    void UpdateDrawing()
    {
        Vector3 worldPos;
        
        if (useScreenSpaceCalculation)
        {
            worldPos = GetScreenSpaceWorldPoint();
        }
        else
        {
            if (!GetSphereHit(out Vector3 hitPoint, out Vector3 normal)) return;
            worldPos = ProjectToTangentPlane(hitPoint);
        }
        
        if (pointCount == 0)
        {
            AddPoint(worldPos, false);
            TriggerPathUpdate();
            return;
        }
        
        float distance = Vector3.Distance(worldPos, points[pointCount - 1]);
        bool pointAdded = false;
        
        // Adaptive Interpolation
        if (enableAdaptiveInterpolation && distance > maxDistance)
        {
            float speed = distance / Time.deltaTime;
            
            if (speed > speedThreshold)
            {
                int interpolationSteps = Mathf.Min(maxInterpolationSteps, 
                    Mathf.RoundToInt(speed / speedThreshold));
                
                Vector3 lastPos = points[pointCount - 1];
                for (int i = 1; i <= interpolationSteps; i++)
                {
                    float t = (float)i / (interpolationSteps + 1);
                    Vector3 interpPos = enableSphereAwareCurves ? 
                        GetSphereAwareInterpolation(lastPos, worldPos, t) :
                        Vector3.Lerp(lastPos, worldPos, t);
                    
                    if (AddPoint(interpPos, true)) pointAdded = true;
                }
            }
            else
            {
                Vector3 midPoint = enableSphereAwareCurves ?
                    GetSphereAwareInterpolation(points[pointCount - 1], worldPos, 0.5f) :
                    Vector3.Lerp(points[pointCount - 1], worldPos, 0.5f);
                
                if (AddPoint(midPoint, true)) pointAdded = true;
            }
            
            if (AddPoint(worldPos, false)) pointAdded = true;
        }
        else if (distance > maxDistance)
        {
            Vector3 midPoint = enableSphereAwareCurves ?
                GetSphereAwareInterpolation(points[pointCount - 1], worldPos, 0.5f) :
                Vector3.Lerp(points[pointCount - 1], worldPos, 0.5f);
            
            if (AddPoint(midPoint, true)) pointAdded = true;
            if (AddPoint(worldPos, false)) pointAdded = true;
        }
        else if (distance > minDistance)
        {
            if (AddPoint(worldPos, false)) pointAdded = true;
        }
        
        if (pointCount >= cleanupThreshold)
        {
            CleanupInterpolationStack();
        }
        
        if (pointAdded)
        {
            TriggerPathUpdate();
        }
    }
    
    bool AddPoint(Vector3 point, bool interpolated)
    {
        if (pointCount >= maxPoints)
        {
            CleanupInterpolationStack();
            if (pointCount >= maxPoints) return false;
        }
        
        if (pointCount > 0)
        {
            float segmentLength = Vector3.Distance(point, points[pointCount - 1]);
            currentLineLength += segmentLength;
            pointArcLengths[pointCount] = currentLineLength;
        }
        else
        {
            pointArcLengths[pointCount] = 0f;
        }
        
        points[pointCount] = point;
        isInterpolated[pointCount] = interpolated;
        pointFadeFactors[pointCount] = 1f;
        pointCount++;
        return true;
    }
    
    void FinishDrawing()
    {
        if (pointCount < 3)
        {
            CancelDrawing();
            return;
        }
        
        if (enableCatmullRom)
        {
            ApplyAdvancedSmoothing();
        }
        
        // NEW: Intelligente Circle-Positionierung
        Vector3 center;
        float drawnRadius;
        
        if (useIntelligentCirclePositioning)
        {
            center = CalculateIntelligentCircleCenter();
            drawnRadius = CalculateRadius(center);
        }
        else
        {
            center = CalculateCenter();
            drawnRadius = CalculateRadius(center);
        }
        
        float quality = CalculateAdvancedQuality(center);
        float finalRadius = CalculateFinalRadius(drawnRadius, quality);
        
        float drawTime = Time.time - drawStartTime;
        float speedBonus = Mathf.Clamp01(maxSpeedBonusTime / drawTime) * speedBonusMultiplier;
        
        lastConfirmedRadius = finalRadius;
        lastConfirmedCenter = center;
        lastConfirmedQuality = quality;
        
        GameEvents.TriggerCircleConfirmed(center, finalRadius, tangentNormal);
        GameEvents.TriggerCircleConfirmedWithQuality(center, finalRadius, quality);
        ProcessTargets(center, finalRadius, speedBonus);
        
        string qualityText = GetQualityText(quality);
        Debug.Log($"Circle: {qualityText} ({quality:F2}) | {drawnRadius:F2} → {finalRadius:F2} | Speed: {speedBonus:F2}");
        
        ResetDrawing();
    }
    
    void CancelDrawing()
    {
        GameEvents.TriggerDrawingCancelled();
        ResetDrawing();
    }
    
    void ResetDrawing()
    {
        isDrawing = false;
        pointCount = 0;
        currentLineLength = 0f;
        
        for (int i = 0; i < pointFadeFactors.Length; i++)
        {
            pointFadeFactors[i] = 1f;
        }
    }
    
    void TriggerPathUpdate()
    {
        Vector3[] activePoints = new Vector3[pointCount];
        
        if (projectVisualsToSurface && useScreenSpaceCalculation)
        {
            UpdateVisualProjection();
            System.Array.Copy(visualPoints, activePoints, pointCount);
        }
        else
        {
            System.Array.Copy(points, activePoints, pointCount);
        }
        
        GameEvents.TriggerPathUpdated(activePoints);
    }
    
    #endregion
    
    #region NEW: Intelligent Circle Positioning
    
    Vector3 CalculateIntelligentCircleCenter()
    {
        if (pointCount < 3) return points[pointCount - 1]; // Fallback: letzter Punkt
        
        // Prüfe ob es ein linearer Pfad ist
        bool isLinear = IsLinearPath();
        
        if (isLinear)
        {
            // Bei linearem Pfad: Verwende Linienspitze (letzter Punkt)
            Debug.Log("Linear path detected - using line tip position");
            return points[pointCount - 1];
        }
        else
        {
            // Bei Kreisansatz: Prüfe Kreisqualität
            Vector3 geometricCenter = CalculateCenter();
            float quality = CalculateAdvancedQuality(geometricCenter);
            
            if (quality >= mediumQualityThreshold)
            {
                // Guter Kreis: Verwende geometrischen Mittelpunkt
                Debug.Log($"Circle detected (Quality: {quality:F2}) - using geometric center");
                return geometricCenter;
            }
            else
            {
                // Schlechter Kreis aber kein linearer Pfad: 
                // Verwende gewichteten Mittelpunkt zwischen geometrischem Center und Linienspitze
                Vector3 lineEnd = points[pointCount - 1];
                float weight = Mathf.Clamp01(quality / mediumQualityThreshold); // 0-1 basierend auf Qualität
                
                Vector3 blendedCenter = Vector3.Lerp(lineEnd, geometricCenter, weight);
                Debug.Log($"Poor circle (Quality: {quality:F2}) - using blended position (weight: {weight:F2})");
                return blendedCenter;
            }
        }
    }
    
    #endregion
    
    #region FIXED: Camera-Aligned Visual Projection
    
    void UpdateVisualProjection()
    {
        for (int i = 0; i < pointCount; i++)
        {
            if (projectVisualsToSurface)
            {
                Vector3 direction = (points[i] - cam.transform.position).normalized;
                Ray ray = new Ray(cam.transform.position, direction);
                
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sphereLayer))
                {
                    // FIXED: Kamera-ausgerichteter Offset statt Surface Normal
                    Vector3 offsetDirection = useCameraAlignedOffset ? 
                        (cam.transform.position - hit.point).normalized : 
                        hit.normal;
                    
                    visualPoints[i] = hit.point + offsetDirection * surfaceOffset;
                }
                else
                {
                    Vector3 projected = ProjectToTangentPlane(points[i]);
                    
                    // FIXED: Kamera-ausgerichteter Offset für Fallback
                    Vector3 offsetDirection = useCameraAlignedOffset ? 
                        (cam.transform.position - projected).normalized : 
                        tangentNormal;
                    
                    visualPoints[i] = projected + offsetDirection * surfaceOffset;
                }
            }
            else
            {
                visualPoints[i] = points[i];
            }
        }
    }
    
    #endregion
    
    #region Advanced Smoothing & Sphere-Aware Curves
    
    Vector3 GetSphereAwareInterpolation(Vector3 start, Vector3 end, float t)
    {
        if (!enableSphereAwareCurves || sphereCenter == null)
        {
            return Vector3.Lerp(start, end, t);
        }
        
        Vector3 spherePos = sphereCenter.position;
        Vector3 linearPoint = Vector3.Lerp(start, end, t);
        Vector3 directionToLinear = (linearPoint - spherePos).normalized;
        Vector3 spherePoint = spherePos + directionToLinear * sphereRadius;
        
        float distanceFromSphere = Vector3.Distance(linearPoint, spherePos);
        float sphereInfluence = Mathf.Clamp01(sphereRadius / distanceFromSphere);
        
        return Vector3.Lerp(linearPoint, spherePoint, sphereInfluence * 0.3f);
    }
    
    void ApplyAdvancedSmoothing()
    {
        if (pointCount < 4) return;
        
        if (enableSphereAwareCurves)
        {
            ApplySphereAwareSmoothing();
        }
        
        for (int pass = 0; pass < smoothingSegments; pass++)
        {
            ApplyCatmullRomSmoothing();
        }
        
        ApplyAdaptiveSmoothing();
    }
    
    void ApplyCatmullRomSmoothing()
    {
        if (pointCount < 4) return;
        
        Vector3[] smoothedPositions = new Vector3[pointCount];
        
        for (int i = 1; i < pointCount - 2; i++)
        {
            Vector3 p0 = points[i - 1];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[i + 2];
            
            smoothedPositions[i] = CatmullRom(p0, p1, p2, p3, curveTension);
        }
        
        smoothedPositions[0] = points[0];
        if (pointCount > 1)
            smoothedPositions[pointCount - 1] = points[pointCount - 1];
        
        System.Array.Copy(smoothedPositions, points, pointCount);
    }
    
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
    
    void ApplySphereAwareSmoothing()
    {
        if (sphereCenter == null) return;
        
        Vector3[] sphereSmoothed = new Vector3[pointCount];
        Vector3 spherePos = sphereCenter.position;
        
        for (int i = 0; i < pointCount; i++)
        {
            if (i == 0 || i == pointCount - 1)
            {
                sphereSmoothed[i] = points[i];
                continue;
            }
            
            Vector3 prev = points[i - 1];
            Vector3 curr = points[i];
            Vector3 next = points[i + 1];
            
            Vector3 localCenter = (prev + curr + next) / 3f;
            Vector3 directionToLocal = (localCenter - spherePos).normalized;
            Vector3 projectedPoint = spherePos + directionToLocal * sphereRadius;
            
            sphereSmoothed[i] = Vector3.Lerp(curr, projectedPoint, 0.2f);
        }
        
        System.Array.Copy(sphereSmoothed, points, pointCount);
    }
    
    void ApplyAdaptiveSmoothing()
    {
        Vector3[] adaptiveSmoothed = new Vector3[pointCount];
        
        for (int i = 1; i < pointCount - 1; i++)
        {
            Vector3 prev = points[i - 1];
            Vector3 curr = points[i];
            Vector3 next = points[i + 1];
            
            Vector3 dir1 = (curr - prev).normalized;
            Vector3 dir2 = (next - curr).normalized;
            float angle = Vector3.Angle(dir1, dir2);
            
            float smoothingStrength = Mathf.Clamp01(angle / 90f);
            
            Vector3 smoothed = (prev + curr * 2f + next) / 4f;
            adaptiveSmoothed[i] = Vector3.Lerp(curr, smoothed, smoothingStrength * 0.5f);
        }
        
        adaptiveSmoothed[0] = points[0];
        adaptiveSmoothed[pointCount - 1] = points[pointCount - 1];
        
        for (int i = 1; i < pointCount - 1; i++)
        {
            points[i] = adaptiveSmoothed[i];
        }
    }
    
    #endregion
    
    #region Screen Space Calculation
    
    Vector3 GetScreenSpaceWorldPoint()
    {
        Vector2 currentScreenPos = Input.mousePosition;
        Ray ray = cam.ScreenPointToRay(currentScreenPos);
        
        if (drawingPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        
        return cam.ScreenToWorldPoint(new Vector3(currentScreenPos.x, currentScreenPos.y, drawingPlaneDistance));
    }
    
    float GetScreenSpaceRadius(Vector3 center)
    {
        if (!useScreenSpaceCalculation) return CalculateRadius(center);
        
        Vector2 centerScreen = cam.WorldToScreenPoint(center);
        float maxScreenDistance = 0f;
        
        for (int i = 0; i < pointCount; i++)
        {
            Vector2 pointScreen = cam.WorldToScreenPoint(points[i]);
            float screenDistance = Vector2.Distance(centerScreen, pointScreen);
            maxScreenDistance = Mathf.Max(maxScreenDistance, screenDistance);
        }
        
        return maxScreenDistance * screenToWorldScale;
    }
    
    #endregion
    
    #region Smooth Fade System
    
    void CleanupByLength()
    {
        if (currentLineLength > maxLineLength)
        {
            float fadeStartLength = maxLineLength - fadeZoneLength;
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
                    // FIXED: Nur sehr aggressive Fadeout für weit entfernte Punkte
                    float excessDistance = distanceFromEnd - maxLineLength;
                    float fadeStrength = Mathf.Clamp01(excessDistance / (fadeZoneLength * 2f)); // Langsamerer Fade
                    
                    float targetFade = useAnimationCurve ? 
                        fadeCurve.Evaluate(1f - fadeStrength) : 
                        Mathf.Lerp(fadeMinAlpha, 0f, fadeStrength); // FIXED: Min fadeMinAlpha statt 0f
                    
                    pointFadeFactors[i] = Mathf.MoveTowards(pointFadeFactors[i], targetFade, fadeSpeed * Time.deltaTime);
                    anyFaded = true;
                }
                else if (distanceFromEnd > fadeStartLength)
                {
                    // FIXED: Sanfter Fade in der Fade-Zone
                    float fadePosition = (maxLineLength - distanceFromEnd) / fadeZoneLength;
                    
                    float targetFade = useAnimationCurve ?
                        fadeCurve.Evaluate(1f - fadePosition) :
                        Mathf.Lerp(1f, fadeZoneMinAlpha, fadePosition); // FIXED: Min fadeZoneMinAlpha statt 0.1f
                    
                    pointFadeFactors[i] = Mathf.MoveTowards(pointFadeFactors[i], targetFade, fadeSpeed * Time.deltaTime);
                    anyFaded = true;
                }
                else
                {
                    pointFadeFactors[i] = 1f;
                }
            }
            
            // FIXED: Weniger aggressive Point-Removal
            if (anyFaded)
            {
                RemoveInvisiblePointsSmooth();
            }
        }
        else
        {
            for (int i = 0; i < pointCount; i++)
            {
                pointFadeFactors[i] = 1f;
            }
        }
    }
    
    // FIXED: Sanftere Point-Removal Logik
    void RemoveInvisiblePointsSmooth()
    {
        int writeIndex = 0;
        
        for (int readIndex = 0; readIndex < pointCount; readIndex++)
        {
            // FIXED: Konfigurierbare Schwelle für Point-Removal
            if (pointFadeFactors[readIndex] > minFadeThreshold)
            {
                if (writeIndex != readIndex)
                {
                    points[writeIndex] = points[readIndex];
                    visualPoints[writeIndex] = visualPoints[readIndex];
                    isInterpolated[writeIndex] = isInterpolated[readIndex];
                    pointFadeFactors[writeIndex] = pointFadeFactors[readIndex];
                    pointArcLengths[writeIndex] = pointArcLengths[readIndex];
                }
                writeIndex++;
            }
        }
        
        // FIXED: Nur entfernen wenn signifikante Anzahl gefaded und nicht mehr als maxPointsToRemovePerFrame
        int pointsToRemove = pointCount - writeIndex;
        if (writeIndex != pointCount && pointsToRemove >= 3 && pointsToRemove <= maxPointsToRemovePerFrame)
        {
            pointCount = writeIndex;
            RecalculateLineLength();
        }
    }
    
    // FIXED: Intelligentere Interpolation Stack Cleanup
    void CleanupInterpolationStack()
    {
        // FIXED: Weniger aggressive Cleanup-Größe
        int removeCount = Mathf.Min(cleanupStackSize / 2, pointCount / 4, maxPointsToRemovePerFrame);
        int removed = 0;
        
        // FIXED: Nur interpolierte Punkte bevorzugt entfernen
        for (int i = 0; i < pointCount && removed < removeCount; i++)
        {
            if (isInterpolated[i])
            {
                removed++;
            }
            else
            {
                break; // Stop bei ersten nicht-interpolierten Punkt
            }
        }
        
        if (removed > 0)
        {
            System.Array.Copy(points, removed, points, 0, pointCount - removed);
            System.Array.Copy(visualPoints, removed, visualPoints, 0, pointCount - removed);
            System.Array.Copy(isInterpolated, removed, isInterpolated, 0, pointCount - removed);
            System.Array.Copy(pointFadeFactors, removed, pointFadeFactors, 0, pointCount - removed);
            System.Array.Copy(pointArcLengths, removed, pointArcLengths, 0, pointCount - removed);
            
            pointCount -= removed;
            RecalculateLineLength();
        }
    }
    
    // FIXED: Adaptive Fade-Parameter basierend auf Drawing-Speed
    void UpdateAdaptiveFadeParameters()
    {
        if (!enableSmartFading) return;
        
        // Berechne Drawing-Geschwindigkeit
        float drawingTime = Time.time - drawStartTime;
        float averageSpeed = currentLineLength / Mathf.Max(drawingTime, 0.1f);
        
        // FIXED: Langsamere Drawings = sanfteres Fading
        if (averageSpeed < 2f) // Langsam
        {
            fadeSpeed = 2f; // Langsamer Fade
            minFadeThreshold = 0.2f; // Höhere Schwelle
        }
        else if (averageSpeed > 8f) // Schnell
        {
            fadeSpeed = 5f; // Schneller Fade
            minFadeThreshold = 0.1f; // Niedrigere Schwelle
        }
        else
        {
            fadeSpeed = 3f; // Normal
            minFadeThreshold = 0.15f; // Standard
        }
    }
    
    #endregion
    
    void RecalculateLineLength()
    {
        currentLineLength = 0f;
        for (int i = 1; i < pointCount; i++)
        {
            float segmentLength = Vector3.Distance(points[i], points[i - 1]);
            currentLineLength += segmentLength;
            pointArcLengths[i] = currentLineLength;
        }
        
        if (pointCount > 0)
        {
            pointArcLengths[0] = 0f;
        }
    }
    
    
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
        if (useScreenSpaceCalculation)
        {
            return GetScreenSpaceRadius(center);
        }
        
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
        
        float startEndDistance = Vector3.Distance(points[0], points[pointCount - 1]);
        float avgRadius = CalculateRadius(center);
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
        
        float finalRadius;
        bool isLinear = IsLinearPath();
        
        if (quality >= excellentQualityThreshold)
        {
            finalRadius = drawnRadius * excellentBonusMultiplier;
        }
        else if (quality >= goodQualityThreshold)
        {
            float bonusRange = Mathf.Lerp(1.0f, excellentBonusMultiplier, 
                (quality - goodQualityThreshold) / (excellentQualityThreshold - goodQualityThreshold));
            finalRadius = drawnRadius * bonusRange;
        }
        else if (quality >= mediumQualityThreshold)
        {
            float bonusRange = Mathf.Lerp(0.7f, 1.0f, 
                (quality - mediumQualityThreshold) / (goodQualityThreshold - mediumQualityThreshold));
            finalRadius = drawnRadius * bonusRange;
        }
        else
        {
            float penalty = Mathf.Lerp(poorQualityPenalty, baseRadiusMultiplier, 
                quality / mediumQualityThreshold);
            finalRadius = drawnRadius * penalty;
            
            if (isLinear)
            {
                finalRadius *= 0.5f;
            }
        }
        
        return Mathf.Max(finalRadius, minCircleRadius);
    }
    
    bool IsLinearPath()
    {
        if (pointCount < 6) return false;
        
        Vector3 start = points[0];
        Vector3 end = points[pointCount - 1];
        Vector3 lineDirection = (end - start).normalized;
        
        int linearPoints = 0;
        float threshold = 0.5f;
        
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
        return linearRatio > linearPathThreshold; // FIXED: Verwende konfigurierbare Schwelle
    }
    
    string GetQualityText(float quality)
    {
        if (quality >= excellentQualityThreshold) return "EXCELLENT";
        if (quality >= goodQualityThreshold) return "GOOD";  
        if (quality >= mediumQualityThreshold) return "MEDIUM";
        return "POOR";
    }
    
    #endregion
    
    #region Target Processing
    
    void ProcessTargets(Vector3 center, float radius, float speedBonus = 1f)
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
            GameEvents.TriggerTargetSelected(bestTarget, center, speedBonus);
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
    public bool IsCurrentlyDrawing() => isDrawing;
    public bool HasValidCircle() => lastConfirmedRadius > 0f;
    
    // Settings API
    public void SetMaxLineLength(float length) => maxLineLength = length;
    public void SetStrictClosureMode(bool enabled) => enableQualityScaling = enabled;
    public void SetClosureThreshold(float threshold) => closureThreshold = threshold;
    public void SetPoorQualityPenalty(float penalty) => poorQualityPenalty = penalty;
    
    // Performance Settings
    public void SetUpdateThrottling(bool enabled) => enableUpdateThrottling = enabled;
    public void SetUpdateInterval(float interval) => updateInterval = interval;
    
    // Screen Space Settings
    public void SetScreenSpaceCalculation(bool enabled) => useScreenSpaceCalculation = enabled;
    public void SetScreenToWorldScale(float scale) => screenToWorldScale = scale;
    public void SetDrawingPlaneDistance(float distance) => drawingPlaneDistance = distance;
    public void SetProjectVisualsToSurface(bool enabled) => projectVisualsToSurface = enabled;
    public void SetSurfaceOffset(float offset) => surfaceOffset = offset;
    
    // NEW: Visual Offset Settings
    public void SetCameraAlignedOffset(bool enabled) => useCameraAlignedOffset = enabled;
    
    // Circle Positioning Settings
    public void SetIntelligentCirclePositioning(bool enabled) => useIntelligentCirclePositioning = enabled;
    public void SetLinearPathThreshold(float threshold) => linearPathThreshold = Mathf.Clamp01(threshold);
    
    // Advanced Smoothing Settings
    public void SetAdaptiveInterpolation(bool enabled) => enableAdaptiveInterpolation = enabled;
    public void SetSpeedThreshold(float threshold) => speedThreshold = threshold;
    public void SetMaxInterpolationSteps(int steps) => maxInterpolationSteps = steps;
    public void SetSphereAwareCurves(bool enabled) => enableSphereAwareCurves = enabled;
    public void SetSphereRadius(float radius) => sphereRadius = radius;
    
    // NEW: Fade System Balancing API
    public void SetFadeMinThreshold(float threshold) => minFadeThreshold = Mathf.Clamp01(threshold);
    public void SetFadeMinAlpha(float alpha) => fadeMinAlpha = Mathf.Clamp01(alpha);
    public void SetFadeZoneMinAlpha(float alpha) => fadeZoneMinAlpha = Mathf.Clamp01(alpha);
    public void SetMaxPointsToRemovePerFrame(int maxPoints) => maxPointsToRemovePerFrame = Mathf.Max(1, maxPoints);
    public void SetSmartFading(bool enabled) => enableSmartFading = enabled;
    
    // NEW: Fade System Status
    public float GetCurrentFadeThreshold() => minFadeThreshold;
    public float GetAverageFadeLevel()
    {
        if (pointCount == 0) return 1f;
        
        float totalFade = 0f;
        for (int i = 0; i < pointCount; i++)
        {
            totalFade += pointFadeFactors[i];
        }
        return totalFade / pointCount;
    }
    
    // NEW: Debug Fade Status
    public string GetFadeDebugInfo()
    {
        int fadedPoints = 0;
        int removedCandidates = 0;
        
        for (int i = 0; i < pointCount; i++)
        {
            if (pointFadeFactors[i] < 1f) fadedPoints++;
            if (pointFadeFactors[i] <= minFadeThreshold) removedCandidates++;
        }
        
        return $"Fade Status: {fadedPoints}/{pointCount} faded, {removedCandidates} removal candidates";
    }
    
    // Area damage trigger for external systems
    public void TriggerAreaDamageEvent(Vector3 position, float radius, int targetCount)
    {
        GameEvents.TriggerAreaDamageDealt(position, radius, targetCount);
    }
    
    #endregion
    
    #region Debug Tools
    
    [ContextMenu("Test Circle Quality")]
    void TestCircleQuality()
    {
        if (pointCount > 0)
        {
            Vector3 center = useIntelligentCirclePositioning ? 
                CalculateIntelligentCircleCenter() : CalculateCenter();
            float radius = CalculateRadius(center);
            float quality = CalculateAdvancedQuality(center);
            float finalRadius = CalculateFinalRadius(radius, quality);
            bool isLinear = IsLinearPath();
            string qualityText = GetQualityText(quality);
            
            Debug.Log($"Circle Analysis:" +
                     $"\nPoints: {pointCount}" +
                     $"\nDrawn Radius: {radius:F2}" +
                     $"\nFinal Radius: {finalRadius:F2}" +
                     $"\nQuality: {qualityText} ({quality:F2})" +
                     $"\nIs Linear: {isLinear}" +
                     $"\nLine Length: {currentLineLength:F2}/{maxLineLength:F2}" +
                     $"\nSmoothing: {enableCatmullRom}" +
                     $"\nThrottling: {enableUpdateThrottling}" +
                     $"\nScreen Space: {useScreenSpaceCalculation}" +
                     $"\nSurface Offset: {surfaceOffset:F3}" +
                     $"\nCamera Aligned Offset: {useCameraAlignedOffset}" +
                     $"\nIntelligent Positioning: {useIntelligentCirclePositioning}" +
                     $"\nLinear Threshold: {linearPathThreshold:F2}" +
                     $"\nAdaptive Interpolation: {enableAdaptiveInterpolation}" +
                     $"\nSphere Aware: {enableSphereAwareCurves}");
        }
    }
    
    [ContextMenu("Toggle Performance Features")]
    void TogglePerformanceFeatures()
    {
        enableUpdateThrottling = !enableUpdateThrottling;
        enableCatmullRom = !enableCatmullRom;
        useAnimationCurve = !useAnimationCurve;
        
        Debug.Log($"Performance Settings:" +
                 $"\nThrottling: {enableUpdateThrottling}" +
                 $"\nSmoothing: {enableCatmullRom}" +
                 $"\nCurve Fading: {useAnimationCurve}");
    }
    
    [ContextMenu("Toggle Visual Settings")]
    void ToggleVisualSettings()
    {
        useCameraAlignedOffset = !useCameraAlignedOffset;
        useIntelligentCirclePositioning = !useIntelligentCirclePositioning;
        
        Debug.Log($"Visual Settings:" +
                 $"\nCamera Aligned Offset: {useCameraAlignedOffset}" +
                 $"\nIntelligent Circle Positioning: {useIntelligentCirclePositioning}" +
                 $"\nLinear Path Threshold: {linearPathThreshold:F2}");
    }
    
    [ContextMenu("Debug Fade System")]
    void DebugFadeSystem()
    {
        Debug.Log($"Fade System Debug:" +
                 $"\n{GetFadeDebugInfo()}" +
                 $"\nAverage Fade Level: {GetAverageFadeLevel():F2}" +
                 $"\nCurrent Fade Threshold: {GetCurrentFadeThreshold():F2}" +
                 $"\nSmart Fading: {enableSmartFading}" +
                 $"\nMax Points Remove/Frame: {maxPointsToRemovePerFrame}" +
                 $"\nFade Min Alpha: {fadeMinAlpha:F2}" +
                 $"\nFade Zone Min Alpha: {fadeZoneMinAlpha:F2}");
    }
    
    [ContextMenu("Reset Fade Parameters")]
    void ResetFadeParameters()
    {
        minFadeThreshold = 0.15f;
        fadeMinAlpha = 0.3f;
        fadeZoneMinAlpha = 0.5f;
        maxPointsToRemovePerFrame = 5;
        enableSmartFading = true;
        fadeSpeed = 3f;
        
        Debug.Log("Fade parameters reset to default values");
    }
    
    #endregion
}