#ifndef OBC_TIME_OF_DAY_SKY_INCLUDED
#define OBC_TIME_OF_DAY_SKY_INCLUDED
float _ObcTodEnabled;
float4 _ObcTodHorizonColor, _ObcTodZenithColor, _ObcTodSkyParams;
float4 _ObcTodSunDirection, _ObcTodMoonDirection, _ObcTodSunColor, _ObcTodMoonColor;
float4 _ObcTodDiscParams, _ObcTodScatterParams, _ObcTodStars, _ObcTodCloudColor, _ObcTodBackgroundParams;
TEXTURE2D(_ObcTodMoonTex); SAMPLER(sampler_ObcTodMoonTex);
TEXTURE2D(_ObcTodBackgroundTex); SAMPLER(sampler_ObcTodBackgroundTex);
struct ObcProceduralSky
{
    float3 horizon, zenith, sunDirection, moonDirection, sunColor, moonColor, cloudColor, stars;
    float falloff, tilt, offset, sunIntensity, moonIntensity, sunRadius, moonSize, sunBloom, moonBloom, sunScatter, moonScatter, cloudBrightness;
    float2 starTilling;
};
float3 EvaluateProceduralSky(float3 direction, ObcProceduralSky p,
    TEXTURE2D_PARAM(background, backgroundSampler), TEXTURE2D_PARAM(moon, moonSampler))
{
    float3 ray = normalize(direction);
    float sunDot = dot(ray, p.sunDirection), moonDot = dot(ray, p.moonDirection);
    float3 sun = p.sunColor * p.sunIntensity * (p.sunBloom * .01)
        / sqrt(max((1 - sunDot) * .5 - p.sunRadius * .01, .0002));
    sun *= sun;
    float horizon = pow(saturate(1 - ray.y * p.falloff + dot(ray.xz, p.sunDirection.xz) * p.tilt + p.offset), 4);
    float3 sky = lerp(p.zenith, p.horizon, horizon);
    float3 sunScatter = p.sunColor * pow(saturate(sunDot), 4) * p.sunScatter * .5;
    float3 moonScatter = p.moonColor * pow(saturate(moonDot), 30) * p.moonScatter * .5;

    // Unbounded positive size uses perspective projection. Increasing size
    // keeps enlarging the disc, without the old lerp(20,2,size) reversal.
    float3 moonRight = normalize(cross(abs(p.moonDirection.y) > .99 ? float3(0,0,1) : float3(0,1,0), p.moonDirection));
    float3 moonUp = cross(p.moonDirection, moonRight);
    float2 uv = float2(dot(ray, moonRight), dot(ray, moonUp))
        / (max(moonDot, .00001) * max(p.moonSize, .0001) * .08) + .5;
    float inside = step(0, moonDot) * step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
    float4 moonTex = SAMPLE_TEXTURE2D_LOD(moon, moonSampler, saturate(uv), 0) * inside * saturate(moonDot);
    float3 moonGlow = p.moonColor * (p.moonBloom * .01) / sqrt(max((1 - moonDot) * .5, .0002));
    float3 moonColor = moonTex.rgb * p.moonColor * p.moonIntensity * 10 + moonGlow * moonGlow * (1 - moonTex.a * .5);

    // Original T_Background packing: R=cloud mask, G=stars, B=twinkle phase.
    float2 backgroundUV = float2(-atan2(ray.z, ray.x) / (2 * PI), -acos(clamp(ray.y, -1, 1)) / (.5 * PI));
    float cloud = SAMPLE_TEXTURE2D_LOD(background, backgroundSampler, backgroundUV, 0).r;
    float4 starTex = SAMPLE_TEXTURE2D_LOD(background, backgroundSampler, backgroundUV * p.starTilling, 0);
    float3 stars = p.stars * pow(starTex.g, 4) * (sin((starTex.b * 3 + _Time.y * .5) * 2 * PI) + 1);
    float cloudMask = saturate(cloud * cloud * p.cloudBrightness);
    float3 color = sun + moonColor + lerp(sky + sunScatter + moonScatter + stars,
        p.cloudColor + sunScatter + moonScatter, cloudMask);
    return max(color, 0);
}
ObcProceduralSky ObcGlobalSkySettings()
{
    ObcProceduralSky p;
    p.horizon = _ObcTodHorizonColor.rgb; p.zenith = _ObcTodZenithColor.rgb;
    p.sunDirection = _ObcTodSunDirection.xyz; p.moonDirection = _ObcTodMoonDirection.xyz;
    p.sunColor = _ObcTodSunColor.rgb; p.moonColor = _ObcTodMoonColor.rgb;
    p.falloff = _ObcTodSkyParams.x; p.tilt = _ObcTodSkyParams.y; p.offset = _ObcTodSkyParams.z;
    p.sunRadius = _ObcTodDiscParams.x; p.moonSize = _ObcTodDiscParams.y; p.sunBloom = _ObcTodDiscParams.z; p.moonBloom = _ObcTodDiscParams.w;
    p.sunScatter = _ObcTodScatterParams.x; p.moonScatter = _ObcTodScatterParams.y; p.sunIntensity = _ObcTodScatterParams.z; p.moonIntensity = _ObcTodScatterParams.w;
    p.cloudColor = _ObcTodCloudColor.rgb; p.cloudBrightness = _ObcTodBackgroundParams.z;
    p.stars = _ObcTodStars.rgb; p.starTilling = _ObcTodBackgroundParams.xy;
    return p;
}
float3 EvaluateObcTimeOfDaySky(float3 direction)
{
    return EvaluateProceduralSky(direction, ObcGlobalSkySettings(),
        TEXTURE2D_ARGS(_ObcTodBackgroundTex, sampler_ObcTodBackgroundTex), TEXTURE2D_ARGS(_ObcTodMoonTex, sampler_ObcTodMoonTex));
}
#endif
