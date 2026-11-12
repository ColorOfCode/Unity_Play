Shader "ObscuredByClouds/TimeOfDay Procedural Sky"
{
    Properties
    {
        [Header(Sky Color)] [HDR] _HorizonColor("HorizonColor", Color) = (.5,.6,.8,1)
        [HDR] _ZenithColor("ZenithColor", Color) = (.1,.3,.6,1)
        _HorizonFalloff("HorizonFalloff", Float) = 1
        _HorizonOffset("HorizonOffset", Float) = -.2
        _HorizonTilt("HorizonTilt", Float) = .2
        [Header(Sun Color)] _SunDirection("Sun Direction", Vector) = (0,1,0,0)
        [HDR] _SunColor("SunColor", Color) = (1,1,1,1)
        _SunIntensity("Sun Intensity", Float) = 1
        _SunRadius("Sun Radius", Float) = .02
        _SunBloom("Sun Bloom", Float) = 1.27
        _SunScattering("Sun Scattering", Float) = .3
        [Header(Moon Color)] _MoonDirection("MoonDirection", Vector) = (0,1,0,0)
        [NoScaleOffset] _MoonTex("MoonTex", 2D) = "white" {}
        [HDR] _MoonColor("MoonColor", Color) = (1,1,1,1)
        _MoonIntensity("MoonIntensity", Float) = .5
        _MoonSize("Moon Size", Float) = 2
        _MoonBloom("Moon Bloom", Float) = 5
        _MoonScattering("Moon Scattering", Float) = .5
        [Header(Static Background)] [NoScaleOffset] _BackGroundTex("BackGround Tex (R Clouds G Stars B Twinkle)", 2D) = "black" {}
        [HDR] _CloudsBackgroundColor("Clouds Background Color", Color) = (1,1,1,1)
        _CloudsBackgroundBrightness("Clouds Background Brightness", Float) = 1
        [HDR] _StarsColor("Stars Color", Color) = (1,1,1,1)
        _StarsIntensity("Stars Intensity", Float) = 100
        _StarTilling("StarTilling", Vector) = (2,2,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TimeOfDaySky.hlsl"
            TEXTURE2D(_BackGroundTex); SAMPLER(sampler_BackGroundTex);
            TEXTURE2D(_MoonTex); SAMPLER(sampler_MoonTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _HorizonColor, _ZenithColor, _SunDirection, _SunColor, _MoonDirection, _MoonColor, _CloudsBackgroundColor, _StarsColor, _StarTilling;
            float _HorizonFalloff, _HorizonOffset, _HorizonTilt, _SunIntensity, _SunRadius, _SunBloom, _SunScattering;
            float _MoonIntensity, _MoonSize, _MoonBloom, _MoonScattering, _CloudsBackgroundBrightness, _StarsIntensity;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS); output.direction = input.positionOS; return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_ObcTodEnabled > .5) return half4(EvaluateObcTimeOfDaySky(input.direction), 1);
                ObcProceduralSky p;
                p.horizon = _HorizonColor.rgb; p.zenith = _ZenithColor.rgb;
                p.sunDirection = normalize(_SunDirection.xyz); p.moonDirection = normalize(_MoonDirection.xyz);
                p.sunColor = _SunColor.rgb; p.moonColor = _MoonColor.rgb;
                p.falloff = _HorizonFalloff; p.tilt = _HorizonTilt; p.offset = _HorizonOffset;
                p.sunRadius = _SunRadius; p.moonSize = _MoonSize; p.sunBloom = _SunBloom; p.moonBloom = _MoonBloom;
                p.sunScatter = _SunScattering; p.moonScatter = _MoonScattering; p.sunIntensity = _SunIntensity; p.moonIntensity = _MoonIntensity;
                p.cloudColor = _CloudsBackgroundColor.rgb; p.cloudBrightness = _CloudsBackgroundBrightness;
                p.stars = _StarsColor.rgb * _StarsIntensity; p.starTilling = _StarTilling.xy;
                return half4(EvaluateProceduralSky(input.direction, p,
                    TEXTURE2D_ARGS(_BackGroundTex, sampler_BackGroundTex), TEXTURE2D_ARGS(_MoonTex, sampler_MoonTex)), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
