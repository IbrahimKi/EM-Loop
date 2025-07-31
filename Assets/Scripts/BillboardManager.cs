using UnityEngine;
using System.Collections.Generic;

// Einfaches, sehr performantes Billboard-System
public class BillboardManager : MonoBehaviour
{
    public static BillboardManager Instance { get; private set; }
    
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool constrainY = false; // Y-Achse fixieren für 2.5D
    
    private static readonly List<Billboard> activeBillboards = new List<Billboard>(100);
    private Transform cameraTransform;
    
    public static event System.Action<Camera> OnCameraChanged;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            targetCamera = targetCamera ?? Camera.main;
            cacheCamera();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void cacheCamera()
    {
        if (targetCamera)
        {
            cameraTransform = targetCamera.transform;
            OnCameraChanged?.Invoke(targetCamera);
        }
    }
    
    void LateUpdate()
    {
        if (!cameraTransform || activeBillboards.Count == 0) return;
        
        UpdateAllBillboards();
    }
    
    private void UpdateAllBillboards()
    {
        Vector3 cameraPos = cameraTransform.position;
        
        // Batch-Update aller Billboards
        for (int i = activeBillboards.Count - 1; i >= 0; i--)
        {
            var billboard = activeBillboards[i];
            
            if (!billboard || !billboard.transform)
            {
                activeBillboards.RemoveAt(i);
                continue;
            }
            
            Vector3 direction = (cameraPos - billboard.transform.position).normalized;
            
            if (constrainY)
            {
                direction.y = 0;
                direction.Normalize();
            }
            
            billboard.transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    public static void RegisterBillboard(Billboard billboard)
    {
        if (!activeBillboards.Contains(billboard))
        {
            activeBillboards.Add(billboard);
        }
    }
    
    public static void UnregisterBillboard(Billboard billboard)
    {
        activeBillboards.Remove(billboard);
    }
    
    public void SetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
        cacheCamera();
    }
}