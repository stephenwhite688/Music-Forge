using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AudioForge.UI.Shared;
using AudioForge.UI.MainWindow.Ribbons;

namespace AudioForge.UI.MainWindow;

public partial class MainForm : Form
{
    /// <summary>The docked transport bar - used to show long-operation progress.</summary>
    public AudioForge.UI.TransportBar.TransportBarControl TransportBar => _dockedTransportBar;

    public MainForm()
    {
        InitializeComponent();
        AttachTabContents();
        LoadApplicationIcon();

        ThemeManager.ThemeChanged += ApplyActiveTheme;
        ApplyActiveTheme();
    }

    // Tabs can expose extra content (e.g. the EQ display) via RibbonStripControl.TabContent.
    // It is docked fill inside the tab page beneath the ribbon strip.
    private void AttachTabContents()
    {
        foreach (TabPage page in _mainTabControl.TabPages)
        {
            RibbonStripControl? ribbon = null;
            foreach (Control control in page.Controls)
            {
                if (control is RibbonStripControl strip)
                {
                    ribbon = strip;
                    break;
                }
            }

            if (ribbon?.TabContent is { } content)
            {
                content.Dock = DockStyle.Fill;
                page.Controls.Add(content);
                // Keep the ribbon processed first so it stays docked at the top edge
                page.Controls.SetChildIndex(content, 0);
            }
        }
    }

    private void LoadApplicationIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Main", "audioforge.ico");
            if (File.Exists(iconPath))
                this.Icon = new Icon(iconPath);
        }
        catch
        {
            // Icon is cosmetic - never block startup over it
        }
    }

    private void ApplyActiveTheme()
    {
        this.BackColor = ThemeManager.Current.WindowBackground;
        mainContentPanel.BackColor = ThemeManager.Current.ContainerBackground;
        _dockedTransportBar.BackColor = ThemeManager.Current.ControlDefaultSurface;

        float size = ThemeManager.Current.FontSizeStandard > 0 ? ThemeManager.Current.FontSizeStandard : 9F;
        Font dynamicFont = new Font(ThemeManager.Current.FontName, size);
        _mainTabControl.Font = dynamicFont;

        foreach (TabPage tab in _mainTabControl.TabPages)
        {
            tab.BackColor = ThemeManager.Current.ContainerBackground;
            tab.ForeColor = ThemeManager.Current.TextPrimary;

            foreach (Control control in tab.Controls)
            {
                control.BackColor = ThemeManager.Current.ControlDefaultSurface;
            }
        }
    }
}
