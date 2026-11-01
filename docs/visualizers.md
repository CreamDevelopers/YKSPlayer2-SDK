# Visualizers

A visualizer is a **Direct2D pixel shader** (HLSL, compiled to `.cso`). The player runs it every frame over the Now
Playing page, behind the content. No plugin code runs on the CPU, which makes visualizers the safest kind of plugin.

## Inputs

Declare any of these at global scope; the player sets the ones you declare.

| Name | Type | Meaning |
|---|---|---|
| `resolution` | `float2` | Drawing area in pixels |
| `time` | `float` | Seconds since the visualizer started |
| `pulse` | `float` | 0..1 beat energy: the KT3 BPM grid when the file has one, loudness otherwise. Already scaled by the user's "effect strength". |
| `flash` | `float` | 0..1 chorus flash (KT3 chorus sections) |
| `accent`, `glow` | `float4` | Colours taken from the cover art (rgb 0..1) |
| `bands` | `float4[16]` | 64 spectrum values 0..1, log-spaced 20 Hz – 20 kHz: `bands[i >> 2][i & 3]` |

## Template

```hlsl
#define D2D_INPUT_COUNT 0
#include "d2d1effecthelpers.hlsli"

float2 resolution;
float time;
float pulse;
float4 accent;
float4 bands[16];

D2D_PS_ENTRY(main)
{
    float2 uv = D2DGetScenePosition().xy / resolution;
    uint i = (uint)(uv.x * 63);
    float level = bands[i >> 2][i & 3];
    float bar = step(1 - uv.y, level);
    float3 c = accent.rgb * bar * (0.7 + 0.3 * pulse);
    return float4(c, bar);   // premultiplied alpha
}
```

## Compile

```powershell
fxc my.hlsl /T ps_4_0 /D D2D_FULL_SHADER /D D2D_ENTRY=main /E main /O3 /I "<Windows Kits>\Include\<ver>\um" /Fo my.cso
```

`samples/visualizer-nebula/build.ps1` finds `fxc` and the include folder for you.

## Tips

- Output **premultiplied** alpha: transparent areas show the player's Afterglow background.
- Keep it light: it runs every frame over the whole page. Avoid loops over all 64 bands per pixel.
- Respect quiet moments: `pulse` and the bands fall to 0 in silence.
- If the shader fails to load, the player falls back to its built-in visualizer and tells the user.
