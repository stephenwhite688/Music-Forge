using System;
using System.Collections.Generic;

namespace AudioForge.Analysis;

/// <summary>
/// Broadcast loudness meter implementing ITU-R BS.1770 / EBU R128.
///
/// Pipeline per channel: K-weighting (high-shelf + RLB high-pass, coefficients
/// derived per sample rate) -> 400 ms gating blocks at 100 ms hops ->
/// absolute (-70 LUFS) and relative (-10 LU) gates -> integrated loudness.
/// Also produces short-term (3 s) loudness statistics for LRA and maximum,
/// sample peak, and true peak via 4x oversampling.
///
/// Feed interleaved PCM via <see cref="Process"/>; call <see cref="Finish"/>
/// then read the <see cref="Report"/>.
/// </summary>
public sealed class LoudnessMeter : IDisposable
{
    /// <summary>Gating block length in seconds (EBU R128).</summary>
    private const double BlockSeconds = 0.400;

    /// <summary>Gating hop in seconds.</summary>
    private const double HopSeconds = 0.100;

    /// <summary>Short-term window in seconds.</summary>
    private const double ShortTermSeconds = 3.0;

    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly float[] _channelWeights;

    // Per-channel K-weighting filter state (two biquads each).
    private readonly BiquadState[] _kStage1;
    private readonly BiquadState[] _kStage2;

    // Per-channel ring buffers of K-weighted samples (covers short-term window).
    private readonly float[][] _rings;
    private readonly int[] _ringPos;
    private readonly int _ringSize;

    // Per-channel true-peak oversamplers.
    private readonly TruePeakChannel[] _tpChannels;

    private readonly List<double> _blockEnergies = new();   // weighted mean-square per 400ms block
    private readonly List<double> _blockLoudness = new();
    private readonly List<double> _shortTermLoudness = new();

    private long _samplesProcessed;
    private long _nextHopSample;
    private double _samplePeak = 0;

    /// <summary>
    /// Result of a loudness measurement.
    /// </summary>
    public sealed class Report
    {
        /// <summary>Integrated loudness in LUFS (BS.1770 gated).</summary>
        public double IntegratedLufs { get; init; }

        /// <summary>Maximum short-term (3 s) loudness in LUFS, or NaN if unavailable.</summary>
        public double MaxShortTermLufs { get; init; }

        /// <summary>Loudness range in LU (P95 - P10 of short-term loudness), or NaN.</summary>
        public double LraLufs { get; init; }

        /// <summary>True peak in dBTP (4x oversampled).</summary>
        public double TruePeakDbTp { get; init; }

        /// <summary>Sample peak in dBFS.</summary>
        public double SamplePeakDbfs { get; init; }

        /// <summary>Per-channel true peaks in dBTP.</summary>
        public double[]? ChannelTruePeaksDbTp { get; init; }

        /// <summary>Number of gating blocks evaluated.</summary>
        public int BlockCount { get; init; }
    }

    /// <summary>
    /// Creates a meter for the given format.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Channel count.</param>
    /// <param name="channelWeights">Optional BS.1770 channel weights. Defaults:
    /// mono/stereo all 1.0; 6 channels assumed 5.1 (L,R,C,LFE,Ls,Rs) =
    /// {1,1,1,0,1.41,1.41}; 8 channels assumed 7.1 =
    /// {1,1,1,0,1.41,1.41,1.41,1.41}; otherwise all 1.0.</param>
    public LoudnessMeter(int sampleRate, int channels, float[]? channelWeights = null)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

        _sampleRate = sampleRate;
        _channels = channels;
        _channelWeights = channelWeights ?? DefaultWeights(channels);

        double k1 = Math.Tan(Math.PI * 1681.974450955533 / sampleRate);
        double k2 = Math.Tan(Math.PI * 38.13547087602444 / sampleRate);

        _kStage1 = new BiquadState[channels];
        _kStage2 = new BiquadState[channels];
        var s1 = KWeightingStage1(sampleRate, k1);
        var s2 = KWeightingStage2(sampleRate, k2);
        for (int c = 0; c < channels; c++)
        {
            _kStage1[c] = new BiquadState(s1.b0, s1.b1, s1.b2, s1.a1, s1.a2);
            _kStage2[c] = new BiquadState(s2.b0, s2.b1, s2.b2, s2.a1, s2.a2);
        }

        _ringSize = (int)Math.Ceiling(ShortTermSeconds * sampleRate) + 1;
        _rings = new float[channels][];
        _ringPos = new int[channels];
        for (int c = 0; c < channels; c++)
            _rings[c] = new float[_ringSize];

        _tpChannels = new TruePeakChannel[channels];
        for (int c = 0; c < channels; c++)
            _tpChannels[c] = new TruePeakChannel(sampleRate);

        _nextHopSample = (long)(HopSeconds * sampleRate);
    }

    private static float[] DefaultWeights(int channels) => channels switch
    {
        1 => new[] { 1f },
        2 => new[] { 1f, 1f },
        6 => new[] { 1f, 1f, 1f, 0f, 1.41f, 1.41f },   // 5.1: L R C LFE Ls Rs
        8 => new[] { 1f, 1f, 1f, 0f, 1.41f, 1.41f, 1.41f, 1.41f }, // 7.1
        _ => Enumerable.Repeat(1f, channels).ToArray()
    };

    // BS.1770 K-weighting stage 1: high-shelf (+4 dB @ ~1682 Hz)
    private static (double b0, double b1, double b2, double a1, double a2) KWeightingStage1(int fs, double k)
    {
        const double G = 3.999843853973347;
        const double Q = 0.7071752369554196;
        double vh = Math.Pow(10.0, G / 20.0);
        double vb = Math.Pow(vh, 0.4996667741545416);
        double a0 = 1.0 + k / Q + k * k;
        return (
            (vh + vb * k / Q + k * k) / a0,
            2.0 * (k * k - vh) / a0,
            (vh - vb * k / Q + k * k) / a0,
            2.0 * (k * k - 1.0) / a0,
            (1.0 - k / Q + k * k) / a0);
    }

    // BS.1770 K-weighting stage 2: RLB high-pass (~38 Hz)
    private static (double b0, double b1, double b2, double a1, double a2) KWeightingStage2(int fs, double k)
    {
        const double Q = 0.5003270373238773;
        double a0 = 1.0 + k / Q + k * k;
        return (
            1.0 / a0,
            -2.0 / a0,
            1.0 / a0,
            2.0 * (k * k - 1.0) / a0,
            (1.0 - k / Q + k * k) / a0);
    }

    /// <summary>
    /// Processes a block of interleaved float PCM samples.
    /// </summary>
    public void Process(ReadOnlySpan<float> interleaved)
    {
        int frames = interleaved.Length / _channels;
        for (int f = 0; f < frames; f++)
        {
            int idx = f * _channels;
            for (int c = 0; c < _channels; c++)
            {
                float x = interleaved[idx + c];
                float ax = Math.Abs(x);
                if (ax > _samplePeak) _samplePeak = ax;
                _tpChannels[c].Process(x);

                // K-weighting: stage 1 (shelf) then stage 2 (RLB high-pass)
                float y = _kStage1[c].Process(x);
                y = _kStage2[c].Process(y);

                int pos = _ringPos[c];
                _rings[c][pos] = y;
                _ringPos[c] = (pos + 1) % _ringSize;
            }
            _samplesProcessed++;
            if (_samplesProcessed >= _nextHopSample)
            {
                EvaluateWindows();
                _nextHopSample += (long)(HopSeconds * _sampleRate);
            }
        }
    }

    private void EvaluateWindows()
    {
        long totalSamples = _samplesProcessed;
        int blockSamples = (int)(BlockSeconds * _sampleRate);
        int stSamples = (int)(ShortTermSeconds * _sampleRate);

        if (totalSamples >= blockSamples)
        {
            double z = WeightedWindowEnergy(blockSamples);
            _blockEnergies.Add(z);
            _blockLoudness.Add(z > 0 ? -0.691 + 10.0 * Math.Log10(z) : double.NegativeInfinity);
        }
        if (totalSamples >= stSamples)
        {
            double z = WeightedWindowEnergy(stSamples);
            _shortTermLoudness.Add(z > 0 ? -0.691 + 10.0 * Math.Log10(z) : double.NegativeInfinity);
        }
    }

    /// <summary>
    /// Mean-square energy over the trailing window, weighted per BS.1770 channel weights.
    /// Absolute frame index -> ring slot is index % ringSize.
    /// </summary>
    private double WeightedWindowEnergy(int windowSamples)
    {
        long totalSamples = _samplesProcessed;
        long start = totalSamples - windowSamples;
        double sum = 0;
        for (int c = 0; c < _channels; c++)
        {
            float w = _channelWeights[c];
            if (w == 0f) continue;
            var ring = _rings[c];
            double e = 0;
            for (int i = 0; i < windowSamples; i++)
            {
                int slot = (int)((start + i) % _ringSize);
                float s = ring[slot];
                e += (double)s * s;
            }
            sum += w * e / windowSamples;
        }
        return sum;
    }

    /// <summary>
    /// Finalizes measurement and returns the report. Safe to call once at end of stream.
    /// </summary>
    public Report Finish()
    {
        // Evaluate any trailing partial windows
        long totalSamples = _samplesProcessed;
        int blockSamples = (int)(BlockSeconds * _sampleRate);
        int stSamples = (int)(ShortTermSeconds * _sampleRate);
        if (totalSamples >= blockSamples && (totalSamples % (long)(HopSeconds * _sampleRate)) != 0)
        {
            double z = WeightedWindowEnergy(blockSamples);
            _blockEnergies.Add(z);
            _blockLoudness.Add(z > 0 ? -0.691 + 10.0 * Math.Log10(z) : double.NegativeInfinity);
        }
        if (totalSamples >= stSamples && (totalSamples % (long)(HopSeconds * _sampleRate)) != 0)
        {
            double z = WeightedWindowEnergy(stSamples);
            _shortTermLoudness.Add(z > 0 ? -0.691 + 10.0 * Math.Log10(z) : double.NegativeInfinity);
        }

        // Absolute gate: loudness >= -70 LUFS
        var absGated = new List<int>();
        for (int i = 0; i < _blockLoudness.Count; i++)
            if (_blockLoudness[i] >= -70.0) absGated.Add(i);

        double integrated = double.NaN;
        if (absGated.Count > 0)
        {
            // Relative gate: mean loudness of abs-gated blocks minus 10 LU
            double meanLoudness = 0;
            foreach (var i in absGated) meanLoudness += _blockLoudness[i];
            meanLoudness /= absGated.Count;
            double relThreshold = meanLoudness - 10.0;

            double energySum = 0;
            int count = 0;
            foreach (var i in absGated)
            {
                if (_blockLoudness[i] >= relThreshold)
                {
                    energySum += _blockEnergies[i];
                    count++;
                }
            }
            if (count > 0)
                integrated = -0.691 + 10.0 * Math.Log10(energySum / count);
        }

        double maxSt = double.NaN, lra = double.NaN;
        var stVals = _shortTermLoudness.FindAll(v => !double.IsNegativeInfinity(v));
        if (stVals.Count > 0)
        {
            stVals.Sort();
            maxSt = stVals[^1];
            if (stVals.Count >= 2)
            {
                double p10 = Percentile(stVals, 0.10);
                double p95 = Percentile(stVals, 0.95);
                lra = p95 - p10;
            }
        }

        var channelPeaks = new double[_channels];
        for (int c = 0; c < _channels; c++)
            channelPeaks[c] = _tpChannels[c].TruePeak;

        return new Report
        {
            IntegratedLufs = integrated,
            MaxShortTermLufs = maxSt,
            LraLufs = lra,
            TruePeakDbTp = Db(channelPeaks.Length > 0 ? Max(channelPeaks) : 0),
            SamplePeakDbfs = Db(_samplePeak),
            ChannelTruePeaksDbTp = channelPeaks,
            BlockCount = _blockLoudness.Count
        };
    }

    private static double Percentile(List<double> sorted, double p)
    {
        double idx = p * (sorted.Count - 1);
        int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
    }

    private static double Max(double[] a) { double m = a[0]; for (int i = 1; i < a.Length; i++) if (a[i] > m) m = a[i]; return m; }
    private static double Db(double linear) => linear <= 0 ? double.NegativeInfinity : 20.0 * Math.Log10(linear);

    public void Dispose()
    {
        foreach (var tp in _tpChannels) tp?.Dispose();
    }

    /// <summary>Direct-form biquad state (transposed direct form II).</summary>
    private sealed class BiquadState
    {
        private readonly double _b0, _b1, _b2, _a1, _a2;
        private double _z1, _z2;
        public BiquadState(double b0, double b1, double b2, double a1, double a2)
        { _b0 = b0; _b1 = b1; _b2 = b2; _a1 = a1; _a2 = a2; }
        public float Process(float x)
        {
            double y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return (float)y;
        }
    }

    /// <summary>
    /// 4x oversampling true-peak estimator using a windowed-sinc interpolation filter.
    /// For each input sample, evaluates the 4 output phases between it and the
    /// previous input sample: y(t) = sum x[n] * sinc(t-n) * window, where t is the
    /// fractional position in input-sample units (cutoff = original Nyquist).
    /// </summary>
    private sealed class TruePeakChannel : IDisposable
    {
        private const int PhaseCount = 4;
        private const int TapsPerPhase = 32;   // input samples of history

        private readonly float[] _history;     // last TapsPerPhase+1 input samples
        private int _histPos;
        private int _histCount;
        private long _inputIndex = -1;         // index of newest sample
        private double _peak;

        public double TruePeak => _peak;

        // kernel[p][k]: weight for input tap k (0 = oldest .. TapsPerPhase = newest)
        private readonly float[][] _kernels = new float[PhaseCount][];

        public TruePeakChannel(int sampleRate)
        {
            _history = new float[TapsPerPhase + 1];
            for (int p = 0; p < PhaseCount; p++)
                _kernels[p] = BuildKernel(p);
        }

        private float[] BuildKernel(int phase)
        {
            // Output phase p sits at t = S-1 + p/4 input-sample units, where S is
            // the newest input index. Tap k covers input index n = S - (TapsPerPhase - k)
            // for k = 0..TapsPerPhase, so d = t - n = (TapsPerPhase - 1 + p/4) - k.
            int taps = TapsPerPhase + 1;
            var kernel = new float[taps];
            double frac = phase / (double)PhaseCount;
            for (int k = 0; k < taps; k++)
            {
                double d = (TapsPerPhase - 1 + frac) - k;
                double s = Sinc(d);
                double w = Window(k, taps, TapsPerPhase - 1 + frac);
                kernel[k] = (float)(s * w);
            }
            return kernel;
        }

        // Blackman window centered on the tap nearest the interpolation point.
        private static double Window(int k, int taps, double centerK)
        {
            double x = (k - centerK + taps / 2.0) / taps;
            x = Math.Clamp(x, 0.0, 1.0);
            return 0.42 - 0.5 * Math.Cos(2 * Math.PI * x) + 0.08 * Math.Cos(4 * Math.PI * x);
        }

        private static double Sinc(double x) =>
            Math.Abs(x) < 1e-9 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);

        public void Process(float x)
        {
            _inputIndex++;
            _history[_histPos] = x;
            _histPos = (_histPos + 1) % _history.Length;
            if (_histCount < _history.Length) _histCount++;

            // Need the full tap window before the first interpolation is valid
            if (_histCount < _history.Length) return;

            for (int p = 0; p < PhaseCount; p++)
            {
                double sum = 0;
                var kernel = _kernels[p];
                for (int k = 0; k < kernel.Length; k++)
                {
                    // kernel[0] is oldest tap, kernel[^1] is newest
                    int idx = (_histPos - kernel.Length + k + _history.Length) % _history.Length;
                    sum += _history[idx] * kernel[k];
                }
                double a = Math.Abs(sum);
                if (a > _peak) _peak = a;
            }
        }

        public void Dispose() { }
    }
}
