using System.Windows.Forms;

namespace MusicForge.UI.Shared;

/// <summary>
/// Installs tooltips across a control tree using one shared
/// <see cref="ToolTip"/>. Tip text comes from button labels and friendly
/// names for the image-based transport controls. Respects
/// <see cref="ThemeModel.GlobalToolTipsEnabled"/> and the theme's delays.
/// </summary>
public static class UiTooltips
{
    private static ToolTip? _tip;

    private static ToolTip Tip
    {
        get
        {
            _tip ??= new ToolTip { AutoPopDelay = 5000, InitialDelay = 500, ReshowDelay = 100 };
            var theme = ThemeManager.Current;
            _tip.AutoPopDelay = theme.ToolTipAutoPopDelayMs > 0 ? theme.ToolTipAutoPopDelayMs : 5000;
            _tip.InitialDelay = theme.ToolTipInitialDelayMs > 0 ? theme.ToolTipInitialDelayMs : 500;
            _tip.ReshowDelay = 100;
            return _tip;
        }
    }

    /// <summary>Walks a control tree assigning tooltip text. Call on theme change.</summary>
    public static void Apply(Control root)
    {
        var theme = ThemeManager.Current;
        ApplyRecursive(root, theme.GlobalToolTipsEnabled);
    }

    private static void ApplyRecursive(Control c, bool enabled)
    {
        string? text = enabled ? TipTextFor(c) : null;
        if (text is not null)
            Tip.SetToolTip(c, text);
        else if (!enabled)
            Tip.SetToolTip(c, string.Empty);

        foreach (Control child in c.Controls)
            ApplyRecursive(child, enabled);
    }

    private static string? TipTextFor(Control c)
    {
        switch (c)
        {
            case Button btn when !string.IsNullOrWhiteSpace(btn.Text):
                return btn.Text;

            // Image-only transport buttons - friendly names by control type
            case MusicForge.UI.TransportBar.PlayButtonControl: return "Play / Pause";
            case MusicForge.UI.TransportBar.StopButtonControl: return "Stop";
            case MusicForge.UI.TransportBar.NextButtonControl: return "Next";
            case MusicForge.UI.TransportBar.PreviousButtonControl: return "Previous";
            case MusicForge.UI.TransportBar.RepeatButtonControl: return "Repeat";
            case MusicForge.UI.TransportBar.ShuffleButtonControl: return "Shuffle";
            case MusicForge.UI.TransportBar.VolumeControl: return "Volume";
            case MusicForge.UI.TransportBar.TrackInfoControl: return "Now playing";
            case MusicForge.UI.TransportBar.MiniVisualizerControl: return "Mini visualiser";
            case MusicForge.UI.TransportBar.VisualizerModeControl: return "Visualiser mode";
            case MusicForge.UI.TransportBar.VisualizerPowerControl: return "Visualiser power";
        }
        return null;
    }
}
