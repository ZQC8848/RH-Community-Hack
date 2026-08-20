// "Neon energy pulse" beat sphere - Fresnel rim glow + emissive core.
// See Docs/Ring-Sphere 交互判定与美术规格.md §5 and .ai/decisions/ring-art-direction.md
//
// Transparent on purpose: BeatTarget's Miss-Touch vanish animation fades _BaseColor.a to 0
// (via MaterialPropertyBlock), which only reads as a fade if this shader honours that alpha -
// an opaque shader would just pop out of existence instead.
//
// _RimPower / _RimIntensity / _CoreAlpha are the main style dials: a low core alpha with a
// tight bright rim reads as a hollow bubble, a high core alpha with a wide soft rim reads as
// a solid glowing orb. Different beat types are expected to differ here, not only in hue.
Shader "RHCommunityHack/BeatSphere"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.12, 0.55, 0.85, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.35, 0.95, 1, 1)

        [Header(Rim)]
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 8)) = 2.5
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 0.3

        [Header(Animation)]
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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _CoreAlpha;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _RimPower);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float3 color = _BaseColor.rgb + _RimColor.rgb * fresnel * _RimIntensity * pulse;
                float alpha = saturate(_CoreAlpha + fresnel) * _BaseColor.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
