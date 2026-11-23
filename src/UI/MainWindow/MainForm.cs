using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MusicForge.UI.Shared;
using MusicForge.UI.MainWindow.Ribbons;

namespace MusicForge.UI.MainWindow;

public partial class MainForm : Form
{
    /// <summary>The docked transport bar - used to show long-operation progress.</summary>
    public MusicForge.UI.TransportBar.TransportBarControl TransportBar => _dockedTransportBar;

    /// <summary>Main tab control - used by Options to switch tabs and persist startup tab.</summary>
    public TabControl MainTabs => _mainTabControl;

    public MainForm()
    {
        InitializeComponent();
        AttachTabContents();
        LoadApplicationIcon();
        SelectStartupTab();
        _mainTabControl.SelectedIndexChanged += (_, _) => PersistLastTab();

        ThemeManager.ThemeChanged += ApplyActiveTheme;
        ApplyActiveTheme();
    }

    private void SelectStartupTab()
    {
        // A fixed startup tab (StartupMode) wins; otherwise reopen the last-used tab.
        string? last = null;
        try
        {
            var settings = new MusicForge.UI.Data.SettingsRepository();
            last = settings.Get("StartupMode") ?? settings.Get("LastOpenedMode");
        }
        catch { /* settings unavailable - default tab */ }

        if (!string.IsNullOrEmpty(last))
        {
            foreach (TabPage page in _mainTabControl.TabPages)
            {
                if (string.Equals(page.Text, last, StringComparison.OrdinalIgnoreCase))
                {
                    _mainTabControl.SelectedTab = page;
                    return;
                }
            }
        }
        _mainTabControl.SelectedIndex = 0;
    }

    private void PersistLastTab()
    {
        try
        {
            if (_mainTabControl.SelectedTab is { } tab)
                new MusicForge.UI.Data.SettingsRepository().Set("LastOpenedMode", tab.Text);
        }
        catch { /* never let settings persistence break tab switching */ }
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
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Main", "musicforge.ico");
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

        // Cascade the theme through every child control (buttons, lists, text, etc.)
        ThemeApplier.Apply(this);
        ThemeApplier.Apply(_dockedTransportBar);
    }
}
