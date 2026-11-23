using System;

namespace AudioForge.Analysis;

/// <summary>
/// Estimates musical tempo (BPM) from an audio buffer using spectral-flux
/// onset detection followed by autocorrelation of the onset envelope.
/// Deterministic and allocation-light; operates on interleaved float PCM.
/// </summary>
public static class BpmDetector
{
    /// <summary>
    /// Detects the dominant tempo of <paramref name="interleavedPcm"/>.
    /// </summary>
    /// <param name="interleavedPcm">Interleaved float PCM (-1..1).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Channel count (downmixed to mono).</param>
    /// <param name="minBpm">Minimum tempo to consider (default 60).</param>
    /// <param name="maxBpm">Maximum tempo to consider (default 200).</param>
    /// <returns>Estimated BPM, or 0 if detection failed (too short/silent).</returns>
    public static double DetectBpm(ReadOnlySpan<float> interleavedPcm,
        int sampleRate, int channels, double minBpm = 60, double maxBpm = 200)
    {
        if (sampleRate <= 0 || channels <= 0 || interleavedPcm.Length < sampleRate / 4)
            return 0;

        int frames = interleavedPcm.Length / channels;

        // --- Onset envelope: short-time energy flux at ~100 Hz hop rate ---
        const int fftSize = 1024;
        int hop = Math.Max(64, sampleRate / 100); // ~10 ms
        int numFrames = Math.Max(0, (frames - fftSize) / hop);
        if (numFrames < 32) return 0;

        // Mono-mix once into a temp buffer.
        var mono = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            for (int c = 0; c < channels; c++)
                sum += interleavedPcm[f * channels + c];
            mono[f] = (float)(sum / channels);
        }

        // Per-frame log magnitude spectrum (naive DFT would be too slow; use
        // a simple band-energy flux instead — robust for tempo).
        var flux = new float[numFrames];
        double prevEnergy = 0;
        int bandCount = 16;
        var bands = new double[bandCount];
        var prevBands = new double[bandCount];
        int bandWidth = fftSize / bandCount;

        // Hann window.
        var window = new float[fftSize];
        for (int i = 0; i < fftSize; i++)
            window[i] = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / (fftSize - 1));

        // Use a radix-2 FFT via the engine's FFT if available; otherwise do a
        // Goertzel-free approach: band energies via Goertzel at band centers.
        var freqs = new double[bandCount];
        for (int b = 0; b < bandCount; b++)
            freqs[b] = (b + 0.5) * (sampleRate / 2.0 / bandCount);

        double[] mag = new double[bandCount];
        for (int fr = 0; fr < numFrames; fr++)
        {
            int start = fr * hop;
            Array.Clear(mag);
            // Goertzel per band center frequency over the window.
            for (int b = 0; b < bandCount; b++)
            {
                double w = 2.0 * Math.PI * freqs[b] / sampleRate;
                double coeff = 2.0 * Math.Cos(w);
                double s0 = 0, s1 = 0, s2 = 0;
                for (int i = 0; i < fftSize; i++)
                {
                    double x = mono[start + i] * window[i];
                    s0 = x + coeff * s1 - s2;
                    s2 = s1; s1 = s0;
                }
                double power = s1 * s1 + s2 * s2 - coeff * s1 * s2;
                mag[b] = Math.Sqrt(Math.Max(0, power));
            }
            double f = 0;
            for (int b = 0; b < bandCount; b++)
            {
                double d = mag[b] - prevBands[b];
                if (d > 0) f += d;
                prevBands[b] = mag[b];
            }
            flux[fr] = (float)f;
            prevEnergy = f;
        }

        // --- Autocorrelation of onset envelope over tempo range ---
        double hopSeconds = (double)hop / sampleRate;
        int minLag = Math.Max(1, (int)Math.Round(60.0 / (maxBpm * hopSeconds)));
        int maxLag = Math.Min(numFrames / 2, (int)Math.Round(60.0 / (minBpm * hopSeconds)));
        if (maxLag <= minLag) return 0;

        // Normalize flux (zero mean).
        double mean = 0;
        foreach (var v in flux) mean += v;
        mean /= flux.Length;
        var env = new double[flux.Length];
        for (int i = 0; i < flux.Length; i++) env[i] = flux[i] - mean;

        // Autocorrelation with unbiased normalization.
        int bestLag = 0;
        double bestCorr = -1;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double sum = 0, e0 = 0, e1 = 0;
            for (int i = 0; i + lag < env.Length; i++)
            {
                sum += env[i] * env[i + lag];
                e0 += env[i] * env[i];
                e1 += env[i + lag] * env[i + lag];
            }
            double denom = Math.Sqrt(e0 * e1);
            if (denom < 1e-12) continue;
            double corr = sum / denom;
            if (corr > bestCorr) { bestCorr = corr; bestLag = lag; }
        }

        if (bestLag == 0 || bestCorr < 0.05) return 0;

        double bpm = 60.0 / (bestLag * hopSeconds);

        // Octave correction: prefer the half/double tempo if correlation is
        // comparable (common octave error in onset autocorrelation).
        foreach (double mult in new[] { 0.5, 2.0 })
        {
            double target = bpm * mult;
            if (target < minBpm || target > maxBpm) continue;
            int lag2 = (int)Math.Round(60.0 / (target * hopSeconds));
            if (lag2 < 1 || lag2 >= env.Length) continue;
            double sum = 0, e0 = 0, e1 = 0;
            for (int i = 0; i + lag2 < env.Length; i++)
            {
                sum += env[i] * env[i + lag2];
                e0 += env[i] * env[i];
                e1 += env[i + lag2] * env[i + lag2];
            }
            double corr2 = Math.Sqrt(e0 * e1) > 1e-12 ? sum / Math.Sqrt(e0 * e1) : 0;
            if (corr2 > bestCorr * 0.85) { bpm = target; bestCorr = corr2; }
        }

        return Math.Round(bpm, 1);
    }
}
