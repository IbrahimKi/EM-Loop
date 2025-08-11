using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Billboard : MonoBehaviour
{
    [Header("Billboard")]
    [SerializeField] private bool autoRegister = true;
    [SerializeField] private bool constrainY = false;
    
    [Header("Sphere Placement")]
    [SerializeField] private bool autoPlaceOnSphere = true;
    [SerializeField] private float sphereRadius = 5f;
    [SerializeField] private Transform sphereCenter;
    
    // Components
    protected SpriteRenderer spriteRenderer;
    private Camera targetCamera;
    private Transform cameraTransform;
    
    // Billboard Cache
    private Vector3 lastCameraPosition;
    private bool needsBillboardUpdate = true;
    
    // Events
    public static System.Action<Billboard> OnBillboardRegistered;
    public static System.Action<Billboard> OnBillboardUnregistered;
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        targetCamera = Camera.main;
        
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
        
        // Performance: Nur Update wenn Kamera bewegt
        if (Vector3.SqrMagnitude(cameraPos - lastCameraPosition) < 0.001f && !needsBillboardUpdate)
            return;
        
        Vector3 direction = (cameraPos - transform.position).normalized;
        
        if (constrainY)
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
        if (sphereCenter == null)
        {
            var placer = GetComponentInParent<SphereBillboardPlacer>();
            if (placer != null)
            {
                sphereCenter = placer.transform;
            }
        }
        
        if (sphereCenter == null) return;
        
        Vector3 direction = (transform.position - sphereCenter.position).normalized;
        Vector3 surfacePosition = sphereCenter.position + direction * sphereRadius;
        
        transform.position = surfacePosition;
        needsBillboardUpdate = true;
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
        sphereRadius = radius;
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
        sphereCenter = center;
        needsBillboardUpdate = true;
    }
    
    public void ForceUpdate()
    {
        needsBillboardUpdate = true;
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
    
    #endregion
}