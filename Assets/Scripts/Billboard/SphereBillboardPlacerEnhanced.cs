using System.Collections.Generic;
using UnityEngine;

public class SphereBillboardPlacerEnhanced : MonoBehaviour
{
    [Header("Sphere Settings")]
    [SerializeField] private float sphereRadius = 5f;
    [SerializeField] private bool autoFindBillboards = true;
    [SerializeField] private bool updateInRealtime = false;
    
    [Header("Billboard Lists")]
    [SerializeField] private List<AnimatedBillboard> animatedBillboards = new List<AnimatedBillboard>();
    [SerializeField] private List<GameObject> legacyObjects = new List<GameObject>();
    
    [Header("Auto Creation")]
    [SerializeField] private bool createTestBillboards = false;
    [SerializeField] private int testBillboardCount = 8;
    [SerializeField] private Sprite[] testSprites;
    
    private SphereCollider sphereCollider;
    private Vector3 lastPosition;
    private float lastRadius;
    
    void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        lastPosition = transform.position;
        lastRadius = sphereRadius;
        
        if (autoFindBillboards)
        {
            FindAllBillboards();
        }
        
        PlaceAllBillboards();
    }
    
    void Update()
    {
        if (updateInRealtime && HasTransformChanged())
        {
            PlaceAllBillboards();
            UpdateCachedTransform();
        }
    }
    
    bool HasTransformChanged()
    {
        return Vector3.Distance(transform.position, lastPosition) > 0.01f ||
               Mathf.Abs(sphereRadius - lastRadius) > 0.01f;
    }
    
    void UpdateCachedTransform()
    {
        lastPosition = transform.position;
        lastRadius = sphereRadius;
    }
    
    void FindAllBillboards()
    {
        // Find AnimatedBillboards in children
        var foundAnimated = GetComponentsInChildren<AnimatedBillboard>();
        animatedBillboards.Clear();
        animatedBillboards.AddRange(foundAnimated);
        
        // Find legacy GameObjects with Billboard component
        var foundLegacy = GetComponentsInChildren<AnimatedBillboard>();
        legacyObjects.Clear();
        foreach (var billboard in foundLegacy)
        {
            legacyObjects.Add(billboard.gameObject);
        }
        
        Debug.Log($"Found {animatedBillboards.Count} AnimatedBillboards and {legacyObjects.Count} legacy billboards");
    }
    
    [ContextMenu("Place All Billboards")]
    void PlaceAllBillboards()
    {
        Vector3 sphereCenter = transform.position;
        
        // Place AnimatedBillboards
        foreach (var billboard in animatedBillboards)
        {
            if (billboard == null) continue;
            
            PlaceBillboardOnSphere(billboard.transform, sphereCenter);
            billboard.SetSphereRadius(sphereRadius);
        }
        
        // Place legacy objects
        foreach (var obj in legacyObjects)
        {
            if (obj == null) continue;
            
            PlaceBillboardOnSphere(obj.transform, sphereCenter);
        }
        
        UpdateCachedTransform();
    }
    
    void PlaceBillboardOnSphere(Transform billboardTransform, Vector3 sphereCenter)
    {
        Vector3 directionFromCenter = (billboardTransform.position - sphereCenter).normalized;
        Vector3 surfacePosition = sphereCenter + directionFromCenter * sphereRadius;
        billboardTransform.position = surfacePosition;
    }
    
    [ContextMenu("Create Test Billboards")]
    void CreateTestBillboards()
    {
        if (!createTestBillboards || testSprites == null || testSprites.Length == 0) return;
        
        // Remove existing test billboards
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("TestBillboard_"))
            {
                DestroyImmediate(child.gameObject);
            }
        }
        
        animatedBillboards.Clear();
        
        // Create new test billboards in circle
        for (int i = 0; i < testBillboardCount; i++)
        {
            float angle = (float)i / testBillboardCount * 360f * Mathf.Deg2Rad;
            Vector3 position = transform.position + new Vector3(
                Mathf.Cos(angle) * sphereRadius,
                0f,
                Mathf.Sin(angle) * sphereRadius
            );
            
            GameObject billboardObj = new GameObject($"TestBillboard_{i:00}");
            billboardObj.transform.SetParent(transform);
            billboardObj.transform.position = position;
            
            var spriteRenderer = billboardObj.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = testSprites[i % testSprites.Length];
            
            var animatedBillboard = billboardObj.AddComponent<AnimatedBillboard>();
            animatedBillboard.SetAnimationSprites(testSprites);
            animatedBillboard.SetSphereRadius(sphereRadius);
            
            animatedBillboards.Add(animatedBillboard);
        }
        
        Debug.Log($"Created {testBillboardCount} test billboards");
    }
    
    public void AddAnimatedBillboard(AnimatedBillboard billboard)
    {
        if (billboard != null && !animatedBillboards.Contains(billboard))
        {
            animatedBillboards.Add(billboard);
            PlaceBillboardOnSphere(billboard.transform, transform.position);
            billboard.SetSphereRadius(sphereRadius);
        }
    }
    
    public void RemoveAnimatedBillboard(AnimatedBillboard billboard)
    {
        animatedBillboards.Remove(billboard);
    }
    
    public void SetSphereRadius(float radius)
    {
        sphereRadius = Mathf.Max(0.1f, radius);
        
        // Update all billboards
        foreach (var billboard in animatedBillboards)
        {
            if (billboard != null)
                billboard.SetSphereRadius(sphereRadius);
        }
        
        if (updateInRealtime)
        {
            PlaceAllBillboards();
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sphereRadius);
        
        // Show connections to billboards
        Gizmos.color = Color.green;
        foreach (var billboard in animatedBillboards)
        {
            if (billboard != null)
            {
                Gizmos.DrawLine(transform.position, billboard.transform.position);
            }
        }
        
        foreach (var obj in legacyObjects)
        {
            if (obj != null)
            {
                Gizmos.DrawLine(transform.position, obj.transform.position);
            }
        }
    }
    
    void OnValidate()
    {
        if (Application.isPlaying && updateInRealtime)
        {
            PlaceAllBillboards();
        }
    }
}
