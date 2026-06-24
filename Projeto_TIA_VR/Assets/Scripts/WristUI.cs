using UnityEngine;
using TMPro;

public class WristUI : MonoBehaviour
{
    public static WristUI Instance;

    public TextMeshProUGUI counterText;
    public int total = 5; // match your ChallangesManager total

    void Awake()
    {
        Instance = this;
    }

    public void UpdateCounter(int completed)
    {
        counterText.text = $"Challenges\n{completed}/{total}";
    }
}