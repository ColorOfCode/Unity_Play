Shader "URP/FX/Particle_common"
//特效通用Shader，支持贴图旋转，可切换Blend模式
{
	Properties
	{
		//_Clip("Clip", Float) = 0.5
		[Enum(Off,0,On,1)]_ZWrite("ZWrite",float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)]_BlendSrc("BlendSrc", Int) = 0
		[Enum(UnityEngine.Rendering.BlendMode)]_BlendDst("BlendDst", Int) = 0
		_AlbadoTex("AlbadoTex", 2D) = "white" {}
		_Color("Color", Color) = (1,1.1,1)
		_ColorIntensity("ColorIntensity", Float) = 1
		_Rotator("Rotator", Range( 0 , 6.283187)) = 0

	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
		

		Blend [_BlendSrc] [_BlendDst]
        //Blend SrcAlpha One //Add
		//Blend SrcAlpha OneMinusSrcAlpha //AlphaBlend
		Cull Off 
		ZWrite [_ZWrite]
		ZTest LEqual
		
		Pass 
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			uniform sampler2D _AlbadoTex;

			CBUFFER_START(UnityPerMaterial)
			uniform float4 _AlbadoTex_ST;
			uniform half4 _Color;
			uniform half _ColorIntensity;
			//uniform float _Clip;
			uniform float _Rotator;
			CBUFFER_END

			struct Attributes 
			{
				float4 positionOS : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct Varyings 
			{
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
				//float3 worldPos : TEXCOORD1;
			};
			
			Varyings vert (Attributes input)
			{
				Varyings output = (Varyings)0;
				
				output.color = input.color;
				float cosAngle = cos( _Rotator );
			    float sinAngle = sin( _Rotator );
				// float cosAngle = cos(_Time.w);//取时间当角度
                // float sinAngle = sin(_Time.w);
			    output.uv = mul(  TRANSFORM_TEX(input.uv,_AlbadoTex) - float2( 0.5,0.5 ) , float2x2( cosAngle , -sinAngle , sinAngle , cosAngle )) + float2( 0.5,0.5 );
				//output.worldPos = mul( unity_ObjectToWorld, input.positionOS).xyz;  //mul(unity_ObjectToWorld, input.positionOS);
				//output.vertex = TransformWorldToHClip(output.worldPos.xyz);
				output.vertex = TransformObjectToHClip(input.positionOS);
				return output;
			}

			half4 frag (Varyings input) : SV_Target
			{
				float4 Color = tex2D( _AlbadoTex, input.uv );
				Color *= _Color * _ColorIntensity * input.color;
				Color.a = saturate(Color.a);

				//clip((Color.a * _Clip) - 0.5);
				return half4(Color.rgb, Color.a);
			}
			ENDHLSL
		}
	}
}

