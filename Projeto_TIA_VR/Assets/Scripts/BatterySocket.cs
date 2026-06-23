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

    public bool IsFilled() => isFilled;
}
