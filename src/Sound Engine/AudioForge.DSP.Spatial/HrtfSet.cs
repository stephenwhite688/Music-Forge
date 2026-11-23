using System;
using System.Collections.Generic;

namespace AudioForge.DSP.Spatial;

/// <summary>
/// A set of HRTF impulse responses measured at discrete source directions.
/// Each entry pairs a direction (azimuth/elevation degrees) with a stereo IR:
/// channel 0 = left ear, channel 1 = right ear, at a known sample rate.
/// </summary>
public sealed class HrtfSet
{
    /// <summary>One measured direction: stereo IR (left, right).</summary>
    public sealed class Entry
    {
        public required float AzimuthDeg { get; init; }
        public required float ElevationDeg { get; init; }
        /// <summary>Interleaved stereo IR, or [left][right] planar — see <see cref="HrtfSet"/> ctor.</summary>
        public required float[] Ir { get; init; }
        public bool Interleaved { get; init; } = true;
    }

    public int SampleRate { get; }
    private readonly List<Entry> _entries;

    private HrtfSet(int sampleRate, List<Entry> entries)
    {
        SampleRate = sampleRate;
        _entries = entries;
    }

    /// <summary>Number of measured directions.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Creates an HRTF set. <paramref name="entries"/> IRs must all be at
    /// <paramref name="sampleRate"/>.
    /// </summary>
    public static HrtfSet Create(int sampleRate, IEnumerable<Entry> entries)
    {
        var list = new List<Entry>();
        foreach (var e in entries)
        {
            if (e.Ir.Length < 2) throw new ArgumentException("IR too short.");
            list.Add(e);
        }
        if (list.Count == 0) throw new ArgumentException("HRTF set is empty.");
        return new HrtfSet(sampleRate, list);
    }

    /// <summary>
    /// Convenience: create a set from parallel arrays of directions and IRs.
    /// </summary>
    public static HrtfSet Create(int sampleRate,
        float[] azimuths, float[] elevations, float[][] irs, bool[]? interleaved = null)
    {
        if (azimuths.Length != elevations.Length || azimuths.Length != irs.Length)
            throw new ArgumentException("Direction/IR arrays must match lengths.");
        var entries = new List<Entry>(azimuths.Length);
        for (int i = 0; i < azimuths.Length; i++)
            entries.Add(new Entry
            {
                AzimuthDeg = azimuths[i],
                ElevationDeg = elevations[i],
                Ir = irs[i],
                Interleaved = interleaved?[i] ?? true
            });
        return Create(sampleRate, entries);
    }

    /// <summary>
    /// Finds the nearest measured direction to (azimuth, elevation) using a
    /// wrapped-azimuth + elevation distance metric.
    /// </summary>
    public Entry Nearest(float azimuthDeg, float elevationDeg)
    {
        Entry best = _entries[0];
        double bestDist = double.MaxValue;
        foreach (var e in _entries)
        {
            double da = Math.Abs(NormalizeAz(e.AzimuthDeg - azimuthDeg));
            double de = e.ElevationDeg - elevationDeg;
            // Weight azimuth error higher than elevation (perceptually).
            double dist = da * da * 1.5 + de * de;
            if (dist < bestDist) { bestDist = dist; best = e; }
        }
        return best;
    }

    /// <summary>
    /// Extracts planar [left, right] IR channels from an entry, resampled to
    /// <paramref name="targetRate"/> if needed (linear interpolation).
    /// </summary>
    public (float[] Left, float[] Right) GetChannelPair(Entry entry, int targetRate)
    {
        int frames = entry.Interleaved ? entry.Ir.Length / 2 : entry.Ir.Length / 2;
        var left = new float[frames];
        var right = new float[frames];
        if (entry.Interleaved)
        {
            for (int i = 0; i < frames; i++)
            {
                left[i] = entry.Ir[i * 2];
                right[i] = entry.Ir[i * 2 + 1];
            }
        }
        else
        {
            // Planar packed: first half left, second half right.
            int half = entry.Ir.Length / 2;
            Array.Copy(entry.Ir, 0, left, 0, Math.Min(frames, half));
            Array.Copy(entry.Ir, half, right, 0, Math.Min(frames, half));
        }

        if (targetRate != SampleRate && frames > 1)
        {
            left = ResampleLinear(left, SampleRate, targetRate);
            right = ResampleLinear(right, SampleRate, targetRate);
        }
        return (left, right);
    }

    private static float NormalizeAz(double deg)
    {
        deg %= 360;
        if (deg > 180) deg -= 360;
        if (deg < -180) deg += 360;
        return (float)deg;
    }

    private static float[] ResampleLinear(float[] src, int srcRate, int dstRate)
    {
        int dstLen = (int)Math.Round(src.Length * (double)dstRate / srcRate);
        var dst = new float[dstLen];
        double step = (double)srcRate / dstRate;
        for (int i = 0; i < dstLen; i++)
        {
            double pos = i * step;
            int i0 = (int)pos;
            int i1 = Math.Min(i0 + 1, src.Length - 1);
            double frac = pos - i0;
            dst[i] = (float)(src[i0] * (1 - frac) + src[i1] * frac);
        }
        return dst;
    }
}
