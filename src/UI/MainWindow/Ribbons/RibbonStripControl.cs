using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicForge.UI.MainWindow.Ribbons;

/// <summary>
/// Base class for tab ribbon strips. Provides a 55px-high docked strip panel
/// that ribbon groups and buttons can be added to via <see cref="RibbonPanel"/>.
/// </summary>
public partial class RibbonStripControl : UserControl
{
    private readonly Panel _stripPanel;

    public RibbonStripControl()
    {
        InitializeComponent();

        _stripPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48),
            AutoScroll = true
        };

        BackColor = Color.FromArgb(45, 45, 48);
        MaximumSize = new Size(0, 55);
        MinimumSize = new Size(0, 55);
        Height = 55;

        Controls.Add(_stripPanel);
    }

    /// <summary>The surface ribbon groups and buttons are added to.</summary>
    public Panel RibbonPanel => _stripPanel;

    /// <summary>
    /// Optional content shown in the tab page below this ribbon
    /// (docked fill). Override in tabs that have a main display area.
    /// </summary>
    public virtual Control? TabContent => null;

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _stripPanel.BackColor = BackColor;
    }
}
