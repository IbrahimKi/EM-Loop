using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private bool autoRegister = true;
    
    void OnEnable()
    {
        if (autoRegister)
        {
            BillboardManager.RegisterBillboard(this);
        }
    }
    
    void OnDisable()
    {
        BillboardManager.UnregisterBillboard(this);
    }
    
    // Manuell registrieren/abmelden
    public void Register() => BillboardManager.RegisterBillboard(this);
    public void Unregister() => BillboardManager.UnregisterBillboard(this);
}
