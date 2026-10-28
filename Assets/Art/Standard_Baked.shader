Shader "URP/Standard_Baked"
{
    Properties
    {
        [Header(Option)][Space(10)]
        //[Toggle(_SURFACE_TYPE_TRANSPARENT)] _TRANSPARENT_ON("TRANSPARENT ON",Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("SrcBlend", float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("DstBlend", float) = 0.0
        [Enum(Off, 0, On, 1)]_ZWrite ("ZWrite", float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)]_Cull ("Cull Mode", float) = 2.0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping",float) = 0.0
        _Cutoff("Cutoff", Range(0.0, 1.0)) = 0.5

        [Space(20)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseColorIntensity("BaseColorIntensity",Float) = 1
        _MetallicGlossMap("Mask(R=Metal,G=AO,A=Smoothness)",2D) = "white"{}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map",2D) = "bump"{}
        _BumpScale("Normal Scale",float) = 1.0
        _Occlusion("Occlusion",Range(0.0,1.0)) = 1.0

        [Space(20)]
        [Toggle(_EMISSION)] _EMISSION("Emission",float) = 0.0
        //_EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        _EmissionIntensity("EmissionIntensity", Float) = 1
        
        _EnvRotation("EnvRotation",Range(0.0,360.0)) = 0.0
        _IBLScale("IBLScale",Float) = 1
        _SHScale("SHScale",Float) = 1

        [Space(20)]
        /*[Toggle(_DIFFUSE_OFF)] _DIFFUSE_OFF("DIFFUSE OFF",Float) = 0.0
        [Toggle(_SPECULAR_OFF)] _SPECULAR_OFF("SPECULAR OFF",Float) = 0.0
        [Toggle(_SH_OFF)] _SH_OFF("SH OFF",Float) = 0.0
        [Toggle(_IBL_OFF)] _IBL_OFF("IBL OFF",Float) = 0.0
        [Toggle(_DIR_OFF)] _DIR_OFF("DIR OFF",Float) = 0.0
        [Toggle(_DIRHIGHLIGHT_OFF)] _DIRHIGHLIGHT_OFF("DIRHIGHLIGHT OFF",Float) = 0.0*/
        
        [Toggle(_ENABLEFOG)] _Enablefog("Enablefog?", Float) = 1
        _FogColor7 ("Fog Color", Color) = (1,1,1,1) 
    	_FogParameter7("FogParameter",Vector) = (0.1,0.1,0.1,0.1)
    }

    SubShader
    {
    	Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True"}
        /*Tags{"RenderPipeline" = "UniversalPipeline" 
        	"RenderType" = "TransparentCutout"
        	"Queue" = "AlphaTest"
	        "IgnoreProjector" = "True"
        	"DisableBatching" = "True"
        	}*/

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            Tags{"LightMode" = "UniversalForward"}
            
            Blend[_SrcBlend][_DstBlend]  //Blend半透明模式时写法，非半透明模式时可不，非半透明时，外面要调成 one and zero
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Material Keywords
            //#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION

            //#pragma shader_feature_local_fragment _DIFFUSE_OFF
            //#pragma shader_feature_local_fragment _SPECULAR_OFF
            //#pragma shader_feature_local_fragment _SH_OFF
            //#pragma shader_feature_local_fragment _IBL_OFF
            //#pragma shader_feature_local_fragment _DIR_OFF
            //#pragma shader_feature_local_fragment _DIRHIGHLIGHT_OFF
            
            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            //#pragma multi_compile_fog

            //自定义高度雾
            #pragma multi_compile_fragment _ _ENABLEFOG

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;    // xyz: tangent, w: sign
                float4 shadowCoord : TEXCOORD4;
                float2 staticLightmapUV : TEXCOORD5;
                float4 positionCS : SV_POSITION;
                float4 fogFactor_zy : TEXCOORD6; // x: fogFactor, yzw: vertex light

            	// 用于识别正面/背面
                float facing  : VARYING_FACE_TYPE;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicGlossMap);    SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _Metallic, _BaseColorIntensity, _EnvRotation, _IBLScale, _SHScale , _EmissionIntensity;
            half _Smoothness;
            half _Occlusion;
            half _BumpScale;
            half _Cutoff;
            float4 _EmissionColor;
            //自定义Fog
	        uniform half4 _FogParameter7; //(x:FogEnd,y:FogStart,z:FogBottom,w:FogTop)
			uniform half4 _FogColor7;
            CBUFFER_END

            //自定义Fog
			/*float Transfer_Fog(float z, float3 posWorld)   //yuanban
			{
			    float clipZ_01 = UNITY_Z_0_FAR_FROM_CLIPSPACE(z);
			    float fogDis  = saturate(clipZ_01 * _FogParameter7.x + _FogParameter7.y);
			    float fogHeight = clamp((_FogParameter7.z - posWorld.y) / (_FogParameter7.z - _FogParameter7.w), 0.0, 0.99);
			    return  max(fogHeight, fogDis);
            	
			}*/
            float Transfer_Fog(float z, float3 posWorld)
			{
			    float clipZ_01 = UNITY_Z_0_FAR_FROM_CLIPSPACE(z);
			    float fogDis  = saturate(clipZ_01 * _FogParameter7.x + _FogParameter7.y);
			    float fogHeight = clamp((_FogParameter7.z - posWorld.y) / (_FogParameter7.z - _FogParameter7.w), 0.0, 0.99);
			    //return  max(fogHeight, fogDis);
            	return  fogDis;
            	
			}
            
			half3 Mix_Fog(half3 col, float fogCoord)
			{
			    //return lerp( col.rgb,_FogColor7.rgb,saturate((1 - fogCoord) * _FogColor7.a));
            	return lerp(_FogColor7, col, saturate((fogCoord) ));
			     
			}
            
            //PBR相关函数
            inline half Pow5 (half x)
            {
                return x*x * x*x * x;
            }
            half3 Diffuse_Lambert( half3 DiffuseColor )
            {
	            return DiffuseColor * (1 / PI);
            }
            float D_GGX_UE4( float a2, float NoH )
            {
	            float d = ( NoH * a2 - NoH ) * NoH + 1;	// 2 mad
	            return a2 / ( PI*d*d + 1e-5 );					// 4 mul, 1 rcp
            }
            half Vis_SmithJointApprox( half a2, half NoV, half NoL )
            {
	            half a = sqrt(a2);
	            half Vis_SmithV = NoL * ( NoV * ( 1.0h - a ) + a );
	            half Vis_SmithL = NoV * ( NoL * ( 1.0h - a ) + a );
	            return 0.5h * rcp( Vis_SmithV + Vis_SmithL + 1e-5);
            }
            half3 F_Schlick_UE4( half3 SpecularColor, half VoH )
            {
	            half Fc = Pow5( 1.0h - VoH );
	            return saturate( 50.0h * SpecularColor.g ) * Fc + (1.0h - Fc) * SpecularColor;
            }

            half3 StandardBRDF( half3 DiffuseColor, half3 SpecularColor, half Roughness, half3 N, half3 V, half3 L,half3 LightColor,half Shadow)
            {
	            half a2 = Pow4( Roughness );
	            half3 H = normalize(L + V);
	            half NoH = saturate(dot(N,H));
	            half NoV = saturate(abs(dot(N,V)) + 1e-5);
	            half NoL = saturate(dot(N,L));
	            half VoH = saturate(dot(V,H));
	            half3 Radiance = NoL * LightColor * Shadow * PI;
	
	            half3 DiffuseTerm = Diffuse_Lambert(DiffuseColor) * Radiance;
	            /*#if defined(_DIFFUSE_OFF)
		            DiffuseTerm = half3(0,0,0);
	            #endif*/
	            // Generalized microfacet specular
	            float D = D_GGX_UE4( a2, NoH );
	            half Vis = Vis_SmithJointApprox( a2, NoV, NoL );
	            half3 F = F_Schlick_UE4( SpecularColor, VoH );
	            half3 SpecularTerm = ((D * Vis) * F) * Radiance;
	            /*#if defined(_SPECULAR_OFF)
		            SpecularTerm = half3(0,0,0);
	            #endif*/

	            half3 DirectLighting = DiffuseTerm + SpecularTerm;
	            return DirectLighting;
            }

            half3 AOMultiBounce( half3 BaseColor, half AO )
            {
	            half3 a =  2.0404 * BaseColor - 0.3324;
	            half3 b = -4.7951 * BaseColor + 0.6417;
	            half3 c =  2.7552 * BaseColor + 0.6903;
	            return max( AO, ( ( AO * a + b ) * AO + c ) * AO );
            }
            half GetSpecularOcclusion(half NoV, half RoughnessSq, half AO)
            {
	            return saturate( pow( abs(NoV + AO), RoughnessSq ) - 1 + AO );
            }
            half3 EnvBRDFApprox( half3 SpecularColor, half Roughness, half NoV )
            {
	            const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
	            const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
	            half4 r = Roughness * c0 + c1;
	            half a004 = min( r.x * r.x, exp2( -9.28 * NoV ) ) * r.x + r.y;
	            half2 AB = half2( -1.04, 1.04 ) * a004 + r.zw;
	            AB.y *= saturate( 50.0 * SpecularColor.g );

	            return SpecularColor * AB.x + AB.y;
            }
            half3 RotateDirection(half3 R, half degrees)
            {
	            float3 reflUVW = R;
	            half theta = degrees * PI / 180.0f;
	            half costha = cos(theta);
	            half sintha = sin(theta);
	            reflUVW = half3(reflUVW.x * costha - reflUVW.z * sintha, reflUVW.y, reflUVW.x * sintha + reflUVW.z * costha);
	            return reflUVW;
            }

            //顶点 Shader
            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = normalInput.normalWS;
                real sign1 = input.tangentOS.w * GetOddNegativeScale();
                half4 tangentWS = half4(normalInput.tangentWS.xyz, sign1);
                output.tangentWS = tangentWS;
                //half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.positionWS.xyz = vertexInput.positionWS;
                output.positionWS.w = ComputeFogFactor(vertexInput.positionCS.z);
                output.positionCS = vertexInput.positionCS;
                output.staticLightmapUV = input.staticLightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;

                //自定义Fog
				output.fogFactor_zy.z = Transfer_Fog(output.positionCS.z, output.positionWS.xyz);

            	// 关键：识别正面/背面
                // 1 = 正面, -1 = 背面
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.facing = sign(dot(output.normalWS, viewDirWS));
                
                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                //---------------输入数据-----------------
                float2 UV = input.uv;
                float3 WorldPos = input.positionWS.xyz;
                float fogFactor = input.positionWS.w;
                half3 ViewDir = GetWorldSpaceNormalizeViewDir(WorldPos);
                half3 WorldNormal = normalize(input.normalWS);

            	
            	
                half3 WorldTangent = normalize(input.tangentWS.xyz);
                half3 WorldBinormal = normalize(cross(WorldNormal,WorldTangent) * input.tangentWS.w);
                half3x3 TBN = half3x3(WorldTangent,WorldBinormal,WorldNormal);

            	

                float2 ScreenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half4 ShadowMask = float4(1.0,1.0,1.0,1.0);
                //------------------材质参数----------------
                half4 BaseColorAlpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,UV) * _BaseColor * _BaseColorIntensity;
                half3 BaseColor = BaseColorAlpha.rgb;
                half BaseAlpha = BaseColorAlpha.a;
                #if defined(_ALPHATEST_ON)
                    clip(BaseAlpha - _Cutoff);
                #endif
                half4 specGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,UV);
                half Metallic = specGloss.r * _Metallic;
                //half Occlusion = lerp(1.0,specGloss.g,_Occlusion);
                half Roughness = max(1.0 - specGloss.a * _Smoothness,0.001f);
                half Occlusion = lerp(1.0,specGloss.g,_Occlusion);

                half3 NormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,UV),_BumpScale);
                WorldNormal = normalize(mul(NormalTS,TBN));

            	// 关键：识别正面/背面
            	float dotProduct = dot(WorldNormal, ViewDir);
            	//WorldNormal = dotProduct < 0 ? -WorldNormal : WorldNormal;
            	
                //--------------------BRDF相关数据-----------------
                half3 DiffuseColor = lerp(BaseColor,half3(0.0,0.0,0.0),Metallic);
                half3 SpecularColor = lerp(half3(0.04,0.04,0.04),BaseColor,Metallic);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(ScreenUV);
                    Occlusion = min(Occlusion,aoFactor.indirectAmbientOcclusion);
                #endif

                //-----------直接光照------------
                half3 DirectLighting = half3(0,0,0);
                {
	                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
	                float4 positionCS = TransformWorldToHClip(WorldPos);
                    float4 ShadowCoord = ComputeScreenPos(positionCS);
	                #else
                    float4 ShadowCoord = TransformWorldToShadowCoord(WorldPos);
	                #endif

	                //float4 ShadowMask = float4(1.0,1.0,1.0,1.0); //正常时

                    //针对开启shadow map时，动态物体不受静态物体的遮挡，室内的动态物体漏光，需要同lightpro的光源来替换环境光SH
                    #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
                    half4 ShadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                    #elif !defined (LIGHTMAP_ON)
                    half4 ShadowMask = unity_ProbesOcclusion; 
                    #else
                    half4 ShadowMask = half4(1,1,1,1);
                    #endif

	                //主光源
                    half3 DirectLighting_MainLight = half3(0,0,0);
                    {
                        Light light = GetMainLight(ShadowCoord,WorldPos,ShadowMask);
                        half3 LightDir = light.direction;
                        half3 LightColor = light.color * light.distanceAttenuation;
                        half Shadow = light.shadowAttenuation;
                        DirectLighting_MainLight = StandardBRDF(DiffuseColor,SpecularColor,Roughness,WorldNormal,ViewDir,LightDir,LightColor,Shadow);
                    }
                    //附加光源
                    half3 DirectLighting_AddLight = half3(0,0,0);
                    {
                        #ifdef _ADDITIONAL_LIGHTS
                        uint pixelLightCount = GetAdditionalLightsCount();
                        for(uint lightIndex = 0; lightIndex < pixelLightCount ; ++lightIndex)
                        {
                            Light light = GetAdditionalLight(lightIndex,WorldPos,ShadowMask);
                            half3 LightDir = light.direction;
                            half3 LightColor = light.color * light.distanceAttenuation;
                            half Shadow = light.shadowAttenuation;
                            DirectLighting_AddLight += StandardBRDF(DiffuseColor,SpecularColor,Roughness,WorldNormal,ViewDir,LightDir,LightColor,Shadow);
                        }
                        #endif
                    }

                    DirectLighting = DirectLighting_MainLight + DirectLighting_AddLight;
                }

                //处理精确光源的间接光照、以及部分光源(环境光等)的直接光+间接光
                half3 IndirectLighting = half3(0,0,0);
                {
	                half NoV = saturate(abs(dot(WorldNormal,ViewDir)) + 1e-5);
	                //Indirect Diffuse
	                float3 DiffuseAO = AOMultiBounce(DiffuseColor,Occlusion);

                    half3 Irradiance = half3(0,0,0);
                    #if defined(LIGHTMAP_ON)
                        float4 encodedIrradiance = SAMPLE_TEXTURE2D(unity_Lightmap,samplerunity_Lightmap,input.staticLightmapUV);
                        Irradiance = DecodeLightmap(encodedIrradiance, float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h));
                        #if defined(DIRLIGHTMAP_COMBINED) //&& !defined(_DIR_OFF)
                            float4 direction = SAMPLE_TEXTURE2D(unity_LightmapInd,samplerunity_Lightmap,input.staticLightmapUV);
                            half3 LightDir = direction.rgb * 2.0f - 1.0f;
                            half halfLambert = dot(WorldNormal,LightDir) * 0.5 + 0.5;
                            Irradiance = Irradiance * halfLambert / max(1e-4,direction.w);

                            //#if !defined(_DIRHIGHLIGHT_OFF)
                            half3 H = normalize(LightDir.rgb + ViewDir.rgb);
                            half NoH = saturate(dot(WorldNormal,normalize(LightDir + ViewDir)));
	                        half NoL = saturate(dot(WorldNormal,LightDir));
	                        half VoH = saturate(dot(ViewDir,H));
                            half a2 = pow( Roughness , 5);
                            float D = D_GGX_UE4( a2, NoH );
	                        half Vis = Vis_SmithJointApprox( a2, NoV, NoL );
	                        half3 F = F_Schlick_UE4( SpecularColor, VoH );
                            float4 ShadowCoord = TransformWorldToShadowCoord(WorldPos);
                            Light light = GetMainLight(ShadowCoord,WorldPos,ShadowMask);
                            half3 LightColor = light.color * light.distanceAttenuation;
                            half Shadow = light.shadowAttenuation;
                    
                            half3 Radiance = NoL * LightColor * Shadow * PI;
	                        half3 SpecularTerm = ((D * Vis) * F) * Radiance;
                            half BlinnPhong = saturate(dot(WorldNormal,normalize(LightDir + ViewDir)));
                            Irradiance = Irradiance + Irradiance * D * 30
                    ;
                            //#endif
                    
                        #endif
                    #else
	                    Irradiance = SampleSH(WorldNormal);//SH,Light Probe
                    #endif

                    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                        float4 positionCS = TransformWorldToHClip(WorldPos);
                        float4 shadowCoord = ComputeScreenPos(positionCS);
                    #else
                        float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
                    #endif
                    float4 ShadowMask = float4(1.0,1.0,1.0,1.0);
                    Light mainLight = GetMainLight(shadowCoord,WorldPos,ShadowMask);
                    #if defined(LIGHTMAP_ON)&& defined(_MIXED_LIGHTING_SUBTRACTIVE)
                        Irradiance = SubtractDirectMainLightFromLightmap(mainLight, WorldNormal,Irradiance);
                    #endif

                 //   #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
	                //float4 positionCS = TransformWorldToHClip(WorldPos);
                 //   float4 ShadowCoord = ComputeScreenPos(positionCS);
	                //#else
                 //   float4 ShadowCoord = TransformWorldToShadowCoord(WorldPos);
	                //#endif
	                //float4 ShadowMask = float4(1.0,1.0,1.0,1.0);
                 //   Light light = GetMainLight(ShadowCoord,WorldPos,ShadowMask);
                 //   half Shadow = light.shadowAttenuation;
                 //   Irradiance *= Shadow;

	                float3 IndirectDiffuse = Irradiance * DiffuseColor * DiffuseAO;
	                /*#if defined(_SH_OFF)
		                IndirectDiffuse = half3(0,0,0);
	                #endif*/
	                //Indirect Specular
	                half3 R = reflect(-ViewDir,WorldNormal);
                    R = RotateDirection(R,_EnvRotation);
	                half3 SpeucularLD = GlossyEnvironmentReflection(R,WorldPos,Roughness,Occlusion);
	                half3 SpecularDFG = EnvBRDFApprox(SpecularColor,Roughness,NoV);
	                half SpecularOcclusion = GetSpecularOcclusion(NoV,Roughness * Roughness,Occlusion);
	                half3 SpecularAO = AOMultiBounce(SpecularColor,SpecularOcclusion);
	                half3 IndirectSpecular = SpeucularLD * SpecularDFG * SpecularAO;
	                /*#if defined(_IBL_OFF)
		                IndirectSpecular = half3(0,0,0);
	                #endif*/

	                IndirectLighting = IndirectDiffuse * _SHScale + IndirectSpecular * _IBLScale;
                }

                half3 Emission = half3(0,0,0);
                #if defined(_EMISSION)
                    Emission = specGloss.b * _EmissionColor.rgb * _EmissionIntensity * BaseColor.rgb;
                #endif

                half4 color = saturate(half4(DirectLighting + IndirectLighting + Emission, BaseAlpha));
                //unity 雾
                //color.rgb = MixFog(color.rgb,fogFactor);

                //自定义Fog
				#ifdef _ENABLEFOG
				color.rgb = Mix_Fog(color.rgb, input.fogFactor_zy.z);
				#endif

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Material Keywords
            
            //#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // -------------------------------------
            // Universal Pipeline keywords

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //半透明模式下的半透明模型阴影的显示写法
            #if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
                #define CAN_SKIP_VPOS
            #endif

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                return positionCS;
            }

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE3D(_DitherMaskLOD);
            SAMPLER(sampler_DitherMaskLOD);

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _Metallic, _BaseColorIntensity, _EnvRotation, _IBLScale, _SHScale , _EmissionIntensity;
            half _Smoothness;
            half _Occlusion;
            half _BumpScale;
            half _Cutoff;
            float4 _EmissionColor;
            //自定义Fog
	        uniform half4 _FogParameter7; //(x:FogEnd,y:FogStart,z:FogBottom,w:FogTop)
			uniform half4 _FogColor7;
            CBUFFER_END

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input
            #if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
            ) : SV_TARGET
            {
                half4 BaseColorAlpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv) * _BaseColor;
                half3 BaseColor = BaseColorAlpha.rgb;
                half BaseAlpha = BaseColorAlpha.a;
                #if defined(_ALPHATEST_ON)
                    clip(BaseAlpha - _Cutoff);
                #endif

                /*#if defined( _SURFACE_TYPE_TRANSPARENT )
                    #if defined( CAN_SKIP_VPOS )
				        float2 vpos = input.positionCS;
				    #endif
                    half alphaRef = SAMPLE_TEXTURE3D( _DitherMaskLOD,sampler_DitherMaskLOD, float3( vpos.xy * 0.25, BaseAlpha * 0.9375 ) ).a;
				    clip( alphaRef - 0.01 );
                #endif*/

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            
            //#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
                #define CAN_SKIP_VPOS
            #endif

            struct Attributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE3D(_DitherMaskLOD);  SAMPLER(sampler_DitherMaskLOD);

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _Metallic, _BaseColorIntensity, _EnvRotation, _IBLScale, _SHScale , _EmissionIntensity;
            half _Smoothness;
            half _Occlusion;
            half _BumpScale;
            half _Cutoff;
            float4 _EmissionColor;
            //自定义Fog
	        uniform half4 _FogParameter7; //(x:FogEnd,y:FogStart,z:FogBottom,w:FogTop)
			uniform half4 _FogColor7;
            CBUFFER_END

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input
             #if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif 
            ) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 BaseColorAlpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv) * _BaseColor;
                half3 BaseColor = BaseColorAlpha.rgb;
                half BaseAlpha = BaseColorAlpha.a;
                #if defined(_ALPHATEST_ON)
                    clip(BaseAlpha - _Cutoff);
                #endif

                /*#if defined(_SURFACE_TYPE_TRANSPARENT)
                    #if defined( CAN_SKIP_VPOS )
				    float2 vpos = input.positionCS;
				    #endif
                    half alphaRef = SAMPLE_TEXTURE3D( _DitherMaskLOD,sampler_DitherMaskLOD, float3( vpos.xy * 0.25, BaseAlpha * 0.9375 ) ).a;
				    clip( alphaRef - 0.01 );
                #endif*/

                return 0;
            }

            ENDHLSL
        }

        //开SSAO时用到
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            
            //#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS     : POSITION;
                float4 tangentOS      : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float3 normal       : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD1;
                half3 normalWS     : TEXCOORD2;
                half4 tangentWS    : TEXCOORD3;    // xyz: tangent, w: sign

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            TEXTURE3D(_DitherMaskLOD);  SAMPLER(sampler_DitherMaskLOD);

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _Metallic, _BaseColorIntensity, _EnvRotation, _IBLScale, _SHScale , _EmissionIntensity;
            half _Smoothness;
            half _Occlusion;
            half _BumpScale;
            half _Cutoff;
            float4 _EmissionColor;
            //自定义Fog
	        uniform half4 _FogParameter7; //(x:FogEnd,y:FogStart,z:FogBottom,w:FogTop)
			uniform half4 _FogColor7;
            CBUFFER_END

            #if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
                #define CAN_SKIP_VPOS
            #endif

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangentOS);

                output.normalWS = half3(normalInput.normalWS);
                output.normalWS = normalInput.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
                output.tangentWS = tangentWS;

                return output;
            }


            half4 DepthNormalsFragment(Varyings input
            #if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
            ) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                //---------------输入数据-----------------
                half3 WorldNormal = normalize(input.normalWS);
                half3 WorldTangent = normalize(input.tangentWS.xyz);
                half3 WorldBinormal = normalize(cross(WorldNormal,WorldTangent) * input.tangentWS.w);
                half3x3 TBN = half3x3(WorldTangent,WorldBinormal,WorldNormal);

                //------------------材质参数----------------
                half4 BaseColorAlpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv) * _BaseColor;
                half3 BaseColor = BaseColorAlpha.rgb;
                half BaseAlpha = BaseColorAlpha.a;
                #if defined(_ALPHATEST_ON)
                    clip(BaseAlpha - _Cutoff);
                #endif

                /*#if defined(_SURFACE_TYPE_TRANSPARENT)
                    #if defined( CAN_SKIP_VPOS )
				    float2 vpos = input.positionCS;
				    #endif
                    half alphaRef = SAMPLE_TEXTURE3D( _DitherMaskLOD,sampler_DitherMaskLOD, float3( vpos.xy * 0.25, BaseAlpha * 0.9375 ) ).a;
				    clip( alphaRef - 0.01 );
                #endif*/

                half3 NormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_BumpScale);
                WorldNormal = normalize(mul(NormalTS,TBN));

                return half4(WorldNormal,0.0f);
            }

            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags{"LightMode" = "Meta"}

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma shader_feature_local_fragment _SPECGLOSSMAP

            //#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv0          : TEXCOORD0;
                float2 uv1          : TEXCOORD1;
                float2 uv2          : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            #ifdef EDITOR_VISUALIZATION
                float2 VizUV        : TEXCOORD1;
                float4 LightCoord   : TEXCOORD2;
            #endif
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicGlossMap);    SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _Metallic, _BaseColorIntensity, _EnvRotation, _IBLScale, _SHScale , _EmissionIntensity;
            half _Smoothness;
            half _Occlusion;
            half _BumpScale;
            half _Cutoff;
            float4 _EmissionColor;
            //自定义Fog
	        uniform half4 _FogParameter7; //(x:FogEnd,y:FogStart,z:FogBottom,w:FogTop)
			uniform half4 _FogColor7;
            CBUFFER_END

            Varyings UniversalVertexMeta(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
            #ifdef EDITOR_VISUALIZATION
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.VizUV, output.LightCoord);
            #endif
                return output;
            }

            half4 UniversalFragmentMetaLit(Varyings input) : SV_Target
            {
                //------------------材质参数----------------
                half4 BaseColorAlpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv) * _BaseColor;
                half3 BaseColor = BaseColorAlpha.rgb;
                half BaseAlpha = BaseColorAlpha.a;
                #if defined(_ALPHATEST_ON)
                    clip(BaseAlpha - _Cutoff);
                #endif
                half4 specGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,input.uv);
                half Metallic = specGloss.r * _Metallic;
                half Occlusion = lerp(1.0,specGloss.g,_Occlusion);
                half Roughness = max(1.0 - specGloss.a * _Smoothness,0.001f);

                //--------------------BRDF相关数据-----------------
                half3 DiffuseColor = lerp(BaseColor,half3(0.0,0.0,0.0),Metallic);
                half3 SpecularColor = lerp(half3(0.04,0.04,0.04),BaseColor,Metallic);

                half3 Emission = half3(0,0,0);
                #if defined(_EMISSION)
                    Emission = specGloss.b * _EmissionColor * _EmissionIntensity * BaseColor; 
                #endif

                MetaInput metaInput;
                metaInput.Albedo = DiffuseColor + SpecularColor *Roughness * 0.5;
                metaInput.Emission = Emission;
                #ifdef EDITOR_VISUALIZATION
                    metaInput.VizUV = input.VizUV;
                    metaInput.LightCoord = input.LightCoord;
                #endif

                return UnityMetaFragment(metaInput);
            }

            ENDHLSL
        }

    }

    //FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
