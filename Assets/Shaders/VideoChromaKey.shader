// Keys a green screen out of a video at draw time, so green footage can be dropped straight in
// without a pre-processing step.
//
// The keying is done on the CbCr (chroma) plane, NOT on RGB distance. Chroma distance ignores
// brightness, so a shadowed fold of the green backdrop and a brightly lit part of it land in the
// same place and key out together - an RGB distance treats them as different colours and leaves
// the dark parts behind.
//
// KNOWN LIMIT, and it is a property of the footage rather than of this shader: video is 4:2:0, so
// the chroma plane this works on is half resolution. Hair, fingers and motion blur are keyed from
// smeared colour data. If the edges are not good enough, key the footage BEFORE compression and
// ship alpha instead - see the chroma-key note in the PlayScene spec.
Shader "RH Community Hack/Video Chroma Key"
{
    Properties
    {
        [MainTexture] _BaseMap ("Video", 2D) = "black" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)

        _KeyColor ("Key Colour", Color) = (0, 0.8, 0.1, 1)
        // Below this chroma distance a pixel is fully keyed out.
        _Threshold ("Key Threshold", Range(0, 0.5)) = 0.12
        // The band above the threshold over which alpha ramps 0 -> 1. Widen it for soft edges,
        // narrow it for a hard cut.
        _Smoothness ("Edge Softness", Range(0.001, 0.3)) = 0.06
        // Pulls green back out of the pixels that survive. Without it, everything the subject
        // owns keeps a green rim picked up from the backdrop.
        _SpillRemoval ("Spill Removal", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Unlit"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Both sides: the quad is a free-standing cutout, and a stage the player walks behind
            // should not turn into nothing.
            Cull Off

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _KeyColor;
                half   _Threshold;
                half   _Smoothness;
                half   _SpillRemoval;
            CBUFFER_END

            // Rec.601 chroma. Only Cb/Cr are needed - dropping Y is the whole point.
            half2 ChromaOf(half3 c)
            {
                half cb = -0.168736h * c.r - 0.331264h * c.g + 0.500000h * c.b;
                half cr =  0.500000h * c.r - 0.418688h * c.g - 0.081312h * c.b;
                return half2(cb, cr);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 src = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                half distance = length(ChromaOf(src.rgb) - ChromaOf(_KeyColor.rgb));
                half keyAlpha = smoothstep(_Threshold, _Threshold + _Smoothness, distance);

                // Despill: anything greener than the average of its own red and blue is carrying
                // bounce from the backdrop. Pull it back toward that average.
                half3 rgb = src.rgb;
                half neutral = (rgb.r + rgb.b) * 0.5h;
                rgb.g = lerp(rgb.g, min(rgb.g, neutral), _SpillRemoval);

                // Multiplied by the texture's own alpha so an alpha PNG poster still works on the
                // same material - a poster with no green simply passes through untouched.
                half alpha = keyAlpha * src.a * _BaseColor.a;
                return half4(rgb * _BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
