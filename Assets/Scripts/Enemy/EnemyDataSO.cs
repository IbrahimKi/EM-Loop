using UnityEngine;

/// <summary>
/// ScriptableObject für Enemy Configuration
/// Definiert Stats und Visuals für Enemy Types
/// </summary>
[CreateAssetMenu(fileName = "New Enemy Data", menuName = "Enemies/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string enemyName = "Basic Enemy";
    public EnemySize enemySize = EnemySize.Medium;
    
    [Header("Stats")]
    public float maxHealth = 100f;
    public int energyValue = 10;
    public float attackRadius = 3f;
    
    [Header("Visual Settings")]
    public Vector3 scale = Vector3.one;
    public Color inactiveColor = Color.gray;
    public Color preparingColor = Color.yellow;
    public Color attackingColor = Color.red;
    public Color idleColor = Color.blue; // Für Large Enemy idle
    
    [Header("Large Enemy Settings")]
    [Range(0.1f, 0.9f)]
    public float dangerHealthThreshold = 0.3f;
    
    [Header("Audio")]
    public AudioClip attackSound;
    public AudioClip hitSound;
    public AudioClip deathSound;
    
    [Header("Effects")]
    public GameObject hitEffect;
    public GameObject deathEffect;
    public GameObject dangerEffect;
    
    #if UNITY_EDITOR
    [Header("Editor Preview")]
    [SerializeField] private bool showPreview = true;
    
    void OnValidate()
    {
        // Auto-setup based on size
        switch (enemySize)
        {
            case EnemySize.Small:
                if (maxHealth == 100f) maxHealth = 50f; // Only auto-set if still default
                if (energyValue == 10) energyValue = 5;
                if (attackRadius == 3f) attackRadius = 2f;
                scale = Vector3.one * 0.7f;
                break;
                
            case EnemySize.Medium:
                if (maxHealth == 50f || maxHealth == 200f) maxHealth = 100f;
                if (energyValue == 5 || energyValue == 20) energyValue = 10;
                if (attackRadius == 2f || attackRadius == 4f) attackRadius = 3f;
                scale = Vector3.one * 1f;
                break;
                
            case EnemySize.Large:
                if (maxHealth == 100f) maxHealth = 200f;
                if (energyValue == 10) energyValue = 20;
                if (attackRadius == 3f) attackRadius = 4f;
                scale = Vector3.one * 1.5f;
                break;
        }
    }
    #endif
}