using UnityEngine;

/// <summary>
/// Player Dash System - Unity 6 LTS
/// UPDATED: GameEvents Integration
/// </summary>
public class PlayerDash : MonoBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private LeanTweenType dashCurve = LeanTweenType.easeOutCubic;
    [SerializeField] private float startupDelay = 0.1f;
    [SerializeField] private bool enableDash = true;
    [SerializeField] private Transform sphereCenter;
    [SerializeField] private int pathSegments = 16;
    
    private bool isDashing;
    private int tweenId = -1;
    private Vector3[] dashPath;
    
    // REMOVED: Static events - now using GameEvents
    
    void Awake()
    {
        if (sphereCenter == null)
            sphereCenter = transform.parent;
        
        dashPath = new Vector3[pathSegments + 1];
    }
    
    void OnEnable()
    {
        // UPDATED: Subscribe to GameEvents
        GameEvents.OnCircleConfirmed += DashToPosition;
    }
    
    void OnDisable()
    {
        // UPDATED: Unsubscribe from GameEvents
        GameEvents.OnCircleConfirmed -= DashToPosition;
        CancelDash();
    }
    
    void DashToPosition(Vector3 center, float radius, Vector3 normal)
    {
        if (!enableDash || isDashing || !sphereCenter) return;
        
        Vector3 startPos = transform.position;
        Vector3 spherePos = sphereCenter.position;
        float sphereRadius = Vector3.Distance(startPos, spherePos);
        
        // Zielposition auf Sphere-Oberfläche projizieren
        Vector3 targetDir = (center - spherePos).normalized;
        Vector3 targetPos = spherePos + targetDir * sphereRadius;
        
        if (Vector3.Distance(startPos, targetPos) < 0.1f) return;
        
        isDashing = true;
        
        // UPDATED: Use GameEvents
        GameEvents.TriggerDashStarted(targetPos);
        
        if (tweenId >= 0) LeanTween.cancel(tweenId);
        
        // Startup Delay
        tweenId = LeanTween.delayedCall(startupDelay, () => {
            // Gebogenen Pfad auf Sphere-Oberfläche berechnen
            CalculateSpherePath(transform.position, targetPos, spherePos, sphereRadius);
            
            tweenId = LeanTween.moveSpline(gameObject, dashPath, dashDuration)
                .setEase(dashCurve)
                .setOnComplete(() => {
                    isDashing = false;
                    
                    // UPDATED: Use GameEvents
                    GameEvents.TriggerDashCompleted(targetPos);
                    tweenId = -1;
                }).id;
        }).id;
    }
    
    void CalculateSpherePath(Vector3 start, Vector3 end, Vector3 center, float radius)
    {
        dashPath[0] = start;
        dashPath[pathSegments] = end;
        
        Vector3 startDir = (start - center).normalized;
        Vector3 endDir = (end - center).normalized;
        
        for (int i = 1; i < pathSegments; i++)
        {
            float t = (float)i / pathSegments;
            Vector3 lerpDir = Vector3.Slerp(startDir, endDir, t).normalized;
            dashPath[i] = center + lerpDir * radius;
        }
    }
    
    void CancelDash()
    {
        if (isDashing && tweenId >= 0)
        {
            LeanTween.cancel(tweenId);
            isDashing = false;
            tweenId = -1;
        }
    }
    
    // Public API
    public bool IsDashing => isDashing;
    public float GetDashDuration() => dashDuration;
    public bool IsDashEnabled() => enableDash;
    
    public void SetDashEnabled(bool enabled) => enableDash = enabled;
    public void SetDashDuration(float duration) => dashDuration = Mathf.Max(0.1f, duration);
    public void SetStartupDelay(float delay) => startupDelay = Mathf.Max(0f, delay);
}