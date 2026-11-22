using AudioForge.UI.Services;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow.Ribbons;

/// <summary>
/// CLAP plugin tab: loads .clap files through the engine's CLAP host,
/// lists them, and inserts enabled plugins into the playback chain via
/// <see cref="PluginService"/>.
/// </summary>
public partial class PluginsRibbonControl : RibbonStripControl
{
    private static readonly Color BgDark = Color.FromArgb(24, 24, 24);
    private static readonly Color PanelDark = Color.FromArgb(32, 32, 32);
    private static readonly Color TextColor = Color.FromArgb(212, 212, 212);
    private static readonly Color DimText = Color.FromArgb(140, 140, 140);

    private static string PluginDir => Path.Combine(AppContext.BaseDirectory, "clap-plugins");

    private readonly Panel _tabHost = new() { Dock = DockStyle.Fill };
    private readonly ListView _list = new();
    private readonly Label _status = new();

    public override Control? TabContent => _tabHost;

    public PluginsRibbonControl()
    {
        InitializeComponent();
        BuildRibbon();
        BuildContent();
        ScanPluginDir();
    }

    private void BuildRibbon()
    {
        var menu = new ContextMenuStrip();
        AddItem(menu, "Add Plugin File…", AddPluginFile);
        AddItem(menu, "Scan Plugins Folder", ScanPluginDir);
        menu.Items.Add(new ToolStripSeparator());
        AddItem(menu, "Remove Selected", RemoveSelected);
        AddItem(menu, "Disable All", () => SetAllEnabled(false));
        AddItem(menu, "Enable All", () => SetAllEnabled(true));
        menu.Items.Add(new ToolStripSeparator());
        AddItem(menu, "Clear All Plugins", ClearAll);

        var group = new RibbonGroupPanel("Plugins") { ContextMenuStrip = menu };
        group.AddButton("Plugins", menu);
        group.Dock = DockStyle.Left;
        group.Caption = string.Empty;
        RibbonPanel.Controls.Add(group);

        RibbonPanel.Controls.Add(new Label
        {
            Text = "CLAP Plugins",
            Dock = DockStyle.Bottom,
            Height = 13,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(51, 51, 55),
            Font = new Font("Segoe UI", 7.5F)
        });
    }

    private static void AddItem(ContextMenuStrip menu, string text, Action handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) => handler();
        menu.Items.Add(item);
    }

    private void BuildContent()
    {
        _tabHost.BackColor = BgDark;

        var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = PanelDark };
        header.Controls.Add(new Label
        {
            Text = "CLAP Plugins — processed after the EQ, before output. Tick = enabled.",
            AutoSize = true, ForeColor = DimText, Location = new Point(12, 14),
            Font = new Font("Segoe UI", 8.5F)
        });

        _status.Dock = DockStyle.Bottom;
        _status.Height = 24;
        _status.BackColor = PanelDark;
        _status.ForeColor = DimText;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(8, 0, 0, 0);

        _list.Dock = DockStyle.Fill;
        _list.BackColor = Color.FromArgb(30, 30, 32);
        _list.ForeColor = TextColor;
        _list.BorderStyle = BorderStyle.None;
        _list.FullRowSelect = true;
        _list.CheckBoxes = true;
        _list.View = View.Details;
        _list.Columns.Add("Plugin", 240);
        _list.Columns.Add("Vendor", 140);
        _list.Columns.Add("File", 340);
        _list.Columns.Add("Channels", 80, HorizontalAlignment.Right);
        _list.ItemCheck += List_ItemCheck;

        _tabHost.Controls.Add(_list);
        _tabHost.Controls.Add(header);
        _tabHost.Controls.Add(_status);
    }

    private void List_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        // Defer: Checked isn't updated until after the event.
        BeginInvoke(() =>
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            if (_list.Items[e.Index].Tag is PluginEntry entry)
                PluginService.Instance.SetEnabled(entry, _list.Items[e.Index].Checked);
        });
    }

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in PluginService.Instance.Plugins)
        {
            var (inCh, outCh) = entry.Node.Plugin.GetMainPortChannels();
            var item = new ListViewItem(entry.Node.Name)
            {
                Tag = entry,
                Checked = entry.Enabled
            };
            item.SubItems.Add(entry.Node.Plugin.Vendor);
            item.SubItems.Add(entry.Path);
            item.SubItems.Add($"{inCh}->{outCh}");
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        int enabled = PluginService.Instance.Plugins.Count(p => p.Enabled);
        _status.Text = _list.Items.Count == 0
            ? $"No plugins loaded. Drop .clap files in {PluginDir} or use Plugins > Add Plugin File."
            : $"{_list.Items.Count} plugin(s), {enabled} enabled";
    }

    private void ScanPluginDir()
    {
        int added = 0;
        try
        {
            Directory.CreateDirectory(PluginDir);
            var existing = PluginService.Instance.Plugins.Select(p => p.Path).ToHashSet(
                StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(PluginDir, "*.clap"))
            {
                if (existing.Contains(file)) continue;
                try
                {
                    PluginService.Instance.AddPlugin(file);
                    added++;
                }
                catch { /* skip unloadable file */ }
            }
        }
        catch (Exception ex) { _status.Text = $"Scan failed: {ex.Message}"; }
        RefreshList();
        if (added > 0) _status.Text = $"Loaded {added} plugin(s) from {PluginDir}";
    }

    private void AddPluginFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select CLAP plugin",
            Filter = "CLAP plugins (*.clap)|*.clap|All files (*.*)|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        int added = 0;
        foreach (var file in dlg.FileNames)
        {
            try
            {
                PluginService.Instance.AddPlugin(file);
                added++;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load '{Path.GetFileName(file)}':\n{ex.Message}",
                    "Plugins", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        RefreshList();
        if (added > 0) _status.Text = $"Added {added} plugin(s)";
    }

    private void RemoveSelected()
    {
        foreach (ListViewItem item in _list.SelectedItems)
            if (item.Tag is PluginEntry entry)
                PluginService.Instance.RemovePlugin(entry);
        RefreshList();
    }

    private void ClearAll()
    {
        if (PluginService.Instance.Plugins.Count == 0) return;
        var result = MessageBox.Show("Remove all plugins from the chain?",
            "Clear Plugins", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        PluginService.Instance.Clear();
        RefreshList();
    }

    private void SetAllEnabled(bool enabled)
    {
        foreach (var entry in PluginService.Instance.Plugins)
            PluginService.Instance.SetEnabled(entry, enabled);
        RefreshList();
    }
}
