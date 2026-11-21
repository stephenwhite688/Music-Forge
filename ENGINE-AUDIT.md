# HelixAudio Engine — Foundation Audit

**Date:** 2026 (pre-SDK-extraction audit)
**Scope:** `src/Sound Engine/HelixAudio.*` (21 projects) + `src/AudioForge.SoundEngine`
**Method:** Full read-only source inventory of every project; cross-checked
against `H:\future projects\AudioForge-DAW-Spec.md` and
`AudioForge-Forensics-Spec.md`.

> **Purpose of this document:** honest baseline before the engine is
> extracted as a standalone SDK. It records what is real, what is
> scaffolded, and what is stubbed — so no downstream spec or marketing
> claim rests on a false assumption.

---

## 1. Verdict

The engine is approximately **60% real / 25% scaffolded / 15% stubbed**.

- **Real and strong:** analysis suite (EBU R128 loudness, true peak,
  hum/pitch/noise-floor detection, statistics, peak files), core DSP
  primitives, EQ (post-fix), codec decode, WASAPI output + loopback.
- **Scaffolded:** plugin host (managed-DLL only), streaming wrappers,
  MIDI shells, several "processor" classes that are not `IAudioNode`s.
- **Stubbed:** VST3/CLAP hosts, WASAPI capture input, HLS/DASH parsing,
  3D audio, Visualization + Benchmarks projects (empty), most encode
  paths, resampling quality, parameter/automation/state model.

---

## 2. Per-project status

| Project | Status | Notes |
|---|---|---|
| `HelixAudio.Core` | Partial / fragmented | Real `IAudioNode`/`AudioNode`/`AudioFormat`; ~15 effect classes are **not** nodes; `WASAPIBackend.cs` is a placeholder (real backend lives in `Backend.Wasapi`) |
| `HelixAudio.Engine` | Partial — mostly real | `ProcessingGraph` allocates heavily per block; `EngineController` pipelines are stubs |
| `HelixAudio.DSP` | Mostly real | Biquads, FIR/IIR, FFT, EQ, dynamics processors, taps; resampler is naive |
| `HelixAudio.DSP.3D` | Mostly stubs | `BinauralNode`/`SpatialAudioNode` pass-throughs; `SurroundNode` naive upmix; `AmbisonicNode` mostly bypass |
| `HelixAudio.Effects` | Partial — wrong abstraction | Real Freeverb-style DSP but `EffectBase.Process` allocates per block; **not `IAudioNode`** |
| `HelixAudio.Filters` | Partial — wrong abstraction | Same `FilterBase` non-node pattern |
| `HelixAudio.Mixing` | Partial — fragmented | `StereoMixerNode` is real; `MixerNode.Process` is a pass-through stub; batch mixers allocate |
| `HelixAudio.IO` | Real (PCM + float WAV) | `WavReader/Writer` int PCM **and IEEE float32** with TPDF dither; chunked `AudioStream` decode; no RF64 |
| `HelixAudio.IO.Codecs` | Partial→improving | WAV R/W (float+int); **chunked decode** for WAV/MF/FLAC/OGG; FLAC/OGG managed-verified; MP3/AAC/WMA/ALAC via OS decoder (unverified); **Opus/DSD throw**; **WAV-only encode** |
| ~~`HelixAudio.MIDI`~~ | **REMOVED** | Scaffolded shells, no real MIDI parse or synth audio — deleted rather than shipped fake |
| `HelixAudio.Analysis` | **Real — strongest project** | BS.1770/EBU R128 `LoudnessMeter`, `HumDetector`, `NoiseFloorEstimator`, `PitchTracker`, `SignalStatistics`, `PeakFileGenerator`, meter/tap nodes |
| ~~`HelixAudio.Streaming`~~ | **REMOVED** | Thin `HttpClient` wrappers, no manifest/demux/decode — deleted; radio works via `MediaFoundationReader` in `RealtimePlayer` |
| ~~`HelixAudio.Visualization`~~ | **REMOVED** | Empty project — deleted |
| `HelixAudio.Benchmarks` | Empty project (kept) | Retained for Phase 4 benchmark suite |
| `HelixAudio.Backend.Wasapi` | Real — output + loopback + **input** | Device enum, `WasapiOut`, `SystemLoopbackCapture`, `WasapiInputCapture` (added: mic/line-in float capture) |
| ~~`HelixAudio.Backend.Alsa` / `.CoreAudio`~~ | **REMOVED** | Empty csproj shells for non-Windows platforms — deleted |
| `HelixAudio.PluginHost` | Partial — managed-DLL only | Reflection-based `IAudioPlugin` load/scan/unload; **no state blobs, no buses, no crash isolation, stringly-typed params** |
| ~~`HelixAudio.PluginHost.VST3`~~ | **REMOVED** | `NotImplementedException` stub — deleted; real VST3 hosting deferred to DAW phase |
| ~~`HelixAudio.PluginHost.CLAP`~~ | **REMOVED** | `NotImplementedException` stub — deleted; same deferral |
| `AudioForge.SoundEngine` | Real, small | Clean facade: `RealtimePlayer` + meter/EQ/tap chain + `OutputTaps` + loopback processor |

---

## 3. Critical corrections to earlier claims

These were stated during development and are **false at code level**:

1. **"VST3/CLAP hosting exists"** — both host classes are 24-line stubs.
   Only the managed-`IAudioPlugin` DLL host is real. There is no native
   plugin hosting of any kind.
2. **"Streaming/HLS/DASH"** — wrappers only; no adaptive-stream logic.
3. **"MIDI support"** — data types exist; file/synth/I/O are empty.
4. **"Compressor/reverb/delay processors"** — real DSP exists but as
   non-node classes; they cannot be inserted into any `IAudioNode` chain.
5. **"3D/spatial audio"** — pass-through stubs.

Any spec or documentation referencing these must be corrected.

---

## 4. Cross-cutting structural problems

### 4.1 Competing abstractions
At least four parallel processor models exist:

- `IAudioNode` / `AudioNode` (whole-buffer `float[]`, allocates)
- `AnalysisBase` / `EffectBase` / `FilterBase` / `MixerBase` (non-node)
- `DSPNode` (per-sample base) and `EngineNode` variants
- Duplicate type names across namespaces: `AudioBuffer` ×2,
  `EngineNode` ×2, `AudioEngine` ×2 (Core vs Engine), `MixerNode` ×2
  (Mixing vs Engine), `EffectNode` ×2, `NodeGraph` ×2

**SDK extraction requires consolidation to a single node contract.**

### 4.2 Realtime safety is inconsistent
- Allocation-conscious: `RealtimePlayer` feed path, `WasapiDeviceManager`,
  `LevelMeterNode`, `StereoMixerNode`, taps.
- Allocating per block: `ProcessingGraph.Process`, `NodeGraph.Process`,
  `AudioMixer`, `DSPNode.Process`, all `Effects`/`Filters` processors.
- `AudioEngine.Process` (Engine) holds a lock across the whole render.
- `AudioDiagnostics.Log` performs file I/O from `ProcessingGraph` per
  node per block (rate-limited, still unsafe).

### 4.3 No parameter / automation / state model
- No typed parameters (min/max/default/unit/taper) anywhere.
- No sample-accurate automation lanes.
- No `GetState()/SetState()` on nodes; EQ presets live in the WinForms
  UI layer (JSON/SQLite), not the engine.
- `PluginParameter` DTO exists but is unused by the host.

### 4.4 Multichannel is nominal
`ChannelLayout` enum exists (Mono/Stereo/5.1/7.1) but most processors
hardcode stereo (`frames*2`); `PanMixer` stereo-only; `Resampler`
treats interleaved multichannel as one stream.

### 4.5 Resampling is naive
`Resampler` = linear or 16-tap windowed-sinc per whole buffer, no
polyphase, no per-channel handling, allocates per call.
`RealtimePlayer` initializes the device at the file's sample rate —
**no SRC between decode rate and device rate.**

### 4.6 Decode is whole-file
`MediaFoundationDecoder` buffers entire files into `List<float>`;
no chunked/streaming decode. Memory-heavy for large files.

### 4.7 Encode/write gaps
- WAV write is PCM-int only — float WAV rejected by `WavReader` and not
  written by `WavWriter`; no RF64.
- No MP3/FLAC/AAC/Opus encoders.
- Integer output has **no dither/noise shaping**.

### 4.8 Transport/clock absent
`AudioClock` is a manual counter; no sample-accurate transport position,
tempo map, MIDI clock, or plugin-delay-compensation (PDC).

---

## 5. Gaps vs the two product specs

### 5.1 AudioForge-DAW-Spec (`H:\future projects\AudioForge-DAW-Spec.md`)

Spec §11 claimed several items "already present" — audit shows otherwise:

| Spec need | Actual state |
|---|---|
| WASAPI record endpoint | **Absent** — `Read()` returns silence; `WasapiCapture`/`WasapiInputThread` stubs |
| N-channel mixer/buses/sends | **Absent** — only `StereoMixerNode`; `MultiTrackMixer` is offline batch |
| Compressor/limiter/gate as nodes | DSP exists, **not nodes** |
| Reverb/delay/chorus nodes | DSP exists in Effects/Core, **not nodes** |
| Pitch shift / time stretch / warp | **Nothing exists** — no phase vocoder, WSOLA, or PSOLA |
| Clip/region/session model, automation, take lanes | **Nothing engine-side** |
| VST3/CLAP hosting | **Stubs** — biggest gap vs "plugin ecosystem" strategy |
| Multichannel tracks | `ChannelLayout` enum only; `SurroundNode` naive |
| PDC / monitoring latency compensation | Absent |
| Export | `OfflineRenderer` real but **WAV-only**; no MP3/FLAC encode |

### 5.2 AudioForge-Forensics-Spec (`H:\future projects\AudioForge-Forensics-Spec.md`)

Confirmed reusable (present and real):

- EBU R128/BS.1770 loudness (`LoudnessMeter`, `LoudnessMeterNode`)
- True peak (`TruePeakNode`, 4× sinc oversample)
- Spectrum/spectrogram/waveform taps
- `HumDetector`, `PitchTracker`, `NoiseFloorEstimator`
- `SignalStatistics`, `PeakFileGenerator`
- Codec provenance hooks (`DecoderBackend`, `IsVerifiedDecoder`,
  `CodecTrust`) — good forensic posture already

Greenfield (nothing exists):

- `DeclipNode`, `DenoiseNode`, `DeEsserNode`, `SpectralEditNode` —
  the entire P0 restoration suite
- File forensics: `FormatDetector` only detects RIFF/WAVE; no
  atom/chunk dump, no hash helpers, no encoder-string/provenance
  extraction, no generation-loss measurement; `AudioMetadata` is an
  unused POCO
- Batch render pipeline; report-generation hooks
- Multichannel >2ch for film/audio evidence

---

## 6. Recommended work order before SDK extraction

1. **Consolidate the node contract** — one `IAudioNode`/processor
   abstraction; migrate or delete `EffectBase`/`FilterBase`/
   `AnalysisBase`/`DSPNode`/`EngineNode` hierarchies and duplicate
   type names.
2. **Realtime-safety pass** — zero-allocation `Process` paths
   (preallocated buffers / `Span<float>` / `ArrayPool`), remove
   per-block file I/O and whole-render locks.
3. **Honest stub resolution** — implement or remove: VST3/CLAP hosts,
   MIDI project, HLS/DASH streaming, DSP.3D, Visualization/Benchmarks
   empty projects, `WASAPIBackend` placeholder, `MixerNode` stub,
   `EngineController` stubs.
4. **Missing fundamentals** — typed parameter model, state
   serialization, automation lanes, per-channel polyphase resampler,
   float WAV + dither, encode codecs, chunked decode, WASAPI capture.
5. **PDC/transport** — latency reporting + compensation, sample-accurate
   position.
6. **Then extract** `H:\AudioForge.SDK\AudioForge.SoundEngine` (or
   renamed SDK repo) with README/license/CI.

---

## 7. Risk register

| Risk | Severity | Mitigation |
|---|---|---|
| Shipping SDK with stubbed VST3/CLAP marketed as "plugin hosting" | High | Implement real host or cut the claim |
| Per-block allocations in `ProcessingGraph`/`NodeGraph` under DAW load | High | RT-safety pass before DAW work |
| Duplicate/competing abstractions confuse SDK consumers | Medium | Consolidation pass (item 6.1) |
| Whole-file decode limits forensic/batch scale | Medium | Chunked decode in codec layer |
| No state serialization blocks session/project formats | High | Parameter/state model is prerequisite for DAW + forensics reporting |
| Stereo-hardcoded DSP breaks multichannel forensics | Medium | Channel-layout plumbing early |
