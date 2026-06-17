using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tiny non-VR fly camera for testing the echo reveal in the editor Game view,
/// so you don't need to put the headset on for every tweak.
/// Hold right mouse to look, WASD to move, Q/E down/up, Shift to go faster.
/// Uses the new Input System (the package this project already uses).
/// </summary>
[AddComponentMenu("Echolocation/Echo Free Look (test only)")]
public class EchoFreeLook : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float lookSpeed = 0.12f;

    private float _yaw;
    private float _pitch;

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null) return;

        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * lookSpeed;
            _pitch -= delta.y * lookSpeed;
            _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += Vector3.forward;
        if (kb.sKey.isPressed) move += Vector3.back;
        if (kb.aKey.isPressed) move += Vector3.left;
        if (kb.dKey.isPressed) move += Vector3.right;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move += Vector3.down;

        float speed = moveSpeed * (kb.leftShiftKey.isPressed ? 3f : 1f);
        transform.Translate(move.normalized * speed * Time.deltaTime, Space.Self);
#endif
    }
}
