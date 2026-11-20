using AudioForge.Visualizer.Contracts;
using System.Drawing;

namespace AudioForge.VisualPlugins.Samples;

/// <summary>Disco floor - beat-synced colour flashes on spectral transients.</summary>
public sealed class DiscoLightsPlugin : IVisualiserPlugin
{
    public string Name => "Disco Lights";
    public string Category => "Retro";
    public string Description => "Colour-flash party lights driven by transients";

    private static readonly Color[] Palette =
    {
        Color.Magenta, Color.Cyan, Color.Yellow, Color.Lime, Color.Orange,
        Color.DeepPink, Color.DeepSkyBlue, Color.Gold
    };

    private float _beat;      // transient energy 0..1
    private float _beatHold;  // decaying flash level
    private int _colorIdx;

    public void Draw(Graphics g, VisualFrame f)
    {
        g.Clear(Color.FromArgb(10, 10, 14));

        // Transient = short-term RMS energy vs its recent average
        float rms = 0;
        foreach (var v in f.RmsDb) rms = Math.Max(rms, v);
        float energy = Math.Clamp((rms + 60f) / 60f, 0f, 1f);
        _beat = energy > _beat * 1.4f ? energy : _beat * 0.92f;
        _beatHold = Math.Max(_beatHold * 0.90f, _beat);

        if (_beat > 0.75f && _beatHold < 0.4f) _colorIdx = (_colorIdx + 1) % Palette.Length;

        int cols = 16, rows = 6;
        float cw = (float)f.Width / cols, ch = (float)f.Height / rows;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                // checkerboard phase + radial flash
                bool on = ((r + c) % 2 == 0) == (((int)(f.TimeSeconds * 3) + _colorIdx) % 2 == 0);
                float dist = MathF.Sqrt(MathF.Pow(c - cols / 2f, 2) + MathF.Pow(r - rows / 2f, 2));
                float flash = Math.Clamp(_beatHold * 1.6f - dist * 0.12f, 0f, 1f);
                if (!on && flash < 0.3f) continue;
                var col = Palette[(_colorIdx + r + c) % Palette.Length];
                int a = (int)(40 + 215 * Math.Max(flash, on ? 0.35f * energy : 0f));
                using var brush = new SolidBrush(Color.FromArgb(a, col));
                g.FillRectangle(brush, c * cw + 2, r * ch + 2, cw - 4, ch - 4);
            }
    }
}
