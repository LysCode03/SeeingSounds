using UnityEngine;
using UnityEngine.InputSystem;

public class Ecolocation : MonoBehaviour
{
    public GameObject ecolocationLight;
    public InputActionReference openMenuAction;

    public void Awake()
    {
        openMenuAction.action.Enable(); // the input system acts weird if we don't activate and disable, at respective times
        openMenuAction.action.performed += ActivateEcolocation;
        InputSystem.onDeviceChange += OnDeviceChange; // in case, for exmaple, the controller is disconnected
    }

    private void OnDestroy()
    {
        openMenuAction.action.Disable();
        openMenuAction.action.performed -= ActivateEcolocation;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void  ActivateEcolocation(InputAction.CallbackContext context)
    {
        ecolocationLight.SetActive(!ecolocationLight.activeSelf);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch(change)
        {
            case InputDeviceChange.Disconnected:
                openMenuAction.action.Disable();
                openMenuAction.action.performed -= ActivateEcolocation;
                break;
            case InputDeviceChange.Reconnected:
                 openMenuAction.action.Enable();
                 openMenuAction.action.performed += ActivateEcolocation;
                 break;
        }
    }
}
