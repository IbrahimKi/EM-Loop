using UnityEngine;

/// <summary>
/// Circle System Manager - Koordiniert alle Komponenten
/// Setup Helper und Debug Tools
/// </summary>
public class CircleSystemManager : MonoBehaviour
{
    [Header("System Components")]
    [SerializeField] private DrawingInputHandler inputHandler;
    [SerializeField] private LineVisualizer lineVisualizer;
    [SerializeField] private CircleAnalyzer circleAnalyzer;
    [SerializeField] private TargetSelector targetSelector;
    [SerializeField] private DashAOEExecutor dashExecutor;
    
    [Header("Quick Setup")]
    [SerializeField] private bool autoSetup = true;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject sphereObject;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool visualizeLastCircle = false;
    
    // Stats
    private int totalCircles;
    private float averageQuality;
    private Vector3 lastCircleCenter;
    private float lastCircleRadius;
    
    void Awake()
    {
        if (autoSetup)
        {
            AutoSetupComponents();
        }
        
        ValidateSetup();
    }
    
    void OnEnable()
    {
        // Subscribe to events for stats
        CircleAnalyzer.OnCircleDetected += OnCircleDetected;
        CircleAnalyzer.OnDrawingRejected += OnDrawingRejected;
    }
    
    void OnDisable()
    {
        CircleAnalyzer.OnCircleDetected -= OnCircleDetected;
        CircleAnalyzer.OnDrawingRejected -= OnDrawingRejected;
    }
    
    void AutoSetupComponents()
    {
        // Find player if not set
        if (!playerObject)
        {
            playerObject = GameObject.FindWithTag("Player");
            if (!playerObject)
            {
                Debug.LogError("CircleSystemManager: No player object found!");
                return;
            }
        }
        
        // Find sphere if not set
        if (!sphereObject)
        {
            sphereObject = GameObject.FindWithTag("Sphere");
            if (!sphereObject)
            {
                Debug.LogError("CircleSystemManager: No sphere object found!");
                return;
            }
        }
        
        // Setup Input Handler
        if (!inputHandler)
        {
            inputHandler = playerObject.GetComponent<DrawingInputHandler>();
            if (!inputHandler)
            {
                inputHandler = playerObject.AddComponent<DrawingInputHandler>();
                Debug.Log("Added DrawingInputHandler to player");
            }
        }
        
        // Setup Line Visualizer
        if (!lineVisualizer)
        {
            GameObject lineObj = new GameObject("LineVisualizer");
            lineObj.transform.SetParent(playerObject.transform);
            lineVisualizer = lineObj.AddComponent<LineVisualizer>();
            Debug.Log("Created LineVisualizer");
        }
        
        // Setup Circle Analyzer
        if (!circleAnalyzer)
        {
            circleAnalyzer = playerObject.GetComponent<CircleAnalyzer>();
            if (!circleAnalyzer)
            {
                circleAnalyzer = playerObject.AddComponent<CircleAnalyzer>();
                Debug.Log("Added CircleAnalyzer to player");
            }
        }
        
        // Setup Target Selector
        if (!targetSelector)
        {
            targetSelector = playerObject.GetComponent<TargetSelector>();
            if (!targetSelector)
            {
                targetSelector = playerObject.AddComponent<TargetSelector>();
                Debug.Log("Added TargetSelector to player");
            }
        }
        
        // Setup Dash Executor
        if (!dashExecutor)
        {
            dashExecutor = playerObject.GetComponent<DashAOEExecutor>();
            if (!dashExecutor)
            {
                dashExecutor = playerObject.AddComponent<DashAOEExecutor>();
                Debug.Log("Added DashAOEExecutor to player");
            }
        }
    }
    
    void ValidateSetup()
    {
        bool isValid = true;
        
        if (!inputHandler) { Debug.LogError("Missing DrawingInputHandler!"); isValid = false; }
        if (!lineVisualizer) { Debug.LogError("Missing LineVisualizer!"); isValid = false; }
        if (!circleAnalyzer) { Debug.LogError("Missing CircleAnalyzer!"); isValid = false; }
        if (!targetSelector) { Debug.LogError("Missing TargetSelector!"); isValid = false; }
        if (!dashExecutor) { Debug.LogError("Missing DashAOEExecutor!"); isValid = false; }
        
        if (isValid)
        {
            Debug.Log("Circle System: All components ready!");
        }
    }
    
    void OnCircleDetected(Vector3 center, float radius, float quality)
    {
        totalCircles++;
        averageQuality = ((averageQuality * (totalCircles - 1)) + quality) / totalCircles;
        lastCircleCenter = center;
        lastCircleRadius = radius;
        
        if (showDebugInfo)
        {
            Debug.Log($"Circle #{totalCircles} - Quality: {quality:F2} - Avg: {averageQuality:F2}");
        }
    }
    
    void OnDrawingRejected(string reason)
    {
        if (showDebugInfo)
        {
            Debug.Log($"Drawing rejected: {reason}");
        }
    }
    
    void OnDrawGizmos()
    {
        if (!visualizeLastCircle || lastCircleRadius <= 0) return;
        
        // Draw last circle
        Gizmos.color = Color.green;
        DrawGizmoCircle(lastCircleCenter, lastCircleRadius, 32);
    }
    
    void DrawGizmoCircle(Vector3 center, float radius, int segments)
    {
        Vector3 prevPoint = center + Vector3.forward * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
    
    // Public API for external systems
    public void ResetStats()
    {
        totalCircles = 0;
        averageQuality = 0;
    }
    
    public int GetTotalCircles() => totalCircles;
    public float GetAverageQuality() => averageQuality;
    public bool IsDrawing() => inputHandler ? inputHandler.IsDrawing : false;
    public bool IsDashing() => dashExecutor ? dashExecutor.IsDashing : false;
    
    // Debug commands
    [ContextMenu("Test Perfect Circle")]
    void TestPerfectCircle()
    {
        if (!inputHandler)
        {
            Debug.LogError("DrawingInputHandler not found!");
            return;
        }
        
        // Simulate a perfect circle draw
        int points = 32;
        Vector3[] testPoints = new Vector3[points];
        Vector3 center = playerObject.transform.position + Vector3.forward * 3f;
        float radius = 2f;
        
        for (int i = 0; i < points; i++)
        {
            float angle = (float)i / points * Mathf.PI * 2f;
            testPoints[i] = center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
        }
        
        // Use the public test method
        inputHandler.SimulateDrawing(testPoints);
        Debug.Log("Perfect circle simulation triggered!");
    }
    
    [ContextMenu("Print System Stats")]
    void PrintStats()
    {
        Debug.Log($"Circle System Stats:\n" +
                 $"Total Circles: {totalCircles}\n" +
                 $"Average Quality: {averageQuality:F2}\n" +
                 $"Is Drawing: {IsDrawing()}\n" +
                 $"Is Dashing: {IsDashing()}");
    }
}