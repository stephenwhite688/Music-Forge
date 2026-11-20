using AudioForge.UI.Services;
using AudioForge.UI.Visualiser;
using AudioForge.Visualizer.Contracts;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow.Ribbons;

public partial class VisualiserRibbonControl : RibbonStripControl
{
    private readonly VisualiserDisplayControl _display = new() { Dock = DockStyle.Fill };
    private readonly Panel _tabHost = new() { Dock = DockStyle.Fill };

    public override Control? TabContent => _tabHost;

    public VisualiserRibbonControl()
    {
        InitializeComponent();
        _tabHost.Controls.Add(_display);
        BuildRibbon();
    }

    private void BuildRibbon()
    {
        var visualiserGroup = BuildVisualiserGroup();
        var displayModeGroup = BuildDisplayModeGroup();
        var fftGroup = BuildFftGroup();
        var displayGroup = BuildDisplayGroup();
        var overlaysGroup = BuildOverlaysGroup();
        var toolsGroup = BuildToolsGroup();

        // WinForms docks later-added controls closer to the edge, so add in
        // reverse order to keep Visualiser leftmost and Tools rightmost.
        foreach (var group in new[] { toolsGroup, overlaysGroup, displayGroup, fftGroup, displayModeGroup, visualiserGroup })
        {
            group.Dock = DockStyle.Left;
            group.Caption = string.Empty;
            RibbonPanel.Controls.Add(group);
        }
    }

    private static void AddItem(ContextMenuStrip menu, string text, Action handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) => handler();
        menu.Items.Add(item);
    }

    private static void AddSeparator(ContextMenuStrip menu) => menu.Items.Add(new ToolStripSeparator());

    private RibbonGroupPanel BuildVisualiserGroup()
    {
        var menu = new ContextMenuStrip();
        AddItem(menu, "Start Visualiser", () => _display.Running = true);
        AddItem(menu, "Stop Visualiser", () => _display.Running = false);
        AddItem(menu, "Pause Visualiser", () => _display.Running = false);
        AddItem(menu, "Resume Visualiser", () => _display.Running = true);
        AddSeparator(menu);
        AddItem(menu, "Full Screen", () =>
        {
            var form = FindForm();
            if (form is not null) form.WindowState = FormWindowState.Maximized;
        });

        var group = new RibbonGroupPanel("Visualiser") { ContextMenuStrip = menu };
        group.AddButton("Visualiser", menu);
        return group;
    }

    private RibbonGroupPanel BuildDisplayModeGroup()
    {
        var menu = new ContextMenuStrip();
        AddItem(menu, "Spectrum Bars", () => SelectMode(VizMode.Bars));
        AddItem(menu, "Oscilloscope", () => SelectMode(VizMode.Oscilloscope));
        AddItem(menu, "FFT Spectrum", () => SelectMode(VizMode.Spectrum));
        AddItem(menu, "Spectrogram", () => SelectMode(VizMode.Spectrogram));
        AddItem(menu, "Waveform", () => SelectMode(VizMode.Waveform));
        AddItem(menu, "Loudness Meters", () => SelectMode(VizMode.Loudness));

        // Plugin effects - everything Winamp-style lives here
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "visualisers");
        var plugins = VisualPluginLoader.Load(pluginDir);
        if (plugins.Count > 0)
        {
            AddSeparator(menu);
            foreach (var cat in plugins.GroupBy(p => p.Category).OrderBy(g => g.Key))
            {
                var catItem = new ToolStripMenuItem(cat.Key);
                foreach (var p in cat)
                {
                    var plugin = p;
                    var item = new ToolStripMenuItem(p.Name) { ToolTipText = p.Description };
                    item.Click += (s, e) =>
                    {
                        _display.Plugin = plugin;
                        _display.Mode = VizMode.Bars; // built-in mode retained underneath
                    };
                    catItem.DropDownItems.Add(item);
                }
                menu.Items.Add(catItem);
            }
            AddItem(menu, "Exit Plugin Mode", () => _display.Plugin = null);
        }

        AddSeparator(menu);
        AddItem(menu, "31 Bands", () => _display.BandCount = 31);
        AddItem(menu, "64 Bands", () => _display.BandCount = 64);
        AddItem(menu, "128 Bands", () => _display.BandCount = 128);

        var group = new RibbonGroupPanel("Display Mode") { ContextMenuStrip = menu };
        group.AddButton("Display Mode", menu);
        return group;
    }

    private void SelectMode(VizMode mode)
    {
        _display.Plugin = null;
        _display.Mode = mode;
    }

    private RibbonGroupPanel BuildFftGroup()
    {
        var menu = new ContextMenuStrip();
        AddItem(menu, "FFT 1024", () => SetFft(1024));
        AddItem(menu, "FFT 2048", () => SetFft(2048));
        AddItem(menu, "FFT 4096", () => SetFft(4096));
        AddItem(menu, "FFT 8192", () => SetFft(8192));
        AddSeparator(menu);
        AddItem(menu, "Hann", () => SetWindow(HelixAudio.DSP.Analysis.WindowType.Hann));
        AddItem(menu, "Hamming", () => SetWindow(HelixAudio.DSP.Analysis.WindowType.Hamming));
        AddItem(menu, "Blackman-Harris", () => SetWindow(HelixAudio.DSP.Analysis.WindowType.BlackmanHarris));
        AddSeparator(menu);
        AddItem(menu, "Toggle Peak Hold", () => _display.PeakHoldEnabled = !_display.PeakHoldEnabled);
        AddItem(menu, "Toggle Log/Linear Freq", () => _display.LogFrequency = !_display.LogFrequency);

        var group = new RibbonGroupPanel("FFT") { ContextMenuStrip = menu };
        group.AddButton("FFT", menu);
        return group;
    }

    private static void SetFft(int size)
    {
        var m = MeterService.Instance;
        m.PreEq.SetFftParameters(size);
        m.PostEq.SetFftParameters(size);
    }

    private static void SetWindow(HelixAudio.DSP.Analysis.WindowType window)
    {
        var m = MeterService.Instance;
        // SetFftParameters keeps current size when window changes via re-set
        m.PreEq.SetFftParameters(m.PreEq.FftSize, window);
        m.PostEq.SetFftParameters(m.PostEq.FftSize, window);
    }

    private RibbonGroupPanel BuildDisplayGroup()
    {
        var menu = new ContextMenuStrip();
        AddItem(menu, "Dark", () => _display.Theme = VizTheme.Dark);
        AddItem(menu, "High Contrast", () => _display.Theme = VizTheme.HighContrast);
        AddItem(menu, "Heat", () => _display.Theme = VizTheme.Heat);
        AddItem(menu, "Neon", () => _display.Theme = VizTheme.Neon);
        AddItem(menu, "Grayscale", () => _display.Theme = VizTheme.Grayscale);

        var group = new RibbonGroupPanel("Display") { ContextMenuStrip = menu };
        group.AddButton("Display", menu);
        return group;
    }

    private RibbonGroupPanel BuildOverlaysGroup()
    {
        var menu = new ContextMenuStrip();
        AddItem(menu, "Toggle Overlays", () => _display.ShowOverlays = !_display.ShowOverlays);
        AddItem(menu, "Meters + True Peak", () => _display.Mode = VizMode.Loudness);

        var group = new RibbonGroupPanel("Overlays") { ContextMenuStrip = menu };
        group.AddButton("Overlays", menu);
        return group;
    }

    private RibbonGroupPanel BuildToolsGroup()
    {
        var menu = new ContextMenuStrip();
        AddItem(menu, "Export Image…", ExportImage);
        AddItem(menu, "Copy Settings", CopySettings);
        AddItem(menu, "About Visualiser", () =>
            MessageBox.Show(
                "AudioForge Visualiser\n\nRealtime engine taps: spectrum (FFT), waveform, spectrogram, level/true-peak meters.\nAll data comes from the live HelixAudio playback chain.",
                "Visualiser", MessageBoxButtons.OK, MessageBoxIcon.Information));

        var group = new RibbonGroupPanel("Tools") { ContextMenuStrip = menu };
        group.AddButton("Tools", menu);
        return group;
    }

    private void ExportImage()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Export visualiser image",
            Filter = "PNG image (*.png)|*.png",
            FileName = $"visualiser-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            using var bmp = new Bitmap(_display.Width, _display.Height);
            _display.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
            MessageBox.Show($"Saved:\n{dlg.FileName}", "Visualiser",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n{ex.Message}", "Visualiser",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopySettings()
    {
        var text = $"Mode={_display.Mode} Theme={_display.Theme} Bands={_display.BandCount} " +
                   $"LogFreq={_display.LogFrequency} PeakHold={_display.PeakHoldEnabled} Overlays={_display.ShowOverlays}";
        Clipboard.SetText(text);
        MessageBox.Show($"Copied:\n{text}", "Visualiser",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
