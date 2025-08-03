using UnityEngine;
using System.Collections;

/// <summary>
/// Dash AOE Executor - Führt Dash zum Target und AOE Damage aus
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class DashAOEExecutor : MonoBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private float dashDuration = 0.4f;
    [SerializeField] private LeanTweenType dashEase = LeanTweenType.easeInOutQuad;
    [SerializeField] private float dashArcHeight = 1f;
    [SerializeField] private float previewTime = 0.2f;
    
    [Header("AOE Settings")]
    [SerializeField] private float baseAOERadius = 2f;
    [SerializeField] private float baseDamage = 50f;
    [SerializeField] private LayerMask damageLayer = -1;
    [SerializeField] private float aoeDelay = 0.1f; // After landing
    
    [Header("Quality Scaling")]
    [SerializeField] private AnimationCurve radiusQualityCurve = AnimationCurve.Linear(0, 0.5f, 1, 1.5f);
    [SerializeField] private AnimationCurve damageQualityCurve = AnimationCurve.Linear(0, 0.5f, 1, 2f);
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject dashTrailPrefab;
    [SerializeField] private GameObject aoeEffectPrefab;
    [SerializeField] private GameObject landingEffectPrefab;
    
    [Header("References")]
    [SerializeField] private Transform sphereCenter;
    
    // Components
    private PlayerMovement playerMovement;
    
    // State
    private bool isDashing;
    private int dashTweenId = -1;
    private float lastQuality;
    
    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        
        if (!sphereCenter)
        {
            GameObject sphere = GameObject.FindWithTag("Sphere");
            if (sphere) sphereCenter = sphere.transform;
        }
    }
    
    void OnEnable()
    {
        TargetSelector.OnTargetSelected += ExecuteDashToTarget;
        TargetSelector.OnNoTargetFound += ShowNoTargetFeedback;
    }
    
    void OnDisable()
    {
        TargetSelector.OnTargetSelected -= ExecuteDashToTarget;
        TargetSelector.OnNoTargetFound -= ShowNoTargetFeedback;
        
        if (dashTweenId >= 0) LeanTween.cancel(dashTweenId);
    }
    
    void ExecuteDashToTarget(GameObject target, Vector3 circleCenter, float quality)
    {
        if (isDashing || target == null) return;
        
        lastQuality = quality;
        Vector3 targetPos = GetTargetPositionOnSphere(target.transform.position);
        
        StartCoroutine(DashSequence(targetPos, quality));
    }
    
    Vector3 GetTargetPositionOnSphere(Vector3 targetWorldPos)
    {
        if (!sphereCenter) return targetWorldPos;
        
        // Project target position to sphere surface
        Vector3 toTarget = targetWorldPos - sphereCenter.position;
        float sphereRadius = Vector3.Distance(transform.position, sphereCenter.position);
        
        return sphereCenter.position + toTarget.normalized * sphereRadius;
    }
    
    IEnumerator DashSequence(Vector3 targetPos, float quality)
    {
        isDashing = true;
        
        // Disable player control
        if (playerMovement) playerMovement.enabled = false;
        
        // Show preview
        yield return new WaitForSeconds(previewTime);
        
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
        
        // Re-enable player control
        if (playerMovement) playerMovement.enabled = true;
        
        isDashing = false;
        dashTweenId = -1;
        
        // Trigger completion event
        GameEvents.TriggerDashCompleted(targetPos);
    }
    
    Vector3[] CalculateDashPath(Vector3 start, Vector3 end)
    {
        if (!sphereCenter)
        {
            // Simple arc without sphere
            Vector3 mid = (start + end) * 0.5f + Vector3.up * dashArcHeight;
            return new Vector3[] { start, mid, end };
        }
        
        // Path along sphere surface with arc
        int segments = 8;
        Vector3[] path = new Vector3[segments + 1];
        
        Vector3 spherePos = sphereCenter.position;
        float sphereRadius = Vector3.Distance(start, spherePos);
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            
            // Interpolate on sphere
            Vector3 startDir = (start - spherePos).normalized;
            Vector3 endDir = (end - spherePos).normalized;
            Vector3 currentDir = Vector3.Slerp(startDir, endDir, t);
            
            // Add arc height
            float arcMultiplier = Mathf.Sin(t * Mathf.PI) * dashArcHeight;
            float currentRadius = sphereRadius + arcMultiplier;
            
            path[i] = spherePos + currentDir * currentRadius;
        }
        
        return path;
    }
    
    void ExecuteAOE(float quality)
    {
        // Calculate scaled values
        float finalRadius = baseAOERadius * radiusQualityCurve.Evaluate(quality);
        float finalDamage = baseDamage * damageQualityCurve.Evaluate(quality);
        
        // Visual effect
        if (aoeEffectPrefab)
        {
            GameObject effect = Instantiate(aoeEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * (finalRadius / baseAOERadius);
            Destroy(effect, 2f);
        }
        
        // Find targets
        Collider[] targets = Physics.OverlapSphere(transform.position, finalRadius, damageLayer);
        int hitCount = 0;
        
        foreach (var target in targets)
        {
            // Skip self
            if (target.transform == transform) continue;
            
            // Try to damage
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive())
            {
                damageable.TakeDamage(finalDamage);
                hitCount++;
            }
        }
        
        // Trigger event
        GameEvents.TriggerAreaDamageDealt(transform.position, finalRadius, hitCount);
        
        Debug.Log($"AOE: {hitCount} targets hit | Radius: {finalRadius:F1} | Damage: {finalDamage:F0} | Quality: {quality:F2}");
    }
    
    void ShowNoTargetFeedback(Vector3 center, float radius)
    {
        Debug.Log("No valid target found in circle!");
        // Could show visual feedback here
    }
    
    // Public API
    public bool IsDashing => isDashing;
    public float GetLastQuality => lastQuality;
    
    public void SetDashDuration(float duration) => dashDuration = Mathf.Max(0.1f, duration);
    public void SetBaseAOERadius(float radius) => baseAOERadius = Mathf.Max(0.5f, radius);
    public void SetBaseDamage(float damage) => baseDamage = Mathf.Max(1f, damage);
    
    // Force cancel dash
    public void CancelDash()
    {
        if (isDashing && dashTweenId >= 0)
        {
            LeanTween.cancel(dashTweenId);
            StopAllCoroutines();
            isDashing = false;
            
            if (playerMovement) playerMovement.enabled = true;
        }
    }
}

// Placeholder for player movement
public class PlayerMovement : MonoBehaviour
{
    // Your existing player movement code
}