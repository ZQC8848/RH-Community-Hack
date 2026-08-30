// Guide orb - Fresnel rim glow (shared with BeatSphere) plus concentric ripples that spread
// from the point where a hand entered the orb.
//
// See Docs/Guide Orb 跟随引导球规格.md §5 and .ai/decisions/guide-orb-contact-ripple.md
//
// The ripple is a world-space distance field from _ContactPoint, deliberately NOT a UV
// pattern: a hex grid or any other texture-space pattern pinches at the sphere's poles
// (spherical UVs) or shows seams (triplanar), and both cost work purely to hide the defect.
// A distance field has neither problem, and it puts the pattern's origin where the player's
// hand actually is - the orb responds to you rather than merely reacting.
//
// Separate from BeatSphere.shader on purpose: the judgment sphere and the guide orb are
// different objects now, and one shader carrying both sets of dials would let each side's
// needs constrain the other's. See .ai/decisions/guide-orb-not-a-beat-target.md
//
// _Excite (0..1) gates ONLY the ripple. Rim and core respond because C# lerps _RimIntensity
// and _CoreAlpha directly through a MaterialPropertyBlock - keeping that in C# is what lets
// the wrong-hand desaturation be a plain colour lerp instead of a second shader channel.
Shader "RHCommunityHack/GuideOrb"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.12, 0.55, 0.85, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.35, 0.95, 1, 1)

        [Header(Rim)]
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 8)) = 2.5
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 0.3

        [Header(Ripple)]
        _Excite ("Excite", Range(0, 1)) = 0
        _ContactPoint ("Contact Point (world)", Vector) = (0, 0, 0, 0)
        [HDR] _RippleColor ("Ripple Color", Color) = (1, 1, 1, 1)
        _RippleFrequency ("Ripple Frequency (rings per metre)", Range(1, 60)) = 12
        _RippleSpeed ("Ripple Speed", Range(0, 12)) = 2.5
        _RippleWidth ("Ripple Width", Range(0.01, 1)) = 0.15
        _RippleFalloff ("Ripple Falloff (metres)", Range(0.01, 2)) = 0.3

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

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _CoreAlpha;
                float _Excite;
                float4 _ContactPoint;
                float4 _RippleColor;
                float _RippleFrequency;
                float _RippleSpeed;
                float _RippleWidth;
                float _RippleFalloff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _RimPower);

                // Rings travelling outward from the contact point. frac() of a distance that
                // shrinks with time is a band that moves away from the origin; smoothstep turns
                // the sawtooth into a soft-edged ring rather than a hard stripe.
                float contactDistance = distance(input.positionWS, _ContactPoint.xyz);
                float phase = frac(contactDistance * _RippleFrequency - _Time.y * _RippleSpeed);
                float ring = 1.0 - smoothstep(0.0, _RippleWidth, phase);

                // Without this the far side of the orb ripples exactly as hard as the point the
                // hand is touching, which reads as the whole orb flashing rather than as
                // something spreading from the contact.
                float falloff = saturate(1.0 - contactDistance / max(_RippleFalloff, 1e-4));
                float ripple = ring * falloff * saturate(_Excite);

                float3 color = _BaseColor.rgb
                             + _RimColor.rgb * fresnel * _RimIntensity
                             + _RippleColor.rgb * ripple;

                float alpha = saturate(_CoreAlpha + fresnel + ripple * 0.5) * _BaseColor.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
