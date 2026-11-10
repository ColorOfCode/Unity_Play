Shader "ObscuredByClouds/Permanent Fog Of War"
{
    Properties
    {
        [HideInInspector] _FogHeight ("Plane Height (from Fog Of War Surface)", Float) = 32
        [HideInInspector] _HeightFade ("Contact Depth Fade (from Fog Of War Surface)", Float) = 2
        _Opacity ("Fog Opacity", Range(0,1)) = 1
        [Header(Cloud Appearance)]
        _NoiseTex ("Noise Texture (Repeat)", 2D) = "gray" {}
        _NoiseWorldSize ("Noise Size X Z (metres)", Vector) = (12,12,0,0)
        _FlowA ("UV Flow A (UV per second)", Vector) = (0.015,0.008,0,0)
        _FlowB ("UV Flow B (UV per second)", Vector) = (-0.009,0.012,0,0)
        _LayerScale ("Second Noise Layer Scale", Range(0.2,5)) = 1.73
        _NoiseContrast ("Noise Contrast", Range(0.1,4)) = 1.6
        _NoiseBrightness ("Noise Brightness", Range(-1,1)) = 0
        _ShadowColor ("Cloud Shadow Color", Color) = (0.23,0.28,0.36,1)
        _CloudColor ("Cloud Light Color", Color) = (0.72,0.78,0.84,1)
        [HideInInspector] _ExploredMask ("Exploration", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+50" }
        Pass
        {
            Name "LevelCloudSurface"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_ExploredMask); SAMPLER(sampler_ExploredMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _NoiseTex_ST, _NoiseWorldSize, _FlowA, _FlowB;
            half4 _ShadowColor, _CloudColor;
            float _FogHeight, _HeightFade, _Opacity, _LayerScale, _NoiseContrast, _NoiseBrightness;
            CBUFFER_END
            float4 _MapBounds;
            float _FogTime;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS); return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // All noise and exploration UVs lie on the same horizontal plane, never on terrain.
                float2 maskUV = (input.positionWS.xz - _MapBounds.xy) / _MapBounds.zw;
                float inside = step(0,maskUV.x)*step(0,maskUV.y)*step(maskUV.x,1)*step(maskUV.y,1);
                float explored = SAMPLE_TEXTURE2D(_ExploredMask,sampler_ExploredMask,saturate(maskUV)).r * inside;
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(screenUV);
                float heightVisibility = 1;
                #if UNITY_REVERSED_Z
                    bool hasSurface = depth > 0.00001;
                #else
                    bool hasSurface = depth < 0.99999;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                if (hasSurface)
                {
                    float3 surface = ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
                    // Shallow-water style contact fade: transparent at contact, dense below the plane.
                    float contactDepth = max(0, _FogHeight - surface.y);
                    heightVisibility = smoothstep(0, max(0.001, _HeightFade), contactDepth);
                }
                float2 uv = input.positionWS.xz / max(float2(0.1,0.1),abs(_NoiseWorldSize.xy));
                uv = uv * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                float a = SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv+_FlowA.xy*_FogTime).r;
                float b = SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,uv*_LayerScale+_FlowB.xy*_FogTime+0.37).r;
                float n = saturate(((a+b)*0.5-0.5)*_NoiseContrast+0.5+_NoiseBrightness);
                half3 color = lerp(_ShadowColor.rgb,_CloudColor.rgb,smoothstep(0,1,n));
                return half4(color,(1-explored)*heightVisibility*_Opacity);
            }
            ENDHLSL
        }
    }
}
