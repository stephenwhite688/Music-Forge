using AudioForge.Core;
using System;

namespace AudioForge.DSP;

/// <summary>
/// WSOLA (Waveform-Similarity Overlap-Add) time-stretch / pitch-shift node.
///
/// Two-stage pipeline per channel:
///   1. Varispeed resample: raw input is linearly interpolated into a
///      resampled stream at ratio R (pitch.factor). Output has inLen/R
///      samples at pitch xR.
///   2. WSOLA stretch: analysis frames are read from the resampled stream,
///      aligned by normalized cross-correlation against the previous output
///      tail (preserves transients), windowed and overlap-added with
///      normalization so amplitude stays flat at any hop.
///
///   stretch T on the resampled stream = time.stretch * pitch.factor, giving
///   final duration = inLen * time.stretch and pitch = inPitch * pitch.factor.
///
/// Because output length differs from input length, in-place Process() emits
/// only what is currently synthesizable (underrun pads silence). Offline/DAW
/// paths must use <see cref="ProcessBlock"/> + <see cref="Flush"/>.
///
/// Quality: broadcast-grade WSOLA — good on speech and most music, mild
/// phasiness possible on dense material at extreme settings. Not a
/// SoundTouch-class phase vocoder. Pitch uses linear interpolation (aliasing
/// above ~6 semitones up is possible on bright material).
/// </summary>
public sealed class TimeStretchNode : AudioNode
{
    private const int FrameSize = 1024;    // analysis/synthesis window
    private const int SearchRange = 384;   // WSOLA alignment search (+/-)
    private const int CorrelationLen = 256;

    private float _stretch = 1f;           // time.stretch: output/input duration
    private float _pitchFactor = 1f;       // linear pitch ratio (2^(st/12))

    private readonly object _gate = new();

    private sealed class ChannelState
    {
        public float[] Input = Array.Empty<float>();     // raw input
        public int InputCount;
        public double ResPhase;                          // next raw-input read pos
        public float[] Resampled = Array.Empty<float>(); // varispeed output
        public int ResCount;
        public float[] Window = Array.Empty<float>();
        public float[] PrevOutTail = new float[CorrelationLen];
        public float[] Frame = new float[FrameSize];
    }
    private ChannelState[] _ch = Array.Empty<ChannelState>();

    private double _phase;          // WSOLA position in resampled-sample units
    private float[] _norm = Array.Empty<float>();
    private float[] _stage = Array.Empty<float>();

    /// <summary>Reported latency: two windows of lookahead.</summary>
    public override int LatencyFrames => FrameSize * 2;

    public TimeStretchNode(int sampleRate = 48000, int channels = 2)
        : base(sampleRate, channels, "TimeStretch")
    {
    }

    public override void Prepare(AudioFormat format)
    {
        int ch = Math.Max(1, format.Channels);
        _ch = new ChannelState[ch];
        for (int c = 0; c < ch; c++)
        {
            _ch[c] = new ChannelState
            {
                Input = new float[FrameSize * 8 + SearchRange * 2],
                Resampled = new float[FrameSize * 8 + SearchRange * 2],
                Window = BuildWindow(FrameSize),
                Frame = new float[FrameSize]
            };
        }
        _prepared = true;
        ResetState();
    }

    private bool _prepared;

    public override void Reset() => ResetState();

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => new[]
    {
        new AudioParameterInfo("time.stretch", 0.25f, 4f, 1f, "x"),
        new AudioParameterInfo("pitch.factor", 0.25f, 4f, 1f, "x"),
        new AudioParameterInfo("pitch.semitones", -12f, 12f, 0f, "st"),
    };

    public override bool TryGetParameter(string name, out float value)
    {
        switch (name)
        {
            case "time.stretch": value = _stretch; return true;
            case "pitch.factor": value = _pitchFactor; return true;
            case "pitch.semitones": value = (float)(Math.Log(_pitchFactor, 2.0) * 12.0); return true;
            default: value = 0; return false;
        }
    }

    public override bool TrySetParameter(string name, float value)
    {
        switch (name)
        {
            case "time.stretch": _stretch = Math.Clamp(value, 0.25f, 4f); return true;
            case "pitch.factor": _pitchFactor = Math.Clamp(value, 0.25f, 4f); return true;
            case "pitch.semitones":
                _pitchFactor = Math.Clamp(MathF.Pow(2f, Math.Clamp(value, -12f, 12f) / 12f), 0.25f, 4f);
                return true;
            default: return false;
        }
    }

    /// <summary>
    /// In-place contract: consumes interleaved input and emits whatever output
    /// is currently synthesizable; underrun pads with silence. Cannot be
    /// length-preserving — use <see cref="ProcessBlock"/> for exact output.
    /// </summary>
    public override void Process(Span<float> buffer)
    {
        if (!_prepared || _ch.Length == 0) return;
        int ch = _ch.Length;
        int frames = buffer.Length / ch;
        lock (_gate)
        {
            for (int f = 0; f < frames; f++)
                for (int c = 0; c < ch; c++)
                    Push(_ch[c], buffer[f * ch + c]);

            if (_stage.Length < frames * ch)
                _stage = new float[frames * ch];
            Array.Clear(_stage, 0, frames * ch);
            int producedFrames = SynthesizeInto(_stage, frames);
            int producedSamples = producedFrames * ch;
            _stage.AsSpan(0, producedSamples).CopyTo(buffer);
            if (producedSamples < buffer.Length)
                buffer.Slice(producedSamples).Fill(0);
        }
    }

    /// <summary>
    /// Offline/block API: pushes <paramref name="inputFrames"/> interleaved
    /// frames and returns produced interleaved output (~inputFrames *
    /// time.stretch frames). Call <see cref="Flush"/> for the tail.
    /// </summary>
    public float[] ProcessBlock(float[] inputInterleaved, int inputFrames)
    {
        if (!_prepared) Prepare(new AudioFormat(SampleRate, _ch.Length, true, 32));
        lock (_gate)
        {
            int ch = _ch.Length;
            for (int f = 0; f < inputFrames; f++)
                for (int c = 0; c < ch; c++)
                    Push(_ch[c], inputInterleaved[f * ch + c]);

            int capFrames = (int)(inputFrames * _stretch) + FrameSize * 4;
            var output = new float[Math.Max(16, capFrames) * ch];
            int produced = SynthesizeInto(output, capFrames);
            if (produced == capFrames) return output;
            var trimmed = new float[produced * ch];
            Array.Copy(output, trimmed, trimmed.Length);
            return trimmed;
        }
    }

    /// <summary>Drains remaining synthesizable output after all input pushed.</summary>
    public float[] Flush()
    {
        lock (_gate)
        {
            int ch = _ch.Length;
            int cap = PendingOutputFrames() + FrameSize * 2;
            var output = new float[Math.Max(16, cap) * ch];
            int produced = SynthesizeInto(output, cap, drain: true);
            var trimmed = new float[produced * ch];
            Array.Copy(output, trimmed, trimmed.Length);
            return trimmed;
        }
    }

    /// <summary>Approximate output frames currently producible from queued input.</summary>
    public int PendingOutputFrames()
    {
        lock (_gate)
        {
            double maxRes = 0;
            foreach (var s in _ch) maxRes = Math.Max(maxRes, s.ResCount);
            return (int)(maxRes * _stretch * _pitchFactor);
        }
    }

    // ---------------- internals ----------------

    private void ResetState()
    {
        if (_ch is null) return;
        foreach (var s in _ch)
        {
            Array.Clear(s.Input); s.InputCount = 0; s.ResPhase = 0;
            Array.Clear(s.Resampled); s.ResCount = 0;
            Array.Clear(s.PrevOutTail); Array.Clear(s.Frame);
        }
        _phase = 0;
    }

    private void Push(ChannelState s, float x)
    {
        if (s.InputCount >= s.Input.Length)
            Array.Resize(ref s.Input, s.Input.Length * 2);
        s.Input[s.InputCount++] = x;
    }

    /// <summary>
    /// Emits resampled samples into s.Resampled until it holds at least
    /// <paramref name="count"/> samples or raw input is exhausted.
    /// Linear interpolation; consumes _pitchFactor raw samples per output.
    /// </summary>
    private void EnsureResampled(ChannelState s, int count)
    {
        float r = _pitchFactor;
        while (s.ResCount < count)
        {
            if (s.Resampled.Length <= s.ResCount)
                Array.Resize(ref s.Resampled, s.Resampled.Length * 2);
            double pos = s.ResPhase;
            int i0 = (int)pos;
            if (i0 + 1 >= s.InputCount) break; // need i0 and i0+1
            float frac = (float)(pos - i0);
            s.Resampled[s.ResCount++] = s.Input[i0] * (1f - frac) + s.Input[i0 + 1] * frac;
            s.ResPhase += r;
        }
    }

    /// <summary>
    /// Synthesizes up to <paramref name="maxOutFrames"/> frames into
    /// <paramref name="dest"/> (interleaved). Returns frames produced.
    /// </summary>
    private int SynthesizeInto(float[] dest, int maxOutFrames, bool drain = false)
    {
        int ch = _ch.Length;
        if (ch == 0 || !_prepared) return 0;

        // WSOLA operates on the resampled stream: stretch T = stretch*R gives
        // duration = inLen*stretch at pitch xR. inHop consumed per frame,
        // outHop = inHop*T emitted per frame.
        float inHop = FrameSize / 2f;
        float stretchT = _stretch * _pitchFactor;
        float outHop = inHop * stretchT;
        if (outHop < 1f) outHop = 1f;

        if (_norm.Length < dest.Length)
            _norm = new float[dest.Length];
        else
            Array.Clear(_norm, 0, dest.Length);

        double pos = 0;
        while (pos + FrameSize <= maxOutFrames)
        {
            double nominalPos = _phase;
            // Top up the resampled stream as the phase advances.
            int want = (int)(nominalPos + SearchRange + FrameSize + inHop * 4) + 64;
            foreach (var s in _ch) EnsureResampled(s, want);
            double need = nominalPos + SearchRange + FrameSize;
            bool allHave = true;
            foreach (var s in _ch)
                if (need > s.ResCount) { allHave = false; break; }
            if (!allHave && !drain) break;

            int outPos = (int)pos;
            for (int c = 0; c < ch; c++)
            {
                var s = _ch[c];
                int best = WsolaAlign(s, (int)nominalPos);
                ExtractFrame(s, best, s.Frame);
                OverlapAdd(dest, _norm, outPos, s.Frame, s.Window, ch, c);
            }
            pos += outHop;
            _phase += inHop;

            if (drain && nominalPos > MaxResCount() + FrameSize) break;
        }
        int produced = Math.Min((int)pos + FrameSize / 2, maxOutFrames);

        // Normalize overlap-add.
        for (int i = 0; i < produced * ch && i < dest.Length; i++)
        {
            float w = _norm[i];
            dest[i] = w > 1e-6f ? dest[i] / w : 0f;
        }
        CompactInputs();
        return produced;
    }

    private double MaxResCount()
    {
        double m = 0;
        foreach (var s in _ch) m = Math.Max(m, s.ResCount);
        return m;
    }

    private void CompactInputs()
    {
        // Drop consumed resampled prefix (before phase minus margin) and the
        // corresponding raw input (resampled pos * pitchFactor).
        int dropRes = (int)_phase - SearchRange - FrameSize;
        if (dropRes <= 0) return;
        int dropRaw = (int)(dropRes * _pitchFactor);
        foreach (var s in _ch)
        {
            if (dropRes < s.ResCount)
            {
                int rem = s.ResCount - dropRes;
                Array.Copy(s.Resampled, dropRes, s.Resampled, 0, rem);
                s.ResCount = rem;
            }
            else s.ResCount = 0;

            if (dropRaw < s.InputCount)
            {
                int rem = s.InputCount - dropRaw;
                Array.Copy(s.Input, dropRaw, s.Input, 0, rem);
                s.InputCount = rem;
                s.ResPhase -= dropRaw;
            }
            else { s.InputCount = 0; s.ResPhase = 0; }
        }
        _phase -= dropRes;
    }

    /// <summary>
    /// Finds the resampled-stream offset near nominalPos whose FrameSize/2
    /// segment best matches the previous output tail.
    /// </summary>
    private int WsolaAlign(ChannelState s, int nominalPos)
    {
        int lo = Math.Max(0, nominalPos - SearchRange);
        int hi = Math.Min(s.ResCount - FrameSize, nominalPos + SearchRange);
        if (hi <= lo) return Math.Min(nominalPos, Math.Max(0, s.ResCount - FrameSize));

        int best = nominalPos;
        float bestCorr = float.NegativeInfinity;
        for (int off = lo; off <= hi; off += 8)
        {
            float corr = CorrelateTail(s, off);
            if (corr > bestCorr) { bestCorr = corr; best = off; }
        }
        int rlo = Math.Max(lo, best - 8), rhi = Math.Min(hi, best + 8);
        for (int off = rlo; off <= rhi; off++)
        {
            float corr = CorrelateTail(s, off);
            if (corr > bestCorr) { bestCorr = corr; best = off; }
        }
        return best;
    }

    private float CorrelateTail(ChannelState s, int offset)
    {
        float sum = 0, a2 = 0, b2 = 0;
        for (int i = 0; i < CorrelationLen; i++)
        {
            float a = s.Resampled[offset + i];
            float b = s.PrevOutTail[i];
            sum += a * b; a2 += a * a; b2 += b * b;
        }
        float denom = MathF.Sqrt(a2 * b2);
        return denom < 1e-9f ? 0f : sum / denom;
    }

    private static void ExtractFrame(ChannelState s, int offset, float[] frame)
    {
        for (int i = 0; i < FrameSize; i++)
        {
            int idx = offset + i;
            frame[i] = (idx >= 0 && idx < s.ResCount) ? s.Resampled[idx] : 0f;
        }
    }

    private void OverlapAdd(float[] dest, float[] norm, int outPos, float[] frame, float[] window, int ch, int channel)
    {
        int maxFrame = dest.Length / ch - outPos;
        int n = Math.Min(FrameSize, maxFrame);
        var s = _ch[channel];
        int tailStart = Math.Max(0, FrameSize - CorrelationLen);
        for (int i = 0; i < CorrelationLen; i++)
        {
            int fi = tailStart + i;
            s.PrevOutTail[i] = fi < n ? frame[fi] * window[fi] : 0f;
        }
        for (int i = 0; i < n; i++)
        {
            int idx = (outPos + i) * ch + channel;
            dest[idx] += frame[i] * window[i];
            norm[idx] += window[i];
        }
    }

    private static float[] BuildWindow(int n)
    {
        var w = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = Math.PI * i / (n - 1);
            w[i] = (float)(0.5 - 0.5 * Math.Cos(2 * t)); // Hann
        }
        return w;
    }

    public override System.Text.Json.Nodes.JsonObject GetState() => new()
    {
        ["time.stretch"] = _stretch,
        ["pitch.factor"] = _pitchFactor,
        ["pitch.semitones"] = TryGetParameter("pitch.semitones", out var st) ? st : 0f
    };

    public override void SetState(System.Text.Json.Nodes.JsonObject state)
    {
        if (state is null) return;
        if (state["time.stretch"] is System.Text.Json.Nodes.JsonValue t)
            TrySetParameter("time.stretch", t.GetValue<float>());
        if (state["pitch.factor"] is System.Text.Json.Nodes.JsonValue p)
            TrySetParameter("pitch.factor", p.GetValue<float>());
        if (state["pitch.semitones"] is System.Text.Json.Nodes.JsonValue s)
            TrySetParameter("pitch.semitones", s.GetValue<float>());
    }
}
