using AudioForge.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.DSP.GraphicEQ;

/// <summary>
/// Professional 31-band ISO third-octave graphic equaliser as an
/// <see cref="IAudioNode"/> for the realtime playback chain.
///
/// Each band is a constant-Q peaking biquad. Filters are per-channel;
/// gains can be linked across all channels or set independently per
/// channel (stereo unlink / multichannel operation). Band Q is adjustable.
/// Gain changes are applied once per block (no zipper noise); UI threads
/// may call the setters while the feed thread processes.
/// </summary>
public sealed class GraphicEqNode : AudioNode
{
    /// <summary>ISO 31-band third-octave centre frequencies (20 Hz - 20 kHz).</summary>
    public static readonly float[] Iso31Frequencies =
    {
        20f, 25f, 31.5f, 40f, 50f, 63f, 80f, 100f, 125f, 160f,
        200f, 250f, 315f, 400f, 500f, 630f, 800f, 1000f, 1250f, 1600f,
        2000f, 2500f, 3150f, 4000f, 5000f, 6300f, 8000f, 10000f, 12500f, 16000f, 20000f
    };

    /// <summary>Q for a 1/3-octave bandwidth (constant-Q bands).</summary>
    public const float ThirdOctaveQ = 4.317f;

    /// <summary>Maximum absolute band gain in dB (pro extended range).</summary>
    public const float MaxBandGainDb = 15f;

    private readonly object _gainLock = new();
    private AudioForge.DSP.BiquadFilter[,]? _filters; // [channel, band]
    private float[] _pendingGains = new float[Iso31Frequencies.Length];   // linked / channel-0 master
    private float[][]? _channelGains;                                    // [channel][band] when unlinked
    private bool[] _bandEnabled = new bool[Iso31Frequencies.Length];
    private bool _gainsDirty;
    private int _sampleRate;
    private volatile float _inputGainDb;
    private volatile float _outputGainDb;
    private volatile float _bandQ = ThirdOctaveQ;
    private volatile bool _channelLinked = true;
    private volatile bool _bypass;
    private long _stateVersion;

    /// <summary>Monotonic counter bumped on every EQ-state mutation. DSP nodes use it to detect when derived data must be rebuilt.</summary>
    public long StateVersion { get { lock (_gainLock) return _stateVersion; } }
    private volatile bool _enabled = true;

    public GraphicEqNode(int sampleRate, int channels, string name = "GraphicEQ")
        : base(sampleRate, channels, name)
    {
        Array.Fill(_bandEnabled, true);
    }

    public int BandCount => Iso31Frequencies.Length;
    public float[] Frequencies => Iso31Frequencies;

    /// <summary>Pre-EQ input trim in dB.</summary>
    public float InputGainDb
    {
        get => _inputGainDb;
        set => _inputGainDb = Math.Clamp(value, -24f, 24f);
    }

    /// <summary>Post-EQ output trim in dB.</summary>
    public float OutputGainDb
    {
        get => _outputGainDb;
        set => _outputGainDb = Math.Clamp(value, -24f, 24f);
    }

    /// <summary>Bandwidth Q for all bands (0.5 narrow/surgical .. 12 wide/musical).</summary>
    public float BandQ
    {
        get => _bandQ;
        set
        {
            _bandQ = Math.Clamp(value, 0.5f, 12f);
            lock (_gainLock) _gainsDirty = true; _stateVersion++;
        }
    }

    /// <summary>True bypass - audio passes through untouched (A/B compare).</summary>
    public bool Bypass
    {
        get => _bypass;
        set => _bypass = value;
    }

    /// <summary>EQ on/off. When false the node is transparent.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>True when all channels share one gain set (stereo link).</summary>
    public bool ChannelLinked
    {
        get => _channelLinked;
        set
        {
            if (_channelLinked == value) return;
            lock (_gainLock)
            {
                if (value)
                {
                    // Linking: channel 0's curve becomes the shared curve
                    if (_channelGains is { Length: > 0 })
                        Array.Copy(_channelGains[0], _pendingGains, _pendingGains.Length);
                }
                else if (_channelGains is not null)
                {
                    // Unlinking: every channel starts from the shared curve
                    for (int ch = 0; ch < _channelGains.Length; ch++)
                        Array.Copy(_pendingGains, _channelGains[ch], _pendingGains.Length);
                }
                _channelLinked = value;
                _gainsDirty = true; _stateVersion++;
            }
        }
    }

    /// <summary>Number of channels the node is currently prepared for.</summary>
    public int PreparedChannels => _filters?.GetLength(0) ?? Channels;

    /// <summary>Current band gains in dB for the linked curve (or channel 0 when unlinked).</summary>
    public float[] GetBandGains() => GetBandGains(0);

    /// <summary>Band gains for a specific channel (ignored index when linked).</summary>
    public float[] GetBandGains(int channel)
    {
        lock (_gainLock)
        {
            if (_channelLinked || _channelGains is null || channel < 0 || channel >= _channelGains.Length)
                return (float[])_pendingGains.Clone();
            return (float[])_channelGains[channel].Clone();
        }
    }

    /// <summary>Sets one band's gain in dB on the linked curve / all channels.</summary>
    public void SetBandGain(int band, float gainDb)
    {
        if (band < 0 || band >= Iso31Frequencies.Length)
            throw new ArgumentOutOfRangeException(nameof(band));
        lock (_gainLock)
        {
            float g = Math.Clamp(gainDb, -MaxBandGainDb, MaxBandGainDb);
            _pendingGains[band] = g;
            if (_channelGains is not null)
                for (int ch = 0; ch < _channelGains.Length; ch++)
                    _channelGains[ch][band] = g;
            _gainsDirty = true; _stateVersion++;
        }
    }

    /// <summary>Sets one band's gain on a single channel (unlinked mode).</summary>
    public void SetBandGain(int channel, int band, float gainDb)
    {
        if (band < 0 || band >= Iso31Frequencies.Length)
            throw new ArgumentOutOfRangeException(nameof(band));
        lock (_gainLock)
        {
            if (_channelLinked || _channelGains is null || channel < 0 || channel >= _channelGains.Length)
            {
                SetBandGain(band, gainDb);
                return;
            }
            _channelGains[channel][band] = Math.Clamp(gainDb, -MaxBandGainDb, MaxBandGainDb);
            _gainsDirty = true; _stateVersion++;
        }
    }

    /// <summary>Sets all band gains at once on the linked curve / all channels.</summary>
    public void SetAllGains(IReadOnlyList<float> gainsDb)
    {
        if (gainsDb.Count != Iso31Frequencies.Length)
            throw new ArgumentException($"Expected {Iso31Frequencies.Length} gains.", nameof(gainsDb));
        lock (_gainLock)
        {
            for (int i = 0; i < _pendingGains.Length; i++)
                _pendingGains[i] = Math.Clamp(gainsDb[i], -MaxBandGainDb, MaxBandGainDb);
            if (_channelGains is not null)
                for (int ch = 0; ch < _channelGains.Length; ch++)
                    Array.Copy(_pendingGains, _channelGains[ch], _pendingGains.Length);
            _gainsDirty = true; _stateVersion++;
        }
    }

    /// <summary>Per-band on/off. Disabled bands process as 0 dB (their stored
    /// gain is preserved for when they are re-enabled).</summary>
    public bool[] GetBandEnabled()
    {
        lock (_gainLock) return (bool[])_bandEnabled.Clone();
    }

    /// <summary>Enables or disables a single band.</summary>
    public void SetBandEnabled(int band, bool enabled)
    {
        if (band < 0 || band >= Iso31Frequencies.Length)
            throw new ArgumentOutOfRangeException(nameof(band));
        lock (_gainLock)
        {
            if (_bandEnabled[band] == enabled) return;
            _bandEnabled[band] = enabled;
            _gainsDirty = true; _stateVersion++;
        }
    }

    /// <summary>Enables or disables every band at once.</summary>
    public void SetAllBandsEnabled(bool enabled)
    {
        lock (_gainLock)
        {
            Array.Fill(_bandEnabled, enabled);
            _gainsDirty = true; _stateVersion++;
        }
    }

    /// <summary>Returns every band to 0 dB (flat).</summary>
    public void Flat()
    {
        lock (_gainLock)
        {
            Array.Clear(_pendingGains, 0, _pendingGains.Length);
            if (_channelGains is not null)
                for (int ch = 0; ch < _channelGains.Length; ch++)
                    Array.Clear(_channelGains[ch], 0, _channelGains[ch].Length);
            _gainsDirty = true; _stateVersion++;
        }
    }

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        int channels = Math.Max(1, format.Channels);
        int sampleRate = format.SampleRate;
        _sampleRate = sampleRate;
        _filters = new AudioForge.DSP.BiquadFilter[channels, Iso31Frequencies.Length];
        _channelGains = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _channelGains[ch] = new float[Iso31Frequencies.Length];
            Array.Copy(_pendingGains, _channelGains[ch], _pendingGains.Length);
        }
        float q = _bandQ;
        for (int ch = 0; ch < channels; ch++)
            for (int b = 0; b < Iso31Frequencies.Length; b++)
            {
                var f = new AudioForge.DSP.BiquadFilter(sampleRate, AudioForge.DSP.BiquadType.Peak);
                f.SetParams(Iso31Frequencies[b], q, 0f);
                _filters[ch, b] = f;
            }
        // Re-apply current gains - Prepare rebuilds filters at 0 dB
        lock (_gainLock) _gainsDirty = true; _stateVersion++;
        ApplyPendingGains();
    }

    public override void Reset()
    {
        if (_filters is null) return;
        for (int ch = 0; ch < _filters.GetLength(0); ch++)
            for (int b = 0; b < Iso31Frequencies.Length; b++)
                _filters[ch, b].Reset();
    }

    /// <summary>Applies pending gain changes to the filter coefficients (once per block).</summary>
    private void ApplyPendingGains()
    {
        float[][] channelGains;
        float[] masterGains;
        bool[] enabled;
        float q;
        lock (_gainLock)
        {
            if (!_gainsDirty) return;
            masterGains = (float[])_pendingGains.Clone();
            enabled = (bool[])_bandEnabled.Clone();
            q = _bandQ;
            _gainsDirty = false;
            channelGains = _channelGains is null
                ? Array.Empty<float[]>()
                : new float[_channelGains.Length][];
            if (_channelGains is not null)
                for (int ch = 0; ch < _channelGains.Length; ch++)
                    channelGains[ch] = (float[])_channelGains[ch].Clone();
        }
        if (_filters is null) return;
        // Bands too close to Nyquist for a stable 1/3-octave biquad
        // (e.g. 20 kHz at 44.1 kHz) are forced transparent.
        float nyquistLimit = 0.45f * Math.Max(1, _sampleRate);
        bool linked = _channelLinked;
        int channels = _filters.GetLength(0);

        // Precompute the per-band effective gain mask once
        Span<float> mask = stackalloc float[Iso31Frequencies.Length];
        for (int b = 0; b < mask.Length; b++)
            mask[b] = enabled[b] && Iso31Frequencies[b] < nyquistLimit ? 1f : 0f;

        for (int ch = 0; ch < channels; ch++)
        {
            var gains = linked || ch >= channelGains.Length ? masterGains : channelGains[ch];
            for (int b = 0; b < Iso31Frequencies.Length; b++)
                _filters[ch, b].SetParams(Iso31Frequencies[b], q, gains[b] * mask[b]);
        }
    }

    public override void Process(Span<float> buffer)
    {
        if (buffer.Length == 0 || !_enabled || _bypass || _filters is null)
            return;

        ApplyPendingGains();

        int channels = _filters.GetLength(0);
        if (channels < 1) return;
        int frames = buffer.Length / channels;
        float inGain = DbToLinear(_inputGainDb);
        float outGain = DbToLinear(_outputGainDb);

        for (int i = 0; i < frames; i++)
        {
            int offset = i * channels;
            for (int ch = 0; ch < channels; ch++)
            {
                int idx = offset + ch;
                float s = buffer[idx] * inGain;
                for (int b = 0; b < Iso31Frequencies.Length; b++)
                    s = _filters[ch, b].ProcessSample(s);
                buffer[idx] = s * outGain;
            }
        }
    }

    /// <summary>
    /// Computes the EQ's summed magnitude response in dB at log-spaced
    /// frequencies, using the exact RBJ peaking coefficients the filters use.
    /// Shows the linked/channel-0 curve; disabled bands and bands beyond the
    /// Nyquist guard contribute nothing, so the curve always matches the audio.
    /// </summary>
    public float[] GetMagnitudeResponseDb(int points = 256, float minFreq = 20f, float maxFreq = 20000f)
    {
        int sr = Math.Max(1, _sampleRate > 0 ? _sampleRate : SampleRate);
        float nyquistLimit = 0.45f * sr;
        float q = _bandQ;
        var gains = GetBandGains();
        var enabled = GetBandEnabled();

        var bands = new (float f0, float b0, float b1, float b2, float a1, float a2)[Iso31Frequencies.Length];
        int bandCount = 0;
        for (int b = 0; b < Iso31Frequencies.Length; b++)
        {
            float g = enabled[b] && Iso31Frequencies[b] < nyquistLimit ? gains[b] : 0f;
            if (MathF.Abs(g) < 0.01f) continue;
            float f0 = Iso31Frequencies[b];
            float w0 = 2f * MathF.PI * f0 / sr;
            float cosW0 = MathF.Cos(w0), sinW0 = MathF.Sin(w0);
            float alpha = sinW0 / (2f * q);
            float A = MathF.Pow(10f, g / 40f);
            float b0 = 1f + alpha * A, b1 = -2f * cosW0, b2 = 1f - alpha * A;
            float a0 = 1f + alpha / A, a1 = -2f * cosW0, a2 = 1f - alpha / A;
            bands[bandCount++] = (f0, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        var result = new float[points];
        float logMin = MathF.Log10(minFreq), logMax = MathF.Log10(maxFreq);
        for (int p = 0; p < points; p++)
        {
            float f = MathF.Pow(10f, logMin + (logMax - logMin) * p / (points - 1));
            float w = 2f * MathF.PI * f / sr;
            float cw = MathF.Cos(w), sw = MathF.Sin(w);
            float c2w = MathF.Cos(2f * w), s2w = MathF.Sin(2f * w);
            double totalDb = 0.0;
            for (int b = 0; b < bandCount; b++)
            {
                var bd = bands[b];
                double nRe = bd.b0 + bd.b1 * cw + bd.b2 * c2w;
                double nIm = -(bd.b1 * sw + bd.b2 * s2w);
                double dRe = 1.0 + bd.a1 * cw + bd.a2 * c2w;
                double dIm = -(bd.a1 * sw + bd.a2 * s2w);
                double mag = Math.Sqrt(nRe * nRe + nIm * nIm) / Math.Sqrt(dRe * dRe + dIm * dIm);
                totalDb += 20.0 * Math.Log10(Math.Max(mag, 1e-12));
            }
            result[p] = (float)totalDb;
        }
        return result;
    }

    /// <summary>Summed magnitude response in dB at a single frequency for one
    /// channel's gain set - used by the linear-phase kernel designer.</summary>
    public double GetMagnitudeDbAt(float freqHz, int channel)
    {
        int sr = Math.Max(1, _sampleRate > 0 ? _sampleRate : SampleRate);
        float nyquistLimit = 0.45f * sr;
        float q = _bandQ;
        var gains = GetBandGains(channel);
        var enabled = GetBandEnabled();

        float w = 2f * MathF.PI * freqHz / sr;
        float cw = MathF.Cos(w), sw = MathF.Sin(w);
        float c2w = MathF.Cos(2f * w), s2w = MathF.Sin(2f * w);
        double totalDb = 0.0;

        for (int b = 0; b < Iso31Frequencies.Length; b++)
        {
            float g = enabled[b] && Iso31Frequencies[b] < nyquistLimit ? gains[b] : 0f;
            if (MathF.Abs(g) < 0.01f) continue;
            float f0 = Iso31Frequencies[b];
            float w0 = 2f * MathF.PI * f0 / sr;
            float cosW0 = MathF.Cos(w0), sinW0 = MathF.Sin(w0);
            float alpha = sinW0 / (2f * q);
            float A = MathF.Pow(10f, g / 40f);
            float b0 = 1f + alpha * A, b1 = -2f * cosW0, b2 = 1f - alpha * A;
            float a0 = 1f + alpha / A, a1 = -2f * cosW0, a2 = 1f - alpha / A;
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;

            double nRe = b0 + b1 * cw + b2 * c2w;
            double nIm = -(b1 * sw + b2 * s2w);
            double dRe = 1.0 + a1 * cw + a2 * c2w;
            double dIm = -(a1 * sw + a2 * s2w);
            double mag = Math.Sqrt(nRe * nRe + nIm * nIm) / Math.Sqrt(dRe * dRe + dIm * dIm);
            totalDb += 20.0 * Math.Log10(Math.Max(mag, 1e-12));
        }
        return totalDb;
    }
    // ---- Parameter / state model (IAudioNode extensions) ----

    private const string BandParamPrefix = "band.gain[";

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo()
    {
        var list = new List<AudioParameterInfo>(BandCount + 6)
        {
            new("input.db", -24f, 24f, 0f, "dB"),
            new("output.db", -24f, 24f, 0f, "dB"),
            new("bandQ", 0.5f, 12f, ThirdOctaveQ),
            new("bypass", 0f, 1f, 0f),
            new("enabled", 0f, 1f, 1f),
            new("channelLinked", 0f, 1f, 1f),
        };
        for (int b = 0; b < BandCount; b++)
            list.Add(new AudioParameterInfo($"band.gain[{b}]", -MaxBandGainDb, MaxBandGainDb, 0f, "dB"));
        return list;
    }

    /// <summary>Resolves "band.gain[i]" to a band index, or -1 for non-band names.</summary>
    private static int TryParseBandParam(string name)
    {
        if (!name.StartsWith(BandParamPrefix, StringComparison.Ordinal)) return -1;
        int close = name.IndexOf(']', BandParamPrefix.Length);
        if (close < 0) return -1;
        return int.TryParse(name.AsSpan(BandParamPrefix.Length, close - BandParamPrefix.Length), out int b) ? b : -1;
    }

    public override bool TryGetParameter(string name, out float value)
    {
        int band = TryParseBandParam(name);
        if (band >= 0)
        {
            if (band >= BandCount) { value = 0f; return false; }
            value = GetBandGains(0)[band];
            return true;
        }
        value = name switch
        {
            "input.db" => InputGainDb,
            "output.db" => OutputGainDb,
            "bandQ" => BandQ,
            "bypass" => Bypass ? 1f : 0f,
            "enabled" => Enabled ? 1f : 0f,
            "channelLinked" => ChannelLinked ? 1f : 0f,
            _ => float.NaN,
        };
        return !float.IsNaN(value);
    }

    public override bool TrySetParameter(string name, float value)
    {
        int band = TryParseBandParam(name);
        if (band >= 0)
        {
            if (band >= BandCount) return false;
            // Linked mode: the shared curve drives all channels. Unlinked:
            // parameters address channel 0 (per-channel automation can use
            // the (channel,band) setters directly).
            if (ChannelLinked) SetBandGain(band, value);
            else SetBandGain(0, band, value);
            return true;
        }
        switch (name)
        {
            case "input.db": InputGainDb = value; return true;
            case "output.db": OutputGainDb = value; return true;
            case "bandQ": BandQ = value; return true;
            case "bypass": Bypass = value >= 0.5f; return true;
            case "enabled": Enabled = value >= 0.5f; return true;
            case "channelLinked": ChannelLinked = value >= 0.5f; return true;
            default: return false;
        }
    }

    public override JsonObject GetState()
    {
        var state = new JsonObject
        {
            ["inputGainDb"] = InputGainDb,
            ["outputGainDb"] = OutputGainDb,
            ["bandQ"] = BandQ,
            ["bypass"] = Bypass,
            ["enabled"] = Enabled,
            ["channelLinked"] = ChannelLinked,
        };

        var enabledArr = new JsonArray();
        foreach (var e in GetBandEnabled()) enabledArr.Add(e);
        state["bandEnabled"] = enabledArr;

        int channels = PreparedChannels;
        var gainsArr = new JsonArray();
        for (int ch = 0; ch < channels; ch++)
        {
            var chArr = new JsonArray();
            foreach (var g in GetBandGains(ch)) chArr.Add(g);
            gainsArr.Add(chArr);
        }
        state["gains"] = gainsArr;
        return state;
    }

    public override void SetState(JsonObject state)
    {
        if (state is null) return;

        // Restore link mode first so per-channel writes land correctly.
        if (state["channelLinked"] is JsonValue cl) ChannelLinked = cl.GetValue<bool>();
        if (state["inputGainDb"] is JsonValue ig) InputGainDb = ig.GetValue<float>();
        if (state["outputGainDb"] is JsonValue og) OutputGainDb = og.GetValue<float>();
        if (state["bandQ"] is JsonValue q) BandQ = q.GetValue<float>();
        if (state["bypass"] is JsonValue bp) Bypass = bp.GetValue<bool>();
        if (state["enabled"] is JsonValue en) Enabled = en.GetValue<bool>();

        if (state["bandEnabled"] is JsonArray be)
            for (int b = 0; b < Math.Min(be.Count, BandCount); b++)
                SetBandEnabled(b, be[b]!.GetValue<bool>());

        if (state["gains"] is JsonArray gains)
            for (int ch = 0; ch < gains.Count && ch < PreparedChannels; ch++)
            {
                if (gains[ch] is not JsonArray chGains) continue;
                for (int b = 0; b < Math.Min(chGains.Count, BandCount); b++)
                {
                    float g = chGains[b]!.GetValue<float>();
                    if (ChannelLinked) SetBandGain(b, g);
                    else SetBandGain(ch, b, g);
                }
            }
    }

    private static float DbToLinear(float db) =>
        db == 0f ? 1f : (float)Math.Pow(10.0, db / 20.0);
}
