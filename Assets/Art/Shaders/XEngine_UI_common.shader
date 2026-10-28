Shader "XEngine/UI/UI_common"
{
	Properties 
	{
		//[HDR]_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
		//_MainTex ("Particle Texture", 2D) = "white" {}
		//[Toggle(_ENABLE_FOG_TRANS)] _enableFog("启用场景雾", Float) = 0
		
		[Enum(Off,0,On,1)]_ZWrite("ZWrite",float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)]_BlendSrc("BlendSrc", Int) = 0
		[Enum(UnityEngine.Rendering.BlendMode)]_BlendDst("BlendDst", Int) = 0
		_MainTex("AlbadoTex", 2D) = "white" {}
		[HDR]_Color("Color", Color) = (1,0.8307333,0,0)
		_ColorIntensity("ColorIntensity", Float) = 1
		_Rotator("Rotator", Range( 0 , 6.283187)) = 0
		
		//[Toggle(_GRAY)] _Gray("_Gray?", Float) = 0

		_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
		[HideInInspector]_LogicControlAlpha ("Logic control alpha",Range(0,1)) = 1
		
		
	}

	SubShader 
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane"}
		Blend [_BlendSrc] [_BlendDst]
        //Blend SrcAlpha One //Add
		//Blend SrcAlpha OneMinusSrcAlpha //AlphaBlend
		Cull Off 
		ZWrite [_ZWrite]
		ZTest LEqual

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Pass 
		{
			HLSLPROGRAM
			#pragma multi_compile _ _UI_CLIP
			//#pragma multi_compile _ _ENABLE_FOG
            //#pragma multi_compile _ _ENABLE_FOG_TRANS           
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _GRAY

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			//#include "Assets/AssetBundles/Shader/MHU3D/Particles/ParticlesCommon.hlsl"

			TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
			half4 _Color;
			half _LogicControlAlpha;
			half _ColorIntensity;
			half _Rotator;
			CBUFFER_END
			float4 _Area;
			struct Attributes 
			{
				float4 positionOS : POSITION;
				half4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				
			};

			struct Varyings 
			{
				float4 vertex : SV_POSITION;
				half4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				//FOG_COORD(2)
			};
			


			Varyings vert (Attributes input)
			{
				Varyings output;
				
				output.color = input.color * _Color;
				output.texcoord = TRANSFORM_TEX(input.texcoord,_MainTex);

				float cosAngle = cos( _Rotator );
			    float sinAngle = sin( _Rotator );
				// float cosAngle = cos(_Time.w);//取时间当角度
                // float sinAngle = sin(_Time.w);
			    output.texcoord = mul(  TRANSFORM_TEX(input.texcoord,_MainTex) - float2( 0.5,0.5 ) , float2x2( cosAngle , -sinAngle , sinAngle , cosAngle )) + float2( 0.5,0.5 );
				
				output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
				output.vertex = TransformWorldToHClip(output.worldPos.xyz);
				//TRANSFER_FOG(output.vertex.z,output.worldPos,output)
				return output;
			}

			half4 frag (Varyings input) : SV_Target
			{
				float4 Color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
				Color *= _Color * _ColorIntensity * input.color;

				//clip((Color.a * _Clip) - 0.5);
				#ifdef _GRAY
				return half4(Color.rrr, Color.a);
				#endif

				//#ifdef _UI_CLIP 
				// if(input.worldPos.x <= _Area.x || input.worldPos.x >= _Area.y || input.worldPos.y <= _Area.z || input.worldPos.y >= _Area.w)
				// {
				// 	Color.a = 0;
				// }
				//#endif

				return half4(Color.rgb, Color.a);
			}
			ENDHLSL 
		}
	}	
}
//特效通用Shader，支持贴图旋转，可切换Blend模式
//{
//	Properties
//	{
//		//_Clip("Clip", Float) = 0.5
//		[Enum(Off,0,On,1)]_ZWrite("ZWrite",float) = 0
//        [Enum(UnityEngine.Rendering.BlendMode)]_BlendSrc("BlendSrc", Int) = 0
//		[Enum(UnityEngine.Rendering.BlendMode)]_BlendDst("BlendDst", Int) = 0
//		_MainTex("AlbadoTex", 2D) = "white" {}
//		_Color("Color", Color) = (1,0.8307333,0,0)
//		//_ColorIntensity("ColorIntensity", Float) = 1
//		_Rotator("Rotator", Range( 0 , 6.283187)) = 0
//		
//		//[Toggle(_GRAY)] _Gray("_Gray?", Float) = 0
//
//	}
//
//	SubShader
//	{
//		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
//
//		Blend [_BlendSrc] [_BlendDst]
//        //Blend SrcAlpha One //Add
//		//Blend SrcAlpha OneMinusSrcAlpha //AlphaBlend
//		Cull Off 
//		ZWrite [_ZWrite]
//		ZTest LEqual
//		
//		Pass 
//		{
//			HLSLPROGRAM
//			#pragma vertex vert
//			#pragma fragment frag
//		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//			
//			#pragma multi_compile _ _GRAY
//
//			
//			TEXTURE2D (_MainTex); SAMPLER(sampler_MainTex);
//			CBUFFER_START(UnityPerMaterial)
//			uniform float4 _MainTex_ST;
//			uniform half4 _Color;
//			uniform half _ColorIntensity;
//			//uniform float _Clip;
//			uniform half _Rotator;
//			CBUFFER_END
//			
//
//			struct Attributes 
//			{
//				float4 positionOS : POSITION;
//				float4 color : COLOR;
//				float2 uv : TEXCOORD0;
//			};
//
//			struct Varyings 
//			{
//				float4 vertex : SV_POSITION;
//				float4 color : COLOR;
//				float2 uv : TEXCOORD0;
//				float3 worldPos : TEXCOORD1;
//			};
//			
//			Varyings vert (Attributes input)
//			{
//				Varyings output = (Varyings)0;
//				
//				output.color = input.color;
//				float cosAngle = cos( _Rotator );
//			    float sinAngle = sin( _Rotator );
//				// float cosAngle = cos(_Time.w);//取时间当角度
//                // float sinAngle = sin(_Time.w);
//			    output.uv = mul(  TRANSFORM_TEX(input.uv,_MainTex) - float2( 0.5,0.5 ) , float2x2( cosAngle , -sinAngle , sinAngle , cosAngle )) + float2( 0.5,0.5 );
//				//output.worldPos = mul( unity_ObjectToWorld, input.positionOS).xyz;  //mul(unity_ObjectToWorld, input.positionOS);
//				//output.vertex = UnityWorldToClipPos(output.worldPos.xyz);
//				output.worldPos =TransformObjectToWorld(input.positionOS.xyz); 
//				output.vertex = TransformWorldToHClip(output.worldPos.xyz);
//				return output;
//			}
//
//			half4 frag (Varyings input) : SV_Target
//			{
//				float4 Color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
//				Color *= _Color * _ColorIntensity * input.color;
//
//				//clip((Color.a * _Clip) - 0.5);
//				#ifdef _GRAY
//				return half4(Color.rrr, Color.a);
//				#endif
//
//				return half4(Color.rgb, Color.a);
//				
//			}
//			ENDHLSL
//		}
//	}
//}

