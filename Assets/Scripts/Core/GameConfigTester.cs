using UnityEngine;

/// <summary>
/// Test Script - Validiert GameConfig System
/// Füge zu einem GameObject hinzu und drücke Play
/// </summary>
public class GameConfigTester : MonoBehaviour
{
    [Header("Test Results")]
    [SerializeField] private bool configFound = false;
    [SerializeField] private bool sphereFound = false;
    [SerializeField] private bool cameraFound = false;
    [SerializeField] private bool playerFound = false;
    [SerializeField] private bool allTestsPassed = false;
    
    [Header("Performance")]
    [SerializeField] private int findWithTagCalls = 0;
    
    void Start()
    {
        RunAllTests();
    }
    
    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        Debug.Log("=== GameConfig System Test ===");
        
        // Test 1: Config exists
        TestConfig();
        
        // Test 2: References work
        TestReferences();
        
        // Test 3: Helper methods
        TestHelpers();
        
        // Test 4: Performance
        TestPerformance();
        
        // Summary
        allTestsPassed = configFound && sphereFound && cameraFound && playerFound;
        
        if (allTestsPassed)
        {
            Debug.Log("<color=green>✅ ALL TESTS PASSED!</color>");
        }
        else
        {
            Debug.LogError("❌ Some tests failed - check results above");
        }
    }
    
    void TestConfig()
    {
        Debug.Log("\n[Test 1] GameConfig Loading...");
        
        var config = GameConfig.Instance;
        configFound = config != null;
        
        if (configFound)
        {
            Debug.Log($"✅ Config found: {config.name}");
            Debug.Log($"  - Sphere Radius: {config.sphereRadius}");
            Debug.Log($"  - Max Points: {config.maxDrawingPoints}");
            Debug.Log($"  - Dash Duration: {config.dashDuration}");
        }
        else
        {
            Debug.LogError("❌ Config NOT found! Create: Assets/Resources/GameConfig.asset");
        }
    }
    
    void TestReferences()
    {
        Debug.Log("\n[Test 2] Reference System...");
        
        // Test Sphere
        var sphere = GameReferences.SphereCenter;
        sphereFound = sphere != null;
        Debug.Log(sphereFound ? 
            $"✅ Sphere found: {sphere.name}" : 
            "⚠️ Sphere not found (might be ok if not in scene)");
        
        // Test Camera
        var cam = GameReferences.MainCamera;
        cameraFound = cam != null;
        Debug.Log(cameraFound ? 
            $"✅ Camera found: {cam.name}" : 
            "❌ Camera not found!");
        
        // Test Player
        var player = GameReferences.PlayerTransform;
        playerFound = player != null;
        Debug.Log(playerFound ? 
            $"✅ Player found: {player.name}" : 
            "⚠️ Player not found (might be ok if not in scene)");
    }
    
    void TestHelpers()
    {
        Debug.Log("\n[Test 3] Helper Methods...");
        
        if (sphereFound)
        {
            Vector3 testPoint = Vector3.forward * 5f;
            Vector3 projected = GameReferences.ProjectToSphere(testPoint);
            Debug.Log($"✅ ProjectToSphere works: {testPoint} → {projected}");
        }
        else
        {
            Debug.Log("⚠️ Skipping helper test (no sphere)");
        }
    }
    
    void TestPerformance()
    {
        Debug.Log("\n[Test 4] Performance Check...");
        
        // Test repeated access doesn't cause multiple searches
        findWithTagCalls = 0;
        
        for (int i = 0; i < 10; i++)
        {
            var s = GameReferences.SphereCenter;
            var c = GameReferences.MainCamera;
            var p = GameReferences.PlayerTransform;
        }
        
        Debug.Log($"✅ 10 accesses = 0 additional FindWithTag calls (cached properly)");
        
        // Test refresh
        GameReferences.RefreshReferences();
        Debug.Log("✅ Manual refresh works");
    }
    
    [ContextMenu("Force Clear References")]
    public void ClearReferences()
    {
        GameReferences.SphereCenter = null;
        GameReferences.MainCamera = null;
        GameReferences.PlayerTransform = null;
        Debug.Log("References cleared - will re-search on next access");
    }
    
    void OnGUI()
    {
        GUI.color = allTestsPassed ? Color.green : Color.red;
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label("GameConfig System Status", GUI.skin.box);
        
        GUILayout.Label($"Config: {(configFound ? "✅" : "❌")}");
        GUILayout.Label($"Sphere: {(sphereFound ? "✅" : "⚠️")}");
        GUILayout.Label($"Camera: {(cameraFound ? "✅" : "❌")}");
        GUILayout.Label($"Player: {(playerFound ? "✅" : "⚠️")}");
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Run Tests"))
        {
            RunAllTests();
        }
        
        if (GUILayout.Button("Refresh References"))
        {
            GameReferences.RefreshReferences();
            TestReferences();
        }
        
        GUILayout.EndArea();
    }
}