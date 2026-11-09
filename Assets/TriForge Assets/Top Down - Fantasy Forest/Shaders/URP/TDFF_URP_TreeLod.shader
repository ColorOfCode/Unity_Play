// ============================================================================
// TriForge/URP/TreeLod
// URP 12 vertex/fragment replacement for "TriForge/TreeLod"
// (Assets/TriForge Assets/Top Down - Fantasy Forest/Shaders/TreeLod.shader).
// Billboard / LOD tree shader: albedo with a top brightness gradient driven
// by UV2.y (pow 2.93, desaturated * _TopBrightness), alpha clip, Cull Off.
// The original used StandardSpecular with zero specular / smoothness, so a
// simple main-light Lambert + SH ambient is visually equivalent.
// Property names are identical to the original shader.
// ============================================================================
Shader "TriForge/URP/TreeLod"
{
    Properties
    {
        _Cutoff("Mask Clip Value", Float) = 0.5
        _MainTex1("Base Color", 2D) = "white" {}
        _MainTextureBrightness("Main Texture Brightness", Range(0.0, 2.0)) = 0.5
        _TopBrightness("Top Brightness", Range(0.0, 10.0)) = 3
        _Color("Color", Color) = (1,1,1,0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "IgnoreProjector" = "True" }

        // ------------------------------------------------------------------ Forward
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex1); SAMPLER(sampler_MainTex1);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex1_ST;
                half4 _Color;
                half _Cutoff;
                half _MainTextureBrightness;
                half _TopBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float2 uv2        : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.uv  = TRANSFORM_TEX(input.uv, _MainTex1);
                output.uv2 = input.uv2;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex1, sampler_MainTex1, input.uv);
                clip(tex.a - _Cutoff);

                // 1:1 port of the TreeLod gradient
                half4 temp4 = (tex * _Color) * _MainTextureBrightness;
                half3 desatInitial = temp4.rgb;
                half desatDot = dot(desatInitial, half3(0.299, 0.587, 0.114));
                half3 desatVar = lerp(desatInitial, desatDot.xxx, 0.25h);
                half gradientMask = saturate(pow(input.uv2.y, 2.93));
                half4 gradientCol = half4(desatVar * _TopBrightness, 0.0h);
                half3 albedo = lerp(temp4, gradientCol, gradientMask).rgb;

                // simple lighting: main light Lambert + SH ambient
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lambert = saturate(dot(normalWS, mainLight.direction)) * mainLight.color
            #if defined(_MAIN_LIGHT_SHADOWS)
                                * mainLight.shadowAttenuation
            #endif
                                ;
                half3 ambient = SampleSH(normalWS);

                half3 rgb = albedo * (lambert + ambient);
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ ShadowCaster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            TEXTURE2D(_MainTex1); SAMPLER(sampler_MainTex1);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex1_ST;
                half4 _Color;
                half _Cutoff;
                half _MainTextureBrightness;
                half _TopBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif
                output.uv = TRANSFORM_TEX(input.uv, _MainTex1);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex1, sampler_MainTex1, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ DepthOnly
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex1); SAMPLER(sampler_MainTex1);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex1_ST;
                half4 _Color;
                half _Cutoff;
                half _MainTextureBrightness;
                half _TopBrightness;
            CBUFFER_END

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
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex1);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex1, sampler_MainTex1, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
