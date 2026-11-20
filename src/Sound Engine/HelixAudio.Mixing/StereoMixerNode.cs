using HelixAudio.Core;
using System;

namespace HelixAudio.Mixing;

/// <summary>
/// Per-channel strip state for <see cref="StereoMixerNode"/>.
/// </summary>
public sealed class MixerStrip
{
    /// <summary>Linear gain 0..2 (1.0 = unity).</summary>
    public float Gain { get; set; } = 1.0f;

    /// <summary>Mute this channel.</summary>
    public bool Mute { get; set; }

    /// <summary>Solo this channel (any solo silences all non-soloed channels).</summary>
    public bool Solo { get; set; }
}

/// <summary>
/// Transparent <see cref="IAudioNode"/> implementing a stereo mixer: per-channel
/// gain, mute and solo on interleaved PCM, plus a stereo-balance control and
/// per-channel peak/RMS metering. Strip parameters are safe to adjust live
/// from the UI thread.
/// </summary>
public sealed class StereoMixerNode : AudioNode
{
    private MixerStrip[] _strips;
    private readonly object _gate = new();
    private float[] _peakDb;
    private float[] _rmsDb;
    private float[] _meterPeaks;
    private double[] _meterSumSq;
    private int _activeChannels;
    private float _balance; // -1 = hard left, 0 = centre, +1 = hard right

    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Input channel count (typically 2).</param>
    /// <param name="name">Node name.</param>
    public StereoMixerNode(int sampleRate, int channels, string name = "StereoMixer")
        : base(sampleRate, channels, name)
    {
        _activeChannels = Math.Max(1, channels);
        _strips = new MixerStrip[_activeChannels];
        for (int i = 0; i < _activeChannels; i++) _strips[i] = new MixerStrip();
        _peakDb = new float[_activeChannels];
        _rmsDb = new float[_activeChannels];
        Array.Fill(_peakDb, -120f);
        Array.Fill(_rmsDb, -120f);
    }

    /// <summary>Per-channel strip controls (index = input channel).</summary>
    public MixerStrip[] Strips => _strips;

    /// <summary>
    /// Stereo balance -1 (left only) .. 0 (centre) .. +1 (right only).
    /// Equal-power law; ignored for non-stereo input.
    /// </summary>
    public float Balance
    {
        get => _balance;
        set => _balance = Math.Clamp(value, -1f, 1f);
    }

    /// <summary>Per-channel instantaneous peak in dBFS (-120 floor).</summary>
    public float[] PeakDbPerChannel { get { lock (_gate) return (float[])_peakDb.Clone(); } }

    /// <summary>Per-channel smoothed RMS in dBFS.</summary>
    public float[] RmsDbPerChannel { get { lock (_gate) return (float[])_rmsDb.Clone(); } }

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        int ch = Math.Max(1, format.Channels);
        if (ch != _activeChannels)
        {
            int old = _activeChannels;
            _activeChannels = ch;
            Array.Resize(ref _strips, ch);
            for (int i = old; i < ch; i++) _strips[i] = new MixerStrip();
            _peakDb = new float[ch];
            _rmsDb = new float[ch];
            Array.Fill(_peakDb, -120f);
            Array.Fill(_rmsDb, -120f);
        }
    }

    public override void Reset()
    {
        lock (_gate)
        {
            Array.Fill(_peakDb, -120f);
            Array.Fill(_rmsDb, -120f);
        }
    }

    public override void Process(Span<float> buffer)
    {
        int ch = Math.Max(1, Format?.Channels ?? _activeChannels);
        int frames = buffer.Length / ch;
        if (frames <= 0) return;

        bool anySolo = false;
        for (int c = 0; c < ch && c < _strips.Length; c++)
            if (_strips[c].Solo) { anySolo = true; break; }

        // Per-channel gain/mute/solo
        for (int f = 0; f < frames; f++)
        {
            int offset = f * ch;
            for (int c = 0; c < ch && c < _strips.Length; c++)
            {
                var s = _strips[c];
                bool active = !s.Mute && (!anySolo || s.Solo);
                buffer[offset + c] = active ? buffer[offset + c] * s.Gain : 0f;
            }
        }

        // Stereo balance (equal-power)
        if (ch == 2)
        {
            float t = (_balance + 1f) * 0.5f;
            float leftAmt = (float)Math.Cos(t * MathF.PI / 2f);
            float rightAmt = (float)Math.Sin(t * MathF.PI / 2f);
            for (int f = 0; f < frames; f++)
            {
                int offset = f * 2;
                buffer[offset] *= leftAmt;
                buffer[offset + 1] *= rightAmt;
            }
        }

        // Post-mix metering (reused buffers - no per-block allocation)
        if (_meterPeaks is null || _meterPeaks.Length < ch)
        {
            _meterPeaks = new float[ch];
            _meterSumSq = new double[ch];
        }
        var peaks = _meterPeaks;
        var sumSq = _meterSumSq;
        Array.Clear(peaks, 0, ch);
        Array.Clear(sumSq, 0, ch);
        for (int f = 0; f < frames; f++)
        {
            int offset = f * ch;
            for (int c = 0; c < ch && c < _strips.Length; c++)
            {
                float a = MathF.Abs(buffer[offset + c]);
                if (a > peaks[c]) peaks[c] = a;
                sumSq[c] += (double)buffer[offset + c] * buffer[offset + c];
            }
        }
        lock (_gate)
        {
            for (int c = 0; c < ch && c < _peakDb.Length; c++)
            {
                _peakDb[c] = peaks[c] > 1e-6f ? 20f * MathF.Log10(peaks[c]) : -120f;
                double rms = frames > 0 ? Math.Sqrt(sumSq[c] / frames) : 0;
                float rmsDb = rms > 1e-7 ? 20f * (float)Math.Log10(rms) : -120f;
                _rmsDb[c] = _rmsDb[c] < -119f ? rmsDb : _rmsDb[c] + 0.4f * (rmsDb - _rmsDb[c]);
            }
        }
    }
}
