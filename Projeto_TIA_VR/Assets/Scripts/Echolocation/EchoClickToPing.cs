using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Testing helper: left-click to fire a one-shot reveal wave from wherever the
/// cursor hits a surface, or press Space to ping straight ahead from the camera.
/// Lets you probe the room interactively without scripting trigger events.
/// </summary>
[AddComponentMenu("Echolocation/Echo Click To Ping (test only)")]
public class EchoClickToPing : MonoBehaviour
{
    public Camera cam;

    [Header("Ping wave")]
    public float speed = 8f;
    public float maxRadius = 10f;
    public float fade = 3f;
    [Range(0f, 4f)] public float intensity = 1.2f;

    [Tooltip("How far the click ray reaches.")]
    public float rayDistance = 50f;

    private void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            PingFromScreen(mouse.position.ReadValue());

        if (kb != null && kb.spaceKey.wasPressedThisFrame && cam != null)
            Ping(cam.transform.position + cam.transform.forward * 2f);
#endif
    }

    private void PingFromScreen(Vector2 screenPos)
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            Ping(hit.point);
        else
            Ping(ray.origin + ray.direction * rayDistance); // no surface hit: ping into the dark
    }

    private void Ping(Vector3 worldPos)
    {
        EchoRevealManager mgr = EchoRevealManager.Instance;
        if (mgr != null)
            mgr.EmitPulse(worldPos, speed, maxRadius, fade, intensity);
    }
}
