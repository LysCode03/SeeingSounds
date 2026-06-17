using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central hub for the echolocation reveal. Collects all active sound "pulses"
/// (one-shot expanding waves + constant emitters) and pushes them to the
/// <c>Custom/EchoReveal</c> shader as global properties every frame.
///
/// Put ONE of these in the scene. Any GameObject using a material with the
/// EchoReveal shader will then react to sounds automatically.
/// </summary>
[DefaultExecutionOrder(1000)] // run after emitters' Update so freshly-emitted pulses are included this frame
[AddComponentMenu("Echolocation/Echo Reveal Manager")]
public class EchoRevealManager : MonoBehaviour
{
    // Must match ECHO_MAX_PULSES in EchoReveal.shader.
    public const int MaxPulses = 16;

    public static EchoRevealManager Instance { get; private set; }

    [Header("Look & feel (tweak at runtime)")]
    [Tooltip("Brightness applied to surfaces with no sound nearby. 0 = pitch black.")]
    [Range(0f, 1f)] public float ambient = 0.02f;

    [Tooltip("Colour of the wave's bright leading edge.")]
    public Color edgeColor = new Color(0.6f, 0.85f, 1f, 1f); // light blue

    [Tooltip("Colour of the revealed interior. White shows the surfaces' true colours.")]
    public Color interiorColor = Color.white;

    [Tooltip("Seconds the bright leading edge of a wave stays visible as it sweeps past a surface.")]
    [Range(0.01f, 1f)] public float ringTime = 0.12f;

    [Tooltip("Master brightness multiplier for the whole reveal. Lower = dimmer overall.")]
    [Range(0f, 2f)] public float brightness = 0.8f;

    [Tooltip("Brightness of the wave's leading-edge outline.")]
    [Range(0f, 2f)] public float edgeStrength = 1f;

    [Tooltip("Sharpness of the leading-edge outline. Higher = thinner, crisper line.")]
    [Range(1f, 8f)] public float edgeSharpness = 2f;

    [Tooltip("Brightness of the dim glow lingering inside the wave.")]
    [Range(0f, 1f)] public float interiorStrength = 0.45f;

    [Header("Range")]
    [Tooltip("Multiplier applied to every wave's radius. Below 1 shrinks the reach of all sounds at once.")]
    [Range(0.05f, 2f)] public float rangeScale = 1f;

    [Tooltip("Distance falloff exponent for the leading edge. Higher pulls the bright ring in toward the origin, reducing visible range.")]
    [Range(1f, 8f)] public float falloff = 2f;

    [Tooltip("ON: the interior fades from the centre outwards, following the wave. OFF: legacy centre-peaked look (fades edge-first).")]
    public bool fadeInsideOut = true;

    [Tooltip("Inside-out mode only: metres over which the interior fades out near its max range. Smaller = harder outer edge.")]
    [Range(0.1f, 10f)] public float rangeSoftness = 2f;

    [Header("Timing & motion (global multipliers)")]
    [Tooltip("Multiplies every wave's expansion speed. Below 1 = slower-moving wavefront.")]
    [Range(0.1f, 3f)] public float speedScale = 1f;

    [Tooltip("Multiplies how long revealed surfaces take to fade back to dark.")]
    [Range(0.1f, 3f)] public float fadeScale = 1f;

    private struct Pulse
    {
        public Vector3 origin;
        public float startTime;
        public float speed;
        public float maxRadius;
        public float fade;
        public float intensity;
    }

    private readonly List<Pulse> _oneShots = new List<Pulse>();
    private readonly List<EchoSoundEmitter> _constants = new List<EchoSoundEmitter>();

    private readonly Vector4[] _a = new Vector4[MaxPulses];
    private readonly Vector4[] _b = new Vector4[MaxPulses];

    private static readonly int IdA       = Shader.PropertyToID("_EchoPulseA");
    private static readonly int IdB       = Shader.PropertyToID("_EchoPulseB");
    private static readonly int IdCount   = Shader.PropertyToID("_EchoPulseCount");
    private static readonly int IdTime    = Shader.PropertyToID("_EchoTime");
    private static readonly int IdAmbient = Shader.PropertyToID("_EchoAmbient");
    private static readonly int IdRing     = Shader.PropertyToID("_EchoRingTime");
    private static readonly int IdEdgeCol  = Shader.PropertyToID("_EchoEdgeColor");
    private static readonly int IdInterCol = Shader.PropertyToID("_EchoInteriorColor");
    private static readonly int IdBright   = Shader.PropertyToID("_EchoBrightness");
    private static readonly int IdEdge     = Shader.PropertyToID("_EchoEdgeStrength");
    private static readonly int IdEdgeSharp = Shader.PropertyToID("_EchoEdgeSharpness");
    private static readonly int IdInterior = Shader.PropertyToID("_EchoInteriorStrength");
    private static readonly int IdFalloff  = Shader.PropertyToID("_EchoFalloff");
    private static readonly int IdFadeIO   = Shader.PropertyToID("_EchoFadeInsideOut");
    private static readonly int IdRangeSoft = Shader.PropertyToID("_EchoRangeSoftness");

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[Echo] More than one EchoRevealManager in the scene. Using the most recent one.", this);
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Emit a single expanding reveal wave from a world position.</summary>
    public void EmitPulse(Vector3 origin, float speed, float maxRadius, float fade, float intensity)
    {
        if (_oneShots.Count >= MaxPulses)
            _oneShots.RemoveAt(0); // budget full: drop the oldest wave

        _oneShots.Add(new Pulse
        {
            origin    = origin,
            startTime = Time.time,
            speed     = Mathf.Max(0.01f, speed),
            maxRadius = Mathf.Max(0.01f, maxRadius),
            fade      = Mathf.Max(0.001f, fade),
            intensity = intensity
        });
    }

    /// <summary>Register a constant (always-on) sound emitter.</summary>
    public void RegisterConstant(EchoSoundEmitter emitter)
    {
        if (emitter != null && !_constants.Contains(emitter))
            _constants.Add(emitter);
    }

    public void UnregisterConstant(EchoSoundEmitter emitter)
    {
        _constants.Remove(emitter);
    }

    private void LateUpdate()
    {
        float now = Time.time;

        // Drop one-shot waves that have fully expanded and faded out.
        // Lifetime must use the SCALED values, or slowing a wave down could cull it early.
        for (int i = _oneShots.Count - 1; i >= 0; i--)
        {
            Pulse p = _oneShots[i];
            float lifetime = (p.maxRadius * rangeScale) / (p.speed * speedScale) + p.fade * fadeScale;
            if (now - p.startTime > lifetime)
                _oneShots.RemoveAt(i);
        }

        int count = 0;

        // Constant sounds get priority on the budget.
        for (int i = 0; i < _constants.Count && count < MaxPulses; i++)
        {
            EchoSoundEmitter e = _constants[i];
            if (e == null || !e.isActiveAndEnabled) continue;

            Vector3 pos = e.transform.position;
            _a[count] = new Vector4(pos.x, pos.y, pos.z, now);
            _b[count] = new Vector4(0f, e.maxRadius * rangeScale, 1f, e.intensity); // speed 0 => constant
            count++;
        }

        // Then the live one-shot waves.
        for (int i = 0; i < _oneShots.Count && count < MaxPulses; i++)
        {
            Pulse p = _oneShots[i];
            _a[count] = new Vector4(p.origin.x, p.origin.y, p.origin.z, p.startTime);
            _b[count] = new Vector4(p.speed * speedScale, p.maxRadius * rangeScale, p.fade * fadeScale, p.intensity);
            count++;
        }

        Shader.SetGlobalVectorArray(IdA, _a);
        Shader.SetGlobalVectorArray(IdB, _b);
        Shader.SetGlobalInt(IdCount, count);
        Shader.SetGlobalFloat(IdTime, now);
        Shader.SetGlobalFloat(IdAmbient, ambient);
        Shader.SetGlobalFloat(IdRing, ringTime);
        Shader.SetGlobalColor(IdEdgeCol, edgeColor);
        Shader.SetGlobalColor(IdInterCol, interiorColor);
        Shader.SetGlobalFloat(IdBright, brightness);
        Shader.SetGlobalFloat(IdEdge, edgeStrength);
        Shader.SetGlobalFloat(IdEdgeSharp, edgeSharpness);
        Shader.SetGlobalFloat(IdInterior, interiorStrength);
        Shader.SetGlobalFloat(IdFalloff, falloff);
        Shader.SetGlobalFloat(IdFadeIO, fadeInsideOut ? 1f : 0f);
        Shader.SetGlobalFloat(IdRangeSoft, rangeSoftness);
    }
}
