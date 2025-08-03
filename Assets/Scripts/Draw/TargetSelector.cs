using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Target Selector - Wählt das beste Target im gezeichneten Kreis
/// </summary>
public class TargetSelector : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField] private LayerMask targetLayer = -1;
    [SerializeField] private float selectionPadding = 0.2f; // Extra radius
    [SerializeField] private bool preferClosestToCenter = true;
    [SerializeField] private bool requireLineOfSight = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject selectionIndicatorPrefab;
    [SerializeField] private float indicatorDuration = 0.5f;
    
    // Events
    public static event System.Action<GameObject, Vector3, float> OnTargetSelected;
    public static event System.Action<Vector3, float> OnNoTargetFound;
    
    void OnEnable()
    {
        CircleAnalyzer.OnCircleDetected += SelectTarget;
    }
    
    void OnDisable()
    {
        CircleAnalyzer.OnCircleDetected -= SelectTarget;
    }
    
    void SelectTarget(Vector3 center, float radius, float quality)
    {
        // Expand radius slightly for generous selection
        float searchRadius = radius + selectionPadding;
        
        // Find all potential targets
        Collider[] colliders = Physics.OverlapSphere(center, searchRadius, targetLayer);
        
        if (colliders.Length == 0)
        {
            OnNoTargetFound?.Invoke(center, radius);
            return;
        }
        
        // Find best target
        GameObject bestTarget = null;
        float bestScore = float.MinValue;
        
        foreach (var collider in colliders)
        {
            // Skip if no line of sight (optional)
            if (requireLineOfSight && !HasLineOfSight(center, collider.transform.position))
                continue;
            
            // Calculate score
            float score = CalculateTargetScore(collider.gameObject, center, radius);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = collider.gameObject;
            }
        }
        
        if (bestTarget != null)
        {
            // Show visual feedback
            ShowSelectionIndicator(bestTarget.transform.position);
            
            // Trigger event with quality as bonus
            OnTargetSelected?.Invoke(bestTarget, center, quality);
        }
        else
        {
            OnNoTargetFound?.Invoke(center, radius);
        }
    }
    
    float CalculateTargetScore(GameObject target, Vector3 center, float radius)
    {
        float score = 0;
        
        // Distance to center (closer is better)
        float distance = Vector3.Distance(target.transform.position, center);
        float distanceScore = 1f - (distance / radius);
        score += distanceScore * (preferClosestToCenter ? 2f : 1f);
        
        // Check for priority component
        var targetComponent = target.GetComponent<CircleTarget>();
        if (targetComponent != null)
        {
            score += targetComponent.Priority * 0.5f;
        }
        
        // Enemy priority
        if (target.GetComponent<RhythmEnemyController>() != null)
        {
            score += 1f;
        }
        
        // Size bonus (bigger targets easier to hit)
        var collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            float size = collider.bounds.size.magnitude;
            score += Mathf.Clamp01(size / 5f) * 0.5f;
        }
        
        return score;
    }
    
    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        
        // Check if anything blocks the path
        if (Physics.Raycast(from, direction.normalized, out RaycastHit hit, distance))
        {
            // If we hit the target, we have line of sight
            return hit.collider.transform.position == to;
        }
        
        return true;
    }
    
    void ShowSelectionIndicator(Vector3 position)
    {
        if (selectionIndicatorPrefab == null) return;
        
        GameObject indicator = Instantiate(selectionIndicatorPrefab, position, Quaternion.identity);
        
        // Auto destroy
        Destroy(indicator, indicatorDuration);
    }
    
    // Public API
    public void SetTargetLayer(LayerMask layer) => targetLayer = layer;
    public void SetSelectionPadding(float padding) => selectionPadding = Mathf.Max(0, padding);
    public void SetPreferCenter(bool prefer) => preferClosestToCenter = prefer;
    public void SetRequireLineOfSight(bool require) => requireLineOfSight = require;
}