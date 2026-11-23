using AudioForge.Core;
using AudioForge.DSP.Analysis;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.DSP;

/// <summary>
/// Transparent <see cref="IAudioNode"/> that computes smoothed magnitude
/// spectra of the interleaved buffer passing through it. Audio is analysed
/// per channel (mono mix available via <see cref="SpectrumDb"/>), accumulated
/// into windowed FFT frames with no overlap, and the resulting dBFS
/// magnitudes are exposed for UI polling. FFT size, window function and
/// channel count are configurable. Allocates nothing per block after
/// construction.
/// </summary>
public sealed class SpectrumTapNode : AudioNode
{
    private readonly object _gate = new();
    private float[] _time;            // channels * fftSize, channel-major
    private float[] _real = Array.Empty<float>();
    private float[] _imag = Array.Empty<float>();
    private float[] _window = Array.Empty<float>();
    private float[] _monoDb = Array.Empty<float>();
    private float[][] _channelDb = Array.Empty<float[]>();
    private int _fill;
    private int _channels = 2;
    private bool _hasData;

    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="fftSize">FFT size - power of two, 256 to 65536 (default 2048).</param>
    /// <param name="window">Window function applied before the FFT.</param>
    /// <param name="name">Node name.</param>
    public SpectrumTapNode(int sampleRate, int channels, int fftSize = 2048,
        WindowType window = WindowType.Hann, string name = "SpectrumTap")
        : base(sampleRate, channels, name)
    {
        if (fftSize < 256 || fftSize > 65536 || (fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of two between 256 and 65536.", nameof(fftSize));

        FftSize = fftSize;
        Window = window;
        _channels = Math.Max(1, channels);
        _window = WindowFunctions.GenerateProfile(window, fftSize);
        _time = new float[_channels * fftSize];
        _real = new float[fftSize];
        _imag = new float[fftSize];
        _monoDb = new float[fftSize / 2];
        Array.Fill(_monoDb, -120f);
        _channelDb = new float[_channels][];
        for (int c = 0; c < _channels; c++)
        {
            _channelDb[c] = new float[fftSize / 2];
            Array.Fill(_channelDb[c], -120f);
        }
    }

    /// <summary>FFT size in points.</summary>
    public int FftSize { get; private set; }

    /// <summary>Window function used before the FFT.</summary>
    public WindowType Window { get; private set; }

    /// <summary>Mono-mixed magnitude spectrum in dBFS, one value per bin.</summary>
    public float[] SpectrumDb
    {
        get { lock (_gate) return (float[])_monoDb.Clone(); }
    }

    /// <summary>
    /// Per-channel magnitude spectra in dBFS. Index [channel][bin].
    /// Channel count follows the prepared audio format.
    /// </summary>
    public float[][] ChannelSpectraDb
    {
        get
        {
            lock (_gate)
            {
                var copy = new float[_channelDb.Length][];
                for (int c = 0; c < _channelDb.Length; c++)
                    copy[c] = (float[])_channelDb[c].Clone();
                return copy;
            }
        }
    }

    /// <summary>
    /// Reconfigures FFT size and/or window at runtime. In-progress frame
    /// accumulation is reset; subsequent blocks use the new parameters.
    /// Safe to call from the UI thread while playing.
    /// </summary>
    public void SetFftParameters(int fftSize, WindowType? window = null)
    {
        if (fftSize < 256 || fftSize > 65536 || (fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of two between 256 and 65536.", nameof(fftSize));

        FftSize = fftSize;
        if (window.HasValue) Window = window.Value;
        _window = WindowFunctions.GenerateProfile(Window, fftSize);
        _time = new float[_channels * fftSize];
        _real = new float[fftSize];
        _imag = new float[fftSize];
        int bins = fftSize / 2;
        _monoDb = new float[bins];
        _channelDb = new float[_channels][];
        for (int c = 0; c < _channels; c++)
        {
            _channelDb[c] = new float[bins];
            Array.Fill(_channelDb[c], -120f);
        }
        Array.Fill(_monoDb, -120f);
        _fill = 0;
        lock (_gate) _hasData = false;
    }

    /// <summary>Hz per spectrum bin.</summary>
    public float BinWidthHz => (float)(Format?.SampleRate ?? SampleRate) / FftSize;

    // ---- Parameter / state model (IAudioNode extensions) ----

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => _params;

    private static readonly AudioParameterInfo[] _params =
    {
        new("fft.size", 256f, 65536f, 2048f),
        new("window", 0f, 3f, (float)WindowType.Hann),
    };

    public override bool TryGetParameter(string name, out float value)
    {
        value = name switch
        {
            "fft.size" => FftSize,
            "window" => (float)Window,
            _ => float.NaN,
        };
        return !float.IsNaN(value);
    }

    public override bool TrySetParameter(string name, float value)
    {
        switch (name)
        {
            case "fft.size":
                int size = (int)value;
                if (size < 256 || size > 65536 || (size & (size - 1)) != 0) return false;
                SetFftParameters(size);
                return true;
            case "window":
                if (value < 0f || value > 3f) return false;
                SetFftParameters(FftSize, (WindowType)(int)value);
                return true;
            default:
                return false;
        }
    }

    public override JsonObject GetState() => new()
    {
        ["fftSize"] = FftSize,
        ["window"] = (int)Window,
    };

    public override void SetState(JsonObject state)
    {
        if (state is null) return;
        int size = FftSize;
        WindowType win = Window;
        if (state["fftSize"] is JsonValue fs) size = fs.GetValue<int>();
        if (state["window"] is JsonValue w) win = (WindowType)w.GetValue<int>();
        if (size != FftSize || win != Window)
            SetFftParameters(size, win);
    }

    /// <summary>True once at least one full FFT frame has been analysed.</summary>
    public bool HasData { get { lock (_gate) return _hasData; } }

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        int ch = Math.Max(1, format.Channels);
        if (ch != _channels)
        {
            // Reallocate channel buffers; history is reset.
            _channels = ch;
            _time = new float[ch * FftSize];
            _channelDb = new float[ch][];
            for (int c = 0; c < ch; c++)
            {
                _channelDb[c] = new float[FftSize / 2];
                Array.Fill(_channelDb[c], -120f);
            }
            Array.Fill(_monoDb, -120f);
            _fill = 0;
            lock (_gate) _hasData = false;
        }
    }

    public override void Reset()
    {
        _fill = 0;
        lock (_gate)
        {
            Array.Fill(_monoDb, -120f);
            foreach (var ch in _channelDb) Array.Fill(ch, -120f);
            _hasData = false;
        }
    }

    public override void Process(Span<float> buffer)
    {
        int channels = Math.Max(1, Format?.Channels ?? _channels);
        int frames = buffer.Length / channels;

        for (int i = 0; i < frames && _fill < FftSize; i++)
        {
            int offset = i * channels;
            for (int ch = 0; ch < channels && ch < _channels; ch++)
                _time[ch * FftSize + _fill] = buffer[offset + ch];
            _fill++;
        }

        if (_fill >= FftSize)
            AnalyzeFrame(channels);
    }

    private void AnalyzeFrame(int inputChannels)
    {
        int bins = FftSize / 2;
        int used = Math.Min(inputChannels, _channels);

        // Per-channel spectra, then a true mono mix (average PCM -> FFT),
        // matching what a mono listener would actually hear.
        var channelDb = new float[used][];
        var monoDbFrame = new float[bins];

        for (int ch = 0; ch < used; ch++)
        {
            Array.Clear(_real);
            Array.Clear(_imag);
            int tbase = ch * FftSize;
            for (int i = 0; i < FftSize; i++)
                _real[i] = _time[tbase + i] * _window[i];
            FFT.Compute(_real, _imag);

            var db = new float[bins];
            for (int bin = 0; bin < bins; bin++)
            {
                float mag = MathF.Sqrt(_real[bin] * _real[bin] + _imag[bin] * _imag[bin]) / FftSize;
                if (bin != 0 && bin != bins - 1) mag *= 2f;
                db[bin] = mag > 1e-7f ? 20f * MathF.Log10(mag) : -120f;
            }
            channelDb[ch] = db;
        }

        // Mono mixdown FFT
        Array.Clear(_real);
        Array.Clear(_imag);
        for (int i = 0; i < FftSize; i++)
        {
            float sum = 0f;
            for (int ch = 0; ch < used; ch++)
                sum += _time[ch * FftSize + i];
            _real[i] = (sum / used) * _window[i];
        }
        FFT.Compute(_real, _imag);
        for (int bin = 0; bin < bins; bin++)
        {
            float mag = MathF.Sqrt(_real[bin] * _real[bin] + _imag[bin] * _imag[bin]) / FftSize;
            if (bin != 0 && bin != bins - 1) mag *= 2f;
            monoDbFrame[bin] = mag > 1e-7f ? 20f * MathF.Log10(mag) : -120f;
        }

        lock (_gate)
        {
            for (int ch = 0; ch < used; ch++)
            {
                var target = _channelDb[ch];
                var src = channelDb[ch];
                for (int bin = 0; bin < bins; bin++)
                    target[bin] = src[bin] > target[bin]
                        ? src[bin]
                        : target[bin] * 0.985f + src[bin] * 0.015f;
            }
            for (int bin = 0; bin < bins; bin++)
                _monoDb[bin] = monoDbFrame[bin] > _monoDb[bin]
                    ? monoDbFrame[bin]
                    : _monoDb[bin] * 0.985f + monoDbFrame[bin] * 0.015f;
            _hasData = true;
        }
        _fill = 0;
    }
}
