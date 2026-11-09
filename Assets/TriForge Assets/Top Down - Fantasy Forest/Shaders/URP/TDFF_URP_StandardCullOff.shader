// ============================================================================
// TriForge/URP/StandardCullOff
// URP 12 vertex/fragment replacement for "TriForge/StardardCullOff"
// (Assets/TriForge Assets/Top Down - Fantasy Forest/Shaders/StandardCullOff.shader).
// PBR lit, Cull Off, alpha clip.
//   albedo    = _BaseColor * _Color
//   normal    = _Normal (packed)
//   metallic  = _MetallicRSmoothnessA.r
//   smoothness= _MetallicRSmoothnessA.a
// Property names are identical to the original shader.
// ============================================================================
Shader "TriForge/URP/StandardCullOff"
{
    Properties
    {
        _Cutoff("Mask Clip Value", Float) = 0.5
        _BaseColor("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,0)
        [Normal] _Normal("Normal Map", 2D) = "bump" {}
        _MetallicRSmoothnessA("Metallic (R), Smoothness (A)", 2D) = "black" {}
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

            TEXTURE2D(_BaseColor);            SAMPLER(sampler_BaseColor);
            TEXTURE2D(_Normal);               SAMPLER(sampler_Normal);
            TEXTURE2D(_MetallicRSmoothnessA); SAMPLER(sampler_MetallicRSmoothnessA);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor_ST;
                float4 _Normal_ST;
                float4 _MetallicRSmoothnessA_ST;
                half4 _Color;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 tangentWS  : TEXCOORD3;
                float3 bitangentWS: TEXCOORD4;
                float  fogFactor  : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.uv = TRANSFORM_TEX(input.uv, _BaseColor);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrmInputs   = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS  = posInputs.positionCS;
                output.positionWS  = posInputs.positionWS;
                output.normalWS    = nrmInputs.normalWS;
                output.tangentWS   = nrmInputs.tangentWS;
                output.bitangentWS = nrmInputs.bitangentWS;
                output.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseColor, sampler_BaseColor, input.uv);
                half4 baseCol = tex * _Color;
                clip(tex.a - _Cutoff);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, input.uv), 1.0h);
                float3x3 tangentToWorld = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 worldNormal = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                half4 mgs = SAMPLE_TEXTURE2D(_MetallicRSmoothnessA, sampler_MetallicRSmoothnessA, input.uv);

                SurfaceData surfaceData;
                surfaceData.albedo     = baseCol.rgb;
                surfaceData.specular   = half3(0.04h, 0.04h, 0.04h);
                surfaceData.metallic   = mgs.r;
                surfaceData.smoothness = mgs.a;
                surfaceData.occlusion  = 1.0h;
                surfaceData.emission   = 0.0h;
                surfaceData.alpha      = 1.0h;
                surfaceData.normalTS   = normalTS;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                InputData inputData = (InputData)0;
                inputData.positionWS       = input.positionWS;
                inputData.positionCS       = input.positionCS;
                inputData.normalWS         = worldNormal;
                inputData.viewDirectionWS  = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord      = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord         = input.fogFactor;
                inputData.bakedGI          = SampleSH(worldNormal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask       = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
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

            TEXTURE2D(_BaseColor); SAMPLER(sampler_BaseColor);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor_ST;
                float4 _Normal_ST;
                float4 _MetallicRSmoothnessA_ST;
                half4 _Color;
                half _Cutoff;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseColor);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseColor, sampler_BaseColor, input.uv).a;
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

            TEXTURE2D(_BaseColor); SAMPLER(sampler_BaseColor);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor_ST;
                float4 _Normal_ST;
                float4 _MetallicRSmoothnessA_ST;
                half4 _Color;
                half _Cutoff;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseColor);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseColor, sampler_BaseColor, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
