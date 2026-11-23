namespace MusicForge.UI.Shared;

/// <summary>
/// UI-level performance switches. Low power mode tells cheap-to-pause visual
/// surfaces (mini visualiser, spectrum/waveform overlays) to stop rendering.
/// </summary>
public static class UiPerformance
{
    public enum Mode { Low, Balanced, High }

    public static Mode Current { get; private set; } = Mode.Balanced;

    /// <summary>True when decorative rendering should be skipped.</summary>
    public static bool LowPower => Current == Mode.Low;

    public static void Set(Mode mode) => Current = mode;
}
