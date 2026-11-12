// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Water_transparent"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		_DayIntensity("总体亮度控制", Float) = 0.75
		_Alpha("总体透明度控制", Float) = 1
		[Header((Color Settings))][Space(5)]_ShallowColor("浅水颜色", Color) = (0.4862745,1,0.8588235,0)
		_DeepColor("深水颜色", Color) = (0,0.4666667,0.7450981,0)
		_WaterDeep("水深浅范围", Float) = 5
		_ShoreDistance("边缘透明范围", Float) = 1.5
		_FresnelColor("菲涅尔颜色", Color) = (0.3686275,0.6431373,0.9137255,0)
		_FresnelIntensity("菲涅尔强度", Float) = 0.2
		_ReflectionAngle("菲涅尔反射角度", Float) = 1
		_SpecTint("SpecTint", Color) = (1,1,1,1)
		_SpecIntensity("SpecIntensity", Float) = 1
		_SpecSmoothness("SpecSmoothness", Float) = 0.1
		_SpecStart("SpecStart", Float) = 0
		_SpecEnd("SpecEnd", Float) = 200
		_ReflectExhance("反射度增强", Float) = 1
		_WaterNoise("WaterNoise", Float) = 1
		_Normal_SpeedDir("Normal_SpeedDir", Vector) = (0,0,0,0)
		_WaterNormalSmall("细波纹法线", 2D) = "bump" {}
		_SmallNormalTiling("Small Normal Tiling", Float) = 10
		_SmallNormalSpeed("Small Normal Speed", Float) = 5
		_SmallNormalIntensity("Small Normal Intensity", Float) = 0.1
		_WaterNormalLarge("大波纹法线", 2D) = "bump" {}
		_LargeNormalTiling("Large Normal Tiling", Float) = 10
		_LargeNormalSpeed("Large Normal Speed", Float) = 5
		_LargeNormalIntensity("Large Normal Intensity", Float) = 0.1
		_BlinkTilling("BlinkTilling", Float) = 8
		_BlinkSpeed("BlinkSpeed", Float) = 1
		_BlinkNoise("BlinkNoise", Float) = 1
		_BlinkThreshold("BlinkThreshold", Float) = 2
		_BlinkIntensity("BlinkIntensity", Float) = 5
		[Header((Caustics))][Space(5)][Toggle(_CAUSTICS_ON)] _Caustics("焦散动画", Float) = 1
		_CausticsTex("焦散图", 2D) = "white" {}
		_CausticsScale("焦散大小", Float) = 5
		_CausticsSpeed("焦散速度", Vector) = (-8,0,0,0)
		_CausticsIntensity("焦散亮度", Float) = 1
		[Header((Foam))][Space(5)][Toggle(_FOAM_ON)] _FOAM("岸边泡沫", Float) = 1
		[NoScaleOffset]_FoamNoise("泡沫Noise", 2D) = "white" {}
		_FoamNoise2("泡沫Noise2", 2D) = "white" {}
		_FoamNoise3("泡沫Noise3", 2D) = "white" {}
		_Noise_speed3("Noise_speed3", Vector) = (0,0,0,0)
		_Noise_FoamD("Noise_FoamD", Float) = 0
		_XTilling("泡沫TillingX", Float) = 10
		_YTilling("泡沫TillingY", Float) = 1
		_FoamNoiseSpeed("泡沫速度", Vector) = (0,-0.3,0,0)
		_FoamOffset("泡沫偏移", Float) = 0
		_FoamRange("泡沫范围", Float) = 1.5
		_FoamColor("泡沫颜色", Color) = (1,1,1,1)
		_FoamColorIntensity("泡沫颜色强度", Float) = 2
		[Header((Wave))][Space(5)][Toggle(_VERTEXWAVE_ON)] _VERTEXWAVE("顶点波纹动画", Float) = 1
		_Direction("水波运动方向（XY）", Vector) = (1,1,0,0)
		_WaveSpeed("水波速度", Float) = 2.4
		_WaveDistance("水波大小", Range( 0 , 1)) = 0.7
		_WaveHeight("水波高度", Float) = 0.15
		_SubWaveDirection("细节波形方向（XYZW）", Vector) = (-1,-1,-1,-1)
		_WaveNormalStr("水波法线强度", Float) = 0.16
		_WaveFadeStart("水波渐隐Start", Float) = 25
		_WaveFadeEnd("水波渐隐End", Float) = 280
		[HDR]_WaveColor("波峰颜色", Color) = (0.3686275,0.6431373,0.9137255,0)
		_Size("Size", Range( 0 , 1)) = 1
		_FallOff("FallOff", Range( 0 , 1)) = 1
		_MotionVertexDisplacement("交互顶点强度", Float) = 0.1

		[HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		_TessValue( "Max Tessellation", Range( 1, 32 ) ) = 16
		_TessMin( "Tess Min Distance", Float ) = 10
		_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25
	}

	SubShader
	{
		LOD 0

		
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
		
		Cull Back
		AlphaToMask Off
		
		HLSLINCLUDE
		#pragma target 3.0

		#pragma prefer_hlslcc gles
		#pragma exclude_renderers d3d11_9x 

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}
		
		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
						  (( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS

		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForwardOnly" }
			
			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_DISTANCE_TESSELLATION
			#define TESSELLATION_ON 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma shader_feature _ _SAMPLE_GI
			#pragma multi_compile _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ DEBUG_DISPLAY
			#define SHADERPASS SHADERPASS_UNLIT


			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"


			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local _VERTEXWAVE_ON
			#pragma shader_feature_local_fragment _CAUSTICS_ON
			#pragma shader_feature_local_fragment _FOAM_ON
			#include "WavesFunction.hlsl"


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef ASE_FOG
				float fogFactor : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_texcoord9 : TEXCOORD9;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _WaveColor;
			float4 _SpecTint;
			float4 _SubWaveDirection;
			float4 _FresnelColor;
			float4 _DeepColor;
			float4 _FoamColor;
			float4 _FoamNoise3_ST;
			float4 _FoamNoise2_ST;
			float4 _ShallowColor;
			float2 _Normal_SpeedDir;
			float2 _CausticsSpeed;
			float2 _FoamNoiseSpeed;
			float2 _Noise_speed3;
			float2 _Direction;
			float _YTilling;
			float _BlinkIntensity;
			float _SpecSmoothness;
			float _SpecIntensity;
			float _Alpha;
			float _ShoreDistance;
			float _SpecEnd;
			float _SpecStart;
			float _DayIntensity;
			float _Noise_FoamD;
			float _FoamOffset;
			float _FoamRange;
			float _BlinkThreshold;
			float _XTilling;
			float _FoamColorIntensity;
			float _BlinkNoise;
			float _WaveSpeed;
			float _BlinkTilling;
			float _WaveDistance;
			float _WaveHeight;
			float _WaveNormalStr;
			float _WaveFadeStart;
			float _WaveFadeEnd;
			float _MotionVertexDisplacement;
			float _CausticsScale;
			float _CausticsIntensity;
			float _ReflectExhance;
			float _BlinkSpeed;
			float _SmallNormalSpeed;
			float _SmallNormalIntensity;
			float _LargeNormalSpeed;
			float _LargeNormalTiling;
			float _LargeNormalIntensity;
			float _WaterDeep;
			float _ReflectionAngle;
			float _FresnelIntensity;
			float _Size;
			float _WaterNoise;
			float _SmallNormalTiling;
			float _FallOff;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WaterMotionRT;
			float4 _WaterFXCamPos;
			float _WaterFXCamSize;
			sampler2D _CausticsTex;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _WaterNormalSmall;
			sampler2D _WaterNormalLarge;
			sampler2D _PlanarReflectionTexture;
			sampler2D _FoamNoise;
			sampler2D _FoamNoise2;
			sampler2D _FoamNoise3;


			float3 ReconstructWorldPos243( float2 ScreenUV, float rawdepth )
			{
				#if UNITY_REVERSED_Z
				real depth = rawdepth;
				#else
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth);
				#endif
				float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP);
				return worldPos;
			}
			
			
			VertexOutput VertexFunction ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (ase_worldPos).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				float3 worldToObj203 = mul( GetWorldToObjectMatrix(), float4( ( ase_worldPos + positionWSOffset548 ), 1 ) ).xyz;
				#ifdef _VERTEXWAVE_ON
				float3 staticSwitch207 = worldToObj203;
				#else
				float3 staticSwitch207 = v.vertex.xyz;
				#endif
				float4 tex2DNode457 = tex2Dlod( _WaterMotionRT, float4( ( ( ( (ase_worldPos).xz - (_WaterFXCamPos).xz ) / ( _WaterFXCamSize * 2.0 ) ) + float2( 0.5,0.5 ) ), 0, 0.0) );
				float2 temp_output_1_0_g8 = ((tex2DNode457).rg*2.0 + -1.0);
				float dotResult4_g8 = dot( temp_output_1_0_g8 , temp_output_1_0_g8 );
				float3 appendResult10_g8 = (float3((temp_output_1_0_g8).x , (temp_output_1_0_g8).y , sqrt( ( 1.0 - saturate( dotResult4_g8 ) ) )));
				float3 normalizeResult12_g8 = normalize( appendResult10_g8 );
				float3 temp_output_461_0 = normalizeResult12_g8;
				float3 MotionNormal467 = temp_output_461_0;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
				float3 tangentTobjectDir202 = mul( GetWorldToObjectMatrix(), float4( mul( ase_tangentToWorldFast, MotionNormal467 ), 0 ) ).xyz;
				float clampResult463 = clamp( tex2DNode457.a , 0.0 , 1.0 );
				float MotionPower468 = clampResult463;
				float3 WaveVertexPos209 = ( staticSwitch207 + ( tangentTobjectDir202 * MotionPower468 * 0.1 * _MotionVertexDisplacement ) );
				
				float3 vertexToFrag413 = WaveVertexPos209;
				o.ase_texcoord3.xyz = vertexToFrag413;
				o.ase_texcoord4.xyz = ase_worldTangent;
				o.ase_texcoord5.xyz = ase_worldNormal;
				o.ase_texcoord6.xyz = ase_worldBitangent;
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord7 = screenPos;
				
				o.ase_texcoord8 = v.vertex;
				o.ase_texcoord9.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord3.w = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				o.ase_texcoord6.w = 0;
				o.ase_texcoord9.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = WaveVertexPos209;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				#ifdef ASE_FOG
				o.fogFactor = ComputeFogFactor( positionCS.z );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif
				float4 color309 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
				float3 vertexToFrag413 = IN.ase_texcoord3.xyz;
				float4 objectToClip8_g9 = TransformObjectToHClip(vertexToFrag413);
				float3 objectToClip8_g9NDC = objectToClip8_g9.xyz/objectToClip8_g9.w;
				float4 appendResult13_g9 = (float4(objectToClip8_g9NDC , 1.0));
				float4 computeScreenPos10_g9 = ComputeScreenPos( appendResult13_g9 );
				float4 ScreenPos415 = computeScreenPos10_g9;
				float2 ScreenUV243 = ScreenPos415.xy;
				float clampDepth240 = SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPos415.xy );
				float rawdepth243 = clampDepth240;
				float3 localReconstructWorldPos243 = ReconstructWorldPos243( ScreenUV243 , rawdepth243 );
				float3 UnderWaterPos398 = localReconstructWorldPos243;
				float2 temp_output_267_0 = ( (UnderWaterPos398).xz / _CausticsScale );
				float2 temp_output_272_0 = ( _CausticsSpeed * _TimeParameters.x * 0.01 );
				float clampResult425 = clamp( ( _ReflectExhance * 0.04 ) , 0.0 , 0.5 );
				float temp_output_496_0 = ( _SmallNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_579_0 = ( float2( 0.1,0.1 ) + _Normal_SpeedDir );
				float2 temp_output_545_0 = ( (WorldPosition).xz / _SmallNormalTiling );
				float2 panner499 = ( temp_output_496_0 * temp_output_579_0 + temp_output_545_0);
				float3 unpack508 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner499 ), _SmallNormalIntensity );
				unpack508.z = lerp( 1, unpack508.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_580_0 = ( float2( -0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner498 = ( temp_output_496_0 * temp_output_580_0 + ( temp_output_545_0 + 0.4 ));
				float3 unpack510 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner498 ), _SmallNormalIntensity );
				unpack510.z = lerp( 1, unpack510.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_581_0 = ( float2( -0.1,0.1 ) + _Normal_SpeedDir );
				float2 panner503 = ( temp_output_496_0 * temp_output_581_0 + ( temp_output_545_0 + float2( 10,0.15 ) ));
				float3 unpack507 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner503 ), _SmallNormalIntensity );
				unpack507.z = lerp( 1, unpack507.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_582_0 = ( float2( 0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner500 = ( temp_output_496_0 * temp_output_582_0 + ( temp_output_545_0 + float2( 0.65,0.75 ) ));
				float3 unpack509 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner500 ), _SmallNormalIntensity );
				unpack509.z = lerp( 1, unpack509.z, saturate(_SmallNormalIntensity) );
				float temp_output_495_0 = ( _LargeNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_542_0 = ( (WorldPosition).xz / _LargeNormalTiling );
				float2 panner502 = ( temp_output_495_0 * temp_output_579_0 + temp_output_542_0);
				float3 unpack505 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner502 ), _LargeNormalIntensity );
				unpack505.z = lerp( 1, unpack505.z, saturate(_LargeNormalIntensity) );
				float2 panner504 = ( temp_output_495_0 * temp_output_580_0 + ( temp_output_542_0 + 0.4 ));
				float3 unpack511 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner504 ), _LargeNormalIntensity );
				unpack511.z = lerp( 1, unpack511.z, saturate(_LargeNormalIntensity) );
				float2 panner497 = ( temp_output_495_0 * temp_output_581_0 + ( temp_output_542_0 + float2( 0.85,0.15 ) ));
				float3 unpack506 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner497 ), _LargeNormalIntensity );
				unpack506.z = lerp( 1, unpack506.z, saturate(_LargeNormalIntensity) );
				float2 panner501 = ( temp_output_495_0 * temp_output_582_0 + ( temp_output_542_0 + float2( 0.65,0.75 ) ));
				float3 unpack512 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner501 ), _LargeNormalIntensity );
				unpack512.z = lerp( 1, unpack512.z, saturate(_LargeNormalIntensity) );
				float2 temp_output_27_0 = ( (BlendNormal( BlendNormal( BlendNormal( unpack508 , unpack510 ) , BlendNormal( unpack507 , unpack509 ) ) , BlendNormal( BlendNormal( unpack505 , unpack511 ) , BlendNormal( unpack506 , unpack512 ) ) )).xy * 0.5 );
				float dotResult29 = dot( temp_output_27_0 , temp_output_27_0 );
				float3 appendResult32 = (float3(temp_output_27_0 , sqrt( ( 1.0 - dotResult29 ) )));
				float3 ase_worldTangent = IN.ase_texcoord4.xyz;
				float3 ase_worldNormal = IN.ase_texcoord5.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord6.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 tanNormal33 = appendResult32;
				float3 worldNormal33 = float3(dot(tanToWorld0,tanNormal33), dot(tanToWorld1,tanNormal33), dot(tanToWorld2,tanNormal33));
				float3 WaterNormal34 = worldNormal33;
				float3 tanNormal416 = WaterNormal34;
				float3 worldNormal416 = float3(dot(tanToWorld0,tanNormal416), dot(tanToWorld1,tanNormal416), dot(tanToWorld2,tanNormal416));
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult418 = dot( worldNormal416 , ase_worldViewDir );
				float temp_output_423_0 = ( 1.0 - max( dotResult418 , 0.0 ) );
				float lerpResult426 = lerp( clampResult425 , 1.0 , ( temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 ));
				float FresnelFactor427 = lerpResult426;
				float depthToLinear241 = LinearEyeDepth(clampDepth240,_ZBufferParams);
				float depthToLinear399 = LinearEyeDepth(ScreenPos415.z,_ZBufferParams);
				float WaterDepth244 = ( depthToLinear241 - depthToLinear399 );
				float clampResult253 = clamp( ( WaterDepth244 / _WaterDeep ) , 0.0 , 1.0 );
				float clampResult282 = clamp( max( FresnelFactor427 , ( FresnelFactor427 + clampResult253 ) ) , 0.0 , 1.0 );
				float WaterDepthRange294 = clampResult282;
				#ifdef _CAUSTICS_ON
				float4 staticSwitch311 = ( ( min( tex2D( _CausticsTex, ( temp_output_267_0 + temp_output_272_0 ) ) , tex2D( _CausticsTex, ( -temp_output_267_0 + temp_output_272_0 ) ) ) * _CausticsIntensity ) * ( 1.0 - WaterDepthRange294 ) );
				#else
				float4 staticSwitch311 = color309;
				#endif
				float4 CausticsColor407 = staticSwitch311;
				float temp_output_429_0 = pow( lerpResult426 , _ReflectionAngle );
				float temp_output_435_0 = ( temp_output_429_0 * _FresnelIntensity * 5.0 );
				float clampResult437 = clamp( ( temp_output_435_0 * temp_output_435_0 ) , 0.0 , 1.0 );
				float ColorFresnel433 = clampResult437;
				float4 lerpResult357 = lerp( _DeepColor , _FresnelColor , ColorFresnel433);
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (WorldPosition).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				#ifdef _FOAM_ON
				float staticSwitch219 = (positionWSOffset548).y;
				#else
				float staticSwitch219 = 0.0;
				#endif
				float WaveY220 = staticSwitch219;
				float clampResult345 = clamp( WaveY220 , 0.0 , 1.0 );
				float4 lerpResult372 = lerp( lerpResult357 , _WaveColor , clampResult345);
				float4 lerpResult339 = lerp( ( _ShallowColor + CausticsColor407 ) , lerpResult372 , clampResult282);
				float dotResult557 = dot( _MainLightPosition.xyz , ase_worldNormal );
				float4 WaterColor360 = ( lerpResult339 * ( ( dotResult557 * 0.5 ) + 0.5 ) * float4( _MainLightColor.rgb , 0.0 ) );
				float4 screenPos = IN.ase_texcoord7;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float4 unityObjectToClipPos40 = TransformWorldToHClip(TransformObjectToWorld(IN.ase_texcoord8.xyz));
				float2 temp_output_115_0 = ( (WorldPosition).xz / _BlinkTilling );
				float temp_output_113_0 = ( _TimeParameters.x * 0.1 * _BlinkSpeed );
				float2 temp_output_126_0 = ( (( UnpackNormalScale( tex2D( _WaterNormalSmall, ( temp_output_115_0 + temp_output_113_0 ) ), 1.0f ) + UnpackNormalScale( tex2D( _WaterNormalSmall, ( ( temp_output_115_0 * 1.5 ) + ( temp_output_113_0 * -1.0 ) ) ), 1.0f ) )).xy * 0.5 );
				float dotResult127 = dot( temp_output_126_0 , temp_output_126_0 );
				float3 appendResult130 = (float3(temp_output_126_0 , sqrt( ( 1.0 - dotResult127 ) )));
				float3 tanNormal131 = appendResult130;
				float3 worldNormal131 = float3(dot(tanToWorld0,tanNormal131), dot(tanToWorld1,tanNormal131), dot(tanToWorld2,tanNormal131));
				float3 WaterNormalBlink132 = worldNormal131;
				float4 temp_cast_2 = (_BlinkThreshold).xxxx;
				float4 ReflectBlink147 = ( max( ( tex2D( _PlanarReflectionTexture, ( (ase_screenPosNorm).xy + ( (WaterNormalBlink132).xz * _BlinkNoise ) ) ) - temp_cast_2 ) , float4( 0,0,0,0 ) ) * _BlinkIntensity );
				float4 ReflectColor44 = ( tex2D( _PlanarReflectionTexture, ( (ase_screenPosNorm).xy + ( ( (WaterNormal34).xz / ( 1.0 + unityObjectToClipPos40.w ) ) * _WaterNoise ) ) ) + ReflectBlink147 );
				float dotResult86 = dot( ase_worldNormal , ase_worldViewDir );
				float clampResult88 = clamp( dotResult86 , 0.0 , 1.0 );
				float temp_output_89_0 = ( 1.0 - clampResult88 );
				float clampResult103 = clamp( ( temp_output_89_0 + 0.2 ) , 0.0 , 1.0 );
				float4 lerpResult90 = lerp( WaterColor360 , ( ReflectColor44 * clampResult103 ) , temp_output_89_0);
				float3 normalizeResult51 = normalize( ( ase_worldViewDir + _MainLightPosition.xyz ) );
				float dotResult52 = dot( WaterNormal34 , normalizeResult51 );
				float clampResult75 = clamp( ( ( _SpecEnd - distance( WorldPosition , _WorldSpaceCameraPos ) ) / ( _SpecEnd - _SpecStart ) ) , 0.0 , 1.0 );
				float4 SpecColor63 = ( ( ( pow( max( dotResult52 , 0.0 ) , ( _SpecSmoothness * 256.0 ) ) * _SpecTint ) * _SpecIntensity ) * clampResult75 );
				float temp_output_249_0 = ( WaterDepth244 / _FoamRange );
				float clampResult280 = clamp( ( _FoamOffset + temp_output_249_0 ) , 0.0 , 1.0 );
				float temp_output_287_0 = ( 1.0 - clampResult280 );
				float depthToLinear254 = LinearEyeDepth(IN.clipPos.z,_ZBufferParams);
				#ifdef _FOAM_ON
				float staticSwitch283 = step( 0.0 , ( depthToLinear241 - depthToLinear254 ) );
				#else
				float staticSwitch283 = 0.0;
				#endif
				float FoamMask292 = staticSwitch283;
				float2 texCoord258 = IN.ase_texcoord9.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult259 = clamp( temp_output_249_0 , 0.0 , 1.0 );
				float2 appendResult276 = (float2(( _XTilling * texCoord258.x ) , ( clampResult259 * _YTilling )));
				float2 panner281 = ( 1.0 * _Time.y * _FoamNoiseSpeed + appendResult276);
				float4 tex2DNode288 = tex2D( _FoamNoise, panner281 );
				float clampResult306 = clamp( ( ( temp_output_287_0 - -1.0 ) * FoamMask292 * ( temp_output_287_0 * tex2DNode288.r ) ) , 0.0 , 1.0 );
				float2 uv_FoamNoise2 = IN.ase_texcoord9.xy * _FoamNoise2_ST.xy + _FoamNoise2_ST.zw;
				float2 uv_FoamNoise3 = IN.ase_texcoord9.xy * _FoamNoise3_ST.xy + _FoamNoise3_ST.zw;
				float2 panner336 = ( 1.0 * _Time.y * _Noise_speed3 + tex2D( _FoamNoise3, uv_FoamNoise3 ).rg);
				float2 lerpResult349 = lerp( uv_FoamNoise2 , panner336 , _Noise_FoamD);
				float dotResult554 = dot( _MainLightPosition.xyz , ase_worldNormal );
				#ifdef _FOAM_ON
				float4 staticSwitch344 = ( clampResult306 * _FoamColor * _FoamColorIntensity * tex2D( _FoamNoise2, lerpResult349 ) * dotResult554 * float4( _MainLightColor.rgb , 0.0 ) );
				#else
				float4 staticSwitch344 = _FoamColor;
				#endif
				float4 FoamColor351 = staticSwitch344;
				#ifdef _FOAM_ON
				float staticSwitch310 = clampResult306;
				#else
				float staticSwitch310 = 0.0;
				#endif
				float Foam313 = staticSwitch310;
				float4 lerpResult443 = lerp( ( ( lerpResult90 + ( lerpResult90 * SpecColor63 ) ) * _DayIntensity ) , FoamColor351 , Foam313);
				
				float WaterHeight291 = (( WorldPosition - UnderWaterPos398 )).y;
				float clampResult307 = clamp( ( WaterHeight291 / max( _ShoreDistance , 0.0 ) ) , 0.0 , 1.0 );
				float WaterOpacity312 = clampResult307;
				float clampResult430 = clamp( temp_output_429_0 , 0.0 , 1.0 );
				float ReflectFresnel431 = clampResult430;
				float clampResult328 = clamp( max( 0.0 , max( max( Foam313 , ( ReflectFresnel431 * WaterOpacity312 ) ) , (CausticsColor407).r ) ) , 0.0 , 1.0 );
				float lerpResult330 = lerp( WaterOpacity312 , 1.0 , clampResult328);
				float clampResult333 = clamp( ( lerpResult330 * _Alpha ) , 0.0 , 1.0 );
				float2 texCoord315 = IN.ase_texcoord9.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_6 = (_Size).xx;
				float clampResult331 = clamp( ( distance( max( ( abs( ( texCoord315 - float2( 0.5,0.5 ) ) ) - temp_cast_6 ) , float2( 0,0 ) ) , float2( 0,0 ) ) / _FallOff ) , 0.0 , 1.0 );
				float FinalAlpha392 = ( clampResult333 * ( 1.0 - clampResult331 ) );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = saturate( lerpResult443 ).rgb;
				float Alpha = FinalAlpha392;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(IN.clipPos, Color);
				#endif

				#if defined(_ALPHAPREMULTIPLY_ON)
				Color *= Alpha;
				#endif


				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				return half4( Color, Alpha );
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0
			AlphaToMask Off

			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_DISTANCE_TESSELLATION
			#define TESSELLATION_ON 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local _VERTEXWAVE_ON
			#pragma shader_feature_local_fragment _FOAM_ON
			#pragma shader_feature_local_fragment _CAUSTICS_ON
			#include "WavesFunction.hlsl"


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _WaveColor;
			float4 _SpecTint;
			float4 _SubWaveDirection;
			float4 _FresnelColor;
			float4 _DeepColor;
			float4 _FoamColor;
			float4 _FoamNoise3_ST;
			float4 _FoamNoise2_ST;
			float4 _ShallowColor;
			float2 _Normal_SpeedDir;
			float2 _CausticsSpeed;
			float2 _FoamNoiseSpeed;
			float2 _Noise_speed3;
			float2 _Direction;
			float _YTilling;
			float _BlinkIntensity;
			float _SpecSmoothness;
			float _SpecIntensity;
			float _Alpha;
			float _ShoreDistance;
			float _SpecEnd;
			float _SpecStart;
			float _DayIntensity;
			float _Noise_FoamD;
			float _FoamOffset;
			float _FoamRange;
			float _BlinkThreshold;
			float _XTilling;
			float _FoamColorIntensity;
			float _BlinkNoise;
			float _WaveSpeed;
			float _BlinkTilling;
			float _WaveDistance;
			float _WaveHeight;
			float _WaveNormalStr;
			float _WaveFadeStart;
			float _WaveFadeEnd;
			float _MotionVertexDisplacement;
			float _CausticsScale;
			float _CausticsIntensity;
			float _ReflectExhance;
			float _BlinkSpeed;
			float _SmallNormalSpeed;
			float _SmallNormalIntensity;
			float _LargeNormalSpeed;
			float _LargeNormalTiling;
			float _LargeNormalIntensity;
			float _WaterDeep;
			float _ReflectionAngle;
			float _FresnelIntensity;
			float _Size;
			float _WaterNoise;
			float _SmallNormalTiling;
			float _FallOff;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WaterMotionRT;
			float4 _WaterFXCamPos;
			float _WaterFXCamSize;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _FoamNoise;
			sampler2D _WaterNormalSmall;
			sampler2D _WaterNormalLarge;
			sampler2D _CausticsTex;


			float3 ReconstructWorldPos243( float2 ScreenUV, float rawdepth )
			{
				#if UNITY_REVERSED_Z
				real depth = rawdepth;
				#else
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth);
				#endif
				float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP);
				return worldPos;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (ase_worldPos).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				float3 worldToObj203 = mul( GetWorldToObjectMatrix(), float4( ( ase_worldPos + positionWSOffset548 ), 1 ) ).xyz;
				#ifdef _VERTEXWAVE_ON
				float3 staticSwitch207 = worldToObj203;
				#else
				float3 staticSwitch207 = v.vertex.xyz;
				#endif
				float4 tex2DNode457 = tex2Dlod( _WaterMotionRT, float4( ( ( ( (ase_worldPos).xz - (_WaterFXCamPos).xz ) / ( _WaterFXCamSize * 2.0 ) ) + float2( 0.5,0.5 ) ), 0, 0.0) );
				float2 temp_output_1_0_g8 = ((tex2DNode457).rg*2.0 + -1.0);
				float dotResult4_g8 = dot( temp_output_1_0_g8 , temp_output_1_0_g8 );
				float3 appendResult10_g8 = (float3((temp_output_1_0_g8).x , (temp_output_1_0_g8).y , sqrt( ( 1.0 - saturate( dotResult4_g8 ) ) )));
				float3 normalizeResult12_g8 = normalize( appendResult10_g8 );
				float3 temp_output_461_0 = normalizeResult12_g8;
				float3 MotionNormal467 = temp_output_461_0;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
				float3 tangentTobjectDir202 = mul( GetWorldToObjectMatrix(), float4( mul( ase_tangentToWorldFast, MotionNormal467 ), 0 ) ).xyz;
				float clampResult463 = clamp( tex2DNode457.a , 0.0 , 1.0 );
				float MotionPower468 = clampResult463;
				float3 WaveVertexPos209 = ( staticSwitch207 + ( tangentTobjectDir202 * MotionPower468 * 0.1 * _MotionVertexDisplacement ) );
				
				float3 vertexToFrag413 = WaveVertexPos209;
				o.ase_texcoord2.xyz = vertexToFrag413;
				o.ase_texcoord4.xyz = ase_worldTangent;
				o.ase_texcoord5.xyz = ase_worldNormal;
				o.ase_texcoord6.xyz = ase_worldBitangent;
				
				o.ase_texcoord3.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.w = 0;
				o.ase_texcoord3.zw = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				o.ase_texcoord6.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = WaveVertexPos209;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				o.clipPos = TransformWorldToHClip( positionWS );
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = o.clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float3 vertexToFrag413 = IN.ase_texcoord2.xyz;
				float4 objectToClip8_g9 = TransformObjectToHClip(vertexToFrag413);
				float3 objectToClip8_g9NDC = objectToClip8_g9.xyz/objectToClip8_g9.w;
				float4 appendResult13_g9 = (float4(objectToClip8_g9NDC , 1.0));
				float4 computeScreenPos10_g9 = ComputeScreenPos( appendResult13_g9 );
				float4 ScreenPos415 = computeScreenPos10_g9;
				float2 ScreenUV243 = ScreenPos415.xy;
				float clampDepth240 = SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPos415.xy );
				float rawdepth243 = clampDepth240;
				float3 localReconstructWorldPos243 = ReconstructWorldPos243( ScreenUV243 , rawdepth243 );
				float3 UnderWaterPos398 = localReconstructWorldPos243;
				float WaterHeight291 = (( WorldPosition - UnderWaterPos398 )).y;
				float clampResult307 = clamp( ( WaterHeight291 / max( _ShoreDistance , 0.0 ) ) , 0.0 , 1.0 );
				float WaterOpacity312 = clampResult307;
				float depthToLinear241 = LinearEyeDepth(clampDepth240,_ZBufferParams);
				float depthToLinear399 = LinearEyeDepth(ScreenPos415.z,_ZBufferParams);
				float WaterDepth244 = ( depthToLinear241 - depthToLinear399 );
				float temp_output_249_0 = ( WaterDepth244 / _FoamRange );
				float clampResult280 = clamp( ( _FoamOffset + temp_output_249_0 ) , 0.0 , 1.0 );
				float temp_output_287_0 = ( 1.0 - clampResult280 );
				float depthToLinear254 = LinearEyeDepth((0),_ZBufferParams);
				#ifdef _FOAM_ON
				float staticSwitch283 = step( 0.0 , ( depthToLinear241 - depthToLinear254 ) );
				#else
				float staticSwitch283 = 0.0;
				#endif
				float FoamMask292 = staticSwitch283;
				float2 texCoord258 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult259 = clamp( temp_output_249_0 , 0.0 , 1.0 );
				float2 appendResult276 = (float2(( _XTilling * texCoord258.x ) , ( clampResult259 * _YTilling )));
				float2 panner281 = ( 1.0 * _Time.y * _FoamNoiseSpeed + appendResult276);
				float4 tex2DNode288 = tex2D( _FoamNoise, panner281 );
				float clampResult306 = clamp( ( ( temp_output_287_0 - -1.0 ) * FoamMask292 * ( temp_output_287_0 * tex2DNode288.r ) ) , 0.0 , 1.0 );
				#ifdef _FOAM_ON
				float staticSwitch310 = clampResult306;
				#else
				float staticSwitch310 = 0.0;
				#endif
				float Foam313 = staticSwitch310;
				float clampResult425 = clamp( ( _ReflectExhance * 0.04 ) , 0.0 , 0.5 );
				float temp_output_496_0 = ( _SmallNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_579_0 = ( float2( 0.1,0.1 ) + _Normal_SpeedDir );
				float2 temp_output_545_0 = ( (WorldPosition).xz / _SmallNormalTiling );
				float2 panner499 = ( temp_output_496_0 * temp_output_579_0 + temp_output_545_0);
				float3 unpack508 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner499 ), _SmallNormalIntensity );
				unpack508.z = lerp( 1, unpack508.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_580_0 = ( float2( -0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner498 = ( temp_output_496_0 * temp_output_580_0 + ( temp_output_545_0 + 0.4 ));
				float3 unpack510 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner498 ), _SmallNormalIntensity );
				unpack510.z = lerp( 1, unpack510.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_581_0 = ( float2( -0.1,0.1 ) + _Normal_SpeedDir );
				float2 panner503 = ( temp_output_496_0 * temp_output_581_0 + ( temp_output_545_0 + float2( 10,0.15 ) ));
				float3 unpack507 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner503 ), _SmallNormalIntensity );
				unpack507.z = lerp( 1, unpack507.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_582_0 = ( float2( 0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner500 = ( temp_output_496_0 * temp_output_582_0 + ( temp_output_545_0 + float2( 0.65,0.75 ) ));
				float3 unpack509 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner500 ), _SmallNormalIntensity );
				unpack509.z = lerp( 1, unpack509.z, saturate(_SmallNormalIntensity) );
				float temp_output_495_0 = ( _LargeNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_542_0 = ( (WorldPosition).xz / _LargeNormalTiling );
				float2 panner502 = ( temp_output_495_0 * temp_output_579_0 + temp_output_542_0);
				float3 unpack505 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner502 ), _LargeNormalIntensity );
				unpack505.z = lerp( 1, unpack505.z, saturate(_LargeNormalIntensity) );
				float2 panner504 = ( temp_output_495_0 * temp_output_580_0 + ( temp_output_542_0 + 0.4 ));
				float3 unpack511 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner504 ), _LargeNormalIntensity );
				unpack511.z = lerp( 1, unpack511.z, saturate(_LargeNormalIntensity) );
				float2 panner497 = ( temp_output_495_0 * temp_output_581_0 + ( temp_output_542_0 + float2( 0.85,0.15 ) ));
				float3 unpack506 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner497 ), _LargeNormalIntensity );
				unpack506.z = lerp( 1, unpack506.z, saturate(_LargeNormalIntensity) );
				float2 panner501 = ( temp_output_495_0 * temp_output_582_0 + ( temp_output_542_0 + float2( 0.65,0.75 ) ));
				float3 unpack512 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner501 ), _LargeNormalIntensity );
				unpack512.z = lerp( 1, unpack512.z, saturate(_LargeNormalIntensity) );
				float2 temp_output_27_0 = ( (BlendNormal( BlendNormal( BlendNormal( unpack508 , unpack510 ) , BlendNormal( unpack507 , unpack509 ) ) , BlendNormal( BlendNormal( unpack505 , unpack511 ) , BlendNormal( unpack506 , unpack512 ) ) )).xy * 0.5 );
				float dotResult29 = dot( temp_output_27_0 , temp_output_27_0 );
				float3 appendResult32 = (float3(temp_output_27_0 , sqrt( ( 1.0 - dotResult29 ) )));
				float3 ase_worldTangent = IN.ase_texcoord4.xyz;
				float3 ase_worldNormal = IN.ase_texcoord5.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord6.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 tanNormal33 = appendResult32;
				float3 worldNormal33 = float3(dot(tanToWorld0,tanNormal33), dot(tanToWorld1,tanNormal33), dot(tanToWorld2,tanNormal33));
				float3 WaterNormal34 = worldNormal33;
				float3 tanNormal416 = WaterNormal34;
				float3 worldNormal416 = float3(dot(tanToWorld0,tanNormal416), dot(tanToWorld1,tanNormal416), dot(tanToWorld2,tanNormal416));
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult418 = dot( worldNormal416 , ase_worldViewDir );
				float temp_output_423_0 = ( 1.0 - max( dotResult418 , 0.0 ) );
				float lerpResult426 = lerp( clampResult425 , 1.0 , ( temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 ));
				float temp_output_429_0 = pow( lerpResult426 , _ReflectionAngle );
				float clampResult430 = clamp( temp_output_429_0 , 0.0 , 1.0 );
				float ReflectFresnel431 = clampResult430;
				float4 color309 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
				float2 temp_output_267_0 = ( (UnderWaterPos398).xz / _CausticsScale );
				float2 temp_output_272_0 = ( _CausticsSpeed * _TimeParameters.x * 0.01 );
				float FresnelFactor427 = lerpResult426;
				float clampResult253 = clamp( ( WaterDepth244 / _WaterDeep ) , 0.0 , 1.0 );
				float clampResult282 = clamp( max( FresnelFactor427 , ( FresnelFactor427 + clampResult253 ) ) , 0.0 , 1.0 );
				float WaterDepthRange294 = clampResult282;
				#ifdef _CAUSTICS_ON
				float4 staticSwitch311 = ( ( min( tex2D( _CausticsTex, ( temp_output_267_0 + temp_output_272_0 ) ) , tex2D( _CausticsTex, ( -temp_output_267_0 + temp_output_272_0 ) ) ) * _CausticsIntensity ) * ( 1.0 - WaterDepthRange294 ) );
				#else
				float4 staticSwitch311 = color309;
				#endif
				float4 CausticsColor407 = staticSwitch311;
				float clampResult328 = clamp( max( 0.0 , max( max( Foam313 , ( ReflectFresnel431 * WaterOpacity312 ) ) , (CausticsColor407).r ) ) , 0.0 , 1.0 );
				float lerpResult330 = lerp( WaterOpacity312 , 1.0 , clampResult328);
				float clampResult333 = clamp( ( lerpResult330 * _Alpha ) , 0.0 , 1.0 );
				float2 texCoord315 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_1 = (_Size).xx;
				float clampResult331 = clamp( ( distance( max( ( abs( ( texCoord315 - float2( 0.5,0.5 ) ) ) - temp_cast_1 ) , float2( 0,0 ) ) , float2( 0,0 ) ) / _FallOff ) , 0.0 , 1.0 );
				float FinalAlpha392 = ( clampResult333 * ( 1.0 - clampResult331 ) );
				
				float Alpha = FinalAlpha392;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }
			
			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_DISTANCE_TESSELLATION
			#define TESSELLATION_ON 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma shader_feature _ _SAMPLE_GI
			#pragma multi_compile _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ DEBUG_DISPLAY
			#define SHADERPASS SHADERPASS_UNLIT


			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"


			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local _VERTEXWAVE_ON
			#pragma shader_feature_local_fragment _CAUSTICS_ON
			#pragma shader_feature_local_fragment _FOAM_ON
			#include "WavesFunction.hlsl"


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef ASE_FOG
				float fogFactor : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_texcoord9 : TEXCOORD9;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _WaveColor;
			float4 _SpecTint;
			float4 _SubWaveDirection;
			float4 _FresnelColor;
			float4 _DeepColor;
			float4 _FoamColor;
			float4 _FoamNoise3_ST;
			float4 _FoamNoise2_ST;
			float4 _ShallowColor;
			float2 _Normal_SpeedDir;
			float2 _CausticsSpeed;
			float2 _FoamNoiseSpeed;
			float2 _Noise_speed3;
			float2 _Direction;
			float _YTilling;
			float _BlinkIntensity;
			float _SpecSmoothness;
			float _SpecIntensity;
			float _Alpha;
			float _ShoreDistance;
			float _SpecEnd;
			float _SpecStart;
			float _DayIntensity;
			float _Noise_FoamD;
			float _FoamOffset;
			float _FoamRange;
			float _BlinkThreshold;
			float _XTilling;
			float _FoamColorIntensity;
			float _BlinkNoise;
			float _WaveSpeed;
			float _BlinkTilling;
			float _WaveDistance;
			float _WaveHeight;
			float _WaveNormalStr;
			float _WaveFadeStart;
			float _WaveFadeEnd;
			float _MotionVertexDisplacement;
			float _CausticsScale;
			float _CausticsIntensity;
			float _ReflectExhance;
			float _BlinkSpeed;
			float _SmallNormalSpeed;
			float _SmallNormalIntensity;
			float _LargeNormalSpeed;
			float _LargeNormalTiling;
			float _LargeNormalIntensity;
			float _WaterDeep;
			float _ReflectionAngle;
			float _FresnelIntensity;
			float _Size;
			float _WaterNoise;
			float _SmallNormalTiling;
			float _FallOff;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WaterMotionRT;
			float4 _WaterFXCamPos;
			float _WaterFXCamSize;
			sampler2D _CausticsTex;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _WaterNormalSmall;
			sampler2D _WaterNormalLarge;
			sampler2D _PlanarReflectionTexture;
			sampler2D _FoamNoise;
			sampler2D _FoamNoise2;
			sampler2D _FoamNoise3;


			float3 ReconstructWorldPos243( float2 ScreenUV, float rawdepth )
			{
				#if UNITY_REVERSED_Z
				real depth = rawdepth;
				#else
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth);
				#endif
				float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP);
				return worldPos;
			}
			
			
			VertexOutput VertexFunction ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (ase_worldPos).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				float3 worldToObj203 = mul( GetWorldToObjectMatrix(), float4( ( ase_worldPos + positionWSOffset548 ), 1 ) ).xyz;
				#ifdef _VERTEXWAVE_ON
				float3 staticSwitch207 = worldToObj203;
				#else
				float3 staticSwitch207 = v.vertex.xyz;
				#endif
				float4 tex2DNode457 = tex2Dlod( _WaterMotionRT, float4( ( ( ( (ase_worldPos).xz - (_WaterFXCamPos).xz ) / ( _WaterFXCamSize * 2.0 ) ) + float2( 0.5,0.5 ) ), 0, 0.0) );
				float2 temp_output_1_0_g8 = ((tex2DNode457).rg*2.0 + -1.0);
				float dotResult4_g8 = dot( temp_output_1_0_g8 , temp_output_1_0_g8 );
				float3 appendResult10_g8 = (float3((temp_output_1_0_g8).x , (temp_output_1_0_g8).y , sqrt( ( 1.0 - saturate( dotResult4_g8 ) ) )));
				float3 normalizeResult12_g8 = normalize( appendResult10_g8 );
				float3 temp_output_461_0 = normalizeResult12_g8;
				float3 MotionNormal467 = temp_output_461_0;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
				float3 tangentTobjectDir202 = mul( GetWorldToObjectMatrix(), float4( mul( ase_tangentToWorldFast, MotionNormal467 ), 0 ) ).xyz;
				float clampResult463 = clamp( tex2DNode457.a , 0.0 , 1.0 );
				float MotionPower468 = clampResult463;
				float3 WaveVertexPos209 = ( staticSwitch207 + ( tangentTobjectDir202 * MotionPower468 * 0.1 * _MotionVertexDisplacement ) );
				
				float3 vertexToFrag413 = WaveVertexPos209;
				o.ase_texcoord3.xyz = vertexToFrag413;
				o.ase_texcoord4.xyz = ase_worldTangent;
				o.ase_texcoord5.xyz = ase_worldNormal;
				o.ase_texcoord6.xyz = ase_worldBitangent;
				float4 ase_clipPos = TransformObjectToHClip((v.vertex).xyz);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord7 = screenPos;
				
				o.ase_texcoord8 = v.vertex;
				o.ase_texcoord9.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord3.w = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				o.ase_texcoord6.w = 0;
				o.ase_texcoord9.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = WaveVertexPos209;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				#ifdef ASE_FOG
				o.fogFactor = ComputeFogFactor( positionCS.z );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif
				float4 color309 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
				float3 vertexToFrag413 = IN.ase_texcoord3.xyz;
				float4 objectToClip8_g9 = TransformObjectToHClip(vertexToFrag413);
				float3 objectToClip8_g9NDC = objectToClip8_g9.xyz/objectToClip8_g9.w;
				float4 appendResult13_g9 = (float4(objectToClip8_g9NDC , 1.0));
				float4 computeScreenPos10_g9 = ComputeScreenPos( appendResult13_g9 );
				float4 ScreenPos415 = computeScreenPos10_g9;
				float2 ScreenUV243 = ScreenPos415.xy;
				float clampDepth240 = SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPos415.xy );
				float rawdepth243 = clampDepth240;
				float3 localReconstructWorldPos243 = ReconstructWorldPos243( ScreenUV243 , rawdepth243 );
				float3 UnderWaterPos398 = localReconstructWorldPos243;
				float2 temp_output_267_0 = ( (UnderWaterPos398).xz / _CausticsScale );
				float2 temp_output_272_0 = ( _CausticsSpeed * _TimeParameters.x * 0.01 );
				float clampResult425 = clamp( ( _ReflectExhance * 0.04 ) , 0.0 , 0.5 );
				float temp_output_496_0 = ( _SmallNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_579_0 = ( float2( 0.1,0.1 ) + _Normal_SpeedDir );
				float2 temp_output_545_0 = ( (WorldPosition).xz / _SmallNormalTiling );
				float2 panner499 = ( temp_output_496_0 * temp_output_579_0 + temp_output_545_0);
				float3 unpack508 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner499 ), _SmallNormalIntensity );
				unpack508.z = lerp( 1, unpack508.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_580_0 = ( float2( -0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner498 = ( temp_output_496_0 * temp_output_580_0 + ( temp_output_545_0 + 0.4 ));
				float3 unpack510 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner498 ), _SmallNormalIntensity );
				unpack510.z = lerp( 1, unpack510.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_581_0 = ( float2( -0.1,0.1 ) + _Normal_SpeedDir );
				float2 panner503 = ( temp_output_496_0 * temp_output_581_0 + ( temp_output_545_0 + float2( 10,0.15 ) ));
				float3 unpack507 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner503 ), _SmallNormalIntensity );
				unpack507.z = lerp( 1, unpack507.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_582_0 = ( float2( 0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner500 = ( temp_output_496_0 * temp_output_582_0 + ( temp_output_545_0 + float2( 0.65,0.75 ) ));
				float3 unpack509 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner500 ), _SmallNormalIntensity );
				unpack509.z = lerp( 1, unpack509.z, saturate(_SmallNormalIntensity) );
				float temp_output_495_0 = ( _LargeNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_542_0 = ( (WorldPosition).xz / _LargeNormalTiling );
				float2 panner502 = ( temp_output_495_0 * temp_output_579_0 + temp_output_542_0);
				float3 unpack505 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner502 ), _LargeNormalIntensity );
				unpack505.z = lerp( 1, unpack505.z, saturate(_LargeNormalIntensity) );
				float2 panner504 = ( temp_output_495_0 * temp_output_580_0 + ( temp_output_542_0 + 0.4 ));
				float3 unpack511 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner504 ), _LargeNormalIntensity );
				unpack511.z = lerp( 1, unpack511.z, saturate(_LargeNormalIntensity) );
				float2 panner497 = ( temp_output_495_0 * temp_output_581_0 + ( temp_output_542_0 + float2( 0.85,0.15 ) ));
				float3 unpack506 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner497 ), _LargeNormalIntensity );
				unpack506.z = lerp( 1, unpack506.z, saturate(_LargeNormalIntensity) );
				float2 panner501 = ( temp_output_495_0 * temp_output_582_0 + ( temp_output_542_0 + float2( 0.65,0.75 ) ));
				float3 unpack512 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner501 ), _LargeNormalIntensity );
				unpack512.z = lerp( 1, unpack512.z, saturate(_LargeNormalIntensity) );
				float2 temp_output_27_0 = ( (BlendNormal( BlendNormal( BlendNormal( unpack508 , unpack510 ) , BlendNormal( unpack507 , unpack509 ) ) , BlendNormal( BlendNormal( unpack505 , unpack511 ) , BlendNormal( unpack506 , unpack512 ) ) )).xy * 0.5 );
				float dotResult29 = dot( temp_output_27_0 , temp_output_27_0 );
				float3 appendResult32 = (float3(temp_output_27_0 , sqrt( ( 1.0 - dotResult29 ) )));
				float3 ase_worldTangent = IN.ase_texcoord4.xyz;
				float3 ase_worldNormal = IN.ase_texcoord5.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord6.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 tanNormal33 = appendResult32;
				float3 worldNormal33 = float3(dot(tanToWorld0,tanNormal33), dot(tanToWorld1,tanNormal33), dot(tanToWorld2,tanNormal33));
				float3 WaterNormal34 = worldNormal33;
				float3 tanNormal416 = WaterNormal34;
				float3 worldNormal416 = float3(dot(tanToWorld0,tanNormal416), dot(tanToWorld1,tanNormal416), dot(tanToWorld2,tanNormal416));
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult418 = dot( worldNormal416 , ase_worldViewDir );
				float temp_output_423_0 = ( 1.0 - max( dotResult418 , 0.0 ) );
				float lerpResult426 = lerp( clampResult425 , 1.0 , ( temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 ));
				float FresnelFactor427 = lerpResult426;
				float depthToLinear241 = LinearEyeDepth(clampDepth240,_ZBufferParams);
				float depthToLinear399 = LinearEyeDepth(ScreenPos415.z,_ZBufferParams);
				float WaterDepth244 = ( depthToLinear241 - depthToLinear399 );
				float clampResult253 = clamp( ( WaterDepth244 / _WaterDeep ) , 0.0 , 1.0 );
				float clampResult282 = clamp( max( FresnelFactor427 , ( FresnelFactor427 + clampResult253 ) ) , 0.0 , 1.0 );
				float WaterDepthRange294 = clampResult282;
				#ifdef _CAUSTICS_ON
				float4 staticSwitch311 = ( ( min( tex2D( _CausticsTex, ( temp_output_267_0 + temp_output_272_0 ) ) , tex2D( _CausticsTex, ( -temp_output_267_0 + temp_output_272_0 ) ) ) * _CausticsIntensity ) * ( 1.0 - WaterDepthRange294 ) );
				#else
				float4 staticSwitch311 = color309;
				#endif
				float4 CausticsColor407 = staticSwitch311;
				float temp_output_429_0 = pow( lerpResult426 , _ReflectionAngle );
				float temp_output_435_0 = ( temp_output_429_0 * _FresnelIntensity * 5.0 );
				float clampResult437 = clamp( ( temp_output_435_0 * temp_output_435_0 ) , 0.0 , 1.0 );
				float ColorFresnel433 = clampResult437;
				float4 lerpResult357 = lerp( _DeepColor , _FresnelColor , ColorFresnel433);
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (WorldPosition).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				#ifdef _FOAM_ON
				float staticSwitch219 = (positionWSOffset548).y;
				#else
				float staticSwitch219 = 0.0;
				#endif
				float WaveY220 = staticSwitch219;
				float clampResult345 = clamp( WaveY220 , 0.0 , 1.0 );
				float4 lerpResult372 = lerp( lerpResult357 , _WaveColor , clampResult345);
				float4 lerpResult339 = lerp( ( _ShallowColor + CausticsColor407 ) , lerpResult372 , clampResult282);
				float dotResult557 = dot( _MainLightPosition.xyz , ase_worldNormal );
				float4 WaterColor360 = ( lerpResult339 * ( ( dotResult557 * 0.5 ) + 0.5 ) * float4( _MainLightColor.rgb , 0.0 ) );
				float4 screenPos = IN.ase_texcoord7;
				float4 ase_screenPosNorm = screenPos / screenPos.w;
				ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
				float4 unityObjectToClipPos40 = TransformWorldToHClip(TransformObjectToWorld(IN.ase_texcoord8.xyz));
				float2 temp_output_115_0 = ( (WorldPosition).xz / _BlinkTilling );
				float temp_output_113_0 = ( _TimeParameters.x * 0.1 * _BlinkSpeed );
				float2 temp_output_126_0 = ( (( UnpackNormalScale( tex2D( _WaterNormalSmall, ( temp_output_115_0 + temp_output_113_0 ) ), 1.0f ) + UnpackNormalScale( tex2D( _WaterNormalSmall, ( ( temp_output_115_0 * 1.5 ) + ( temp_output_113_0 * -1.0 ) ) ), 1.0f ) )).xy * 0.5 );
				float dotResult127 = dot( temp_output_126_0 , temp_output_126_0 );
				float3 appendResult130 = (float3(temp_output_126_0 , sqrt( ( 1.0 - dotResult127 ) )));
				float3 tanNormal131 = appendResult130;
				float3 worldNormal131 = float3(dot(tanToWorld0,tanNormal131), dot(tanToWorld1,tanNormal131), dot(tanToWorld2,tanNormal131));
				float3 WaterNormalBlink132 = worldNormal131;
				float4 temp_cast_2 = (_BlinkThreshold).xxxx;
				float4 ReflectBlink147 = ( max( ( tex2D( _PlanarReflectionTexture, ( (ase_screenPosNorm).xy + ( (WaterNormalBlink132).xz * _BlinkNoise ) ) ) - temp_cast_2 ) , float4( 0,0,0,0 ) ) * _BlinkIntensity );
				float4 ReflectColor44 = ( tex2D( _PlanarReflectionTexture, ( (ase_screenPosNorm).xy + ( ( (WaterNormal34).xz / ( 1.0 + unityObjectToClipPos40.w ) ) * _WaterNoise ) ) ) + ReflectBlink147 );
				float dotResult86 = dot( ase_worldNormal , ase_worldViewDir );
				float clampResult88 = clamp( dotResult86 , 0.0 , 1.0 );
				float temp_output_89_0 = ( 1.0 - clampResult88 );
				float clampResult103 = clamp( ( temp_output_89_0 + 0.2 ) , 0.0 , 1.0 );
				float4 lerpResult90 = lerp( WaterColor360 , ( ReflectColor44 * clampResult103 ) , temp_output_89_0);
				float3 normalizeResult51 = normalize( ( ase_worldViewDir + _MainLightPosition.xyz ) );
				float dotResult52 = dot( WaterNormal34 , normalizeResult51 );
				float clampResult75 = clamp( ( ( _SpecEnd - distance( WorldPosition , _WorldSpaceCameraPos ) ) / ( _SpecEnd - _SpecStart ) ) , 0.0 , 1.0 );
				float4 SpecColor63 = ( ( ( pow( max( dotResult52 , 0.0 ) , ( _SpecSmoothness * 256.0 ) ) * _SpecTint ) * _SpecIntensity ) * clampResult75 );
				float temp_output_249_0 = ( WaterDepth244 / _FoamRange );
				float clampResult280 = clamp( ( _FoamOffset + temp_output_249_0 ) , 0.0 , 1.0 );
				float temp_output_287_0 = ( 1.0 - clampResult280 );
				float depthToLinear254 = LinearEyeDepth((0),_ZBufferParams);
				#ifdef _FOAM_ON
				float staticSwitch283 = step( 0.0 , ( depthToLinear241 - depthToLinear254 ) );
				#else
				float staticSwitch283 = 0.0;
				#endif
				float FoamMask292 = staticSwitch283;
				float2 texCoord258 = IN.ase_texcoord9.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult259 = clamp( temp_output_249_0 , 0.0 , 1.0 );
				float2 appendResult276 = (float2(( _XTilling * texCoord258.x ) , ( clampResult259 * _YTilling )));
				float2 panner281 = ( 1.0 * _Time.y * _FoamNoiseSpeed + appendResult276);
				float4 tex2DNode288 = tex2D( _FoamNoise, panner281 );
				float clampResult306 = clamp( ( ( temp_output_287_0 - -1.0 ) * FoamMask292 * ( temp_output_287_0 * tex2DNode288.r ) ) , 0.0 , 1.0 );
				float2 uv_FoamNoise2 = IN.ase_texcoord9.xy * _FoamNoise2_ST.xy + _FoamNoise2_ST.zw;
				float2 uv_FoamNoise3 = IN.ase_texcoord9.xy * _FoamNoise3_ST.xy + _FoamNoise3_ST.zw;
				float2 panner336 = ( 1.0 * _Time.y * _Noise_speed3 + tex2D( _FoamNoise3, uv_FoamNoise3 ).rg);
				float2 lerpResult349 = lerp( uv_FoamNoise2 , panner336 , _Noise_FoamD);
				float dotResult554 = dot( _MainLightPosition.xyz , ase_worldNormal );
				#ifdef _FOAM_ON
				float4 staticSwitch344 = ( clampResult306 * _FoamColor * _FoamColorIntensity * tex2D( _FoamNoise2, lerpResult349 ) * dotResult554 * float4( _MainLightColor.rgb , 0.0 ) );
				#else
				float4 staticSwitch344 = _FoamColor;
				#endif
				float4 FoamColor351 = staticSwitch344;
				#ifdef _FOAM_ON
				float staticSwitch310 = clampResult306;
				#else
				float staticSwitch310 = 0.0;
				#endif
				float Foam313 = staticSwitch310;
				float4 lerpResult443 = lerp( ( ( lerpResult90 + ( lerpResult90 * SpecColor63 ) ) * _DayIntensity ) , FoamColor351 , Foam313);
				
				float WaterHeight291 = (( WorldPosition - UnderWaterPos398 )).y;
				float clampResult307 = clamp( ( WaterHeight291 / max( _ShoreDistance , 0.0 ) ) , 0.0 , 1.0 );
				float WaterOpacity312 = clampResult307;
				float clampResult430 = clamp( temp_output_429_0 , 0.0 , 1.0 );
				float ReflectFresnel431 = clampResult430;
				float clampResult328 = clamp( max( 0.0 , max( max( Foam313 , ( ReflectFresnel431 * WaterOpacity312 ) ) , (CausticsColor407).r ) ) , 0.0 , 1.0 );
				float lerpResult330 = lerp( WaterOpacity312 , 1.0 , clampResult328);
				float clampResult333 = clamp( ( lerpResult330 * _Alpha ) , 0.0 , 1.0 );
				float2 texCoord315 = IN.ase_texcoord9.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_6 = (_Size).xx;
				float clampResult331 = clamp( ( distance( max( ( abs( ( texCoord315 - float2( 0.5,0.5 ) ) ) - temp_cast_6 ) , float2( 0,0 ) ) , float2( 0,0 ) ) / _FallOff ) , 0.0 , 1.0 );
				float FinalAlpha392 = ( clampResult333 * ( 1.0 - clampResult331 ) );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = saturate( lerpResult443 ).rgb;
				float Alpha = FinalAlpha392;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(IN.clipPos, Color);
				#endif

				#if defined(_ALPHAPREMULTIPLY_ON)
				Color *= Alpha;
				#endif


				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, IN.fogFactor );
				#endif

				return half4( Color, Alpha );
			}

			ENDHLSL
		}


		
        Pass
        {
			
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }
        
			Cull Off

			HLSLPROGRAM
        
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_DISTANCE_TESSELLATION
			#define TESSELLATION_ON 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

        
			#pragma only_renderers d3d11 glcore gles gles3 
			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _VERTEXWAVE_ON
			#pragma shader_feature_local_fragment _FOAM_ON
			#pragma shader_feature_local_fragment _CAUSTICS_ON
			#include "WavesFunction.hlsl"


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _WaveColor;
			float4 _SpecTint;
			float4 _SubWaveDirection;
			float4 _FresnelColor;
			float4 _DeepColor;
			float4 _FoamColor;
			float4 _FoamNoise3_ST;
			float4 _FoamNoise2_ST;
			float4 _ShallowColor;
			float2 _Normal_SpeedDir;
			float2 _CausticsSpeed;
			float2 _FoamNoiseSpeed;
			float2 _Noise_speed3;
			float2 _Direction;
			float _YTilling;
			float _BlinkIntensity;
			float _SpecSmoothness;
			float _SpecIntensity;
			float _Alpha;
			float _ShoreDistance;
			float _SpecEnd;
			float _SpecStart;
			float _DayIntensity;
			float _Noise_FoamD;
			float _FoamOffset;
			float _FoamRange;
			float _BlinkThreshold;
			float _XTilling;
			float _FoamColorIntensity;
			float _BlinkNoise;
			float _WaveSpeed;
			float _BlinkTilling;
			float _WaveDistance;
			float _WaveHeight;
			float _WaveNormalStr;
			float _WaveFadeStart;
			float _WaveFadeEnd;
			float _MotionVertexDisplacement;
			float _CausticsScale;
			float _CausticsIntensity;
			float _ReflectExhance;
			float _BlinkSpeed;
			float _SmallNormalSpeed;
			float _SmallNormalIntensity;
			float _LargeNormalSpeed;
			float _LargeNormalTiling;
			float _LargeNormalIntensity;
			float _WaterDeep;
			float _ReflectionAngle;
			float _FresnelIntensity;
			float _Size;
			float _WaterNoise;
			float _SmallNormalTiling;
			float _FallOff;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			sampler2D _WaterMotionRT;
			float4 _WaterFXCamPos;
			float _WaterFXCamSize;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _FoamNoise;
			sampler2D _WaterNormalSmall;
			sampler2D _WaterNormalLarge;
			sampler2D _CausticsTex;


			float3 ReconstructWorldPos243( float2 ScreenUV, float rawdepth )
			{
				#if UNITY_REVERSED_Z
				real depth = rawdepth;
				#else
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth);
				#endif
				float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP);
				return worldPos;
			}
			

			int _ObjectId;
			int _PassValue;

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
        
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);


				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (ase_worldPos).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				float3 worldToObj203 = mul( GetWorldToObjectMatrix(), float4( ( ase_worldPos + positionWSOffset548 ), 1 ) ).xyz;
				#ifdef _VERTEXWAVE_ON
				float3 staticSwitch207 = worldToObj203;
				#else
				float3 staticSwitch207 = v.vertex.xyz;
				#endif
				float4 tex2DNode457 = tex2Dlod( _WaterMotionRT, float4( ( ( ( (ase_worldPos).xz - (_WaterFXCamPos).xz ) / ( _WaterFXCamSize * 2.0 ) ) + float2( 0.5,0.5 ) ), 0, 0.0) );
				float2 temp_output_1_0_g8 = ((tex2DNode457).rg*2.0 + -1.0);
				float dotResult4_g8 = dot( temp_output_1_0_g8 , temp_output_1_0_g8 );
				float3 appendResult10_g8 = (float3((temp_output_1_0_g8).x , (temp_output_1_0_g8).y , sqrt( ( 1.0 - saturate( dotResult4_g8 ) ) )));
				float3 normalizeResult12_g8 = normalize( appendResult10_g8 );
				float3 temp_output_461_0 = normalizeResult12_g8;
				float3 MotionNormal467 = temp_output_461_0;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
				float3 tangentTobjectDir202 = mul( GetWorldToObjectMatrix(), float4( mul( ase_tangentToWorldFast, MotionNormal467 ), 0 ) ).xyz;
				float clampResult463 = clamp( tex2DNode457.a , 0.0 , 1.0 );
				float MotionPower468 = clampResult463;
				float3 WaveVertexPos209 = ( staticSwitch207 + ( tangentTobjectDir202 * MotionPower468 * 0.1 * _MotionVertexDisplacement ) );
				
				o.ase_texcoord.xyz = ase_worldPos;
				float3 vertexToFrag413 = WaveVertexPos209;
				o.ase_texcoord1.xyz = vertexToFrag413;
				o.ase_texcoord3.xyz = ase_worldTangent;
				o.ase_texcoord4.xyz = ase_worldNormal;
				o.ase_texcoord5.xyz = ase_worldBitangent;
				
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord.w = 0;
				o.ase_texcoord1.w = 0;
				o.ase_texcoord2.zw = 0;
				o.ase_texcoord3.w = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = WaveVertexPos209;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				o.clipPos = TransformWorldToHClip(positionWS);
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif
			
			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float3 ase_worldPos = IN.ase_texcoord.xyz;
				float3 vertexToFrag413 = IN.ase_texcoord1.xyz;
				float4 objectToClip8_g9 = TransformObjectToHClip(vertexToFrag413);
				float3 objectToClip8_g9NDC = objectToClip8_g9.xyz/objectToClip8_g9.w;
				float4 appendResult13_g9 = (float4(objectToClip8_g9NDC , 1.0));
				float4 computeScreenPos10_g9 = ComputeScreenPos( appendResult13_g9 );
				float4 ScreenPos415 = computeScreenPos10_g9;
				float2 ScreenUV243 = ScreenPos415.xy;
				float clampDepth240 = SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPos415.xy );
				float rawdepth243 = clampDepth240;
				float3 localReconstructWorldPos243 = ReconstructWorldPos243( ScreenUV243 , rawdepth243 );
				float3 UnderWaterPos398 = localReconstructWorldPos243;
				float WaterHeight291 = (( ase_worldPos - UnderWaterPos398 )).y;
				float clampResult307 = clamp( ( WaterHeight291 / max( _ShoreDistance , 0.0 ) ) , 0.0 , 1.0 );
				float WaterOpacity312 = clampResult307;
				float depthToLinear241 = LinearEyeDepth(clampDepth240,_ZBufferParams);
				float depthToLinear399 = LinearEyeDepth(ScreenPos415.z,_ZBufferParams);
				float WaterDepth244 = ( depthToLinear241 - depthToLinear399 );
				float temp_output_249_0 = ( WaterDepth244 / _FoamRange );
				float clampResult280 = clamp( ( _FoamOffset + temp_output_249_0 ) , 0.0 , 1.0 );
				float temp_output_287_0 = ( 1.0 - clampResult280 );
				float depthToLinear254 = LinearEyeDepth((0),_ZBufferParams);
				#ifdef _FOAM_ON
				float staticSwitch283 = step( 0.0 , ( depthToLinear241 - depthToLinear254 ) );
				#else
				float staticSwitch283 = 0.0;
				#endif
				float FoamMask292 = staticSwitch283;
				float2 texCoord258 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult259 = clamp( temp_output_249_0 , 0.0 , 1.0 );
				float2 appendResult276 = (float2(( _XTilling * texCoord258.x ) , ( clampResult259 * _YTilling )));
				float2 panner281 = ( 1.0 * _Time.y * _FoamNoiseSpeed + appendResult276);
				float4 tex2DNode288 = tex2D( _FoamNoise, panner281 );
				float clampResult306 = clamp( ( ( temp_output_287_0 - -1.0 ) * FoamMask292 * ( temp_output_287_0 * tex2DNode288.r ) ) , 0.0 , 1.0 );
				#ifdef _FOAM_ON
				float staticSwitch310 = clampResult306;
				#else
				float staticSwitch310 = 0.0;
				#endif
				float Foam313 = staticSwitch310;
				float clampResult425 = clamp( ( _ReflectExhance * 0.04 ) , 0.0 , 0.5 );
				float temp_output_496_0 = ( _SmallNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_579_0 = ( float2( 0.1,0.1 ) + _Normal_SpeedDir );
				float2 temp_output_545_0 = ( (ase_worldPos).xz / _SmallNormalTiling );
				float2 panner499 = ( temp_output_496_0 * temp_output_579_0 + temp_output_545_0);
				float3 unpack508 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner499 ), _SmallNormalIntensity );
				unpack508.z = lerp( 1, unpack508.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_580_0 = ( float2( -0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner498 = ( temp_output_496_0 * temp_output_580_0 + ( temp_output_545_0 + 0.4 ));
				float3 unpack510 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner498 ), _SmallNormalIntensity );
				unpack510.z = lerp( 1, unpack510.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_581_0 = ( float2( -0.1,0.1 ) + _Normal_SpeedDir );
				float2 panner503 = ( temp_output_496_0 * temp_output_581_0 + ( temp_output_545_0 + float2( 10,0.15 ) ));
				float3 unpack507 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner503 ), _SmallNormalIntensity );
				unpack507.z = lerp( 1, unpack507.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_582_0 = ( float2( 0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner500 = ( temp_output_496_0 * temp_output_582_0 + ( temp_output_545_0 + float2( 0.65,0.75 ) ));
				float3 unpack509 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner500 ), _SmallNormalIntensity );
				unpack509.z = lerp( 1, unpack509.z, saturate(_SmallNormalIntensity) );
				float temp_output_495_0 = ( _LargeNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_542_0 = ( (ase_worldPos).xz / _LargeNormalTiling );
				float2 panner502 = ( temp_output_495_0 * temp_output_579_0 + temp_output_542_0);
				float3 unpack505 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner502 ), _LargeNormalIntensity );
				unpack505.z = lerp( 1, unpack505.z, saturate(_LargeNormalIntensity) );
				float2 panner504 = ( temp_output_495_0 * temp_output_580_0 + ( temp_output_542_0 + 0.4 ));
				float3 unpack511 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner504 ), _LargeNormalIntensity );
				unpack511.z = lerp( 1, unpack511.z, saturate(_LargeNormalIntensity) );
				float2 panner497 = ( temp_output_495_0 * temp_output_581_0 + ( temp_output_542_0 + float2( 0.85,0.15 ) ));
				float3 unpack506 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner497 ), _LargeNormalIntensity );
				unpack506.z = lerp( 1, unpack506.z, saturate(_LargeNormalIntensity) );
				float2 panner501 = ( temp_output_495_0 * temp_output_582_0 + ( temp_output_542_0 + float2( 0.65,0.75 ) ));
				float3 unpack512 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner501 ), _LargeNormalIntensity );
				unpack512.z = lerp( 1, unpack512.z, saturate(_LargeNormalIntensity) );
				float2 temp_output_27_0 = ( (BlendNormal( BlendNormal( BlendNormal( unpack508 , unpack510 ) , BlendNormal( unpack507 , unpack509 ) ) , BlendNormal( BlendNormal( unpack505 , unpack511 ) , BlendNormal( unpack506 , unpack512 ) ) )).xy * 0.5 );
				float dotResult29 = dot( temp_output_27_0 , temp_output_27_0 );
				float3 appendResult32 = (float3(temp_output_27_0 , sqrt( ( 1.0 - dotResult29 ) )));
				float3 ase_worldTangent = IN.ase_texcoord3.xyz;
				float3 ase_worldNormal = IN.ase_texcoord4.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord5.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 tanNormal33 = appendResult32;
				float3 worldNormal33 = float3(dot(tanToWorld0,tanNormal33), dot(tanToWorld1,tanNormal33), dot(tanToWorld2,tanNormal33));
				float3 WaterNormal34 = worldNormal33;
				float3 tanNormal416 = WaterNormal34;
				float3 worldNormal416 = float3(dot(tanToWorld0,tanNormal416), dot(tanToWorld1,tanNormal416), dot(tanToWorld2,tanNormal416));
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult418 = dot( worldNormal416 , ase_worldViewDir );
				float temp_output_423_0 = ( 1.0 - max( dotResult418 , 0.0 ) );
				float lerpResult426 = lerp( clampResult425 , 1.0 , ( temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 ));
				float temp_output_429_0 = pow( lerpResult426 , _ReflectionAngle );
				float clampResult430 = clamp( temp_output_429_0 , 0.0 , 1.0 );
				float ReflectFresnel431 = clampResult430;
				float4 color309 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
				float2 temp_output_267_0 = ( (UnderWaterPos398).xz / _CausticsScale );
				float2 temp_output_272_0 = ( _CausticsSpeed * _TimeParameters.x * 0.01 );
				float FresnelFactor427 = lerpResult426;
				float clampResult253 = clamp( ( WaterDepth244 / _WaterDeep ) , 0.0 , 1.0 );
				float clampResult282 = clamp( max( FresnelFactor427 , ( FresnelFactor427 + clampResult253 ) ) , 0.0 , 1.0 );
				float WaterDepthRange294 = clampResult282;
				#ifdef _CAUSTICS_ON
				float4 staticSwitch311 = ( ( min( tex2D( _CausticsTex, ( temp_output_267_0 + temp_output_272_0 ) ) , tex2D( _CausticsTex, ( -temp_output_267_0 + temp_output_272_0 ) ) ) * _CausticsIntensity ) * ( 1.0 - WaterDepthRange294 ) );
				#else
				float4 staticSwitch311 = color309;
				#endif
				float4 CausticsColor407 = staticSwitch311;
				float clampResult328 = clamp( max( 0.0 , max( max( Foam313 , ( ReflectFresnel431 * WaterOpacity312 ) ) , (CausticsColor407).r ) ) , 0.0 , 1.0 );
				float lerpResult330 = lerp( WaterOpacity312 , 1.0 , clampResult328);
				float clampResult333 = clamp( ( lerpResult330 * _Alpha ) , 0.0 , 1.0 );
				float2 texCoord315 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_1 = (_Size).xx;
				float clampResult331 = clamp( ( distance( max( ( abs( ( texCoord315 - float2( 0.5,0.5 ) ) ) - temp_cast_1 ) , float2( 0,0 ) ) , float2( 0,0 ) ) / _FallOff ) , 0.0 , 1.0 );
				float FinalAlpha392 = ( clampResult333 * ( 1.0 - clampResult331 ) );
				
				surfaceDescription.Alpha = FinalAlpha392;
				surfaceDescription.AlphaClipThreshold = 0.5;


				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				return outColor;
			}

			ENDHLSL
        }

		
        Pass
        {
			
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }
        
			HLSLPROGRAM

			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_DISTANCE_TESSELLATION
			#define TESSELLATION_ON 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma only_renderers d3d11 glcore gles gles3 
			#pragma vertex vert
			#pragma fragment frag

        
			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY
			

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _VERTEXWAVE_ON
			#pragma shader_feature_local_fragment _FOAM_ON
			#pragma shader_feature_local_fragment _CAUSTICS_ON
			#include "WavesFunction.hlsl"


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _WaveColor;
			float4 _SpecTint;
			float4 _SubWaveDirection;
			float4 _FresnelColor;
			float4 _DeepColor;
			float4 _FoamColor;
			float4 _FoamNoise3_ST;
			float4 _FoamNoise2_ST;
			float4 _ShallowColor;
			float2 _Normal_SpeedDir;
			float2 _CausticsSpeed;
			float2 _FoamNoiseSpeed;
			float2 _Noise_speed3;
			float2 _Direction;
			float _YTilling;
			float _BlinkIntensity;
			float _SpecSmoothness;
			float _SpecIntensity;
			float _Alpha;
			float _ShoreDistance;
			float _SpecEnd;
			float _SpecStart;
			float _DayIntensity;
			float _Noise_FoamD;
			float _FoamOffset;
			float _FoamRange;
			float _BlinkThreshold;
			float _XTilling;
			float _FoamColorIntensity;
			float _BlinkNoise;
			float _WaveSpeed;
			float _BlinkTilling;
			float _WaveDistance;
			float _WaveHeight;
			float _WaveNormalStr;
			float _WaveFadeStart;
			float _WaveFadeEnd;
			float _MotionVertexDisplacement;
			float _CausticsScale;
			float _CausticsIntensity;
			float _ReflectExhance;
			float _BlinkSpeed;
			float _SmallNormalSpeed;
			float _SmallNormalIntensity;
			float _LargeNormalSpeed;
			float _LargeNormalTiling;
			float _LargeNormalIntensity;
			float _WaterDeep;
			float _ReflectionAngle;
			float _FresnelIntensity;
			float _Size;
			float _WaterNoise;
			float _SmallNormalTiling;
			float _FallOff;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			sampler2D _WaterMotionRT;
			float4 _WaterFXCamPos;
			float _WaterFXCamSize;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _FoamNoise;
			sampler2D _WaterNormalSmall;
			sampler2D _WaterNormalLarge;
			sampler2D _CausticsTex;


			float3 ReconstructWorldPos243( float2 ScreenUV, float rawdepth )
			{
				#if UNITY_REVERSED_Z
				real depth = rawdepth;
				#else
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth);
				#endif
				float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP);
				return worldPos;
			}
			

        
			float4 _SelectionID;

        
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
        
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);


				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (ase_worldPos).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				float3 worldToObj203 = mul( GetWorldToObjectMatrix(), float4( ( ase_worldPos + positionWSOffset548 ), 1 ) ).xyz;
				#ifdef _VERTEXWAVE_ON
				float3 staticSwitch207 = worldToObj203;
				#else
				float3 staticSwitch207 = v.vertex.xyz;
				#endif
				float4 tex2DNode457 = tex2Dlod( _WaterMotionRT, float4( ( ( ( (ase_worldPos).xz - (_WaterFXCamPos).xz ) / ( _WaterFXCamSize * 2.0 ) ) + float2( 0.5,0.5 ) ), 0, 0.0) );
				float2 temp_output_1_0_g8 = ((tex2DNode457).rg*2.0 + -1.0);
				float dotResult4_g8 = dot( temp_output_1_0_g8 , temp_output_1_0_g8 );
				float3 appendResult10_g8 = (float3((temp_output_1_0_g8).x , (temp_output_1_0_g8).y , sqrt( ( 1.0 - saturate( dotResult4_g8 ) ) )));
				float3 normalizeResult12_g8 = normalize( appendResult10_g8 );
				float3 temp_output_461_0 = normalizeResult12_g8;
				float3 MotionNormal467 = temp_output_461_0;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
				float3 tangentTobjectDir202 = mul( GetWorldToObjectMatrix(), float4( mul( ase_tangentToWorldFast, MotionNormal467 ), 0 ) ).xyz;
				float clampResult463 = clamp( tex2DNode457.a , 0.0 , 1.0 );
				float MotionPower468 = clampResult463;
				float3 WaveVertexPos209 = ( staticSwitch207 + ( tangentTobjectDir202 * MotionPower468 * 0.1 * _MotionVertexDisplacement ) );
				
				o.ase_texcoord.xyz = ase_worldPos;
				float3 vertexToFrag413 = WaveVertexPos209;
				o.ase_texcoord1.xyz = vertexToFrag413;
				o.ase_texcoord3.xyz = ase_worldTangent;
				o.ase_texcoord4.xyz = ase_worldNormal;
				o.ase_texcoord5.xyz = ase_worldBitangent;
				
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord.w = 0;
				o.ase_texcoord1.w = 0;
				o.ase_texcoord2.zw = 0;
				o.ase_texcoord3.w = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = WaveVertexPos209;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				o.clipPos = TransformWorldToHClip(positionWS);
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float3 ase_worldPos = IN.ase_texcoord.xyz;
				float3 vertexToFrag413 = IN.ase_texcoord1.xyz;
				float4 objectToClip8_g9 = TransformObjectToHClip(vertexToFrag413);
				float3 objectToClip8_g9NDC = objectToClip8_g9.xyz/objectToClip8_g9.w;
				float4 appendResult13_g9 = (float4(objectToClip8_g9NDC , 1.0));
				float4 computeScreenPos10_g9 = ComputeScreenPos( appendResult13_g9 );
				float4 ScreenPos415 = computeScreenPos10_g9;
				float2 ScreenUV243 = ScreenPos415.xy;
				float clampDepth240 = SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPos415.xy );
				float rawdepth243 = clampDepth240;
				float3 localReconstructWorldPos243 = ReconstructWorldPos243( ScreenUV243 , rawdepth243 );
				float3 UnderWaterPos398 = localReconstructWorldPos243;
				float WaterHeight291 = (( ase_worldPos - UnderWaterPos398 )).y;
				float clampResult307 = clamp( ( WaterHeight291 / max( _ShoreDistance , 0.0 ) ) , 0.0 , 1.0 );
				float WaterOpacity312 = clampResult307;
				float depthToLinear241 = LinearEyeDepth(clampDepth240,_ZBufferParams);
				float depthToLinear399 = LinearEyeDepth(ScreenPos415.z,_ZBufferParams);
				float WaterDepth244 = ( depthToLinear241 - depthToLinear399 );
				float temp_output_249_0 = ( WaterDepth244 / _FoamRange );
				float clampResult280 = clamp( ( _FoamOffset + temp_output_249_0 ) , 0.0 , 1.0 );
				float temp_output_287_0 = ( 1.0 - clampResult280 );
				float depthToLinear254 = LinearEyeDepth((0),_ZBufferParams);
				#ifdef _FOAM_ON
				float staticSwitch283 = step( 0.0 , ( depthToLinear241 - depthToLinear254 ) );
				#else
				float staticSwitch283 = 0.0;
				#endif
				float FoamMask292 = staticSwitch283;
				float2 texCoord258 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult259 = clamp( temp_output_249_0 , 0.0 , 1.0 );
				float2 appendResult276 = (float2(( _XTilling * texCoord258.x ) , ( clampResult259 * _YTilling )));
				float2 panner281 = ( 1.0 * _Time.y * _FoamNoiseSpeed + appendResult276);
				float4 tex2DNode288 = tex2D( _FoamNoise, panner281 );
				float clampResult306 = clamp( ( ( temp_output_287_0 - -1.0 ) * FoamMask292 * ( temp_output_287_0 * tex2DNode288.r ) ) , 0.0 , 1.0 );
				#ifdef _FOAM_ON
				float staticSwitch310 = clampResult306;
				#else
				float staticSwitch310 = 0.0;
				#endif
				float Foam313 = staticSwitch310;
				float clampResult425 = clamp( ( _ReflectExhance * 0.04 ) , 0.0 , 0.5 );
				float temp_output_496_0 = ( _SmallNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_579_0 = ( float2( 0.1,0.1 ) + _Normal_SpeedDir );
				float2 temp_output_545_0 = ( (ase_worldPos).xz / _SmallNormalTiling );
				float2 panner499 = ( temp_output_496_0 * temp_output_579_0 + temp_output_545_0);
				float3 unpack508 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner499 ), _SmallNormalIntensity );
				unpack508.z = lerp( 1, unpack508.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_580_0 = ( float2( -0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner498 = ( temp_output_496_0 * temp_output_580_0 + ( temp_output_545_0 + 0.4 ));
				float3 unpack510 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner498 ), _SmallNormalIntensity );
				unpack510.z = lerp( 1, unpack510.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_581_0 = ( float2( -0.1,0.1 ) + _Normal_SpeedDir );
				float2 panner503 = ( temp_output_496_0 * temp_output_581_0 + ( temp_output_545_0 + float2( 10,0.15 ) ));
				float3 unpack507 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner503 ), _SmallNormalIntensity );
				unpack507.z = lerp( 1, unpack507.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_582_0 = ( float2( 0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner500 = ( temp_output_496_0 * temp_output_582_0 + ( temp_output_545_0 + float2( 0.65,0.75 ) ));
				float3 unpack509 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner500 ), _SmallNormalIntensity );
				unpack509.z = lerp( 1, unpack509.z, saturate(_SmallNormalIntensity) );
				float temp_output_495_0 = ( _LargeNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_542_0 = ( (ase_worldPos).xz / _LargeNormalTiling );
				float2 panner502 = ( temp_output_495_0 * temp_output_579_0 + temp_output_542_0);
				float3 unpack505 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner502 ), _LargeNormalIntensity );
				unpack505.z = lerp( 1, unpack505.z, saturate(_LargeNormalIntensity) );
				float2 panner504 = ( temp_output_495_0 * temp_output_580_0 + ( temp_output_542_0 + 0.4 ));
				float3 unpack511 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner504 ), _LargeNormalIntensity );
				unpack511.z = lerp( 1, unpack511.z, saturate(_LargeNormalIntensity) );
				float2 panner497 = ( temp_output_495_0 * temp_output_581_0 + ( temp_output_542_0 + float2( 0.85,0.15 ) ));
				float3 unpack506 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner497 ), _LargeNormalIntensity );
				unpack506.z = lerp( 1, unpack506.z, saturate(_LargeNormalIntensity) );
				float2 panner501 = ( temp_output_495_0 * temp_output_582_0 + ( temp_output_542_0 + float2( 0.65,0.75 ) ));
				float3 unpack512 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner501 ), _LargeNormalIntensity );
				unpack512.z = lerp( 1, unpack512.z, saturate(_LargeNormalIntensity) );
				float2 temp_output_27_0 = ( (BlendNormal( BlendNormal( BlendNormal( unpack508 , unpack510 ) , BlendNormal( unpack507 , unpack509 ) ) , BlendNormal( BlendNormal( unpack505 , unpack511 ) , BlendNormal( unpack506 , unpack512 ) ) )).xy * 0.5 );
				float dotResult29 = dot( temp_output_27_0 , temp_output_27_0 );
				float3 appendResult32 = (float3(temp_output_27_0 , sqrt( ( 1.0 - dotResult29 ) )));
				float3 ase_worldTangent = IN.ase_texcoord3.xyz;
				float3 ase_worldNormal = IN.ase_texcoord4.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord5.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, ase_worldNormal.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, ase_worldNormal.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, ase_worldNormal.z );
				float3 tanNormal33 = appendResult32;
				float3 worldNormal33 = float3(dot(tanToWorld0,tanNormal33), dot(tanToWorld1,tanNormal33), dot(tanToWorld2,tanNormal33));
				float3 WaterNormal34 = worldNormal33;
				float3 tanNormal416 = WaterNormal34;
				float3 worldNormal416 = float3(dot(tanToWorld0,tanNormal416), dot(tanToWorld1,tanNormal416), dot(tanToWorld2,tanNormal416));
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult418 = dot( worldNormal416 , ase_worldViewDir );
				float temp_output_423_0 = ( 1.0 - max( dotResult418 , 0.0 ) );
				float lerpResult426 = lerp( clampResult425 , 1.0 , ( temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 ));
				float temp_output_429_0 = pow( lerpResult426 , _ReflectionAngle );
				float clampResult430 = clamp( temp_output_429_0 , 0.0 , 1.0 );
				float ReflectFresnel431 = clampResult430;
				float4 color309 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
				float2 temp_output_267_0 = ( (UnderWaterPos398).xz / _CausticsScale );
				float2 temp_output_272_0 = ( _CausticsSpeed * _TimeParameters.x * 0.01 );
				float FresnelFactor427 = lerpResult426;
				float clampResult253 = clamp( ( WaterDepth244 / _WaterDeep ) , 0.0 , 1.0 );
				float clampResult282 = clamp( max( FresnelFactor427 , ( FresnelFactor427 + clampResult253 ) ) , 0.0 , 1.0 );
				float WaterDepthRange294 = clampResult282;
				#ifdef _CAUSTICS_ON
				float4 staticSwitch311 = ( ( min( tex2D( _CausticsTex, ( temp_output_267_0 + temp_output_272_0 ) ) , tex2D( _CausticsTex, ( -temp_output_267_0 + temp_output_272_0 ) ) ) * _CausticsIntensity ) * ( 1.0 - WaterDepthRange294 ) );
				#else
				float4 staticSwitch311 = color309;
				#endif
				float4 CausticsColor407 = staticSwitch311;
				float clampResult328 = clamp( max( 0.0 , max( max( Foam313 , ( ReflectFresnel431 * WaterOpacity312 ) ) , (CausticsColor407).r ) ) , 0.0 , 1.0 );
				float lerpResult330 = lerp( WaterOpacity312 , 1.0 , clampResult328);
				float clampResult333 = clamp( ( lerpResult330 * _Alpha ) , 0.0 , 1.0 );
				float2 texCoord315 = IN.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_1 = (_Size).xx;
				float clampResult331 = clamp( ( distance( max( ( abs( ( texCoord315 - float2( 0.5,0.5 ) ) ) - temp_cast_1 ) , float2( 0,0 ) ) , float2( 0,0 ) ) / _FallOff ) , 0.0 , 1.0 );
				float FinalAlpha392 = ( clampResult333 * ( 1.0 - clampResult331 ) );
				
				surfaceDescription.Alpha = FinalAlpha392;
				surfaceDescription.AlphaClipThreshold = 0.5;


				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;
				outColor = _SelectionID;
				
				return outColor;
			}
        
			ENDHLSL
        }
		
		
        Pass
        {
			
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }

			ZTest LEqual
			ZWrite On

        
			HLSLPROGRAM
			
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_DISTANCE_TESSELLATION
			#define TESSELLATION_ON 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

			
			#pragma only_renderers d3d11 glcore gles gles3 
			#pragma multi_compile_fog
			#pragma instancing_options renderinglayer
			#pragma vertex vert
			#pragma fragment frag

        
			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define VARYINGS_NEED_NORMAL_WS

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _VERTEXWAVE_ON
			#pragma shader_feature_local_fragment _FOAM_ON
			#pragma shader_feature_local_fragment _CAUSTICS_ON
			#include "WavesFunction.hlsl"


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float3 normalWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _WaveColor;
			float4 _SpecTint;
			float4 _SubWaveDirection;
			float4 _FresnelColor;
			float4 _DeepColor;
			float4 _FoamColor;
			float4 _FoamNoise3_ST;
			float4 _FoamNoise2_ST;
			float4 _ShallowColor;
			float2 _Normal_SpeedDir;
			float2 _CausticsSpeed;
			float2 _FoamNoiseSpeed;
			float2 _Noise_speed3;
			float2 _Direction;
			float _YTilling;
			float _BlinkIntensity;
			float _SpecSmoothness;
			float _SpecIntensity;
			float _Alpha;
			float _ShoreDistance;
			float _SpecEnd;
			float _SpecStart;
			float _DayIntensity;
			float _Noise_FoamD;
			float _FoamOffset;
			float _FoamRange;
			float _BlinkThreshold;
			float _XTilling;
			float _FoamColorIntensity;
			float _BlinkNoise;
			float _WaveSpeed;
			float _BlinkTilling;
			float _WaveDistance;
			float _WaveHeight;
			float _WaveNormalStr;
			float _WaveFadeStart;
			float _WaveFadeEnd;
			float _MotionVertexDisplacement;
			float _CausticsScale;
			float _CausticsIntensity;
			float _ReflectExhance;
			float _BlinkSpeed;
			float _SmallNormalSpeed;
			float _SmallNormalIntensity;
			float _LargeNormalSpeed;
			float _LargeNormalTiling;
			float _LargeNormalIntensity;
			float _WaterDeep;
			float _ReflectionAngle;
			float _FresnelIntensity;
			float _Size;
			float _WaterNoise;
			float _SmallNormalTiling;
			float _FallOff;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WaterMotionRT;
			float4 _WaterFXCamPos;
			float _WaterFXCamSize;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _FoamNoise;
			sampler2D _WaterNormalSmall;
			sampler2D _WaterNormalLarge;
			sampler2D _CausticsTex;


			float3 ReconstructWorldPos243( float2 ScreenUV, float rawdepth )
			{
				#if UNITY_REVERSED_Z
				real depth = rawdepth;
				#else
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth);
				#endif
				float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP);
				return worldPos;
			}
			
      
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
        
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (ase_worldPos).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				float3 worldToObj203 = mul( GetWorldToObjectMatrix(), float4( ( ase_worldPos + positionWSOffset548 ), 1 ) ).xyz;
				#ifdef _VERTEXWAVE_ON
				float3 staticSwitch207 = worldToObj203;
				#else
				float3 staticSwitch207 = v.vertex.xyz;
				#endif
				float4 tex2DNode457 = tex2Dlod( _WaterMotionRT, float4( ( ( ( (ase_worldPos).xz - (_WaterFXCamPos).xz ) / ( _WaterFXCamSize * 2.0 ) ) + float2( 0.5,0.5 ) ), 0, 0.0) );
				float2 temp_output_1_0_g8 = ((tex2DNode457).rg*2.0 + -1.0);
				float dotResult4_g8 = dot( temp_output_1_0_g8 , temp_output_1_0_g8 );
				float3 appendResult10_g8 = (float3((temp_output_1_0_g8).x , (temp_output_1_0_g8).y , sqrt( ( 1.0 - saturate( dotResult4_g8 ) ) )));
				float3 normalizeResult12_g8 = normalize( appendResult10_g8 );
				float3 temp_output_461_0 = normalizeResult12_g8;
				float3 MotionNormal467 = temp_output_461_0;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
				float3 tangentTobjectDir202 = mul( GetWorldToObjectMatrix(), float4( mul( ase_tangentToWorldFast, MotionNormal467 ), 0 ) ).xyz;
				float clampResult463 = clamp( tex2DNode457.a , 0.0 , 1.0 );
				float MotionPower468 = clampResult463;
				float3 WaveVertexPos209 = ( staticSwitch207 + ( tangentTobjectDir202 * MotionPower468 * 0.1 * _MotionVertexDisplacement ) );
				
				o.ase_texcoord1.xyz = ase_worldPos;
				float3 vertexToFrag413 = WaveVertexPos209;
				o.ase_texcoord2.xyz = vertexToFrag413;
				o.ase_texcoord4.xyz = ase_worldTangent;
				o.ase_texcoord5.xyz = ase_worldBitangent;
				
				o.ase_texcoord3.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord1.w = 0;
				o.ase_texcoord2.w = 0;
				o.ase_texcoord3.zw = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = WaveVertexPos209;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float3 normalWS = TransformObjectToWorldNormal(v.ase_normal);

				o.clipPos = TransformWorldToHClip(positionWS);
				o.normalWS.xyz =  normalWS;

				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float3 ase_worldPos = IN.ase_texcoord1.xyz;
				float3 vertexToFrag413 = IN.ase_texcoord2.xyz;
				float4 objectToClip8_g9 = TransformObjectToHClip(vertexToFrag413);
				float3 objectToClip8_g9NDC = objectToClip8_g9.xyz/objectToClip8_g9.w;
				float4 appendResult13_g9 = (float4(objectToClip8_g9NDC , 1.0));
				float4 computeScreenPos10_g9 = ComputeScreenPos( appendResult13_g9 );
				float4 ScreenPos415 = computeScreenPos10_g9;
				float2 ScreenUV243 = ScreenPos415.xy;
				float clampDepth240 = SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPos415.xy );
				float rawdepth243 = clampDepth240;
				float3 localReconstructWorldPos243 = ReconstructWorldPos243( ScreenUV243 , rawdepth243 );
				float3 UnderWaterPos398 = localReconstructWorldPos243;
				float WaterHeight291 = (( ase_worldPos - UnderWaterPos398 )).y;
				float clampResult307 = clamp( ( WaterHeight291 / max( _ShoreDistance , 0.0 ) ) , 0.0 , 1.0 );
				float WaterOpacity312 = clampResult307;
				float depthToLinear241 = LinearEyeDepth(clampDepth240,_ZBufferParams);
				float depthToLinear399 = LinearEyeDepth(ScreenPos415.z,_ZBufferParams);
				float WaterDepth244 = ( depthToLinear241 - depthToLinear399 );
				float temp_output_249_0 = ( WaterDepth244 / _FoamRange );
				float clampResult280 = clamp( ( _FoamOffset + temp_output_249_0 ) , 0.0 , 1.0 );
				float temp_output_287_0 = ( 1.0 - clampResult280 );
				float depthToLinear254 = LinearEyeDepth((0),_ZBufferParams);
				#ifdef _FOAM_ON
				float staticSwitch283 = step( 0.0 , ( depthToLinear241 - depthToLinear254 ) );
				#else
				float staticSwitch283 = 0.0;
				#endif
				float FoamMask292 = staticSwitch283;
				float2 texCoord258 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult259 = clamp( temp_output_249_0 , 0.0 , 1.0 );
				float2 appendResult276 = (float2(( _XTilling * texCoord258.x ) , ( clampResult259 * _YTilling )));
				float2 panner281 = ( 1.0 * _Time.y * _FoamNoiseSpeed + appendResult276);
				float4 tex2DNode288 = tex2D( _FoamNoise, panner281 );
				float clampResult306 = clamp( ( ( temp_output_287_0 - -1.0 ) * FoamMask292 * ( temp_output_287_0 * tex2DNode288.r ) ) , 0.0 , 1.0 );
				#ifdef _FOAM_ON
				float staticSwitch310 = clampResult306;
				#else
				float staticSwitch310 = 0.0;
				#endif
				float Foam313 = staticSwitch310;
				float clampResult425 = clamp( ( _ReflectExhance * 0.04 ) , 0.0 , 0.5 );
				float temp_output_496_0 = ( _SmallNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_579_0 = ( float2( 0.1,0.1 ) + _Normal_SpeedDir );
				float2 temp_output_545_0 = ( (ase_worldPos).xz / _SmallNormalTiling );
				float2 panner499 = ( temp_output_496_0 * temp_output_579_0 + temp_output_545_0);
				float3 unpack508 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner499 ), _SmallNormalIntensity );
				unpack508.z = lerp( 1, unpack508.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_580_0 = ( float2( -0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner498 = ( temp_output_496_0 * temp_output_580_0 + ( temp_output_545_0 + 0.4 ));
				float3 unpack510 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner498 ), _SmallNormalIntensity );
				unpack510.z = lerp( 1, unpack510.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_581_0 = ( float2( -0.1,0.1 ) + _Normal_SpeedDir );
				float2 panner503 = ( temp_output_496_0 * temp_output_581_0 + ( temp_output_545_0 + float2( 10,0.15 ) ));
				float3 unpack507 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner503 ), _SmallNormalIntensity );
				unpack507.z = lerp( 1, unpack507.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_582_0 = ( float2( 0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner500 = ( temp_output_496_0 * temp_output_582_0 + ( temp_output_545_0 + float2( 0.65,0.75 ) ));
				float3 unpack509 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner500 ), _SmallNormalIntensity );
				unpack509.z = lerp( 1, unpack509.z, saturate(_SmallNormalIntensity) );
				float temp_output_495_0 = ( _LargeNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_542_0 = ( (ase_worldPos).xz / _LargeNormalTiling );
				float2 panner502 = ( temp_output_495_0 * temp_output_579_0 + temp_output_542_0);
				float3 unpack505 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner502 ), _LargeNormalIntensity );
				unpack505.z = lerp( 1, unpack505.z, saturate(_LargeNormalIntensity) );
				float2 panner504 = ( temp_output_495_0 * temp_output_580_0 + ( temp_output_542_0 + 0.4 ));
				float3 unpack511 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner504 ), _LargeNormalIntensity );
				unpack511.z = lerp( 1, unpack511.z, saturate(_LargeNormalIntensity) );
				float2 panner497 = ( temp_output_495_0 * temp_output_581_0 + ( temp_output_542_0 + float2( 0.85,0.15 ) ));
				float3 unpack506 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner497 ), _LargeNormalIntensity );
				unpack506.z = lerp( 1, unpack506.z, saturate(_LargeNormalIntensity) );
				float2 panner501 = ( temp_output_495_0 * temp_output_582_0 + ( temp_output_542_0 + float2( 0.65,0.75 ) ));
				float3 unpack512 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner501 ), _LargeNormalIntensity );
				unpack512.z = lerp( 1, unpack512.z, saturate(_LargeNormalIntensity) );
				float2 temp_output_27_0 = ( (BlendNormal( BlendNormal( BlendNormal( unpack508 , unpack510 ) , BlendNormal( unpack507 , unpack509 ) ) , BlendNormal( BlendNormal( unpack505 , unpack511 ) , BlendNormal( unpack506 , unpack512 ) ) )).xy * 0.5 );
				float dotResult29 = dot( temp_output_27_0 , temp_output_27_0 );
				float3 appendResult32 = (float3(temp_output_27_0 , sqrt( ( 1.0 - dotResult29 ) )));
				float3 ase_worldTangent = IN.ase_texcoord4.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord5.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, IN.normalWS.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, IN.normalWS.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, IN.normalWS.z );
				float3 tanNormal33 = appendResult32;
				float3 worldNormal33 = float3(dot(tanToWorld0,tanNormal33), dot(tanToWorld1,tanNormal33), dot(tanToWorld2,tanNormal33));
				float3 WaterNormal34 = worldNormal33;
				float3 tanNormal416 = WaterNormal34;
				float3 worldNormal416 = float3(dot(tanToWorld0,tanNormal416), dot(tanToWorld1,tanNormal416), dot(tanToWorld2,tanNormal416));
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult418 = dot( worldNormal416 , ase_worldViewDir );
				float temp_output_423_0 = ( 1.0 - max( dotResult418 , 0.0 ) );
				float lerpResult426 = lerp( clampResult425 , 1.0 , ( temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 ));
				float temp_output_429_0 = pow( lerpResult426 , _ReflectionAngle );
				float clampResult430 = clamp( temp_output_429_0 , 0.0 , 1.0 );
				float ReflectFresnel431 = clampResult430;
				float4 color309 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
				float2 temp_output_267_0 = ( (UnderWaterPos398).xz / _CausticsScale );
				float2 temp_output_272_0 = ( _CausticsSpeed * _TimeParameters.x * 0.01 );
				float FresnelFactor427 = lerpResult426;
				float clampResult253 = clamp( ( WaterDepth244 / _WaterDeep ) , 0.0 , 1.0 );
				float clampResult282 = clamp( max( FresnelFactor427 , ( FresnelFactor427 + clampResult253 ) ) , 0.0 , 1.0 );
				float WaterDepthRange294 = clampResult282;
				#ifdef _CAUSTICS_ON
				float4 staticSwitch311 = ( ( min( tex2D( _CausticsTex, ( temp_output_267_0 + temp_output_272_0 ) ) , tex2D( _CausticsTex, ( -temp_output_267_0 + temp_output_272_0 ) ) ) * _CausticsIntensity ) * ( 1.0 - WaterDepthRange294 ) );
				#else
				float4 staticSwitch311 = color309;
				#endif
				float4 CausticsColor407 = staticSwitch311;
				float clampResult328 = clamp( max( 0.0 , max( max( Foam313 , ( ReflectFresnel431 * WaterOpacity312 ) ) , (CausticsColor407).r ) ) , 0.0 , 1.0 );
				float lerpResult330 = lerp( WaterOpacity312 , 1.0 , clampResult328);
				float clampResult333 = clamp( ( lerpResult330 * _Alpha ) , 0.0 , 1.0 );
				float2 texCoord315 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_1 = (_Size).xx;
				float clampResult331 = clamp( ( distance( max( ( abs( ( texCoord315 - float2( 0.5,0.5 ) ) ) - temp_cast_1 ) , float2( 0,0 ) ) , float2( 0,0 ) ) / _FallOff ) , 0.0 , 1.0 );
				float FinalAlpha392 = ( clampResult333 * ( 1.0 - clampResult331 ) );
				
				surfaceDescription.Alpha = FinalAlpha392;
				surfaceDescription.AlphaClipThreshold = 0.5;

				#if _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				float3 normalWS = IN.normalWS;
				return half4(NormalizeNormalPerPixel(normalWS), 0.0);

			}
        
			ENDHLSL
        }

		
        Pass
        {
			
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
        
			ZTest LEqual
			ZWrite On
        
        
			HLSLPROGRAM
        
			#pragma multi_compile_instancing
			#define _RECEIVE_SHADOWS_OFF 1
			#define ASE_DISTANCE_TESSELLATION
			#define TESSELLATION_ON 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_ABSOLUTE_VERTEX_POS 1
			#define ASE_SRP_VERSION 999999
			#define REQUIRE_DEPTH_TEXTURE 1

        
			#pragma exclude_renderers glcore gles gles3 
			#pragma vertex vert
			#pragma fragment frag
        
			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define ATTRIBUTES_NEED_TEXCOORD1
			#define VARYINGS_NEED_NORMAL_WS
			#define VARYINGS_NEED_TANGENT_WS
        
			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
        
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _VERTEXWAVE_ON
			#pragma shader_feature_local_fragment _FOAM_ON
			#pragma shader_feature_local_fragment _CAUSTICS_ON
			#include "WavesFunction.hlsl"


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float3 normalWS : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
        
			CBUFFER_START(UnityPerMaterial)
			float4 _WaveColor;
			float4 _SpecTint;
			float4 _SubWaveDirection;
			float4 _FresnelColor;
			float4 _DeepColor;
			float4 _FoamColor;
			float4 _FoamNoise3_ST;
			float4 _FoamNoise2_ST;
			float4 _ShallowColor;
			float2 _Normal_SpeedDir;
			float2 _CausticsSpeed;
			float2 _FoamNoiseSpeed;
			float2 _Noise_speed3;
			float2 _Direction;
			float _YTilling;
			float _BlinkIntensity;
			float _SpecSmoothness;
			float _SpecIntensity;
			float _Alpha;
			float _ShoreDistance;
			float _SpecEnd;
			float _SpecStart;
			float _DayIntensity;
			float _Noise_FoamD;
			float _FoamOffset;
			float _FoamRange;
			float _BlinkThreshold;
			float _XTilling;
			float _FoamColorIntensity;
			float _BlinkNoise;
			float _WaveSpeed;
			float _BlinkTilling;
			float _WaveDistance;
			float _WaveHeight;
			float _WaveNormalStr;
			float _WaveFadeStart;
			float _WaveFadeEnd;
			float _MotionVertexDisplacement;
			float _CausticsScale;
			float _CausticsIntensity;
			float _ReflectExhance;
			float _BlinkSpeed;
			float _SmallNormalSpeed;
			float _SmallNormalIntensity;
			float _LargeNormalSpeed;
			float _LargeNormalTiling;
			float _LargeNormalIntensity;
			float _WaterDeep;
			float _ReflectionAngle;
			float _FresnelIntensity;
			float _Size;
			float _WaterNoise;
			float _SmallNormalTiling;
			float _FallOff;
			#ifdef TESSELLATION_ON
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END
			sampler2D _WaterMotionRT;
			float4 _WaterFXCamPos;
			float _WaterFXCamSize;
			uniform float4 _CameraDepthTexture_TexelSize;
			sampler2D _FoamNoise;
			sampler2D _WaterNormalSmall;
			sampler2D _WaterNormalLarge;
			sampler2D _CausticsTex;


			float3 ReconstructWorldPos243( float2 ScreenUV, float rawdepth )
			{
				#if UNITY_REVERSED_Z
				real depth = rawdepth;
				#else
				real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth);
				#endif
				float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP);
				return worldPos;
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};
      
			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 ase_worldPos = mul(GetObjectToWorldMatrix(), v.vertex).xyz;
				float localGetWaveInfo548 = ( 0.0 );
				float2 position548 = (ase_worldPos).xz;
				float2 time548 = ( _WaveSpeed * _TimeParameters.x * _Direction );
				float4 directionABCD548 = _SubWaveDirection;
				float wavedistance548 = _WaveDistance;
				float height548 = _WaveHeight;
				float normalStr548 = _WaveNormalStr;
				float fadeStart548 = _WaveFadeStart;
				float fadeEnd548 = _WaveFadeEnd;
				float3 positionWSOffset548 = float3( 0,0,0 );
				float3 normalWS548 = float3( 0,0,0 );
				GetWaveInfo( position548 , time548 , directionABCD548 , wavedistance548 , height548 , normalStr548 , fadeStart548 , fadeEnd548 , positionWSOffset548 , normalWS548 );
				float3 worldToObj203 = mul( GetWorldToObjectMatrix(), float4( ( ase_worldPos + positionWSOffset548 ), 1 ) ).xyz;
				#ifdef _VERTEXWAVE_ON
				float3 staticSwitch207 = worldToObj203;
				#else
				float3 staticSwitch207 = v.vertex.xyz;
				#endif
				float4 tex2DNode457 = tex2Dlod( _WaterMotionRT, float4( ( ( ( (ase_worldPos).xz - (_WaterFXCamPos).xz ) / ( _WaterFXCamSize * 2.0 ) ) + float2( 0.5,0.5 ) ), 0, 0.0) );
				float2 temp_output_1_0_g8 = ((tex2DNode457).rg*2.0 + -1.0);
				float dotResult4_g8 = dot( temp_output_1_0_g8 , temp_output_1_0_g8 );
				float3 appendResult10_g8 = (float3((temp_output_1_0_g8).x , (temp_output_1_0_g8).y , sqrt( ( 1.0 - saturate( dotResult4_g8 ) ) )));
				float3 normalizeResult12_g8 = normalize( appendResult10_g8 );
				float3 temp_output_461_0 = normalizeResult12_g8;
				float3 MotionNormal467 = temp_output_461_0;
				float3 ase_worldTangent = TransformObjectToWorldDir(v.ase_tangent.xyz);
				float3 ase_worldNormal = TransformObjectToWorldNormal(v.ase_normal);
				float ase_vertexTangentSign = v.ase_tangent.w * unity_WorldTransformParams.w;
				float3 ase_worldBitangent = cross( ase_worldNormal, ase_worldTangent ) * ase_vertexTangentSign;
				float3x3 ase_tangentToWorldFast = float3x3(ase_worldTangent.x,ase_worldBitangent.x,ase_worldNormal.x,ase_worldTangent.y,ase_worldBitangent.y,ase_worldNormal.y,ase_worldTangent.z,ase_worldBitangent.z,ase_worldNormal.z);
				float3 tangentTobjectDir202 = mul( GetWorldToObjectMatrix(), float4( mul( ase_tangentToWorldFast, MotionNormal467 ), 0 ) ).xyz;
				float clampResult463 = clamp( tex2DNode457.a , 0.0 , 1.0 );
				float MotionPower468 = clampResult463;
				float3 WaveVertexPos209 = ( staticSwitch207 + ( tangentTobjectDir202 * MotionPower468 * 0.1 * _MotionVertexDisplacement ) );
				
				o.ase_texcoord1.xyz = ase_worldPos;
				float3 vertexToFrag413 = WaveVertexPos209;
				o.ase_texcoord2.xyz = vertexToFrag413;
				o.ase_texcoord4.xyz = ase_worldTangent;
				o.ase_texcoord5.xyz = ase_worldBitangent;
				
				o.ase_texcoord3.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord1.w = 0;
				o.ase_texcoord2.w = 0;
				o.ase_texcoord3.zw = 0;
				o.ase_texcoord4.w = 0;
				o.ase_texcoord5.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = WaveVertexPos209;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float3 normalWS = TransformObjectToWorldNormal(v.ase_normal);

				o.clipPos = TransformWorldToHClip(positionWS);
				o.normalWS.xyz =  normalWS;
				return o;
			}

			#if defined(TESSELLATION_ON)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.vertex;
				o.ase_normal = v.ase_normal;
				o.ase_tangent = v.ase_tangent;
				o.ase_texcoord = v.ase_texcoord;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
			   return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.ase_normal = patch[0].ase_normal * bary.x + patch[1].ase_normal * bary.y + patch[2].ase_normal * bary.z;
				o.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				o.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.vertex.xyz - patch[i].ase_normal * (dot(o.vertex.xyz, patch[i].ase_normal) - dot(patch[i].vertex.xyz, patch[i].ase_normal));
				float phongStrength = _TessPhongStrength;
				o.vertex.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.vertex.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				float3 ase_worldPos = IN.ase_texcoord1.xyz;
				float3 vertexToFrag413 = IN.ase_texcoord2.xyz;
				float4 objectToClip8_g9 = TransformObjectToHClip(vertexToFrag413);
				float3 objectToClip8_g9NDC = objectToClip8_g9.xyz/objectToClip8_g9.w;
				float4 appendResult13_g9 = (float4(objectToClip8_g9NDC , 1.0));
				float4 computeScreenPos10_g9 = ComputeScreenPos( appendResult13_g9 );
				float4 ScreenPos415 = computeScreenPos10_g9;
				float2 ScreenUV243 = ScreenPos415.xy;
				float clampDepth240 = SHADERGRAPH_SAMPLE_SCENE_DEPTH( ScreenPos415.xy );
				float rawdepth243 = clampDepth240;
				float3 localReconstructWorldPos243 = ReconstructWorldPos243( ScreenUV243 , rawdepth243 );
				float3 UnderWaterPos398 = localReconstructWorldPos243;
				float WaterHeight291 = (( ase_worldPos - UnderWaterPos398 )).y;
				float clampResult307 = clamp( ( WaterHeight291 / max( _ShoreDistance , 0.0 ) ) , 0.0 , 1.0 );
				float WaterOpacity312 = clampResult307;
				float depthToLinear241 = LinearEyeDepth(clampDepth240,_ZBufferParams);
				float depthToLinear399 = LinearEyeDepth(ScreenPos415.z,_ZBufferParams);
				float WaterDepth244 = ( depthToLinear241 - depthToLinear399 );
				float temp_output_249_0 = ( WaterDepth244 / _FoamRange );
				float clampResult280 = clamp( ( _FoamOffset + temp_output_249_0 ) , 0.0 , 1.0 );
				float temp_output_287_0 = ( 1.0 - clampResult280 );
				float depthToLinear254 = LinearEyeDepth((0),_ZBufferParams);
				#ifdef _FOAM_ON
				float staticSwitch283 = step( 0.0 , ( depthToLinear241 - depthToLinear254 ) );
				#else
				float staticSwitch283 = 0.0;
				#endif
				float FoamMask292 = staticSwitch283;
				float2 texCoord258 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult259 = clamp( temp_output_249_0 , 0.0 , 1.0 );
				float2 appendResult276 = (float2(( _XTilling * texCoord258.x ) , ( clampResult259 * _YTilling )));
				float2 panner281 = ( 1.0 * _Time.y * _FoamNoiseSpeed + appendResult276);
				float4 tex2DNode288 = tex2D( _FoamNoise, panner281 );
				float clampResult306 = clamp( ( ( temp_output_287_0 - -1.0 ) * FoamMask292 * ( temp_output_287_0 * tex2DNode288.r ) ) , 0.0 , 1.0 );
				#ifdef _FOAM_ON
				float staticSwitch310 = clampResult306;
				#else
				float staticSwitch310 = 0.0;
				#endif
				float Foam313 = staticSwitch310;
				float clampResult425 = clamp( ( _ReflectExhance * 0.04 ) , 0.0 , 0.5 );
				float temp_output_496_0 = ( _SmallNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_579_0 = ( float2( 0.1,0.1 ) + _Normal_SpeedDir );
				float2 temp_output_545_0 = ( (ase_worldPos).xz / _SmallNormalTiling );
				float2 panner499 = ( temp_output_496_0 * temp_output_579_0 + temp_output_545_0);
				float3 unpack508 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner499 ), _SmallNormalIntensity );
				unpack508.z = lerp( 1, unpack508.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_580_0 = ( float2( -0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner498 = ( temp_output_496_0 * temp_output_580_0 + ( temp_output_545_0 + 0.4 ));
				float3 unpack510 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner498 ), _SmallNormalIntensity );
				unpack510.z = lerp( 1, unpack510.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_581_0 = ( float2( -0.1,0.1 ) + _Normal_SpeedDir );
				float2 panner503 = ( temp_output_496_0 * temp_output_581_0 + ( temp_output_545_0 + float2( 10,0.15 ) ));
				float3 unpack507 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner503 ), _SmallNormalIntensity );
				unpack507.z = lerp( 1, unpack507.z, saturate(_SmallNormalIntensity) );
				float2 temp_output_582_0 = ( float2( 0.1,-0.1 ) + _Normal_SpeedDir );
				float2 panner500 = ( temp_output_496_0 * temp_output_582_0 + ( temp_output_545_0 + float2( 0.65,0.75 ) ));
				float3 unpack509 = UnpackNormalScale( tex2D( _WaterNormalSmall, panner500 ), _SmallNormalIntensity );
				unpack509.z = lerp( 1, unpack509.z, saturate(_SmallNormalIntensity) );
				float temp_output_495_0 = ( _LargeNormalSpeed * _TimeParameters.x * 0.1 );
				float2 temp_output_542_0 = ( (ase_worldPos).xz / _LargeNormalTiling );
				float2 panner502 = ( temp_output_495_0 * temp_output_579_0 + temp_output_542_0);
				float3 unpack505 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner502 ), _LargeNormalIntensity );
				unpack505.z = lerp( 1, unpack505.z, saturate(_LargeNormalIntensity) );
				float2 panner504 = ( temp_output_495_0 * temp_output_580_0 + ( temp_output_542_0 + 0.4 ));
				float3 unpack511 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner504 ), _LargeNormalIntensity );
				unpack511.z = lerp( 1, unpack511.z, saturate(_LargeNormalIntensity) );
				float2 panner497 = ( temp_output_495_0 * temp_output_581_0 + ( temp_output_542_0 + float2( 0.85,0.15 ) ));
				float3 unpack506 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner497 ), _LargeNormalIntensity );
				unpack506.z = lerp( 1, unpack506.z, saturate(_LargeNormalIntensity) );
				float2 panner501 = ( temp_output_495_0 * temp_output_582_0 + ( temp_output_542_0 + float2( 0.65,0.75 ) ));
				float3 unpack512 = UnpackNormalScale( tex2D( _WaterNormalLarge, panner501 ), _LargeNormalIntensity );
				unpack512.z = lerp( 1, unpack512.z, saturate(_LargeNormalIntensity) );
				float2 temp_output_27_0 = ( (BlendNormal( BlendNormal( BlendNormal( unpack508 , unpack510 ) , BlendNormal( unpack507 , unpack509 ) ) , BlendNormal( BlendNormal( unpack505 , unpack511 ) , BlendNormal( unpack506 , unpack512 ) ) )).xy * 0.5 );
				float dotResult29 = dot( temp_output_27_0 , temp_output_27_0 );
				float3 appendResult32 = (float3(temp_output_27_0 , sqrt( ( 1.0 - dotResult29 ) )));
				float3 ase_worldTangent = IN.ase_texcoord4.xyz;
				float3 ase_worldBitangent = IN.ase_texcoord5.xyz;
				float3 tanToWorld0 = float3( ase_worldTangent.x, ase_worldBitangent.x, IN.normalWS.x );
				float3 tanToWorld1 = float3( ase_worldTangent.y, ase_worldBitangent.y, IN.normalWS.y );
				float3 tanToWorld2 = float3( ase_worldTangent.z, ase_worldBitangent.z, IN.normalWS.z );
				float3 tanNormal33 = appendResult32;
				float3 worldNormal33 = float3(dot(tanToWorld0,tanNormal33), dot(tanToWorld1,tanNormal33), dot(tanToWorld2,tanNormal33));
				float3 WaterNormal34 = worldNormal33;
				float3 tanNormal416 = WaterNormal34;
				float3 worldNormal416 = float3(dot(tanToWorld0,tanNormal416), dot(tanToWorld1,tanNormal416), dot(tanToWorld2,tanNormal416));
				float3 ase_worldViewDir = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult418 = dot( worldNormal416 , ase_worldViewDir );
				float temp_output_423_0 = ( 1.0 - max( dotResult418 , 0.0 ) );
				float lerpResult426 = lerp( clampResult425 , 1.0 , ( temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 * temp_output_423_0 ));
				float temp_output_429_0 = pow( lerpResult426 , _ReflectionAngle );
				float clampResult430 = clamp( temp_output_429_0 , 0.0 , 1.0 );
				float ReflectFresnel431 = clampResult430;
				float4 color309 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
				float2 temp_output_267_0 = ( (UnderWaterPos398).xz / _CausticsScale );
				float2 temp_output_272_0 = ( _CausticsSpeed * _TimeParameters.x * 0.01 );
				float FresnelFactor427 = lerpResult426;
				float clampResult253 = clamp( ( WaterDepth244 / _WaterDeep ) , 0.0 , 1.0 );
				float clampResult282 = clamp( max( FresnelFactor427 , ( FresnelFactor427 + clampResult253 ) ) , 0.0 , 1.0 );
				float WaterDepthRange294 = clampResult282;
				#ifdef _CAUSTICS_ON
				float4 staticSwitch311 = ( ( min( tex2D( _CausticsTex, ( temp_output_267_0 + temp_output_272_0 ) ) , tex2D( _CausticsTex, ( -temp_output_267_0 + temp_output_272_0 ) ) ) * _CausticsIntensity ) * ( 1.0 - WaterDepthRange294 ) );
				#else
				float4 staticSwitch311 = color309;
				#endif
				float4 CausticsColor407 = staticSwitch311;
				float clampResult328 = clamp( max( 0.0 , max( max( Foam313 , ( ReflectFresnel431 * WaterOpacity312 ) ) , (CausticsColor407).r ) ) , 0.0 , 1.0 );
				float lerpResult330 = lerp( WaterOpacity312 , 1.0 , clampResult328);
				float clampResult333 = clamp( ( lerpResult330 * _Alpha ) , 0.0 , 1.0 );
				float2 texCoord315 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_cast_1 = (_Size).xx;
				float clampResult331 = clamp( ( distance( max( ( abs( ( texCoord315 - float2( 0.5,0.5 ) ) ) - temp_cast_1 ) , float2( 0,0 ) ) , float2( 0,0 ) ) / _FallOff ) , 0.0 , 1.0 );
				float FinalAlpha392 = ( clampResult333 * ( 1.0 - clampResult331 ) );
				
				surfaceDescription.Alpha = FinalAlpha392;
				surfaceDescription.AlphaClipThreshold = 0.5;
				
				#if _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				float3 normalWS = IN.normalWS;
				return half4(NormalizeNormalPerPixel(normalWS), 0.0);

			}

			ENDHLSL
        }
		
	}
	
	CustomEditor "ASEMaterialInspector"
	Fallback "Hidden/InternalErrorShader"
	

}
/*ASEBEGIN
Version=18935
1920;6;1920;1013;1034.985;1384.958;1;True;False
Node;AmplifyShaderEditor.WorldPosInputsNode;447;4804.513,-249.2989;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Vector4Node;446;4767.865,-100.2706;Inherit;False;Global;_WaterFXCamPos;_WaterFXCamPos;59;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SwizzleNode;451;5004.513,-255.2989;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;450;5121.191,122.703;Inherit;False;Constant;_Float19;Float 19;59;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SwizzleNode;448;5024.865,-107.2707;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;449;5069.191,16.70299;Inherit;False;Global;_WaterFXCamSize;_WaterFXCamSize;59;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;540;-7562.74,-3237.989;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;469;-7507.592,-3328.519;Inherit;False;3002.933;1172.903;Big Normals;27;527;526;520;514;512;511;506;505;504;502;501;497;495;493;492;490;488;487;485;482;478;475;538;14;541;471;542;Big Normals;1,1,1,1;0;0
Node;AmplifyShaderEditor.WorldPosInputsNode;543;-7700.62,-4435.006;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SwizzleNode;541;-7307.109,-3242.141;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;471;-7384.467,-3125.118;Inherit;False;Property;_LargeNormalTiling;Large Normal Tiling;25;0;Create;True;0;0;0;False;0;False;10;20;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;474;-7520.891,-4320.09;Inherit;False;Property;_SmallNormalTiling;Small Normal Tiling;21;0;Create;True;0;0;0;False;0;False;10;8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;453;5379.732,-17.76086;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;452;5319.406,-250.7359;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;470;-7491.592,-4528.519;Inherit;False;2989.379;1116.15;Small Normals;31;521;516;515;513;510;509;508;507;503;500;499;498;496;494;491;489;484;483;481;479;477;476;575;576;577;578;579;580;581;582;545;Small Normals;1,1,1,1;0;0
Node;AmplifyShaderEditor.SwizzleNode;544;-7504.74,-4439.903;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;483;-7448.569,-3963.569;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;570;-7838.138,-3770.818;Inherit;False;Property;_Normal_SpeedDir;Normal_SpeedDir;19;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;575;-6918.093,-4543.113;Inherit;False;Constant;_Vector5;Vector 5;65;0;Create;True;0;0;0;False;0;False;0.1,0.1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleTimeNode;482;-7331.592,-2784.519;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;545;-7324.617,-4435.956;Inherit;True;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;485;-7043.592,-2656.519;Inherit;False;Constant;_Vector9;Vector 9;37;0;Create;True;0;0;0;False;0;False;0.85,0.15;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;481;-7166.381,-4229.471;Inherit;False;Constant;_Vector2;Vector 2;37;0;Create;True;0;0;0;False;0;False;0.4,0.35;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;576;-6935.862,-4170.699;Inherit;False;Constant;_Vector6;Vector 6;65;0;Create;True;0;0;0;False;0;False;-0.1,-0.1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;577;-6923.072,-3856.98;Inherit;False;Constant;_Vector12;Vector 12;65;0;Create;True;0;0;0;False;0;False;-0.1,0.1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;476;-7416.569,-3883.569;Inherit;False;Constant;_Float21;Float 21;7;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;578;-6936.714,-3588.056;Inherit;False;Constant;_Vector0;Vector 0;65;0;Create;True;0;0;0;False;0;False;0.1,-0.1;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;478;-7299.592,-2704.519;Inherit;False;Constant;_Float22;Float 22;7;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;484;-7480.569,-4091.567;Inherit;False;Property;_SmallNormalSpeed;Small Normal Speed;22;0;Create;True;0;0;0;False;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;455;5520.406,-238.7359;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;479;-7171.02,-3606.044;Inherit;False;Constant;_Vector4;Vector 4;37;0;Create;True;0;0;0;False;0;False;0.65,0.75;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;475;-7363.592,-2912.519;Inherit;False;Property;_LargeNormalSpeed;Large Normal Speed;26;0;Create;True;0;0;0;False;0;False;5;3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;454;5522.406,-52.73599;Inherit;False;Constant;_Vector10;Vector 10;59;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;488;-7075.592,-3008.519;Inherit;False;Constant;_Vector8;Vector 8;37;0;Create;True;0;0;0;False;0;False;0.4,0.35;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;477;-7160.57,-3866.994;Inherit;False;Constant;_Vector3;Vector 3;37;0;Create;True;0;0;0;False;0;False;10,0.15;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node;487;-7139.592,-2416.519;Inherit;False;Constant;_Vector7;Vector 7;37;0;Create;True;0;0;0;False;0;False;0.65,0.75;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleDivideOpNode;542;-7123.738,-3239.939;Inherit;True;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;493;-6691.592,-2672.519;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;490;-6707.592,-2432.519;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;580;-6742.329,-4083.85;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;456;5759.909,-230.8526;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;581;-6739.062,-3814.924;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;582;-6744.51,-3510.068;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;489;-6930.85,-3700.437;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;492;-6787.592,-2992.519;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;496;-7230.979,-4053.758;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;579;-6768.46,-4411.568;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;494;-6942.98,-4274.578;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;491;-6930.168,-3971.204;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;495;-7075.592,-2848.519;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;500;-6514.592,-3632.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0.1,-0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;499;-6595.592,-4432.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0.1,0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;497;-6515.592,-2656.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;-0.1,0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;498;-6579.592,-4192.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;-0.1,-0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;521;-6597.083,-4016.883;Inherit;False;Property;_SmallNormalIntensity;Small Normal Intensity;23;0;Create;True;0;0;0;False;0;False;0.1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;503;-6515.592,-3856.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;-0.1,0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;457;5948.509,-263.9586;Inherit;True;Global;_WaterMotionRT;_WaterMotionRT;55;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;502;-6611.592,-3232.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0.1,0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;504;-6579.592,-2992.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;-0.1,-0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;520;-6537.834,-2786.026;Inherit;False;Property;_LargeNormalIntensity;Large Normal Intensity;27;0;Create;True;0;0;0;False;0;False;0.1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;501;-6515.592,-2432.519;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0.1,-0.1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;506;-6275.592,-2688.519;Inherit;True;Property;_TextureSample5;Texture Sample 5;24;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Instance;505;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;507;-6275.592,-3888.519;Inherit;True;Property;_TextureSample4;Texture Sample 4;20;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Instance;508;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;509;-6275.592,-3648.519;Inherit;True;Property;_TextureSample6;Texture Sample 6;20;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Instance;508;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;512;-6275.592,-2448.519;Inherit;True;Property;_TextureSample9;Texture Sample 9;24;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Instance;505;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;511;-6291.592,-3024.519;Inherit;True;Property;_TextureSample8;Texture Sample 8;24;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Instance;505;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;505;-6275.592,-3264.519;Inherit;True;Property;_WaterNormalLarge;大波纹法线;24;0;Create;False;0;0;0;False;0;False;-1;None;6136843415cc71c4d8cc3b8d367c4c88;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;510;-6275.592,-4224.519;Inherit;True;Property;_TextureSample7;Texture Sample 7;20;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Instance;508;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SwizzleNode;458;6344.198,-264.0186;Inherit;False;FLOAT2;0;1;2;3;1;0;COLOR;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;176;4786.543,-1564.948;Inherit;False;2341.999;889.5058;Comment;27;234;225;220;219;213;212;211;210;207;205;204;203;200;199;196;195;194;193;192;191;190;189;188;187;186;185;548;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;508;-6275.592,-4464.519;Inherit;True;Property;_WaterNormalSmall;细波纹法线;20;0;Create;False;0;0;0;False;0;False;-1;None;df633636402a12a40b1ce0d9467c56a4;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;187;4867.543,-1315.965;Inherit;False;Property;_WaveSpeed;水波速度;53;0;Create;False;0;0;0;False;0;False;2.4;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;514;-5875.592,-2592.519;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.Vector2Node;188;4846.564,-1110.92;Inherit;True;Property;_Direction;水波运动方向（XY）;52;0;Create;False;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.BlendNormalsNode;515;-5875.592,-3792.519;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;459;6528.541,-256.3438;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT;2;False;2;FLOAT;-1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;185;4836.543,-1225.965;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;513;-5907.292,-4181.318;Inherit;True;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BlendNormalsNode;526;-5875.592,-2976.519;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WorldPosInputsNode;186;5105.614,-1514.948;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SwizzleNode;194;5308.063,-1432.348;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;189;5067.539,-1085.527;Inherit;False;Property;_WaveDistance;水波大小;54;0;Create;False;0;0;0;False;0;False;0.7;0.925;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;195;5296.037,-1021.869;Inherit;False;Property;_WaveHeight;水波高度;56;0;Create;False;0;0;0;False;0;False;0.15;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;516;-5595.402,-3937.518;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;196;5274.412,-862.8445;Inherit;False;Property;_WaveFadeStart;水波渐隐Start;59;0;Create;False;0;0;0;False;0;False;25;-106.7;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;192;5265.232,-785.6155;Inherit;False;Property;_WaveFadeEnd;水波渐隐End;60;0;Create;False;0;0;0;False;0;False;280;238.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;527;-5603.592,-2736.519;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;191;5071.543,-1319.965;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;193;5268.8,-1240.969;Inherit;False;Property;_SubWaveDirection;细节波形方向（XYZW）;57;0;Create;False;0;0;0;False;0;False;-1,-1,-1,-1;-1,-1,-1,-1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;461;6732.541,-192.5206;Inherit;False;Normal Reconstruct Z;-1;;8;63ba85b764ae0c84ab3d698b86364ae9;0;1;1;FLOAT2;0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;190;5256.726,-949.8455;Inherit;False;Property;_WaveNormalStr;水波法线强度;58;0;Create;False;0;0;0;False;0;False;0.16;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;35;-4365.316,-3015.453;Inherit;False;1473.428;788.8916;WaterNormal;8;34;33;32;31;30;29;27;28;WaterNormal;0,0.2551723,1,1;0;0
Node;AmplifyShaderEditor.BlendNormalsNode;538;-5246.55,-2743.433;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CustomExpressionNode;548;5658.462,-1244.16;Inherit;False; ;7;File;10;True;position;FLOAT2;0,0;In;;Inherit;False;True;time;FLOAT2;0,0;In;;Inherit;False;True;directionABCD;FLOAT4;0,0,0,0;In;;Inherit;False;True;wavedistance;FLOAT;0;In;;Inherit;False;True;height;FLOAT;0;In;;Inherit;False;True;normalStr;FLOAT;0;In;;Inherit;False;True;fadeStart;FLOAT;0;In;;Inherit;False;True;fadeEnd;FLOAT;0;In;;Inherit;False;True;positionWSOffset;FLOAT3;0,0,0;Out;;Inherit;False;True;normalWS;FLOAT3;0,0,0;Out;;Inherit;False;GetWaveInfo;False;False;0;;False;11;0;FLOAT;0;False;1;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;9;FLOAT3;0,0,0;False;10;FLOAT3;0,0,0;False;3;FLOAT;0;FLOAT3;10;FLOAT3;11
Node;AmplifyShaderEditor.RegisterLocalVarNode;467;7322.705,-263.5128;Inherit;False;MotionNormal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;463;6555.504,-33.79456;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;199;6061.759,-1489.369;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;468;7313.323,-122.861;Inherit;False;MotionPower;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SwizzleNode;14;-4932.495,-2737.729;Inherit;False;FLOAT2;0;1;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;198;6790.791,-1627.204;Inherit;False;467;MotionNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;28;-4300.44,-2522.621;Inherit;False;Constant;_Float3;Float 3;5;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-4179.44,-2711.621;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TransformDirectionNode;202;7003.79,-1638.204;Inherit;False;Tangent;Object;False;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.PosVertexDataNode;205;6314.059,-1342.972;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;201;7031.79,-1299.204;Inherit;False;Constant;_Float11;Float 11;61;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;200;6991.79,-1379.204;Inherit;False;468;MotionPower;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;204;7055.79,-1164.204;Inherit;False;Property;_MotionVertexDisplacement;交互顶点强度;64;0;Create;False;0;0;0;False;0;False;0.1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TransformPositionNode;203;6309.2,-1498.273;Inherit;False;World;Object;False;Fast;True;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DotProductOpNode;29;-4001.439,-2630.621;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;207;6658.374,-1447.603;Inherit;False;Property;_VERTEXWAVE;顶点波纹动画;51;0;Create;False;0;0;0;False;2;Header((Wave));Space(5);False;0;1;1;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;206;7201.79,-1404.204;Inherit;False;4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.OneMinusNode;30;-3860.439,-2613.621;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;208;7348.823,-1445.944;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SqrtOpNode;31;-3692.439,-2612.621;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;209;7542.963,-1433.029;Inherit;False;WaveVertexPos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;32;-3545.44,-2701.621;Inherit;False;FLOAT3;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;412;-124.2822,-601.657;Inherit;False;209;WaveVertexPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.VertexToFragmentNode;413;196.876,-589.2023;Inherit;False;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WorldNormalVector;33;-3367.048,-2666.666;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RegisterLocalVarNode;34;-3113.302,-2717.131;Inherit;False;WaterNormal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;177;1247.508,-7458.266;Inherit;False;2584.837;577.0111;Fresnel;19;437;436;435;434;433;432;431;430;429;428;427;426;424;423;420;418;417;416;439;Fresnel;1,1,1,1;0;0
Node;AmplifyShaderEditor.FunctionNode;414;401.6311,-588.8784;Inherit;False;Custom Screen Position;-1;;9;35530643343074e4bb5fb02a7011bd50;2,9,1,12,0;2;6;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;415;689.4961,-596.4368;Inherit;False;ScreenPos;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;439;1302.575,-7393.643;Inherit;False;34;WaterNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;178;1142.655,-6471.265;Inherit;False;2222.39;1211.228;Water Depth;27;401;400;399;398;369;368;367;366;365;364;363;292;291;284;283;275;274;270;268;254;251;244;243;242;241;240;239;Water Depth;1,1,1,1;0;0
Node;AmplifyShaderEditor.WorldNormalVector;416;1580.246,-7392.526;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;400;1235.286,-6351.824;Inherit;False;415;ScreenPos;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;417;1560.246,-7230.526;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DotProductOpNode;418;1807.247,-7328.526;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;239;1647.5,-5899.891;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.ScreenDepthNode;240;1647.871,-6428.562;Inherit;False;1;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;419;1984.405,-7535.474;Inherit;False;Constant;_Float17;Float 17;54;0;Create;True;0;0;0;False;0;False;0.04;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;421;1990.405,-7664.473;Inherit;False;Property;_ReflectExhance;反射度增强;17;0;Create;False;0;0;0;False;0;False;1;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LinearDepthNode;399;1957.549,-5866.778;Inherit;False;0;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;420;1928.247,-7296.526;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LinearDepthNode;241;2005.97,-6417.768;Inherit;False;0;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;242;2477.539,-6416.385;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;422;2207.405,-7605.474;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;423;2081.324,-7294.86;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;424;2285.585,-7336.229;Inherit;False;5;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;243;1776.494,-6112.772;Inherit;False;#if UNITY_REVERSED_Z$real depth = rawdepth@$#else$real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawdepth)@$#endif$float3 worldPos = ComputeWorldSpacePosition(ScreenUV, depth, UNITY_MATRIX_I_VP)@$return worldPos@;3;Create;2;True;ScreenUV;FLOAT2;0,0;In;;Inherit;False;True;rawdepth;FLOAT;0;In;;Inherit;False;ReconstructWorldPos;True;False;0;;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;425;2355.405,-7574.474;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;244;2765.962,-6414.939;Inherit;False;WaterDepth;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;179;1156.685,-5025.866;Inherit;False;2361.305;1215.626;Water Color;34;405;404;403;373;372;371;370;362;360;359;358;357;355;345;340;339;338;337;294;282;271;266;257;253;250;246;245;557;558;559;562;560;568;569;Water Color;0.1020221,0.5797669,0.8161765,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;180;1211.345,-2571.309;Inherit;False;2460.068;1106.858;Foam Color;31;354;353;352;347;346;310;306;305;304;300;297;296;288;287;286;281;280;278;277;276;269;262;261;260;259;258;252;249;248;247;361;Foam Color;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;246;1219.599,-3920.074;Inherit;False;Property;_WaterDeep;水深浅范围;6;0;Create;False;0;0;0;False;0;False;5;7;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;248;1266.326,-2337.594;Inherit;False;244;WaterDepth;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;426;2470.314,-7383.111;Inherit;False;3;0;FLOAT;0.04;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;245;1203.375,-4021.637;Inherit;False;244;WaterDepth;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;398;2387.02,-5970.199;Inherit;False;UnderWaterPos;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;181;1193.182,-3697.618;Inherit;False;2422.145;803.8394;Caustics Color;22;409;408;407;311;309;308;303;302;299;295;293;289;285;279;273;272;267;265;264;263;256;255;Caustics Color;0.7379313,0,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;247;1260.67,-2221.73;Inherit;False;Property;_FoamRange;泡沫范围;48;0;Create;False;0;0;0;False;0;False;1.5;1.48;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;249;1437.326,-2250.595;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;250;1451.375,-3971.637;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;409;1298.84,-3647.471;Inherit;False;398;UnderWaterPos;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;427;2770.246,-7391.856;Inherit;False;FresnelFactor;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateFragmentDataNode;251;1645.893,-5531.732;Inherit;False;0;1;clipPos;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SwizzleNode;256;1536.136,-3647.618;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LinearDepthNode;254;1958.181,-5503.118;Inherit;False;0;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;259;1656.151,-2088.083;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;253;1615.495,-3958.367;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;258;1320.53,-1758.602;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;252;1606.775,-1950.575;Inherit;False;Property;_YTilling;泡沫TillingY;45;0;Create;False;0;0;0;False;0;False;1;-0.22;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;255;1495.137,-3526.618;Inherit;False;Property;_CausticsScale;焦散大小;35;0;Create;False;0;0;0;False;0;False;5;2.38;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;260;1383.365,-1847.389;Inherit;False;Property;_XTilling;泡沫TillingX;44;0;Create;False;0;0;0;False;0;False;10;7;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;257;1720.756,-4055.545;Inherit;False;427;FresnelFactor;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;268;2394.359,-5791.328;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;269;1556.212,-1791.026;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;267;1755.136,-3613.618;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;261;1820.454,-1998.561;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;266;1956.505,-3980.493;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;265;1465.459,-3242.349;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;264;1483.906,-3165.831;Inherit;False;Constant;_Float13;Float 13;15;0;Create;True;0;0;0;False;0;False;0.01;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;262;1406.956,-2425.875;Inherit;False;Property;_FoamOffset;泡沫偏移;47;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;263;1456.135,-3370.205;Inherit;False;Property;_CausticsSpeed;焦散速度;36;0;Create;False;0;0;0;False;0;False;-8,0;-8,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.WorldPosInputsNode;270;2431.919,-6175.323;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;276;2007.901,-1803.54;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.NegateNode;273;1870.902,-3357.055;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;271;2123.739,-4062.83;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;275;2569.834,-5809.421;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;277;1611.946,-2335.834;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;272;1702.191,-3269.778;Inherit;False;3;3;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;278;1959.741,-1659.527;Inherit;False;Property;_FoamNoiseSpeed;泡沫速度;46;0;Create;False;0;0;0;False;0;False;0,-0.3;0,0.1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleSubtractOpNode;274;2640.313,-6099.573;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;280;1728.847,-2249.266;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SwizzleNode;284;2886.179,-6099.352;Inherit;False;FLOAT;1;1;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;182;1237.836,-858.7538;Inherit;False;2599.256;1461.626;Alpha;35;410;406;397;396;395;394;393;392;335;334;333;332;331;330;329;328;327;326;325;324;323;322;321;320;319;318;317;316;315;314;312;307;301;298;290;Alpha;1,1,1,1;0;0
Node;AmplifyShaderEditor.PannerNode;281;2215.251,-1807.853;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;282;2497.332,-4063.511;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;285;2079.136,-3614.618;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;279;2066.243,-3286.489;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;283;2776.547,-5778.908;Inherit;False;Property;_UNDERWATER2;水底;38;0;Create;False;0;0;0;False;0;False;0;1;1;True;;Toggle;2;Key0;Key1;Reference;310;True;True;Fragment;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;292;3035.161,-5815.656;Inherit;False;FoamMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;289;2240.136,-3642.618;Inherit;True;Property;_CausticsTex;焦散图;34;0;Create;False;0;0;0;False;0;False;-1;None;0e73d4bc69d83b34caf01bf0cd0c1819;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;286;2259.536,-2358.209;Inherit;False;Constant;_Float14;Float 14;40;0;Create;True;0;0;0;False;0;False;-1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;293;2222.471,-3316.734;Inherit;True;Property;_TextureSample3;Texture Sample 3;34;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;289;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;288;2399.784,-1837.183;Inherit;True;Property;_FoamNoise;泡沫Noise;39;1;[NoScaleOffset];Create;False;0;0;0;False;0;False;-1;None;f27f8d736add2654d924e4472763b2de;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;290;1380.365,-569.9398;Inherit;False;Property;_ShoreDistance;边缘透明范围;8;0;Create;False;0;0;0;False;0;False;1.5;0.7;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;291;3056.179,-6099.352;Inherit;False;WaterHeight;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;287;1920.044,-2253.72;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;294;3067.924,-4029.387;Inherit;False;WaterDepthRange;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;299;2565.772,-3308.83;Inherit;False;Property;_CausticsIntensity;焦散亮度;37;0;Create;False;0;0;0;False;0;False;1;0.7;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;297;2575.656,-2305.969;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;428;2626.432,-7194.791;Inherit;False;Property;_ReflectionAngle;菲涅尔反射角度;11;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;296;2489.003,-2457.099;Inherit;False;292;FoamMask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMinOpNode;295;2553.903,-3450.058;Inherit;False;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;300;2669.95,-1982.461;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;394;1366.711,-770.4577;Inherit;False;291;WaterHeight;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;408;2546.381,-3122.513;Inherit;False;294;WaterDepthRange;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;298;1550.357,-563.5248;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;301;1674.379,-762.7529;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;429;2845.604,-7244.59;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;304;2895.441,-2320.964;Inherit;True;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;303;2784.772,-3433.831;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;302;2797.777,-3161.825;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;430;3111.041,-7408.266;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;308;2980.738,-3364.472;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;307;1919.014,-758.5998;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;309;2922.093,-3616.533;Inherit;False;Constant;_Color0;Color 0;51;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;305;3216.026,-2447.895;Inherit;False;Constant;_Float15;Float 15;50;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;306;3121.92,-2326.174;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;310;3441.69,-2321.366;Inherit;False;Property;_FOAM;岸边泡沫;38;0;Create;False;0;0;0;False;2;Header((Foam));Space(5);False;0;1;1;True;;Toggle;2;Key0;Key1;Create;True;True;Fragment;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;312;2176.401,-761.8049;Inherit;False;WaterOpacity;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;431;3278.949,-7407.552;Inherit;False;ReflectFresnel;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;311;3139.394,-3399.182;Inherit;False;Property;_Caustics;焦散动画;33;0;Create;False;0;0;0;False;2;Header((Caustics));Space(5);False;0;1;1;True;;Toggle;2;Key0;Key1;Create;True;True;Fragment;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;410;1360.177,-361.5578;Inherit;False;431;ReflectFresnel;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;313;3652.839,-2319.988;Inherit;False;Foam;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;315;1404.539,75.55492;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;396;1373.01,-277.2839;Inherit;False;312;WaterOpacity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;407;3345.662,-3399.753;Inherit;False;CausticsColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.Vector2Node;314;1456.29,208.191;Inherit;False;Constant;_Vector1;Vector 1;57;0;Create;True;0;0;0;False;0;False;0.5,0.5;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;397;1413.007,-194.8918;Inherit;False;407;CausticsColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;317;1598.294,-317.5058;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;406;1369.847,-442.4156;Inherit;False;313;Foam;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;316;1660.735,75.48486;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;320;1716.31,357.9055;Inherit;False;Property;_Size;Size;62;0;Create;True;0;0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;321;1737.047,-373.0148;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode;319;1851.536,74.70605;Inherit;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SwizzleNode;318;1639.202,-186.8637;Inherit;False;FLOAT;0;1;2;3;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;322;2095.459,78.62499;Inherit;True;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;323;1833.925,-251.7309;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;325;2360.31,75.906;Inherit;True;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;324;1983.006,-188.1828;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;328;2069.966,-367.6069;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;395;1782.405,-508.0748;Inherit;False;312;WaterOpacity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.DistanceOpNode;326;2649.969,79.13988;Inherit;True;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;327;2599.287,342.2033;Inherit;False;Property;_FallOff;FallOff;63;0;Create;True;0;0;0;False;0;False;1;0.05;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;329;2916.672,75.6079;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;393;2236.367,-285.7748;Inherit;False;Property;_Alpha;总体透明度控制;3;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;330;2213.49,-488.1527;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;331;3092.245,76.18285;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;332;2442.367,-419.7748;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;334;3350.631,77.78686;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;333;2687.665,-394.7748;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;335;3496.969,-390.3029;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;64;-4336.844,-1438.941;Inherit;False;2073.883;1063.079;SpecColor;26;59;61;63;62;60;55;57;54;58;52;56;51;53;50;49;48;68;69;70;71;75;67;72;73;74;76;SpecColor;1,0.3931034,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;392;3651.804,-398.8827;Inherit;False;FinalAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;133;-6871.744,-950.5721;Inherit;False;2389.194;728.6564;ReflectBlink;14;147;153;152;151;149;146;150;145;143;144;141;142;139;135;ReflectBlink;0.03448272,1,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;46;-6557.708,-92.72696;Inherit;False;2047.295;726.5347;ReflectColor;15;154;39;42;40;43;41;1;3;37;36;38;16;2;17;155;ReflectColor;0.03448272,1,0,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;105;-7514.271,-1923.564;Inherit;False;3042.977;786.5243;WaterNormalBlink;26;132;131;130;129;128;127;126;125;124;123;122;121;119;118;117;116;115;114;113;112;111;110;109;108;107;106;WaterNormalBlink;1,0.8068966,0,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;535;-3732.733,-3317.34;Inherit;False;467;MotionNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SwizzleNode;112;-7212.797,-1873.564;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;531;-3725.733,-3216.34;Inherit;False;468;MotionPower;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.BlendNormalsNode;530;-4004.07,-3465.924;Inherit;False;0;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;339;2708.332,-4429.556;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;411;-137.0007,-749.9516;Inherit;False;392;FinalAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;43;-6229.709,295.9291;Inherit;False;Constant;_Float4;Float 4;5;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;128;-5436.825,-1414.487;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;373;1666.384,-4169.376;Inherit;False;220;WaveY;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;131;-4931.434,-1509.533;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMaxOpNode;151;-5197.883,-835.4548;Inherit;False;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;349;2495.632,-1369.851;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;66;-557.7593,-1009.343;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;153;-5296.883,-630.4548;Inherit;False;Property;_BlinkIntensity;BlinkIntensity;32;0;Create;True;0;0;0;False;0;False;5;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenPosInputsNode;2;-5886.576,-40.67593;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DotProductOpNode;127;-5577.825,-1431.487;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;460;6753.659,-340.0777;Inherit;False;Constant;_Vector11;Vector 11;62;0;Create;True;0;0;0;False;0;False;0,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;529;-4385.1,-3316.1;Inherit;False;-1;;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;352;3238.979,-1857.328;Inherit;False;6;6;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;3;COLOR;0,0,0,0;False;4;FLOAT;0;False;5;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;403;1728.802,-4715.164;Inherit;False;244;WaterDepth;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SwizzleNode;139;-6413.334,-626.8448;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;364;1479.969,-5690.892;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DistanceOpNode;68;-3562.133,-677.2746;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;443;-131.7748,-1005.366;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;147;-4814.092,-861.8207;Inherit;False;ReflectBlink;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;546;-808.5472,-824.7178;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;74;-2956.133,-664.2746;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;54;-3462.842,-1280.941;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;48;-4223.844,-1354.941;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SwizzleNode;3;-5657.577,-39.67593;Inherit;False;FLOAT2;0;1;2;3;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;125;-5945.825,-1485.487;Inherit;False;Constant;_Float10;Float 10;5;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;550;2866.111,-1200.457;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;70;-3384.133,-758.2744;Inherit;False;Property;_SpecEnd;SpecEnd;16;0;Create;True;0;0;0;False;0;False;200;200;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;354;3030.876,-1792.495;Inherit;False;Property;_FoamColorIntensity;泡沫颜色强度;50;0;Create;False;0;0;0;False;0;False;2;20;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;39;-5927.709,259.9291;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;568;3114.094,-4218.774;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;558;2768,-4128;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RegisterLocalVarNode;63;-2448.847,-1169.549;Inherit;False;SpecColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WorldSpaceCameraPos;69;-3914.133,-581.2746;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;111;-7245.157,-1701.142;Inherit;False;Property;_BlinkTilling;BlinkTilling;28;0;Create;True;0;0;0;False;0;False;8;8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;55;-3305.842,-1221.941;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;59;-3025.846,-1221.549;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;135;-6648.334,-631.9538;Inherit;False;132;WaterNormalBlink;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch;344;3862.662,-2002.11;Inherit;True;Property;_FOAM1;岸边泡沫;38;0;Create;False;0;0;0;False;2;Header((Foam));Space(5);False;0;0;0;True;;Toggle;2;Key0;Key1;Reference;310;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;574;178.7141,-918.4034;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;534;-3226.719,-3436.612;Inherit;False;SurfaceNormal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;435;3150.019,-7255.255;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;89;-1381.734,-1081.639;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;371;1351.124,-4465.5;Inherit;False;Property;_FresnelColor;菲涅尔颜色;9;0;Create;False;0;0;0;False;0;False;0.3686275,0.6431373,0.9137255,0;0.4826449,0.5151395,0.5471698,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;445;-388.5726,-569.4753;Inherit;False;313;Foam;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;212;6606.089,-999.0154;Inherit;False;Property;_VERTEXWAVE;VERTEXWAVE;26;0;Create;True;0;0;0;False;0;False;0;0;1;True;;Toggle;2;Key0;Key1;Reference;-1;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.UnityObjToClipPosHlpNode;40;-6297.709,424.9289;Inherit;False;1;0;FLOAT3;0,0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldNormalVector;84;-1958.733,-1209.639;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleSubtractOpNode;73;-3150.133,-557.2746;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;72;-3155.133,-715.2744;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;404;1380.968,-4279.334;Inherit;False;433;ColorFresnel;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;122;-6436.567,-1511.001;Inherit;True;Property;_TextureSample1;Texture Sample 1;20;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Instance;508;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;441;-375.7386,-1006.177;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;42;-6026.709,416.9289;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;533;-3444.733,-3428.34;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SwizzleNode;37;-6142.308,214.593;Inherit;False;FLOAT2;0;2;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;361;2931.979,-1641.68;Inherit;True;Property;_FoamNoise2;泡沫Noise2;40;0;Create;False;0;0;0;False;0;False;-1;None;eae82cea2e0854844b2bcce320c8f8da;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;45;-1462.44,-1355.405;Inherit;False;44;ReflectColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;155;-5084.337,251.836;Inherit;False;147;ReflectBlink;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;115;-7039.676,-1808.717;Inherit;True;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;123;-6106.98,-1600.684;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TransformDirectionNode;210;6294.228,-1030.777;Inherit;False;World;Object;True;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SwizzleNode;124;-5936.605,-1602.193;Inherit;False;FLOAT2;0;1;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;340;1977.802,-4687.164;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;116;-6882.742,-1366.386;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;557;2961,-4218;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;86;-1690.734,-1084.639;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;60;-3232.847,-1028.549;Inherit;False;Property;_SpecTint;SpecTint;12;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StepOpNode;347;2670.159,-2101.573;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;559;2736,-4288;Inherit;False;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;113;-7158.028,-1377.739;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;213;6863,-1044.495;Inherit;False;WaveVertexNormal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;114;-7028.479,-1531.18;Inherit;False;Constant;_Float9;Float 9;5;0;Create;True;0;0;0;False;0;False;1.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;67;-3869.88,-771.8054;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;49;-4286.844,-1186.941;Inherit;False;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LinearDepthNode;367;1956.743,-5708.329;Inherit;False;0;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;536;-4402.227,-3456.8;Inherit;False;44;ReflectColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;71;-3364.133,-531.2745;Inherit;False;Property;_SpecStart;SpecStart;15;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;132;-4714.293,-1520.137;Inherit;False;WaterNormalBlink;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch;462;7019.047,-296.712;Inherit;False;Property;_DynamicMotion;水体交互;0;0;Create;False;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;560;3392,-4245.884;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;126;-5755.825,-1512.487;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LightColorNode;562;2784,-3968;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;118;-6665.24,-1797.232;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;146;-5842.973,-850.0975;Inherit;True;Property;_ReflectionTex;ReflectionTex;1;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;62;-2957.847,-1011.549;Inherit;False;Property;_SpecIntensity;SpecIntensity;13;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;357;1686.422,-4513.427;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ScreenDepthNode;363;1625.474,-5702.449;Inherit;False;1;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;110;-7464.271,-1368.141;Inherit;False;Constant;_Float8;Float 8;3;0;Create;True;0;0;0;False;0;False;0.1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;337;2111.891,-4702.9;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;52;-3625.842,-1332.941;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;342;1938.633,-1225.851;Inherit;False;Property;_Noise_speed3;Noise_speed3;42;0;Create;True;0;0;0;False;0;False;0,0;0.1,0.1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.StaticSwitch;219;6621.058,-1223.271;Inherit;False;Property;_VERTEXWAVE;VERTEXWAVE;38;0;Create;True;0;0;0;False;0;False;0;0;1;True;;Toggle;2;Key0;Key1;Reference;310;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;65;-1077.194,-807.7078;Inherit;False;63;SpecColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.WorldPosInputsNode;108;-7427.676,-1867.716;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ColorNode;353;2961.553,-1986.374;Inherit;False;Property;_FoamColor;泡沫颜色;49;0;Create;False;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;142;-6279.628,-472.7409;Inherit;False;Property;_BlinkNoise;BlinkNoise;30;0;Create;True;0;0;0;False;0;False;1;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;17;-5407.701,67.49;Inherit;True;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;366;2289.66,-5623.844;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;76;-2564.133,-921.2745;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;358;1357.664,-4639.107;Inherit;False;Property;_DeepColor;深水颜色;5;0;Create;False;0;0;0;False;0;False;0,0.4666667,0.7450981,0;0,0,0,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;442;-595.2777,-872.8581;Inherit;False;Property;_DayIntensity;总体亮度控制;2;0;Create;False;0;0;0;False;0;False;0.75;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;346;2198.462,-1510.79;Inherit;False;0;361;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;75;-2777.133,-664.2746;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;119;-6631.627,-1485.217;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode;355;1667.427,-4383.533;Inherit;False;Property;_WaveColor;波峰颜色;61;1;[HDR];Create;False;0;0;0;False;0;False;0.3686275,0.6431373,0.9137255,0;0,0.6626718,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;437;3460.021,-7265.255;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;56;-3737.842,-1103.941;Inherit;False;Property;_SpecSmoothness;SpecSmoothness;14;0;Create;True;0;0;0;False;0;False;0.1;5.96;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;401;1236.061,-5540.661;Inherit;False;-1;;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;36;-6377.306,209.9665;Inherit;False;34;WaterNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;350;1818.633,-1420.851;Inherit;True;Property;_FoamNoise3;泡沫Noise3;41;0;Create;False;0;0;0;False;0;False;-1;None;f27f8d736add2654d924e4472763b2de;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;83;-1147.956,-1353.154;Inherit;False;360;WaterColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;61;-2744.847,-1164.549;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;444;-605.6766,-775.2112;Inherit;False;351;FoamColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;220;6845.829,-1242.38;Inherit;False;WaveY;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;465;7001.658,4.92223;Inherit;False;Property;_DynamicMotion1;水体交互;78;0;Create;False;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Reference;207;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;365;2559.159,-5607.413;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;16;-5777.602,351.696;Inherit;False;Property;_WaterNoise;WaterNoise;18;0;Create;True;0;0;0;False;0;False;1;0.98;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;362;1933.773,-4884.011;Inherit;False;Property;_ShallowColor;浅水颜色;4;0;Create;False;0;0;0;False;2;Header((Color Settings));Space(5);False;0.4862745,1,0.8588235,0;0.05433348,0.3490554,0.1607607,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NormalVertexDataNode;211;6351.09,-845.0154;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;101;-1081.884,-1251.638;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SwizzleNode;144;-6258.603,-862.113;Inherit;False;FLOAT2;0;1;2;3;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;1;-5189.888,-50.76896;Inherit;True;Global;_PlanarReflectionTexture;_PlanarReflectionTexture;1;0;Create;True;0;0;0;False;0;False;-1;None;;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;556;2841.302,-1356.979;Inherit;False;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;58;-3684.842,-977.9415;Inherit;False;Constant;_Float5;Float 5;6;0;Create;True;0;0;0;False;0;False;256;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LightColorNode;561;3285.563,-1135.255;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.DotProductOpNode;554;3123.322,-1294.416;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;57;-3459.842,-1035.941;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;547;-137.1108,-502.689;Inherit;False;213;WaveVertexNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;348;1573.633,-1397.851;Inherit;False;0;350;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;345;1958.422,-4165.903;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode;532;-3707.034,-3439.584;Inherit;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;88;-1526.734,-1084.639;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;130;-5121.825,-1502.487;Inherit;False;FLOAT3;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;106;-7444.399,-1271.481;Inherit;False;Property;_BlinkSpeed;BlinkSpeed;29;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;434;3314.019,-7263.255;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;50;-3989.843,-1256.941;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SqrtOpNode;129;-5268.825,-1413.487;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;154;-4878.658,132.1891;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;149;-5384.883,-837.4545;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;117;-6864.806,-1545.753;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;152;-5014.883,-822.4545;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;143;-6081.335,-563.8448;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;360;3159.52,-4432.606;Inherit;False;WaterColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;103;-1115.3,-1150.619;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;145;-5981.156,-844.6385;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;87;-1963.733,-1029.639;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.PannerNode;336;2224.632,-1345.851;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;109;-7399.908,-1491.543;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;351;4129.034,-1998.894;Inherit;True;FoamColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMaxOpNode;338;1848.014,-4608.635;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;432;2897.02,-7086.255;Inherit;False;Property;_FresnelIntensity;菲涅尔强度;10;0;Create;False;0;0;0;False;0;False;0.2;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SwizzleNode;234;6344.93,-1182.104;Inherit;False;FLOAT;1;1;2;3;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;359;2540.952,-4767.705;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;150;-5641.381,-636.2308;Inherit;False;Property;_BlinkThreshold;BlinkThreshold;31;0;Create;True;0;0;0;False;0;False;2;0.34;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;369;3017.233,-5614.955;Inherit;False;RefractionMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PosVertexDataNode;41;-6507.708,423.9289;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;107;-7028.479,-1252.039;Inherit;False;Constant;_Float7;Float 7;5;0;Create;True;0;0;0;False;0;False;-1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;225;6487.09,-1249.015;Inherit;False;Constant;_Float12;Float 12;49;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;464;6761.659,-121.0778;Inherit;False;Constant;_Float20;Float 20;62;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;372;2151.058,-4503.992;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch;368;2751.582,-5606.917;Inherit;False;Property;_UNDERWATER1;水底;37;0;Create;False;0;0;0;False;0;False;0;1;1;True;;Toggle;2;Key0;Key1;Reference;-1;True;True;Fragment;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode;51;-3838.842,-1257.941;Inherit;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;102;-1235.3,-1150.619;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.2;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;38;-5584.309,261.592;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;569;3261.094,-4213.774;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;53;-3900.843,-1388.941;Inherit;False;34;WaterNormal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;90;-847.881,-1122.325;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;343;2256.632,-1185.852;Inherit;False;Property;_Noise_FoamD;Noise_FoamD;43;0;Create;True;0;0;0;False;0;False;0;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenPosInputsNode;141;-6487.602,-863.113;Float;False;0;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;44;-4642.93,129.4408;Inherit;False;ReflectColor;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;405;2310.486,-4621.286;Inherit;False;407;CausticsColor;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;433;3608.345,-7262.709;Inherit;False;ColorFresnel;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;436;2959.019,-6997.255;Inherit;False;Constant;_Float18;Float 18;33;0;Create;True;0;0;0;False;0;False;5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;370;1665.25,-4613.734;Inherit;False;Property;_WaterDeep1;清水范围;7;0;Create;False;0;0;0;False;0;False;1.5;1.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;121;-6438.24,-1818.232;Inherit;True;Property;_TextureSample2;Texture Sample 2;20;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Instance;508;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;166;-67.05505,-786.1531;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;True;1;1;False;-1;0;False;-1;0;1;False;-1;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;0;False;False;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;169;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;False;False;True;False;False;False;False;0;False;-1;False;False;False;False;False;False;False;False;False;True;1;False;-1;False;False;True;1;LightMode=DepthOnly;False;False;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;168;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;False;False;True;False;False;False;False;0;False;-1;False;False;False;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=ShadowCaster;False;False;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;171;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;True;1;5;False;-1;10;False;-1;1;1;False;-1;10;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;2;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;LightMode=Universal2D;False;False;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;172;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;SceneSelectionPass;0;6;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;True;4;d3d11;glcore;gles;gles3;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;173;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ScenePickingPass;0;7;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;True;4;d3d11;glcore;gles;gles3;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;174;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormals;0;8;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=DepthNormalsOnly;False;True;4;d3d11;glcore;gles;gles3;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;170;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;167;475.2286,-999.6098;Float;False;True;-1;2;ASEMaterialInspector;0;3;Water_transparent;2992e84f91cbeb14eab234972e07ea9d;True;Forward;0;1;Forward;8;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;True;1;5;False;-1;10;False;-1;1;1;False;-1;10;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;2;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;LightMode=UniversalForwardOnly;False;False;2;Include;;False;;Native;Include;WavesFunction.hlsl;False;;Custom;Hidden/InternalErrorShader;1;=;0;Standard;22;Surface;1;638556572169281360;  Blend;0;0;Two Sided;1;0;Cast Shadows;0;638555824172572385;  Use Shadow Threshold;0;0;Receive Shadows;0;638555824183408475;GPU Instancing;1;0;LOD CrossFade;0;0;Built-in Fog;0;0;DOTS Instancing;0;0;Meta Pass;0;0;Extra Pre Pass;0;0;Tessellation;1;638556772848195450;  Phong;0;0;  Strength;0.5,False,-1;0;  Type;1;638556681311235105;  Tess;16,False,-1;0;  Min;10,False,-1;0;  Max;25,False,-1;0;  Edge Length;16,False,-1;0;  Max Displacement;25,False,-1;0;Vertex Position,InvertActionOnDeselection;0;638556773070683729;0;10;False;True;False;True;False;True;True;True;True;True;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;175;1053.854,415.3145;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;3;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormalsOnly;0;9;DepthNormalsOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;-1;False;True;0;False;-1;False;False;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;False;False;False;True;3;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;True;2;True;17;d3d9;d3d11;glcore;gles;gles3;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;-1;True;3;False;-1;False;True;1;LightMode=DepthNormalsOnly;False;True;15;d3d9;d3d11_9x;d3d11;metal;vulkan;xbox360;xboxone;xboxseries;ps4;playstation;psp2;n3ds;wiiu;switch;nomrt;0;Hidden/InternalErrorShader;1;=;0;Standard;0;False;0
Node;AmplifyShaderEditor.CommentaryNode;466;4733.326,-421.2648;Inherit;False;2563.814;100;Interactive;0;Interactive;1,0,0.6004562,1;0;0
WireConnection;451;0;447;0
WireConnection;448;0;446;0
WireConnection;541;0;540;0
WireConnection;453;0;449;0
WireConnection;453;1;450;0
WireConnection;452;0;451;0
WireConnection;452;1;448;0
WireConnection;544;0;543;0
WireConnection;545;0;544;0
WireConnection;545;1;474;0
WireConnection;455;0;452;0
WireConnection;455;1;453;0
WireConnection;542;0;541;0
WireConnection;542;1;471;0
WireConnection;493;0;542;0
WireConnection;493;1;485;0
WireConnection;490;0;542;0
WireConnection;490;1;487;0
WireConnection;580;0;576;0
WireConnection;580;1;570;0
WireConnection;456;0;455;0
WireConnection;456;1;454;0
WireConnection;581;0;577;0
WireConnection;581;1;570;0
WireConnection;582;0;578;0
WireConnection;582;1;570;0
WireConnection;489;0;545;0
WireConnection;489;1;479;0
WireConnection;492;0;542;0
WireConnection;492;1;488;1
WireConnection;496;0;484;0
WireConnection;496;1;483;0
WireConnection;496;2;476;0
WireConnection;579;0;575;0
WireConnection;579;1;570;0
WireConnection;494;0;545;0
WireConnection;494;1;481;1
WireConnection;491;0;545;0
WireConnection;491;1;477;0
WireConnection;495;0;475;0
WireConnection;495;1;482;0
WireConnection;495;2;478;0
WireConnection;500;0;489;0
WireConnection;500;2;582;0
WireConnection;500;1;496;0
WireConnection;499;0;545;0
WireConnection;499;2;579;0
WireConnection;499;1;496;0
WireConnection;497;0;493;0
WireConnection;497;2;581;0
WireConnection;497;1;495;0
WireConnection;498;0;494;0
WireConnection;498;2;580;0
WireConnection;498;1;496;0
WireConnection;503;0;491;0
WireConnection;503;2;581;0
WireConnection;503;1;496;0
WireConnection;457;1;456;0
WireConnection;502;0;542;0
WireConnection;502;2;579;0
WireConnection;502;1;495;0
WireConnection;504;0;492;0
WireConnection;504;2;580;0
WireConnection;504;1;495;0
WireConnection;501;0;490;0
WireConnection;501;2;582;0
WireConnection;501;1;495;0
WireConnection;506;1;497;0
WireConnection;506;5;520;0
WireConnection;507;1;503;0
WireConnection;507;5;521;0
WireConnection;509;1;500;0
WireConnection;509;5;521;0
WireConnection;512;1;501;0
WireConnection;512;5;520;0
WireConnection;511;1;504;0
WireConnection;511;5;520;0
WireConnection;505;1;502;0
WireConnection;505;5;520;0
WireConnection;510;1;498;0
WireConnection;510;5;521;0
WireConnection;458;0;457;0
WireConnection;508;1;499;0
WireConnection;508;5;521;0
WireConnection;514;0;506;0
WireConnection;514;1;512;0
WireConnection;515;0;507;0
WireConnection;515;1;509;0
WireConnection;459;0;458;0
WireConnection;513;0;508;0
WireConnection;513;1;510;0
WireConnection;526;0;505;0
WireConnection;526;1;511;0
WireConnection;194;0;186;0
WireConnection;516;0;513;0
WireConnection;516;1;515;0
WireConnection;527;0;526;0
WireConnection;527;1;514;0
WireConnection;191;0;187;0
WireConnection;191;1;185;0
WireConnection;191;2;188;0
WireConnection;461;1;459;0
WireConnection;538;0;516;0
WireConnection;538;1;527;0
WireConnection;548;1;194;0
WireConnection;548;2;191;0
WireConnection;548;3;193;0
WireConnection;548;4;189;0
WireConnection;548;5;195;0
WireConnection;548;6;190;0
WireConnection;548;7;196;0
WireConnection;548;8;192;0
WireConnection;467;0;461;0
WireConnection;463;0;457;4
WireConnection;199;0;186;0
WireConnection;199;1;548;10
WireConnection;468;0;463;0
WireConnection;14;0;538;0
WireConnection;27;0;14;0
WireConnection;27;1;28;0
WireConnection;202;0;198;0
WireConnection;203;0;199;0
WireConnection;29;0;27;0
WireConnection;29;1;27;0
WireConnection;207;1;205;0
WireConnection;207;0;203;0
WireConnection;206;0;202;0
WireConnection;206;1;200;0
WireConnection;206;2;201;0
WireConnection;206;3;204;0
WireConnection;30;0;29;0
WireConnection;208;0;207;0
WireConnection;208;1;206;0
WireConnection;31;0;30;0
WireConnection;209;0;208;0
WireConnection;32;0;27;0
WireConnection;32;2;31;0
WireConnection;413;0;412;0
WireConnection;33;0;32;0
WireConnection;34;0;33;0
WireConnection;414;6;413;0
WireConnection;415;0;414;0
WireConnection;416;0;439;0
WireConnection;418;0;416;0
WireConnection;418;1;417;0
WireConnection;239;0;400;0
WireConnection;240;0;400;0
WireConnection;399;0;239;2
WireConnection;420;0;418;0
WireConnection;241;0;240;0
WireConnection;242;0;241;0
WireConnection;242;1;399;0
WireConnection;422;0;421;0
WireConnection;422;1;419;0
WireConnection;423;0;420;0
WireConnection;424;0;423;0
WireConnection;424;1;423;0
WireConnection;424;2;423;0
WireConnection;424;3;423;0
WireConnection;424;4;423;0
WireConnection;243;0;400;0
WireConnection;243;1;240;0
WireConnection;425;0;422;0
WireConnection;244;0;242;0
WireConnection;426;0;425;0
WireConnection;426;2;424;0
WireConnection;398;0;243;0
WireConnection;249;0;248;0
WireConnection;249;1;247;0
WireConnection;250;0;245;0
WireConnection;250;1;246;0
WireConnection;427;0;426;0
WireConnection;256;0;409;0
WireConnection;254;0;251;3
WireConnection;259;0;249;0
WireConnection;253;0;250;0
WireConnection;268;0;241;0
WireConnection;268;1;254;0
WireConnection;269;0;260;0
WireConnection;269;1;258;1
WireConnection;267;0;256;0
WireConnection;267;1;255;0
WireConnection;261;0;259;0
WireConnection;261;1;252;0
WireConnection;266;0;257;0
WireConnection;266;1;253;0
WireConnection;276;0;269;0
WireConnection;276;1;261;0
WireConnection;273;0;267;0
WireConnection;271;0;257;0
WireConnection;271;1;266;0
WireConnection;275;1;268;0
WireConnection;277;0;262;0
WireConnection;277;1;249;0
WireConnection;272;0;263;0
WireConnection;272;1;265;0
WireConnection;272;2;264;0
WireConnection;274;0;270;0
WireConnection;274;1;398;0
WireConnection;280;0;277;0
WireConnection;284;0;274;0
WireConnection;281;0;276;0
WireConnection;281;2;278;0
WireConnection;282;0;271;0
WireConnection;285;0;267;0
WireConnection;285;1;272;0
WireConnection;279;0;273;0
WireConnection;279;1;272;0
WireConnection;283;0;275;0
WireConnection;292;0;283;0
WireConnection;289;1;285;0
WireConnection;293;1;279;0
WireConnection;288;1;281;0
WireConnection;291;0;284;0
WireConnection;287;0;280;0
WireConnection;294;0;282;0
WireConnection;297;0;287;0
WireConnection;297;1;286;0
WireConnection;295;0;289;0
WireConnection;295;1;293;0
WireConnection;300;0;287;0
WireConnection;300;1;288;1
WireConnection;298;0;290;0
WireConnection;301;0;394;0
WireConnection;301;1;298;0
WireConnection;429;0;426;0
WireConnection;429;1;428;0
WireConnection;304;0;297;0
WireConnection;304;1;296;0
WireConnection;304;2;300;0
WireConnection;303;0;295;0
WireConnection;303;1;299;0
WireConnection;302;0;408;0
WireConnection;430;0;429;0
WireConnection;308;0;303;0
WireConnection;308;1;302;0
WireConnection;307;0;301;0
WireConnection;306;0;304;0
WireConnection;310;1;305;0
WireConnection;310;0;306;0
WireConnection;312;0;307;0
WireConnection;431;0;430;0
WireConnection;311;1;309;0
WireConnection;311;0;308;0
WireConnection;313;0;310;0
WireConnection;407;0;311;0
WireConnection;317;0;410;0
WireConnection;317;1;396;0
WireConnection;316;0;315;0
WireConnection;316;1;314;0
WireConnection;321;0;406;0
WireConnection;321;1;317;0
WireConnection;319;0;316;0
WireConnection;318;0;397;0
WireConnection;322;0;319;0
WireConnection;322;1;320;0
WireConnection;323;0;321;0
WireConnection;323;1;318;0
WireConnection;325;0;322;0
WireConnection;324;1;323;0
WireConnection;328;0;324;0
WireConnection;326;0;325;0
WireConnection;329;0;326;0
WireConnection;329;1;327;0
WireConnection;330;0;395;0
WireConnection;330;2;328;0
WireConnection;331;0;329;0
WireConnection;332;0;330;0
WireConnection;332;1;393;0
WireConnection;334;0;331;0
WireConnection;333;0;332;0
WireConnection;335;0;333;0
WireConnection;335;1;334;0
WireConnection;392;0;335;0
WireConnection;112;0;108;0
WireConnection;530;0;536;0
WireConnection;530;1;529;0
WireConnection;339;0;359;0
WireConnection;339;1;372;0
WireConnection;339;2;282;0
WireConnection;128;0;127;0
WireConnection;131;0;130;0
WireConnection;151;0;149;0
WireConnection;349;0;346;0
WireConnection;349;1;336;0
WireConnection;349;2;343;0
WireConnection;66;0;90;0
WireConnection;66;1;546;0
WireConnection;127;0;126;0
WireConnection;127;1;126;0
WireConnection;352;0;306;0
WireConnection;352;1;353;0
WireConnection;352;2;354;0
WireConnection;352;3;361;0
WireConnection;352;4;554;0
WireConnection;352;5;561;1
WireConnection;139;0;135;0
WireConnection;364;0;400;0
WireConnection;364;1;401;0
WireConnection;68;0;67;0
WireConnection;68;1;69;0
WireConnection;443;0;441;0
WireConnection;443;1;444;0
WireConnection;443;2;445;0
WireConnection;147;0;152;0
WireConnection;546;0;90;0
WireConnection;546;1;65;0
WireConnection;74;0;72;0
WireConnection;74;1;73;0
WireConnection;54;0;52;0
WireConnection;3;0;2;0
WireConnection;39;0;37;0
WireConnection;39;1;42;0
WireConnection;568;0;557;0
WireConnection;63;0;76;0
WireConnection;55;0;54;0
WireConnection;55;1;57;0
WireConnection;59;0;55;0
WireConnection;59;1;60;0
WireConnection;344;1;353;0
WireConnection;344;0;352;0
WireConnection;574;0;443;0
WireConnection;534;0;533;0
WireConnection;435;0;429;0
WireConnection;435;1;432;0
WireConnection;435;2;436;0
WireConnection;89;0;88;0
WireConnection;212;1;211;0
WireConnection;212;0;210;0
WireConnection;40;0;41;0
WireConnection;73;0;70;0
WireConnection;73;1;71;0
WireConnection;72;0;70;0
WireConnection;72;1;68;0
WireConnection;122;1;119;0
WireConnection;441;0;66;0
WireConnection;441;1;442;0
WireConnection;42;0;43;0
WireConnection;42;1;40;4
WireConnection;533;0;532;0
WireConnection;533;1;535;0
WireConnection;533;2;531;0
WireConnection;37;0;36;0
WireConnection;361;1;349;0
WireConnection;115;0;112;0
WireConnection;115;1;111;0
WireConnection;123;0;121;0
WireConnection;123;1;122;0
WireConnection;210;0;548;11
WireConnection;124;0;123;0
WireConnection;340;0;403;0
WireConnection;340;1;338;0
WireConnection;116;0;113;0
WireConnection;116;1;107;0
WireConnection;557;0;559;0
WireConnection;557;1;558;0
WireConnection;86;0;84;0
WireConnection;86;1;87;0
WireConnection;347;0;288;1
WireConnection;347;1;287;0
WireConnection;113;0;109;0
WireConnection;113;1;110;0
WireConnection;113;2;106;0
WireConnection;213;0;210;0
WireConnection;367;0;363;0
WireConnection;132;0;131;0
WireConnection;462;1;460;0
WireConnection;462;0;461;0
WireConnection;560;0;339;0
WireConnection;560;1;569;0
WireConnection;560;2;562;1
WireConnection;126;0;124;0
WireConnection;126;1;125;0
WireConnection;118;0;115;0
WireConnection;118;1;113;0
WireConnection;146;1;145;0
WireConnection;357;0;358;0
WireConnection;357;1;371;0
WireConnection;357;2;404;0
WireConnection;363;0;364;0
WireConnection;337;0;340;0
WireConnection;52;0;53;0
WireConnection;52;1;51;0
WireConnection;219;1;225;0
WireConnection;219;0;234;0
WireConnection;17;0;3;0
WireConnection;17;1;38;0
WireConnection;366;0;367;0
WireConnection;366;1;254;0
WireConnection;76;0;61;0
WireConnection;76;1;75;0
WireConnection;75;0;74;0
WireConnection;119;0;117;0
WireConnection;119;1;116;0
WireConnection;437;0;434;0
WireConnection;350;1;348;0
WireConnection;61;0;59;0
WireConnection;61;1;62;0
WireConnection;220;0;219;0
WireConnection;465;1;464;0
WireConnection;465;0;463;0
WireConnection;365;1;366;0
WireConnection;101;0;45;0
WireConnection;101;1;103;0
WireConnection;144;0;141;0
WireConnection;1;1;17;0
WireConnection;554;0;556;0
WireConnection;554;1;550;0
WireConnection;57;0;56;0
WireConnection;57;1;58;0
WireConnection;345;0;373;0
WireConnection;532;0;530;0
WireConnection;88;0;86;0
WireConnection;130;0;126;0
WireConnection;130;2;129;0
WireConnection;434;0;435;0
WireConnection;434;1;435;0
WireConnection;50;0;48;0
WireConnection;50;1;49;0
WireConnection;129;0;128;0
WireConnection;154;0;1;0
WireConnection;154;1;155;0
WireConnection;149;0;146;0
WireConnection;149;1;150;0
WireConnection;117;0;115;0
WireConnection;117;1;114;0
WireConnection;152;0;151;0
WireConnection;152;1;153;0
WireConnection;143;0;139;0
WireConnection;143;1;142;0
WireConnection;360;0;560;0
WireConnection;103;0;102;0
WireConnection;145;0;144;0
WireConnection;145;1;143;0
WireConnection;336;0;350;0
WireConnection;336;2;342;0
WireConnection;351;0;344;0
WireConnection;338;0;370;0
WireConnection;234;0;548;10
WireConnection;359;0;362;0
WireConnection;359;1;405;0
WireConnection;369;0;368;0
WireConnection;372;0;357;0
WireConnection;372;1;355;0
WireConnection;372;2;345;0
WireConnection;368;0;365;0
WireConnection;51;0;50;0
WireConnection;102;0;89;0
WireConnection;38;0;39;0
WireConnection;38;1;16;0
WireConnection;569;0;568;0
WireConnection;90;0;83;0
WireConnection;90;1;101;0
WireConnection;90;2;89;0
WireConnection;44;0;154;0
WireConnection;433;0;437;0
WireConnection;121;1;118;0
WireConnection;167;2;574;0
WireConnection;167;3;411;0
WireConnection;167;5;412;0
ASEEND*/
//CHKSM=DF9581C418AFD7EA7FC39E8129CC056F17D9B5A5