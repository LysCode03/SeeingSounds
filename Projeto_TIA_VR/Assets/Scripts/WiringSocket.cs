using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class WiringSocket : MonoBehaviour
{
    public int expectedShapeOrder;
    public WiringPuzzle parentPuzzle;

    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;
    private bool isCorrectlyFilled = false;
    private bool hasWrongPiece = false;

    void Awake()
    {
        socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        socket.selectEntered.AddListener(OnShapeInserted);
        socket.selectExited.AddListener(OnShapeRemoved);
    }

    void OnShapeInserted(SelectEnterEventArgs args)
    {
        WiringShape shape = args.interactableObject.transform.GetComponent<WiringShape>();

        if (shape == null) return;

        if (shape.shapeOrder == expectedShapeOrder)
        {
            isCorrectlyFilled = true;
            hasWrongPiece = false;
            parentPuzzle.AdvanceOrder();
            parentPuzzle.CheckCompletion();
            parentPuzzle.RefreshVisual();
        }
        else
        {
            isCorrectlyFilled = false;
            hasWrongPiece = true;
            parentPuzzle.RefreshVisual();
        }
    }

    void OnShapeRemoved(SelectExitEventArgs args)
    {
        if (isCorrectlyFilled)
        {
            isCorrectlyFilled = false;
        }

        hasWrongPiece = false;
        parentPuzzle.RefreshVisual();
    }

    public bool IsCorrectlyFilled() => isCorrectlyFilled;
    public bool HasWrongPiece() => hasWrongPiece;
}