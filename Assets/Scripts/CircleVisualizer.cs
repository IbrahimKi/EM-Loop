using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class CircleVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CircleSelector circleSelector;
    [SerializeField] private LineRenderer lineRenderer;
    
    [Header("Visual Settings")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color drawingColor = Color.yellow;
    [SerializeField] private Color confirmedColor = Color.green;
    
    private List<Vector3> visualPoints = new List<Vector3>();
    
    void Start()
    {
        SetupLineRenderer();
        
        if (circleSelector != null)
            circleSelector.OnCircleConfirmed.AddListener(ShowConfirmedCircle);
    }
    
    void Update()
    {
        if (circleSelector != null && circleSelector.IsDrawing)
        {
            UpdateDrawingVisualization();
        }
        else if (!circleSelector.IsDrawing)
        {
            ClearVisualization();
        }
    }
    
    void SetupLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
        
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
    }
    
    void UpdateDrawingVisualization()
    {
        var path = circleSelector.CurrentPath;
        if (path.Count < 2) return;
        
        lineRenderer.material.color = drawingColor;
        lineRenderer.positionCount = path.Count;
        
        for (int i = 0; i < path.Count; i++)
        {
            lineRenderer.SetPosition(i, path[i] + Vector3.up * 0.1f);
        }
    }
    
    void ShowConfirmedCircle(Vector3 center, float radius)
    {
        StartCoroutine(ShowConfirmationEffect(center, radius));
    }
    
    System.Collections.IEnumerator ShowConfirmationEffect(Vector3 center, float radius)
    {
        // Kreis aus Punkten generieren
        int segments = 32;
        visualPoints.Clear();
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0.1f,
                Mathf.Sin(angle) * radius
            );
            visualPoints.Add(point);
        }
        
        // Bestätigungskreis anzeigen
        lineRenderer.material.color = confirmedColor;
        lineRenderer.positionCount = visualPoints.Count;
        lineRenderer.SetPositions(visualPoints.ToArray());
        
        // 1 Sekunde anzeigen, dann ausblenden
        yield return new WaitForSeconds(1f);
        ClearVisualization();
    }
    
    void ClearVisualization()
    {
        lineRenderer.positionCount = 0;
    }
    
    void OnDestroy()
    {
        if (circleSelector != null)
            circleSelector.OnCircleConfirmed.RemoveListener(ShowConfirmedCircle);
    }
}