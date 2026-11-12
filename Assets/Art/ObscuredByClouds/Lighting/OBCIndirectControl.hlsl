#ifndef OBC_INDIRECT_CONTROL_INCLUDED
#define OBC_INDIRECT_CONTROL_INCLUDED
// A disabled or absent TimeOfDay leaves existing materials unchanged.
float4 _OBCIndirectLightControl;
half OBCGlobalIndirectStrength()
{
    return _OBCIndirectLightControl.x > .5 ? max(0, _OBCIndirectLightControl.y) : 1.0h;
}
half OBCIndirectStrength(half materialStrength)
{
    return max(0.0h, materialStrength) * OBCGlobalIndirectStrength();
}
#endif
