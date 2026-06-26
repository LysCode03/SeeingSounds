using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class BatterySocket : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;
    private bool isFilled = false;

    public BatteryPuzzle parentPuzzle;

    void Awake()
    {
        socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        socket.selectEntered.AddListener(OnBatteryInserted);
        socket.selectExited.AddListener(OnBatteryRemoved);
    }

    void OnBatteryInserted(SelectEnterEventArgs args)
    {
        isFilled = true;
        parentPuzzle.CheckCompletion();
    }

    void OnBatteryRemoved(SelectExitEventArgs args)
    {
        isFilled = false;
        parentPuzzle.CheckCompletion();
    }

    public void LockObject()
    {
        var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>()
            .firstInteractableSelected;
        
        if (interactable != null)
        {
            var grab = (interactable as MonoBehaviour)
                ?.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null) grab.enabled = false;
        }
    }

    public bool IsFilled() => isFilled;
}
