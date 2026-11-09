// ============================================================================
// TriForge/URP/Particles
// URP 12 vertex/fragment replacement for the Built-in "Particles/Standard
// Shader" used by the Top Down - Fantasy Forest particle materials.
// Unlit transparent particle shader:
//   color = texture * vertex color * _Color
//   optional depth soft fade (_SOFTPARTICLES_ON, needs URP Depth Texture)
//   optional camera distance fade (_FADING_ON)
// Blend / ZWrite / Cull come from the preserved _SrcBlend/_DstBlend/_ZWrite/
// _Cull floats stored in the original materials.
// ============================================================================
Shader "TriForge/URP/Particles"
{
    Properties
    {
        _MainTex("Particle Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _TintColor("Tint Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _SrcBlend("SrcBlend", Float) = 5
        [HideInInspector] _DstBlend("DstBlend", Float) = 10
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0
        [HideInInspector] _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 0.5
        [HideInInspector] _CameraNearFadeDistance("Camera Near Fade", Float) = 1
        [HideInInspector] _CameraFarFadeDistance("Camera Far Fade", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "DisableBatching" = "True" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog

            // Keywords carried by the original particle materials
            #pragma shader_feature_local_fragment _ALPHABLEND_ON
            #pragma shader_feature_local_fragment _FADING_ON
            #pragma shader_feature_local_fragment _SOFTPARTICLES_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _TintColor;
                half _Cutoff;
                half _SrcBlend;
                half _DstBlend;
                half _ZWrite;
                half _Cull;
                half4 _SoftParticleFadeParams;
                half4 _CameraFadeParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;   // NDC: xy / w = screen UV, z / w = NDC depth
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.screenPos  = posInputs.positionNDC;   // xyzw: xy = ndc*w, z = clip z, w = clip w
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 col = tex * input.color * _Color;

            #if defined(_SOFTPARTICLES_ON)
                // depth-based soft particles (needs Depth Texture enabled in URP asset)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thisZ  = LinearEyeDepth(input.screenPos.z / input.screenPos.w, _ZBufferParams);
                float softFade = saturate((sceneZ - thisZ - _SoftParticleFadeParams.x) * _SoftParticleFadeParams.y);
                col.a *= softFade;
            #endif

            #if defined(_FADING_ON)
                // camera distance fade
                float camDist = length(input.positionWS - _WorldSpaceCameraPos.xyz);
                float camFade = saturate((camDist - _CameraFadeParams.x) * _CameraFadeParams.y);
                col.a *= camFade;
            #endif

                col.rgb = MixFog(col.rgb, input.fogFactor);
                return half4(col.rgb, col.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
