using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Makes a sound source reveal its surroundings. EVERY setting here is per-source,
/// so each emitter can have its own range, speed, timing, colour and shape:
/// e.g. small fast blue footsteps vs a big slow orange beacon.
///
/// OneShot  - emits an expanding wave per <see cref="Emit"/> call; can auto-repeat
///            at a set frequency and/or fire from a key / input action.
/// Constant - keeps the area around it lit while enabled (radio, machine, lamp).
/// </summary>
[AddComponentMenu("Echolocation/Echo Sound Emitter")]
public class EchoSoundEmitter : MonoBehaviour
{
    public enum Mode { OneShot, Constant }

    [Header("Mode")]
    public Mode mode = Mode.OneShot;

    [Header("Wave shape (per source)")]
    [Tooltip("How fast the wavefront expands, in metres/second. (OneShot only.)")]
    public float speed = 8f;

    [Tooltip("How far the sound reaches, in metres.")]
    public float maxRadius = 8f;

    [Tooltip("Seconds a revealed surface takes to fade back to dark. (OneShot only.)")]
    public float fade = 2.5f;

    [Tooltip("Overall brightness of this sound.")]
    [Range(0f, 4f)] public float intensity = 1f;

    [Header("Wave look (per source)")]
    [Tooltip("Colour of the bright leading edge.")]
    public Color edgeColor = new Color(0.6f, 0.85f, 1f, 1f);

    [Tooltip("Colour of the revealed interior. White shows surfaces' true colours.")]
    public Color interiorColor = Color.white;

    [Tooltip("Brightness of the leading-edge outline.")]
    [Range(0f, 2f)] public float edgeStrength = 1f;

    [Tooltip("Outline sharpness (higher = thinner, crisper line).")]
    [Range(1f, 8f)] public float edgeSharpness = 2f;

    [Tooltip("Brightness of the lingering interior glow.")]
    [Range(0f, 1f)] public float interiorStrength = 0.45f;

    [Tooltip("Thickness/duration of the leading-edge outline (seconds).")]
    [Range(0.01f, 1f)] public float ringTime = 0.12f;

    [Tooltip("Distance falloff exponent (higher = shorter visible range).")]
    [Range(1f, 8f)] public float falloff = 2f;

    [Tooltip("ON: interior fades centre-first (inside-out). OFF: legacy centre-peaked look.")]
    public bool fadeInsideOut = true;

    [Header("Auto-repeat")]
    [Tooltip("Emit one wave as soon as the scene starts.")]
    public bool emitOnStart = false;

    [Tooltip("Automatically re-emit on a steady timer.")]
    public bool autoRepeat = false;

    [Tooltip("Waves per second when auto-repeating. High = frequent (footsteps); low = occasional (beacon).")]
    public float frequency = 1f;

    [Tooltip("If ON, auto-repeat only fires when moving (checked at each tick, so small jitters add no extra waves).")]
    public bool onlyWhenMoving = false;

    [Tooltip("Minimum horizontal speed (m/s) that counts as 'moving'.")]
    public float moveThreshold = 0.15f;

    [Tooltip("Pushes the wave's origin ahead in the direction of travel (metres), so moving forward " +
             "doesn't leave the wave behind you. 0 = emit exactly at the source.")]
    [Range(0f, 5f)] public float moveBias = 0f;

    [Header("Manual trigger")]
    [Tooltip("Allow emitting a wave on a key press (e.g. a deliberate, large 'ping').")]
    public bool triggerOnKey = false;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Keyboard key that fires a manual wave (desktop testing only - there's no keyboard in the headset).")]
    public Key triggerKey = Key.E;

    [Tooltip("Optional: an input action (e.g. a VR controller button) that also fires a manual wave. " +
             "Bind this in the Inspector to make the wave work in the headset.")]
    public InputActionReference triggerAction;
#endif

    [Header("Audio (optional)")]
    [Tooltip("If set, the matching sound plays when a wave is emitted.")]
    public AudioSource audioSource;

    [Tooltip("OneShot mode: played via PlayOneShot each emission. Leave empty to just .Play() the AudioSource.")]
    public AudioClip oneShotClip;

    private float _timer;
    private Vector3 _lastPos;
    private bool _hasLastPos;
    private float _currentSpeed;
    private Vector3 _moveDir; // unit horizontal direction of travel (zero when stationary)

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (mode == Mode.Constant)
        {
            if (EchoRevealManager.Instance != null)
                EchoRevealManager.Instance.RegisterConstant(this);

            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.loop = true;
                audioSource.Play();
            }
        }

#if ENABLE_INPUT_SYSTEM
        if (triggerAction != null && triggerAction.action != null)
        {
            triggerAction.action.performed += OnTriggerActionPerformed;
            triggerAction.action.Enable();
        }
#endif
    }

    private void Start()
    {
        if (mode == Mode.Constant && EchoRevealManager.Instance != null)
            EchoRevealManager.Instance.RegisterConstant(this);

        if (mode == Mode.OneShot && emitOnStart)
            Emit();
    }

    private void OnDisable()
    {
        if (EchoRevealManager.Instance != null)
            EchoRevealManager.Instance.UnregisterConstant(this);

#if ENABLE_INPUT_SYSTEM
        if (triggerAction != null && triggerAction.action != null)
            triggerAction.action.performed -= OnTriggerActionPerformed;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void OnTriggerActionPerformed(InputAction.CallbackContext context) => Emit();
#endif

    private void Update()
    {
        TrackSpeed();

#if ENABLE_INPUT_SYSTEM
        if (triggerOnKey && Keyboard.current != null && Keyboard.current[triggerKey].wasPressedThisFrame)
            Emit();
#endif

        if (mode != Mode.OneShot || !autoRepeat) return;

        _timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(frequency, 0.0001f);
        if (_timer >= interval)
        {
            _timer -= interval;
            if (!onlyWhenMoving || _currentSpeed >= moveThreshold)
                Emit();
        }
    }

    private void TrackSpeed()
    {
        Vector3 pos = transform.position;
        if (!_hasLastPos)
        {
            _lastPos = pos;
            _hasLastPos = true;
            _currentSpeed = 0f;
            return;
        }

        Vector3 delta = pos - _lastPos;
        _lastPos = pos;
        delta.y = 0f; // ignore vertical (head bob / crouch)

        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        _currentSpeed = delta.magnitude / dt;

        if (_currentSpeed >= moveThreshold && delta.sqrMagnitude > 1e-10f)
            _moveDir = delta.normalized;
        else
            _moveDir = Vector3.zero;
    }

    /// <summary>All of this source's wave + look settings as one struct.</summary>
    public EchoRevealManager.WaveSettings GetWaveSettings()
    {
        return new EchoRevealManager.WaveSettings
        {
            speed = speed,
            maxRadius = maxRadius,
            fade = fade,
            intensity = intensity,
            edgeColor = edgeColor,
            interiorColor = interiorColor,
            edgeStrength = edgeStrength,
            edgeSharpness = edgeSharpness,
            interiorStrength = interiorStrength,
            ringTime = ringTime,
            falloff = falloff,
            fadeInsideOut = fadeInsideOut
        };
    }

    /// <summary>Emit a single expanding reveal wave from this object's position.</summary>
    public void Emit()
    {
        if (audioSource != null)
        {
            if (oneShotClip != null) audioSource.PlayOneShot(oneShotClip);
            else if (!audioSource.isPlaying) audioSource.Play();
        }

        EchoRevealManager mgr = EchoRevealManager.Instance;
        if (mgr != null)
            mgr.EmitPulse(transform.position + _moveDir * moveBias, GetWaveSettings());
    }
    
    // Change Mode to constant
    public void SetConstant()
    {
        mode = Mode.Constant;
        if (EchoRevealManager.Instance != null)
            EchoRevealManager.Instance.RegisterConstant(this);

        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.loop = true;
            audioSource.Play();
        }
    }
}
