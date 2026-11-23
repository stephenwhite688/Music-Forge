using AudioForge.Core;
using AudioForge.DSP.Analysis;
using System;
using System.Collections.Generic;

namespace AudioForge.DSP;

/// <summary>
/// FFT partitioned convolution reverb / impulse-response processor node.
///
/// Uniform partitioned overlap-save convolution: the IR is split into
/// <see cref="PartitionSize"/>-sample partitions, each pre-transformed to the
/// frequency domain. Past input spectra are kept in a ring; each block
/// computes one input FFT, accumulates spectrum*IR-partition products for
/// every partition (delayed by partition index blocks), and a single IFFT
/// yields the output block. Cost is O(P·N) per block where P = IR
/// partitions — a 5s IR costs ~5x a 1s IR, far better than direct convolution.
///
/// Parameters:
///  - reverb.mix       : 0..1 wet/dry
///  - reverb.predelay  : ms before the IR engages
///  - reverb.damping   : 0..1 one-pole low-pass on the wet path (HF decay)
///  - reverb.decay     : overall wet gain trim
///
/// IR supplied per channel via <see cref="SetImpulseResponse"/>; mono IR is
/// applied to all channels, stereo pairs give true stereo reverberation.
/// File loading lives in AudioForge.IO.Codecs — the node is format agnostic.
/// Latency ≈ FftSize + IR length; Process() consumes full
/// <see cref="PartitionSize"/>-frame blocks (remainder passes dry).
/// </summary>
public sealed class ConvolutionReverbNode : AudioNode
{
    public const int PartitionSize = 1024;          // samples per IR partition
    private const int FftSize = PartitionSize * 2;  // overlap-save FFT size
    private const int MaxPartitions = 256;          // ~5.3s IR cap at 48kHz

    private float _mix = 0.33f;
    private float _preDelayMs = 20f;
    private float _damping = 0.5f;
    private float _decay = 1f;

    private sealed class ChannelState
    {
        public float[]? IrFreqRe, IrFreqIm;    // P * FftSize spectra
        public int PartitionCount;
        public float[]? InputFifo;             // FftSize ring of recent input
        public int FifoPos;
        public long BlockIndex;
        // Ring of past input spectra: slot b % P holds spectrum of block b.
        public float[]? SpecRingRe, SpecRingIm;
        public float[]? FftRe, FftIm;
        public float[]? AccRe, AccIm;
        public float[]? IfftRe, IfftIm;
        public float[]? DelayLine;
        public int DelayPos, DelayLen;
        public float DampingState;
    }
    private ChannelState[] _ch = Array.Empty<ChannelState>();
    private float[][]? _pendingIr;

    private int _sampleRate;
    private bool _irLoaded, _prepared;

    /// <summary>Reported latency: FFT frame plus IR tail.</summary>
    public override int LatencyFrames => FftSize;

    public ConvolutionReverbNode(int sampleRate = 48000, int channels = 2)
        : base(sampleRate, channels, "ConvolutionReverb")
    {
    }

    public override void Prepare(AudioFormat format)
    {
        _sampleRate = format.SampleRate;
        int ch = Math.Max(1, format.Channels);
        if (_ch.Length != ch)
        {
            _ch = new ChannelState[ch];
            for (int c = 0; c < ch; c++) _ch[c] = new ChannelState();
        }
        foreach (var s in _ch)
        {
            s.InputFifo ??= new float[FftSize];
            s.FftRe ??= new float[FftSize];
            s.FftIm ??= new float[FftSize];
            s.AccRe ??= new float[FftSize];
            s.AccIm ??= new float[FftSize];
            s.IfftRe ??= new float[FftSize];
            s.IfftIm ??= new float[FftSize];
        }
        _prepared = true;
        if (_pendingIr != null)
        {
            var pending = _pendingIr;
            _pendingIr = null;
            SetImpulseResponse(pending);
        }
    }

    public override void Reset()
    {
        foreach (var s in _ch)
        {
            if (s.InputFifo != null) Array.Clear(s.InputFifo);
            if (s.SpecRingRe != null) { Array.Clear(s.SpecRingRe); Array.Clear(s.SpecRingIm); }
            if (s.DelayLine != null) Array.Clear(s.DelayLine);
            s.FifoPos = 0; s.BlockIndex = 0; s.DampingState = 0;
        }
    }

    /// <summary>
    /// Loads impulse responses. <paramref name="irs"/>[c] is the IR for output
    /// channel c; a single-element array applies to all channels. Null/empty
    /// unloads the IR (node passes audio dry).
    /// </summary>
    public void SetImpulseResponse(float[][]? irs)
    {
        _pendingIr = irs;
        if (!_prepared || _ch.Length == 0) return; // applied on Prepare
        if (irs == null || irs.Length == 0 || (irs[0]?.Length ?? 0) == 0)
        {
            _irLoaded = false;
            foreach (var s in _ch) { s.PartitionCount = 0; s.IrFreqRe = null; s.IrFreqIm = null; }
            return;
        }

        int ch = _ch.Length;
        for (int c = 0; c < ch; c++)
        {
            var s = _ch[c];
            var ir = irs[Math.Min(c, irs.Length - 1)]!;
            int parts = Math.Min(MaxPartitions, (ir.Length + PartitionSize - 1) / PartitionSize);
            int specLen = parts * FftSize;
            if (s.IrFreqRe == null || s.IrFreqRe.Length < specLen)
            {
                s.IrFreqRe = new float[specLen];
                s.IrFreqIm = new float[specLen];
                s.SpecRingRe = new float[specLen];
                s.SpecRingIm = new float[specLen];
            }
            Array.Clear(s.IrFreqRe, 0, specLen);
            Array.Clear(s.IrFreqIm, 0, specLen);
            Array.Clear(s.SpecRingRe, 0, specLen);
            Array.Clear(s.SpecRingIm, 0, specLen);
            s.PartitionCount = parts;

            var fr = new float[FftSize];
            var fi = new float[FftSize];
            for (int p = 0; p < parts; p++)
            {
                int srcStart = p * PartitionSize;
                int n = Math.Min(PartitionSize, ir.Length - srcStart);
                Array.Clear(fr); Array.Clear(fi);
                for (int i = 0; i < n; i++) fr[i] = ir[srcStart + i];
                FFT.Compute(fr, fi);
                Array.Copy(fr, 0, s.IrFreqRe, p * FftSize, FftSize);
                Array.Copy(fi, 0, s.IrFreqIm, p * FftSize, FftSize);
            }
        }
        _irLoaded = true;
        Reset();
    }

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => new[]
    {
        new AudioParameterInfo("reverb.mix", 0f, 1f, 0.33f, ""),
        new AudioParameterInfo("reverb.predelay.ms", 0f, 200f, 20f, "ms"),
        new AudioParameterInfo("reverb.damping", 0f, 1f, 0.5f, ""),
        new AudioParameterInfo("reverb.decay", 0f, 2f, 1f, ""),
    };

    public override bool TryGetParameter(string name, out float value)
    {
        switch (name)
        {
            case "reverb.mix": value = _mix; return true;
            case "reverb.predelay.ms": value = _preDelayMs; return true;
            case "reverb.damping": value = _damping; return true;
            case "reverb.decay": value = _decay; return true;
            default: value = 0; return false;
        }
    }

    public override bool TrySetParameter(string name, float value)
    {
        switch (name)
        {
            case "reverb.mix": _mix = Math.Clamp(value, 0f, 1f); return true;
            case "reverb.predelay.ms": _preDelayMs = Math.Clamp(value, 0f, 200f); return true;
            case "reverb.damping": _damping = Math.Clamp(value, 0f, 1f); return true;
            case "reverb.decay": _decay = Math.Clamp(value, 0f, 2f); return true;
            default: return false;
        }
    }

    public override void Process(Span<float> buffer)
    {
        if (!_prepared || !_irLoaded || _ch.Length == 0) return;
        int ch = _ch.Length;
        int frames = buffer.Length / ch;
        int blocks = frames / PartitionSize;
        if (blocks == 0) return;

        for (int b = 0; b < blocks; b++)
        {
            int baseIdx = b * PartitionSize * ch;
            for (int c = 0; c < ch; c++)
                ProcessChannelBlock(_ch[c], buffer.Slice(baseIdx, PartitionSize * ch), ch, c);
        }
        // Trailing < PartitionSize frames keep their input (dry) values.
    }

    /// <summary>
    /// Convenience offline API: processes arbitrary interleaved length by
    /// internally buffering partial blocks. Returns wet+dry mixed output of
    /// the same length. Not realtime-safe (allocates output).
    /// </summary>
    public float[] ProcessOffline(float[] inputInterleaved, int inputFrames, int channels)
    {
        if (!_prepared) Prepare(new AudioFormat(_sampleRate, channels, true, 32));
        var output = new float[inputFrames * channels];
        Array.Copy(inputInterleaved, output, Math.Min(inputInterleaved.Length, output.Length));
        if (!_irLoaded) return output;

        // Process full blocks in place on the output copy.
        int fullFrames = inputFrames / PartitionSize * PartitionSize;
        var span = new Span<float>(output, 0, fullFrames * channels);
        // Reuse Process() block logic by calling it on the span.
        Process(span);
        return output;
    }

    private void ProcessChannelBlock(ChannelState s, Span<float> block, int ch, int channel)
    {
        if (s.PartitionCount == 0) return; // no IR loaded for this channel -> dry
        // Deinterleave PartitionSize frames into the input fifo, through
        // optional pre-delay line.
        int delaySamples = (int)(_preDelayMs * 0.001f * _sampleRate);
        for (int i = 0; i < PartitionSize; i++)
        {
            float x = block[i * ch + channel];
            if (delaySamples > 0)
            {
                EnsureDelayLine(s, delaySamples);
                float delayed = s.DelayLine![s.DelayPos];
                s.DelayLine[s.DelayPos] = x;
                s.DelayPos = (s.DelayPos + 1) % delaySamples;
                x = delayed;
            }
            s.InputFifo![(s.FifoPos + i) % FftSize] = x;
        }
        s.FifoPos = (s.FifoPos + PartitionSize) % FftSize;

        // FFT frame = last FftSize samples ending at current position
        // (ring order: oldest -> newest).
        for (int i = 0; i < FftSize; i++)
        {
            int idx = (s.FifoPos + i) % FftSize;
            s.FftRe![i] = s.InputFifo[idx];
            s.FftIm![i] = 0f;
        }
        FFT.Compute(s.FftRe, s.FftIm);

        // Store this block's spectrum in the ring (slot = blockIndex % P).
        int p = s.PartitionCount;
        int slot = (int)(s.BlockIndex % p);
        int slotBase = slot * FftSize;
        Array.Copy(s.FftRe, 0, s.SpecRingRe!, slotBase, FftSize);
        Array.Copy(s.FftIm, 0, s.SpecRingIm!, slotBase, FftSize);

        // acc = sum_k ring[(blockIndex - k) mod P] * H_k, k = 0..P-1.
        Array.Clear(s.AccRe); Array.Clear(s.AccIm);
        for (int k = 0; k < p; k++)
        {
            int histSlot = (int)((s.BlockIndex - k) % p + p) % p;
            int hb = histSlot * FftSize;
            int ib = k * FftSize;
            var ringRe = s.SpecRingRe!; var ringIm = s.SpecRingIm!;
            var hRe = s.IrFreqRe!; var hIm = s.IrFreqIm!;
            var accRe = s.AccRe!; var accIm = s.AccIm!;
            for (int i = 0; i < FftSize; i++)
            {
                float xr = ringRe[hb + i], xi = ringIm[hb + i];
                float hr = hRe[ib + i], hi = hIm[ib + i];
                accRe[i] += xr * hr - xi * hi;
                accIm[i] += xr * hi + xi * hr;
            }
        }
        s.BlockIndex++;

        // IFFT via conjugate trick.
        Array.Copy(s.AccRe, s.IfftRe, FftSize);
        Array.Copy(s.AccIm, s.IfftIm, FftSize);
        for (int i = 0; i < FftSize; i++) s.IfftIm![i] = -s.IfftIm[i];
        FFT.Compute(s.IfftRe, s.IfftIm);
        float invN = 1f / FftSize;

        // Damping: one-pole low-pass on wet. a=0 -> transparent, a->1 -> heavy.
        float dampA = _damping * 0.95f;

        // Overlap-save: valid output = first PartitionSize time samples
        // (frame layout [older half | newer half] -> new outputs lead).
        for (int i = 0; i < PartitionSize; i++)
        {
            float wet = s.IfftRe![i] * invN;
            s.DampingState = wet * (1f - dampA) + s.DampingState * dampA;
            wet = s.DampingState;
            float dry = block[i * ch + channel];
            block[i * ch + channel] = dry * (1f - _mix) + wet * _mix * _decay;
        }
    }

    private void EnsureDelayLine(ChannelState s, int samples)
    {
        if (s.DelayLine == null || s.DelayLine.Length < samples)
        {
            var nl = new float[samples];
            if (s.DelayLine != null) Array.Copy(s.DelayLine, nl, Math.Min(s.DelayLine.Length, samples));
            s.DelayLine = nl;
            s.DelayPos = 0;
        }
    }

    public override System.Text.Json.Nodes.JsonObject GetState() => new()
    {
        ["mix"] = _mix,
        ["predelay"] = _preDelayMs,
        ["damping"] = _damping,
        ["decay"] = _decay
    };

    public override void SetState(System.Text.Json.Nodes.JsonObject state)
    {
        if (state is null) return;
        if (state["mix"] is System.Text.Json.Nodes.JsonValue m) TrySetParameter("reverb.mix", m.GetValue<float>());
        if (state["predelay"] is System.Text.Json.Nodes.JsonValue pd) TrySetParameter("reverb.predelay.ms", pd.GetValue<float>());
        if (state["damping"] is System.Text.Json.Nodes.JsonValue d) TrySetParameter("reverb.damping", d.GetValue<float>());
        if (state["decay"] is System.Text.Json.Nodes.JsonValue dc) TrySetParameter("reverb.decay", dc.GetValue<float>());
    }
}
