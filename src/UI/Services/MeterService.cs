using AudioForge.Core;
using AudioForge.DSP;
using System.Collections.Generic;

namespace MusicForge.UI.Services;

/// <summary>
/// Owns the meter and spectrum tap nodes that sit in the playback chain
/// around the EQ. The UI polls them on a timer for peak/RMS/clip and
/// pre/post-EQ spectrum display.
/// </summary>
public sealed class MeterService
{
    public static MeterService Instance { get; } = new MeterService();

    public LevelMeterNode Input { get; } = new(48000, 2, "InputMeter");
    public LevelMeterNode Output { get; } = new(48000, 2, "OutputMeter");
    public SpectrumTapNode PreEq { get; } = new(48000, 2, name: "PreEqSpectrum");
    public SpectrumTapNode PostEq { get; } = new(48000, 2, name: "PostEqSpectrum");
    public TruePeakNode TruePeak { get; } = new(48000, 2, "TruePeak");
    public AudioForge.Mixing.StereoMixerNode Mixer { get; } = new(48000, 2, "StereoMixer");
    public WaveformTapNode Waveform { get; } = new(48000, 2, historySeconds: 4.0, name: "WaveformTap");
    public SpectrogramTapNode Spectrogram { get; } = new(48000, 2, fftSize: 2048, historySeconds: 12.0, perChannel: true, name: "SpectrogramTap");

    private MeterService() { }

    /// <summary>Full processing chain: input meter -> pre spectrum -> EQ ->
    /// post spectrum -> CLAP plugins -> mixer -> output meter -> taps.
    /// The EQ node stays in the chain and short-circuits on bypass/disable
    /// so toggling works live mid-playback.</summary>
    public IEnumerable<IAudioNode> Chain()
    {
        var chain = new List<IAudioNode>
        {
            Input, PreEq, EqService.Instance.ActiveNode, PostEq
        };
        chain.AddRange(PluginService.Instance.ActiveNodes);
        chain.AddRange(new IAudioNode[]
        {
            Mixer, Output, TruePeak, Waveform, Spectrogram,
            NetworkSpeakersService.Instance.BroadcastNode
        });
        return chain;
    }
}
