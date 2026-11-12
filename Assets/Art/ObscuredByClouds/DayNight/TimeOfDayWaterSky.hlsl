#ifndef OBC_TIME_OF_DAY_WATER_SKY_INCLUDED
#define OBC_TIME_OF_DAY_WATER_SKY_INCLUDED
#include "TimeOfDaySky.hlsl"
TEXTURECUBE(_ShuimianHorizonSky);
SAMPLER(sampler_ShuimianHorizonSky);
float4 _ShuimianHorizonSkyHDR, _ShuimianHorizonSkyTint, _ShuimianHorizonParams;
float _ShuimianHorizonProcedural;
float3 SampleObcWaterSky(float3 ray, float mip)
{
    ray = normalize(ray);
    if (_ShuimianHorizonProcedural > .5) return EvaluateObcTimeOfDaySky(ray);
    float s, c; sincos(_ShuimianHorizonParams.w, s, c);
    float3 skyRay = float3(c * ray.x - s * ray.z, ray.y, s * ray.x + c * ray.z);
    half4 encoded = SAMPLE_TEXTURECUBE_LOD(_ShuimianHorizonSky, sampler_ShuimianHorizonSky, skyRay, mip);
    return DecodeHDREnvironment(encoded, _ShuimianHorizonSkyHDR) * _ShuimianHorizonSkyTint.rgb;
}
#endif
