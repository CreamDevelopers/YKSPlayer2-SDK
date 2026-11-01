/*
 * YKSPlayer plugin ABI — native decoders and DSP effects.
 *
 * A native plugin is a Windows DLL exporting one function, yks_plugin_query(). YKSPlayer never loads the DLL into
 * its own process: each plugin runs inside yks-plugin-host.exe at low integrity, inside a job object. If the plugin
 * crashes or hangs, the player skips the track (decoders) or bypasses the effect (DSP) and keeps playing.
 *
 * Rules
 *  - All functions are called from one thread at a time per instance. Different instances may run concurrently.
 *  - Audio is 32-bit float, interleaved, nominal range [-1, 1].
 *  - Decoders get the whole file in memory: plugins need no file system access (and at low integrity have little).
 *  - Strings are UTF-8. Return 0 for success and a negative value for errors unless stated otherwise.
 *  - Do not keep pointers passed to you beyond the call unless stated otherwise.
 *
 * Versioning: YKS_PLUGIN_ABI changes only for incompatible changes. New optional functions are added at the end of
 * the tables, and `struct_size` tells the host what a plugin provides.
 *
 * SPDX-License-Identifier: MIT
 */
#ifndef YKS_PLUGIN_H
#define YKS_PLUGIN_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define YKS_PLUGIN_ABI 1

#ifdef _WIN32
#  define YKS_PLUGIN_EXPORT __declspec(dllexport)
#  define YKS_PLUGIN_CALL __cdecl
#else
#  define YKS_PLUGIN_EXPORT __attribute__((visibility("default")))
#  define YKS_PLUGIN_CALL
#endif

typedef enum yks_plugin_kind {
    YKS_PLUGIN_DECODER = 1,
    YKS_PLUGIN_DSP = 2,
} yks_plugin_kind;

typedef enum yks_log_level { YKS_LOG_DEBUG = 0, YKS_LOG_INFO = 1, YKS_LOG_WARN = 2, YKS_LOG_ERROR = 3 } yks_log_level;

/* Services the host offers to plugins. */
typedef struct yks_host_api {
    uint32_t struct_size;
    uint32_t abi;
    void (YKS_PLUGIN_CALL* log)(int level, const char* message);
} yks_host_api;

/* ---------------------------------------------------------------- decoders */

typedef struct yks_audio_format {
    uint32_t sample_rate;
    uint32_t channels;        /* 1..8 */
    uint32_t bits_per_sample; /* source precision, 0 for synthesised or lossy audio */
    uint32_t reserved;
    int64_t length;           /* frames; -1 when unknown or endless */
} yks_audio_format;

typedef struct yks_decoder_api {
    uint32_t struct_size;
    /* Opens `size` bytes of file data. `ext` is the lower-case extension including the dot (a hint).
     * Returns an instance, or NULL with a message in `error` (up to error_size bytes, NUL-terminated). */
    void* (YKS_PLUGIN_CALL* open)(const uint8_t* data, size_t size, const char* ext, yks_audio_format* format,
                                  char* error, size_t error_size);
    /* Decodes up to `frames` frames. Returns frames written, 0 at the end, < 0 on error. */
    int64_t (YKS_PLUGIN_CALL* read)(void* instance, float* out, int64_t frames);
    /* Seeks to a frame. Returns 0, or < 0 if not possible. */
    int (YKS_PLUGIN_CALL* seek)(void* instance, int64_t frame);
    void (YKS_PLUGIN_CALL* close)(void* instance);
    /* Optional: tags as a JSON object {"title":..,"artist":..,"album":..}. Returns bytes written (without NUL),
     * or < 0. May be NULL. */
    int (YKS_PLUGIN_CALL* metadata)(void* instance, char* json, size_t size);
    /* Optional: a short codec name such as "MOD" or "NSF". May be NULL. */
    const char* (YKS_PLUGIN_CALL* codec_name)(void* instance);
} yks_decoder_api;

/* ---------------------------------------------------------------- DSP effects */

typedef struct yks_param_desc {
    const char* id;           /* stable key, e.g. "bits" */
    const char* name;         /* label shown to the user */
    double min, max, default_value;
    const char* unit;         /* "dB", "Hz", "%", "" */
} yks_param_desc;

typedef struct yks_dsp_api {
    uint32_t struct_size;
    void* (YKS_PLUGIN_CALL* create)(uint32_t sample_rate, uint32_t channels);
    /* Processes in place. Must not allocate, block or take longer than the block's duration. */
    void (YKS_PLUGIN_CALL* process)(void* instance, float* io, uint32_t frames);
    void (YKS_PLUGIN_CALL* set_param)(void* instance, const char* id, double value);
    /* Clears internal state (seek, track change without gapless). */
    void (YKS_PLUGIN_CALL* reset)(void* instance);
    void (YKS_PLUGIN_CALL* destroy)(void* instance);
    /* Extra latency in frames the effect introduces (0 for most). */
    uint32_t (YKS_PLUGIN_CALL* latency)(void* instance);
} yks_dsp_api;

/* ---------------------------------------------------------------- entry point */

typedef struct yks_plugin_info {
    uint32_t struct_size;
    uint32_t abi;             /* set to YKS_PLUGIN_ABI */
    int kind;                 /* yks_plugin_kind */
    const char* name;
    const char* version;      /* SemVer of the plugin */
    const yks_decoder_api* decoder;  /* when kind == YKS_PLUGIN_DECODER */
    const yks_dsp_api* dsp;          /* when kind == YKS_PLUGIN_DSP */
    const yks_param_desc* params;    /* DSP parameters (may be NULL) */
    uint32_t param_count;
} yks_plugin_info;

/* The only export. Fill `info` and return 0. `host` stays valid for the lifetime of the process. */
typedef int (YKS_PLUGIN_CALL* yks_plugin_query_fn)(const yks_host_api* host, yks_plugin_info* info);

#ifdef __cplusplus
}
#endif

#endif /* YKS_PLUGIN_H */
