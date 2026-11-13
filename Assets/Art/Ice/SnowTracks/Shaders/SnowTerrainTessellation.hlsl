// Terrain instancing must run after domain interpolation, so the original patch
// coordinates (rather than already sampled heightmap vertices) are interpolated.
struct SnowControl
{
    float4 positionOS : INTERNALTESSPOS;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct SnowFactors {float edge[3]:SV_TessFactor;float inside:SV_InsideTessFactor;};
#if defined(SNOW_PASS_FORWARD)
SnowControl SnowVertexControl(Attributes v)
#elif defined(SNOW_PASS_NORMAL)
SnowControl SnowVertexControl(AttributesDepthNormal v)
#else
SnowControl SnowVertexControl(AttributesLean v)
#endif
{
    SnowControl o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);
    #if defined(SNOW_PASS_FORWARD) || defined(SNOW_PASS_NORMAL)
    o.positionOS=v.positionOS;
    #else
    o.positionOS=v.position;
    #endif
    o.normalOS=v.normalOS;o.texcoord=v.texcoord;return o;
}
float3 SnowControlWorld(SnowControl v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    TerrainInstancing(v.positionOS,v.normalOS,v.texcoord);
    return TransformObjectToWorld(v.positionOS.xyz);
}
float SnowEdgeFactor(float3 a,float3 b)
{
    float3 midpoint=(a+b)*.5;
    float d=min(distance(midpoint.xz,_SnowFocus.xz),distance(midpoint,_WorldSpaceCameraPos));
    float proximity=1-smoothstep(3,12,d);
    return lerp(1,min(min(_SnowLook.z,64),max(1,length(a-b)/.016)),proximity*_SnowLook.w);
}
SnowFactors SnowConstants(InputPatch<SnowControl,3> p)
{
    float3 a=SnowControlWorld(p[0]),b=SnowControlWorld(p[1]),c=SnowControlWorld(p[2]);SnowFactors o;
    o.edge[0]=SnowEdgeFactor(b,c);o.edge[1]=SnowEdgeFactor(c,a);o.edge[2]=SnowEdgeFactor(a,b);
    o.inside=max(o.edge[0],max(o.edge[1],o.edge[2]));return o;
}
[domain("tri")][partitioning("fractional_odd")][outputtopology("triangle_cw")]
[patchconstantfunc("SnowConstants")][outputcontrolpoints(3)]
SnowControl SnowHull(InputPatch<SnowControl,3> p,uint id:SV_OutputControlPointID){return p[id];}
[domain("tri")]
#if defined(SNOW_PASS_FORWARD)
Varyings SnowDomain(SnowFactors f,OutputPatch<SnowControl,3> p,float3 bary:SV_DomainLocation)
#elif defined(SNOW_PASS_NORMAL)
VaryingsDepthNormal SnowDomain(SnowFactors f,OutputPatch<SnowControl,3> p,float3 bary:SV_DomainLocation)
#else
VaryingsLean SnowDomain(SnowFactors f,OutputPatch<SnowControl,3> p,float3 bary:SV_DomainLocation)
#endif
{
    #if defined(SNOW_PASS_FORWARD)
    Attributes v=(Attributes)0;
    #elif defined(SNOW_PASS_NORMAL)
    AttributesDepthNormal v=(AttributesDepthNormal)0;
    #else
    AttributesLean v=(AttributesLean)0;
    #endif
    UNITY_TRANSFER_INSTANCE_ID(p[0],v);
    float4 position=p[0].positionOS*bary.x+p[1].positionOS*bary.y+p[2].positionOS*bary.z;
    #if defined(SNOW_PASS_FORWARD) || defined(SNOW_PASS_NORMAL)
    v.positionOS=position;
    #else
    v.position=position;
    #endif
    v.normalOS=normalize(p[0].normalOS*bary.x+p[1].normalOS*bary.y+p[2].normalOS*bary.z);
    v.texcoord=p[0].texcoord*bary.x+p[1].texcoord*bary.y+p[2].texcoord*bary.z;
    #if defined(SNOW_PASS_FORWARD)
    return SplatmapVert(v);
    #elif defined(SNOW_PASS_NORMAL)
    return DepthNormalOnlyVertex(v);
    #elif defined(SNOW_PASS_SHADOW)
    return ShadowPassVertex(v);
    #else
    return DepthOnlyVertex(v);
    #endif
}
