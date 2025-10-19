// Signed distance field of a rounded rectangle
// https://iquilezles.org/articles/distfunctions2d/
// top-left | top-right | bottom-right | bottom-left
float sdRoundedBox(float2 position, float2 size, half4 radius)
{
    radius = radius.yzxw;
    radius.xy = (position.x > 0.0) ? radius.xy : radius.zw;
    radius.x  = (position.y > 0.0) ? radius.x  : radius.y;
    float2 dist = abs(position) - size + radius.x;
    return min(max(dist.x, dist.y), 0.0) + length(max(dist, 0.0)) - radius.x;
}

// Signed distance field of a rounded rectangle & its gradient vector
// .x = f(p)
// .y = ∂f(p)/∂x
// .z = ∂f(p)/∂y
// .yz = ∇f(p) with ‖∇f(p)‖ = 1
// https://www.shadertoy.com/view/wlcXD2
float3 sdgRoundedBox(in float2 position, in float2 size, half4 radius)
{
    radius = radius.yzxw;
    radius.xy = (position.x > 0.0) ? radius.xy : radius.zw;
    radius.x  = (position.y > 0.0) ? radius.x  : radius.y;

    float2 w = abs(position)-(size-radius.x);
    float2 s = float2(position.x<0.0?-1:1,position.y<0.0?-1:1);
    
    float g = max(w.x,w.y);
    float2  q = max(w,0.0);
    float l = length(q);
    
    return float3((g>0.0) ? l-radius.x : g-radius.x,
                s*((g>0.0) ? q / l : ((w.x>w.y) ? float2(1,0) : float2(0,1))));
}


// Signed distance field of an arc
// sc is the sin/cos of the arc's aperture
float sdArc(float2 position, float2 sc, float innerRadius, float thickness )
{
    position.x = abs(position.x);
    return (sc.y * position.x > sc.x * position.y ?
        length(position - sc * innerRadius) : abs(length(position) - innerRadius)) - thickness;
}

// Retrieves 2 floats from a packed value
float2 unpack2floats(float value) 
{
    uint valueInt = asuint(value);
    uint aInt = valueInt & 0xffff;
    uint bInt = (valueInt >> 16) & 0xffff;

    return float2((float)aInt, bInt) / 0x0000ffff;
}

#define GAMMA 2.2

// Retrieves a 32-bit color from a packed value
half4 unpackColor(float color)
{
    uint colorInt = asuint(color);
    uint r = colorInt & 0xff;
    uint g = (colorInt >> 8) & 0xff;
    uint b = (colorInt >> 16) & 0xff;
    uint a = (colorInt >> 24) & 0xff;

    return half4((float)r / 255, (float)g / 255, (float)b / 255, (float)a / 255);
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

// Returns a pseudo-random float with components between 0 and 1 for a given float2 uv
float random2(float2 uv)
{
    float c = dot(uv, float2(127.1, 311.7));
    return frac(43758.5453123 * sin(c));
}