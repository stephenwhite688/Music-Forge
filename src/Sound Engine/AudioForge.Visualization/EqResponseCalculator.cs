using AudioForge.DSP;
using AudioForge.DSP.GraphicEQ;

namespace AudioForge.Visualization;

/// <summary>
/// Engine-side EQ response computation. Evaluates the exact cascade of
/// biquad magnitude responses that <see cref="GraphicEqNode"/> applies to
/// audio - the same coefficients the DSP uses, via
/// <see cref="BiquadFilter.MagnitudeResponseDb"/> - so any UI can draw a
/// truthful EQ curve without reimplementing filter maths.
/// </summary>
public static class EqResponseCalculator
{
    /// <summary>
    /// Computes the combined magnitude response of a graphic EQ in dB at
    /// <paramref name="points"/> logarithmically spaced frequencies between
    /// <paramref name="fmin"/> and <paramref name="fmax"/> Hz.
    /// </summary>
    /// <param name="eq">The EQ node (must be prepared for its sample rate).</param>
    /// <param name="points">Number of output points (e.g. 512 for a smooth curve).</param>
    /// <param name="fmin">Lowest frequency in Hz.</param>
    /// <param name="fmax">Highest frequency in Hz.</param>
    /// <param name="channel">Channel whose gains to evaluate (ignored when channel-linked).</param>
    /// <returns>dB values, one per point, in ascending frequency order.</returns>
    public static float[] ComputeDbResponse(GraphicEqNode eq, int points = 512,
        float fmin = 20f, float fmax = 20000f, int channel = 0)
    {
        int sampleRate = eq.Format?.SampleRate ?? eq.SampleRate;
        var gains = eq.GetBandGains(Math.Clamp(channel, 0, eq.PreparedChannels - 1));
        var enabled = eq.GetBandEnabled();
        float q = eq.BandQ;
        var freqs = eq.Frequencies;

        // Rebuild the same biquad set the node uses (Peak type, same params).
        var filters = new AudioForge.DSP.BiquadFilter[freqs.Length];
        for (int b = 0; b < freqs.Length; b++)
        {
            filters[b] = new AudioForge.DSP.BiquadFilter(sampleRate, BiquadType.Peak);
            filters[b].SetParams(freqs[b], q, gains[b]);
        }

        var result = new float[points];
        bool bypass = eq.Bypass || !eq.Enabled;
        float inGainDb = eq.InputGainDb;
        float outGainDb = eq.OutputGainDb;

        for (int i = 0; i < points; i++)
        {
            double t = points == 1 ? 0 : (double)i / (points - 1);
            float f = (float)(fmin * Math.Pow(fmax / (double)fmin, t));

            if (bypass)
            {
                result[i] = 0f;
                continue;
            }

            double sumDb = inGainDb + outGainDb;
            for (int b = 0; b < filters.Length; b++)
            {
                if (!enabled[b]) continue;
                // Skip bands whose gain is ~0 - response is 0 dB anyway, saves math.
                if (Math.Abs(gains[b]) < 0.001f) continue;
                sumDb += filters[b].MagnitudeResponseDb(f);
            }
            result[i] = (float)sumDb;
        }
        return result;
    }

    /// <summary>Log-spaced frequency axis matching <see cref="ComputeDbResponse"/> points.</summary>
    public static float[] FrequencyAxis(int points, float fmin = 20f, float fmax = 20000f)
    {
        var axis = new float[points];
        for (int i = 0; i < points; i++)
        {
            double t = points == 1 ? 0 : (double)i / (points - 1);
            axis[i] = (float)(fmin * Math.Pow(fmax / (double)fmin, t));
        }
        return axis;
    }
}
