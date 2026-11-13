// Water lighting and internal-reflection algorithms adapted from Crest Ocean System.
// Copyright 2020 Wave Harmonic Ltd.
// Mirror blending follows the screen-space captured-image / blend-weight approach
// of DemoScene01's R1_MirrorPlane3; it uses the existing water normal and colour.

float _ShuimianMirrorStrength;
float _ShuimianReflectSky;
float _ShuimianMirrorNormalInfluence;
float4 _ShuimianMirrorUVScaleOffset;
float4 _ShuimianMirrorDistanceParams;
half4 _ShuimianMirrorFadeColor;
float _ShuimianMirrorRecursiveDarkening;
float4 _ShuimianMirrorRecursionData;
// Curves and reflection captures all use bilinear clamping.
SamplerState sampler_ShuiCapture_linear_clamp;
Texture2D _ShuimianMirrorDistanceCurve;
Texture2D _ShuimianMirrorRecursiveCurve;
float _ShuimianUseReflectionCoverage;
Texture2D _ShuimianReflectionCoverage;
float _ShuimianReflectionPostProcessingStrength;
Texture2D _ShuimianReflectionUnprocessed;

float SampleShuimianMirrorCurve(Texture2D curve, float t)
{
    // Key endpoints map to texel centres, so editable Unity curves retain their
    // exact endpoints rather than acquiring a half-texel plateau.
    return saturate(curve.Sample(sampler_ShuiCapture_linear_clamp, float2(saturate(t) * (255.0 / 256.0) + 0.5 / 256.0, 0.5)).r);
}

float ShuimianMirrorDistanceAlpha(float3 positionWS)
{
    float t = distance(positionWS, _WorldSpaceCameraPos) / max(_ShuimianMirrorDistanceParams.x, 0.01);
    return t >= 1.0 ? 0.0 : SampleShuimianMirrorCurve(_ShuimianMirrorDistanceCurve, t);
}

float ShuimianMirrorSurfaceBlend()
{
    float maxDistance = max(_ShuimianMirrorDistanceParams.x, 0.01);
    float fadeFraction = saturate(_ShuimianMirrorDistanceParams.y);
    float cameraDistance = _ShuimianMirrorDistanceParams.w;
    float blend = fadeFraction > 0.0 ? saturate((maxDistance - cameraDistance) / (maxDistance * fadeFraction))
        : (cameraDistance < maxDistance ? 1.0 : 0.0);
    float depth = _ShuimianMirrorRecursionData.x;
    float recursions = max(_ShuimianMirrorRecursionData.y, 1.0);
    if (depth > recursions) return 0.0;
    if (_ShuimianMirrorRecursiveDarkening > 0.5 && depth > 1.0)
        blend = SampleShuimianMirrorCurve(_ShuimianMirrorRecursiveCurve, 1.0 - (depth - 1.0) / recursions);
    return blend;
}

#if _PROCEDURALSKY_ON
half3 SkyProceduralDP(in const half3 i_refl, in const half3 i_lightDir)
{
    half dp = dot(i_refl, i_lightDir);
    if (dp > _SkyDirectionality)
    {
        dp = (dp - _SkyDirectionality) / (1. - _SkyDirectionality);
        return lerp(_SkyBase, _SkyTowardsSun, dp);
    }
    dp = (dp - -1.0) / (_SkyDirectionality - -1.0);
    return lerp(_SkyAwayFromSun, _SkyBase, dp);
}
#endif

#if _PLANARREFLECTIONS_ON
half4 SampleShuimianMirror(in const float4 screenPosition, in const half3 normalWS, in const float pixelZ)
{
    float2 uv = screenPosition.xy / max(screenPosition.w, 0.0001);
    // Project only deviations from a flat water normal into the camera plane.
    // All wave and fine-detail normals still come from the water's own shader.
    float3 perturbationVS = mul((float3x3)UNITY_MATRIX_V, float3(normalWS.x, 0.0, normalWS.z));
    float2 offset = perturbationVS.xy;
    offset.y *= _ProjectionParams.x;
    offset *= max(1.0, pixelZ * _PlanarReflectionDistanceFactor) * _PlanarReflectionNormalsStrength / max(pixelZ, 1.0);
    // The capture covers a larger frustum. Its padding contains the distorted
    // edge pixels, so no screen-shaped alpha fade or clamped edge streak occurs.
    uv = (uv + clamp(offset, -0.05, 0.05) * saturate(_ShuimianMirrorNormalInfluence))
        * _ShuimianMirrorUVScaleOffset.xy + _ShuimianMirrorUVScaleOffset.zw;
    half4 reflection = tex2D(_ReflectionTex, uv);
    // Both captures share the same projection and frame. Mix only colour;
    // geometric coverage comes from before post-processing below.
    if (_ShuimianReflectionPostProcessingStrength < 1.0)
        reflection.rgb = lerp(_ShuimianReflectionUnprocessed.Sample(sampler_ShuiCapture_linear_clamp, uv).rgb, reflection.rgb,
            saturate(_ShuimianReflectionPostProcessingStrength));
    // URP post-processing can replace alpha with one. In that mode the capture
    // supplies coverage from before post-processing, at the identical UV.
    half coverage = _ShuimianUseReflectionCoverage > 0.5
        ? _ShuimianReflectionCoverage.Sample(sampler_ShuiCapture_linear_clamp, uv).a : reflection.a;
    reflection.a = saturate(coverage);
    return reflection;
}
#endif

float CalculateFresnelReflectionCoefficient(float cosTheta)
{
    float R_0 = (_RefractiveIndexOfAir - _RefractiveIndexOfWater) / (_RefractiveIndexOfAir + _RefractiveIndexOfWater);
    R_0 *= R_0;
    return R_0 + (1.0 - R_0) * pow(max(0., 1.0 - cosTheta), _FresnelPower);
}

void ApplyReflectionSky
(
    in const half3 i_view,
    in const half3 i_n_pixel,
    in const half3 i_lightDir,
    in const half i_shadow,
    in const float4 i_screenPos,
    in const float i_pixelZ,
    in const half i_weight,
    in const float3 i_positionWS,
    inout half3 io_col
)
{
    // The original Water environment reflection is part of its base shading.
    // Disabling the additional mirror must retain this Fresnel sky/probe colour
    // and its normal response, otherwise the ocean becomes unnaturally dark.
    half3 skyColour;
    half3 refl = reflect(-i_view, i_n_pixel);
    refl.y = max(refl.y, 0.0);
    const real mip = _ReflectionBlur;
#if _PROCEDURALSKY_ON
        skyColour = SkyProceduralDP(refl, i_lightDir);
#else
#if USE_FORWARD_PLUS
        skyColour = SAMPLE_TEXTURECUBE_LOD(_GlossyEnvironmentCubeMap, sampler_GlossyEnvironmentCubeMap, refl, mip).rgb;
#else
        half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, refl, mip);
#if !defined(UNITY_USE_NATIVE_HDR)
        skyColour = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
#else
        skyColour = encodedIrradiance.rbg;
#endif
#endif
#endif
    // Baked probes retain the daytime sky. The day/night water samples the
    // current HDR sky and its palette directly, independent of probe refresh.
    if (_ObcTodEnabled > .5 && _ShuimianHorizonParams.x > .5)
        skyColour = SampleObcWaterSky(refl, mip);
    // Keep the original Water lighting order: lighting joins the environment,
    // then the complete result is mixed through Fresnel once.
    float smoothness = _Smoothness;
#if _VARYSMOOTHNESSOVERDISTANCE_ON
    smoothness = lerp(smoothness, _SmoothnessFar, pow(saturate(i_pixelZ / _SmoothnessFarDistance), _SmoothnessPower));
#endif
    half alpha = 1.0;
    BRDFData brdfData;
    InitializeBRDFData(0.0, 0.0, 1.0, smoothness, alpha, brdfData);
    const Light mainLight = GetMainLight();
    half3 lightColor = LightingPhysicallyBased(brdfData, mainLight, i_n_pixel, i_view) * i_shadow * _LightIntensityMultiplier;
#if !USE_FORWARD_PLUS
#ifdef _ADDITIONAL_LIGHTS
    int pixelLightCount = GetAdditionalLightsCount();
    for (int i = 0; i < pixelLightCount; ++i)
    {
        Light light = GetAdditionalLight(i, i_positionWS, unity_ProbesOcclusion);
        lightColor += LightingPhysicallyBased(brdfData, light, i_n_pixel, i_view);
    }
#endif
#endif
    skyColour *= OBCWaterIndirectStrength();
    skyColour += lightColor;
    float R_theta = CalculateFresnelReflectionCoefficient(max(dot(i_n_pixel, i_view), 0.0));
    io_col = lerp(io_col, skyColour, R_theta * _Specular * i_weight);

#if _PLANARREFLECTIONS_ON
    float mirrorStrength = saturate(_ShuimianMirrorStrength) * saturate(_ShuimianMirrorDistanceParams.z)
        * ShuimianMirrorDistanceAlpha(i_positionWS);
    if (mirrorStrength > 0.0)
    {
        half4 reflected = SampleShuimianMirror(i_screenPos, i_n_pixel, i_pixelZ);
        float surfaceBlend = ShuimianMirrorSurfaceBlend();
        // R1's distance/recursive blend fades the captured colour towards its
        // Fade Color. The additional per-pixel curve controls alpha separately.
        // Transparent Fade Color returns to water; neither fade nor sky escapes
        // the master strength, maximum blend and editable distance-alpha curve.
        half3 fadeBase = lerp(io_col, _ShuimianMirrorFadeColor.rgb, saturate(_ShuimianMirrorFadeColor.a));
        half3 mirrorColour = lerp(fadeBase, reflected.rgb * OBCWaterIndirectStrength(), surfaceBlend);
        io_col = lerp(io_col, mirrorColour, mirrorStrength * reflected.a * saturate(i_weight));
    }
#endif
}

// Foam remains an opaque, lit layer with its original pattern and alpha.
// Modulate that lighting by the same mirror image, rather than laying unrelated
// pure-white foam over it or making the reflected image erase the foam.
void ApplyShuimianMirrorFoam
(
    in const float4 i_screenPos,
    in const half3 i_n_pixel,
    in const float i_pixelZ,
    in const half i_weight,
    in const float3 i_positionWS,
    inout real4 io_foam
)
{
#if _PLANARREFLECTIONS_ON
    float mirrorStrength = saturate(_ShuimianMirrorStrength) * saturate(_ShuimianMirrorDistanceParams.z)
        * ShuimianMirrorDistanceAlpha(i_positionWS);
    if (io_foam.a > 0.0 && mirrorStrength > 0.0)
    {
        half4 reflected = SampleShuimianMirror(i_screenPos, i_n_pixel, i_pixelZ);
        float weight = mirrorStrength * reflected.a * saturate(i_weight);
        // A reflection can be HDR-bright or almost black. Bound the multiplier
        // to preserve the lit foam detail without clipping it or removing it.
        half3 reflectedTint = clamp(reflected.rgb, 0.25, 1.0);
        half3 fadeTint = lerp(half3(1.0, 1.0, 1.0), clamp(_ShuimianMirrorFadeColor.rgb, 0.25, 1.0),
            saturate(_ShuimianMirrorFadeColor.a));
        half3 tint = lerp(fadeTint, reflectedTint, ShuimianMirrorSurfaceBlend());
        io_foam.rgb *= lerp(half3(1.0, 1.0, 1.0), tint, weight);
    }
#endif
}

// The original Snell-window and internal-reflection branch is preserved for
// viewing the animated surface from beneath; planar mirrors apply above water.
#if _UNDERWATER_ON
void ApplyReflectionUnderwater
(
    in const half3 i_view,
    in const half3 i_n_pixel,
    in const half3 i_lightDir,
    in const half i_shadow,
    in const half4 i_screenPos,
    half3 scatterCol,
    in const half i_weight,
    inout half3 io_col
)
{
    const half3 underwaterColor = scatterCol;
    const float cosOutgoingAngle = max(dot(i_n_pixel, i_view), 0.);
    {
        const float cosIncomingAngle = cos(asin(clamp((_RefractiveIndexOfWater * sin(acos(cosOutgoingAngle))) / _RefractiveIndexOfAir, -1.0, 1.0)));
        const float reflectionCoefficient = CalculateFresnelReflectionCoefficient(cosIncomingAngle) * i_weight;
        io_col *= 1.0 - reflectionCoefficient;
        io_col = max(io_col, 0.0);
    }
    {
        const float reflectionCoefficient = CalculateFresnelReflectionCoefficient(cosOutgoingAngle) * i_weight;
        io_col += underwaterColor * reflectionCoefficient;
    }
}
#endif
