using AudioForge.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.DSP.Spatial;

/// <summary>
/// Binaural panner: positions a stereo (or mono-folded) source in 3D space
/// for headphone listening using interaural time difference (ITD),
/// interaural level difference (ILD) and distance attenuation.
///
/// Model (perceptual approximation, HRTF-ready):
///  - Azimuth -180..180 deg (0 = front, +/-90 = sides, +/-180 = rear)
///  - Elevation -90..90 deg (affects ILD slightly, no spectral cues yet)
///  - Distance in metres -> inverse-distance gain + air absorption HF rolloff
///
/// The architecture isolates the spatialisation math in
/// <see cref="ComputeSpatialGains"/> so a future HRTF convolution backend can
/// replace ITD/ILD without changing the node contract.
///
/// Realtime-safe: delay lines are preallocated; Process allocates nothing.
/// </summary>
public sealed class BinauralPannerNode : AudioNode
{
    // ---- tunables ----
    private const double MaxItcSeconds = 0.0007;    // ~0.7 ms max interaural delay
    private const double SpeedOfSound = 343.0;       // m/s
    private const double HeadPathLength = 0.20;      // ~20cm ear-to-ear path for ITD
    private const float ReferenceDistance = 1.0f;    // metres, unity gain
    private const float MaxDistance = 100f;

    // Fractional-delay lines per ear (mono-sum input -> per-ear output).
    private float[] _delayL = Array.Empty<float>();
    private float[] _delayR = Array.Empty<float>();
    private int _delaySize;
    private int _writePos;

    // Parameters.
    private float _azimuthDeg;      // -180..180
    private float _elevationDeg;    // -90..90
    private float _distanceM = 1f;  // metres
    private bool _enabled = true;

    /// <summary>Azimuth in degrees. 0=front, +90=left, -90=right, +/-180=rear.</summary>
    public float AzimuthDeg { get => _azimuthDeg; set => _azimuthDeg = Math.Clamp(value, -180f, 180f); }

    /// <summary>Elevation in degrees (-90 below .. +90 above).</summary>
    public float ElevationDeg { get => _elevationDeg; set => _elevationDeg = Math.Clamp(value, -90f, 90f); }

    /// <summary>Source distance in metres (>= 0.1).</summary>
    public float DistanceM { get => _distanceM; set => _distanceM = Math.Clamp(value, 0.1f, MaxDistance); }

    /// <summary>Master enable - bypasses processing when false.</summary>
    public bool Enabled { get => _enabled; set => _enabled = value; }

    /// <summary>Computed per-ear gains (linear) for the current parameters.</summary>
    public (float Left, float Right) CurrentGains { get; private set; } = (1f, 1f);
    /// <summary>Computed ITD in samples at the current format (positive = left ear delayed).</summary>
    public double CurrentItcSamples { get; private set; }

    public BinauralPannerNode(int sampleRate, int channels = 2, string name = "BinauralPanner")
        : base(sampleRate, channels, name)
    {
        // Delay buffer must cover max ITD (~0.65ms) plus margin.
        _delaySize = Math.Max(64, (int)(SampleRate * 0.002)); // 2ms
        _delayL = new float[_delaySize];
        _delayR = new float[_delaySize];
    }

    public override void Reset()
    {
        Array.Clear(_delayL);
        Array.Clear(_delayR);
        _writePos = 0;
    }

    // ---- parameter / state model ----

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => new[]
    {
        new AudioParameterInfo("spatial.azimuth", -180f, 180f, _azimuthDeg, "deg"),
        new AudioParameterInfo("spatial.elevation", -90f, 90f, _elevationDeg, "deg"),
        new AudioParameterInfo("spatial.distance", 0.1f, MaxDistance, _distanceM, "m"),
        new AudioParameterInfo("spatial.enabled", 0f, 1f, _enabled ? 1f : 0f, ""),
    };

    public override bool TryGetParameter(string name, out float value)
    {
        value = name switch
        {
            "spatial.azimuth" => _azimuthDeg,
            "spatial.elevation" => _elevationDeg,
            "spatial.distance" => _distanceM,
            "spatial.enabled" => _enabled ? 1f : 0f,
            _ => float.NaN,
        };
        return !float.IsNaN(value);
    }

    public override bool TrySetParameter(string name, float value)
    {
        switch (name)
        {
            case "spatial.azimuth": AzimuthDeg = value; return true;
            case "spatial.elevation": ElevationDeg = value; return true;
            case "spatial.distance": DistanceM = value; return true;
            case "spatial.enabled": Enabled = value >= 0.5f; return true;
            default: return false;
        }
    }

    public override JsonObject GetState() => new()
    {
        ["azimuth"] = _azimuthDeg,
        ["elevation"] = _elevationDeg,
        ["distance"] = _distanceM,
        ["enabled"] = _enabled,
    };

    public override void SetState(JsonObject state)
    {
        if (state is null) return;
        if (state["azimuth"] is JsonValue a) AzimuthDeg = a.GetValue<float>();
        if (state["elevation"] is JsonValue e) ElevationDeg = e.GetValue<float>();
        if (state["distance"] is JsonValue d) DistanceM = d.GetValue<float>();
        if (state["enabled"] is JsonValue en) Enabled = en.GetValue<bool>();
    }

    // ---- spatial model ----

    /// <summary>
    /// Computes per-ear linear gains and ITC delay for the current
    /// azimuth/elevation. Isolated so an HRTF backend can replace it.
    /// </summary>
    private void ComputeSpatialGains(int sampleRate, out float gainL, out float gainR, out double itcSamples)
    {
        double az = _azimuthDeg * Math.PI / 180.0;
        double el = _elevationDeg * Math.PI / 180.0;

        // ILD: equal-power-ish pan law shaped by azimuth. Front centre = equal.
        // Positive azimuth = source to the left -> left ear louder.
        // Clamp to ~0.9 so extreme azimuths keep a faint contralateral
        // component - real ILDs max out around 15-20 dB, never infinite.
        double pan = Math.Clamp(Math.Sin(az) * Math.Cos(el * 0.5), -0.9, 0.9);
        gainL = (float)Math.Sqrt(0.5 * (1.0 + pan));
        gainR = (float)Math.Sqrt(0.5 * (1.0 - pan));

        // ITD: path-length difference across the head.
        // itd = (r/c) * (sin(az) - elContribution); clamp to max ~0.65ms.
        double itdSeconds = (HeadPathLength / SpeedOfSound)
            * Math.Abs(Math.Sin(az)) * Math.Cos(el * 0.3);
        itdSeconds = Math.Min(itdSeconds, MaxItcSeconds);
        itcSamples = itdSeconds * sampleRate;

        // Distance: inverse-law gain + gentle HF air absorption approximated
        // as a slight broadband level reduction beyond ~10m.
        float distGain = ReferenceDistance / Math.Max(ReferenceDistance, _distanceM);
        float airAbsorption = _distanceM > 10f ? MathF.Pow(10f, -(_distanceM - 10f) * 0.002f) : 1f;
        gainL *= distGain * airAbsorption;
        gainR *= distGain * airAbsorption;
    }

    public override void Process(Span<float> buffer)
    {
        if (!_enabled || Format is null) return;
        int channels = Math.Max(1, Format.Channels);
        int frames = buffer.Length / channels;
        if (frames <= 0) return;
        int sampleRate = Format.SampleRate;

        ComputeSpatialGains(sampleRate, out float gainL, out float gainR, out double itc);
        CurrentGains = (gainL, gainR);
        CurrentItcSamples = itc;

        // Per-ear fractional delay. Negative azimuth = source right => left
        // ear is delayed; positive azimuth = source left => right delayed.
        double delayL = _azimuthDeg < 0 ? itc : 0;
        double delayR = _azimuthDeg > 0 ? itc : 0;

        for (int f = 0; f < frames; f++)
        {
            int idx = f * channels;
            // Mono-sum input (average channels) as the source signal.
            float src = 0f;
            for (int c = 0; c < channels && c < 2; c++) src += buffer[idx + c];
            src /= Math.Min(channels, 2);

            // Write into delay lines.
            _delayL[_writePos] = src;
            _delayR[_writePos] = src;

            // Read with fractional delay (look back delaySamples).
            float outL = ReadDelay(_delayL, delayL, gainL);
            float outR = ReadDelay(_delayR, delayR, gainR);
            _writePos = (_writePos + 1) % _delaySize;

            if (channels >= 2) { buffer[idx] = outL; buffer[idx + 1] = outR; }
            else buffer[idx] = (outL + outR) * 0.5f;
        }
    }

    /// <summary>Reads the delay line at (writePos - delaySamples) with linear interpolation.</summary>
    private float ReadDelay(float[] line, double delaySamples, float gain)
    {
        // _writePos holds the sample just written this frame.
        if (delaySamples <= 0.001) return line[_writePos] * gain;
        double pos = _writePos - delaySamples;
        int i0 = (int)Math.Floor(pos);
        double frac = pos - i0;
        int a = ((i0 % _delaySize) + _delaySize) % _delaySize;
        int b = (a + 1) % _delaySize;
        float s0 = line[a], s1 = line[b];
        return (float)(s0 + (s1 - s0) * frac) * gain;
    }
}
