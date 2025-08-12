using UnityEngine;

/// <summary>
/// Reference Manager - Findet und setzt Scene References beim Start
/// MUSS in jeder Scene sein!
/// </summary>
public class GameReferenceManager : MonoBehaviour
{
    [Header("Config Asset")]
    [SerializeField] private GameConfig gameConfig;
    
    [Header("Manual References (Optional)")]
    [SerializeField] private Transform sphereOverride;
    [SerializeField] private Camera cameraOverride;
    [SerializeField] private Transform playerOverride;
    
    [Header("Auto Setup")]
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private bool debugLog = true;
    
    private static GameReferenceManager _instance;
    
    void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        
        // Ensure config exists
        if (gameConfig == null)
        {
            gameConfig = GameConfig.Instance;
        }
        
        if (gameConfig == null)
        {
            Debug.LogError("No GameConfig found! Create: Assets/Resources/GameConfig.asset");
            return;
        }
        
        // Setup references
        SetupReferences();
    }
    
    void SetupReferences()
    {
        // Manual overrides haben Priorität
        Transform sphere = sphereOverride;
        Camera cam = cameraOverride;
        Transform player = playerOverride;
        
        if (autoFindReferences)
        {
            // Auto-find wenn nicht manuell gesetzt
            if (sphere == null)
            {
                GameObject sphereGO = GameObject.FindWithTag("Sphere");
                if (sphereGO == null)
                {
                    sphereGO = GameObject.Find("Sphere");
                    if (sphereGO != null && debugLog)
                    {
                        Debug.LogWarning("Sphere found by name - add 'Sphere' tag for better performance!");
                    }
                }
                sphere = sphereGO?.transform;
            }
            
            if (cam == null)
            {
                cam = Camera.main;
            }
            
            if (player == null)
            {
                GameObject playerGO = GameObject.FindWithTag("Player");
                if (playerGO == null)
                {
                    playerGO = GameObject.Find("Player");
                    if (playerGO != null && debugLog)
                    {
                        Debug.LogWarning("Player found by name - add 'Player' tag for better performance!");
                    }
                }
                player = playerGO?.transform;
            }
        }
        
        // Set in static reference manager
        GameReferences.SphereCenter = sphere;
        GameReferences.MainCamera = cam;
        GameReferences.PlayerTransform = player;
        
        if (debugLog)
        {
            Debug.Log($"[ReferenceManager] Setup complete:\n" +
                     $"  Sphere: {sphere?.name ?? "NOT FOUND"}\n" +
                     $"  Camera: {cam?.name ?? "NOT FOUND"}\n" +
                     $"  Player: {player?.name ?? "NOT FOUND"}");
        }
    }
    
    [ContextMenu("Refresh References")]
    public void RefreshReferences()
    {
        SetupReferences();
    }
    
    void OnValidate()
    {
        // Im Editor: Zeige Warnings für fehlende References
        #if UNITY_EDITOR
        if (autoFindReferences) return;
        
        if (sphereOverride == null)
            Debug.LogWarning("Sphere reference missing - will try auto-find at runtime");
        if (cameraOverride == null)
            Debug.LogWarning("Camera reference missing - will use Camera.main");
        if (playerOverride == null)
            Debug.LogWarning("Player reference missing - will try auto-find at runtime");
        #endif
    }
    
    void OnDrawGizmos()
    {
        if (gameConfig == null) return;
        
        // Visualize sphere
        Transform center = sphereOverride ?? GameReferences.SphereCenter;
        if (center != null)
        {
            Gizmos.color = gameConfig.sphereGizmoColor;
            Gizmos.DrawWireSphere(center.position, gameConfig.sphereRadius);
        }
    }
}