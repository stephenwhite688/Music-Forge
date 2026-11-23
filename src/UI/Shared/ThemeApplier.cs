using System.Drawing;
using System.Windows.Forms;

namespace MusicForge.UI.Shared;

/// <summary>
/// Recursively applies <see cref="ThemeManager.Current"/> to a control tree.
/// Covers standard WinForms controls; custom-drawn surfaces (visualiser,
/// EQ display, artwork) read the theme themselves.
/// </summary>
public static class ThemeApplier
{
    /// <summary>Applies the current theme to a form or container and its children.</summary>
    public static void Apply(Control root)
    {
        var theme = ThemeManager.Current;
        ApplyTo(root, theme);
        foreach (Control child in root.Controls)
            ApplyRecursive(child, theme);
    }

    private static void ApplyRecursive(Control c, ThemeModel theme)
    {
        ApplyTo(c, theme);
        foreach (Control child in c.Controls)
            ApplyRecursive(child, theme);
    }

    private static void ApplyTo(Control c, ThemeModel theme)
    {
        switch (c)
        {
            case Form:
            case UserControl:
            case Panel:      // also covers TabPage, SplitContainer, TableLayoutPanel, FlowLayoutPanel
            case GroupBox:
                c.BackColor = theme.ContainerBackground;
                c.ForeColor = theme.TextPrimary;
                break;

            case Label:
                // Labels sit on varied backgrounds - only take the text colour
                c.ForeColor = theme.TextPrimary;
                break;

            case Button:
                c.BackColor = theme.ControlDefaultSurface;
                c.ForeColor = theme.TextPrimary;
                c.Font = MakeFont(theme);
                if (c is Button btn && btn.FlatStyle == FlatStyle.Flat)
                {
                    btn.FlatAppearance.BorderColor = theme.BorderColor;
                    btn.FlatAppearance.BorderSize = Math.Max(1, theme.BorderThickness);
                    btn.FlatAppearance.MouseOverBackColor = theme.ControlHoverSurface;
                    btn.FlatAppearance.MouseDownBackColor = theme.ControlPressedSurface;
                }
                break;

            case TextBoxBase:
            case ComboBox:
            case ListBox:
            case ListView:
            case TreeView:
            case NumericUpDown:
            case TrackBar:
                c.BackColor = theme.ControlDefaultSurface;
                c.ForeColor = theme.TextPrimary;
                c.Font = MakeFont(theme);
                break;

            case TabControl:
                c.BackColor = theme.ContainerBackground;
                c.ForeColor = theme.TextPrimary;
                break;
        }

        // Context menus get the dropdown colours
        if (c.ContextMenuStrip is { } cms)
        {
            cms.BackColor = theme.MenuDropdownBackground;
            cms.ForeColor = theme.TextPrimary;
        }
    }

    private static Font? _cachedFont;
    private static string _cachedKey = "";

    /// <summary>Theme font, cached per (name,size) so we don't leak Font objects.</summary>
    public static Font MakeFont(ThemeModel theme)
    {
        string key = $"{theme.FontName}|{theme.FontSizeStandard}";
        if (_cachedFont is null || _cachedKey != key)
        {
            _cachedFont?.Dispose();
            _cachedFont = new Font(theme.FontName, theme.FontSizeStandard > 0 ? theme.FontSizeStandard : 9F);
            _cachedKey = key;
        }
        return _cachedFont;
    }
}
