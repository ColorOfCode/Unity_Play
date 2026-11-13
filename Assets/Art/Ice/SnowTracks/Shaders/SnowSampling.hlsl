#ifndef SNOWFIELD_SAMPLING_INCLUDED
#define SNOWFIELD_SAMPLING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(_SnowTrail0);TEXTURE2D(_SnowTrail1);TEXTURE2D(_SnowTrail2);
SAMPLER(sampler_SnowTrail0);
float4 _SnowMap0, _SnowMap1, _SnowMap2;
float4 _SnowShape; // depression, rim, fresh boost, normal strength
float4 _SnowLook; // darkening, local shadow, tessellation, enabled
float4 _SnowFocus;
TEXTURE2D(_SnowDepthMap);SAMPLER(sampler_SnowDepthMap);
float4 _SnowDepthBounds,_SnowDepthConfig;
float SnowDepth(float3 world)
{
    if(_SnowDepthConfig.w<.5)return 0;
    float2 uv=(world.xz-_SnowDepthBounds.xy)*_SnowDepthBounds.zw;
    if(any(uv<0)||any(uv>1))return _SnowDepthConfig.y;
    return SAMPLE_TEXTURE2D_LOD(_SnowDepthMap,sampler_SnowDepthMap,uv,0).r*_SnowDepthConfig.x;
}
float SnowEdge(float2 p,float4 map)
{
    float d=max(abs(p.x-map.x),abs(p.y-map.y))*map.z*2;
    return 1-smoothstep(.76,.98,d);
}
float4 SnowGate(float4 s,float3 world)
{
    float valid=1-smoothstep(.65,1.25,abs(world.y-(s.b*512-256)));
    return s*valid;
}
float4 SnowSample(float3 world)
{
    if(_SnowLook.w<.5)return 0;
    float2 uv0=(world.xz-_SnowMap0.xy)*_SnowMap0.z+.5;
    float2 uv1=(world.xz-_SnowMap1.xy)*_SnowMap1.z+.5;
    float2 uv2=(world.xz-_SnowMap2.xy)*_SnowMap2.z+.5;
    float4 a=SnowGate(SAMPLE_TEXTURE2D_LOD(_SnowTrail0,sampler_SnowTrail0,uv0,0),world);
    float4 b=SnowGate(SAMPLE_TEXTURE2D_LOD(_SnowTrail1,sampler_SnowTrail0,uv1,0),world);
    float4 c=SnowGate(SAMPLE_TEXTURE2D_LOD(_SnowTrail2,sampler_SnowTrail0,uv2,0),world)*SnowEdge(world.xz,_SnowMap2);
    return lerp(lerp(c,b,SnowEdge(world.xz,_SnowMap1)),a,SnowEdge(world.xz,_SnowMap0))*smoothstep(.006,.025,SnowDepth(world));
}
float SnowHeight(float3 world)
{
    float depth=SnowDepth(world);float4 s=SnowSample(world);
    float outside=pow(saturate(1-s.r),2);
    float grain=sin(world.x*5.1+world.z*2.7)*sin(world.z*4.3-world.x*3.1);
    return depth-min(s.r*_SnowShape.x*(.96+.04*grain),depth*.94)+(s.g*_SnowShape.y+s.a*_SnowShape.z)*saturate(depth/.15)*outside;
}
float3 SnowDisplace(float3 world,float3 normal)
{
    world.y+=SnowHeight(world)*smoothstep(.5,.8,normal.y);return world;
}
void SnowSurface(float3 world,inout half3 normal,inout half3 albedo,inout half smoothness,inout half occlusion)
{
    float4 s=SnowSample(world);float up=smoothstep(.5,.8,normal.y);
    if(max(s.r,s.g)*up<.001)return;
    float e=max(.035,_SnowMap0.w*2);
    float dx=(SnowHeight(world+float3(e,0,0))-SnowHeight(world-float3(e,0,0)))/(2*e);
    float dz=(SnowHeight(world+float3(0,0,e))-SnowHeight(world-float3(0,0,e)))/(2*e);
    normal=normalize(normal+float3(-dx,0,-dz)*_SnowShape.w*up);
    albedo*=1-_SnowLook.x*s.r*up;
    smoothness=lerp(smoothness,.04,s.r*up*.6);
    occlusion*=1-.16*s.r*up;
    // Short height-field shadow trace supplies sub-texel contact shading.
    float3 lightDir=GetMainLight().direction;
    float start=SnowHeight(world);float shadow=1;
    [unroll] for(int j=1;j<=4;j++)
    {
        float t=j*.035;float h=SnowHeight(world+float3(lightDir.x,0,lightDir.z)*t);
        shadow=min(shadow,saturate((start+t*max(.12,lightDir.y)-h+.006)*60));
    }
    albedo*=1-(1-shadow)*_SnowLook.y*up;
}
#endif
