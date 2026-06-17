using UnityEngine;

/// <summary>
/// Makes a sound source reveal its surroundings.
///
/// OneShot  - emits a single expanding wave each time <see cref="Emit"/> is called
///            (great for footsteps, knocks, dropped objects). Can auto-repeat for testing.
/// Constant - keeps the area around it permanently lit while enabled
///            (great for a radio, dripping pipe, humming machine).
///
/// Optionally drives an AudioSource so the sound you hear matches the reveal you see.
/// </summary>
[AddComponentMenu("Echolocation/Echo Sound Emitter")]
public class EchoSoundEmitter : MonoBehaviour
{
    public enum Mode { OneShot, Constant }

    [Header("Mode")]
    public Mode mode = Mode.OneShot;

    [Header("Wave shape")]
    [Tooltip("How fast the wavefront expands, in metres/second. (OneShot only.)")]
    public float speed = 8f;

    [Tooltip("How far the sound reaches, in metres.")]
    public float maxRadius = 8f;

    [Tooltip("Seconds a revealed surface takes to fade back to dark. (OneShot only.)")]
    public float fade = 2.5f;

    [Tooltip("Brightness multiplier for this sound.")]
    [Range(0f, 4f)] public float intensity = 1f;

    [Header("One-shot triggering")]
    [Tooltip("Emit one wave as soon as the scene starts.")]
    public bool emitOnStart = false;

    [Tooltip("Keep re-emitting on an interval. Handy for simulating footsteps while testing.")]
    public bool autoRepeat = false;

    [Tooltip("Seconds between auto-repeat emissions.")]
    public float repeatInterval = 0.6f;

    [Tooltip("Only emit while this object is actually moving (real footsteps). Tracks world-space speed.")]
    public bool onlyWhenMoving = false;

    [Tooltip("Minimum horizontal speed (m/s) that counts as 'moving'.")]
    public float moveThreshold = 0.15f;

    [Header("Audio (optional)")]
    [Tooltip("If set, the matching sound plays when a wave is emitted.")]
    public AudioSource audioSource;

    [Tooltip("OneShot mode: played via PlayOneShot each emission. Leave empty to just .Play() the AudioSource.")]
    public AudioClip oneShotClip;

    private float _timer;
    private Vector3 _lastPos;
    private bool _hasLastPos;

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
        if (mode != Mode.OneShot || !autoRepeat) return;

        if (onlyWhenMoving && !IsMoving())
        {
            _timer = repeatInterval; // primed so a step fires the moment movement resumes
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= repeatInterval)
        {
            _timer = 0f;
            Emit();
        }
    }

    /// <summary>True if this object's horizontal speed exceeds the move threshold this frame.</summary>
    private bool IsMoving()
    {
        Vector3 pos = transform.position;
        if (!_hasLastPos)
        {
            _lastPos = pos;
            _hasLastPos = true;
            return false;
        }

        Vector3 delta = pos - _lastPos;
        _lastPos = pos;
        delta.y = 0f; // ignore vertical (head bob / crouch)

        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        return (delta.magnitude / dt) >= moveThreshold;
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
