using AudioForge.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.DSP.Spatial;

/// <summary>
/// HRTF convolution panner: positions a mono-folded source in 3D space for
/// headphone listening by convolving the input with per-ear head-related
/// impulse responses (left ear IR -> left output, right ear IR -> right
/// output). This is the convolution-based upgrade path over
/// <see cref="BinauralPannerNode"/>'s ITD/ILD approximation.
///
/// Requires an <see cref="HrtfSet"/> (measured HRTF data, e.g. KEMAR/SOFA-
/// derived). Direction changes select the nearest measured IR pair; there is
/// no interpolation between positions in this version.
///
/// Input is folded to mono before convolution (standard HRTF practice).
/// Distance applies inverse-distance gain + gentle air absorption.
/// </summary>
public sealed class HrtfPannerNode : AudioNode
{
    private const float ReferenceDistance = 1.0f;
    private const float MaxDistance = 100f;

    private readonly ConvolutionReverbNode _convolver;
    private readonly object _irLock = new();
    private HrtfSet? _hrtf;
    private float[]? _fallbackLeft, _fallbackRight;
    private float _azimuthDeg, _elevationDeg = 0f;
    private float _distanceM = 1f;
    private float _mix = 1f;      // 0 = dry, 1 = fully spatialised
    private bool _enabled = true;
    private bool _prepared;
    private float _airAbsorption = 0.5f; // HF damping per 10m, 0..1

    // Dry-mono buffer for mixing.
    private float[] _dryBuf = Array.Empty<float>();
    private int _dryLen;

    /// <summary>Azimuth in degrees (-180..180; 0=front, +/-90=sides, 180=rear).</summary>
    public float AzimuthDeg { get => _azimuthDeg; set => _azimuthDeg = Math.Clamp(value, -180f, 180f); }
    /// <summary>Elevation in degrees (-90..90).</summary>
    public float ElevationDeg { get => _elevationDeg; set => _elevationDeg = Math.Clamp(value, -90f, 90f); }
    /// <summary>Distance in metres (>=0.1).</summary>
    public float DistanceM { get => _distanceM; set => _distanceM = Math.Clamp(value, 0.1f, MaxDistance); }
    /// <summary>Wet/dry mix 0..1 (1 = fully HRTF).</summary>
    public float Mix { get => _mix; set => _mix = Math.Clamp(value, 0f, 1f); }
    /// <summary>Master enable.</summary>
    public bool Enabled { get => _enabled; set => _enabled = value; }
    /// <summary>Air absorption strength 0..1 (HF loss per 10 m).</summary>
    public float AirAbsorption { get => _airAbsorption; set => _airAbsorption = Math.Clamp(value, 0f, 1f); }

    /// <summary>The loaded HRTF dataset, if any.</summary>
    public HrtfSet? Hrtf => _hrtf;

    public HrtfPannerNode(int sampleRate = 48000, int channels = 2)
        : base(sampleRate, channels, "HrtfPanner")
    {
        _convolver = new ConvolutionReverbNode(sampleRate, 2);
        // HRTF IRs carry their own timing/timbre — wet-only, no reverb EQ.
        _convolver.TrySetParameter("reverb.mix", 1f);
        _convolver.TrySetParameter("reverb.predelay.ms", 0f);
        _convolver.TrySetParameter("reverb.damping", 0f);
    }

    /// <summary>Loads an HRTF dataset and immediately selects the current direction.</summary>
    public void SetHrtf(HrtfSet hrtf)
    {
        _hrtf = hrtf;
        ReloadIr();
    }

    /// <summary>
    /// Fallback single IR pair (left/right, planar) at the node's sample rate —
    /// used when no <see cref="HrtfSet"/> is loaded.
    /// </summary>
    public void SetImpulseResponsePair(float[] leftIr, float[] rightIr)
    {
        _fallbackLeft = leftIr;
        _fallbackRight = rightIr;
        ReloadIr();
    }

    private void ReloadIr()
    {
        float[][]? irs = null;
        lock (_irLock)
        {
            if (_hrtf != null)
            {
                var entry = _hrtf.Nearest(_azimuthDeg, _elevationDeg);
                var (l, r) = _hrtf.GetChannelPair(entry, SampleRate);
                irs = new[] { l, r };
            }
            else if (_fallbackLeft != null && _fallbackRight != null)
            {
                irs = new[] { _fallbackLeft, _fallbackRight };
            }
            if (irs != null)
                _convolver.SetImpulseResponse(irs);
        }
    }

    public override void Prepare(AudioFormat format)
    {
        _convolver.Prepare(format);
        _prepared = true;
        int frames = Math.Max(64, format.SampleRate / 20);
        _dryBuf = new float[frames * Math.Max(1, format.Channels)];
    }

    public override void Reset()
    {
        _convolver.Reset();
        base.Reset();
    }

    public override int LatencyFrames => _convolver.LatencyFrames;

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => new[]
    {
        new AudioParameterInfo("hrtf.azimuth", -180f, 180f, _azimuthDeg, "deg"),
        new AudioParameterInfo("hrtf.elevation", -90f, 90f, _elevationDeg, "deg"),
        new AudioParameterInfo("hrtf.distance", 0.1f, MaxDistance, _distanceM, "m"),
        new AudioParameterInfo("hrtf.mix", 0f, 1f, _mix, ""),
        new AudioParameterInfo("hrtf.airAbsorption", 0f, 1f, _airAbsorption, ""),
    };

        public override bool TryGetParameter(string name, out float value)
    {
        value = name switch
        {
            "azimuth" => _azimuthDeg,
            "elevation" => _elevationDeg,
            "distance" => _distanceM,
            "mix" => _mix,
            "airAbsorption" => _airAbsorption,
            _ => 0f
        };
        return name is "azimuth" or "elevation" or "distance" or "mix" or "airAbsorption";
    }

    public override bool TrySetParameter(string name, float value)
    {
        switch (name)
        {
            case "azimuth":
                float az = Math.Clamp(value, -180f, 180f);
                if (az != _azimuthDeg) { _azimuthDeg = az; ReloadIr(); }
                return true;
            case "elevation":
                float el = Math.Clamp(value, -90f, 90f);
                if (el != _elevationDeg) { _elevationDeg = el; ReloadIr(); }
                return true;
            case "distance": _distanceM = Math.Clamp(value, 0.1f, MaxDistance); return true;
            case "mix": _mix = Math.Clamp(value, 0f, 1f); return true;
            case "airAbsorption": _airAbsorption = Math.Clamp(value, 0f, 1f); return true;
            default: return false;
        }
    }

    public override void Process(Span<float> buffer)
    {
        if (!_enabled || _mix <= 0f || !_prepared)
        {
            if (!_enabled) return;
            ApplyDryMix(buffer);
            return;
        }

        int ch = Math.Max(1, Channels);
        int frames = buffer.Length / ch;

        // Fold input to mono in-place-safe manner: copy to dry buffer.
        _dryLen = Math.Min(buffer.Length, _dryBuf.Length);
        buffer[.._dryLen].CopyTo(_dryBuf);

        // Build duplicated-mono stereo for the convolver: ch0=ch1=mono mean.
        // Process per block of frames.
        Span<float> work = buffer;
        // The convolver is stereo; feed it [mono, mono] per frame.
        // For stereo input, mono = (L+R)/2.
        if (ch == 2)
        {
            for (int f = 0; f < frames; f++)
            {
                float m = (buffer[f * 2] + buffer[f * 2 + 1]) * 0.5f;
                buffer[f * 2] = m;
                buffer[f * 2 + 1] = m;
            }
        }
        else
        {
            for (int f = 0; f < frames; f++)
            {
                float m = buffer[f * ch];
                buffer[f * 2] = m;
                buffer[f * 2 + 1] = m;
            }
            // If channels==1, buffer may only hold frames samples — handled by
            // caller contract (engine is stereo). Guard:
            if (ch == 1) return;
        }

        _convolver.Process(buffer);

        // Distance gain + air absorption (simple one-pole HF-ish tilt via gain
        // only — real air absorption needs a filter; keep gain honest).
        float distGain = ReferenceDistance / Math.Max(ReferenceDistance, _distanceM);
        // Air absorption: reduce gain slightly with distance*strength.
        float air = 1f - Math.Min(0.5f, _distanceM / 100f * _airAbsorption * 2f);
        float gain = distGain * air;

        // Wet/dry mix + gain.
        for (int f = 0; f < frames; f++)
        {
            for (int c = 0; c < 2; c++)
            {
                int i = f * 2 + c;
                int dryIdx = ch == 2 ? i : f; // mono dry index
                float dry = _dryBuf[Math.Min(dryIdx, _dryBuf.Length - 1)];
                float wet = buffer[i] * gain;
                buffer[i] = dry * (1f - _mix) + wet * _mix;
            }
        }
    }

    private void ApplyDryMix(Span<float> buffer)
    {
        // Pass-through (enabled=false) or mix=0 handled by caller; here just
        // apply distance gain to dry signal for consistency.
        float distGain = ReferenceDistance / Math.Max(ReferenceDistance, _distanceM);
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] *= distGain;
    }

    public override JsonObject GetState() => new()
    {
        ["azimuth"] = _azimuthDeg,
        ["elevation"] = _elevationDeg,
        ["distance"] = _distanceM,
        ["mix"] = _mix,
        ["enabled"] = _enabled,
        ["airAbsorption"] = _airAbsorption,
    };

    public override void SetState(JsonObject state)
    {
        if (state["azimuth"] is JsonValue az) _azimuthDeg = az.GetValue<float>();
        if (state["elevation"] is JsonValue el) _elevationDeg = el.GetValue<float>();
        if (state["distance"] is JsonValue d) _distanceM = d.GetValue<float>();
        if (state["mix"] is JsonValue m) _mix = m.GetValue<float>();
        if (state["enabled"] is JsonValue en) _enabled = en.GetValue<bool>();
        if (state["airAbsorption"] is JsonValue aa) _airAbsorption = aa.GetValue<float>();
        ReloadIr();
    }
}
