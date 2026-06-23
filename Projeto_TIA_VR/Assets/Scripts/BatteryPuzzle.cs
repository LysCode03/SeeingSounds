using UnityEngine;

public class BatteryPuzzle : MonoBehaviour
{
    public BatterySocket[] sockets; // assign all sockets in Inspector

    public void CheckCompletion()
    {
        foreach (var socket in sockets)
        {
            if (!socket.IsFilled()) return;
        }

        Debug.Log("Battery puzzle complete!");
        ChallangesManager.Instance.CompleteChallange();
    }
}
