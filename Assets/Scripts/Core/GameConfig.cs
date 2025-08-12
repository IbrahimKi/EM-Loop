using UnityEngine;

/// <summary>
/// Zentrale Game Configuration - Settings only!
/// WICHTIG: ScriptableObjects können KEINE Scene References speichern!
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Configuration")]
public class GameConfig : ScriptableObject
{
    private static GameConfig _instance;
    public static GameConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GameConfig>("GameConfig");
                if (_instance == null)
                {
                    Debug.LogError("GameConfig not found! Create one at: Assets/Resources/GameConfig.asset");
                }
            }
            return _instance;
        }
    }
    
    [Header("=== Sphere Settings ===")]
    public float sphereRadius = 10f;
    public LayerMask sphereLayer = -1;
    public Color sphereGizmoColor = Color.cyan;
    
    [Header("=== Drawing Settings ===")]
    public float minPointDistance = 0.02f;
    public float maxPointDistance = 0.2f;
    public int maxDrawingPoints = 500;
    public float drawingLineWidth = 0.05f;
    public Color drawingLineColor = Color.yellow;
    
    [Header("=== Circle Detection ===")]
    public float minCircleRadius = 0.5f;
    public float maxCircleRadius = 5f;
    public float circleClosureThreshold = 0.5f;
    public float minCircleCoverage = 180f;
    public AnimationCurve circleQualityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("=== Dash Settings ===")]
    public float dashDuration = 0.4f;
    public float dashArcHeight = 1f;
    public float baseAOERadius = 3f;
    public AnimationCurve aoeRadiusByQuality = AnimationCurve.Linear(0, 0.7f, 1, 1.5f);
    public LeanTweenType dashEaseType = LeanTweenType.easeInOutQuad;
    
    [Header("=== Rhythm System ===")]
    public float beatDuration = 1f;
    public bool pauseOnAreaTransition = true;
    
    [Header("=== Enemy Settings ===")]
    public LayerMask enemyLayer = -1;
    public LayerMask playerLayer = -1;
    
    [Header("=== Billboard Settings ===")]
    public bool billboardConstrainY = false;
    public float billboardUpdateThreshold = 0.001f;
    
    [Header("=== Performance ===")]
    public bool useJobSystem = true;
    public bool useBulkBillboardUpdate = true;
    
    #region Validation
    
    void OnValidate()
    {
        // Clamp values to sensible ranges
        sphereRadius = Mathf.Max(1f, sphereRadius);
        minPointDistance = Mathf.Clamp(minPointDistance, 0.001f, 1f);
        maxPointDistance = Mathf.Max(minPointDistance, maxPointDistance);
        maxDrawingPoints = Mathf.Clamp(maxDrawingPoints, 10, 1000);
        
        minCircleRadius = Mathf.Max(0.1f, minCircleRadius);
        maxCircleRadius = Mathf.Max(minCircleRadius, maxCircleRadius);
        
        dashDuration = Mathf.Max(0.1f, dashDuration);
        baseAOERadius = Mathf.Max(0.5f, baseAOERadius);
        beatDuration = Mathf.Max(0.1f, beatDuration);
    }
    
    #endregion
}

/// <summary>
/// Statische Reference Manager - Verwaltet Scene References
/// </summary>
public static class GameReferences
{
    private static Transform _sphereCenter;
    private static Camera _mainCamera;
    private static Transform _playerTransform;
    
    private static bool _searchedForSphere = false;
    public static Transform SphereCenter
    {
        get
        {
            if (_sphereCenter == null && !_searchedForSphere)
            {
                _sphereCenter = GameObject.FindWithTag("Sphere")?.transform;
                _searchedForSphere = true;
            }
            return _sphereCenter;
        }
        set 
        { 
            _sphereCenter = value;
            _searchedForSphere = (value != null);
        }
    }
    
    private static bool _searchedForCamera = false;
    public static Camera MainCamera
    {
        get
        {
            if (_mainCamera == null && !_searchedForCamera)
            {
                _mainCamera = Camera.main;
                _searchedForCamera = true;
            }
            return _mainCamera;
        }
        set 
        { 
            _mainCamera = value;
            _searchedForCamera = (value != null);
        }
    }
    
    private static bool _searchedForPlayer = false;
    public static Transform PlayerTransform
    {
        get
        {
            if (_playerTransform == null && !_searchedForPlayer)
            {
                _playerTransform = GameObject.FindWithTag("Player")?.transform;
                _searchedForPlayer = true;
            }
            return _playerTransform;
        }
        set 
        { 
            _playerTransform = value;
            _searchedForPlayer = (value != null);
        }
    }
    
    /// <summary>
    /// Clear on scene change
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        _sphereCenter = null;
        _mainCamera = null;
        _playerTransform = null;
        _searchedForSphere = false;
        _searchedForCamera = false;
        _searchedForPlayer = false;
    }
    
    /// <summary>
    /// Helper: Project to sphere
    /// </summary>
    public static Vector3 ProjectToSphere(Vector3 worldPoint)
    {
        if (SphereCenter == null) return worldPoint;
        
        float radius = GameConfig.Instance?.sphereRadius ?? 10f;
        Vector3 direction = (worldPoint - SphereCenter.position).normalized;
        return SphereCenter.position + direction * radius;
    }
    
    /// <summary>
    /// Force refresh all references
    /// </summary>
    public static void RefreshReferences()
    {
        _searchedForSphere = false;
        _searchedForCamera = false;
        _searchedForPlayer = false;
        
        _sphereCenter = GameObject.FindWithTag("Sphere")?.transform;
        _mainCamera = Camera.main;
        _playerTransform = GameObject.FindWithTag("Player")?.transform;
        
        _searchedForSphere = true;
        _searchedForCamera = true;
        _searchedForPlayer = true;
        
        Debug.Log($"[GameReferences] Refreshed - Sphere: {_sphereCenter?.name ?? "null"}, Camera: {_mainCamera?.name ?? "null"}, Player: {_playerTransform?.name ?? "null"}");
    }
}