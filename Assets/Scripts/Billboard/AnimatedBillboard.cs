using UnityEngine;

public class AnimatedBillboard : Billboard
{
    [Header("Animation")]
    [SerializeField] private Sprite[] animationSprites;
    [SerializeField] private bool useGlobalRhythm = true;
    [SerializeField] private float manualFrameRate = 1f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool autoStart = true;
    
    [Header("Current State")]
    [SerializeField, ReadOnly] private int currentFrame = 0;
    [SerializeField, ReadOnly] private bool isPlaying = false;
    [SerializeField, ReadOnly] private bool isPaused = false;
    
    // Animation
    private float manualTimer = 0f;
    
    // Config
    private GameConfig config;
    
    // Events
    public static System.Action<AnimatedBillboard> OnAnimationStarted;
    public static System.Action<AnimatedBillboard> OnAnimationStopped;
    public static System.Action<AnimatedBillboard, int> OnFrameChanged;
    
    public bool IsPlaying => isPlaying;
    public int CurrentFrame => currentFrame;
    public int TotalFrames => animationSprites?.Length ?? 0;
    
    protected override void OnAwakeOverride()
    {
        config = GameConfig.Instance;
        ValidateSetup();
    }
    
    protected override void OnStartOverride()
    {
        if (autoStart)
        {
            Play();
        }
        
        SubscribeToRhythm();
    }
    
    protected override void OnEnableOverride()
    {
        if (isPlaying && !isPaused)
        {
            SubscribeToRhythm();
        }
    }
    
    protected override void OnDisableOverride()
    {
        UnsubscribeFromRhythm();
    }
    
    protected override void OnDestroyOverride()
    {
        UnsubscribeFromRhythm();
    }
    
    protected override void OnUpdateOverride()
    {
        // Manual Animation
        if (!useGlobalRhythm && isPlaying && !isPaused)
        {
            manualTimer += Time.deltaTime;
            
            float frameRate = GetEffectiveFrameRate();
            if (manualTimer >= frameRate)
            {
                NextFrame();
                manualTimer = 0f;
            }
        }
    }
    
    float GetEffectiveFrameRate()
    {
        // Nutze manualFrameRate falls gesetzt, sonst Rhythm-basiert
        if (manualFrameRate > 0)
            return manualFrameRate;
            
        // Fallback zu Beat Duration aus Config
        return config?.beatDuration ?? 1f;
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
    
    #region Rhythm System
    
    void SubscribeToRhythm()
    {
        if (!useGlobalRhythm) return;
        
        // Check if GlobalRhythmManager exists
        if (GlobalRhythmManager.Instance == null)
        {
            if (config != null)
            {
                Debug.LogWarning($"[{name}] GlobalRhythmManager not found, using config beat duration");
            }
            return;
        }
        
        GlobalRhythmManager.OnBeatTick += OnRhythmBeat;
        GlobalRhythmManager.OnRhythmPaused += OnRhythmPaused;
        GlobalRhythmManager.OnRhythmResumed += OnRhythmResumed;
        GlobalRhythmManager.OnRhythmStopped += OnRhythmStopped;
    }
    
    void UnsubscribeFromRhythm()
    {
        if (!useGlobalRhythm) return;
        
        if (GlobalRhythmManager.Instance == null) return;
        
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
        OnAnimationStarted?.Invoke(this);
    }
    
    public void Stop()
    {
        isPlaying = false;
        isPaused = false;
        currentFrame = 0;
        manualTimer = 0f;
        
        UnsubscribeFromRhythm();
        UpdateSprite();
        OnAnimationStopped?.Invoke(this);
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
        OnFrameChanged?.Invoke(this, currentFrame);
    }
    
    public void SetFrame(int frame)
    {
        if (animationSprites == null || animationSprites.Length == 0) return;
        
        int newFrame = Mathf.Clamp(frame, 0, animationSprites.Length - 1);
        if (newFrame != currentFrame)
        {
            currentFrame = newFrame;
            UpdateSprite();
            OnFrameChanged?.Invoke(this, currentFrame);
        }
    }
    
    void UpdateSprite()
    {
        if (spriteRenderer == null || animationSprites == null || animationSprites.Length == 0) return;
        
        Sprite spriteToSet = currentFrame < animationSprites.Length ? animationSprites[currentFrame] : null;
        SetSprite(spriteToSet);
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
    
    public void SetFrameRate(float frameRate)
    {
        manualFrameRate = Mathf.Max(0.1f, frameRate);
    }
    
    public void SetLoop(bool shouldLoop)
    {
        loop = shouldLoop;
    }
    
    public void SetUseGlobalRhythm(bool useRhythm)
    {
        if (useGlobalRhythm != useRhythm)
        {
            UnsubscribeFromRhythm();
            useGlobalRhythm = useRhythm;
            
            if (useGlobalRhythm && isPlaying && !isPaused)
            {
                SubscribeToRhythm();
            }
        }
    }
    
    public Sprite[] GetAnimationSprites()
    {
        return animationSprites;
    }
    
    public float GetFrameRate()
    {
        return GetEffectiveFrameRate();
    }
    
    public bool IsLooping()
    {
        return loop;
    }
    
    public bool IsUsingGlobalRhythm()
    {
        return useGlobalRhythm;
    }
    
    public bool HasGlobalRhythmManager()
    {
        return GlobalRhythmManager.Instance != null;
    }
    
    public float GetConfigBeatDuration()
    {
        return config?.beatDuration ?? 1f;
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Play Animation")]
    void DebugPlay() => Play();
    
    [ContextMenu("Stop Animation")]
    void DebugStop() => Stop();
    
    [ContextMenu("Next Frame")]
    void DebugNextFrame() => NextFrame();
    
    [ContextMenu("Validate Setup")]
    void DebugValidateSetup() => ValidateSetup();
    
    [ContextMenu("Debug Animation Info")]
    void DebugAnimationInfo()
    {
        Debug.Log($"[{name}] Animation Info:\n" +
                 $"  Playing: {isPlaying} | Paused: {isPaused}\n" +
                 $"  Frame: {currentFrame}/{TotalFrames}\n" +
                 $"  Use Global Rhythm: {useGlobalRhythm}\n" +
                 $"  Global Rhythm Available: {HasGlobalRhythmManager()}\n" +
                 $"  Frame Rate: {GetFrameRate()}\n" +
                 $"  Config Beat Duration: {GetConfigBeatDuration()}\n" +
                 $"  Loop: {loop}");
    }
    
    [ContextMenu("Test Rhythm Fallback")]
    void DebugTestRhythmFallback()
    {
        bool originalRhythm = useGlobalRhythm;
        useGlobalRhythm = false;
        
        Debug.Log($"Testing without rhythm system - Frame Rate: {GetFrameRate()}");
        
        useGlobalRhythm = originalRhythm;
    }
    
    #endregion
}