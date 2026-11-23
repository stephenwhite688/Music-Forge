using System.Drawing;
using System.Windows.Forms;

namespace MusicForge.UI.Shared;

/// <summary>
/// Theme-aware <see cref="ToolStripProfessionalRenderer"/> for context menus.
/// Paints the dropdown background, item hover, and selection from
/// <see cref="ThemeManager.Current"/>; honours MouseOverEffectsEnabled.
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
        var theme = ThemeManager.Current;
        e.TextColor = theme.TextPrimary;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var theme = ThemeManager.Current;
        using var pen = new Pen(theme.BorderColor);
        int y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 28, y, e.Item.Width - 4, y);
    }
}
