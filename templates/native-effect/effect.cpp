// MyEffect — a YKSPlayer effect plugin. Start by editing process().
#include <algorithm>
#include <cmath>
#include <cstring>

#include "yks/yks_plugin.h"

namespace {

struct State {
    uint32_t channels;
    double gain_db = 0;
};

const yks_param_desc kParams[] = {
    {"gain", "Gain", -24, 24, 0, "dB"},
};

void* YKS_PLUGIN_CALL create(uint32_t /*rate*/, uint32_t channels) { return new State{channels}; }

// Real time: no allocation, no locks, no I/O in here.
void YKS_PLUGIN_CALL process(void* inst, float* io, uint32_t frames) {
    auto* s = static_cast<State*>(inst);
    const float g = float(std::pow(10.0, s->gain_db / 20.0));
    for (uint32_t i = 0; i < frames * s->channels; ++i) io[i] *= g;
}

void YKS_PLUGIN_CALL set_param(void* inst, const char* id, double v) {
    if (!std::strcmp(id, "gain")) static_cast<State*>(inst)->gain_db = std::clamp(v, -24.0, 24.0);
}

void YKS_PLUGIN_CALL reset(void*) {}
void YKS_PLUGIN_CALL destroy(void* inst) { delete static_cast<State*>(inst); }
uint32_t YKS_PLUGIN_CALL latency(void*) { return 0; }

const yks_dsp_api kDsp = {sizeof(yks_dsp_api), create, process, set_param, reset, destroy, latency};

}  // namespace

extern "C" YKS_PLUGIN_EXPORT int YKS_PLUGIN_CALL yks_plugin_query(const yks_host_api*, yks_plugin_info* info) {
    info->struct_size = sizeof(yks_plugin_info);
    info->abi = YKS_PLUGIN_ABI;
    info->kind = YKS_PLUGIN_DSP;
    info->name = "MyEffect";
    info->version = "0.1.0";
    info->dsp = &kDsp;
    info->params = kParams;
    info->param_count = 1;
    return 0;
}
