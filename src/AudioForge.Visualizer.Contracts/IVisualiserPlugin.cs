using System.Drawing;

namespace AudioForge.Visualizer.Contracts;

/// <summary>
/// A visualiser effect plugin. Implementations are discovered by scanning a
/// folder for assemblies containing types that implement this interface.
/// Plugins render into the provided <see cref="Graphics"/> using only the
/// data in <see cref="VisualFrame"/> - no engine access required.
/// </summary>
public interface IVisualiserPlugin
{
    /// <summary>Display name shown in the visualiser menu.</summary>
    string Name { get; }

    /// <summary>Grouping category (e.g. "Classic", "Modern", "Retro").</summary>
    string Category { get; }

    /// <summary>One-line description shown in tooltips/menus.</summary>
    string Description { get; }

    /// <summary>
    /// Draw one frame. Called on the UI thread at the display refresh rate.
    /// Keep it fast - target &lt;8 ms per frame at 1080p.
    /// </summary>
    void Draw(Graphics graphics, VisualFrame frame);

    /// <summary>Called when the plugin is activated. Use for one-time setup.</summary>
    void OnActivated() { }

    /// <summary>Called when the plugin is deactivated. Use for cleanup.</summary>
    void OnDeactivated() { }
}
