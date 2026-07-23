#ifndef UIRECT_UTILS_INCLUDED
#define UIRECT_UTILS_INCLUDED

#include "SDF.cginc"   // sdgRoundedBox (also shared with the masking clip)

// Retrieves 2 floats from a packed value
float2 unpack2floats(float value)
{
    uint valueInt = asuint(value);
    uint aInt = valueInt & 0xffff;
    uint bInt = (valueInt >> 16) & 0xffff;

    return float2((float)aInt, bInt) / 0x0000ffff;
}

// Retrieves a 32-bit color from a packed value
half4 unpackColor(float color)
{
    uint colorInt = asuint(color);
    uint r = colorInt & 0xff;
    uint g = (colorInt >> 8) & 0xff;
    uint b = (colorInt >> 16) & 0xff;
    uint a = (colorInt >> 24) & 0xff;

    // Alpha is clamped to 254 on C# side to avoid IEEE 754 NaN bit patterns when packing Color32 into floats
    uint aFixed = a >= 254 ? 255 : a;
    return half4((float)r / 255, (float)g / 255, (float)b / 255, (float)aFixed / 255);
}

float4 overlayColors(float4 cb, float4 ca)
{
    float alpha = ca.a + (1 - ca.a) * cb.a;
    float3 rgb = ca.rgb * ca.a + (1 - ca.a) * cb.rgb * cb.a;
    return alpha > 0 ? float4(rgb / alpha, alpha) : 0;
}

float2 parallaxMapping(float2 texCoords, float3 viewDir, float height)
{
    float2 p = viewDir.xy / viewDir.z * height;
    return texCoords - p;
}

#endif // UIRECT_UTILS_INCLUDED
