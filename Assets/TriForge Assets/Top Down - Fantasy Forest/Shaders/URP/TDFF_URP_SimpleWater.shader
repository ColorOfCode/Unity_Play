// ============================================================================
// TriForge/URP/SimpleWater
// URP 12 vertex/fragment replacement for "TriForge/SimpleWater"
// (Assets/TriForge Assets/Top Down - Fantasy Forest/Shaders/Water.shader).
//
// Ported features:
//   * Two octaves of 4-way panned normal maps (world XZ planar mapping)
//   * Depth-based color blend (shallow -> deep, needs URP Depth Texture)
//   * Depth-based opacity + edge fade
//   * Foam (two panned foam textures + depth mask)
//   * Screen-space refraction: the Built-in GrabPass was replaced by
//     _CameraOpaqueTexture (enable "Opaque Texture" on the URP asset)
//   * Blinn-Phong sun specular (_WaterSpecularity / _WaterSmoothness)
// Property names are identical to the original shader.
// ============================================================================
Shader "TriForge/URP/SimpleWater"
{
    Properties
    {
        _IndirectLightStrength("Indirect Light Strength", Range(0,2)) = 1
        [Normal]_NormalMap("Normal Map", 2D) = "bump" {}
        _NormalMapScale("Normal Map Scale", Range(0.0, 10.0)) = 1
        _NormalIntensity("Normal Intensity", Range(0.0, 5.0)) = 0
        _WaterColorBlend("Water Color Blend", Float) = 0
        _WaterDeepColor("Water Deep Color", Color) = (0.1320755, 0.1320755, 0.1320755, 0)
        _WaterShallowColor("Water Shallow Color", Color) = (0.1320755, 0.1320755, 0.1320755, 0)
        _MainOpacity("Main Opacity", Range(0.0, 1.0)) = 0.2
        _WaterOpacityBlend("Water Opacity Blend", Float) = 0
        _WaterSmoothness("Water Smoothness", Range(0.0, 1.0)) = 0.95
        _WaterSpecularity("Water Specularity", Range(0.0, 1.0)) = 1
        _FoamColor("Foam Color", Color) = (0.9150943, 0.9150943, 0.9150943, 0)
        _FoamAmount("Foam Amount", Range(0.0, 2.0)) = 0
        _FoamContrast("Foam Contrast", Range(0.0, 5.0)) = 1.5
        _FoamTexture("Foam Texture", 2D) = "white" {}
        _FoamScale("Foam Scale", Float) = 3
        _RefractionIntensity("Refraction Intensity", Range(0.0, 5.0)) = 1
        [HideInInspector][ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [HideInInspector][ToggleOff] _GlossyReflections("Reflections", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardTransparent"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // kept valid so existing material keywords survive the swap
            #pragma shader_feature_local_fragment _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ _GLOSSYREFLECTIONS_OFF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "TDFF_Shared.hlsl"

            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamTexture); SAMPLER(sampler_FoamTexture);

            CBUFFER_START(UnityPerMaterial)
                half _IndirectLightStrength;
                half _NormalMapScale;
                half _NormalIntensity;
                half4 _WaterDeepColor;
                half4 _WaterShallowColor;
                half _WaterColorBlend;
                half _RefractionIntensity;
                half _MainOpacity;
                half _WaterOpacityBlend;
                half _FoamAmount;
                half _FoamContrast;
                half _FoamScale;
                half4 _FoamColor;
                half _WaterSpecularity;
                half _WaterSmoothness;
                half _SpecularHighlights;
                half _GlossyReflections;
            CBUFFER_END
            #include "Assets/Art/ObscuredByClouds/Lighting/OBCIndirectControl.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;   // NDC: xy / w = screen UV, zw = clip zw
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.screenPos  = posInputs.positionNDC;
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            // 4-way panned normal sum, ported from the ASE "4WayChaos" function
            float3 SampleNormalOctave(float2 baseUV, float timeScale, float t)
            {
                float2 p5  = (t * timeScale) * float2( 0.1,  0.1) + baseUV;
                float2 p16 = (t * timeScale) * float2(-0.1, -0.1) + (baseUV + float2(0.418, 0.355));
                float2 p19 = (t * timeScale) * float2(-0.1,  0.1) + (baseUV + float2(0.865, 0.148));
                float2 p23 = (t * timeScale) * float2( 0.1, -0.1) + (baseUV + float2(0.651, 0.752));
                float3 n5  = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p5 ), _NormalIntensity);
                float3 n16 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p16), _NormalIntensity);
                float3 n19 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p19), _NormalIntensity);
                float3 n23 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p23), _NormalIntensity);
                return ((n5 + n16) + (n19 + n23)) * 1.0;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float3 worldPos = input.positionWS;

                // ---------------- water normal (two octaves, world planar)
                float2 baseUV1 = (t * 1.0) * float2(0.02, 0.1) + worldPos.xz / (3.0 * _NormalMapScale);
                float2 baseUV2 = (t * 1.0) * float2(0.1, 0.06) + worldPos.xz / (8.0 * _NormalMapScale);
                float3 n1 = SampleNormalOctave(baseUV1, 1.0, t);
                float3 n2 = SampleNormalOctave(baseUV2, 0.6, t);
                float3 waterNormal = BlendNormalsTDF(n1, n2);

                // ---------------- screen / depth setup
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float  fragEyeZ = LinearEyeDepth(input.screenPos.z / input.screenPos.w, _ZBufferParams);
                float  sceneEyeZ = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);

                // ---------------- color blend by depth
                float distanceDepth154 = saturate(abs((sceneEyeZ - fragEyeZ) / _WaterColorBlend));
                half4 waterColor = lerp(_WaterShallowColor, _WaterDeepColor, distanceDepth154);

                // ---------------- opacity by depth
                float distanceDepth161 = saturate(abs((sceneEyeZ - fragEyeZ) / _WaterOpacityBlend));

                // ---------------- foam
                float distanceDepth182 = saturate(abs((sceneEyeZ - fragEyeZ) / _FoamAmount));
                float saferPower188 = abs(1.0 - distanceDepth182);
                float2 panner197 = (t * 1.0) * float2(0.2, 0.0) + worldPos.xz / _FoamScale;
                float2 panner194 = (t * 1.0) * float2(0.3, 0.0) + worldPos.xz / _FoamScale;
                float foam = saturate(saturate(pow(saferPower188, _FoamContrast))
                            - SAMPLE_TEXTURE2D(_FoamTexture, sampler_FoamTexture, panner197).r
                            - saturate(pow(SAMPLE_TEXTURE2D(_FoamTexture, sampler_FoamTexture, panner194).g * 0.88, 0.65)));

                // ---------------- refraction (URP opaque texture replaces GrabPass)
                float opacity = lerp(_MainOpacity * distanceDepth161, 1.0, foam);
                // original: (screenPos.xy + normal * _RefractionIntensity) / screenPos.w
                float2 refrUV = screenUV + waterNormal.xy * _RefractionIntensity / max(input.screenPos.w, 1e-4);
                float3 screenColor = SampleSceneColor(refrUV);
                half4 refraction = half4(screenColor, 1.0h) * 0.5h * (1.0h - opacity);

                // ---------------- final albedo
                half4 albedo = lerp(waterColor + refraction, _FoamColor, foam);

                // ---------------- specular (Blinn-Phong from the main light)
                half specMask = _WaterSpecularity * lerp(1.0h, 0.45h, foam);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float specPower = exp2(_WaterSmoothness * 6.0 + 2.0);
                half3 spec = mainLight.color * specMask * pow(saturate(dot(waterNormal, halfDir)), specPower);

                // ---------------- alpha (water edge fade)
                float distanceDepth256 = saturate(abs((sceneEyeZ - fragEyeZ) / 0.11));
                half alpha = saturate(pow(abs(distanceDepth256), 1.5));

                half3 surfaceTint = lerp(waterColor.rgb, _FoamColor.rgb, foam);
                half3 rgb = surfaceTint * OBCIndirectStrength(_IndirectLightStrength) + refraction.rgb * (1.0h - foam) + spec;
                // water ambient tint
                rgb += surfaceTint * SampleSH(waterNormal) * 0.1h * OBCIndirectStrength(_IndirectLightStrength);

                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "ObscuredByClouds.EditorTools.IndirectLightingShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
