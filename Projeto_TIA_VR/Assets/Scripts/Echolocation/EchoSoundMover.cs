using UnityEngine;

/// <summary>
/// Ping-pongs this object between two world points. Pair it with an
/// EchoSoundEmitter (Constant or repeating OneShot) to test how a moving
/// sound source — e.g. footsteps of a walking NPC — reveals the room as it travels.
/// </summary>
[AddComponentMenu("Echolocation/Echo Sound Mover (test only)")]
public class EchoSoundMover : MonoBehaviour
{
    public Vector3 pointA = new Vector3(-3f, 0.1f, -3f);
    public Vector3 pointB = new Vector3(3f, 0.1f, 3f);

    [Tooltip("Movement speed in metres/second.")]
    public float speed = 1.5f;

    private void Update()
    {
        float segment = Vector3.Distance(pointA, pointB);
        if (segment < 0.001f) return;

        float t = Mathf.PingPong(Time.time * speed, segment) / segment;
        transform.position = Vector3.Lerp(pointA, pointB, t);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(pointA, 0.15f);
        Gizmos.DrawSphere(pointB, 0.15f);
        Gizmos.DrawLine(pointA, pointB);
    }
}
