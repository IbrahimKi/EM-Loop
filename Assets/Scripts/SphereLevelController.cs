using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum RotationCommand
{
    Left90,
    Right90,
    Flip180,
    FlipCounter180
}

[System.Serializable]
public class RotationEvent : UnityEvent<RotationCommand> { }
[System.Serializable]
public class StateEvent : UnityEvent<int> { }

public class SphereLevelController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float rotationDuration = 0.5f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeInOutCubic;
    
    [Header("State")]
    [SerializeField, Range(0, 7)] private int currentState = 0;
    
    [Header("Events")]
    public RotationEvent OnRotationCommand;
    public StateEvent OnStateChanged;
    
    [Header("Gizmo")]
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float gizmoAlpha = 0.3f;
    
    // Feste Rotationen für 8 Oktanten
    private static readonly Vector3[] OctantRotations = {
        new Vector3(0, 0, 0),       // 0: Top-Front-Right
        new Vector3(0, 90, 0),      // 1: Top-Front-Left
        new Vector3(0, -90, 0),     // 2: Top-Back-Right
        new Vector3(0, 180, 0),     // 3: Top-Back-Left
        new Vector3(180, 0, 0),     // 4: Bottom-Front-Right
        new Vector3(180, 90, 0),    // 5: Bottom-Front-Left
        new Vector3(180, -90, 0),   // 6: Bottom-Back-Right
        new Vector3(180, 180, 0)    // 7: Bottom-Back-Left
    };
    
    private bool isRotating;
    private int tweenId = -1;
    
    void Awake()
    {
        OnRotationCommand.AddListener(ExecuteRotation);
    }
    
    void OnDestroy()
    {
        if (tweenId >= 0) LeanTween.cancel(tweenId);
    }
    
    public void LoadState(int state)
    {
        if (state < 0 || state > 7) return;
        
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Editor-Modus: Direkte Rotation
            transform.localEulerAngles = OctantRotations[state];
            currentState = state;
            return;
        }
        #endif
        
        // Play-Modus: Animierte Rotation
        if (isRotating) return;
        
        isRotating = true;
        if (tweenId >= 0) LeanTween.cancel(tweenId);
        
        tweenId = LeanTween.rotateLocal(gameObject, OctantRotations[state], rotationDuration)
            .setEase(easeType)
            .setOnComplete(() => {
                isRotating = false;
                currentState = state;
                OnStateChanged?.Invoke(state);
                tweenId = -1;
            }).id;
    }
    
    void ExecuteRotation(RotationCommand cmd)
    {
        if (isRotating) return;
        
        Vector3 rotation = cmd switch
        {
            RotationCommand.Left90 => new Vector3(0, -90, 0),
            RotationCommand.Right90 => new Vector3(0, 90, 0),
            RotationCommand.Flip180 => new Vector3(180, 0, 0),
            RotationCommand.FlipCounter180 => new Vector3(-180, 0, 0),
            _ => Vector3.zero
        };
        
        isRotating = true;
        if (tweenId >= 0) LeanTween.cancel(tweenId);
        
        tweenId = LeanTween.rotateLocal(gameObject, transform.localEulerAngles + rotation, rotationDuration)
            .setEase(easeType)
            .setOnComplete(() => {
                isRotating = false;
                UpdateCurrentState();
                tweenId = -1;
            }).id;
    }
    
    void UpdateCurrentState()
    {
        float minAngle = float.MaxValue;
        int closest = 0;
        
        Quaternion current = Quaternion.Euler(transform.localEulerAngles);
        
        for (int i = 0; i < 8; i++)
        {
            float angle = Quaternion.Angle(current, Quaternion.Euler(OctantRotations[i]));
            if (angle < minAngle)
            {
                minAngle = angle;
                closest = i;
            }
        }
        
        if (closest != currentState)
        {
            currentState = closest;
            OnStateChanged?.Invoke(currentState);
        }
    }
    
    public void Rotate(RotationCommand cmd) => OnRotationCommand?.Invoke(cmd);
    
    void OnDrawGizmos()
    {
        float radius = transform.lossyScale.x * 0.5f;
        Vector3 center = transform.position;
        
        Gizmos.color = gizmoColor;
        
        // Hauptebenen
        DrawPlane(center, Vector3.forward, Vector3.right, radius);
        DrawPlane(center, Vector3.up, Vector3.right, radius);
        DrawPlane(center, Vector3.up, Vector3.forward, radius);
        
        // Diagonale Ebenen
        Vector3 diag1 = (Vector3.forward + Vector3.right).normalized;
        Vector3 diag2 = (Vector3.forward - Vector3.right).normalized;
        DrawPlane(center, Vector3.up, diag1, radius);
        DrawPlane(center, Vector3.up, diag2, radius);
        
        // Wireframe
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoAlpha);
        Gizmos.DrawWireSphere(center, radius);
        
        // Aktiver Oktant
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Vector3 offset = GetOctantDirection(currentState) * radius * 0.5f;
            Gizmos.DrawWireCube(center + offset, Vector3.one * radius * 0.5f);
        }
    }
    
    void DrawPlane(Vector3 center, Vector3 n1, Vector3 n2, float size)
    {
        Vector3 p1 = center + n1 * size + n2 * size;
        Vector3 p2 = center + n1 * size - n2 * size;
        Vector3 p3 = center - n1 * size - n2 * size;
        Vector3 p4 = center - n1 * size + n2 * size;
        
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }
    
    Vector3 GetOctantDirection(int state)
    {
        float x = (state & 1) == 0 ? 1 : -1;
        float y = state < 4 ? 1 : -1;
        float z = (state & 2) == 0 ? 1 : -1;
        return new Vector3(x, y, z).normalized;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SphereLevelController))]
public class SphereLevelControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        SphereLevelController controller = (SphereLevelController)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Load States", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            if (GUILayout.Button((i + 1).ToString()))
            {
                // Direkte Rotation im Editor
                controller.transform.localEulerAngles = OctantRotations[i];
                serializedObject.FindProperty("currentState").intValue = i;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        for (int i = 4; i < 8; i++)
        {
            if (GUILayout.Button((i + 1).ToString()))
            {
                // Direkte Rotation im Editor
                controller.transform.localEulerAngles = OctantRotations[i];
                serializedObject.FindProperty("currentState").intValue = i;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    
    // Zugriff auf private static Array
    private static readonly Vector3[] OctantRotations = {
        new Vector3(0, 0, 0),       // 0: Top-Front-Right
        new Vector3(0, 90, 0),      // 1: Top-Front-Left
        new Vector3(0, -90, 0),     // 2: Top-Back-Right
        new Vector3(0, 180, 0),     // 3: Top-Back-Left
        new Vector3(180, 0, 0),     // 4: Bottom-Front-Right
        new Vector3(180, 90, 0),    // 5: Bottom-Front-Left
        new Vector3(180, -90, 0),   // 6: Bottom-Back-Right
        new Vector3(180, 180, 0)    // 7: Bottom-Back-Left
    };
}
#endif
