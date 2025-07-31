using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color drawColor = Color.yellow;
    [SerializeField] private Color confirmColor = Color.green;
    [SerializeField] private float heightOffset = 0.1f;
    [SerializeField] private Transform sphereCenter;
    
    private LineRenderer lineRenderer;
    private Coroutine confirmRoutine;
    
    void Awake()
    {
        SetupLineRenderer();
        if (sphereCenter == null)
            sphereCenter = transform.parent; // Fallback zu Parent
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
    
    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startColor = drawColor;
        lineRenderer.endColor = drawColor;
        lineRenderer.positionCount = 0;
    }
    
    void UpdatePath(List<Vector3> path, Vector3 normal)
    {
        lineRenderer.startColor = drawColor;
        lineRenderer.endColor = drawColor;
        lineRenderer.positionCount = path.Count;
        
        for (int i = 0; i < path.Count; i++)
        {
            // Leicht über Oberfläche heben entlang der Normale
            lineRenderer.SetPosition(i, path[i] + normal * heightOffset);
        }
    }
    
    void ShowConfirmation(Vector3 center, float radius, Vector3 normal)
    {
        if (confirmRoutine != null)
            StopCoroutine(confirmRoutine);
        
        confirmRoutine = StartCoroutine(ConfirmationEffect(center, radius, normal));
    }
    
    System.Collections.IEnumerator ConfirmationEffect(Vector3 center, float radius, Vector3 normal)
    {
        // Perfect circle in tangent plane
        int segments = 32;
        Vector3[] circlePoints = new Vector3[segments + 1];
        
        // Basis-Vektoren der Tangentialebene
        Vector3 tangent1 = Vector3.Cross(normal, Vector3.up);
        if (tangent1.magnitude < 0.1f) tangent1 = Vector3.Cross(normal, Vector3.forward);
        tangent1.Normalize();
        
        Vector3 tangent2 = Vector3.Cross(normal, tangent1).normalized;
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 circlePoint = center + 
                (tangent1 * Mathf.Cos(angle) + tangent2 * Mathf.Sin(angle)) * radius +
                normal * heightOffset;
            
            circlePoints[i] = circlePoint;
        }
        
        lineRenderer.startColor = confirmColor;
        lineRenderer.endColor = confirmColor;
        lineRenderer.positionCount = circlePoints.Length;
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