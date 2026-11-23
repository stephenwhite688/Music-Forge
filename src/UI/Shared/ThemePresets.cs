using System.Drawing;

namespace MusicForge.UI.Shared;

/// <summary>
/// Built-in theme presets. Each factory starts from the dark default and
/// adjusts the palette - presets are data, not code paths.
/// </summary>
public static class ThemePresets
{
    public static ThemeModel Dark() => ThemeManager.CreateDefault();

    public static ThemeModel Light()
    {
        var t = ThemeManager.CreateDefault();
        t.WindowBackground = Color.FromArgb(240, 240, 242);
        t.ContainerBackground = Color.FromArgb(228, 228, 232);
        t.ControlDefaultSurface = Color.FromArgb(250, 250, 252);
        t.ControlHoverSurface = Color.FromArgb(235, 238, 245);
        t.ControlPressedSurface = Color.FromArgb(220, 226, 238);
        t.ControlDisabledSurface = Color.FromArgb(235, 235, 238);
        t.TextPrimary = Color.FromArgb(30, 30, 34);
        t.TextSecondary = Color.FromArgb(70, 90, 130);
        t.TextDisabled = Color.FromArgb(140, 140, 145);
        t.TextAccent = Color.FromArgb(20, 60, 120);
        t.BorderColor = Color.FromArgb(190, 195, 205);
        t.MenuDropdownBackground = Color.FromArgb(250, 250, 252);
        t.MenuDropdownHover = Color.FromArgb(225, 235, 250);
        t.TabActiveBackground = Color.FromArgb(250, 250, 252);
        t.TabInactiveBackground = Color.FromArgb(228, 228, 232);
        t.SliderTrackBackground = Color.FromArgb(200, 205, 215);
        t.OscilloscopeBackground = Color.FromArgb(245, 246, 250);
        return t;
    }

    public static ThemeModel Blue()
    {
        var t = ThemeManager.CreateDefault();
        t.PrimaryAccent = Color.FromArgb(30, 100, 220);
        t.SecondaryAccent = Color.FromArgb(15, 50, 120);
        t.HighlightColor = Color.FromArgb(90, 170, 255);
        t.FocusBorderColor = Color.FromArgb(120, 190, 255);
        t.TextAccent = Color.FromArgb(160, 210, 255);
        t.EqualizerBarDefault = Color.FromArgb(30, 100, 220);
        t.EqualizerBarActive = Color.FromArgb(90, 170, 255);
        t.OscilloscopeLine = Color.FromArgb(90, 170, 255);
        t.WaveformPrimary = Color.FromArgb(90, 170, 255);
        return t;
    }

    public static ThemeModel Green()
    {
        var t = ThemeManager.CreateDefault();
        t.PrimaryAccent = Color.FromArgb(20, 140, 80);
        t.SecondaryAccent = Color.FromArgb(10, 70, 40);
        t.HighlightColor = Color.FromArgb(80, 210, 140);
        t.FocusBorderColor = Color.FromArgb(110, 230, 160);
        t.TextAccent = Color.FromArgb(140, 235, 180);
        t.EqualizerBarDefault = Color.FromArgb(20, 140, 80);
        t.EqualizerBarActive = Color.FromArgb(80, 210, 140);
        t.OscilloscopeLine = Color.FromArgb(80, 210, 140);
        t.WaveformPrimary = Color.FromArgb(80, 210, 140);
        return t;
    }

    public static ThemeModel Red()
    {
        var t = ThemeManager.CreateDefault();
        t.PrimaryAccent = Color.FromArgb(190, 40, 50);
        t.SecondaryAccent = Color.FromArgb(100, 20, 28);
        t.HighlightColor = Color.FromArgb(240, 90, 100);
        t.FocusBorderColor = Color.FromArgb(255, 120, 130);
        t.TextAccent = Color.FromArgb(255, 150, 160);
        t.EqualizerBarDefault = Color.FromArgb(190, 40, 50);
        t.EqualizerBarActive = Color.FromArgb(240, 90, 100);
        t.OscilloscopeLine = Color.FromArgb(240, 90, 100);
        t.WaveformPrimary = Color.FromArgb(240, 90, 100);
        return t;
    }

    public static ThemeModel HighContrast()
    {
        var t = ThemeManager.CreateDefault();
        t.WindowBackground = Color.Black;
        t.ContainerBackground = Color.Black;
        t.ControlDefaultSurface = Color.FromArgb(20, 20, 20);
        t.ControlHoverSurface = Color.FromArgb(60, 60, 60);
        t.ControlPressedSurface = Color.FromArgb(80, 80, 80);
        t.TextPrimary = Color.White;
        t.TextSecondary = Color.FromArgb(230, 230, 230);
        t.TextAccent = Color.FromArgb(255, 230, 120);
        t.BorderColor = Color.FromArgb(200, 200, 200);
        t.FocusBorderColor = Color.Yellow;
        t.BorderThickness = 1;
        t.HighlightColor = Color.Yellow;
        t.EqualizerBarActive = Color.Yellow;
        t.OscilloscopeLine = Color.Cyan;
        t.WaveformPrimary = Color.Cyan;
        return t;
    }
}
