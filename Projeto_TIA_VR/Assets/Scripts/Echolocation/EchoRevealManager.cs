using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central hub for the echolocation reveal. Collects all active waves (one-shot +
/// constant emitters) and pushes them to the <c>Custom/EchoReveal</c> shader as
/// global properties every frame. Each wave carries its OWN look (colour, edge,
/// interior, falloff, fade direction) so sources are fully independent.
///
/// Put ONE of these in the scene.
/// </summary>
[DefaultExecutionOrder(1000)] // run after emitters' Update so freshly-emitted waves are included this frame
[AddComponentMenu("Echolocation/Echo Reveal Manager")]
public class EchoRevealManager : MonoBehaviour
{
    // Must match ECHO_MAX_PULSES in EchoReveal.shader.
    public const int MaxPulses = 16;

    public static EchoRevealManager Instance { get; private set; }

    [Header("Scene (global)")]
    [Tooltip("Brightness applied to surfaces with no sound nearby. 0 = pitch black.")]
    [Range(0f, 1f)] public float ambient = 0.02f;

    [Tooltip("Master multiplier over every wave's reveal.")]
    [Range(0f, 2f)] public float brightness = 0.8f;

    [Tooltip("Metres over which interiors fade out near their max range (inside-out mode).")]
    [Range(0.1f, 10f)] public float rangeSoftness = 2f;

    [Header("Global multipliers (scale every source)")]
    [Range(0.05f, 2f)] public float rangeScale = 1f;
    [Range(0.1f, 3f)] public float speedScale = 1f;
    [Range(0.1f, 3f)] public float fadeScale = 1f;

    /// <summary>A single source's full wave description: geometry + look.</summary>
    public struct WaveSettings
    {
        public float speed, maxRadius, fade, intensity;
        public Color edgeColor, interiorColor;
        public float edgeStrength, edgeSharpness, interiorStrength, ringTime, falloff;
        public bool fadeInsideOut;
    }

    private struct Pulse
    {
        public Vector3 origin;
        public float startTime;
        public WaveSettings s;
    }

    private readonly List<Pulse> _oneShots = new List<Pulse>();
    private readonly List<EchoSoundEmitter> _constants = new List<EchoSoundEmitter>();

    private readonly Vector4[] _a = new Vector4[MaxPulses]; // origin.xyz, startTime
    private readonly Vector4[] _b = new Vector4[MaxPulses]; // speed(<=0=constant), maxRadius, fade, intensity
    private readonly Vector4[] _c = new Vector4[MaxPulses]; // edgeColor.rgb, edgeStrength
    private readonly Vector4[] _d = new Vector4[MaxPulses]; // interiorColor.rgb, interiorStrength
    private readonly Vector4[] _e = new Vector4[MaxPulses]; // ringTime, falloff, edgeSharpness, fadeInsideOut

    private static readonly int IdA         = Shader.PropertyToID("_EchoPulseA");
    private static readonly int IdB         = Shader.PropertyToID("_EchoPulseB");
    private static readonly int IdC         = Shader.PropertyToID("_EchoPulseC");
    private static readonly int IdD         = Shader.PropertyToID("_EchoPulseD");
    private static readonly int IdE         = Shader.PropertyToID("_EchoPulseE");
    private static readonly int IdCount     = Shader.PropertyToID("_EchoPulseCount");
    private static readonly int IdTime      = Shader.PropertyToID("_EchoTime");
    private static readonly int IdAmbient   = Shader.PropertyToID("_EchoAmbient");
    private static readonly int IdBright    = Shader.PropertyToID("_EchoBrightness");
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

    /// <summary>Emit a single expanding reveal wave from a world position with the given look.</summary>
    public void EmitPulse(Vector3 origin, WaveSettings s)
    {
        if (_oneShots.Count >= MaxPulses)
            _oneShots.RemoveAt(0); // budget full: drop the oldest wave

        s.speed = Mathf.Max(0.01f, s.speed);
        s.maxRadius = Mathf.Max(0.01f, s.maxRadius);
        s.fade = Mathf.Max(0.001f, s.fade);

        _oneShots.Add(new Pulse { origin = origin, startTime = Time.time, s = s });
    }

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

        // Drop one-shot waves that have fully expanded and faded out (using the scaled timing).
        for (int i = _oneShots.Count - 1; i >= 0; i--)
        {
            Pulse p = _oneShots[i];
            float lifetime = (p.s.maxRadius * rangeScale) / (p.s.speed * speedScale) + p.s.fade * fadeScale;
            if (now - p.startTime > lifetime)
                _oneShots.RemoveAt(i);
        }

        int count = 0;

        // Constant sources first (priority on the budget).
        for (int i = 0; i < _constants.Count && count < MaxPulses; i++)
        {
            EchoSoundEmitter e = _constants[i];
            if (e == null || !e.isActiveAndEnabled) continue;
            Pack(count, e.transform.position, now, e.GetWaveSettings(), true);
            count++;
        }

        // Then the live one-shot waves.
        for (int i = 0; i < _oneShots.Count && count < MaxPulses; i++)
        {
            Pulse p = _oneShots[i];
            Pack(count, p.origin, p.startTime, p.s, false);
            count++;
        }

        Shader.SetGlobalVectorArray(IdA, _a);
        Shader.SetGlobalVectorArray(IdB, _b);
        Shader.SetGlobalVectorArray(IdC, _c);
        Shader.SetGlobalVectorArray(IdD, _d);
        Shader.SetGlobalVectorArray(IdE, _e);
        Shader.SetGlobalInt(IdCount, count);
        Shader.SetGlobalFloat(IdTime, now);
        Shader.SetGlobalFloat(IdAmbient, ambient);
        Shader.SetGlobalFloat(IdBright, brightness);
        Shader.SetGlobalFloat(IdRangeSoft, rangeSoftness);
    }

    private void Pack(int k, Vector3 origin, float startTime, WaveSettings s, bool constant)
    {
        float speed = constant ? 0f : s.speed * speedScale; // 0 flags a constant source in the shader
        _a[k] = new Vector4(origin.x, origin.y, origin.z, startTime);
        _b[k] = new Vector4(speed, s.maxRadius * rangeScale, s.fade * fadeScale, s.intensity);
        _c[k] = new Vector4(s.edgeColor.r, s.edgeColor.g, s.edgeColor.b, s.edgeStrength);
        _d[k] = new Vector4(s.interiorColor.r, s.interiorColor.g, s.interiorColor.b, s.interiorStrength);
        _e[k] = new Vector4(s.ringTime, s.falloff, s.edgeSharpness, s.fadeInsideOut ? 1f : 0f);
    }
}
