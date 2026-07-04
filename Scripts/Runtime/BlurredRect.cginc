// Based on Fast Rounded Rectangle Shadows by Evan Wallace
// https://madebyevan.com/shaders/fast-rounded-rectangle-shadows/
// Extended for per-corner radii: each slice endpoint is shaped by the corner
// radius on its own side, and samples are weighted by the exact gaussian mass
// of their stratum instead of a midpoint-rule estimate.

#define SQRT_HALF 0.70710678  // sqrt(0.5)

// 1 / (Phi(3) - Phi(-3)): renormalizes the +-3 sigma truncated integral so the
// mask reaches exactly 1 deep inside the shape
#define INV_MASS_3SIGMA 1.0027099

// This approximates the error function, needed for the gaussian integral
float erf(float x)
{
  float s = sign(x), a = abs(x);
  x = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
  x *= x;
  return s - s / (x * x);
}

float2 erf(float2 x)
{
  float2 s = sign(x), a = abs(x);
  x = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
  x *= x;
  return s - s / (x * x);
}

float4 erf(float4 x)
{
  float4 s = sign(x), a = abs(x);
  x = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
  x *= x;
  return s - s / (x * x);
}

// Blurred mask of one horizontal slice of the box, which spans
// [-curved.x, curved.y]. lr holds the (left, right) corner radii for the
// slice's vertical half; using each side's own radius keeps differing corners
// seam-free even when the box is narrower than the blur. k = SQRT_HALF / sigma.
float roundedBoxShadowX(float x, float y, float k, float2 lr, float2 halfSize)
{
  float2 delta = min(halfSize.y - lr - abs(y), 0.0);
  float2 curved = halfSize.x - lr + sqrt(max(0.0, lr * lr - delta * delta));
  float2 integral = 0.5 + 0.5 * erf((x + float2(-curved.y, curved.x)) * k);
  return integral.y - integral.x;
}

// Return the mask for the shadow of a box at halfSize scale.
// radius = (topLeft, topRight, bottomRight, bottomLeft), already clamped to
// [0, min(halfSize)]. Radii must stay float: squaring a half-precision radius
// overflows fp16 for radii > 255 and blows the mask up on mobile GPUs.
float roundedBoxShadow(float2 pos, float2 halfSize, float sigma, float4 radius)
{
  // The signal is only non-zero in a limited range, so don't waste samples
  float low = pos.y - halfSize.y;
  float high = pos.y + halfSize.y;
  float start = clamp(-3.0 * sigma, low, high);
  float end = clamp(3.0 * sigma, low, high);
  float k = SQRT_HALF / sigma;

  // 4 strata, each sample weighted by the exact gaussian mass of its stratum
  float step = (end - start) * 0.25;
  float4 bounds = start + step * float4(0.0, 1.0, 2.0, 3.0);
  float4 cdfLo = 0.5 + 0.5 * erf(bounds * k);
  float4 cdfHi = float4(cdfLo.yzw, 0.5 + 0.5 * erf(end * k));
  float4 weights = cdfHi - cdfLo;
  float4 samples = bounds + step * 0.5;

  float value = 0.0;
  [unroll]
  for (int i = 0; i < 4; i++)
  {
    float y = pos.y - samples[i];
    // Top corners shape the upper half of the box, bottom corners the lower
    float2 lr = (y > 0.0) ? radius.xy : radius.wz;
    value += roundedBoxShadowX(pos.x, y, k, lr, halfSize) * weights[i];
  }

  return value * INV_MASS_3SIGMA;
}
