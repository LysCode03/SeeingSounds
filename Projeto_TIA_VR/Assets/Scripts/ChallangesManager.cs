using UnityEngine;

public class ChallengesManager : MonoBehaviour
{
    public static ChallengesManager Instance; // making sure anyone can access it

    public int completedChallenges = 0;
    public int totalChallenges = 5;

    void Awake()
    {
        Instance = this;
    }

    public void CompleteChallange()
    {
        completedChallenges++;
        Debug.Log($"Challenges: {completedChallenges}/{totalChallenges}");

        WristUI.Instance.UpdateCounter(completedChallenges);

        if (completedChallenges >= totalChallenges)
        {
            Debug.Log("All objectives complete!");
            WristUI.Instance.EndGameCounter();
        }
    }
}
