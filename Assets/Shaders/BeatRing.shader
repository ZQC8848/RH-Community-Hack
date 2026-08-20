// "Neon energy pulse" approach ring - a glowing annulus drawn on a billboard quad.
// See Docs/Ring-Sphere 交互判定与美术规格.md §4-5 and .ai/decisions/ring-art-direction.md
//
// Drawn in the quad's UV space: dist 0 = quad centre, dist 1 = quad edge. BeatTarget scales
// the quad so _RingRadius of its half-width equals the current logical ring radius, and reads
// _RingRadius off the material to do so - so retuning _RingRadius here stays in sync with the
// gameplay radius automatically. Do not hard-code that ratio anywhere else.
//
// Style knobs (_Segments / _SpinSpeed / _Pulse*) exist so different beat types can read as
// genuinely different shapes of thing, not just different hues of the same ring.
Shader "RHCommunityHack/BeatRing"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.35, 0.95, 1, 1)

        [Header(Shape)]
        _RingRadius ("Ring Radius (quad space)", Range(0.1, 1)) = 0.88
        _RingThickness ("Ring Thickness", Range(0.005, 0.5)) = 0.07
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.06

        [Header(Dashes)]
        _Segments ("Segment Count (0 = solid ring)", Range(0, 48)) = 0
        _DashCoverage ("Dash Coverage", Range(0.05, 1)) = 0.55
        _SpinSpeed ("Spin Speed (rad/s)", Range(-8, 8)) = 0

        [Header(Animation)]
        _Intensity ("Intensity", Range(0, 8)) = 2
        _PulseSpeed ("Pulse Speed", Range(0, 30)) = 0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            // Additive, but alpha-scaled so the ring can still be faded out as a whole.
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define RH_TWO_PI 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GlowColor;
                float _RingRadius;
                float _RingThickness;
                float _EdgeSoftness;
                float _Segments;
                float _DashCoverage;
                float _SpinSpeed;
                float _Intensity;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetVertexPositionInputs(input.positionOS.xyz).positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0;

                // Radial band
                float halfThickness = _RingThickness * 0.5;
                float distanceFromBand = abs(dist - _RingRadius);
                float ring = 1.0 - smoothstep(halfThickness, halfThickness + _EdgeSoftness, distanceFromBand);

                // Optional dashes around the circumference, optionally spinning.
                if (_Segments > 0.5)
                {
                    float angle = atan2(centered.y, centered.x) + _Time.y * _SpinSpeed;
                    float segment = frac(angle / RH_TWO_PI * _Segments);
                    float fromDashCentre = abs(segment - 0.5) * 2.0;
                    float dash = 1.0 - smoothstep(_DashCoverage, _DashCoverage + 0.15, fromDashCentre);
                    ring *= dash;
                }

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float3 color = _GlowColor.rgb * _BaseColor.rgb * _Intensity * pulse * ring;
                float alpha = ring * _BaseColor.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
