using AudioForge.Core;
using System;

namespace AudioForge.DSP;

/// <summary>
/// Transparent <see cref="IAudioNode"/> that measures peak and RMS levels of
/// the interleaved buffer passing through it. Ballistics: instant block peak,
/// ~1.5 s peak hold decay, ~300 ms RMS integration. Values are read by the UI
/// on a timer - the node itself allocates nothing per block.
/// </summary>
public sealed class LevelMeterNode : AudioNode
{
    private readonly object _gate = new();
    private float _peakHoldDb = -120f;
    private float _rmsDb = -120f;
    private float _blockPeak;

    // One-pole coefficients approximating the ballistics at ~10 ms blocks
    private const float RmsAttack = 0.5f;
    private const float RmsRelease = 0.08f;
    private const float PeakDecayPerBlock = 0.995f; // ~1.5 s hold at 100 blocks/s

    public LevelMeterNode(int sampleRate, int channels, string name = "LevelMeter")
        : base(sampleRate, channels, name) { }

    /// <summary>Instantaneous block peak in dBFS (-120 floor).</summary>
    public float PeakDb { get { lock (_gate) return _blockPeak; } }

    /// <summary>Peak-hold level in dBFS (decays slowly after transients).</summary>
    public float HeldPeakDb { get { lock (_gate) return _peakHoldDb; } }

    /// <summary>Smoothed RMS level in dBFS.</summary>
    public float RmsDb { get { lock (_gate) return _rmsDb; } }

    /// <summary>True when the last block hit or exceeded 0 dBFS.</summary>
    public bool Clip { get { lock (_gate) return _blockPeak >= 0f; } }

    public override void Process(Span<float> buffer)
    {
        int channels = Math.Max(1, Format?.Channels ?? Channels);
        int frames = buffer.Length / channels;
        float peak = 0f;
        double sumSq = 0.0;

        for (int i = 0; i < buffer.Length; i++)
        {
            float a = MathF.Abs(buffer[i]);
            if (a > peak) peak = a;
            sumSq += (double)buffer[i] * buffer[i];
        }

        float blockDb = peak > 1e-6f ? 20f * MathF.Log10(peak) : -120f;
        float blockRms = frames > 0 && sumSq > 0
            ? 20f * (float)Math.Log10(Math.Sqrt(sumSq / buffer.Length) + 1e-6)
            : -120f;

        lock (_gate)
        {
            _blockPeak = blockDb;
            _peakHoldDb = blockDb > _peakHoldDb ? blockDb : Math.Max(_peakHoldDb * PeakDecayPerBlock, -120f);

            float a = blockRms > _rmsDb ? RmsAttack : RmsRelease;
            _rmsDb = _rmsDb + (_rmsDb < -119f ? (blockRms - _rmsDb) : a * (blockRms - _rmsDb));
            if (_rmsDb < -120f) _rmsDb = -120f;
        }
    }

    public override void Reset()
    {
        lock (_gate)
        {
            _blockPeak = -120f;
            _peakHoldDb = -120f;
            _rmsDb = -120f;
        }
    }
}
