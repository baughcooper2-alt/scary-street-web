// Planar mirror: shows the reflection camera's image (rendered by Mirror.cs from the viewer's reflected point
// of view) at this pixel's screen position. The reflection is rendered horizontally flipped (so its triangles
// keep their winding), which is undone here with 1 - x.
Shader "ScaryStreet/Mirror"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.93, 0.95, 0.97, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Mirror"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ReflectionTex_ST;
                half4 _Tint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                uv.x = 1.0 - uv.x;
                half3 c = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv).rgb;
                return half4(c * _Tint.rgb + half3(0.02, 0.025, 0.03), 1);
            }
            ENDHLSL
        }
    }
}
