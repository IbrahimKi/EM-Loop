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
    
    // Events
    public static System.Action<AnimatedBillboard> OnAnimationStarted;
    public static System.Action<AnimatedBillboard> OnAnimationStopped;
    public static System.Action<AnimatedBillboard, int> OnFrameChanged;
    
    public bool IsPlaying => isPlaying;
    public int CurrentFrame => currentFrame;
    public int TotalFrames => animationSprites?.Length ?? 0;
    
    protected override void OnAwakeOverride()
    {
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
            
            if (manualTimer >= manualFrameRate)
            {
                NextFrame();
                manualTimer = 0f;
            }
        }
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
        return manualFrameRate;
    }
    
    public bool IsLooping()
    {
        return loop;
    }
    
    public bool IsUsingGlobalRhythm()
    {
        return useGlobalRhythm;
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
    
    #endregion
}