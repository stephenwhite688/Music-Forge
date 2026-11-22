using System;
using System.Drawing;

namespace MusicForge.UI.TransportBar;

/// <summary>Draws an oscilloscope trace from real waveform tap data.</summary>
public class OscilloscopeRenderer
{
    public void Draw(Graphics graphics, Rectangle bounds, float[] samples)
    {
        if (samples is null || samples.Length < 2) return;

        using var pen = new Pen(Color.LightSkyBlue, 1.2f);
        int midY = bounds.Y + bounds.Height / 2;
        float scale = bounds.Height / 2f * 0.9f;

        for (int x = 1; x < samples.Length && x < bounds.Width; x++)
        {
            int y0 = midY - (int)(samples[x - 1] * scale);
            int y1 = midY - (int)(samples[x] * scale);
            graphics.DrawLine(pen, bounds.X + x - 1, y0, bounds.X + x, y1);
        }
        using var gridPen = new Pen(Color.FromArgb(45, 45, 50));
        graphics.DrawLine(gridPen, bounds.X, midY, bounds.Right, midY);
    }
}
