using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Echolocation : MonoBehaviour
{
    public GameObject echolocationLight;
    public InputActionReference openMenuAction;
    public float lightDuration = 2f; // to simualte echolocation

    public void Awake()
    {
        openMenuAction.action.Enable(); // the input system acts weird if we don't activate and disable, at respective times
        openMenuAction.action.performed += ActivateEcholocation;
        InputSystem.onDeviceChange += OnDeviceChange; // in case, for exmaple, the controller is disconnected
    }

    private void OnDestroy()
    {
        openMenuAction.action.Disable();
        openMenuAction.action.performed -= ActivateEcholocation;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void  ActivateEcholocation(InputAction.CallbackContext context)
    {
        if (!echolocationLight.activeSelf)
            StartCoroutine(EcholocationPulse());
    }

    private IEnumerator EcholocationPulse()
    {
        echolocationLight.SetActive(true);
        yield return new WaitForSeconds(lightDuration);
        echolocationLight.SetActive(false);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch(change)
        {
            case InputDeviceChange.Disconnected:
                openMenuAction.action.Disable();
                openMenuAction.action.performed -= ActivateEcholocation;
                break;
            case InputDeviceChange.Reconnected:
                 openMenuAction.action.Enable();
                 openMenuAction.action.performed += ActivateEcholocation;
                 break;
        }
    }
}
