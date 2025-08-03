using UnityEngine;

/// <summary>
/// Concrete Damageable Component - Kann direkt attached werden
/// </summary>
public class Damageable : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    
    private float currentHealth;
    private Renderer objectRenderer;
    private Color originalColor;
    private int flashId = -1;
    
    // Events
    public static event System.Action<Damageable, float> OnDamaged;
    public static event System.Action<Damageable> OnDestroyed;
    
    void Awake()
    {
        currentHealth = maxHealth;
        objectRenderer = GetComponent<Renderer>();
        
        if (objectRenderer)
            originalColor = objectRenderer.material.color;
    }
    
    public bool TakeDamage(float damage)
    {
        if (!IsAlive()) return false;
        
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        
        ShowDamageFlash();
        OnDamaged?.Invoke(this, damage);
        
        if (!IsAlive())
        {
            HandleDestruction();
            return false;
        }
        
        return true;
    }
    
    void ShowDamageFlash()
    {
        if (!objectRenderer) return;
        
        if (flashId >= 0) LeanTween.cancel(flashId);
        
        objectRenderer.material.color = damageColor;
        
        flashId = LeanTween.delayedCall(flashDuration, () => {
            if (objectRenderer)
                objectRenderer.material.color = originalColor;
            flashId = -1;
        }).id;
    }
    
    void HandleDestruction()
    {
        OnDestroyed?.Invoke(this);
        
        if (destroyOnDeath)
        {
            if (flashId >= 0) LeanTween.cancel(flashId);
            Destroy(gameObject);
        }
    }
    
    // IDamageable Implementation
    public float GetCurrentHealth() => currentHealth;
    public bool IsAlive() => currentHealth > 0f;
    public Vector3 GetPosition() => transform.position;
    
    // Public API
    public void SetMaxHealth(float health)
    {
        maxHealth = health;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }
    
    public float GetHealthPercentage() => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    
    void OnDestroy()
    {
        if (flashId >= 0) LeanTween.cancel(flashId);
    }
}

public interface IDamageable
{
    bool TakeDamage(float damage);
    float GetCurrentHealth();
    bool IsAlive();
    Vector3 GetPosition();
}
