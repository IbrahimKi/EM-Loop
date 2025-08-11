using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ParallaxLayer
{
    [Header("Layer Config")]
    public string layerName = "Layer";
    public Sprite sprite;
    public float distanceFromCamera = 10f;
    public bool followCameraRotation = true;
    public Vector3 localOffset = Vector3.zero;
    
    [Header("Parallax Settings")]
    [Range(0f, 1f)] public float parallaxStrength = 1f;
    public bool useVerticalParallax = true;
    
    [Header("Visual")]
    public Color tint = Color.white;
    public Vector2 scale = Vector2.one;
    public int sortingOrder = 0;
    
    [Header("Runtime")]
    [ReadOnly] public GameObject layerObject;
    [ReadOnly] public SpriteRenderer spriteRenderer;
    [ReadOnly] public Billboard billboard;
}

public enum ParallaxMode
{
    FollowCamera,       // Elemente folgen Kamera-Rotation
    WorldStatic,        // Statisch in Weltkoordinaten
    Mixed              // Pro Layer konfigurierbar
}

public class ParallaxBillboard : Billboard
{
    [Header("Parallax Configuration")]
    [SerializeField] private ParallaxMode parallaxMode = ParallaxMode.Mixed;
    [SerializeField] private List<ParallaxLayer> parallaxLayers = new List<ParallaxLayer>();
    [SerializeField] private bool autoCreateLayers = true;
    [SerializeField] private bool updateInRealtime = true;
    
    [Header("Performance")]
    [SerializeField] private bool useBulkUpdate = true;
    [SerializeField] private float updateThreshold = 0.01f;
    
    [Header("Debug")]
    [SerializeField] private bool showLayerGizmos = false;
    [SerializeField] private bool enableDebugLogs = false;
    
    // Runtime
    private Camera targetCamera;
    private Transform cameraTransform;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private bool hasParallaxChanged = true;
    
    // Events
    public static System.Action<ParallaxBillboard> OnParallaxCreated;
    public static System.Action<ParallaxBillboard> OnParallaxDestroyed;
    public static System.Action<ParallaxBillboard, int> OnLayerCountChanged;
    
    protected override void OnAwakeOverride()
    {
        targetCamera = Camera.main;
        if (targetCamera != null)
        {
            cameraTransform = targetCamera.transform;
            lastCameraPosition = cameraTransform.position;
            lastCameraRotation = cameraTransform.rotation;
        }
        
        // Don't auto-create in Awake, do it in Start
        ValidateSetup();
    }
    
    protected override void OnStartOverride()
    {
        if (autoCreateLayers)
        {
            CreateParallaxLayers();
        }
        
        UpdateAllLayers();
        OnParallaxCreated?.Invoke(this);
    }
    
    protected override void OnUpdateOverride()
    {
        if (updateInRealtime && HasCameraChanged())
        {
            if (useBulkUpdate)
            {
                UpdateAllLayers();
            }
            else
            {
                UpdateLayersIndividually();
            }
            
            UpdateCachedCameraState();
        }
    }
    
    protected override void OnDestroyOverride()
    {
        CleanupParallaxLayers();
        OnParallaxDestroyed?.Invoke(this);
    }
    
    bool HasCameraChanged()
    {
        if (cameraTransform == null) return false;
        
        Vector3 currentPos = cameraTransform.position;
        Quaternion currentRot = cameraTransform.rotation;
        
        bool posChanged = Vector3.SqrMagnitude(currentPos - lastCameraPosition) > updateThreshold * updateThreshold;
        bool rotChanged = Quaternion.Angle(currentRot, lastCameraRotation) > updateThreshold;
        
        return posChanged || rotChanged;
    }
    
    void UpdateCachedCameraState()
    {
        if (cameraTransform == null) return;
        
        lastCameraPosition = cameraTransform.position;
        lastCameraRotation = cameraTransform.rotation;
    }
    
    #region Layer Management
    
    void CreateParallaxLayers()
    {
        CleanupParallaxLayers();
        
        for (int i = 0; i < parallaxLayers.Count; i++)
        {
            CreateLayerObject(i);
        }
        
        LogDebug($"Created {parallaxLayers.Count} parallax layers");
        OnLayerCountChanged?.Invoke(this, parallaxLayers.Count);
    }
    
    void CreateLayerObject(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= parallaxLayers.Count) return;
        
        var layer = parallaxLayers[layerIndex];
        if (layer.sprite == null) return;
        
        // Create layer GameObject
        GameObject layerObj = new GameObject($"{layer.layerName}_{layerIndex:00}");
        layerObj.transform.SetParent(transform);
        
        // Setup SpriteRenderer
        var spriteRenderer = layerObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = layer.sprite;
        spriteRenderer.color = layer.tint;
        spriteRenderer.sortingOrder = layer.sortingOrder;
        
        // Setup Billboard component
        var billboard = layerObj.AddComponent<Billboard>();
        billboard.SetCamera(targetCamera);
        
        // Apply scale
        layerObj.transform.localScale = new Vector3(layer.scale.x, layer.scale.y, 1f);
        
        // Cache references
        layer.layerObject = layerObj;
        layer.spriteRenderer = spriteRenderer;
        layer.billboard = billboard;
        
        // Initial positioning
        UpdateLayerPosition(layerIndex);
    }
    
    void CleanupParallaxLayers()
    {
        foreach (var layer in parallaxLayers)
        {
            if (layer.layerObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(layer.layerObject);
                }
                else
                {
                    DestroyImmediate(layer.layerObject);
                }
            }
            
            layer.layerObject = null;
            layer.spriteRenderer = null;
            layer.billboard = null;
        }
    }
    
    #endregion
    
    #region Parallax Updates
    
    void UpdateAllLayers()
    {
        if (cameraTransform == null) return;
        
        Vector3 cameraPos = cameraTransform.position;
        Quaternion cameraRot = cameraTransform.rotation;
        
        for (int i = 0; i < parallaxLayers.Count; i++)
        {
            UpdateLayerPosition(i, cameraPos, cameraRot);
        }
    }
    
    void UpdateLayersIndividually()
    {
        for (int i = 0; i < parallaxLayers.Count; i++)
        {
            UpdateLayerPosition(i);
        }
    }
    
    void UpdateLayerPosition(int layerIndex)
    {
        if (cameraTransform == null) return;
        UpdateLayerPosition(layerIndex, cameraTransform.position, cameraTransform.rotation);
    }
    
    void UpdateLayerPosition(int layerIndex, Vector3 cameraPos, Quaternion cameraRot)
    {
        if (layerIndex < 0 || layerIndex >= parallaxLayers.Count) return;
        
        var layer = parallaxLayers[layerIndex];
        if (layer.layerObject == null) return;
        
        // Determine if this layer follows camera rotation
        bool shouldFollowRotation = parallaxMode switch
        {
            ParallaxMode.FollowCamera => true,
            ParallaxMode.WorldStatic => false,
            ParallaxMode.Mixed => layer.followCameraRotation,
            _ => true
        };
        
        // Calculate parallax offset
        Vector3 parallaxOffset = CalculateParallaxOffset(layer, cameraPos);
        
        // Base position: camera + distance in forward direction
        Vector3 baseDirection = shouldFollowRotation ? cameraRot * Vector3.forward : Vector3.forward;
        Vector3 basePosition = cameraPos + baseDirection * layer.distanceFromCamera;
        
        // Apply parallax and local offset
        Vector3 finalPosition = basePosition + parallaxOffset + layer.localOffset;
        
        layer.layerObject.transform.position = finalPosition;
        
        // Handle rotation
        if (shouldFollowRotation && layer.billboard != null)
        {
            // Billboard will handle rotation automatically
        }
        else if (layer.billboard != null)
        {
            // Disable billboard behavior for static world objects
            layer.billboard.enabled = false;
        }
    }
    
    Vector3 CalculateParallaxOffset(ParallaxLayer layer, Vector3 cameraPos)
    {
        // Simple parallax based on camera movement
        Vector3 cameraMovement = cameraPos - transform.position;
        
        float parallaxFactor = layer.parallaxStrength * (1f - (layer.distanceFromCamera / 50f));
        
        Vector3 parallaxOffset = cameraMovement * parallaxFactor;
        
        if (!layer.useVerticalParallax)
        {
            parallaxOffset.y = 0;
        }
        
        return parallaxOffset;
    }
    
    #endregion
    
    #region Public API
    
    public void AddLayer(ParallaxLayer newLayer)
    {
        if (newLayer == null) return;
        
        parallaxLayers.Add(newLayer);
        
        if (Application.isPlaying && autoCreateLayers)
        {
            CreateLayerObject(parallaxLayers.Count - 1);
        }
        
        OnLayerCountChanged?.Invoke(this, parallaxLayers.Count);
        LogDebug($"Added layer: {newLayer.layerName}");
    }
    
    public void RemoveLayer(int index)
    {
        if (index < 0 || index >= parallaxLayers.Count) return;
        
        var layer = parallaxLayers[index];
        if (layer.layerObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(layer.layerObject);
            }
            else
            {
                DestroyImmediate(layer.layerObject);
            }
        }
        
        parallaxLayers.RemoveAt(index);
        OnLayerCountChanged?.Invoke(this, parallaxLayers.Count);
        LogDebug($"Removed layer at index {index}");
    }
    
    public void SetParallaxMode(ParallaxMode mode)
    {
        if (parallaxMode != mode)
        {
            parallaxMode = mode;
            UpdateAllLayers();
            LogDebug($"Parallax mode changed to: {mode}");
        }
    }
    
    public void SetLayerDistance(int layerIndex, float distance)
    {
        if (layerIndex < 0 || layerIndex >= parallaxLayers.Count) return;
        
        parallaxLayers[layerIndex].distanceFromCamera = Mathf.Max(0.1f, distance);
        UpdateLayerPosition(layerIndex);
    }
    
    public void SetLayerFollowRotation(int layerIndex, bool follow)
    {
        if (layerIndex < 0 || layerIndex >= parallaxLayers.Count) return;
        
        parallaxLayers[layerIndex].followCameraRotation = follow;
        UpdateLayerPosition(layerIndex);
    }
    
    public void RefreshAllLayers()
    {
        if (Application.isPlaying)
        {
            CreateParallaxLayers();
        }
    }
    
    public int GetLayerCount() => parallaxLayers.Count;
    
    public ParallaxLayer GetLayer(int index)
    {
        if (index < 0 || index >= parallaxLayers.Count) return null;
        return parallaxLayers[index];
    }
    
    public List<ParallaxLayer> GetAllLayers()
    {
        return new List<ParallaxLayer>(parallaxLayers);
    }
    
    #endregion
    
    #region Validation & Debug
    
    void ValidateSetup()
    {
        if (parallaxLayers == null || parallaxLayers.Count == 0)
        {
            LogDebug("No parallax layers configured");
            return;
        }
        
        for (int i = 0; i < parallaxLayers.Count; i++)
        {
            var layer = parallaxLayers[i];
            if (layer.sprite == null)
            {
                Debug.LogWarning($"Layer {i} ({layer.layerName}) has no sprite assigned!");
            }
            
            if (layer.distanceFromCamera <= 0)
            {
                layer.distanceFromCamera = 1f;
                Debug.LogWarning($"Layer {i} distance was <= 0, set to 1.0");
            }
        }
    }
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ParallaxBillboard:{name}] {message}");
        }
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Create Parallax Layers")]
    void DebugCreateLayers() => CreateParallaxLayers();
    
    [ContextMenu("Update All Layers")]
    void DebugUpdateLayers() => UpdateAllLayers();
    
    [ContextMenu("Validate Setup")]
    void DebugValidateSetup() => ValidateSetup();
    
    [ContextMenu("Add Test Layer")]
    void DebugAddTestLayer()
    {
        var testLayer = new ParallaxLayer
        {
            layerName = $"TestLayer_{parallaxLayers.Count}",
            distanceFromCamera = 5f + parallaxLayers.Count * 2f,
            followCameraRotation = true,
            parallaxStrength = 0.5f,
            tint = Color.white,
            scale = Vector2.one
        };
        
        AddLayer(testLayer);
    }
    
    [ContextMenu("Debug Layer Positions")]
    void DebugLayerPositions()
    {
        if (cameraTransform == null)
        {
            Debug.Log("No camera found!");
            return;
        }
        
        Debug.Log($"Camera at: {cameraTransform.position}");
        
        for (int i = 0; i < parallaxLayers.Count; i++)
        {
            var layer = parallaxLayers[i];
            if (layer.layerObject != null)
            {
                Debug.Log($"Layer {i} ({layer.layerName}): " +
                         $"Position={layer.layerObject.transform.position}, " +
                         $"Distance={layer.distanceFromCamera}, " +
                         $"Active={layer.layerObject.activeInHierarchy}, " +
                         $"Sprite={layer.sprite?.name}");
            }
            else
            {
                Debug.Log($"Layer {i} ({layer.layerName}): NO GAMEOBJECT");
            }
        }
    }
    
    #endregion
    
    #region Gizmos
    
    void OnDrawGizmosSelected()
    {
        if (!showLayerGizmos || parallaxLayers == null) return;
        
        Vector3 center = transform.position;
        
        for (int i = 0; i < parallaxLayers.Count; i++)
        {
            var layer = parallaxLayers[i];
            
            // Layer distance visualization
            Gizmos.color = Color.Lerp(Color.blue, Color.red, (float)i / parallaxLayers.Count);
            Gizmos.DrawWireSphere(center, layer.distanceFromCamera);
            
            // Layer position if created
            if (layer.layerObject != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(layer.layerObject.transform.position, Vector3.one * 0.5f);
                Gizmos.DrawLine(center, layer.layerObject.transform.position);
            }
        }
        
        // Camera connection
        if (cameraTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(center, cameraTransform.position);
        }
    }
    
    #endregion
}