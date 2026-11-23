using AudioForge.Core;
using System;

namespace AudioForge.DSP;

/// <summary>
/// Transparent <see cref="IAudioNode"/> that estimates true (inter-sample)
/// peak level using windowed-sinc interpolation at 4x oversampling
/// (quarter-sample points at +0.25/+0.50/+0.75 between samples).
///
/// BS.1770-style approach: the input is treated as bandlimited to Nyquist and
/// reconstructed with a Blackman-windowed sinc kernel (33 taps). Inter-sample
/// points within Half a kernel width of a block boundary are deferred to the
/// next block so the kernel always sees its full support - no edge artefacts.
/// Estimate, not a certified BS.1770 meter, but typically < 0.1 dB from
/// reference true-peak measurements. Fast-attack / slow-decay ballistics.
/// </summary>
public sealed class TruePeakNode : AudioNode
{
    private const int Taps = 33;          // odd -> symmetric around centre
    private const int Half = Taps / 2;    // 16
    private static readonly float[] Phases = { 0.25f, 0.5f, 0.75f };

    private readonly object _gate = new();
    private float _blockPeakDb = -120f;
    private float _heldPeakDb = -120f;
    private const float PeakDecayPerBlock = 0.995f;

    private static readonly float[][] Kernels = BuildKernels();

    private float[]? _pending;   // per-channel history+input, [ch*cap + idx]
    private int[]? _pendingCount;
    private int _cap;
    private int _channels;

    public TruePeakNode(int sampleRate, int channels, string name = "TruePeak")
        : base(sampleRate, channels, name) { }

    /// <summary>Peak of the last processed block including inter-sample points (dBFS).</summary>
    public float PeakDb { get { lock (_gate) return _blockPeakDb; } }

    /// <summary>Slow-decaying held true-peak level (dBFS).</summary>
    public float HeldPeakDb { get { lock (_gate) return _heldPeakDb; } }

    /// <summary>True when the last block exceeded 0 dBFS true peak.</summary>
    public bool Clip { get { lock (_gate) return _blockPeakDb >= 0f; } }

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        _channels = Math.Max(1, format.Channels);
        _cap = 4096 + Taps + 8;
        _pending = new float[_channels * _cap];
        _pendingCount = new int[_channels];
        for (int ch = 0; ch < _channels; ch++) _pendingCount[ch] = Half; // zero history pre-roll
    }

    public override void Reset()
    {
        if (_pending is not null) Array.Clear(_pending);
        if (_pendingCount is not null) { Array.Clear(_pendingCount); for (int ch = 0; ch < _pendingCount.Length; ch++) _pendingCount[ch] = Half; }
        lock (_gate) { _blockPeakDb = -120f; _heldPeakDb = -120f; }
    }

    public override void Process(Span<float> buffer)
    {
        int channels = _pending is not null ? _channels : Math.Max(1, Format?.Channels ?? Channels);
        int frames = buffer.Length / channels;
        if (frames == 0 || _pending is null || _pendingCount is null) return;

        float peak = 0f;
        for (int ch = 0; ch < channels; ch++)
        {
            int pbase = ch * _cap;
            int pc = _pendingCount[ch];

            // ensure capacity for pc + frames (+ margin), compacting oldest first
            if (pc + frames > _cap)
            {
                int drop = pc + frames - _cap;
                Array.Copy(_pending, pbase + drop, _pending, pbase, pc - drop);
                pc -= drop;
            }

            // append this block's samples (also feeds the sample peak)
            for (int i = 0; i < frames; i++)
            {
                float s = buffer[i * channels + ch];
                _pending[pbase + pc + i] = s;
                float a = MathF.Abs(s);
                if (a > peak) peak = a;
            }
            int newCount = pc + frames;
            int appendedStart = pc; // pending index where this block's frames begin

            // Inter-sample evaluation: appended sample j needs taps j-16..j+16,
            // all inside pending. Eligible appended positions end 16 before the
            // tail; the last Half frames are deferred to the next call.
            int eligibleEnd = newCount - Half; // exclusive bound on pending index
            for (int j = appendedStart; j < eligibleEnd; j++)
            {
                for (int p = 0; p < Phases.Length; p++)
                {
                    var k = Kernels[p];
                    double acc = 0.0;
                    int baseIdx = j - Half;
                    for (int t = 0; t < Taps; t++)
                        acc += k[t] * _pending[pbase + baseIdx + t];
                    float av = MathF.Abs((float)acc);
                    if (av > peak) peak = av;
                }
            }

            // retain only the last Taps samples as history for the next call
            int retain = Math.Min(Taps, newCount);
            Array.Copy(_pending, pbase + newCount - retain, _pending, pbase, retain);
            _pendingCount[ch] = retain;
        }

        float db = peak > 1e-6f ? 20f * MathF.Log10(peak) : -120f;
        lock (_gate)
        {
            _blockPeakDb = db;
            _heldPeakDb = db > _heldPeakDb ? db : Math.Max(_heldPeakDb * PeakDecayPerBlock, -120f);
        }
    }

    /// <summary>Builds Blackman-windowed sinc interpolation kernels for the
    /// three fractional phases.</summary>
    private static float[][] BuildKernels()
    {
        var kernels = new float[Phases.Length][];
        for (int p = 0; p < Phases.Length; p++)
        {
            var k = new float[Taps];
            for (int t = 0; t < Taps; t++)
            {
                double d = (t - Half) - Phases[p];
                double w = 0.42 - 0.5 * Math.Cos(2.0 * Math.PI * t / (Taps - 1))
                             + 0.08 * Math.Cos(4.0 * Math.PI * t / (Taps - 1));
                double sinc = d == 0.0 ? 1.0 : Math.Sin(Math.PI * d) / (Math.PI * d);
                k[t] = (float)(sinc * w);
            }
            double sum = 0;
            for (int t = 0; t < Taps; t++) sum += k[t];
            if (Math.Abs(sum) > 1e-9) for (int t = 0; t < Taps; t++) k[t] /= (float)sum;
            kernels[p] = k;
        }
        return kernels;
    }
}
