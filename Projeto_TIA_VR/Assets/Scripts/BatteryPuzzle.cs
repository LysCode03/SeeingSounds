using UnityEngine;

public class BatteryPuzzle : MonoBehaviour
{
    public BatterySocket[] sockets;
    public EchoSoundEmitter echoEmitter;

    void Start()
    {
        // Start silent
        if (echoEmitter != null)
            echoEmitter.enabled = false;
    }

    public void CheckCompletion()
    {
        foreach (var socket in sockets)
            if (!socket.IsFilled()) return;

        Debug.Log("Battery puzzle complete!");
        if (echoEmitter != null)
        {
            echoEmitter.enabled = true;
            echoEmitter.SetConstant();
        }

        echoEmitter.edgeColor = Color.white;
        echoEmitter.interiorColor = Color.white;
        ChallangesManager.Instance.CompleteChallange();
    }
}
