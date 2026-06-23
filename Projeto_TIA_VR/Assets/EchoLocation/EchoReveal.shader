Shader "Custom/EchoReveal"
{
    // Surface-reveal shader for the echolocation game.
    // Surfaces are (near) black until an expanding sound wave sweeps over them,
    // then they light up showing their real albedo and slowly fade back to dark.
    // Wave data is fed in globally from EchoRevealManager (no per-material setup).
    // Unlit + cheap math -> safe for Quest standalone.
    Properties
    {
        _BaseMap ("Albedo (Base Map)", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "EchoRevealForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Must match EchoRevealManager.MaxPulses
            #define ECHO_MAX_PULSES 16

            // Per-material data (kept in UnityPerMaterial for SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Global echo data, set every frame from C# (NOT in the material CBUFFER).
            // Wave geometry:
            float4 _EchoPulseA[ECHO_MAX_PULSES]; // xyz = origin, w = startTime
            float4 _EchoPulseB[ECHO_MAX_PULSES]; // x = speed (<=0 = constant), y = maxRadius, z = fade, w = intensity
            // Per-wave look (so every source is customised independently):
            float4 _EchoPulseC[ECHO_MAX_PULSES]; // xyz = edge colour,     w = edge strength
            float4 _EchoPulseD[ECHO_MAX_PULSES]; // xyz = interior colour, w = interior strength
            float4 _EchoPulseE[ECHO_MAX_PULSES]; // x = ringTime, y = falloff, z = edgeSharpness, w = fadeInsideOut(0/1)
            int    _EchoPulseCount;
            float  _EchoTime;          // shared clock (Time.time)
            float  _EchoAmbient;       // floor brightness with no sound (0 = pitch black)
            float  _EchoBrightness;    // global master multiplier on the whole reveal
            float  _EchoRangeSoftness; // metres over which interiors fade out near max range (inside-out mode)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO   // required for VR single-pass instanced rendering
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT); // VR: pick the correct eye

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            // Accumulates the coloured reveal at this world point across all active waves,
            // each wave using its OWN colour / strengths / shape.
            half3 EchoReveal(float3 wpos)
            {
                half3 revealColor = 0.0;

                [loop]
                for (int i = 0; i < _EchoPulseCount; i++)
                {
                    float3 origin    = _EchoPulseA[i].xyz;
                    float  startTime = _EchoPulseA[i].w;
                    float  speed     = _EchoPulseB[i].x;
                    float  maxRadius = _EchoPulseB[i].y;
                    float  fade      = max(_EchoPulseB[i].z, 1e-3);
                    float  intensity = _EchoPulseB[i].w;

                    half3  edgeColor     = _EchoPulseC[i].rgb;
                    float  edgeStrength  = _EchoPulseC[i].w;
                    half3  interiorColor = _EchoPulseD[i].rgb;
                    float  interiorStr   = _EchoPulseD[i].w;
                    float  ringTime      = max(_EchoPulseE[i].x, 1e-3);
                    float  falloff       = max(_EchoPulseE[i].y, 0.01);
                    float  edgeSharpness = max(_EchoPulseE[i].z, 0.01);
                    float  fadeInsideOut = _EchoPulseE[i].w;

                    float d   = distance(wpos, origin);
                    float rad = max(maxRadius, 1e-3);

                    // Centre-peaked falloff for the leading edge / range limit.
                    float distFall = pow(saturate(1.0 - d / rad), falloff);
                    // Interior attenuation: ~uniform with a soft outer cutoff in inside-out mode.
                    float softCut = saturate((rad - d) / max(_EchoRangeSoftness, 1e-3));
                    float interiorAtten = lerp(distFall, softCut, step(0.5, fadeInsideOut));

                    float interiorWeight;
                    float edgeTerm = 0.0;

                    if (speed <= 0.0)
                    {
                        // Constant source: steady, full-strength interior glow.
                        interiorWeight = interiorAtten;
                    }
                    else
                    {
                        // Expanding wavefront.
                        float t       = _EchoTime - startTime;
                        float arrival = d / speed;
                        float since   = t - arrival; // <0 not reached yet, >0 already passed

                        // Dim interior lingering behind the wave; inner points fade first (inside-out).
                        interiorWeight = (since >= 0.0 ? saturate(1.0 - since / fade) : 0.0) * interiorAtten * interiorStr;

                        // Bright, sharp outline at the wavefront.
                        float edge = saturate(1.0 - abs(since) / ringTime);
                        edge = pow(edge, edgeSharpness);
                        edgeTerm = edge * distFall;
                    }

                    half3 col = (edgeTerm * edgeStrength) * edgeColor + interiorWeight * interiorColor;
                    col *= intensity;

                    // Per-channel max so overlapping waves don't blow out to white.
                    revealColor = max(revealColor, col);
                }

                return revealColor;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN); // VR: resolve eye index per fragment

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                half3 reveal = EchoReveal(IN.positionWS);

                // Faint neutral ambient on the base albedo, plus the coloured reveal light.
                half3 col = albedo.rgb * (_EchoAmbient + reveal * _EchoBrightness);
                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
