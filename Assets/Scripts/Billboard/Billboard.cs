using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Billboard : MonoBehaviour
{
    [Header("Billboard")]
    [SerializeField] private bool autoRegister = true;
    [SerializeField] private bool constrainY = false;
    
    [Header("Sphere Placement")]
    [SerializeField] private bool autoPlaceOnSphere = true;
    [SerializeField] private float sphereRadiusOverride = -1f; // -1 = use config
    [SerializeField] private Transform sphereCenterOverride;
    
    // Components
    protected SpriteRenderer spriteRenderer;
    private Camera targetCamera;
    private Transform cameraTransform;
    
    // Config & References
    private GameConfig config;
    
    // Billboard Cache
    private Vector3 lastCameraPosition;
    private bool needsBillboardUpdate = true;
    
    // Events
    public static System.Action<Billboard> OnBillboardRegistered;
    public static System.Action<Billboard> OnBillboardUnregistered;
    
    void Awake()
    {
        config = GameConfig.Instance;
        spriteRenderer = GetComponent<SpriteRenderer>();
        targetCamera = GameReferences.MainCamera;
        
        if (targetCamera != null)
        {
            cameraTransform = targetCamera.transform;
        }
        
        if (autoPlaceOnSphere)
        {
            PlaceOnSphere();
        }
        
        OnAwakeOverride();
    }
    
    void Start()
    {
        if (autoRegister)
        {
            Register();
        }
        
        OnStartOverride();
    }
    
    void OnEnable()
    {
        if (autoRegister)
        {
            Register();
        }
        
        OnEnableOverride();
    }
    
    void OnDisable()
    {
        Unregister();
        OnDisableOverride();
    }
    
    void OnDestroy()
    {
        Unregister();
        OnDestroyOverride();
    }
    
    void Update()
    {
        UpdateBillboard();
        OnUpdateOverride();
    }
    
    void UpdateBillboard()
    {
        if (cameraTransform == null) return;
        
        Vector3 cameraPos = cameraTransform.position;
        
        // Performance: Use config threshold
        float threshold = config?.billboardUpdateThreshold ?? 0.001f;
        if (Vector3.SqrMagnitude(cameraPos - lastCameraPosition) < threshold && !needsBillboardUpdate)
            return;
        
        Vector3 direction = (cameraPos - transform.position).normalized;
        
        // Use config setting for Y constraint
        bool shouldConstrainY = constrainY || (config?.billboardConstrainY ?? false);
        if (shouldConstrainY)
        {
            direction.y = 0;
            direction.Normalize();
        }
        
        transform.rotation = Quaternion.LookRotation(direction);
        
        lastCameraPosition = cameraPos;
        needsBillboardUpdate = false;
    }
    
    void PlaceOnSphere()
    {
        Transform sphereCenter = GetSphereCenter();
        if (sphereCenter == null) return;
        
        float radius = GetSphereRadius();
        Vector3 direction = (transform.position - sphereCenter.position).normalized;
        Vector3 surfacePosition = sphereCenter.position + direction * radius;
        
        transform.position = surfacePosition;
        needsBillboardUpdate = true;
    }
    
    Transform GetSphereCenter()
    {
        if (sphereCenterOverride != null)
            return sphereCenterOverride;
            
        if (GameReferences.SphereCenter != null)
            return GameReferences.SphereCenter;
            
        // Fallback: Check parent for SphereBillboardPlacer
        var placer = GetComponentInParent<SphereBillboardPlacer>();
        if (placer != null)
            return placer.transform;
            
        return null;
    }
    
    float GetSphereRadius()
    {
        if (sphereRadiusOverride > 0)
            return sphereRadiusOverride;
            
        return config?.sphereRadius ?? 5f;
    }
    
    #region Registration System
    
    public void Register()
    {
        BillboardManager.RegisterBillboard(this);
        OnBillboardRegistered?.Invoke(this);
    }
    
    public void Unregister()
    {
        BillboardManager.UnregisterBillboard(this);
        OnBillboardUnregistered?.Invoke(this);
    }
    
    #endregion
    
    #region Public API
    
    public void SetSphereRadius(float radius)
    {
        sphereRadiusOverride = radius;
        if (autoPlaceOnSphere)
        {
            PlaceOnSphere();
        }
    }
    
    public void SetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
        cameraTransform = newCamera != null ? newCamera.transform : null;
        needsBillboardUpdate = true;
    }
    
    public void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
        }
    }
    
    public Sprite GetSprite()
    {
        return spriteRenderer?.sprite;
    }
    
    public void SetSphereCenter(Transform center)
    {
        sphereCenterOverride = center;
        needsBillboardUpdate = true;
    }
    
    public void ForceUpdate()
    {
        needsBillboardUpdate = true;
    }
    
    public float GetCurrentSphereRadius()
    {
        return GetSphereRadius();
    }
    
    public Transform GetCurrentSphereCenter()
    {
        return GetSphereCenter();
    }
    
    #endregion
    
    #region Virtual Methods für Erweiterungen
    
    protected virtual void OnAwakeOverride() { }
    protected virtual void OnStartOverride() { }
    protected virtual void OnEnableOverride() { }
    protected virtual void OnDisableOverride() { }
    protected virtual void OnDestroyOverride() { }
    protected virtual void OnUpdateOverride() { }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Place On Sphere")]
    void DebugPlaceOnSphere() => PlaceOnSphere();
    
    [ContextMenu("Force Update")]
    void DebugForceUpdate() => ForceUpdate();
    
    [ContextMenu("Debug Sphere Info")]
    void DebugSphereInfo()
    {
        Debug.Log($"[{name}] Sphere Info:\n" +
                 $"  Center: {GetCurrentSphereCenter()?.name ?? "NULL"}\n" +
                 $"  Radius: {GetCurrentSphereRadius()}\n" +
                 $"  Position: {transform.position}\n" +
                 $"  Config Radius: {config?.sphereRadius ?? 0}");
    }
    
    #endregion
}