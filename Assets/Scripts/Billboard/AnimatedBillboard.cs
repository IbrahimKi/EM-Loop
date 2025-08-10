using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AnimatedBillboard : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Sprite[] animationSprites;
    [SerializeField] private bool useGlobalRhythm = true;
    [SerializeField] private float manualFrameRate = 1f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool autoStart = true;
    
    [Header("Billboard")]
    [SerializeField] private bool autoRegister = true;
    [SerializeField] private bool constrainY = false;
    
    [Header("Sphere Placement")]
    [SerializeField] private bool autoPlaceOnSphere = true;
    [SerializeField] private float sphereRadius = 5f;
    [SerializeField] private Transform sphereCenter;
    
    [Header("Current State")]
    [SerializeField, ReadOnly] private int currentFrame = 0;
    [SerializeField, ReadOnly] private bool isPlaying = false;
    [SerializeField, ReadOnly] private bool isPaused = false;
    
    // Components
    private SpriteRenderer spriteRenderer;
    private Camera targetCamera;
    private Transform cameraTransform;
    
    // Animation
    private float manualTimer = 0f;
    
    // Billboard Cache
    private Vector3 lastCameraPosition;
    private bool needsBillboardUpdate = true;
    
    public bool IsPlaying => isPlaying;
    public int CurrentFrame => currentFrame;
    public int TotalFrames => animationSprites?.Length ?? 0;
    
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
        
        ValidateSetup();
    }
    
    void Start()
    {
        if (autoRegister)
        {
            BillboardManager.RegisterAnimatedBillboard(this);
        }
        
        if (autoStart)
        {
            Play();
        }
        
        SubscribeToRhythm();
    }
    
    void OnEnable()
    {
        if (autoRegister)
        {
            BillboardManager.RegisterBillboard(this);
        }
        
        if (isPlaying && !isPaused)
        {
            SubscribeToRhythm();
        }
    }
    
    void OnDisable()
    {
        BillboardManager.UnregisterBillboard(this);
        UnsubscribeFromRhythm();
    }
    
    void OnDestroy()
    {
        BillboardManager.UnregisterBillboard(this);
        UnsubscribeFromRhythm();
    }
    
    void Update()
    {
        // Billboard Update - nur wenn Kamera bewegt
        UpdateBillboard();
        
        // Manual Animation
        if (!useGlobalRhythm && isPlaying && !isPaused)
        {
            manualTimer += Time.deltaTime;
            
            if (manualTimer >= manualFrameRate)
            {
                NextFrame();
                manualTimer = 0f;
            }
        }
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
    
    void ValidateSetup()
    {
        if (animationSprites == null || animationSprites.Length == 0)
        {
            Debug.LogWarning($"No animation sprites assigned to {name}!");
            return;
        }
        
        for (int i = 0; i < animationSprites.Length; i++)
        {
            if (animationSprites[i] == null)
            {
                Debug.LogWarning($"Null sprite at index {i} in {name}!");
            }
        }
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
    
    #region Rhythm System
    
    void SubscribeToRhythm()
    {
        if (!useGlobalRhythm) return;
        
        GlobalRhythmManager.OnBeatTick += OnRhythmBeat;
        GlobalRhythmManager.OnRhythmPaused += OnRhythmPaused;
        GlobalRhythmManager.OnRhythmResumed += OnRhythmResumed;
        GlobalRhythmManager.OnRhythmStopped += OnRhythmStopped;
    }
    
    void UnsubscribeFromRhythm()
    {
        if (!useGlobalRhythm) return;
        
        GlobalRhythmManager.OnBeatTick -= OnRhythmBeat;
        GlobalRhythmManager.OnRhythmPaused -= OnRhythmPaused;
        GlobalRhythmManager.OnRhythmResumed -= OnRhythmResumed;
        GlobalRhythmManager.OnRhythmStopped -= OnRhythmStopped;
    }
    
    void OnRhythmBeat(int frame)
    {
        if (!isPlaying || isPaused) return;
        NextFrame();
    }
    
    void OnRhythmPaused()
    {
        if (isPlaying) Pause();
    }
    
    void OnRhythmResumed()
    {
        if (isPaused) Resume();
    }
    
    void OnRhythmStopped()
    {
        Stop();
    }
    
    #endregion
    
    #region Animation Control
    
    public void Play()
    {
        isPlaying = true;
        isPaused = false;
        manualTimer = 0f;
        
        if (useGlobalRhythm)
        {
            SubscribeToRhythm();
        }
        
        UpdateSprite();
    }
    
    public void Stop()
    {
        isPlaying = false;
        isPaused = false;
        currentFrame = 0;
        manualTimer = 0f;
        
        UnsubscribeFromRhythm();
        UpdateSprite();
    }
    
    public void Pause()
    {
        if (!isPlaying) return;
        
        isPaused = true;
        UnsubscribeFromRhythm();
    }
    
    public void Resume()
    {
        if (!isPlaying || !isPaused) return;
        
        isPaused = false;
        manualTimer = 0f;
        
        if (useGlobalRhythm)
        {
            SubscribeToRhythm();
        }
    }
    
    public void NextFrame()
    {
        if (animationSprites == null || animationSprites.Length == 0) return;
        
        currentFrame++;
        
        if (currentFrame >= animationSprites.Length)
        {
            if (loop)
            {
                currentFrame = 0;
            }
            else
            {
                currentFrame = animationSprites.Length - 1;
                Stop();
                return;
            }
        }
        
        UpdateSprite();
    }
    
    public void SetFrame(int frame)
    {
        if (animationSprites == null || animationSprites.Length == 0) return;
        
        currentFrame = Mathf.Clamp(frame, 0, animationSprites.Length - 1);
        UpdateSprite();
    }
    
    void UpdateSprite()
    {
        if (spriteRenderer == null || animationSprites == null || animationSprites.Length == 0) return;
        
        Sprite spriteToSet = currentFrame < animationSprites.Length ? animationSprites[currentFrame] : null;
        spriteRenderer.sprite = spriteToSet;
    }
    
    #endregion
    
    #region Public API
    
    public void SetAnimationSprites(Sprite[] sprites)
    {
        animationSprites = sprites;
        currentFrame = 0;
        ValidateSetup();
        
        if (isPlaying)
        {
            UpdateSprite();
        }
    }
    
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
    
    // Billboard Registration Methods - für Kompatibilität mit BillboardManager
    public void Register() => BillboardManager.RegisterBillboard(this);
    public void Unregister() => BillboardManager.UnregisterBillboard(this);
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Place On Sphere")]
    void DebugPlaceOnSphere() => PlaceOnSphere();
    
    [ContextMenu("Play Animation")]
    void DebugPlay() => Play();
    
    [ContextMenu("Stop Animation")]
    void DebugStop() => Stop();
    
    [ContextMenu("Next Frame")]
    void DebugNextFrame() => NextFrame();
    
    #endregion
}