using System.Drawing;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow.Ribbons;

public partial class PluginsRibbonControl : RibbonStripControl
{
    public PluginsRibbonControl()
    {
        InitializeComponent();
        RibbonPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Plugins — under development",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(120, 120, 128),
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        });
    }
}
