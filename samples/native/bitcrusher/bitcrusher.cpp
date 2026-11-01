// Sample effect plugin: a bit crusher with sample-rate reduction and dry/wet mix.
// Shows parameters (the player builds sliders from them) and real-time rules: no allocation in process().
// SPDX-License-Identifier: MIT
#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

#include "yks/yks_plugin.h"

namespace {

struct Crusher {
    uint32_t channels;
    double bits = 8, downsample = 4, mix = 1.0;
    std::vector<float> held;
    uint32_t counter = 0;
};

const yks_param_desc kParams[] = {
    {"bits", "Bit depth", 1, 16, 8, "bit"},
    {"downsample", "Sample rate divisor", 1, 32, 4, "x"},
    {"mix", "Mix", 0, 1, 1, ""},
};

void* YKS_PLUGIN_CALL create(uint32_t, uint32_t channels) {
    auto* c = new Crusher{channels};
    c->held.assign(channels, 0.0f);
    return c;
}

void YKS_PLUGIN_CALL process(void* inst, float* io, uint32_t frames) {
    auto* c = static_cast<Crusher*>(inst);
    const float levels = float(std::pow(2.0, c->bits - 1));
    const auto every = uint32_t(std::max(1.0, c->downsample));
    const float wet = float(c->mix), dry = 1.0f - wet;
    for (uint32_t f = 0; f < frames; ++f) {
        if (c->counter++ % every == 0)
            for (uint32_t ch = 0; ch < c->channels; ++ch) c->held[ch] = std::round(io[f * c->channels + ch] * levels) / levels;
        for (uint32_t ch = 0; ch < c->channels; ++ch) {
            float& s = io[f * c->channels + ch];
            s = s * dry + c->held[ch] * wet;
        }
    }
}

void YKS_PLUGIN_CALL set_param(void* inst, const char* id, double v) {
    auto* c = static_cast<Crusher*>(inst);
    if (!std::strcmp(id, "bits")) c->bits = std::clamp(v, 1.0, 16.0);
    else if (!std::strcmp(id, "downsample")) c->downsample = std::clamp(v, 1.0, 32.0);
    else if (!std::strcmp(id, "mix")) c->mix = std::clamp(v, 0.0, 1.0);
}

void YKS_PLUGIN_CALL reset(void* inst) {
    auto* c = static_cast<Crusher*>(inst);
    std::fill(c->held.begin(), c->held.end(), 0.0f);
    c->counter = 0;
}

void YKS_PLUGIN_CALL destroy(void* inst) { delete static_cast<Crusher*>(inst); }

uint32_t YKS_PLUGIN_CALL latency(void*) { return 0; }

const yks_dsp_api kDsp = {sizeof(yks_dsp_api), create, process, set_param, reset, destroy, latency};

}  // namespace

extern "C" YKS_PLUGIN_EXPORT int YKS_PLUGIN_CALL yks_plugin_query(const yks_host_api*, yks_plugin_info* info) {
    info->struct_size = sizeof(yks_plugin_info);
    info->abi = YKS_PLUGIN_ABI;
    info->kind = YKS_PLUGIN_DSP;
    info->name = "Bit Crusher (sample)";
    info->version = "1.0.0";
    info->dsp = &kDsp;
    info->params = kParams;
    info->param_count = uint32_t(std::size(kParams));
    return 0;
}
