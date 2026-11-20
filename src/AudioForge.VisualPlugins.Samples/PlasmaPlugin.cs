using AudioForge.Visualizer.Contracts;
using System.Drawing;

namespace AudioForge.VisualPlugins.Samples;

/// <summary>Classic plasma field with audio-reactive colour modulation.</summary>
public sealed class PlasmaPlugin : IVisualiserPlugin
{
    public string Name => "Plasma";
    public string Category => "Retro";
    public string Description => "Generative plasma field modulated by audio energy";

    // Precomputed plasma value noise grid, animated by time + audio energy
    private float _energy;

    public void Draw(Graphics g, VisualFrame f)
    {
        g.Clear(Color.Black);
        float rms = 0;
        foreach (var v in f.RmsDb) rms = Math.Max(rms, v);
        _energy = _energy * 0.95f + Math.Clamp((rms + 60f) / 60f, 0f, 1f) * 0.05f;

        int cell = 8;
        int cols = f.Width / cell + 1, rows = f.Height / cell + 1;
        double t = f.TimeSeconds * 0.7;

        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                double v = Math.Sin(x * 0.11 + t)
                         + Math.Sin(y * 0.13 - t * 1.3)
                         + Math.Sin((x + y) * 0.09 + t * 0.7)
                         + Math.Sin(Math.Sqrt(x * x + y * y) * 0.07 - t * 2.0);
                v += _energy * 2.0;
                double hue = (v + 4.0) / 8.0;
                hue -= Math.Floor(hue);
                using var brush = new SolidBrush(Hsv((float)hue, 0.85f, 0.9f));
                g.FillRectangle(brush, x * cell, y * cell, cell + 1, cell + 1);
            }
    }

    private static Color Hsv(float h, float s, float v)
    {
        int i = (int)(h * 6);
        float ff = h * 6 - i;
        float p = v * (1 - s), q = v * (1 - ff * s), t = v * (1 - (1 - ff) * s);
        (float r, float gg, float b) = (i % 6) switch
        {
            0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t),
            3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q)
        };
        return Color.FromArgb((int)(r * 255), (int)(gg * 255), (int)(b * 255));
    }
}
