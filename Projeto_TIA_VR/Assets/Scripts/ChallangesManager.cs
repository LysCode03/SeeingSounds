using UnityEngine;

public class ChallangesManager : MonoBehaviour
{
    public static ChallangesManager Instance; // making sure anyone can access it

    public int completedChallanges = 0;
    public int totalChallanges = 5;

    void Awake()
    {
        Instance = this;
    }

    public void CompleteChallange()
    {
        completedChallanges++;
        Debug.Log($"Challanges: {completedChallanges}/{totalChallanges}");

        if (completedChallanges >= totalChallanges)
        {
            Debug.Log("All objectives complete!");
        }
    }
}
