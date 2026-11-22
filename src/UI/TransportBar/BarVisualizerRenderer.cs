using System;
using System.Drawing;

namespace MusicForge.UI.TransportBar;

/// <summary>Draws a spectrum bar display from real FFT tap data (dBFS).</summary>
public class BarVisualizerRenderer
{
    public void Draw(Graphics graphics, Rectangle bounds, float[] spectrumDb, int bandCount = 24)
    {
        if (spectrumDb is null || spectrumDb.Length == 0) return;

        using var brush = new SolidBrush(Color.LightSkyBlue);
        using var peakPen = new Pen(Color.OrangeRed, 1f);
        float barW = (float)bounds.Width / bandCount;
        float binHz = 1f; // caller passes already-mapped spectrum; bands are positional

        for (int b = 0; b < bandCount; b++)
        {
            int bin0 = b * spectrumDb.Length / bandCount;
            int bin1 = Math.Max(bin0 + 1, (b + 1) * spectrumDb.Length / bandCount);
            float maxDb = -120f;
            for (int i = bin0; i < bin1 && i < spectrumDb.Length; i++)
                if (spectrumDb[i] > maxDb) maxDb = spectrumDb[i];

            float t = Math.Clamp((maxDb + 90f) / 90f, 0f, 1f);
            float h = t * (bounds.Height - 4);
            float x = b * barW + 1;
            graphics.FillRectangle(brush, x, bounds.Bottom - h, Math.Max(1, barW - 2), h);
        }
    }
}
