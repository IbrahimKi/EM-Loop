using System.Collections.Generic;
using UnityEngine;


public class CircleAreaInstance : MonoBehaviour
{
    private CircleManager manager;
    private LineRenderer circleRenderer;
    private SphereCollider damageCollider;
    private HashSet<IDamageable> damagedTargets;
    
    private int scaleId = -1;
    private int fadeId = -1;
    private bool isActive = false;
    private bool hasDamaged = false;
    
    public void Initialize(CircleManager mgr, Material material, int segments)
    {
        manager = mgr;
        damagedTargets = new HashSet<IDamageable>();
        
        CreateCircleRenderer(material, segments);
        CreateDamageCollider();
    }
    
    void CreateCircleRenderer(Material material, int segments)
    {
        circleRenderer = gameObject.AddComponent<LineRenderer>();
        circleRenderer.material = material ? material : CreateDefaultMaterial();
        circleRenderer.startWidth = 0.1f;
        circleRenderer.endWidth = 0.1f;
        circleRenderer.useWorldSpace = true;
        circleRenderer.positionCount = segments + 1;
        circleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        circleRenderer.receiveShadows = false;
    }
    
    void CreateDamageCollider()
    {
        damageCollider = gameObject.AddComponent<SphereCollider>();
        damageCollider.isTrigger = true;
        damageCollider.enabled = false;
    }
    
    Material CreateDefaultMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(1f, 0.5f, 0f, 0.8f);
        return mat;
    }
    
    public void Activate(Vector3 position, float radius, float damage, LayerMask damageLayer,
                        float lifetime, float scaleInDuration, float fadeOutDuration, float damageDelay)
    {
        if (isActive) return;
        
        isActive = true;
        hasDamaged = false;
        damagedTargets.Clear();
        
        // Position auf Boden projizieren
        SetupPosition(position);
        SetupCircle(radius);
        SetupCollider(radius);
        
        gameObject.SetActive(true);
        
        // Animationen
        AnimateIn(radius, scaleInDuration);
        ScheduleDamage(damage, damageLayer, damageDelay);
        ScheduleCleanup(lifetime, fadeOutDuration);
    }
    
    void SetupPosition(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
        {
            transform.position = hit.point + hit.normal * 0.02f; // Leicht über dem Boden
        }
        else
        {
            transform.position = position;
        }
    }
    
    void SetupCircle(float radius)
    {
        int segments = circleRenderer.positionCount - 1;
        Vector3[] circlePoints = new Vector3[segments + 1];
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            circlePoints[i] = transform.position + new Vector3(x, 0f, z);
        }
        
        circleRenderer.SetPositions(circlePoints);
        circleRenderer.startColor = circleRenderer.material.color;
        circleRenderer.endColor = circleRenderer.material.color;
    }
    
    void SetupCollider(float radius)
    {
        damageCollider.radius = radius;
    }
    
    void AnimateIn(float radius, float duration)
    {
        // Scale von 0 zu full size
        transform.localScale = Vector3.zero;
        
        scaleId = LeanTween.scale(gameObject, Vector3.one, duration)
            .setEase(LeanTweenType.easeOutBack)
            .setOnComplete(() => scaleId = -1).id;
    }
    
    void ScheduleDamage(float damage, LayerMask damageLayer, float delay)
    {
        LeanTween.delayedCall(gameObject, delay, () => DealDamage(damage, damageLayer));
    }
    
    void ScheduleCleanup(float lifetime, float fadeOutDuration)
    {
        float fadeStartTime = lifetime - fadeOutDuration;
        
        LeanTween.delayedCall(gameObject, fadeStartTime, () => {
            fadeId = LeanTween.value(gameObject, 1f, 0f, fadeOutDuration)
                .setEase(LeanTweenType.easeInQuad)
                .setOnUpdate((float alpha) => SetAlpha(alpha))
                .setOnComplete(() => Deactivate()).id;
        });
    }
    
    void DealDamage(float damage, LayerMask damageLayer)
    {
        if (hasDamaged || !isActive) return;
        
        hasDamaged = true;
        damageCollider.enabled = true;
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, damageCollider.radius, damageLayer);
        int damageCount = 0;
        
        foreach (var collider in colliders)
        {
            var damageable = collider.GetComponent<IDamageable>();
            if (damageable == null || damagedTargets.Contains(damageable)) continue;
            
            damageable.TakeDamage(damage);
            damagedTargets.Add(damageable);
            damageCount++;
        }
        
        manager.TriggerAreaDamageEvent(transform.position, damageCollider.radius, damageCount);
        damageCollider.enabled = false;
    }
    
    void SetAlpha(float alpha)
    {
        Color color = circleRenderer.material.color;
        color.a = alpha;
        circleRenderer.startColor = color;
        circleRenderer.endColor = color;
    }
    
    public void Deactivate()
    {
        if (!isActive) return;

        isActive = false;
        CancelAnimations();
        gameObject.SetActive(false);
    }
    
    public void ForceCleanup()
    {
        CancelAnimations();
        isActive = false;
    }
    
    void CancelAnimations()
    {
        if (scaleId >= 0)
        {
            LeanTween.cancel(scaleId);
            scaleId = -1;
        }
        
        if (fadeId >= 0)
        {
            LeanTween.cancel(fadeId);
            fadeId = -1;
        }
    }
    
    void OnDestroy()
    {
        CancelAnimations();
    }
}