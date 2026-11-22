using MusicForge.UI.Data;
using MusicForge.UI.Services;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicForge.UI.MainWindow.Ribbons;

public partial class InternetRadioRibbonControl : RibbonStripControl
{
    private readonly RadioBrowserService _radioService = new();
    private readonly RadioRepository _radio = new();
    private readonly RadioBrowserControl _radioBrowser = new();
    private readonly Panel _tabHost = new() { Dock = DockStyle.Fill };

    public override Control? TabContent => _tabHost;

    private bool _directoryLoading;
    private CancellationTokenSource? _loadCts;

    public InternetRadioRibbonControl()
    {
        InitializeComponent();
        BuildRibbon();
        _tabHost.Controls.Add(_radioBrowser);
    }

    private void BuildRibbon()
    {
        var directoryGroup = BuildDirectoryGroup();
        var searchGroup = BuildSearchGroup();
        var playbackGroup = BuildPlaybackGroup();
        var favouritesGroup = BuildFavouritesGroup();
        var informationGroup = BuildInformationGroup();

        // WinForms docks later-added controls closer to the edge, so add in
        // reverse order to keep Directory leftmost and Information rightmost.
        foreach (var group in new[] { informationGroup, favouritesGroup, playbackGroup, searchGroup, directoryGroup })
        {
            group.Dock = DockStyle.Left;
            group.Caption = string.Empty; // single ribbon-wide caption below
            RibbonPanel.Controls.Add(group);
        }

        RibbonPanel.Controls.Add(new Label
        {
            Text = "Internet Radio",
            Dock = DockStyle.Bottom,
            Height = 13,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(51, 51, 55),
            Font = new Font("Segoe UI", 7.5F)
        });
    }

    private static ContextMenuStrip BuildMenu(params string[] items) => RibbonMenuFactory.Create(items);

    private static void AddMenuItem(ContextMenuStrip menu, string text, Action handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) => handler();
        menu.Items.Add(item);
    }

    private static void AddStubItem(ContextMenuStrip menu, string text)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) =>
            MessageBox.Show($"'{text}' is not implemented yet.", "AudioForge",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        menu.Items.Add(item);
    }

    // ---------- Directory ----------

    private RibbonGroupPanel BuildDirectoryGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "Load Directory", LoadDirectory);
        AddMenuItem(menu, "Refresh Directory", LoadDirectory);
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Browse All Stations", () => _radioBrowser.ShowView("view:all"));
        AddMenuItem(menu, "Browse By Country", () => _radioBrowser.ShowView("view:countries"));
        AddMenuItem(menu, "Browse By Language", () => _radioBrowser.ShowView("view:languages"));
        AddMenuItem(menu, "Browse By Genre", () => _radioBrowser.ShowView("view:genres"));
        AddMenuItem(menu, "Popular Stations", () => _radioBrowser.ShowView("view:popular"));
        AddMenuItem(menu, "Recently Added Stations", () => _radioBrowser.ShowView("view:recent"));
        AddMenuItem(menu, "My Favourites", () => _radioBrowser.ShowView("view:favs"));

        var group = new RibbonGroupPanel("Directory") { ContextMenuStrip = menu };
        group.AddButton("Directory", menu);
        return group;
    }

    /// <summary>
    /// Downloads the Radio Browser station directory into the local SQLite
    /// cache. Progress is shown in the transport bar: "Loading…" in the track
    /// name slot, elapsed time in the current-time slot and the estimated
    /// total duration in the total-time slot.
    /// </summary>
    private async void LoadDirectory()
    {
        if (_directoryLoading)
        {
            MessageBox.Show("Directory load already in progress.", "Internet Radio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var form = FindForm() as MainForm;
        var transport = form?.TransportBar;
        _directoryLoading = true;
        _loadCts = new CancellationTokenSource();
        transport?.ShowOperationProgress("Loading radio directory…");

        int existing = _radioService.CachedStationCount;
        var progress = new Progress<RadioLoadProgress>(p =>
        {
            TimeSpan estimatedTotal = p.Fetched > 0 && p.EstimatedTotal > p.Fetched
                ? TimeSpan.FromTicks((long)(p.Elapsed.Ticks * (double)p.EstimatedTotal / p.Fetched))
                : TimeSpan.Zero;
            transport?.UpdateOperationProgress(p.Elapsed, estimatedTotal);
        });

        try
        {
            int saved = await _radioService.LoadDirectoryAsync(progress, _loadCts.Token);
            transport?.EndOperationProgress();
            _radioBrowser.ShowDirectory(); // reveal the freshly loaded station list
            MessageBox.Show(
                $"Directory loaded.\nStations written: {saved}\nTotal in database: {_radioService.CachedStationCount}\nPreviously cached: {existing}",
                "Internet Radio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            transport?.EndOperationProgress();
        }
        catch (Exception ex)
        {
            transport?.EndOperationProgress();
            MessageBox.Show($"Directory load failed:\n{ex.Message}", "Internet Radio",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _directoryLoading = false;
            _loadCts?.Dispose();
            _loadCts = null;
        }
    }

    // ---------- Search ----------

    private RibbonGroupPanel BuildSearchGroup()
    {
        var group = new RibbonGroupPanel("Search");

        var searchBox = new TextBox
        {
            Size = new Size(150, 24),
            BackColor = Color.FromArgb(30, 30, 32),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 8F)
        };
        group.AddControl(searchBox);
        group.AddButton("Search", onClick: () =>
        {
            _radioBrowser.Visible = true;
            _radioBrowser.SearchStations(searchBox.Text);
        });
        group.AddButton("Clear", onClick: () => searchBox.Clear());
        return group;
    }

    // ---------- Playback ----------

    private RibbonGroupPanel BuildPlaybackGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "Play Station", () => _radioBrowser.PlaySelectedStation());
        AddMenuItem(menu, "Stop Station", () => PlaybackService.Instance.Stop());
        AddMenuItem(menu, "Refresh Station", RefreshStation);
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Open Station Website", () => _radioBrowser.OpenHomepage());
        AddMenuItem(menu, "Station Properties", () => _radioBrowser.ShowStationProperties());

        var group = new RibbonGroupPanel("Playback") { ContextMenuStrip = menu };
        group.AddButton("Playback", menu);
        return group;
    }

    private void RefreshStation()
    {
        var playback = PlaybackService.Instance;
        if (playback.CurrentStationUrl is { } url && playback.CurrentTrackName is { Length: > 0 } name)
            playback.PlayStation(name, url);
        else
            MessageBox.Show("No station is playing.", "Internet Radio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---------- Favourites ----------

    private RibbonGroupPanel BuildFavouritesGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "Add Favourite", () => _radioBrowser.ToggleFavourite(true));
        AddMenuItem(menu, "Remove Favourite", () => _radioBrowser.ToggleFavourite(false));
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Show Favourites", () => _radioBrowser.ShowView("view:favs"));
        AddMenuItem(menu, "Recently Played", () => _radioBrowser.ShowView("view:played"));
        AddMenuItem(menu, "Clear History", ClearPlayHistory);

        var group = new RibbonGroupPanel("Favourites") { ContextMenuStrip = menu };
        group.AddButton("Favourites", menu);
        return group;
    }

    private void ClearPlayHistory()
    {
        var result = MessageBox.Show("Clear all radio play history?", "Clear History",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _radio.ClearPlayHistory();
        MessageBox.Show("Play history cleared.", "Clear History",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---------- Information ----------

    private RibbonGroupPanel BuildInformationGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "Station Information", () => _radioBrowser.ShowStationProperties());
        AddMenuItem(menu, "Stream Information", ShowStreamInfo);
        AddMenuItem(menu, "Current Track", ShowCurrentTrack);
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Station Homepage", () => _radioBrowser.OpenHomepage());
        AddMenuItem(menu, "Reliability Information", ShowReliability);

        var group = new RibbonGroupPanel("Information") { ContextMenuStrip = menu };
        group.AddButton("Information", menu);
        return group;
    }

    private void ShowStreamInfo()
    {
        var playback = PlaybackService.Instance;
        if (!playback.IsStationPlaying)
        {
            MessageBox.Show("No station is playing.", "Internet Radio",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        MessageBox.Show(
            $"Station: {playback.CurrentTrackName}\nStream URL: {playback.CurrentStationUrl}\n" +
            $"Sample rate: {(playback.IsPlaying ? "(live)" : "-")}",
            "Stream Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowCurrentTrack()
    {
        var playback = PlaybackService.Instance;
        MessageBox.Show(
            playback.IsPlaying || playback.IsPaused
                ? $"Now playing: {playback.CurrentTrackName}"
                : "Nothing is playing.",
            "Current Track", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowReliability()
    {
        // Votes are the community reliability signal in the Radio Browser data
        var stations = _radio.GetStations(1);
        int total = _radio.StationCount();
        MessageBox.Show(
            $"Stations cached: {total}\nMost popular station: {(stations.Count > 0 ? stations[0].Name : "-")} " +
            $"({stations.FirstOrDefault()?.Votes ?? 0:N0} votes)\n\nReliability is community-driven: higher vote counts indicate better uptime.",
            "Reliability", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

}
