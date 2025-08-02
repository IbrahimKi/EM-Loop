using UnityEngine;

public class CircleTarget : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private int priority = 0;
    [SerializeField] private int pointValue = 100;
    [SerializeField] private bool isActive = true;
    [SerializeField] private float detectionRadius = 1f;  // Radius für Raycast-Detection
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject selectionEffect;
    [SerializeField] private Color highlightColor = Color.yellow;
    
    private Renderer targetRenderer;
    private Color originalColor;
    
    public int Priority => priority;
    public int PointValue => pointValue;
    public bool IsActive => isActive;
    public float DetectionRadius => detectionRadius;
    
    void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer)
            originalColor = targetRenderer.material.color;
        
        // Stelle sicher, dass Collider für Raycast-Detection vorhanden ist
        if (!GetComponent<Collider>())
        {
            var sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.radius = detectionRadius;
            sphereCol.isTrigger = true;
        }
    }
    
    void OnEnable()
    {
        CircleManager.OnTargetSelected += OnAnyTargetSelected;
    }
    
    void OnDisable()
    {
        CircleManager.OnTargetSelected -= OnAnyTargetSelected;
    }
    
    public void SetActive(bool active) => isActive = active;
    public void SetPriority(int newPriority) => priority = newPriority;
    public void SetPointValue(int newValue) => pointValue = newValue;
    
    public static event System.Action<CircleTarget, Vector3, float> OnTargetSelected; // + speedBonus
    
    public void SelectTarget(Vector3 selectionCenter, float speedBonus = 1f)
    {
        if (!isActive) return;
        
        // Hole Player Speed Multiplier (Placeholder)
        float playerSpeedMultiplier = GetPlayerSpeedMultiplier();
        float finalBonus = speedBonus * playerSpeedMultiplier;
        int bonusPoints = Mathf.RoundToInt(pointValue * finalBonus);
        
        OnTargetSelected?.Invoke(this, selectionCenter, finalBonus);
        ShowSelectionEffect();
        Debug.Log($"Target {name} selected - Priority: {priority}, Base: {pointValue}, Bonus: {bonusPoints}, Total: {pointValue + bonusPoints}");
    }
    
    void OnAnyTargetSelected(CircleTarget target, Vector3 center, float speedBonus)
    {
        if (target == this)
        {
            ShowSelectionEffect();
        }
        else
        {
            HideSelectionEffect();
        }
    }
    
    float GetPlayerSpeedMultiplier()
    {
        // TODO: Hier Player-Component oder GameManager abfragen
        // Placeholder: Spieler-Speed-Variable
        return 1.5f; // Beispiel: 1.5x Speed Multiplier
    }
    
    void ShowSelectionEffect()
    {
        if (selectionEffect)
            selectionEffect.SetActive(true);
        
        if (targetRenderer)
            targetRenderer.material.color = highlightColor;
    }
    
    void HideSelectionEffect()
    {
        if (selectionEffect)
            selectionEffect.SetActive(false);
        
        if (targetRenderer)
            targetRenderer.material.color = originalColor;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}