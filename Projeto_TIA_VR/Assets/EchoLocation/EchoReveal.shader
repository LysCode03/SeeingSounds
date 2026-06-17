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
            float4 _EchoPulseA[ECHO_MAX_PULSES]; // xyz = origin, w = startTime
            float4 _EchoPulseB[ECHO_MAX_PULSES]; // x = speed (<=0 means constant), y = maxRadius, z = fade, w = intensity
            int    _EchoPulseCount;
            float  _EchoTime;       // shared clock (Time.time)
            float  _EchoAmbient;          // floor brightness with no sound (0 = pitch black)
            float  _EchoRingTime;         // seconds the bright leading edge stays lit
            float4 _EchoEdgeColor;        // colour of the wave's leading-edge outline (light blue)
            float4 _EchoInteriorColor;    // colour of the revealed interior (white = true surface colour)
            float  _EchoBrightness;       // master multiplier on the whole reveal
            float  _EchoEdgeStrength;     // brightness of the wave's leading-edge outline
            float  _EchoEdgeSharpness;    // outline sharpness (higher = thinner, crisper line)
            float  _EchoInteriorStrength; // brightness of the lingering interior glow
            float  _EchoFalloff;          // distance falloff exponent (higher = shorter visible range)
            float  _EchoFadeInsideOut;    // 1 = interior fades centre-first (inside->out); 0 = legacy centre-peaked look
            float  _EchoRangeSoftness;    // metres over which the interior fades out near max range (inside-out mode)

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

            // Returns reveal amounts for this world point: x = leading-edge outline, y = interior glow.
            // Each is the strongest contribution across all active sound waves.
            float2 EchoReveal(float3 wpos)
            {
                float edgeAmt = 0.0;
                float interiorAmt = 0.0;

                [loop]
                for (int i = 0; i < _EchoPulseCount; i++)
                {
                    float3 origin    = _EchoPulseA[i].xyz;
                    float  startTime = _EchoPulseA[i].w;
                    float  speed     = _EchoPulseB[i].x;
                    float  maxRadius = _EchoPulseB[i].y;
                    float  fade      = max(_EchoPulseB[i].z, 1e-3);
                    float  intensity = _EchoPulseB[i].w;

                    float d = distance(wpos, origin);

                    float rad = max(maxRadius, 1e-3);

                    // Centre-peaked falloff: weakens the leading edge with distance and limits range.
                    float distFall = saturate(1.0 - d / rad);
                    distFall = pow(distFall, max(_EchoFalloff, 0.01));

                    // Interior attenuation. In inside-out mode it's ~uniform across the lit area with a
                    // soft cutoff near max range, so the centre-first TIME fade dominates the visual and
                    // the dark hole grows outward. In legacy mode it reuses the centre-peaked falloff.
                    float softCut = saturate((rad - d) / max(_EchoRangeSoftness, 1e-3));
                    float interiorAtten = lerp(distFall, softCut, step(0.5, _EchoFadeInsideOut));

                    if (speed <= 0.0)
                    {
                        // Constant sound: steady interior glow within radius.
                        interiorAmt = max(interiorAmt, interiorAtten * intensity);
                    }
                    else
                    {
                        // Expanding wavefront.
                        float t       = _EchoTime - startTime;   // time since emitted
                        float arrival = d / speed;               // when the wave reaches this point
                        float since   = t - arrival;             // <0 not reached yet, >0 already passed

                        // Dim interior glow lingering behind the wave, fading over 'fade' seconds.
                        // Inner points were swept first, so they reach 0 first -> fade goes inside->out.
                        float interior = (since >= 0.0 ? saturate(1.0 - since / fade) : 0.0) * _EchoInteriorStrength;

                        // Bright, sharp outline right at the wavefront.
                        float edge = saturate(1.0 - abs(since) / max(_EchoRingTime, 1e-3));
                        edge = pow(edge, max(_EchoEdgeSharpness, 0.01)); // sharpen into a tighter band
                        edge *= _EchoEdgeStrength;

                        // Combine with max() so overlapping waves don't blow out to white.
                        interiorAmt = max(interiorAmt, interior * interiorAtten * intensity);
                        edgeAmt     = max(edgeAmt,     edge     * distFall      * intensity);
                    }
                }

                return float2(edgeAmt, interiorAmt);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN); // VR: resolve eye index per fragment

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float2 reveal = EchoReveal(IN.positionWS); // x = edge, y = interior
                half3 revealColor = reveal.x * _EchoEdgeColor.rgb + reveal.y * _EchoInteriorColor.rgb;

                // Faint neutral ambient on the base albedo, plus the coloured reveal light.
                half3 col = albedo.rgb * (_EchoAmbient + revealColor * _EchoBrightness);
                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
