using UnityEngine;

public class EcholocationFollow : MonoBehaviour
{
    public Transform headset;
    public float forwardDistance = 1.0f;
    public float fixedY = 0.2f;

    void LateUpdate()
    {
        Vector3 flatForward = headset.forward;
        flatForward.y = 0f;
        flatForward.Normalize();

        Vector3 flatPosition = headset.position;
        flatPosition.y = 0f;

        Vector3 targetPos = flatPosition + flatForward * forwardDistance;

        targetPos.y = fixedY;

        transform.position = targetPos;

        transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
    }
}