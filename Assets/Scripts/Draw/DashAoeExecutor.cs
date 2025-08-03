using UnityEngine;
using System.Collections;

/// <summary>
/// Simplified Dash AOE Executor - One-Hit Energy System
/// Circle-based targeting mit player movement und AOE damage
/// </summary>
public class DashAOEExecutor : MonoBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private float dashDuration = 0.4f;
    [SerializeField] private LeanTweenType dashEase = LeanTweenType.easeInOutQuad;
    [SerializeField] private float dashArcHeight = 1f;
    
    [Header("AOE Settings")]
    [SerializeField] private float baseAOERadius = 3f;
    [SerializeField] private LayerMask enemyLayer = -1;
    [SerializeField] private float aoeDelay = 0.1f;
    
    [Header("Quality Scaling")]
    [SerializeField] private AnimationCurve radiusQualityCurve = AnimationCurve.Linear(0, 0.7f, 1, 1.5f);
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject dashTrailPrefab;
    [SerializeField] private GameObject aoeEffectPrefab;
    [SerializeField] private GameObject landingEffectPrefab;
    
    [Header("References")]
    [SerializeField] private Transform sphereCenter;
    
    // Components
    private SimplePlayerController playerController;
    
    // State
    private bool isDashing;
    private int dashTweenId = -1;
    
    void Awake()
    {
        playerController = GetComponent<SimplePlayerController>();
        
        if (!sphereCenter)
        {
            GameObject sphere = GameObject.FindWithTag("Sphere");
            if (sphere) sphereCenter = sphere.transform;
        }
    }
    
    void OnEnable()
    {
        // Subscribe to circle confirmation instead of target selection
        GameEvents.OnCircleConfirmed += OnCircleConfirmed;
    }
    
    void OnDisable()
    {
        GameEvents.OnCircleConfirmed -= OnCircleConfirmed;
        
        if (dashTweenId >= 0) LeanTween.cancel(dashTweenId);
    }
    
    void OnCircleConfirmed(Vector3 center, float radius, Vector3 normal)
    {
        if (isDashing || !playerController.IsAlive()) return;
        
        // No energy cost for dash - dash is always available
        Vector3 targetPos = GetTargetPositionOnSphere(center);
        float quality = CalculateCircleQuality(radius);
        
        StartCoroutine(DashSequence(targetPos, quality));
    }
    
    float CalculateCircleQuality(float radius)
    {
        // Simple quality: optimal radius = 3f, quality decreases with distance from optimal
        float optimalRadius = 3f;
        float deviation = Mathf.Abs(radius - optimalRadius) / optimalRadius;
        return Mathf.Clamp01(1f - deviation);
    }
    
    Vector3 GetTargetPositionOnSphere(Vector3 targetWorldPos)
    {
        if (!sphereCenter) return targetWorldPos;
        
        Vector3 toTarget = targetWorldPos - sphereCenter.position;
        float sphereRadius = Vector3.Distance(transform.position, sphereCenter.position);
        
        return sphereCenter.position + toTarget.normalized * sphereRadius;
    }
    
    IEnumerator DashSequence(Vector3 targetPos, float quality)
    {
        isDashing = true;
        
        // Create trail
        GameObject trail = null;
        if (dashTrailPrefab)
        {
            trail = Instantiate(dashTrailPrefab, transform.position, Quaternion.identity);
            trail.transform.SetParent(transform);
        }
        
        // Calculate path
        Vector3 startPos = transform.position;
        Vector3[] dashPath = CalculateDashPath(startPos, targetPos);
        
        // Perform dash
        dashTweenId = LeanTween.moveSpline(gameObject, dashPath, dashDuration)
            .setEase(dashEase).id;
        
        // Trigger dash event
        GameEvents.TriggerDashStarted(targetPos);
        
        // Wait for dash to complete
        yield return new WaitForSeconds(dashDuration);
        
        // Landing effect
        if (landingEffectPrefab)
        {
            Instantiate(landingEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Cleanup trail
        if (trail)
        {
            trail.transform.SetParent(null);
            Destroy(trail, 1f);
        }
        
        // Small delay before AOE
        yield return new WaitForSeconds(aoeDelay);
        
        // Execute AOE
        ExecuteAOE(quality);
        
        isDashing = false;
        dashTweenId = -1;
        
        // Trigger completion event
        GameEvents.TriggerDashCompleted(targetPos);
    }
    
    Vector3[] CalculateDashPath(Vector3 start, Vector3 end)
    {
        if (!sphereCenter)
        {
            Vector3 mid = (start + end) * 0.5f + Vector3.up * dashArcHeight;
            return new Vector3[] { start, mid, end };
        }
        
        // Path along sphere surface with arc
        int segments = 6; // Reduced for performance
        Vector3[] path = new Vector3[segments + 1];
        
        Vector3 spherePos = sphereCenter.position;
        float sphereRadius = Vector3.Distance(start, spherePos);
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            
            Vector3 startDir = (start - spherePos).normalized;
            Vector3 endDir = (end - spherePos).normalized;
            Vector3 currentDir = Vector3.Slerp(startDir, endDir, t);
            
            float arcMultiplier = Mathf.Sin(t * Mathf.PI) * dashArcHeight;
            float currentRadius = sphereRadius + arcMultiplier;
            
            path[i] = spherePos + currentDir * currentRadius;
        }
        
        return path;
    }
    
    void ExecuteAOE(float quality)
    {
        // Calculate scaled radius
        float finalRadius = baseAOERadius * radiusQualityCurve.Evaluate(quality);
        
        // Visual effect
        if (aoeEffectPrefab)
        {
            GameObject effect = Instantiate(aoeEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * (finalRadius / baseAOERadius);
            Destroy(effect, 2f);
        }
        
        // Find enemies
        Collider[] targets = Physics.OverlapSphere(transform.position, finalRadius, enemyLayer);
        int enemiesHit = 0;
        
        foreach (var target in targets)
        {
            if (target.transform == transform) continue;
            
            // Check for enemy
            var enemy = target.GetComponent<EnemyController>();
            if (enemy != null && enemy.IsAlive())
            {
                // One-hit kill all enemies in AOE
                enemy.TakeDamage(9999f); // Instant kill
                enemiesHit++;
            }
        }
        
        // Trigger event
        GameEvents.TriggerAreaDamageDealt(transform.position, finalRadius, enemiesHit);
        
        Debug.Log($"Dash AOE: {enemiesHit} enemies destroyed | Radius: {finalRadius:F1} | Quality: {quality:F2}");
    }
    
    // Public API
    public bool IsDashing => isDashing;
    public void SetDashDuration(float duration) => dashDuration = Mathf.Max(0.1f, duration);
    public void SetBaseAOERadius(float radius) => baseAOERadius = Mathf.Max(0.5f, radius);
    
    public void CancelDash()
    {
        if (isDashing && dashTweenId >= 0)
        {
            LeanTween.cancel(dashTweenId);
            StopAllCoroutines();
            isDashing = false;
        }
    }
}