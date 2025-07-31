using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class CircleSelector : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent<Vector3, float> OnCircleConfirmed;
    
    [Header("Settings")]
    [SerializeField] private float minRadius = 1f;
    [SerializeField] private Camera playerCamera;
    
    private Vector3 startPosition;
    private bool isDrawing = false;
    private List<Vector3> currentPath = new List<Vector3>();
    
    public bool IsDrawing => isDrawing;
    public List<Vector3> CurrentPath => currentPath;
    
    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
            StartDrawing();
        
        if (isDrawing)
        {
            if (Input.GetMouseButton(0))
                UpdateDrawing();
            else if (Input.GetMouseButtonUp(0))
                FinishDrawing();
        }
    }
    
    void StartDrawing()
    {
        Vector3 worldPos = GetWorldPosition();
        if (worldPos == Vector3.zero) return;
        
        startPosition = worldPos;
        isDrawing = true;
        currentPath.Clear();
        currentPath.Add(startPosition);
    }
    
    void UpdateDrawing()
    {
        Vector3 worldPos = GetWorldPosition();
        if (worldPos == Vector3.zero) return;
        
        currentPath.Add(worldPos);
    }
    
    void FinishDrawing()
    {
        if (currentPath.Count < 3)
        {
            CancelDrawing();
            return;
        }
        
        Vector3 center = CalculateCenter();
        float radius = CalculateRadius(center);
        
        if (radius >= minRadius)
        {
            OnCircleConfirmed?.Invoke(center, radius);
        }
        
        CancelDrawing();
    }
    
    void CancelDrawing()
    {
        isDrawing = false;
        currentPath.Clear();
    }
    
    Vector3 GetWorldPosition()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.point;
        
        return Vector3.zero;
    }
    
    Vector3 CalculateCenter()
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 point in currentPath)
            sum += point;
        return sum / currentPath.Count;
    }
    
    float CalculateRadius(Vector3 center)
    {
        float maxDist = 0f;
        foreach (Vector3 point in currentPath)
        {
            float dist = Vector3.Distance(center, point);
            if (dist > maxDist) maxDist = dist;
        }
        return maxDist;
    }
}