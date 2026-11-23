$ErrorActionPreference = "Stop"
$specDir = "H:\Audio forge with helix sound engine using winforms\Specs"
New-Item -ItemType Directory -Force -Path $specDir | Out-Null
$outPath = Join-Path $specDir "Sound Engine Specs.docx"

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Add()

function Add-Para([string]$text, [string]$style = "Normal", [bool]$bold = $false) {
    $p = $doc.Content.Paragraphs.Add() | Out-Null
    $r = $doc.Paragraphs.Item($doc.Paragraphs.Count).Range
    $r.Text = $text
    try { $r.Style = $doc.Styles.Item($style) } catch {}
    if ($bold) { $r.Bold = 1 }
    $r.InsertParagraphAfter() | Out-Null
}

function Add-Bullet([string]$text) {
    $p = $doc.Content.Paragraphs.Add() | Out-Null
    $r = $doc.Paragraphs.Item($doc.Paragraphs.Count).Range
    $r.Text = $text
    try { $r.Style = $doc.Styles.Item("List Bullet") } catch { $r.Text = "- " + $text }
    $r.InsertParagraphAfter() | Out-Null
}

function Add-Heading1([string]$text) { Add-Para $text "Heading 1" $true }
function Add-Heading2([string]$text) { Add-Para $text "Heading 2" }

Add-Para "HELIX AUDIO ENGINE CAPABILITY SPECIFICATION" "Title" $true
Add-Para "AudioForge - Sound Engine Design Document"
Add-Para "Status: Working specification based on the AudioForge.* codebase under src\Sound Engine. Items marked [IMPLEMENTED] exist in code today. Items marked [PLANNED] are designed for but not yet functional. Items marked [STUB] have interfaces/scaffolding only."

Add-Heading1 "1. Purpose"
Add-Bullet "Describe every capability the Helix engine provides to Audio Forge: playback, mixing, DSP, analysis, visualisation data, recording, network audio, spatial audio, internet radio, plugins, automation, API surface, and performance envelope."
Add-Bullet "Define limitations and unsupported functionality so the UI can present honest capability reporting."

Add-Heading1 "2. Playback Engine"
Add-Heading2 "Audio formats"
Add-Bullet "[IMPLEMENTED] WAV / PCM - full read and write (WavReader, WavWriter, PcmReader, PcmWriter, FormatDetector; WavCodec wired through CodecManager)."
Add-Bullet "[IMPLEMENTED] MP3, AAC, WMA, ALAC read - decoded via NAudio AudioFileReader (Media Foundation on Windows) through CodecManager."
Add-Bullet "[IMPLEMENTED] FLAC read - decoded via PureFlac (pure managed, bit-exact, MD5-verified)."
Add-Bullet "[IMPLEMENTED] OGG Vorbis read - decoded via NVorbis."
Add-Bullet "[STUB] Opus, DSD - codec shells registered but report CanRead=false; decode not implemented."
Add-Bullet "Note: compressed decode is whole-file-to-32-bit-PCM-memory (not streaming) and decode-only - no compressed encode/write yet."
Add-Bullet "Core format model: AudioFormat (sample rate, channel count, float vs integer, bits per sample 8/16/24/32)."
Add-Heading2 "Streams and playback features"
Add-Bullet "[IMPLEMENTED] Simultaneous streams - bounded by CPU/memory, not a fixed engine limit. MultiTrackMixer and CrossfadeMixer support layered playback."
Add-Bullet "[PLANNED] Gapless playback - no gapless API exists yet."
Add-Bullet "[IMPLEMENTED] Crossfading - CrossfadeMixer class."
Add-Bullet "[PLANNED] ReplayGain - not implemented."
Add-Bullet "[PLANNED] Cue sheets - not implemented."

Add-Heading1 "3. Mixer Engine"
Add-Bullet "[IMPLEMENTED] Per-channel gain, pan (equal-power pan law, configurable in dB, default -3 dB), mute, solo, and enable. MixerProcessor.Process mixes N inputs to stereo L/R."
Add-Bullet "[IMPLEMENTED] Mixer variants: SimpleMixer, MultiTrackMixer, PanMixer, MixerChannel, MixerBase."
Add-Bullet "Maximum channels: no hard limit in code; practical limit is CPU and memory. Output stage is stereo (2 channels) in the current MixerProcessor."
Add-Bullet "Unlimited channels: not supported - channel count is fixed per buffer/mix pass."
Add-Bullet "[PLANNED] Subgroups, aux buses, matrix routing, bus-to-bus routing, multiple outputs, channel linking, mute groups, solo groups - the Engine ProcessingGraph (Input/Output/Mixer/Filter/Effect/Analysis nodes) provides the routing substrate, but dedicated bus/subgroup semantics are not yet implemented."

Add-Heading1 "4. DSP Engine"
Add-Bullet "Node types [IMPLEMENTED] in AudioForge.Core: GainNode, DelayNode, ChorusNode, FlangerNode, PhaserNode, ReverbNode, TremoloNode, VibratoNode, CompressorNode, LimiterNode, NoiseGateNode, PanNode, HighPassNode, LowPassNode, MeterNode, EffectNode."
Add-Bullet "DSP primitives [IMPLEMENTED] in AudioForge.DSP: BiquadFilter (peaking/low/high etc.), FIRFilter, IIRFilter, Convolution (impulse-response reverb), DelayLine, EnvelopeADSR, Oscillator, NoiseGenerator, Resampler, WindowFunctions, BufferUtils, DSPMath."
Add-Bullet "EQ [IMPLEMENTED]: GraphicEQ (BiquadCoefficients, EQPresets) and MasterEQ (MasterEQProcessor, MasterEQPresets)."
Add-Bullet "Stereo [IMPLEMENTED]: MidSide, StereoImagerProcessor."
Add-Bullet "[IMPLEMENTED] Chain architecture: two graph systems exist - AudioEngine (Core) chains IAudioNode processors in sequence; AudioForge.DSP NodeGraph and AudioForge.Engine ProcessingGraph support node graphs."
Add-Bullet "Multiple chains: possible via graph; not yet exposed as a managed multi-chain API."
Add-Bullet "Per-channel DSP, bus DSP, master DSP: graph architecture permits insertion at any node; dedicated per-channel/bus/master chain management is [PLANNED]."

Add-Heading1 "5. Visualiser Data"
Add-Bullet "[IMPLEMENTED] FFT - AudioForge.DSP.Analysis.FFT and SpectrumProcessor."
Add-Bullet "[IMPLEMENTED] Waveform - raw AudioBuffer float[] access at every node boundary."
Add-Bullet "[IMPLEMENTED] Spectrum - SpectrumAnalyzer (Analysis) + SpectrumProcessor (DSP)."
Add-Bullet "[IMPLEMENTED] Phase information - PhaseCorrelationNode (Analysis)."
Add-Bullet "[IMPLEMENTED] Loudness - LoudnessAnalyzer and LoudnessMeterNode (perceived-loudness measurement)."
Add-Bullet "[IMPLEMENTED] Correlation - PhaseCorrelationNode provides stereo correlation data."
Add-Bullet "[IMPLEMENTED] VisualizerDspHub (Engine) aggregates DSP state for visualisation."

Add-Heading1 "6. Analysis Engine"
Add-Bullet "[IMPLEMENTED] Peak metering - PeakDetector, PeakMeterNode."
Add-Bullet "[IMPLEMENTED] RMS metering - RMSAnalyzer, RMSMeterNode."
Add-Bullet "[IMPLEMENTED] Loudness metering - LoudnessAnalyzer, LoudnessMeterNode."
Add-Bullet "[IMPLEMENTED] Oscilloscope feed - waveform buffers available per node."
Add-Bullet "[IMPLEMENTED] Vectorscope feed - MidSide/Correlation data via Analysis nodes."
Add-Bullet "[IMPLEMENTED] Spectrogram feed - FFT/SpectrumProcessor can stream successive frames; no dedicated spectrogram accumulator yet [PLANNED]."
Add-Bullet "[IMPLEMENTED] ZeroCrossingRate for pitch/tonal analysis."

Add-Heading1 "7. Recording Engine"
Add-Bullet "[IMPLEMENTED] Device capture - WasapiCapture, WasapiInputThread, WasapiDeviceManager (WASAPI backend)."
Add-Bullet "[IMPLEMENTED] Stream/file writing - StreamWriter, PcmWriter, WavWriter."
Add-Bullet "[PLANNED] Record playback (loopback of output), microphone recording polish, stream recording, multi-track recording, loopback recording - WASAPI loopback capture is a backend capability but not yet wired into a recording API."

Add-Heading1 "8. Network Audio"
Add-Bullet "All items [PLANNED] - no implementation exists yet:"
Add-Bullet "Multi-room support, device discovery, speaker groups, synchronization, latency compensation. The Networked Speakers UI tab is prepared for these features."

Add-Heading1 "9. Spatial Audio"
Add-Bullet "[IMPLEMENTED] Stereo, 5.1, 7.1 channel layouts - ChannelLayout enum (Mono, Stereo, Surround51, Surround71)."
Add-Bullet "[IMPLEMENTED] 3D nodes in AudioForge.DSP.3D: SpatialAudioNode, SpatialNode, ListenerNode, BinauralNode, SurroundNode, AmbisonicNode (AmbisonicOrder enum), Quaternion, Vector3, SpatialMode."
Add-Bullet "HRTF: BinauralNode provides binaural rendering; dedicated HRTF dataset loading is [PLANNED]."
Add-Bullet "Object audio: not implemented; Ambisonic scene encoding is the current spatial format."

Add-Heading1 "10. Internet Radio"
Add-Bullet "[IMPLEMENTED] Stream protocols - HTTPStream, HLSStream, DASHStream, ShoutcastStream, IStream, AudioStreamer, StreamManager, StreamReader, StreamWriter."
Add-Bullet "[IMPLEMENTED] Metadata - MetadataUpdated events on stream classes."
Add-Bullet "[PLANNED] Stream recording (StreamWriter exists; radio-specific path not wired), stream failover/reconnect logic."

Add-Heading1 "11. Plugin Architecture"
Add-Bullet "[IMPLEMENTED] Managed plugin model - IAudioPlugin (Name, Author, Version, Type, Initialize, Reset, Process, GetParameters, SetParameter), PluginManager, PluginLoader, PluginNode, PluginParameterController, PluginHostStatistics, BuiltinPlugin."
Add-Bullet "[STUB] VST3 - VST3Host.cs exists but throws NotImplementedException."
Add-Bullet "[STUB] CLAP - CLAPHost.cs exists but throws NotImplementedException."
Add-Bullet "Plugin chains: supported through the node graph; per-plugin latency compensation reporting is [PLANNED]."

Add-Heading1 "12. Automation"
Add-Bullet "[IMPLEMENTED] Presets (data level) - EQPresets, MasterEQPresets in DSP."
Add-Bullet "[STUB] Pipeline presets - EngineController.LoadPipeline_DefaultPlayback / GraphicEQ / SpatialDemo are placeholders."
Add-Bullet "[PLANNED] Scenes, snapshots, macros, parameter scheduling/automation envelopes."

Add-Heading1 "13. API Integration (surface exposed to AudioForge)"
Add-Heading2 "Key classes"
Add-Bullet "AudioForge.Core: AudioEngine (node-chain Process), AudioNode, IAudioNode, IAudioSource, AudioBuffer, AudioFormat, AudioFormatConverter, AudioMixer, AudioPipeline, AudioClock, AudioCallback, AudioDiagnostics, AudioDevice, IAudioBackend, IAudioDeviceManager, WASAPIBackend, BackendOptions, LatencyProfile, ChannelLayout, SampleType, DeviceDirection, SilenceSource."
Add-Bullet "All effect nodes listed in section 4."
Add-Bullet "AudioForge.Engine: AudioEngine (state machine: Start/Stop/Pause/Resume, EngineState), EngineController, EngineConfig, EngineBase, ProcessingGraph, InputNode, OutputNode, MixerNode, FilterNode, EffectNode, AnalysisNode, VisualizerDspHub."
Add-Bullet "AudioForge.DSP: DSPNode, NodeGraph, all filters/envelopes/oscillators/resampler/FFT/spectrum/dynamics/EQ/stereo processors."
Add-Bullet "AudioForge.DSP.3D: spatial node set (section 9)."
Add-Bullet "AudioForge.Analysis: PeakDetector, RMSAnalyzer, LoudnessAnalyzer, SpectrumAnalyzer, ZeroCrossingRate + meter nodes."
Add-Bullet "AudioForge.IO: AudioStream, AudioMetadata, FormatDetector, Wav/Pcm readers-writers, codec decoders."
Add-Bullet "AudioForge.IO.Codecs: IAudioCodec, CodecManager, per-format codecs."
Add-Bullet "AudioForge.Mixing: MixerProcessor, MixerChannel, SimpleMixer, MultiTrackMixer, PanMixer, CrossfadeMixer."
Add-Bullet "AudioForge.Streaming: IStream and stream implementations."
Add-Bullet "AudioForge.PluginHost(.CLAP/.VST3): plugin model and hosts."
Add-Bullet "AudioForge.MIDI: MIDIFile, MIDIEvent, MIDIEnums, GeneralMIDI, Synthesizer, MIDIInput, MIDIOutput."
Add-Heading2 "Methods / events / callbacks"
Add-Bullet "IAudioNode.Process(float[]) -> float[] - per-buffer processing contract."
Add-Bullet "IAudioCodec.ReadAsync/WriteAsync(path, ..., CancellationToken)."
Add-Bullet "IStream.StartAsync/StopAsync/ReadAsync/PlayAsync; MetadataUpdated events on HTTP/HLS/DASH streams."
Add-Bullet "MIDIInput events: NoteOn, NoteOff, ControlChange, ProgramChange, PitchBend, SystemExclusive."
Add-Bullet "AudioDiagnostics logging API (e.g. LogError(component, message, exception))."
Add-Bullet "Plugin events/parameters via IAudioPlugin.GetParameters/SetParameter."
Add-Bullet "Data exposed to AudioForge: AudioBuffer samples, AudioFormat, meter/analysis node outputs, spectrum/FFT frames, loudness/correlation values, stream metadata events, plugin parameters, engine state."

Add-Heading1 "14. Performance"
Add-Bullet "Sample rates: any integer Hz via AudioFormat; actual support depends on WASAPI device (typically 44100/48000)."
Add-Bullet "Bit depths: integer 8/16/24/32 and 32-bit float internal processing."
Add-Bullet "Maximum tracks/streams: no fixed cap - CPU/memory bound; MultiTrackMixer handles arbitrary channel lists."
Add-Bullet "Maximum DSP chains: graph-size bound; no hard limit defined."
Add-Bullet "Memory: streaming/buffer based; CircularBuffer used in WASAPI backend; no global memory cap enforced."
Add-Bullet "Latency: LatencyProfile type exists for latency reporting; real-time callback path via AudioCallback and WASAPI event-driven threads. Target latency is device-buffer dependent, not yet benchmarked [PLANNED]."

Add-Heading1 "15. Limitations"
Add-Bullet "Opus and DSD decoding are NOT implemented (codecs registered but disabled). Compressed-format decoding covers MP3, FLAC, OGG Vorbis, AAC, WMA, ALAC - decode-only, whole-file-to-memory; no compressed encoding."
Add-Bullet "VST3 and CLAP hosting is stubbed - only managed IAudioPlugin plugins can load."
Add-Bullet "Backend coverage: WASAPI implemented; ALSA (Linux) and CoreAudio (macOS) projects exist but contain no source code. Windows-only in practice."
Add-Bullet "No gapless playback, ReplayGain, or cue sheet support."
Add-Bullet "No network audio (multi-room, sync, latency compensation) implemented."
Add-Bullet "Recording is capture-only at present; no loopback/multi-track recording API."
Add-Bullet "Mixer lacks subgroups, aux buses, matrix routing, and multiple physical outputs."
Add-Bullet "No scene/macro/scheduling automation system."
Add-Heading2 "Maximum supported sizes"
Add-Bullet "No hard limits coded. Practical ceilings: RAM for decoded buffers, WASAPI shared-mode buffer sizes, and GC pauses in the managed real-time path."

Add-Heading1 "16. Additional Items (beyond the core questions)"
Add-Bullet "Diagnostics: AudioDiagnostics central logging (component, message, exception)."
Add-Bullet "MIDI subsystem: file parsing (MIDIFile), General MIDI instruments, software Synthesizer, device MIDIInput/MIDIOutput with events."
Add-Bullet "AudioClock and AudioCallback for timing and real-time callback plumbing."
Add-Bullet "AudioFormatConverter for on-the-fly format/channel conversion; Resampler for sample-rate conversion."
Add-Bullet "Convolution engine enables impulse-response-based effects (cabinet/space simulation)."
Add-Bullet "NoiseGenerator (white/pink/etc.) and Oscillator for test-tone and synthesis sources; SilenceSource for gaps."
Add-Bullet "Two parallel engine implementations exist (AudioForge.Core chain-style and AudioForge.Engine graph-style) - consolidation is a design decision still open."
Add-Bullet "All engine projects target .NET 10 and build warning-clean (nullable warnings only)."

Add-Heading1 "17. Threading and Real-Time Model"
Add-Bullet "[IMPLEMENTED] WASAPI backend uses dedicated realtime threads: WasapiOutputThread (render) and WasapiInputThread (capture), driven by WASAPI events."
Add-Bullet "[IMPLEMENTED] CircularBuffer decouples the realtime callback thread from consumer/producer threads."
Add-Bullet "[IMPLEMENTED] AudioCallback delegate (float[] buffer) is the per-buffer processing contract; IAudioNode.Process(float[]) chains nodes per buffer."
Add-Bullet "[IMPLEMENTED] LatencyProfile enum models latency/quality trade-offs for buffer sizing."
Add-Bullet "[PLANNED] Lock-free ring buffers throughout the graph, GC-free realtime path (pre-allocated buffers), and measured callback deadline monitoring. Current design allocates float[] per Process call - acceptable now, needs pooling before heavy realtime use."

Add-Heading1 "18. Format Support Table"
$tblRange = $doc.Content.Paragraphs.Item($doc.Paragraphs.Count).Range
$ft = $doc.Tables.Add($tblRange, 11, 4)
$ft.Borders.Enable = 1
$hdr = @("Format", "Codec class", "Read", "Write")
for ($i = 0; $i -lt 4; $i++) { $ft.Cell(1, $i + 1).Range.Text = $hdr[$i]; $ft.Cell(1, $i + 1).Range.Bold = 1 }
$rows = @(
    @("WAV / PCM", "WavCodec, WavReader/Writer, PcmReader/Writer", "Yes", "Yes"),
    @("MP3", "Mp3Codec (NAudio MediaFoundation)", "Yes", "No"),
    @("FLAC", "FlacCodec (PureFlac)", "Yes", "No"),
    @("OGG Vorbis", "OggCodec (NVorbis)", "Yes", "No"),
    @("AAC", "AacCodec (NAudio MediaFoundation)", "Yes", "No"),
    @("ALAC", "AlacCodec (NAudio MediaFoundation)", "Yes", "No"),
    @("DSD", "DsdCodec", "No", "No"),
    @("Opus", "OpusCodec", "No", "No"),
    @("WMA", "WmaCodec (NAudio MediaFoundation)", "Yes", "No"),
    @("HTTP/HLS/DASH/Shoutcast streams", "Streaming project (IStream impls)", "Partial", "N/A")
)
for ($r = 0; $r -lt $rows.Count; $r++) {
    for ($c = 0; $c -lt 4; $c++) { $ft.Cell($r + 2, $c + 1).Range.Text = $rows[$r][$c] }
}
Add-Para "" "Normal"

Add-Heading1 "19. API Quick Reference"
Add-Heading2 "Playback / engine control"
Add-Bullet "AudioEngine (Core) - AddNode(IAudioNode), Process(float[]) - simple sequential chain."
Add-Bullet "AudioEngine (Engine) - Start(), Stop(), Pause(), Resume(), EngineState, ProcessingGraph - stateful graph engine."
Add-Bullet "EngineController - LoadPipeline_DefaultPlayback/GraphicEQ/SpatialDemo (stubs), ClearPipeline, Start/Stop/Pause/Resume, Engine property."
Add-Heading2 "Audio data"
Add-Bullet "AudioBuffer - Data (float[]), Frames, Channels, Length, Clear()."
Add-Bullet "AudioFormat - SampleRate, Channels, IsFloat, BitsPerSample; IEquatable."
Add-Bullet "AudioFormatConverter - converts between formats/channel counts."
Add-Heading2 "Devices / backends"
Add-Bullet "IAudioBackend, WASAPIBackend, IAudioDeviceManager, AudioDevice, DeviceDirection, BackendOptions."
Add-Heading2 "Analysis / metering"
Add-Bullet "PeakDetector, RMSAnalyzer, LoudnessAnalyzer, SpectrumAnalyzer, ZeroCrossingRate - analyzers returning values per buffer."
Add-Bullet "PeakMeterNode, RMSMeterNode, LoudnessMeterNode, PhaseCorrelationNode - node wrappers exposing data to the graph."
Add-Heading2 "Files / codecs / streams"
Add-Bullet "AudioStream (decoded buffer + format), AudioMetadata, FormatDetector."
Add-Bullet "IAudioCodec / CodecManager - ReadAsync/WriteAsync with CancellationToken."
Add-Bullet "IStream - Format, StartAsync, StopAsync, ReadAsync(count), PlayAsync, Dispose; MetadataUpdated events on stream classes."
Add-Heading2 "Plugins / MIDI"
Add-Bullet "IAudioPlugin, PluginManager, PluginLoader, PluginNode, PluginParameterController, PluginHostStatistics."
Add-Bullet "MIDIFile, MIDIEvent, GeneralMIDI, Synthesizer, MIDIInput (events: NoteOn/Off, CC, ProgramChange, PitchBend, SysEx), MIDIOutput."

Add-Heading1 "20. Data Flow"
Add-Para "Source (file/stream/device/MIDI/silence) -> Codec/Decoder or Stream reader -> AudioStream/AudioBuffer (float[]) -> ProcessingGraph nodes (filters, effects, EQ, analysis taps) -> Mixer (gain/pan/mute/solo, crossfade) -> Master section -> IAudioBackend (WASAPI render thread) -> Output device. Capture path mirrors this in reverse via WasapiCapture/InputThread.", "Normal" $false

Add-Heading1 "21. Roadmap (suggested order)"
Add-Bullet "1. Consolidate on one engine implementation (Core chain vs Engine graph) - decide the canonical API before UI wiring."
Add-Bullet "2. Streaming/incremental decode for large files (current decode buffers entire file in memory)."
Add-Bullet "3. Wire EngineController pipeline presets to real node graphs."
Add-Bullet "4. Buffer pooling + realtime-safe allocations in the DSP path."
Add-Bullet "5. ReplayGain + gapless playback for the Media tab."
Add-Bullet "6. VST3/CLAP host implementation (Plugins tab)."
Add-Bullet "7. Recording API (capture to file, loopback)."
Add-Bullet "8. Network audio layer for the Networked Speakers tab."

$savePath = [string]$outPath
$saveFmt = 16
$doc.SaveAs([ref]$savePath, [ref]$saveFmt)  # wdFormatXMLDocument = .docx
$doc.Close()
$word.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
Write-Output "Saved: $outPath"
