// Dissolves a surface away, driven by one 0..1 number, so a stage's dome can open up and
// disappear when the player is done with it.
//
// Used on BOTH the inverted sphere and the disc floor inside it, which is why the cull mode is a
// property rather than baked in: the dome is Cull Back (its faces point inward - see
// InvertedSphereGenerator) and the floor is Cull Off. One shader, two materials.
//
// The pattern is procedural noise in OBJECT space, not a texture. Object space because both
// meshes are unit-sized, so one noise scale reads the same on an 8m dome and a 3m floor and does
// not shift when the object moves. No texture because a dissolve map on an equirectangular sphere
// pinches at the poles, which is exactly where a dome is most visible from inside.
//
// It samples _BaseMap even though the domes are currently flat colour. The sphere's UVs are
// equirectangular precisely so it can carry 360 video later; wiring that in should not mean
// rewriting this.
//
// Alpha-tested rather than transparent: the dome is a room, and it has to keep writing depth and
// occluding what is behind it right up until it is gone.
Shader "RH Community Hack/Dome Dissolve"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)

        // 0 = solid, 1 = completely gone. Driven from DancePlace.SetDissolve through a
        // MaterialPropertyBlock, so all six domes share this one material.
        _Dissolve ("Dissolve", Range(0, 1)) = 0

        _NoiseScale ("Noise Scale", Range(1, 40)) = 8
        // Blends the noise toward a top-down sweep. 0 is pure noise, which is what a flat floor
        // wants; higher values make a dome open at the roof first and sink to the horizon.
        _Sweep ("Top-down Sweep", Range(0, 1)) = 0.55

        _EdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.08
        [HDR] _EdgeColor ("Edge Colour", Color) = (1, 0.55, 0.2, 1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Render Face", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Unlit"

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Required for VR single-pass instanced, or this draws into one eye only.
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EdgeColor;
                half   _Dissolve;
                half   _NoiseScale;
                half   _Sweep;
                half   _EdgeWidth;
                half   _Cull;
            CBUFFER_END

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise: hash the eight lattice corners and blend with a smoothstep. Cheap, and
            // good enough for an edge that is going to be lit up anyway.
            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash13(i + float3(0, 0, 0));
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float noise = ValueNoise(IN.positionOS * _NoiseScale);

                // Both meshes are unit-sized, so y is -1..1 and this maps to 0 at the bottom and
                // 1 at the top. Inverted, so the LOW values - which go first - are at the top.
                float height = saturate(IN.positionOS.y * 0.5 + 0.5);
                float field = lerp(noise, 1.0 - height, _Sweep);

                // Overshooting by the edge width is what guarantees the surface is completely
                // gone at _Dissolve = 1 rather than leaving the brightest speckles behind.
                float threshold = _Dissolve * (1.0 + _EdgeWidth);
                float d = field - threshold;
                clip(d);

                half4 src = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 rgb = src.rgb * _BaseColor.rgb;

                // Burn along the surviving edge. Suppressed entirely at rest, or every dome would
                // wear a permanent glowing rim before anything has started.
                half edge = saturate(1.0 - d / _EdgeWidth) * step(0.0001h, _Dissolve);
                rgb = lerp(rgb, _EdgeColor.rgb, edge);

                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
