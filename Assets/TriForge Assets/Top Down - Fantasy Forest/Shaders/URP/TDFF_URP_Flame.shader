// ============================================================================
// TriForge/URP/Flame
// URP 12 vertex/fragment replacement for the "Flame" shader
// (Assets/TriForge Assets/Top Down - Fantasy Forest/Shaders/Flame.shader).
//
// Ported features (1:1 from the ASE surface function):
//   * Simplex-noise UV distortion scrolled over time / world position
//   * Static voronoi dissolve mask multiplied by the animated noise
//   * HDR tint * particle vertex color (emission look, unlit)
//   * Depth fade: alpha softens near geometry (needs URP Depth Texture)
// Property names are identical to the original shader.
// ============================================================================
Shader "TriForge/URP/Flame"
{
    Properties
    {
        _MainTex("Particle Texture", 2D) = "white" {}
        [HDR]_TintColor("Tint Color", Color) = (0.5294118, 0.5294118, 0.5294118, 0)
        _DistortionStrength("Distortion Strength", Range(0.0, 1.0)) = 0.1126335
        _DissolveStrength("Dissolve Strength", Range(0.0, 10.0)) = 1
        _DissolveScale("Dissolve Scale", Range(0.0, 10.0)) = 2
        _DistortionScale("Distortion Scale", Range(0.0, 10.0)) = 2
        _EffectSpeed("Effect Speed", Range(0.1, 10.0)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "TDFF_Shared.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half _DistortionStrength;
                half _DissolveStrength;
                half _DissolveScale;
                half _DistortionScale;
                half _EffectSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;   // NDC: xy / w = screen UV, zw = clip zw
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.screenPos  = posInputs.positionNDC;
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 worldPos = input.positionWS;
                float2 uv = input.uv;

                // time + world offset (1:1 from the original)
                float mulTime = _Time.y * _EffectSpeed;
                float3 temp50 = mulTime + worldPos * 2.0;

                // simplex distortion noise
                float2 uvDistort = uv + float2(0.0, -0.3 * temp50.y);
                float perlin = snoise2D(uvDistort * _DistortionScale) * 0.5 + 0.5;

                // distorted UV lerp + texture sample
                float2 lerpedUV = lerp(uv, perlin.xx, _DistortionStrength);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, lerpedUV);

                // static voronoi dissolve
                float2 coords = uv * _DissolveScale;
                float2 vorId = 0;
                float vor = voronoi23(coords, 0.0, vorId);
                float dissolve = perlin * pow(vor, _DissolveStrength);

                // color: tex * dissolve * HDR tint * particle vertex color
                half4 outCol = (tex * dissolve) * _TintColor * input.color;

                // depth fade (alpha softens near geometry)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneEyeZ = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragEyeZ  = LinearEyeDepth(input.screenPos.z / input.screenPos.w, _ZBufferParams);
                float distanceDepth = abs((sceneEyeZ - fragEyeZ) / 3.0);

                half alpha = saturate(tex.a * dissolve * input.color.a * distanceDepth);

                half3 rgb = MixFog(outCol.rgb, input.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
