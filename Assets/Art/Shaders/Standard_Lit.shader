// Targets Unity 2021.3 / Universal RP 12.1.x.
// Standalone shader: no custom HLSL dependency. Opaque and alpha-tested surfaces.
Shader "URP/Standard_Lit"
{
    Properties
    {
        [Header(Surface)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Cutoff", Range(0,1)) = 0.5
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1

        [Header(Material)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseColorIntensity("Base Color Intensity (RGB only)", Float) = 1
        [NoScaleOffset] _MetallicGlossMap("ORM Mask (R AO, G Roughness, B Metal, A Emission)", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _Roughness("Roughness Scale", Range(0,2)) = 1
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0,2)) = 1
        _Occlusion("Occlusion Strength", Range(0,1)) = 1

        [Header(Emission)]
        [Toggle(_EMISSION)] _EMISSION("Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionIntensity("Emission Intensity", Float) = 1

        [Header(Indirect Lighting)]
        _SHScale("Indirect Diffuse Scale (Lightmap and Probes)", Float) = 1
        _IBLScale("Indirect Specular Scale", Float) = 1
        _EnvRotation("Environment Rotation (Unboxed Probes Only)", Range(0,360)) = 0
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("Environment Reflections", Float) = 1

        [Header(Fog)]
        [Toggle(_ENABLEFOG)] _Enablefog("Legacy Custom Distance Fog", Float) = 0
        _FogColor7("Custom Fog Color", Color) = (1,1,1,1)
        _FogParameter7("Legacy Fog (X Distance Scale, Y Bias, ZW Unused)", Vector) = (0.1,0.1,0.1,0.1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        // Normal mapping is always available, including after changing shaders.
        #define _NORMALMAP 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #if defined(_CLUSTERED_RENDERING)
            // URP 12's cookie atlas uses implicit derivatives. Those cannot be used
            // inside a per-pixel clustered while-loop on D3D11. Isolate that sampler
            // and use explicit LOD 0 for the atlas; other texture samplers are unchanged.
            #define SampleAdditionalLightsCookieAtlasTexture StandardUnusedImplicitCookieAtlas
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LightCookie/LightCookieInput.hlsl"
            #undef SampleAdditionalLightsCookieAtlasTexture
            real4 SampleAdditionalLightsCookieAtlasTexture(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_AdditionalLightsCookieAtlasTexture,
                    sampler_AdditionalLightsCookieAtlasTexture, uv, 0);
            }
        #endif
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        // Identical layout in every pass: compatible with the SRP Batcher.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            float4 _FogColor7;
            float4 _FogParameter7;
            float _BaseColorIntensity;
            float _EmissionIntensity;
            float _SHScale;
            float _IBLScale;
            float _EnvRotation;
            half _Metallic;
            half _Roughness;
            half _BumpScale;
            half _Occlusion;
            half _Cutoff;
            half _Cull;
            half _AlphaClip;
            half _ReceiveShadows;
            // _EMISSION is a keyword and cannot also be an HLSL variable name.
            half _Enablefog;
            half _SpecularHighlights;
            half _EnvironmentReflections;
        CBUFFER_END
        TEXTURE2D(_MetallicGlossMap);
        SAMPLER(sampler_MetallicGlossMap);

        // This same initializer is consumed by the forward and official Lit Meta passes.
        void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
        {
            surface = (SurfaceData)0;
            half4 baseSample = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
            surface.alpha = Alpha(baseSample.a, _BaseColor, _Cutoff);
            surface.albedo = max(baseSample.rgb * _BaseColor.rgb * max(_BaseColorIntensity, 0.0), 0.0);
            half4 mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
            // ORM: R = AO, G = perceptual roughness, B = metallic.
            surface.metallic = saturate(mask.b * _Metallic);
            surface.smoothness = 1.0h - saturate(mask.g * _Roughness);
            surface.occlusion = lerp(1.0h, saturate(mask.r), saturate(_Occlusion));
            surface.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
            #if defined(_EMISSION)
                // Optional A-channel emission mask, still tinted by the base color.
                // RGB-only ORM maps must leave Emission disabled when not emissive.
                surface.emission = mask.a * max(_EmissionColor.rgb, 0.0h)
                    * max(_EmissionIntensity, 0.0) * surface.albedo;
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            // Both shaders deliberately retain their own lighting in Deferred renderers.
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One Zero
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StandardVertex
            #pragma fragment StandardFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ENABLEFOG
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            // Reuse URP 12 vertex transforms, tangent frame, GI UVs and shadowmask input.
            // Its unused stock fragment is renamed; our actual fragment follows below.
            #define _Surface 0.0h
            #define LitPassFragment StandardUnusedStockFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef LitPassFragment
            #undef _Surface

            half3 StandardDirect(BRDFData brdf, Light light, half3 normalWS, half3 viewWS)
            {
                // Stock URP 12 Lit direct-light BRDF.
                return LightingPhysicallyBased(brdf, light, normalWS, viewWS);
            }

            half3 StandardGI(BRDFData brdf, InputData data, half occlusion)
            {
                half3 reflection = reflect(-data.viewDirectionWS, data.normalWS);
                // A room's box projection is geometric. Do not rotate it out of alignment.
                #if !defined(_REFLECTION_PROBE_BOX_PROJECTION)
                    float sineAngle, cosineAngle;
                    sincos(radians(_EnvRotation), sineAngle, cosineAngle);
                    reflection.xz = float2(reflection.x * cosineAngle - reflection.z * sineAngle,
                                           reflection.x * sineAngle + reflection.z * cosineAngle);
                #endif
                half3 indirectSpecular = GlossyEnvironmentReflection(reflection, data.positionWS,
                    brdf.perceptualRoughness, 1.0h);
                half NoV = saturate(dot(data.normalWS, data.viewDirectionWS));
                half fresnel = Pow4(1.0h - NoV);
                // URP's standard environment BRDF; AO is applied exactly once.
                half3 gi = EnvironmentBRDF(brdf, data.bakedGI * max(_SHScale, 0.0),
                    indirectSpecular * max(_IBLScale, 0.0), fresnel);
                if (IsOnlyAOLightingFeatureEnabled())
                    gi = half3(1,1,1);
                return gi * occlusion;
            }

            // URP 12 UniversalFragmentPBR flow with isolated direct BRDF and GI art controls.
            half4 StandardPBR(InputData data, SurfaceData surface)
            {
                BRDFData brdf;
                InitializeBRDFData(surface, brdf);
                #if defined(DEBUG_DISPLAY)
                    half4 debugColor;
                    if (CanDebugOverrideOutputColor(data, surface, brdf, debugColor))
                        return debugColor;
                #endif
                half4 shadowMask = CalculateShadowMask(data);
                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(data, surface);
                uint layers = GetMeshRenderingLightLayer();
                Light mainLight = GetMainLight(data, shadowMask, ao);
                MixRealtimeAndBakedGI(mainLight, data.normalWS, data.bakedGI);
                LightingData lighting = CreateLightingData(data, surface);
                lighting.giColor = StandardGI(brdf, data, ao.indirectAmbientOcclusion);
                if (IsMatchingLightLayer(mainLight.layerMask, layers))
                    lighting.mainLightColor = StandardDirect(brdf, mainLight, data.normalWS, data.viewDirectionWS);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    #if USE_CLUSTERED_LIGHTING
                        for (uint lightIndex = 0; lightIndex < min(_AdditionalLightsDirectionalCount, MAX_VISIBLE_LIGHTS); ++lightIndex)
                        {
                            Light light = GetAdditionalLight(lightIndex, data, shadowMask, ao);
                            if (IsMatchingLightLayer(light.layerMask, layers))
                                lighting.additionalLightsColor += StandardDirect(brdf, light, data.normalWS, data.viewDirectionWS);
                        }
                    #endif
                    // URP 12's clustered loop macro requires this exact variable name.
                    InputData inputData = data;
                    LIGHT_LOOP_BEGIN(count)
                        Light light = GetAdditionalLight(lightIndex, data, shadowMask, ao);
                        if (IsMatchingLightLayer(light.layerMask, layers))
                            lighting.additionalLightsColor += StandardDirect(brdf, light, data.normalWS, data.viewDirectionWS);
                    LIGHT_LOOP_END
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    // Standard URP per-vertex fallback: diffuse only, no additional shadows.
                    lighting.vertexLightingColor = data.vertexLighting * brdf.diffuse;
                #endif
                return CalculateFinalColor(lighting, 1.0h);
            }

            Varyings StandardVertex(Attributes input)
            {
                Varyings output = LitPassVertex(input);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    // Add light-layer filtering to the standard per-vertex fallback.
                    UNITY_SETUP_INSTANCE_ID(input);
                    half3 vertexLighting = 0;
                    uint layers = GetMeshRenderingLightLayer();
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light light = GetAdditionalLight(lightIndex, output.positionWS);
                        if (IsMatchingLightLayer(light.layerMask, layers))
                            vertexLighting += LightingLambert(light.color * light.distanceAttenuation,
                                light.direction, output.normalWS);
                    LIGHT_LOOP_END
                    output.fogFactorAndVertexLight.yzw = vertexLighting;
                #endif
                return output;
            }

            half4 StandardFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv, surface);
                // Flip the complete shading normal on the back of a two-sided thin surface.
                surface.normalTS *= IS_FRONT_VFACE(face, 1.0h, -1.0h);
                InputData data;
                InitializeInputData(input, surface.normalTS, data);
                SETUP_DEBUG_TEXTURE_DATA(data, input.uv, _BaseMap);
                #ifdef _DBUFFER
                    ApplyDecalToSurfaceData(input.positionCS, surface, data);
                #endif
                half4 color = StandardPBR(data, surface);
                #if defined(_ENABLEFOG)
                    // Preserve the original distance-fog convention; Z/W remain unused.
                    float clipZ = TransformWorldToHClip(data.positionWS).z;
                    float visibility = saturate(UNITY_Z_0_FAR_FROM_CLIPSPACE(clipZ)
                        * _FogParameter7.x + _FogParameter7.y);
                    color.rgb = lerp(_FogColor7.rgb, color.rgb, visibility);
                #else
                    color.rgb = MixFog(color.rgb, data.fogCoord);
                #endif
                // Opaque output; coverage was handled by the shared Alpha() test above.
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex StandardDepthNormalsVertex
            #pragma fragment StandardDepthNormalsFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            Varyings StandardDepthNormalsVertex(Attributes input)
            {
                Varyings output = DepthNormalsVertex(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                return output;
            }

            half4 StandardDepthNormalsFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                half3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                normalTS *= IS_FRONT_VFACE(face, 1.0h, -1.0h);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normalWS);
                    half3 packed = PackFloat2To888(saturate(oct * 0.5 + 0.5));
                    return half4(packed, 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "StandardURPShaderGUI"
}