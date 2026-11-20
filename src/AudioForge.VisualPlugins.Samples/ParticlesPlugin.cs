using AudioForge.Visualizer.Contracts;
using System.Drawing;

namespace AudioForge.VisualPlugins.Samples;

/// <summary>
/// Spectrum-driven particles: each particle is bound to a frequency band.
/// Band energy controls launch speed and apex height - loud bands fire
/// particles high, quiet bands barely lift. Genuinely audio-reactive.
/// </summary>
public sealed class ParticlesPlugin : IVisualiserPlugin
{
    public string Name => "Particles";
    public string Category => "Modern";
    public string Description => "Particles rising with spectral energy";

    private const int Bands = 48;
    private const int PerBand = 4;
    private const int Count = Bands * PerBand;

    private readonly Random _rng = new(1234);
    private readonly float[] _bandEnergy = new float[Bands];
    private readonly float[] _px = new float[Count];
    private readonly float[] _py = new float[Count];
    private readonly float[] _vy = new float[Count];
    private readonly float[] _size = new float[Count];
    private readonly float[] _hue = new float[Count];
    private bool _init;

    public void OnActivated() => _init = false;

    public void Draw(Graphics g, VisualFrame f)
    {
        g.Clear(Color.FromArgb(8, 10, 18));
        var spec = f.SpectrumDb;
        if (spec.Length == 0) return;

        if (!_init)
        {
            for (int i = 0; i < Count; i++)
            {
                _px[i] = (float)_rng.NextDouble() * f.Width;
                _py[i] = f.Height;
                _vy[i] = 0f;
                _size[i] = 1.5f + (float)_rng.NextDouble() * 3f;
                _hue[i] = (float)_rng.NextDouble();
            }
            _init = true;
        }

        // Band energies, smoothed
        for (int b = 0; b < Bands; b++)
        {
            int bin0 = b * spec.Length / Bands;
            int bin1 = Math.Max(bin0 + 1, (b + 1) * spec.Length / Bands);
            float maxDb = -120f;
            for (int i = bin0; i < bin1 && i < spec.Length; i++) maxDb = Math.Max(maxDb, spec[i]);
            float t = Math.Clamp((maxDb + 90f) / 90f, 0f, 1f);
            _bandEnergy[b] = _bandEnergy[b] * 0.85f + t * 0.15f;
        }

        float dt = 0.033f; // ~30fps frame step
        using var brush = new SolidBrush(Color.White);
        for (int i = 0; i < Count; i++)
        {
            int band = i / PerBand;
            float e = _bandEnergy[band];

            // Energy drives launch speed and ceiling; gravity pulls back down
            float targetVy = -(60f + e * e * 900f); // px/s upward
            _vy[i] += (targetVy - _vy[i]) * 0.15f;
            _py[i] += _vy[i] * dt * (f.Height / 600f);
            _px[i] += (float)Math.Sin(f.TimeSeconds * 0.8 + i * 0.7) * 0.4f;

            // Fade out near apex, respawn at bottom
            float life = Math.Clamp(-_py[i] / f.Height, 0f, 1f);
            if (_py[i] < -20 || life > 0.98f)
            {
                _py[i] = f.Height + (float)_rng.NextDouble() * 30f;
                _px[i] = (float)_rng.NextDouble() * f.Width;
                _vy[i] = 0f;
                continue;
            }

            // Colour by band position + energy
            float hue = (band / (float)Bands * 0.7f + _hue[i] * 0.3f + e * 0.15f) % 1f;
            int alpha = (int)(60 + 195 * Math.Max(e, 0.15f));
            brush.Color = Hsv(hue, 0.9f, 1f, alpha);
            float s = _size[i] * (0.6f + e);
            g.FillEllipse(brush, _px[i], _py[i], s, s);
        }
    }

    private static Color Hsv(float h, float s, float v, int alpha)
    {
        int i = (int)(h * 6) % 6;
        float ff = h * 6 - (int)(h * 6);
        float p = v * (1 - s), q = v * (1 - ff * s), t = v * (1 - (1 - ff) * s);
        (float r, float gg, float b) = i switch
        {
            0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t),
            3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q)
        };
        return Color.FromArgb(alpha, (int)(r * 255), (int)(gg * 255), (int)(b * 255));
    }
}
