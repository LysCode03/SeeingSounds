using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Makes a sound source reveal its surroundings. Every source is configured
/// independently: give the player short-range, high-frequency waves (footsteps)
/// and a beacon long-range, low-frequency waves just by changing these fields.
///
/// OneShot  - emits an expanding wave per <see cref="Emit"/> call. Can auto-repeat
///            at a set frequency, and/or fire on a key press.
/// Constant - keeps the area around it permanently lit while enabled
///            (radio, dripping pipe, humming machine).
///
/// Optionally drives an AudioSource so the sound you hear matches the reveal you see.
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

    [Tooltip("How far the sound reaches, in metres. Small = a tight wave hugging the source.")]
    public float maxRadius = 8f;

    [Tooltip("Seconds a revealed surface takes to fade back to dark. (OneShot only.)")]
    public float fade = 2.5f;

    [Tooltip("Brightness multiplier for this sound.")]
    [Range(0f, 4f)] public float intensity = 1f;

    [Header("Auto-repeat")]
    [Tooltip("Emit one wave as soon as the scene starts.")]
    public bool emitOnStart = false;

    [Tooltip("Automatically re-emit on a steady timer.")]
    public bool autoRepeat = false;

    [Tooltip("Frequency of the wave: how many waves per second when auto-repeating. " +
             "High = frequent (footsteps); low = occasional (a distant beacon).")]
    public float frequency = 1f;

    [Tooltip("If ON, auto-repeat only fires when the source is moving (checked at each timer tick, " +
             "so small jitters never cause extra waves). If OFF, the timer always fires.")]
    public bool onlyWhenMoving = false;

    [Tooltip("Minimum horizontal speed (m/s) that counts as 'moving'.")]
    public float moveThreshold = 0.15f;

    [Header("Manual trigger")]
    [Tooltip("Allow emitting a wave on a key press (e.g. a deliberate, large 'ping').")]
    public bool triggerOnKey = false;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Key that fires a manual wave.")]
    public Key triggerKey = Key.E;
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
    }

    private void Start()
    {
        // Re-register in case the manager initialised after this emitter.
        if (mode == Mode.Constant && EchoRevealManager.Instance != null)
            EchoRevealManager.Instance.RegisterConstant(this);

        if (mode == Mode.OneShot && emitOnStart)
            Emit();
    }

    private void OnDisable()
    {
        if (EchoRevealManager.Instance != null)
            EchoRevealManager.Instance.UnregisterConstant(this);
    }

    private void Update()
    {
        TrackSpeed();

#if ENABLE_INPUT_SYSTEM
        if (triggerOnKey && Keyboard.current != null && Keyboard.current[triggerKey].wasPressedThisFrame)
            Emit();
#endif

        if (mode != Mode.OneShot || !autoRepeat) return;

        // Steady free-running timer: cadence is unaffected by starting/stopping, so small
        // movements can't pile up extra waves. Movement (if required) is only checked at the tick.
        _timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(frequency, 0.0001f);
        if (_timer >= interval)
        {
            _timer -= interval;
            if (!onlyWhenMoving || _currentSpeed >= moveThreshold)
                Emit();
        }
    }

    /// <summary>Per-frame horizontal speed, used by the optional movement gate.</summary>
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
            mgr.EmitPulse(transform.position, speed, maxRadius, fade, intensity);
    }
}
