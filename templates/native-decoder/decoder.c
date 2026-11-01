/* MyDecoder — a YKSPlayer decoder plugin. Replace the silence below with real decoding. */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "yks/yks_plugin.h"

typedef struct state {
    int64_t length, pos;
} state;

static void* YKS_PLUGIN_CALL open_(const uint8_t* data, size_t size, const char* ext, yks_audio_format* fmt, char* err, size_t es) {
    (void)ext;
    (void)data; /* parse the header here: you get the whole file in memory */
    if (size < 4) {
        snprintf(err, es, "file too short");
        return NULL;
    }
    state* s = (state*)calloc(1, sizeof(state));
    s->length = 48000 * 2;
    fmt->sample_rate = 48000;
    fmt->channels = 2;
    fmt->bits_per_sample = 16;
    fmt->length = s->length;
    return s;
}

static int64_t YKS_PLUGIN_CALL read_(void* inst, float* out, int64_t frames) {
    state* s = (state*)inst;
    if (frames > s->length - s->pos) frames = s->length - s->pos;
    memset(out, 0, (size_t)frames * 2 * sizeof(float));
    s->pos += frames;
    return frames;
}

static int YKS_PLUGIN_CALL seek_(void* inst, int64_t frame) {
    state* s = (state*)inst;
    if (frame < 0 || frame > s->length) return -1;
    s->pos = frame;
    return 0;
}

static void YKS_PLUGIN_CALL close_(void* inst) { free(inst); }

static const yks_decoder_api kApi = {sizeof(yks_decoder_api), open_, read_, seek_, close_, NULL, NULL};

YKS_PLUGIN_EXPORT int YKS_PLUGIN_CALL yks_plugin_query(const yks_host_api* host, yks_plugin_info* info) {
    (void)host;
    info->struct_size = sizeof(yks_plugin_info);
    info->abi = YKS_PLUGIN_ABI;
    info->kind = YKS_PLUGIN_DECODER;
    info->name = "MyDecoder";
    info->version = "0.1.0";
    info->decoder = &kApi;
    return 0;
}
