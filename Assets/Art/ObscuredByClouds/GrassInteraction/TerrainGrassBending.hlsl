#ifndef OBC_TERRAIN_GRASS_BENDING_INCLUDED
#define OBC_TERRAIN_GRASS_BENDING_INCLUDED
// The dedicated material is used only by the interactive Terrain detail prefab.
int _OBCGrassPointCount;
float4 _OBCGrassPoints[9];
float4 _OBCGrassDirections[9];
float4 _OBCGrassBend;
float _OBCGrassMeshBase;
float _OBCGrassMeshHeight;

float3 OBCBendGrass(float3 displacedOS, float3 originalOS, float3 pivotWS)
{
    float baseY = _OBCGrassMeshBase;
    float mask = saturate((originalOS.y - baseY) / max(_OBCGrassMeshHeight, 0.001));
    if (_OBCGrassPointCount <= 0 || mask <= 0.0001) return displacedOS;
    float3 rootWS = TransformObjectToWorld(float3(0, baseY, 0));
    float influence = 0;
    float2 pushDirection = float2(0, 1);
    [loop] for (int i = 0; i < min(_OBCGrassPointCount, 9); ++i)
    {
        float4 impactPoint = _OBCGrassPoints[i];
        float2 away = rootWS.xz - impactPoint.xz;
        float distanceToCharacter = length(away);
        float falloff = saturate(1 - distanceToCharacter / max(impactPoint.w, 0.1));
        falloff = falloff * falloff * (3 - 2 * falloff);
        float vertical = 1 - smoothstep(_OBCGrassBend.y * 0.5, _OBCGrassBend.y, abs(rootWS.y - impactPoint.y));
        float weight = falloff * vertical * _OBCGrassDirections[i].z;
        if (weight <= influence) continue;
        float2 motion = _OBCGrassDirections[i].xy;
        float2 radial = distanceToCharacter > 0.03 ? away / distanceToCharacter : motion;
        float2 direction = radial + motion * 0.25;
        pushDirection = direction / max(length(direction), 0.001);
        influence = weight;
    }
    float angle = min(1.3962634, _OBCGrassBend.x) * influence * sqrt(mask);
    float scaleY = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
    float heightWS = max(0, originalOS.y - baseY) * scaleY;
    float3 positionWS = TransformObjectToWorld(displacedOS);
    positionWS.xz += pushDirection * (sin(angle) * heightWS);
    positionWS.y -= (1 - cos(angle)) * heightWS;
    return TransformWorldToObject(positionWS);
}
#endif
