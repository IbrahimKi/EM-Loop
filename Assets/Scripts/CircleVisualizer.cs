using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color drawColor = Color.yellow;
    [SerializeField] private Color confirmColor = Color.green;
    
    private LineRenderer lineRenderer;
    private Coroutine confirmRoutine;
    
    void Awake()
    {
        SetupLineRenderer();
    }
    
    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startColor = drawColor;
        lineRenderer.endColor = drawColor;
        lineRenderer.positionCount = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }
    
    void OnEnable()
    {
        CircleSelector.OnPathUpdated += UpdatePath;
        CircleSelector.OnCircleConfirmed += ShowConfirmation;
        CircleSelector.OnDrawingCancelled += ClearPath;
    }
    
    void OnDisable()
    {
        CircleSelector.OnPathUpdated -= UpdatePath;
        CircleSelector.OnCircleConfirmed -= ShowConfirmation;
        CircleSelector.OnDrawingCancelled -= ClearPath;
    }
    
    void UpdatePath(Vector3[] points)
    {
        // PERFORMANCE: Direkt vom Array ohne Kopieren
        var selector = FindObjectOfType<CircleSelector>();
        int pointCount = selector.GetPointCount();
        
        if (pointCount == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }
        
        // FADE SYSTEM: Berechne durchschnittlichen Fade-Faktor
        float[] fadeFactors = selector.GetPointFadeFactors();
        float averageFade = 1f;
        
        if (fadeFactors != null && pointCount > 0)
        {
            // Durchschnittlicher Fade der letzten 20% der Punkte
            int fadeCheckCount = Mathf.Max(1, pointCount / 5);
            float fadeSum = 0f;
            
            for (int i = 0; i < fadeCheckCount; i++)
            {
                fadeSum += fadeFactors[i];
            }
            averageFade = fadeSum / fadeCheckCount;
        }
        
        // SIMPLE FADE: Gesamte Linie mit durchschnittlichem Alpha
        Color startColor = new Color(drawColor.r, drawColor.g, drawColor.b, drawColor.a * averageFade);
        Color endColor = new Color(drawColor.r, drawColor.g, drawColor.b, drawColor.a);
        
        lineRenderer.startColor = startColor; // Hinten (gefaded)
        lineRenderer.endColor = endColor;     // Vorne (voll sichtbar)
        lineRenderer.positionCount = pointCount;
        
        // PERFORMANCE: SetPositions mit slice
        Vector3[] positions = new Vector3[pointCount];
        System.Array.Copy(points, 0, positions, 0, pointCount);
        lineRenderer.SetPositions(positions);
    }
    
    void ShowConfirmation(Vector3 center, float radius, Vector3 normal)
    {
        if (confirmRoutine != null) StopCoroutine(confirmRoutine);
        confirmRoutine = StartCoroutine(ConfirmationEffect(center, radius, normal));
    }
    
    System.Collections.IEnumerator ConfirmationEffect(Vector3 center, float radius, Vector3 normal)
    {
        // SIMPLE: Zeichne perfekten Kreis
        int segments = 32;
        Vector3[] circlePoints = new Vector3[segments + 1];
        
        Vector3 tangent1 = Vector3.Cross(normal, Vector3.up);
        if (tangent1.sqrMagnitude < 0.01f) tangent1 = Vector3.Cross(normal, Vector3.forward);
        tangent1.Normalize();
        
        Vector3 tangent2 = Vector3.Cross(normal, tangent1);
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            circlePoints[i] = center + 
                (tangent1 * Mathf.Cos(angle) + tangent2 * Mathf.Sin(angle)) * radius;
        }
        
        lineRenderer.startColor = confirmColor;
        lineRenderer.endColor = confirmColor;
        lineRenderer.positionCount = segments + 1;
        lineRenderer.SetPositions(circlePoints);
        
        yield return new WaitForSeconds(1f);
        ClearPath();
    }
    
    void ClearPath()
    {
        if (confirmRoutine != null)
        {
            StopCoroutine(confirmRoutine);
            confirmRoutine = null;
        }
        lineRenderer.positionCount = 0;
    }
}