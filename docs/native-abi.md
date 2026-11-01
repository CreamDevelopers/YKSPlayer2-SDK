# Native ABI: decoders and effects

Header: [`include/yks/yks_plugin.h`](../include/yks/yks_plugin.h). Plain C, so any language with a C FFI works
(C, C++, Rust, Zig, …). Build a 64-bit Windows DLL that exports one function:

```c
YKS_PLUGIN_EXPORT int YKS_PLUGIN_CALL yks_plugin_query(const yks_host_api* host, yks_plugin_info* info);
```

Fill `info` (`abi = YKS_PLUGIN_ABI`, `kind`, `name`, `version`, and `decoder` or `dsp`) and return 0.

## How the player runs your DLL

```
YKSPlayer ──shared memory + events──▶ yks-plugin-host.exe (low integrity, job object) ──▶ your.dll
```

- One host process per decoder instance (per track) and per effect in the chain.
- The host is killed with the player; it cannot start other processes; memory is capped (512 MB).
- Calls time out: open 5 s, read/seek 3 s, effect block 150 ms. A crash or timeout ends that instance only:
  the track is skipped (decoder) or the effect is bypassed (dsp). After three failures the player disables the plugin.
- Your DLL may load DLLs that sit next to it (the host sets the DLL directory to your folder).
- Link the C runtime statically (`/MT`) or ship it next to your DLL.

## Decoders

```c
void*   open(const uint8_t* data, size_t size, const char* ext, yks_audio_format* fmt, char* error, size_t error_size);
int64_t read(void* inst, float* out, int64_t frames);   // interleaved float; 0 = end
int     seek(void* inst, int64_t frame);
void    close(void* inst);
int     metadata(void* inst, char* json, size_t size);  // optional: {"title","artist","album","genre"}
const char* codec_name(void* inst);                    // optional: "MOD", "NSF", ...
```

- You receive the **whole file in memory**; you never open files yourself.
- Fill `fmt`: sample rate, channels (1–8), `bits_per_sample` (0 for synthesised/lossy), `length` in frames (−1 if unknown).
- Seek must be sample accurate if you can; otherwise land as close as possible.
- `metadata` feeds the library (title, artist, album, genre).
- Built-in formats (WAV/FLAC/MP3/…) are always tried first; plugins are chosen by `decoder.extensions`.

## Effects

```c
void* create(uint32_t sample_rate, uint32_t channels);
void  process(void* inst, float* io, uint32_t frames);   // in place, real time
void  set_param(void* inst, const char* id, double value);
void  reset(void* inst);
void  destroy(void* inst);
uint32_t latency(void* inst);
```

- Effects run on the output format (after resampling). `create` is called again when the output changes.
- `process` must not allocate, lock or block. Blocks are up to 8192 frames.
- Describe your parameters in `info.params` (`id`, `name`, `min`, `max`, `default_value`, `unit`): the player
  builds sliders from them and remembers the values.

## Logging

`host->log(level, "message")` writes to the host's stderr (collected by the player in debug builds).

## Testing locally

1. Build the DLL and put `yks-plugin.json` next to it.
2. `yks-pack pack <folder> --key me.key.pem` and drop the `.ykspkg` on the player.
3. Or run the engine's own tests in this repository as a reference: `native/tests/engine_test.cpp` (`Plugins.*`).
