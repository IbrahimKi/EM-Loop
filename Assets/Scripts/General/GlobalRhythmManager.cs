using UnityEngine;
using System.Collections;

/// <summary>
/// Globaler Rhythm Manager - steuert den 3-Frame Takt für alle Gegner
/// Synchronisiert alle Enemies und Environment-Elemente
/// </summary>
public class GlobalRhythmManager : MonoBehaviour
{
    [Header("Rhythm Settings")]
    [SerializeField] private float beatDuration = 1f; // Sekunden pro Beat
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool pauseOnAreaTransition = true;
    
    [Header("Current State")]
    [SerializeField, ReadOnly] private int currentFrame = 0;
    [SerializeField, ReadOnly] private EnemyRhythmState currentState = EnemyRhythmState.Inactive;
    [SerializeField, ReadOnly] private bool isRunning = false;
    [SerializeField, ReadOnly] private float nextBeatTime = 0f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;
    [SerializeField] private bool enableDebugLogs = false;
    
    // Singleton
    public static GlobalRhythmManager Instance { get; private set; }
    
    // Events
    public static System.Action<int> OnBeatTick;
    public static System.Action<EnemyRhythmState> OnStateChanged;
    public static System.Action<int> OnFrameChanged; // Für UI/Visual updates
    public static System.Action OnRhythmStarted;
    public static System.Action OnRhythmStopped;
    public static System.Action OnRhythmPaused;
    public static System.Action OnRhythmResumed;
    
    // Performance
    private Coroutine rhythmCoroutine;
    private WaitForSeconds beatWait;
    
    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Cache wait object
        beatWait = new WaitForSeconds(beatDuration);
        
        // Subscribe to level events
        SphereLevelManager.OnAreaTransition += OnAreaTransition;
        SphereLevelManager.OnAreaStarted += OnAreaStarted;
    }
    
    void Start()
    {
        if (autoStart)
        {
            StartRhythm();
        }
    }
    
    void OnDestroy()
    {
        SphereLevelManager.OnAreaTransition -= OnAreaTransition;
        SphereLevelManager.OnAreaStarted -= OnAreaStarted;
        
        if (rhythmCoroutine != null)
        {
            StopCoroutine(rhythmCoroutine);
        }
    }
    
    #region Rhythm Control
    
    public void StartRhythm()
    {
        if (isRunning) return;
        
        isRunning = true;
        currentFrame = 0;
        currentState = EnemyRhythmState.Inactive;
        nextBeatTime = Time.time + beatDuration;
        
        rhythmCoroutine = StartCoroutine(RhythmLoop());
        
        OnRhythmStarted?.Invoke();
        LogDebug("Rhythm started");
    }
    
    public void StopRhythm()
    {
        if (!isRunning) return;
        
        isRunning = false;
        
        if (rhythmCoroutine != null)
        {
            StopCoroutine(rhythmCoroutine);
            rhythmCoroutine = null;
        }
        
        OnRhythmStopped?.Invoke();
        LogDebug("Rhythm stopped");
    }
    
    public void PauseRhythm()
    {
        if (!isRunning) return;
        
        isRunning = false;
        
        if (rhythmCoroutine != null)
        {
            StopCoroutine(rhythmCoroutine);
            rhythmCoroutine = null;
        }
        
        OnRhythmPaused?.Invoke();
        LogDebug("Rhythm paused");
    }
    
    public void ResumeRhythm()
    {
        if (isRunning) return;
        
        isRunning = true;
        nextBeatTime = Time.time + beatDuration;
        
        rhythmCoroutine = StartCoroutine(RhythmLoop());
        
        OnRhythmResumed?.Invoke();
        LogDebug("Rhythm resumed");
    }
    
    #endregion
    
    #region Rhythm Loop
    
    IEnumerator RhythmLoop()
    {
        while (isRunning)
        {
            // Trigger current frame
            TriggerBeat();
            
            // Wait for next beat
            yield return beatWait;
            
            // Advance to next frame
            AdvanceFrame();
        }
    }
    
    void TriggerBeat()
    {
        // Update next beat time
        nextBeatTime = Time.time + beatDuration;
        
        // Trigger events
        OnBeatTick?.Invoke(currentFrame);
        OnStateChanged?.Invoke(currentState);
        OnFrameChanged?.Invoke(currentFrame);
        
        // Update static RhythmManager for backwards compatibility
        RhythmManager.OnBeatTick?.Invoke(currentFrame);
        RhythmManager.OnStateChanged?.Invoke(currentState);
        
        LogDebug($"Beat: Frame {currentFrame} ({currentState})");
    }
    
    void AdvanceFrame()
    {
        currentFrame = (currentFrame + 1) % 3;
        currentState = (EnemyRhythmState)currentFrame;
    }
    
    #endregion
    
    #region Settings
    
    public void SetBeatDuration(float duration)
    {
        beatDuration = Mathf.Max(0.1f, duration);
        beatWait = new WaitForSeconds(beatDuration);
        
        LogDebug($"Beat duration set to {beatDuration}s");
    }
    
    public void SetFrameDirectly(int frame)
    {
        currentFrame = Mathf.Clamp(frame, 0, 2);
        currentState = (EnemyRhythmState)currentFrame;
        
        if (isRunning)
        {
            TriggerBeat();
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    void OnAreaTransition(int from, int to)
    {
        if (pauseOnAreaTransition)
        {
            PauseRhythm();
        }
    }
    
    void OnAreaStarted(int areaIndex)
    {
        if (pauseOnAreaTransition)
        {
            // Resume with small delay
            Invoke(nameof(ResumeRhythm), 0.5f);
        }
    }
    
    #endregion
    
    #region Public API
    
    public int GetCurrentFrame() => currentFrame;
    public EnemyRhythmState GetCurrentState() => currentState;
    public bool IsRunning() => isRunning;
    public float GetBeatDuration() => beatDuration;
    public float GetTimeToNextBeat() => Mathf.Max(0f, nextBeatTime - Time.time);
    public float GetBeatProgress() => 1f - (GetTimeToNextBeat() / beatDuration);
    
    #endregion
    
    #region Debug
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[RhythmManager] {message}");
        }
    }
    
    void OnGUI()
    {
        if (!showDebugGUI) return;
        
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(10, 10, 200, 150));
        GUILayout.Label("Rhythm Manager", GUI.skin.box);
        
        GUILayout.Label($"Running: {isRunning}");
        GUILayout.Label($"Frame: {currentFrame} ({currentState})");
        GUILayout.Label($"Beat Duration: {beatDuration:F2}s");
        GUILayout.Label($"Next Beat: {GetTimeToNextBeat():F2}s");
        GUILayout.Label($"Progress: {GetBeatProgress():F1%}");
        
        GUILayout.Space(5);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start")) StartRhythm();
        if (GUILayout.Button("Stop")) StopRhythm();
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Pause")) PauseRhythm();
        if (GUILayout.Button("Resume")) ResumeRhythm();
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        GUILayout.Label("Manual Frames:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0")) SetFrameDirectly(0);
        if (GUILayout.Button("1")) SetFrameDirectly(1);
        if (GUILayout.Button("2")) SetFrameDirectly(2);
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Start Rhythm")]
    void DebugStart() => StartRhythm();
    
    [ContextMenu("Stop Rhythm")]
    void DebugStop() => StopRhythm();
    
    [ContextMenu("Trigger Beat")]
    void DebugTriggerBeat() => TriggerBeat();
    
    #endregion
}

/// <summary>
/// Static wrapper für backwards compatibility
/// </summary>
public static class RhythmManager
{
    public static System.Action<int> OnBeatTick;
    public static System.Action<EnemyRhythmState> OnStateChanged;
    
    public static void TriggerBeat(int frame)
    {
        OnBeatTick?.Invoke(frame);
        OnStateChanged?.Invoke((EnemyRhythmState)frame);
    }
    
    public static int GetCurrentFrame()
    {
        return GlobalRhythmManager.Instance?.GetCurrentFrame() ?? 0;
    }
    
    public static bool IsRunning()
    {
        return GlobalRhythmManager.Instance?.IsRunning() ?? false;
    }
}