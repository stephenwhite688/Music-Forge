using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicForge.UI.Shared;

/// <summary>
/// JSON serialisation for <see cref="ThemeModel"/> - colours as ARGB ints,
/// everything else native. Used for theme export/import files and DB storage.
/// </summary>
public static class ThemeSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class ThemeDto
    {
        public int WindowBackground, ContainerBackground, ControlDefaultSurface,
            ControlHoverSurface, ControlPressedSurface, ControlDisabledSurface,
            PrimaryAccent, SecondaryAccent, HighlightColor, BorderColor,
            FocusBorderColor, BorderThickness, CornerRadius,
            TextPrimary, TextSecondary, TextDisabled, TextAccent,
            EqualizerBarDefault, EqualizerBarActive, OscilloscopeLine,
            OscilloscopeBackground, WaveformPrimary, WaveformSecondary,
            VisualizerSpacing, SliderTrackBackground, SliderTrackProgress,
            SliderThumbDefault, SliderThumbHover, SliderThumbSize,
            TabActiveBackground, TabInactiveBackground, MenuDropdownBackground,
            MenuDropdownHover;
        public string? FontName;
        public float FontSizeStandard, FontSizeHeader, FontSizeDigital;
        public bool FontAntiAliasing, GlobalToolTipsEnabled, MouseOverEffectsEnabled;
        public int ToolTipInitialDelayMs, ToolTipAutoPopDelayMs;
    }

    private static int C(Color c) => c.ToArgb();
    private static Color C(int argb) => Color.FromArgb(argb);

    public static string ToJson(ThemeModel t)
    {
        var d = new ThemeDto
        {
            WindowBackground = C(t.WindowBackground),
            ContainerBackground = C(t.ContainerBackground),
            ControlDefaultSurface = C(t.ControlDefaultSurface),
            ControlHoverSurface = C(t.ControlHoverSurface),
            ControlPressedSurface = C(t.ControlPressedSurface),
            ControlDisabledSurface = C(t.ControlDisabledSurface),
            PrimaryAccent = C(t.PrimaryAccent),
            SecondaryAccent = C(t.SecondaryAccent),
            HighlightColor = C(t.HighlightColor),
            BorderColor = C(t.BorderColor),
            FocusBorderColor = C(t.FocusBorderColor),
            BorderThickness = t.BorderThickness,
            CornerRadius = t.CornerRadius,
            TextPrimary = C(t.TextPrimary),
            TextSecondary = C(t.TextSecondary),
            TextDisabled = C(t.TextDisabled),
            TextAccent = C(t.TextAccent),
            EqualizerBarDefault = C(t.EqualizerBarDefault),
            EqualizerBarActive = C(t.EqualizerBarActive),
            OscilloscopeLine = C(t.OscilloscopeLine),
            OscilloscopeBackground = C(t.OscilloscopeBackground),
            WaveformPrimary = C(t.WaveformPrimary),
            WaveformSecondary = C(t.WaveformSecondary),
            VisualizerSpacing = t.VisualizerSpacing,
            SliderTrackBackground = C(t.SliderTrackBackground),
            SliderTrackProgress = C(t.SliderTrackProgress),
            SliderThumbDefault = C(t.SliderThumbDefault),
            SliderThumbHover = C(t.SliderThumbHover),
            SliderThumbSize = t.SliderThumbSize,
            TabActiveBackground = C(t.TabActiveBackground),
            TabInactiveBackground = C(t.TabInactiveBackground),
            MenuDropdownBackground = C(t.MenuDropdownBackground),
            MenuDropdownHover = C(t.MenuDropdownHover),
            FontName = t.FontName,
            FontSizeStandard = t.FontSizeStandard,
            FontSizeHeader = t.FontSizeHeader,
            FontSizeDigital = t.FontSizeDigital,
            FontAntiAliasing = t.FontAntiAliasing,
            GlobalToolTipsEnabled = t.GlobalToolTipsEnabled,
            MouseOverEffectsEnabled = t.MouseOverEffectsEnabled,
            ToolTipInitialDelayMs = t.ToolTipInitialDelayMs,
            ToolTipAutoPopDelayMs = t.ToolTipAutoPopDelayMs
        };
        return JsonSerializer.Serialize(d, Options);
    }

    public static ThemeModel? FromJson(string json)
    {
        ThemeDto? d;
        try { d = JsonSerializer.Deserialize<ThemeDto>(json); }
        catch { return null; }
        if (d is null) return null;

        // Reject degenerate themes (e.g. "{}" or partial files): every colour
        // would deserialize as transparent and crash controls that reject
        // transparent BackColor.
        if (d.WindowBackground == 0 || d.ControlDefaultSurface == 0 || d.TextPrimary == 0)
            return null;

        return new ThemeModel
        {
            WindowBackground = C(d.WindowBackground),
            ContainerBackground = C(d.ContainerBackground),
            ControlDefaultSurface = C(d.ControlDefaultSurface),
            ControlHoverSurface = C(d.ControlHoverSurface),
            ControlPressedSurface = C(d.ControlPressedSurface),
            ControlDisabledSurface = C(d.ControlDisabledSurface),
            PrimaryAccent = C(d.PrimaryAccent),
            SecondaryAccent = C(d.SecondaryAccent),
            HighlightColor = C(d.HighlightColor),
            BorderColor = C(d.BorderColor),
            FocusBorderColor = C(d.FocusBorderColor),
            BorderThickness = d.BorderThickness,
            CornerRadius = d.CornerRadius,
            TextPrimary = C(d.TextPrimary),
            TextSecondary = C(d.TextSecondary),
            TextDisabled = C(d.TextDisabled),
            TextAccent = C(d.TextAccent),
            EqualizerBarDefault = C(d.EqualizerBarDefault),
            EqualizerBarActive = C(d.EqualizerBarActive),
            OscilloscopeLine = C(d.OscilloscopeLine),
            OscilloscopeBackground = C(d.OscilloscopeBackground),
            WaveformPrimary = C(d.WaveformPrimary),
            WaveformSecondary = C(d.WaveformSecondary),
            VisualizerSpacing = d.VisualizerSpacing,
            SliderTrackBackground = C(d.SliderTrackBackground),
            SliderTrackProgress = C(d.SliderTrackProgress),
            SliderThumbDefault = C(d.SliderThumbDefault),
            SliderThumbHover = C(d.SliderThumbHover),
            SliderThumbSize = d.SliderThumbSize,
            TabActiveBackground = C(d.TabActiveBackground),
            TabInactiveBackground = C(d.TabInactiveBackground),
            MenuDropdownBackground = C(d.MenuDropdownBackground),
            MenuDropdownHover = C(d.MenuDropdownHover),
            FontName = d.FontName ?? "Segoe UI",
            FontSizeStandard = d.FontSizeStandard > 0 ? d.FontSizeStandard : 9F,
            FontSizeHeader = d.FontSizeHeader,
            FontSizeDigital = d.FontSizeDigital,
            FontAntiAliasing = d.FontAntiAliasing,
            GlobalToolTipsEnabled = d.GlobalToolTipsEnabled,
            MouseOverEffectsEnabled = d.MouseOverEffectsEnabled,
            ToolTipInitialDelayMs = d.ToolTipInitialDelayMs,
            ToolTipAutoPopDelayMs = d.ToolTipAutoPopDelayMs
        };
    }
}
