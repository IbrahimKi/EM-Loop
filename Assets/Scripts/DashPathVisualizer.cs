using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class DashPathVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color pathColor = new Color(0f, 1f, 1f, 1f); // Cyan
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private float heightOffset = 0.05f;
    [SerializeField] private float curveAltitude = 0.5f; // Höhe der Kurve über Sphere
    [SerializeField] private bool showOnlyDuringStartup = true;
    
    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private LeanTweenType fadeEase = LeanTweenType.easeOutQuad;
    
    [Header("References")]
    [SerializeField] private Transform sphereCenter;
    [SerializeField] private PlayerDash playerDash;
    
    private LineRenderer lineRenderer;
    private Vector3[] pathCache;
    private int fadeId = -1;
    private bool isVisible;
    
    void Awake()
    {
        SetupLineRenderer();
        
        if (sphereCenter == null)
            sphereCenter = transform.parent;
        
        if (playerDash == null)
            playerDash = GetComponent<PlayerDash>();
    }
    
    void OnEnable()
    {
        CircleManager.OnCircleConfirmed += ShowDashPath;
        PlayerDash.OnDashStarted += OnDashStarted;
        PlayerDash.OnDashCompleted += HidePath;
    }
    
    void OnDisable()
    {
        CircleManager.OnCircleConfirmed -= ShowDashPath;
        PlayerDash.OnDashStarted -= OnDashStarted;
        PlayerDash.OnDashCompleted -= HidePath;
        
        if (fadeId >= 0) LeanTween.cancel(fadeId);
    }
    
    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = pathColor;
        lineRenderer.endColor = pathColor;
        lineRenderer.positionCount = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }
    
    void ShowDashPath(Vector3 center, float radius, Vector3 normal)
    {
        if (!sphereCenter) return;
        
        Vector3 startPos = transform.position;
        Vector3 spherePos = sphereCenter.position;
        float sphereRadius = Vector3.Distance(startPos, spherePos);
        
        // Zielposition auf Sphere projizieren
        Vector3 targetDir = (center - spherePos).normalized;
        Vector3 targetPos = spherePos + targetDir * sphereRadius;
        
        // Pfad berechnen
        CalculateSpherePath(startPos, targetPos, spherePos, sphereRadius);
        
        // LineRenderer setzen
        lineRenderer.positionCount = pathCache.Length;
        lineRenderer.SetPositions(pathCache);
        
        // Fade In
        FadeIn();
    }
    
    void OnDashStarted(Vector3 targetPos)
    {
        if (showOnlyDuringStartup)
        {
            FadeOut();
        }
    }
    
    void HidePath(Vector3 targetPos)
    {
        FadeOut();
    }
    
    void CalculateSpherePath(Vector3 start, Vector3 end, Vector3 center, float radius)
    {
        int segments = 16;
        if (pathCache == null || pathCache.Length != segments + 1)
            pathCache = new Vector3[segments + 1];
        
        Vector3 startDir = (start - center).normalized;
        Vector3 endDir = (end - center).normalized;
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 lerpDir = Vector3.Slerp(startDir, endDir, t).normalized;
            
            // Kurven-Altitude: Sinus-Kurve für natürliche Höhenvariation
            float altitudeMultiplier = Mathf.Sin(t * Mathf.PI);
            float currentRadius = radius + (curveAltitude * altitudeMultiplier);
            
            Vector3 surfacePos = center + lerpDir * currentRadius;
            pathCache[i] = surfacePos + lerpDir * heightOffset;
        }
    }
    
    void FadeIn()
    {
        if (fadeId >= 0) LeanTween.cancel(fadeId);
        
        Color transparent = new Color(pathColor.r, pathColor.g, pathColor.b, 0f);
        lineRenderer.startColor = transparent;
        lineRenderer.endColor = transparent;
        
        fadeId = LeanTween.value(gameObject, 0f, pathColor.a, fadeInDuration)
            .setEase(fadeEase)
            .setOnUpdate((float alpha) => {
                Color c = new Color(pathColor.r, pathColor.g, pathColor.b, alpha);
                lineRenderer.startColor = c;
                lineRenderer.endColor = c;
            })
            .setOnComplete(() => {
                isVisible = true;
                fadeId = -1;
            }).id;
    }
    
    void FadeOut()
    {
        if (!isVisible || fadeId >= 0) return;
        
        fadeId = LeanTween.value(gameObject, lineRenderer.startColor.a, 0f, fadeOutDuration)
            .setEase(fadeEase)
            .setOnUpdate((float alpha) => {
                Color c = new Color(pathColor.r, pathColor.g, pathColor.b, alpha);
                lineRenderer.startColor = c;
                lineRenderer.endColor = c;
            })
            .setOnComplete(() => {
                lineRenderer.positionCount = 0;
                isVisible = false;
                fadeId = -1;
            }).id;
    }
}