using AudioForge.Visualizer.Contracts;
using System.Drawing;

namespace AudioForge.VisualPlugins.Samples;

/// <summary>Spectrum-driven particles rising from the bottom edge.</summary>
public sealed class ParticlesPlugin : IVisualiserPlugin
{
    public string Name => "Particles";
    public string Category => "Modern";
    public string Description => "Particles rising with spectral energy";

    private readonly Random _rng = new(1234);
    private float[]? _energy; // smoothed band energies
    private float[]? _vel;
    private float[]? _x;

    private const int Count = 140;

    public void Draw(Graphics g, VisualFrame f)
    {
        g.Clear(Color.FromArgb(8, 10, 18));
        int bands = 48;
        _energy ??= new float[bands];
        _vel ??= new float[Count];
        _x ??= new float[Count];
        for (int i = 0; i < Count; i++) _x[i] = (float)_rng.NextDouble() * f.Width;

        // Band energies from spectrum
        var spec = f.SpectrumDb;
        for (int b = 0; b < bands; b++)
        {
            int bin0 = b * spec.Length / bands;
            int bin1 = Math.Max(bin0 + 1, (b + 1) * spec.Length / bands);
            float maxDb = -120f;
            for (int i = bin0; i < bin1 && i < spec.Length; i++) maxDb = Math.Max(maxDb, spec[i]);
            float t = Math.Clamp((maxDb + 90f) / 90f, 0f, 1f);
            _energy[b] = _energy[b] * 0.9f + t * 0.1f;
        }

        using var brush = new SolidBrush(Color.FromArgb(220, 120, 255));
        for (int i = 0; i < Count; i++)
        {
            int band = i % bands;
            float energy = _energy[band];
            _vel[i] = _vel[i] * 0.96f + energy * 6f + 0.4f;
            float y = f.Height - (_vel[i] % f.Height);
            // wrap: accumulate position via velocity stored mod height is crude; use time-based drift
            double phase = (f.TimeSeconds * _vel[i] * 30 + i * 97.0) % f.Height;
            y = f.Height - (float)phase;
            int size = 1 + (int)(energy * 5);
            float x = (_x[i] + (float)Math.Sin(f.TimeSeconds * 0.7 + i) * 8 + f.Width) % f.Width;
            brush.Color = Color.FromArgb(180 + (int)(75 * energy), (int)(80 + 120 * energy), 255);
            g.FillEllipse(brush, x, y, size, size);
        }
    }
}
