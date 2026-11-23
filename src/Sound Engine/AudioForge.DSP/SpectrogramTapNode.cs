using AudioForge.Core;
using AudioForge.DSP.Analysis;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.DSP;

/// <summary>
/// Transparent <see cref="IAudioNode"/> that builds a scrolling spectrogram:
/// successive windowed FFT frames are stored as magnitude columns (dBFS) in a
/// ring buffer covering a configurable time span. Per-channel or mono-mixed
/// columns are supported. This is the engine-side data source for
/// spectrogram displays and forensic spectral-history analysis.
/// </summary>
public sealed class SpectrogramTapNode : AudioNode
{
    private readonly object _gate = new();
    private float[] _time = Array.Empty<float>();
    private float[] _real = Array.Empty<float>();
    private float[] _imag = Array.Empty<float>();
    private float[] _window = Array.Empty<float>();
    private float[] _columns;       // channels * maxCols * bins, column-major ring
    private int _fill;
    private int _writeCol;
    private int _colCount;
    private int _activeChannels;
    private bool _hasData;
    private readonly double _historySeconds;

    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="fftSize">FFT size - power of two, 256 to 16384 (default 2048).</param>
    /// <param name="window">Window function.</param>
    /// <param name="historySeconds">Seconds of spectrogram history retained.</param>
    /// <param name="perChannel">True = one column set per channel; false = mono mix.</param>
    /// <param name="name">Node name.</param>
    public SpectrogramTapNode(int sampleRate, int channels, int fftSize = 2048,
        WindowType window = WindowType.Hann, double historySeconds = 10.0,
        bool perChannel = true, string name = "SpectrogramTap")
        : base(sampleRate, channels, name)
    {
        if (fftSize < 256 || fftSize > 16384 || (fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of two between 256 and 16384.", nameof(fftSize));
        if (historySeconds <= 0) throw new ArgumentOutOfRangeException(nameof(historySeconds));

        FftSize = fftSize;
        BinCount = fftSize / 2;
        Window = window;
        PerChannel = perChannel;
        _historySeconds = historySeconds;
        _window = WindowFunctions.GenerateProfile(window, fftSize);
        _activeChannels = Math.Max(1, channels);

        // Columns are produced once per FFT frame; estimate frame rate and
        // size the ring for the requested history span.
        double framesPerSec = sampleRate / (double)fftSize;
        MaxColumns = Math.Max(8, (int)Math.Ceiling(framesPerSec * historySeconds));
        int colChannels = perChannel ? _activeChannels : 1;
        _columns = new float[colChannels * MaxColumns * BinCount];
        _time = new float[_activeChannels * fftSize];
        _real = new float[fftSize];
        _imag = new float[fftSize];
    }

    /// <summary>FFT size in points.</summary>
    public int FftSize { get; private set; }

    /// <summary>Number of magnitude bins per column (fftSize / 2).</summary>
    public int BinCount { get; private set; }

    /// <summary>Window function used.</summary>
    public WindowType Window { get; private set; }

    /// <summary>Whether columns are stored per channel (true) or mono-mixed.</summary>
    public bool PerChannel { get; }

    /// <summary>Maximum number of time columns retained.</summary>
    public int MaxColumns { get; private set; }

    /// <summary>Hz per bin.</summary>
    public float BinWidthHz => (float)(Format?.SampleRate ?? SampleRate) / FftSize;

    /// <summary>Seconds of history per column.</summary>
    public double ColumnSeconds => (double)FftSize / Math.Max(1, SampleRate);

    /// <summary>True once at least one column has been produced.</summary>
    public bool HasData { get { lock (_gate) return _hasData; } }

    /// <summary>Number of valid columns currently stored (0..MaxColumns).</summary>
    public int ColumnCount { get { lock (_gate) return _colCount; } }

    /// <summary>Number of column sets stored (1 for mono, channels for per-channel).</summary>
    public int StoredChannels => PerChannel ? _activeChannels : 1;

    /// <summary>
    /// Reconfigures FFT size and window at runtime. Clears accumulated history
    /// and resizes all buffers. Safe to call from the UI thread while playing.
    /// </summary>
    public void SetFftParameters(int fftSize, WindowType window)
    {
        if (fftSize < 256 || fftSize > 16384 || (fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of two between 256 and 16384.", nameof(fftSize));

        lock (_gate)
        {
            FftSize = fftSize;
            BinCount = fftSize / 2;
            Window = window;
            _window = WindowFunctions.GenerateProfile(window, fftSize);

            double framesPerSec = (Format?.SampleRate ?? SampleRate) / (double)fftSize;
            MaxColumns = Math.Max(8, (int)Math.Ceiling(framesPerSec * _historySeconds));
            int colChannels = PerChannel ? _activeChannels : 1;
            _columns = new float[colChannels * MaxColumns * BinCount];
            _time = new float[_activeChannels * fftSize];
            _real = new float[fftSize];
            _imag = new float[fftSize];
            _fill = 0;
            _writeCol = 0;
            _colCount = 0;
            _hasData = false;
        }
    }

    // ---- Parameter / state model (IAudioNode extensions) ----

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => _params;

    private static readonly AudioParameterInfo[] _params =
    {
        new("fft.size", 256f, 16384f, 2048f),
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
                if (size < 256 || size > 16384 || (size & (size - 1)) != 0) return false;
                SetFftParameters(size, Window);
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

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        int ch = Math.Max(1, format.Channels);
        if (ch != _activeChannels)
        {
            _activeChannels = ch;
            int colChannels = PerChannel ? ch : 1;
            _columns = new float[colChannels * MaxColumns * BinCount];
            _time = new float[ch * FftSize];
            _fill = 0;
            _writeCol = 0;
            _colCount = 0;
            lock (_gate) _hasData = false;
        }
    }

    public override void Reset()
    {
        _fill = 0;
        lock (_gate)
        {
            _writeCol = 0;
            _colCount = 0;
            _hasData = false;
        }
    }

    public override void Process(Span<float> buffer)
    {
        // Locked so SetFftParameters can't swap buffers mid-frame.
        lock (_gate)
        {
        int channels = Math.Max(1, Format?.Channels ?? _activeChannels);
        int frames = buffer.Length / channels;

        for (int i = 0; i < frames && _fill < FftSize; i++)
        {
            int offset = i * channels;
            for (int ch = 0; ch < channels && ch < _activeChannels; ch++)
                _time[ch * FftSize + _fill] = buffer[offset + ch];
            _fill++;
        }

        if (_fill >= FftSize)
            AnalyzeFrame(channels);
        }
    }

    private void AnalyzeFrame(int inputChannels)
    {
        int bins = BinCount;
        int colChannels = PerChannel ? Math.Min(inputChannels, _activeChannels) : 1;
        var mags = new float[colChannels * bins];

        for (int set = 0; set < colChannels; set++)
        {
            // Mono mode: average all input channels into channel slot 0's frame
            if (!PerChannel)
            {
                Array.Clear(_real);
                Array.Clear(_imag);
                for (int i = 0; i < FftSize; i++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < inputChannels && ch < _activeChannels; ch++)
                        sum += _time[ch * FftSize + i];
                    _real[i] = (sum / Math.Max(1, Math.Min(inputChannels, _activeChannels))) * _window[i];
                }
            }
            else
            {
                Array.Clear(_real);
                Array.Clear(_imag);
                int tbase = set * FftSize;
                for (int i = 0; i < FftSize; i++)
                    _real[i] = _time[tbase + i] * _window[i];
            }

            FFT.Compute(_real, _imag);
            for (int bin = 0; bin < bins; bin++)
            {
                float mag = MathF.Sqrt(_real[bin] * _real[bin] + _imag[bin] * _imag[bin]) / FftSize;
                if (bin != 0 && bin != bins - 1) mag *= 2f;
                mags[set * bins + bin] = mag > 1e-7f ? 20f * MathF.Log10(mag) : -120f;
            }
        }

        lock (_gate)
        {
            int colStride = bins;
            int writeBase = _writeCol * colChannels * bins;
            for (int i = 0; i < mags.Length; i++)
                _columns[writeBase + i] = mags[i];
            _writeCol = (_writeCol + 1) % MaxColumns;
            _colCount = Math.Min(_colCount + 1, MaxColumns);
            _hasData = true;
        }
        _fill = 0;
    }

    /// <summary>
    /// Copies the retained spectrogram into a caller-provided array.
    /// Layout: [channelSet][timeColumn][bin], chronological order (oldest first).
    /// </summary>
    /// <param name="destination">float[sets * columns * bins].</param>
    /// <returns>Number of valid columns copied.</returns>
    public int CopyTo(float[] destination)
    {
        lock (_gate)
        {
            int sets = StoredChannels;
            int cols = _colCount;
            int bins = BinCount;
            if (destination.Length < sets * cols * bins) return 0;

            // Storage layout is [column][set][bin]; destination is [set][column][bin]
            int oldest = cols < MaxColumns ? 0 : _writeCol;
            for (int s = 0; s < sets; s++)
                for (int c = 0; c < cols; c++)
                {
                    int srcCol = (oldest + c) % MaxColumns;
                    int srcBase = (srcCol * sets + s) * bins;
                    int dstBase = (s * cols + c) * bins;
                    Array.Copy(_columns, srcBase, destination, dstBase, bins);
                }
            return cols;
        }
    }

    /// <summary>Allocates and returns the full spectrogram snapshot.</summary>
    public float[] Snapshot()
    {
        lock (_gate)
        {
            int sets = StoredChannels;
            int cols = _colCount;
            var result = new float[sets * Math.Max(1, cols) * BinCount];
            if (cols == 0) return result;
            int copied = CopyTo(result);
            return copied == cols ? result : result;
        }
    }

    /// <summary>
    /// Atomically returns the snapshot AND the dimensions it was captured with.
    /// Use this instead of reading ColumnCount/BinCount separately - the node
    /// can be reconfigured (SetFftParameters) between calls.
    /// </summary>
    public (int Sets, int Cols, int Bins, float[] Data) SnapshotDetailed()
    {
        lock (_gate)
        {
            int sets = StoredChannels;
            int cols = _colCount;
            int bins = BinCount;
            var data = new float[sets * Math.Max(1, cols) * bins];
            if (cols > 0) CopyTo(data);
            return (sets, cols, bins, data);
        }
    }
}
