using SpeakerForge;
using AudioForge.UI.MainWindow.NetworkSpeakers;
using AudioForge.UI.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow.Ribbons;

public partial class NetworkedSpeakersRibbonControl : RibbonStripControl
{
    private static NetworkSpeakerService Engine => NetworkSpeakersService.Instance.Engine;
    private static NetworkSpeakersService Ui => NetworkSpeakersService.Instance;

    private readonly NetworkSpeakersPanel _panel = new();

    public NetworkedSpeakersRibbonControl()
    {
        InitializeComponent();
        BuildRibbon();
    }

    public override System.Windows.Forms.Control? TabContent => _panel;

    private void BuildRibbon()
    {
        var devicesGroup = BuildDevicesGroup();
        var playbackGroup = BuildPlaybackGroup();

        foreach (var group in new[] { playbackGroup, devicesGroup })
        {
            group.Dock = DockStyle.Left;
            RibbonPanel.Controls.Add(group);
        }
    }

    private static ContextMenuStrip Menu(params (string Text, Func<Task>? Action)[] items)
    {
        var menu = new ContextMenuStrip();
        foreach (var (text, action) in items)
        {
            var mi = new ToolStripMenuItem(text);
            if (action is not null)
                mi.Click += async (_, _) => await RunAsync(action);
            menu.Items.Add(mi);
        }
        return menu;
    }

    private static async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Network Speakers", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private static void NeedSelection(NetworkSpeakersPanel panel)
    {
        if (panel.TickedDevices().Count == 0 && !panel.LocalTicked)
            MessageBox.Show("Tick at least one speaker (or Local) in the list below first.");
    }

    private RibbonGroupPanel BuildDevicesGroup()
    {
        var menu = Menu(
            ("Scan Network", async () =>
            {
                Ui.EnsureServerRunning();
                var found = await Task.Run(() => Engine.DiscoverAsync(3000));
                Ui.NotifyChanged();
                if (found.Count == 0)
                    MessageBox.Show("No UPnP renderers found.");
            }),
            ("Refresh Devices", () => { Ui.NotifyChanged(); return Task.CompletedTask; }),
            ("Stop All Playback", async () => { await _panel.StopAllAsync(); }));
        var group = new RibbonGroupPanel("") { ContextMenuStrip = menu };
        group.AddButton("Devices", menu);
        return group;
    }

    private RibbonGroupPanel BuildPlaybackGroup()
    {
        var menu = Menu(
            ("Play Selected Speakers", async () =>
            {
                NeedSelection(_panel);
                await _panel.PlaySelectedAsync();
            }),
            ("Local + Selected Speakers", async () =>
            {
                NeedSelection(_panel);
                await _panel.PlayLocalAndSelectedAsync();
            }),
            ("Play All Speakers", async () => await _panel.PlayAllAsync()),
            ("Play Local Only", () => { _panel.PlayLocalOnly(); return Task.CompletedTask; }),
            ("", null),
            ("Stop All Speakers", async () => await _panel.StopAllAsync()));
        var group = new RibbonGroupPanel("") { ContextMenuStrip = menu };
        group.AddButton("Playback", menu);
        return group;
    }
}