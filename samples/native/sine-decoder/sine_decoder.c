/*
 * Sample decoder plugin: plays ".ykstone" text files, a tiny melody format.
 *
 *   # title: Twinkle
 *   # artist: Sample
 *   440 0.5      <- frequency in Hz, duration in seconds (0 Hz = rest)
 *   494 0.5
 *
 * Shows the whole decoder API: open from memory, read, seek, tags and a codec name.
 * SPDX-License-Identifier: MIT
 */
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "yks/yks_plugin.h"

#define RATE 48000
#define MAX_NOTES 4096

typedef struct note { double hz; int64_t start, frames; } note;

typedef struct tone {
    note notes[MAX_NOTES];
    int count;
    int64_t length, pos;
    char title[128], artist[128];
} tone;

static const yks_host_api* g_host;

static void copy_tag(const char* line, const char* key, char* dst, size_t n) {
    size_t k = strlen(key);
    if (strncmp(line, key, k) != 0) return;
    line += k;
    while (*line == ' ') line++;
    size_t i = 0;
    for (; i + 1 < n && line[i] && line[i] != '\r' && line[i] != '\n'; i++) dst[i] = line[i];
    dst[i] = 0;
}

static void* YKS_PLUGIN_CALL tone_open(const uint8_t* data, size_t size, const char* ext, yks_audio_format* fmt, char* err, size_t err_size) {
    (void)ext;
    tone* t = (tone*)calloc(1, sizeof(tone));
    if (!t) return NULL;
    char* text = (char*)malloc(size + 1);
    memcpy(text, data, size);
    text[size] = 0;
    for (char* line = strtok(text, "\n"); line; line = strtok(NULL, "\n")) {
        if (line[0] == '#') {
            copy_tag(line, "# title:", t->title, sizeof t->title);
            copy_tag(line, "# artist:", t->artist, sizeof t->artist);
            continue;
        }
        double hz, sec;
        if (sscanf(line, "%lf %lf", &hz, &sec) == 2 && sec > 0 && t->count < MAX_NOTES) {
            note* n = &t->notes[t->count++];
            n->hz = hz;
            n->start = t->length;
            n->frames = (int64_t)(sec * RATE);
            t->length += n->frames;
        }
    }
    free(text);
    if (t->count == 0) {
        snprintf(err, err_size, "no notes found");
        free(t);
        return NULL;
    }
    fmt->sample_rate = RATE;
    fmt->channels = 2;
    fmt->bits_per_sample = 0;
    fmt->length = t->length;
    if (g_host) g_host->log(YKS_LOG_INFO, "ykstone opened");
    return t;
}

static int64_t YKS_PLUGIN_CALL tone_read(void* inst, float* out, int64_t frames) {
    tone* t = (tone*)inst;
    int64_t done = 0;
    int idx = 0;
    while (done < frames && t->pos < t->length) {
        while (idx < t->count && t->notes[idx].start + t->notes[idx].frames <= t->pos) idx++;
        const note* n = &t->notes[idx];
        const int64_t in = t->pos - n->start;
        /* Short attack and release so notes do not click. */
        double env = 1.0;
        if (in < 480) env = in / 480.0;
        if (n->frames - in < 2400) env *= (n->frames - in) / 2400.0;
        const double v = n->hz > 0 ? 0.25 * env * sin(2 * 3.14159265358979 * n->hz * in / RATE) : 0.0;
        out[done * 2] = out[done * 2 + 1] = (float)v;
        done++;
        t->pos++;
    }
    return done;
}

static int YKS_PLUGIN_CALL tone_seek(void* inst, int64_t frame) {
    tone* t = (tone*)inst;
    if (frame < 0 || frame > t->length) return -1;
    t->pos = frame;
    return 0;
}

static void YKS_PLUGIN_CALL tone_close(void* inst) { free(inst); }

static int YKS_PLUGIN_CALL tone_metadata(void* inst, char* json, size_t size) {
    tone* t = (tone*)inst;
    return snprintf(json, size, "{\"title\":\"%s\",\"artist\":\"%s\"}", t->title, t->artist);
}

static const char* YKS_PLUGIN_CALL tone_codec(void* inst) {
    (void)inst;
    return "YKS Tone";
}

static const yks_decoder_api kDecoder = {sizeof(yks_decoder_api), tone_open, tone_read, tone_seek, tone_close, tone_metadata, tone_codec};

YKS_PLUGIN_EXPORT int YKS_PLUGIN_CALL yks_plugin_query(const yks_host_api* host, yks_plugin_info* info) {
    g_host = host;
    info->struct_size = sizeof(yks_plugin_info);
    info->abi = YKS_PLUGIN_ABI;
    info->kind = YKS_PLUGIN_DECODER;
    info->name = "YKS Tone (sample)";
    info->version = "1.0.0";
    info->decoder = &kDecoder;
    return 0;
}
