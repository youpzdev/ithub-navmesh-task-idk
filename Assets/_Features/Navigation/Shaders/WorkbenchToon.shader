Shader "Workbench/Toon"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (0.8,0.85,0.9,1)
        _Outline ("Outline", Range(0,0.05)) = 0.018
        _HitFlash ("Hit flash", Range(0,1)) = 0
        _GlowStrength ("Glow strength", Range(0,4)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Outline;
            float _HitFlash;
            float _GlowStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half n = saturate(dot(normalize(input.normalWS), light.direction));
                half band = n > 0.65 ? 1.0 : n > 0.2 ? 0.77 : 0.53;
                band *= lerp(0.66, 1, light.shadowAttenuation);
                return half4(lerp(_BaseColor.rgb * (band + _GlowStrength), half3(1,1,1), saturate(_HitFlash)), _BaseColor.a);
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Outline;
            float _HitFlash;
            float _GlowStrength;
            CBUFFER_END
            float4 vert(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            { return TransformObjectToHClip(positionOS.xyz + normalOS * _Outline); }
            half4 frag() : SV_Target { return half4(0.055,0.075,0.095,1); }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
