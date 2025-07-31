using UnityEngine;

public class CircleTarget : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private int priority = 0;
    [SerializeField] private int pointValue = 100;
    [SerializeField] private bool isActive = true;
    
    public int Priority => priority;
    public int PointValue => pointValue;
    public bool IsActive => isActive;
    
    public void SetActive(bool active) => isActive = active;
    public void SetPriority(int newPriority) => priority = newPriority;
    public void SetPointValue(int newValue) => pointValue = newValue;
    
    // Event für wenn Target ausgewählt wird
    public static event System.Action<CircleTarget, Vector3> OnTargetSelected;
    
    public void SelectTarget(Vector3 selectionCenter)
    {
        if (!isActive) return;
        
        OnTargetSelected?.Invoke(this, selectionCenter);
        Debug.Log($"Target {name} selected - Priority: {priority}, Points: {pointValue}");
    }
}