// Minimal CLAP test plugin: stereo gain with one "gain" parameter.
// Used to verify the AudioForge CLAP host end-to-end.
#include "clap/clap.h"

#include <string.h>
#include <stdio.h>
#include <stdlib.h>

static bool gain_start_processing(const clap_plugin_t *plugin);
static void      gain_stop_processing(const clap_plugin_t *plugin);

#define PLUGIN_ID   "com.audioforge.test.gain"
#define PLUGIN_NAME "AudioForge Test Gain"
#define PARAM_GAIN  0u

typedef struct {
    double gain;
    double sample_rate;
    bool activated;
    bool processing;
} gain_plugin_t;

static const char *feat_list[] = { CLAP_PLUGIN_FEATURE_AUDIO_EFFECT, CLAP_PLUGIN_FEATURE_STEREO, NULL };

static const clap_plugin_descriptor_t gain_descriptor = {
    .clap_version = CLAP_VERSION_INIT,
    .id           = PLUGIN_ID,
    .name         = PLUGIN_NAME,
    .vendor       = "AudioForge",
    .url          = "",
    .manual_url   = "",
    .support_url  = "",
    .version      = "1.0.0",
    .description  = "Test gain plugin for host verification",
    .features     = feat_list,
};

// ---- plugin lifecycle ----

static bool gain_init(const clap_plugin_t *plugin) { (void)plugin; return true; }
static void      gain_destroy(const clap_plugin_t *plugin) { free((void *)plugin->plugin_data); }

static bool gain_activate(const clap_plugin_t *plugin, double sample_rate,
                               uint32_t min_frames, uint32_t max_frames)
{
    (void)min_frames; (void)max_frames;
    gain_plugin_t *p = (gain_plugin_t *)plugin->plugin_data;
    p->sample_rate = sample_rate;
    p->activated = true;
    return true;
}

static void gain_deactivate(const clap_plugin_t *plugin)
{
    gain_plugin_t *p = (gain_plugin_t *)plugin->plugin_data;
    p->activated = false;
}

static bool gain_start(const clap_plugin_t *plugin) { (void)plugin; return true; }
static void      gain_stop(const clap_plugin_t *plugin)  { (void)plugin; }
static void      gain_reset(const clap_plugin_t *plugin) { (void)plugin; }

static int gain_process(const clap_plugin_t *plugin, const clap_process_t *process)
{
    gain_plugin_t *p = (gain_plugin_t *)plugin->plugin_data;
    if (!p->activated || !p->processing) return CLAP_PROCESS_ERROR;
    if (process->audio_outputs_count < 1 || process->audio_inputs_count < 1)
        return CLAP_PROCESS_CONTINUE;

    const clap_audio_buffer_t *in  = &process->audio_inputs[0];
    const clap_audio_buffer_t *out = &process->audio_outputs[0];
    uint32_t n = process->frames_count;
    uint32_t ch = out->channel_count < in->channel_count ? out->channel_count : in->channel_count;

    for (uint32_t c = 0; c < ch; c++) {
        const float *src = in->data32 ? in->data32[c] : NULL;
        float *dst = out->data32 ? out->data32[c] : NULL;
        if (!dst) continue;
        if (src) {
            for (uint32_t i = 0; i < n; i++) dst[i] = src[i] * (float)p->gain;
        } else {
            for (uint32_t i = 0; i < n; i++) dst[i] = 0.0f;
        }
    }
    return CLAP_PROCESS_CONTINUE;
}

// ---- audio-ports extension ----

static uint32_t ports_count(const clap_plugin_t *plugin, bool is_input)
{
    (void)plugin;
    return is_input ? 1u : 1u;
}

static bool ports_get(const clap_plugin_t *plugin, uint32_t index, bool is_input,
                      clap_audio_port_info_t *info)
{
    (void)plugin;
    if (index != 0) return false;
    memset(info, 0, sizeof(*info));
    info->id = 0;
    strcpy(info->name, "Main");
    info->flags = CLAP_AUDIO_PORT_IS_MAIN;
    info->channel_count = 2;
    info->port_type = is_input ? CLAP_PORT_STEREO : CLAP_PORT_STEREO;
    info->in_place_pair = CLAP_INVALID_ID;
    return true;
}

static const clap_plugin_audio_ports_t audio_ports_ext = {
    .count = ports_count,
    .get   = ports_get,
};

// ---- params extension ----

static const clap_param_info_t gain_info = {
    .id          = PARAM_GAIN,
    .flags       = 0,
    .cookie      = NULL,
    .name        = { "gain" },
    .module      = { "" },
    .min_value   = 0.0,
    .max_value   = 2.0,
    .default_value = 1.0,
};

static uint32_t params_count(const clap_plugin_t *plugin) { (void)plugin; return 1; }

static bool params_get_info(const clap_plugin_t *plugin, uint32_t index, clap_param_info_t *info)
{
    (void)plugin;
    if (index != 0) return false;
    *info = gain_info;
    return true;
}

static bool params_get_value(const clap_plugin_t *plugin, clap_id param_id, double *value)
{
    gain_plugin_t *p = (gain_plugin_t *)plugin->plugin_data;
    if (param_id != PARAM_GAIN) return false;
    *value = p->gain;
    return true;
}

static bool params_value_to_text(const clap_plugin_t *plugin, clap_id param_id,
                                 double value, char *buffer, uint32_t capacity)
{
    (void)plugin; (void)param_id;
    if (capacity < 32) return false;
    snprintf(buffer, capacity, "%.3f", value);
    return true;
}

static bool params_text_to_value(const clap_plugin_t *plugin, clap_id param_id,
                                 const char *text, double *value)
{
    (void)plugin; (void)param_id;
    return sscanf(text, "%lf", value) == 1;
}

static void params_flush(const clap_plugin_t *plugin,
                         const clap_input_events_t *in,
                         const clap_output_events_t *out)
{
    (void)out;
    gain_plugin_t *p = (gain_plugin_t *)plugin->plugin_data;
    uint32_t n = in->size(in);
    for (uint32_t i = 0; i < n; i++) {
        const clap_event_header_t *hdr = in->get(in, i);
        if (hdr->type == CLAP_EVENT_PARAM_VALUE) {
            const clap_event_param_value_t *ev = (const clap_event_param_value_t *)hdr;
            if (ev->param_id == PARAM_GAIN) p->gain = ev->value;
        }
    }
}

static const clap_plugin_params_t params_ext = {
    .count        = params_count,
    .get_info     = params_get_info,
    .get_value    = params_get_value,
    .value_to_text = params_value_to_text,
    .text_to_value = params_text_to_value,
    .flush        = params_flush,
};

// ---- get_extension ----

static const void *gain_get_extension(const clap_plugin_t *plugin, const char *id)
{
    (void)plugin;
    if (strcmp(id, CLAP_EXT_AUDIO_PORTS) == 0) return &audio_ports_ext;
    if (strcmp(id, CLAP_EXT_PARAMS) == 0)       return &params_ext;
    return NULL;
}

// ---- plugin instance ----

static const clap_plugin_t gain_plugin = {
    .desc             = &gain_descriptor,
    .plugin_data      = NULL,
    .init             = gain_init,
    .destroy          = gain_destroy,
    .activate         = gain_activate,
    .deactivate       = gain_deactivate,
    .start_processing = gain_start_processing,
    .stop_processing  = gain_stop_processing,
    .reset            = gain_reset,
    .process          = gain_process,
    .get_extension    = gain_get_extension,
    .on_main_thread   = NULL,
};

static bool gain_start_processing(const clap_plugin_t *plugin)
{
    gain_plugin_t *p = (gain_plugin_t *)plugin->plugin_data;
    p->processing = true;
    return true;
}

static void gain_stop_processing(const clap_plugin_t *plugin)
{
    gain_plugin_t *p = (gain_plugin_t *)plugin->plugin_data;
    p->processing = false;
}

static const clap_plugin_t *gain_create(const clap_plugin_factory_t *factory,
                                        const clap_host_t *host, const char *plugin_id)
{
    (void)factory; (void)host;
    if (strcmp(plugin_id, PLUGIN_ID) != 0) return NULL;
    gain_plugin_t *p = (gain_plugin_t *)calloc(1, sizeof(*p));
    p->gain = 1.0;
    clap_plugin_t *inst = (clap_plugin_t *)malloc(sizeof(clap_plugin_t));
    *inst = gain_plugin;
    inst->plugin_data = p;
    return inst;
}

// ---- factory ----

static uint32_t factory_count(const clap_plugin_factory_t *factory) { (void)factory; return 1; }

static const clap_plugin_descriptor_t *factory_get_desc(const clap_plugin_factory_t *factory,
                                                        uint32_t index)
{
    (void)factory;
    return index == 0 ? &gain_descriptor : NULL;
}

static const clap_plugin_t *factory_create(const clap_plugin_factory_t *factory,
                                           const clap_host_t *host, const char *plugin_id)
{
    return gain_create(factory, host, plugin_id);
}

static const clap_plugin_factory_t gain_factory = {
    .get_plugin_count      = factory_count,
    .get_plugin_descriptor = factory_get_desc,
    .create_plugin         = factory_create,
};

// ---- entry ----

static bool entry_init(const char *plugin_path) { (void)plugin_path; return true; }
static void      entry_deinit(void) {}

static const void *entry_get_factory(const char *factory_id)
{
    return strcmp(factory_id, CLAP_PLUGIN_FACTORY_ID) == 0 ? &gain_factory : NULL;
}

const clap_plugin_entry_t clap_entry = {
    .clap_version = CLAP_VERSION_INIT,
    .init         = entry_init,
    .deinit       = entry_deinit,
    .get_factory  = entry_get_factory,
};
