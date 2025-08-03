using UnityEngine;
public class SphereLevelInput : MonoBehaviour
{
    private SphereLevelController controller;
    
    void Start()
    {
        controller = GetComponent<SphereLevelController>();
    }
    
    void Update()
    {
        // Rotation
        if (Input.GetKeyDown(KeyCode.A)) controller.Rotate(RotationCommand.Left90);
        if (Input.GetKeyDown(KeyCode.D)) controller.Rotate(RotationCommand.Right90);
        if (Input.GetKeyDown(KeyCode.W)) controller.Rotate(RotationCommand.Flip180);
        if (Input.GetKeyDown(KeyCode.S)) controller.Rotate(RotationCommand.FlipCounter180);
        
        // States 1-8
        for (int i = 0; i < 8; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) controller.LoadState(i);
        }
    }
}