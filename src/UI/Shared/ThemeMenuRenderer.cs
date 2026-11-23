using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicForge.UI.Shared;

/// <summary>
/// Theme-aware <see cref="ToolStripProfessionalRenderer"/> for context menus.
/// Owns every paint pass - background, item hover/selection, check glyphs,
/// arrows, separators - so no system-coloured chrome (the "black box") leaks.
/// </summary>
public sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
{
    public static readonly ThemeMenuRenderer Instance = new();

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var theme = ThemeManager.Current;
        using var b = new SolidBrush(theme.MenuDropdownBackground);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var theme = ThemeManager.Current;
        if (!e.Item.Selected) return;

        var rect = new Rectangle(Point.Empty, e.Item.Size);
        rect.Inflate(-1, -1);
        Color fill = theme.MouseOverEffectsEnabled
            ? theme.MenuDropdownHover
            : theme.MenuDropdownBackground;
        using var b = new SolidBrush(fill);
        e.Graphics.FillRectangle(b, rect);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = ThemeManager.Current.TextPrimary;
        base.OnRenderItemText(e);
    }

    /// <summary>Check glyphs + item images - drawn in theme colours, never system black.</summary>
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item is ToolStripMenuItem { Checked: true } item && item.Image is null)
        {
            // Draw our own check in the image margin
            var theme = ThemeManager.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = Math.Min(e.ImageRectangle.Width, e.ImageRectangle.Height);
            var rect = new Rectangle(e.ImageRectangle.X + (e.ImageRectangle.Width - size) / 2,
                e.ImageRectangle.Y + (e.ImageRectangle.Height - size) / 2, size, size);
            using var b = new SolidBrush(theme.PrimaryAccent);
            using var path = new GraphicsPath();
            // simple check mark
            int pad = size / 4;
            var pts = new[]
            {
                new PointF(rect.X + pad, rect.Y + size / 2f),
                new PointF(rect.X + size / 2f - pad / 2, rect.Y + size - pad),
                new PointF(rect.X + size - pad, rect.Y + pad)
            };
            using var pen = new Pen(theme.PrimaryAccent, Math.Max(1.5f, size / 8f))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, pts);
            return;
        }
        base.OnRenderItemImage(e);
    }

    /// <summary>Submenu arrows - theme border colour instead of system grey.</summary>
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var theme = ThemeManager.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(theme.TextSecondary, 1.6f)
        { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int cx = e.ArrowRectangle.X + e.ArrowRectangle.Width / 2;
        int cy = e.ArrowRectangle.Y + e.ArrowRectangle.Height / 2;
        g.DrawLine(pen, cx - 3, cy - 4, cx + 1, cy);
        g.DrawLine(pen, cx + 1, cy, cx - 3, cy + 4);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var theme = ThemeManager.Current;
        using var pen = new Pen(theme.BorderColor);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var theme = ThemeManager.Current;
        using var pen = new Pen(theme.BorderColor);
        int y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 28, y, e.Item.Width - 4, y);
    }
}
