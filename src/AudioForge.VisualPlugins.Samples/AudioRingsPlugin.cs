using AudioForge.Visualizer.Contracts;
using System.Drawing;

namespace AudioForge.VisualPlugins.Samples;

/// <summary>Circular spectrum ring - classic Winamp-style audio rings.</summary>
public sealed class AudioRingsPlugin : IVisualiserPlugin
{
    public string Name => "Audio Rings";
    public string Category => "Classic";
    public string Description => "Circular frequency rings pulsing with the spectrum";

    public void Draw(Graphics g, VisualFrame f)
    {
        g.Clear(Color.FromArgb(12, 12, 16));
        float cx = f.Width / 2f, cy = f.Height / 2f;
        float baseR = Math.Min(f.Width, f.Height) * 0.32f;
        var spec = f.SpectrumDb;
        if (spec.Length == 0) return;

        using var pen = new Pen(Color.Cyan, 3f);
        int bars = 96;
        for (int i = 0; i < bars; i++)
        {
            int bin0 = i * spec.Length / bars;
            int bin1 = Math.Max(bin0 + 1, (i + 1) * spec.Length / bars);
            float maxDb = -120f;
            for (int b = bin0; b < bin1 && b < spec.Length; b++) maxDb = Math.Max(maxDb, spec[b]);
            float t = Math.Clamp((maxDb + 90f) / 90f, 0f, 1f);

            double angle = i * 2.0 * Math.PI / bars - Math.PI / 2;
            float r0 = baseR, r1 = baseR + t * baseR * 0.9f;
            g.DrawLine(pen,
                cx + (float)(Math.Cos(angle) * r0), cy + (float)(Math.Sin(angle) * r0),
                cx + (float)(Math.Cos(angle) * r1), cy + (float)(Math.Sin(angle) * r1));
        }
        // inner ring
        using var pen2 = new Pen(Color.FromArgb(120, 200, 255), 1.5f);
        float pulse = baseR * 0.55f * (0.9f + 0.1f * (float)Math.Sin(f.TimeSeconds * 2.0));
        g.DrawEllipse(pen2, cx - pulse, cy - pulse, pulse * 2, pulse * 2);
    }
}
