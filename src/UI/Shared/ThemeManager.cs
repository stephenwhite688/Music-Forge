// ==========================================
// FILE: ThemeManager.cs
// BLOCK: 1 OF 1 (Dynamic Runtime Engine)
// ==========================================

using System;
using System.Drawing;

namespace AudioForge.UI.Shared;

public static class ThemeManager
{
    // The live configuration that your UI components look at
    public static ThemeModel Current { get; private set; } = LoadBlankTemplate();

    // Notifies your TransportBar items to instantly redraw when an option changes
    public static event Action? ThemeChanged;

    public static void ApplyCustomTheme(ThemeModel customTheme)
    {
        Current = customTheme ?? throw new ArgumentNullException(nameof(customTheme));
        ThemeChanged?.Invoke();
    }

    public static void UpdateSingleColor(Action<ThemeModel> updateAction)
    {
        // Allows your Options menu sliders/pickers to modify one color at a time dynamically
        updateAction(Current);
        ThemeChanged?.Invoke();
    }

    private static ThemeModel LoadBlankTemplate()
    {
        return new ThemeModel
        {
            WindowBackground = Color.FromArgb(18, 18, 18),
            ContainerBackground = Color.FromArgb(28, 28, 28),
            ControlDefaultSurface = Color.FromArgb(40, 40, 40),
            ControlHoverSurface = Color.FromArgb(55, 55, 55),
            ControlPressedSurface = Color.FromArgb(70, 70, 70),
            ControlDisabledSurface = Color.FromArgb(60, 60, 60),
            PrimaryAccent = Color.FromArgb(10, 71, 138),
            SecondaryAccent = Color.FromArgb(0, 31, 77),
            HighlightColor = Color.FromArgb(80, 160, 230),
            BorderColor = Color.FromArgb(80, 80, 80),
            FocusBorderColor = Color.FromArgb(100, 180, 255),
            BorderThickness = 0,
            CornerRadius = 0,
            TextPrimary = Color.FromArgb(240, 240, 240),
            TextSecondary = Color.FromArgb(181, 226, 255),
            TextDisabled = Color.FromArgb(130, 130, 130),
            TextAccent = Color.FromArgb(223, 244, 255),
            EqualizerBarDefault = Color.FromArgb(10, 71, 138),
            EqualizerBarActive = Color.FromArgb(80, 160, 230),
            OscilloscopeLine = Color.FromArgb(80, 160, 230),
            OscilloscopeBackground = Color.FromArgb(0, 31, 77),
            WaveformPrimary = Color.FromArgb(80, 160, 230),
            WaveformSecondary = Color.FromArgb(181, 226, 255),
            VisualizerSpacing = 2,
            SliderTrackBackground = Color.FromArgb(55, 55, 55),
            SliderTrackProgress = Color.FromArgb(80, 160, 230),
            SliderThumbDefault = Color.FromArgb(223, 244, 255),
            SliderThumbHover = Color.White,
            SliderThumbSize = 10,
            TabActiveBackground = Color.FromArgb(40, 40, 40),
            TabInactiveBackground = Color.FromArgb(28, 28, 28),
            MenuDropdownBackground = Color.FromArgb(40, 40, 40),
            MenuDropdownHover = Color.FromArgb(55, 55, 55)
        };
    }
}
