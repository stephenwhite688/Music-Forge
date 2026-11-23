using AudioForge.Core;
using AudioForge.DSP.Analysis;
using AudioForge.DSP.GraphicEQ;
using System;

namespace AudioForge.DSP;

/// <summary>
/// Linear-phase graphic EQ implemented as partitioned overlap-save FFT
/// convolution. The magnitude response is sampled from a
/// <see cref="GraphicEqNode"/> spec at FIR bin frequencies and converted to a
/// linear-phase FIR via frequency-sampling + circular shift, giving true
/// zero-phase-shift EQ curves (constant group delay, no phase distortion).
///
/// Trade-off vs the biquad node: introduces <see cref="LatencySamples"/>
/// of latency (~43 ms at 48 kHz with the default 4096-tap kernel) and the
/// bass bands below ~30 Hz are only approximately resolved. Rebuilds its
/// kernels automatically whenever the spec state version changes.
/// </summary>
public sealed class LinearPhaseEqNode : AudioNode
{
    private readonly GraphicEqNode _spec;
    private readonly int _kernelLen;
    private readonly int _blockFrames;
    private readonly int _fftSize;
    private ChannelState[]? _states;
    private long _builtVersion = -1;
    private int _sampleRate;

    /// <summary>Creates a linear-phase EQ driven by <paramref name="spec"/>
    /// for gains/Q/enables/link state.</summary>
    /// <param name="kernelLen">FIR length in taps (latency ~= kernelLen/2 samples... actually kernelLen-1). 2048/4096/8192.</param>
    /// <param name="blockFrames">Max new frames processed per convolution block.</param>
    public LinearPhaseEqNode(GraphicEqNode spec, int kernelLen = 4096, int blockFrames = 2048, string name = "LinearPhaseEQ")
        : base(spec.SampleRate, spec.Channels, name)
    {
        _spec = spec;
        _kernelLen = Math.Max(256, kernelLen);
        _blockFrames = Math.Max(64, blockFrames);
        int needed = _kernelLen - 1 + _blockFrames;
        int f = 1;
        while (f < needed) f <<= 1;
        _fftSize = f;
    }

    /// <summary>Group delay introduced by the FIR (samples).</summary>
    public int LatencySamples => (_kernelLen - 1) / 2;

    /// <summary>FIR group delay in frames - feeds latency reporting/PDC.</summary>
    public override int LatencyFrames => LatencySamples;

    public int KernelLength => _kernelLen;
    public int FftSize => _fftSize;

    private sealed class ChannelState
    {
        public required float[] Hist;      // kernelLen-1 previous input samples
        public required float[] Pending;   // unprocessed input frames
        public int PendingCount;
        public required float[] FrameR;
        public required float[] FrameI;
        public required float[] KerR;
        public required float[] KerI;
        public required float[] OutR;
        public required float[] OutI;
        public int HistCount;
    }

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        _sampleRate = format.SampleRate;
        int channels = Math.Max(1, format.Channels);
        _states = new ChannelState[channels];
        for (int ch = 0; ch < channels; ch++)
        {
            _states[ch] = new ChannelState
            {
                Hist = new float[_kernelLen - 1],
                Pending = new float[_blockFrames + 8],
                FrameR = new float[_fftSize],
                FrameI = new float[_fftSize],
                KerR = new float[_fftSize],
                KerI = new float[_fftSize],
                OutR = new float[_fftSize],
                OutI = new float[_fftSize]
            };
        }
        _builtVersion = -1;
    }

    public override void Reset()
    {
        if (_states is null) return;
        foreach (var s in _states)
        {
            Array.Clear(s.Hist, 0, s.Hist.Length);
            s.HistCount = 0;
            s.PendingCount = 0;
        }
    }

    public override void Process(Span<float> buffer)
    {
        if (buffer.Length == 0 || _states is null || _spec.Bypass || !_spec.Enabled)
            return;

        EnsureKernelsBuilt();

        int channels = _states.Length;
        int frames = buffer.Length / channels;
        if (frames == 0) return;
        float inGain = DbToLinear(_spec.InputGainDb);
        float outGain = DbToLinear(_spec.OutputGainDb);

        // Deinterleave into per-channel pending buffers, then convolve.
        for (int ch = 0; ch < channels; ch++)
        {
            var s = _states[ch];
            EnsurePendingCapacity(s, s.PendingCount + frames);
            for (int i = 0; i < frames; i++)
                s.Pending[s.PendingCount++] = buffer[i * channels + ch] * inGain;
        }

        int maxBlock = _fftSize - _kernelLen + 1; // largest n overlap-save allows
        for (int ch = 0; ch < channels; ch++)
        {
            var s = _states[ch];
            int produced = 0;
            while (s.PendingCount > 0 && produced < frames)
            {
                int n = Math.Min(Math.Min(s.PendingCount, frames - produced), Math.Min(_blockFrames, maxBlock));
                ConvolveBlock(s, n);
                // last n samples of s.OutR are the output for this block -
                // write straight into the interleaved buffer (input already
                // captured in Pending, so overwriting is safe)
                int src = _fftSize - n;
                for (int j = 0; j < n; j++)
                    buffer[(produced + j) * channels + ch] = s.OutR[src + j] * outGain;
                produced += n;
                s.PendingCount -= n;
                if (s.PendingCount > 0)
                    Array.Copy(s.Pending, n, s.Pending, 0, s.PendingCount);
            }
        }
    }

    private void EnsurePendingCapacity(ChannelState s, int needed)
    {
        if (s.Pending.Length >= needed) return;
        int cap = s.Pending.Length;
        while (cap < needed) cap <<= 1;
        Array.Resize(ref s.Pending, cap);
    }

    /// <summary>Overlap-save: frame = [hist(K-1) | new(n) | zeros], last n IFFT
    /// outputs are the linear convolution result.</summary>
    private void ConvolveBlock(ChannelState s, int n)
    {
        int k = _kernelLen;
        int histLen = Math.Min(s.HistCount, k - 1);
        int total = histLen + n;
        Array.Clear(s.FrameR, 0, _fftSize);
        Array.Clear(s.FrameI, 0, _fftSize);

        // Newest `total` samples end at the frame tail:
        // frame[F-total .. F) = hist[0..histLen) ++ pending[0..n)
        int dst = _fftSize - total;
        Array.Copy(s.Hist, 0, s.FrameR, dst, histLen);
        Array.Copy(s.Pending, 0, s.FrameR, dst + histLen, n);

        FFT.Compute(s.FrameR, s.FrameI);
        // multiply by kernel spectrum
        for (int i = 0; i < _fftSize; i++)
        {
            float ar = s.FrameR[i], ai = s.FrameI[i];
            s.FrameR[i] = ar * s.KerR[i] - ai * s.KerI[i];
            s.FrameI[i] = ar * s.KerI[i] + ai * s.KerR[i];
        }
        // inverse FFT via conjugation: ifft(x) = conj(fft(conj(x))) / N
        for (int i = 0; i < _fftSize; i++) s.FrameI[i] = -s.FrameI[i];
        FFT.Compute(s.FrameR, s.FrameI);
        float inv = 1f / _fftSize;
        for (int i = 0; i < _fftSize; i++)
        {
            s.OutR[i] = s.FrameR[i] * inv;
            s.OutI[i] = -s.FrameI[i] * inv; // conjugate back -> real output in OutR
        }

        // Update history: last k-1 samples of (hist ++ pending[0..n)),
        // kept left-aligned in Hist[0..HistCount).
        int keepFromHist = Math.Min(s.HistCount, Math.Max(0, (k - 1) - n));
        if (keepFromHist > 0)
            Array.Copy(s.Hist, s.HistCount - keepFromHist, s.Hist, 0, keepFromHist);
        int histWrite = keepFromHist;
        int fromPending = Math.Min(n, k - 1 - histWrite);
        if (fromPending > 0)
            Array.Copy(s.Pending, n - fromPending, s.Hist, histWrite, fromPending);
        s.HistCount = Math.Min(k - 1, s.HistCount + n);
    }

    /// <summary>Rebuilds all channel kernels when the spec state changed.</summary>
    private void EnsureKernelsBuilt()
    {
        long v = _spec.StateVersion;
        if (v == _builtVersion && _states is not null) return;
        _builtVersion = v;
        if (_states is null) return;

        int sr = _sampleRate > 0 ? _sampleRate : _spec.SampleRate;
        int k = _kernelLen;
        int half = k / 2;

        for (int ch = 0; ch < _states.Length; ch++)
        {
            var s = _states[ch];
            // 1) Desired magnitude at K bins -> zero-phase spectrum S[k]
            var specR = new float[k];
            var specI = new float[k];
            for (int b = 0; b <= half; b++)
            {
                float freq = b * (float)sr / k;
                double db = _spec.GetMagnitudeDbAt(freq, ch);
                float mag = (float)Math.Pow(10.0, db / 20.0);
                specR[b] = mag;
                specI[b] = 0f;
                if (b > 0 && b < half)
                {
                    specR[k - b] = mag;
                    specI[k - b] = 0f;
                }
            }
            // 2) K-point inverse FFT -> zero-phase impulse centred at n=0
            for (int i = 0; i < k; i++) specI[i] = -specI[i];
            FFT.Compute(specR, specI);
            float kinv = 1f / k;
            for (int i = 0; i < k; i++)
            {
                specR[i] *= kinv;
                specI[i] = -specI[i] * kinv;
            }
            // 3) Circular shift by k/2 -> causal linear-phase kernel (delay ~k/2)
            var kernel = new float[k];
            for (int i = 0; i < k; i++)
                kernel[i] = specR[(i + half) % k];
            // 4) Zero-pad to F and forward FFT -> kernel spectrum
            Array.Clear(s.KerR, 0, _fftSize);
            Array.Clear(s.KerI, 0, _fftSize);
            Array.Copy(kernel, s.KerR, k);
            FFT.Compute(s.KerR, s.KerI);
        }
    }

    private static float DbToLinear(float db) =>
        db == 0f ? 1f : (float)Math.Pow(10.0, db / 20.0);
}
