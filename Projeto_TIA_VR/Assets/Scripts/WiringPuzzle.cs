using UnityEngine;

public class WiringPuzzle : MonoBehaviour
{
    public WiringSocket[] sockets;
    public EchoSoundEmitter echoEmitter;
    public Color correctColor;
    public Color errorColor = Color.red;

    private int currentOrder = 0;

    public void AdvanceOrder() => currentOrder++;

    public void RefreshVisual()
    {
        if (echoEmitter == null) return;
        foreach (var socket in sockets)
        {
            if (socket.HasWrongPiece())
            {
                echoEmitter.edgeColor = errorColor;
                echoEmitter.interiorColor = errorColor;
                return;
            }
        }

        if (currentOrder >= sockets.Length)
        {
            SetCompleted();
            return;
        }

        echoEmitter.edgeColor = correctColor;
        echoEmitter.interiorColor = correctColor;
    }

    public void CheckCompletion() => RefreshVisual();

    public void SetCompleted()
    {
        echoEmitter.edgeColor = Color.white;
        echoEmitter.interiorColor = Color.white;
        echoEmitter.SetConstant();
        ChallangesManager.Instance.CompleteChallange();
    }
}

