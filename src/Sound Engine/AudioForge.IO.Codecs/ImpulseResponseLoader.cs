using AudioForge.IO;
using System;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Loads impulse-response files (typically WAV) into per-channel float
/// arrays for <c>ConvolutionReverbNode.SetImpulseResponse</c>. Mono files
/// produce a single-element array (applied to all channels); stereo files
/// produce left/right pairs for true stereo convolution. Any sample rate is
/// accepted — resample upstream if the IR rate differs from the engine rate
/// (the node does not resample IRs itself).
/// </summary>
public static class ImpulseResponseLoader
{
    /// <summary>
    /// Reads a WAV impulse response. Returns deinterleaved per-channel arrays.
    /// </summary>
    /// <exception cref="FormatException">Thrown for non-WAV or empty files.</exception>
    public static float[][] Load(string path)
    {
        using var reader = new WavReader(path);
        if (reader.TotalFrames <= 0)
            throw new FormatException($"Empty or unreadable impulse response: {path}");
        var interleaved = reader.ReadAll();
        int ch = Math.Max(1, reader.Channels);
        int frames = interleaved.Length / ch;
        var channels = new float[ch][];
        for (int c = 0; c < ch; c++) channels[c] = new float[frames];
        for (int f = 0; f < frames; f++)
            for (int c = 0; c < ch; c++)
                channels[c][f] = interleaved[f * ch + c];
        return channels;
    }

    /// <summary>Convenience: load and normalize peak to <paramref name="targetPeak"/>.</summary>
    public static float[][] LoadNormalized(string path, float targetPeak = 0.5f)
    {
        var irs = Load(path);
        float max = 0;
        foreach (var ch in irs)
            foreach (var s in ch) max = Math.Max(max, Math.Abs(s));
        if (max > 1e-9f)
        {
            float g = targetPeak / max;
            foreach (var ch in irs)
                for (int i = 0; i < ch.Length; i++) ch[i] *= g;
        }
        return irs;
    }
}
