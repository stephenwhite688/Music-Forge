using SpeakerForge;
using AudioForge.UI.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow.NetworkSpeakers;

/// <summary>
/// Content panel for the Networked Speakers tab: one list of output targets
/// (local PC pinned on top + discovered renderers) with tick boxes. Ticking
/// any number of speakers IS the group - there are no group objects.
/// Playback routing lives in the ribbon's Playback menu.
/// </summary>
public sealed class NetworkSpeakersPanel : UserControl
{
    private static readonly object LocalTag = new();

    private readonly NetworkSpeakersService _svc = NetworkSpeakersService.Instance;

    private readonly Label _serverLabel = new() { AutoSize = true };
    private readonly Button _btnScan = new() { Text = "Scan Network", Width = 100 };
    private readonly TextBox _txtHost = new() { Width = 140, PlaceholderText = "speaker IP / host" };
    private readonly Button _btnAddHost = new() { Text = "Add", Width = 60 };

    private readonly ListView _lvDevices = new()
    {
        View = View.Details, FullRowSelect = true, MultiSelect = true,
        CheckBoxes = true, HideSelection = false,
        Dock = DockStyle.Fill
    };

    private readonly ComboBox _cmbMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly TrackBar _volSlider = new() { Width = 140, TickStyle = TickStyle.None };
    private readonly Label _volLabel = new() { AutoSize = true, Text = "Vol" };
    private readonly CheckBox _chkSystemAudio = new()
    {
        Text = "System audio (Spotify etc.)",
        AutoSize = true,
        ForeColor = Color.Gainsboro
    };

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private bool _refreshing;
    private bool _autoScanned;
    private bool _togglingSystemAudio;
    private float? _savedLocalVolume;

    public NetworkSpeakersPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 34);
        ForeColor = Color.Gainsboro;

        _lvDevices.Columns.Add("Name", 240);
        _lvDevices.Columns.Add("Manufacturer / Model", 200);
        _lvDevices.Columns.Add("Address", 160);

        _cmbMode.Items.AddRange(new object[] { "Stereo", "Stereo Pair (L/R)", "Left", "Right" });
        _cmbMode.SelectedIndex = 0;
        _volSlider.Minimum = 0; _volSlider.Maximum = 100; _volSlider.Value = 50;

        BuildLayout();
        WireEvents();

        _timer.Tick += (_, _) => RefreshLists();
        _timer.Start();
        _svc.Changed += (_, _) => BeginInvoke(new Action(RefreshLists));
        SystemAudioTapService.Instance.Changed += (_, _) => BeginInvoke(new Action(RefreshLists));
        VisibleChanged += async (_, e) =>
        {
            if (!Visible) return;
            RefreshLists();
            if (!_autoScanned && _svc.Engine.Devices.Count == 0)
            {
                _autoScanned = true;
                await ScanAsync();
            }
        };
    }

    private void BuildLayout()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(6, 4, 6, 0) };
        top.Controls.Add(_serverLabel);
        top.Controls.Add(_btnScan);
        top.Controls.Add(_txtHost);
        top.Controls.Add(_btnAddHost);
        _serverLabel.SetBounds(6, 8, 320, 20);
        _btnScan.SetBounds(330, 4, 100, 26);
        _txtHost.SetBounds(436, 6, 140, 23);
        _btnAddHost.SetBounds(582, 4, 60, 26);
        _chkSystemAudio.SetBounds(650, 8, 220, 22);
        _chkSystemAudio.CheckedChanged += (_, _) => ToggleSystemAudio();

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 34 };
        bottom.Controls.AddRange(new Control[] { _cmbMode, _volLabel, _volSlider });
        _cmbMode.SetBounds(6, 5, 150, 24);
        _volLabel.SetBounds(300, 8, 30, 20);
        _volSlider.SetBounds(332, 5, 160, 24);
        var hint = new Label
        {
            Text = "Tick speakers = your group. Play controls are in the ribbon Playback menu.",
            AutoSize = true, ForeColor = Color.FromArgb(110, 110, 118),
            Location = new Point(510, 10)
        };
        bottom.Controls.Add(hint);

        Controls.Add(_lvDevices);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private void WireEvents()
    {
        _btnScan.Click += async (_, _) => await ScanAsync();
        _btnAddHost.Click += async (_, _) => await AddHostAsync();
        _volSlider.ValueChanged += async (_, _) =>
        {
            var dev = TickedDevices().FirstOrDefault();
            if (dev is not null)
                await _svc.Engine.SetDeviceVolumeAsync(dev.Id, _volSlider.Value);
        };
    }

    // ---------------- public API for the ribbon ----------------

    /// <summary>Ticked (or, if none ticked, highlighted) network speakers.</summary>
    public IReadOnlyList<INetworkRenderer> TickedDevices()
    {
        var items = _lvDevices.CheckedItems.Count > 0
            ? _lvDevices.CheckedItems.Cast<ListViewItem>()
            : _lvDevices.SelectedItems.Cast<ListViewItem>();
        return items.Select(i => i.Tag).OfType<INetworkRenderer>().ToList();
    }

    /// <summary>True when the pinned "Local" row is ticked.</summary>
    public bool LocalTicked =>
        _lvDevices.Items.Count > 0 && _lvDevices.Items[0].Tag == LocalTag && _lvDevices.Items[0].Checked;

    /// <summary>All discovered network devices (for "play all").</summary>
    public IReadOnlyList<INetworkRenderer> AllDevices() =>
        _svc.Engine.Devices.Values.OrderBy(d => d.FriendlyName).ToList();

    /// <summary>Applies the selected mode to a set of devices and plays them.</summary>
    private async Task PlayDevicesAsync(IReadOnlyList<INetworkRenderer> devices, bool includeLocal)
    {
        _svc.EnsureServerRunning();
        // Stop everything first - otherwise previously-playing speakers keep
        // streaming when they're not in the new selection.
        await _svc.Engine.StopAllAsync();
        var mode = _cmbMode.SelectedIndex;
        var tasks = new List<Task<bool>>();

        if (mode == 1 && devices.Count >= 2) // Stereo Pair: first=L, second=R, rest=stereo
        {
            tasks.Add(_svc.Engine.PlayToDeviceAsync(devices[0].Id, SpeakerAssignment.Left));
            tasks.Add(_svc.Engine.PlayToDeviceAsync(devices[1].Id, SpeakerAssignment.Right));
            foreach (var d in devices.Skip(2))
                tasks.Add(_svc.Engine.PlayToDeviceAsync(d.Id, SpeakerAssignment.Stereo));
        }
        else
        {
            var role = mode switch { 2 => SpeakerAssignment.Left, 3 => SpeakerAssignment.Right, _ => SpeakerAssignment.Stereo };
            foreach (var d in devices)
                tasks.Add(_svc.Engine.PlayToDeviceAsync(d.Id, role));
        }
        await Task.WhenAll(tasks);
        SetLocalOutput(includeLocal);
    }

    /// <summary>Plays the ticked speakers (plus local if its row is ticked).</summary>
    public Task PlaySelectedAsync() =>
        PlayDevicesAsync(TickedDevices(), LocalTicked);

    /// <summary>Plays every discovered speaker in stereo, local included.</summary>
    public Task PlayAllAsync() =>
        PlayDevicesAsync(AllDevices(), includeLocal: true);

    /// <summary>Local output only - disables the network feed and restores volume.</summary>
    public void PlayLocalOnly()
    {
        _svc.BroadcastNode.Enabled = false;
        RestoreLocalVolume();
    }

    /// <summary>Stops every renderer and restores local output.</summary>
    public async Task StopAllAsync()
    {
        await _svc.Engine.StopAllAsync();
        _svc.BroadcastNode.Enabled = !SystemAudioTapService.Instance.IsActive;
        RestoreLocalVolume();
    }

    /// <summary>Local+network: unmute local, feed the ticked set.</summary>
    public Task PlayLocalAndSelectedAsync() =>
        PlayDevicesAsync(TickedDevices(), includeLocal: true);

    private void SetLocalOutput(bool includeLocal)
    {
        var pb = PlaybackService.Instance;
        var systemAudioOwnsFeed = SystemAudioTapService.Instance.IsActive;
        if (includeLocal)
        {
            _svc.BroadcastNode.Enabled = !systemAudioOwnsFeed;
            RestoreLocalVolume();
        }
        else
        {
            _svc.BroadcastNode.Enabled = !systemAudioOwnsFeed;
            if (_savedLocalVolume is null) { _savedLocalVolume = pb.Volume; pb.SetVolume(0f); }
        }
    }

    private void RestoreLocalVolume()
    {
        if (_savedLocalVolume is not { } v) return;
        PlaybackService.Instance.SetVolume(v);
        _savedLocalVolume = null;
    }

    private void ToggleSystemAudio()
    {
        if (_togglingSystemAudio) return;
        _togglingSystemAudio = true;
        try
        {
            var tap = SystemAudioTapService.Instance;
            if (_chkSystemAudio.Checked) tap.Start();
            else tap.Stop();
        }
        catch (Exception ex)
        {
            _chkSystemAudio.Checked = false;
            MessageBox.Show($"System audio capture failed: {ex.Message}");
        }
        finally { _togglingSystemAudio = false; }
        RefreshLists();
    }

    // ---------------- internals ----------------

    private async Task ScanAsync()
    {
        _btnScan.Enabled = false;
        _btnScan.Text = "Scanning...";
        try
        {
            _svc.EnsureServerRunning();
            var found = await Task.Run(() => _svc.Engine.DiscoverAsync(3000));
            _svc.NotifyChanged();
            if (found.Count == 0)
                MessageBox.Show("No UPnP renderers found. Make sure the speakers are on the same network.");
        }
        catch (Exception ex) { MessageBox.Show($"Scan failed: {ex.Message}"); }
        finally { _btnScan.Enabled = true; _btnScan.Text = "Scan Network"; RefreshLists(); }
    }

    private async Task AddHostAsync()
    {
        var host = _txtHost.Text.Trim();
        if (host.Length == 0) return;
        _svc.EnsureServerRunning();
        var dev = await _svc.Engine.AddDeviceByAddressAsync(host);
        if (dev is null) MessageBox.Show($"No UPnP renderer answered at {host}.");
        else _svc.NotifyChanged();
        _txtHost.Clear();
        RefreshLists();
    }

    private void RefreshLists()
    {
        if (_refreshing || IsDisposed) return;
        _refreshing = true;
        try
        {
            var tap = SystemAudioTapService.Instance;
            _serverLabel.Text = $"Server: {_svc.Engine.Server.LocalUrl ?? "not running"}  •  {_svc.Engine.Server.ConnectedClients} stream listener(s)"
                + (tap.IsActive ? $"  •  SYSTEM AUDIO → network ({tap.DeviceName})" : "");

            var checkedUdns = _lvDevices.CheckedItems.Cast<ListViewItem>()
                .Select(i => i.Tag).OfType<INetworkRenderer>().Select(d => d.Id).ToHashSet();
            var localChecked = _lvDevices.Items.Count > 0 && _lvDevices.Items[0].Tag == LocalTag && _lvDevices.Items[0].Checked;

            _lvDevices.BeginUpdate();
            _lvDevices.Items.Clear();

            // Pinned local-output row
            var local = new ListViewItem("Local (this PC)") { Tag = LocalTag, Checked = localChecked };
            local.SubItems.Add("WASAPI output");
            local.SubItems.Add("- ");
            _lvDevices.Items.Add(local);

            foreach (var d in _svc.Engine.Devices.Values.OrderBy(d => d.FriendlyName))
            {
                var host = d.Address;
                var item = new ListViewItem(d.FriendlyName) { Tag = d, Checked = checkedUdns.Contains(d.Id) };
                item.SubItems.Add($"{d.Manufacturer} {d.ModelName}".Trim());
                item.SubItems.Add(host);
                _lvDevices.Items.Add(item);
            }
            _lvDevices.EndUpdate();
        }
        finally { _refreshing = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Stop(); _timer.Dispose(); }
        base.Dispose(disposing);
    }
}
