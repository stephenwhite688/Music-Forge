// ==========================================
// FILE: ThemeModel.cs
// BLOCK: 1 OF 1 (Complete UI Layout Grid Slots)
// ==========================================

using System.Drawing;

namespace MusicForge.UI.Shared;

public class ThemeModel
{
    // --- GLOBAL SURFACE COLORS ---
    public Color WindowBackground { get; set; }
    public Color ContainerBackground { get; set; }
    public Color ControlDefaultSurface { get; set; }
    public Color ControlHoverSurface { get; set; }
    public Color ControlPressedSurface { get; set; }
    public Color ControlDisabledSurface { get; set; }

    // --- ACCENTS, BORDERS & SHAPES ---
    public Color PrimaryAccent { get; set; }
    public Color SecondaryAccent { get; set; }
    public Color HighlightColor { get; set; }
    public Color BorderColor { get; set; }
    public Color FocusBorderColor { get; set; }
    public int BorderThickness { get; set; }
    public int CornerRadius { get; set; }

    // --- TYPOGRAPHY COLORS ---
    public Color TextPrimary { get; set; }
    public Color TextSecondary { get; set; }
    public Color TextDisabled { get; set; }
    public Color TextAccent { get; set; }

    // --- DYNAMIC FONTS (Adjustable from Options) ---
    public string FontName { get; set; } = "Segoe UI";
    public float FontSizeStandard { get; set; } = 9F;
    public float FontSizeHeader { get; set; } = 12F;
    public float FontSizeDigital { get; set; } = 10F;
    public bool FontAntiAliasing { get; set; }

    // --- AUDIO SPECIFIC ENGINE GRAPHICS ---
    public Color EqualizerBarDefault { get; set; }
    public Color EqualizerBarActive { get; set; }
    public Color OscilloscopeLine { get; set; }
    public Color OscilloscopeBackground { get; set; }
    public Color WaveformPrimary { get; set; }
    public Color WaveformSecondary { get; set; }
    public int VisualizerSpacing { get; set; }

    // --- SLIDERS & INTERACTIVE KNOBS ---
    public Color SliderTrackBackground { get; set; }
    public Color SliderTrackProgress { get; set; }
    public Color SliderThumbDefault { get; set; }
    public Color SliderThumbHover { get; set; }
    public int SliderThumbSize { get; set; }

    // --- TABS & MENUS ---
    public Color TabActiveBackground { get; set; }
    public Color TabInactiveBackground { get; set; }
    public Color MenuDropdownBackground { get; set; }
    public Color MenuDropdownHover { get; set; }

    // --- GLOBAL UX BEHAVIOUR OPTIONS ---
    public bool GlobalToolTipsEnabled { get; set; } = true;
    public int ToolTipInitialDelayMs { get; set; } = 500;
    public int ToolTipAutoPopDelayMs { get; set; } = 5000;
}
// ==========================================
// END OF FILE: ThemeModel.cs
// ==========================================
