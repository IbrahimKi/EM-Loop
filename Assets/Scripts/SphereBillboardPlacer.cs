using UnityEngine;
using System.Collections.Generic;

public class SphereBillboardPlacer : MonoBehaviour
{
    [SerializeField] private float sphereRadius = 5f;
    [SerializeField] private List<GameObject> billboardObjects = new List<GameObject>();
    
    private SphereCollider sphereCollider;
    
    void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        
        PlaceObjectsOnSphere();
    }
    
    [ContextMenu("Place Objects on Sphere")]
    void PlaceObjectsOnSphere()
    {
        Vector3 sphereCenter = transform.position;
        
        for (int i = 0; i < billboardObjects.Count; i++)
        {
            if (billboardObjects[i] == null) continue;
            
            // Objekt zur Sphere-Oberfläche bewegen
            Vector3 directionFromCenter = (billboardObjects[i].transform.position - sphereCenter).normalized;
            Vector3 surfacePosition = sphereCenter + directionFromCenter * sphereRadius;
            
            billboardObjects[i].transform.position = surfacePosition;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sphereRadius);
    }
}