// Sample visualizer plugin: "Nebula" — a slow nebula whose rings swell with the bass and glitter with the treble.
// Visualizers are Direct2D pixel shaders run by the player (Win2D PixelShaderEffect): no code runs outside the GPU.
//
// Inputs the player sets every frame (names must match):
//   float2 resolution   size of the drawing area in pixels
//   float  time         seconds since the visualizer started
//   float  pulse        0..1 beat energy (KT3 BPM grid, or loudness)
//   float4 accent, glow cover colours (rgb 0..1, a = 1)
//   float4 bands[16]    64 spectrum values, 0..1, log-spaced 20 Hz – 20 kHz (bands[i].x = bin 4i, .y = 4i+1, ...)
// Compile: see build.ps1 next to this file. SPDX-License-Identifier: MIT
#define D2D_INPUT_COUNT 0
#include "d2d1effecthelpers.hlsli"

float2 resolution;
float time;
float pulse;
float4 accent;
float4 glow;
float4 bands[16];

float bin(float x)  // x in 0..1 across the spectrum, linear interpolation between bins
{
    float f = saturate(x) * 63.0;
    uint i = (uint)f;
    uint j = min(i + 1, 63u);
    float a = bands[i >> 2][i & 3];
    float b = bands[j >> 2][j & 3];
    return lerp(a, b, frac(f));
}

float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

D2D_PS_ENTRY(main)
{
    float2 uv = D2DGetScenePosition().xy / resolution;
    float2 c = (uv - 0.5) * float2(resolution.x / resolution.y, 1.0);
    float r = length(c);
    float ang = atan2(c.y, c.x);

    // Each ring radius maps to a part of the spectrum: bass in the middle, treble outside.
    float energy = bin(saturate(r * 1.4));
    float rings = 0.5 + 0.5 * sin(r * 40.0 - time * 1.5 + energy * 6.0);
    rings = pow(rings, 6.0) * energy * (0.6 + 0.8 * pulse);

    // Soft rotating cloud.
    float cloud = 0.5 + 0.5 * sin(ang * 3.0 + time * 0.2 + r * 6.0) * sin(r * 9.0 - time * 0.3);
    cloud *= exp(-r * 2.2);

    // Treble sparkle.
    float2 cell = floor(uv * resolution / 6.0);
    float star = step(0.995 - bin(0.85) * 0.02, hash(cell + floor(time * 4.0)));

    float3 col = accent.rgb * cloud * 0.9 + glow.rgb * rings + glow.rgb * star * 0.8;
    col += accent.rgb * pulse * 0.08 * exp(-r * 4.0);
    float alpha = saturate(max(max(col.r, col.g), col.b) * 1.2);
    return float4(col, alpha);  // premultiplied: colour already scaled by its own brightness
}
