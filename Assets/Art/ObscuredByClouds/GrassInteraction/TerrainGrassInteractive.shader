// Independent OBC Terrain detail interaction; retains the project's TriForge foliage shading.
// ============================================================================
// TriForge/URP/Tree
// URP 12 vertex/fragment replacement for "TriForge/Top Down/Tree"
// (Assets/TriForge Assets/Top Down - Common Files/Shaders/TreeTopDown.shader).
//
// Features ported 1:1 from the original ASE surface shader:
//   * Wind vertex animation (global TD_WindDirection / TD_WindStrength,
//     main bend simplex noise + detail bend noise, UV2.y bend mask)
//   * Top-down gradient (_USEGRADIENT_ON, UV2/UV3 selection, _TopColor/_TopBrightness)
//   * Color variance (_USECOLORVARIANCE_ON, world XZ / 50 mask)
//   * Mask map: smoothness (A) + AO (G)
//   * Normal map with intensity, alpha clip, Cull Off, DisableBatching
// Property names and keywords are identical to the original shader, so the
// existing .mat files keep their textures / colors / floats.
// ============================================================================
Shader "ObscuredByClouds/Terrain Grass Interactive"
{
    Properties
    {
        _OBCGrassMeshBase("Mesh Base", Float) = 0
        _OBCGrassMeshHeight("Mesh Height", Float) = 1
        [ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1
        _IndirectLightStrength("Indirect Light Strength", Range(0,1)) = 0.45
        _GrassSpecularStrength("Grass Specular Strength", Range(0,1)) = 0
        _Cutoff("Mask Clip Value", Float) = 0.5
        _MainTex("Base Color", 2D) = "white" {}
        _Normal("Normal", 2D) = "bump" {}
        _MaskMap("Mask Map", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.18
        _AOIntensity("AO Intensity", Range(0.0, 1.0)) = 1
        _MainTextureBrightness("Main Texture Brightness", Range(0.0, 2.0)) = 0.5
        [Toggle(_USEGRADIENT_ON)] _UseGradient("UseGradient", Float) = 0
        _DetailBendingScale("Detail Bending Scale", Float) = 5
        _GradientPosition("Gradient Position", Float) = 2.93
        _TopBrightness("Top Brightness", Range(0.0, 10.0)) = 3
        [HDR]_TopColor("Top Color", Color) = (0,0,0,0)
        [HDR]_Color("Color", Color) = (0,0,0,0)
        [KeywordEnum(UV2, UV3)] _GradientUV("Gradient UV", Float) = 0
        _NormalIntensity("Normal Intensity", Float) = 1
        _WindDirectionRandomness("Wind Direction Randomness", Range(0.0, 1.0)) = 0
        _WindMultiplier("Wind Multiplier", Float) = 1
        [Toggle(_USECOLORVARIANCE_ON)] _UseColorVariance("UseColorVariance", Float) = 0
        _ColorVarianceAmount("Color Variance Amount", Range(0.0, 1.0)) = 0.44
        _ColorVariationMask("Color Variation Mask", 2D) = "white" {}
        _DetailBendingIntensity("Detail Bending Intensity", Range(0.0, 1.0)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "DisableBatching" = "True" "IgnoreProjector" = "True" }

        // ------------------------------------------------------------------ Forward
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma shader_feature_local _USEGRADIENT_ON
            #pragma shader_feature_local _USECOLORVARIANCE_ON
            #pragma shader_feature_local _GRADIENTUV_UV2 _GRADIENTUV_UV3

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define _SPECULAR_SETUP 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            #include "Assets/TriForge Assets/Top Down - Fantasy Forest/Shaders/URP/TDFF_Shared.hlsl"
            #include "TerrainGrassBending.hlsl"

            TEXTURE2D(_MainTex);           SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal);            SAMPLER(sampler_Normal);
            TEXTURE2D(_MaskMap);           SAMPLER(sampler_MaskMap);
            TEXTURE2D(_ColorVariationMask); SAMPLER(sampler_ColorVariationMask);

            // globals set by the TriForge wind scripts (unchanged)
            float3 TD_WindDirection;
            float  TD_WindStrength;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Normal_ST;
                float4 _MaskMap_ST;
                half4 _Color;
                half4 _TopColor;
                half _Cutoff;
                half _Smoothness;
                half _GrassSpecularStrength;
                half _IndirectLightStrength;
                half _AOIntensity;
                half _MainTextureBrightness;
                half _DetailBendingScale;
                half _GradientPosition;
                half _TopBrightness;
                half _NormalIntensity;
                half _WindDirectionRandomness;
                half _WindMultiplier;
                half _ColorVarianceAmount;
                half _DetailBendingIntensity;
            CBUFFER_END

            // ------- wind vertex displacement, ported from TreeTopDown vertexDataFunc
            float3 ApplyWind(float3 positionOS, float3 positionWS, float3 pivotWS, float uv2y)
            {
                // fall back to +X if the script has not set a wind direction
                float3 windDir = TD_WindDirection;
                float ifX = (windDir.x == 0.0) ? 1.0 : 0.0;
                float ifZ = (windDir.z == 0.0) ? 1.0 : 0.0;
                float3 dirWS = lerp(windDir, float3(1.0, 0.0, 0.0), ifX * ifZ);
                float3 worldToObjDir = normalize(TransformWorldToObjectDir(dirWS));
                float3 axis = lerp(worldToObjDir, dirWS, _WindDirectionRandomness);

                // main bend: world noise around the object pivot
                float2 pannerMain = (_Time.y * 1.0) * float2(0.12, 0.0) + pivotWS.xz / 50.0;
                float mainNoise = snoise3D(float3(pannerMain, 0.0) * 3.0) * 0.5 + 0.5;
                float mainBend = (-0.4 + (mainNoise - 0.0) * (0.6 - (-0.4)) / (1.0 - 0.0));

                // detail bend: world noise around the vertex
                float2 pannerDetail = (_Time.y * 0.6) * float2(-0.1, 0.1) + positionWS.xz / _DetailBendingScale;
                float detailNoise = snoise3D(float3(pannerDetail, 0.0) * 10.0) * 0.5 + 0.5;

                float angleDeg = (mainBend + (_DetailBendingIntensity * detailNoise)) * 30.0;
                float3 rotated = RotateAroundAxis(float3(0.0, 0.0, 0.0), positionOS, axis, radians(angleDeg));

                return positionOS + (uv2y * (rotated - positionOS)) * (TD_WindStrength * _WindMultiplier);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float2 uv3        : TEXCOORD2;
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
                float2 uv2        : TEXCOORD5;
                float2 uv3        : TEXCOORD6;
                float3 pivotWS    : TEXCOORD7;
                float  fogFactor  : TEXCOORD8;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 pivotWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 displacedOS = ApplyWind(input.positionOS.xyz, positionWS, pivotWS, input.uv2.y);
                displacedOS = OBCBendGrass(displacedOS, input.positionOS.xyz, pivotWS);

                output.uv  = TRANSFORM_TEX(input.uv, _MainTex);
                output.uv2 = input.uv2;
                output.uv3 = input.uv3;
                output.pivotWS = pivotWS;

                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedOS);
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
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(tex.a - _Cutoff);

                // ---------- albedo with top-down gradient (1:1 port)
                half4 temp155 = tex * _Color;
                half4 temp47  = temp155 * _MainTextureBrightness;

                half3 desatInitial = temp47.rgb;
                half desatDot = dot(desatInitial, half3(0.299, 0.587, 0.114));
                half3 desatVar = lerp(desatInitial, desatDot.xxx, 0.25h);

            #if defined(_GRADIENTUV_UV3)
                float gradientY = input.uv3.y;
            #else
                float gradientY = input.uv2.y;
            #endif
                half gradientMask = saturate(pow(gradientY, _GradientPosition));
                half4 gradientCol = half4(desatVar, 0.0h) * (_TopColor * _TopBrightness);
                half4 lerp32 = lerp(temp47, gradientCol, gradientMask);

            #if defined(_USEGRADIENT_ON)
                half4 baseCol = lerp32;
            #else
                half4 baseCol = temp155;
            #endif

                // ---------- color variance (world XZ / 50 mask)
            #if defined(_USECOLORVARIANCE_ON)
                half dot205 = dot(lerp32, half4(0.55h, 0.55h, 0.55h, 0.0h));
                half4 colorVar = (dot205 * 5.0h) * SAMPLE_TEXTURE2D(_ColorVariationMask, sampler_ColorVariationMask, input.pivotWS.xz / 50.0);
                half4 albedo = lerp(baseCol, colorVar, _ColorVarianceAmount);
            #else
                half4 albedo = baseCol;
            #endif

                // ---------- normal / mask map
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, input.uv), _NormalIntensity);
                float3x3 tangentToWorld = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 worldNormal = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                half smoothness = mask.a * _Smoothness * _GrassSpecularStrength;
                half occlusion  = lerp(1.0h, mask.g, _AOIntensity);

                // ---------- lighting (PBR, metallic 0 like the original Standard surface)
                SurfaceData surfaceData;
                surfaceData.albedo     = albedo.rgb;
                surfaceData.specular   = half3(0.04h, 0.04h, 0.04h) * _GrassSpecularStrength;
                surfaceData.metallic   = 0.0h;
                surfaceData.smoothness = smoothness;
                surfaceData.occlusion  = occlusion;
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
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
                inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0,0,0,0);
            #endif
                inputData.fogCoord         = input.fogFactor;
                inputData.bakedGI          = SampleSH(worldNormal) * _IndirectLightStrength;
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                inputData.vertexLighting = VertexLighting(input.positionWS, worldNormal);
            #endif
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
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Assets/TriForge Assets/Top Down - Fantasy Forest/Shaders/URP/TDFF_Shared.hlsl"
            #include "TerrainGrassBending.hlsl"

            float3 _LightDirection;
            float3 TD_WindDirection;
            float  TD_WindStrength;

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Normal_ST;
                float4 _MaskMap_ST;
                half4 _Color;
                half4 _TopColor;
                half _Cutoff;
                half _Smoothness;
                half _GrassSpecularStrength;
                half _IndirectLightStrength;
                half _AOIntensity;
                half _MainTextureBrightness;
                half _DetailBendingScale;
                half _GradientPosition;
                half _TopBrightness;
                half _NormalIntensity;
                half _WindDirectionRandomness;
                half _WindMultiplier;
                half _ColorVarianceAmount;
                half _DetailBendingIntensity;
            CBUFFER_END

            float3 ApplyWind(float3 positionOS, float3 positionWS, float3 pivotWS, float uv2y)
            {
                float3 windDir = TD_WindDirection;
                float ifX = (windDir.x == 0.0) ? 1.0 : 0.0;
                float ifZ = (windDir.z == 0.0) ? 1.0 : 0.0;
                float3 dirWS = lerp(windDir, float3(1.0, 0.0, 0.0), ifX * ifZ);
                float3 worldToObjDir = normalize(TransformWorldToObjectDir(dirWS));
                float3 axis = lerp(worldToObjDir, dirWS, _WindDirectionRandomness);

                float2 pannerMain = (_Time.y * 1.0) * float2(0.12, 0.0) + pivotWS.xz / 50.0;
                float mainNoise = snoise3D(float3(pannerMain, 0.0) * 3.0) * 0.5 + 0.5;
                float mainBend = (-0.4 + (mainNoise - 0.0) * (0.6 - (-0.4)) / (1.0 - 0.0));

                float2 pannerDetail = (_Time.y * 0.6) * float2(-0.1, 0.1) + positionWS.xz / _DetailBendingScale;
                float detailNoise = snoise3D(float3(pannerDetail, 0.0) * 10.0) * 0.5 + 0.5;

                float angleDeg = (mainBend + (_DetailBendingIntensity * detailNoise)) * 30.0;
                float3 rotated = RotateAroundAxis(float3(0.0, 0.0, 0.0), positionOS, axis, radians(angleDeg));

                return positionOS + (uv2y * (rotated - positionOS)) * (TD_WindStrength * _WindMultiplier);
            }

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
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 pivotWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 displacedOS = ApplyWind(input.positionOS.xyz, positionWS, pivotWS, input.uv2.y);
                displacedOS = OBCBendGrass(displacedOS, input.positionOS.xyz, pivotWS);

                float3 displacedWS = TransformObjectToWorld(displacedOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(displacedWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
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
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/TriForge Assets/Top Down - Fantasy Forest/Shaders/URP/TDFF_Shared.hlsl"
            #include "TerrainGrassBending.hlsl"

            float3 TD_WindDirection;
            float  TD_WindStrength;

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Normal_ST;
                float4 _MaskMap_ST;
                half4 _Color;
                half4 _TopColor;
                half _Cutoff;
                half _Smoothness;
                half _GrassSpecularStrength;
                half _IndirectLightStrength;
                half _AOIntensity;
                half _MainTextureBrightness;
                half _DetailBendingScale;
                half _GradientPosition;
                half _TopBrightness;
                half _NormalIntensity;
                half _WindDirectionRandomness;
                half _WindMultiplier;
                half _ColorVarianceAmount;
                half _DetailBendingIntensity;
            CBUFFER_END

            float3 ApplyWind(float3 positionOS, float3 positionWS, float3 pivotWS, float uv2y)
            {
                float3 windDir = TD_WindDirection;
                float ifX = (windDir.x == 0.0) ? 1.0 : 0.0;
                float ifZ = (windDir.z == 0.0) ? 1.0 : 0.0;
                float3 dirWS = lerp(windDir, float3(1.0, 0.0, 0.0), ifX * ifZ);
                float3 worldToObjDir = normalize(TransformWorldToObjectDir(dirWS));
                float3 axis = lerp(worldToObjDir, dirWS, _WindDirectionRandomness);

                float2 pannerMain = (_Time.y * 1.0) * float2(0.12, 0.0) + pivotWS.xz / 50.0;
                float mainNoise = snoise3D(float3(pannerMain, 0.0) * 3.0) * 0.5 + 0.5;
                float mainBend = (-0.4 + (mainNoise - 0.0) * (0.6 - (-0.4)) / (1.0 - 0.0));

                float2 pannerDetail = (_Time.y * 0.6) * float2(-0.1, 0.1) + positionWS.xz / _DetailBendingScale;
                float detailNoise = snoise3D(float3(pannerDetail, 0.0) * 10.0) * 0.5 + 0.5;

                float angleDeg = (mainBend + (_DetailBendingIntensity * detailNoise)) * 30.0;
                float3 rotated = RotateAroundAxis(float3(0.0, 0.0, 0.0), positionOS, axis, radians(angleDeg));

                return positionOS + (uv2y * (rotated - positionOS)) * (TD_WindStrength * _WindMultiplier);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
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

                float3 pivotWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 displacedOS = ApplyWind(input.positionOS.xyz, positionWS, pivotWS, input.uv2.y);
                displacedOS = OBCBendGrass(displacedOS, input.positionOS.xyz, pivotWS);

                output.positionCS = TransformObjectToHClip(displacedOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
