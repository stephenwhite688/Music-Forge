using AudioForge.UI.Data;
using AudioForge.UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow;

/// <summary>
/// Internet radio directory panel: Continent -> Country -> stations tree on
/// the left (plus All / Favourites / Language / Genre / Popular / Recent
/// views), station list on the right. Double-click or Play starts the
/// station through <see cref="PlaybackService"/> (Helix engine).
/// </summary>
public sealed class RadioBrowserControl : UserControl
{
    private static readonly Color BgDark = Color.FromArgb(24, 24, 24);
    private static readonly Color PanelDark = Color.FromArgb(32, 32, 32);
    private static readonly Color LineColor = Color.FromArgb(60, 60, 60);
    private static readonly Color TextColor = Color.FromArgb(212, 212, 212);
    private static readonly Color DimText = Color.FromArgb(140, 140, 140);

    private readonly RadioRepository _radio = new();
    private readonly TreeView _tree = new();
    private readonly ListView _stations = new();
    private readonly ContextMenuStrip _stationMenu = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _countLabel = new();

    private List<StationRecord> _shown = new();

    public RadioBrowserControl()
    {
        Dock = DockStyle.Fill;
        BackColor = BgDark;
        Visible = false;

        // ---- Left: directory tree ----
        _tree.Dock = DockStyle.Left;
        _tree.Width = 280;
        _tree.BackColor = PanelDark;
        _tree.ForeColor = TextColor;
        _tree.LineColor = LineColor;
        _tree.HotTracking = true;
        _tree.AfterSelect += Tree_AfterSelect;
        _tree.NodeMouseClick += Tree_NodeMouseClick;

        // ---- Right: station list ----
        _stations.Dock = DockStyle.Fill;
        _stations.View = View.Details;
        _stations.FullRowSelect = true;
        _stations.BackColor = BgDark;
        _stations.ForeColor = TextColor;
        _stations.BorderStyle = BorderStyle.None;
        _stations.Columns.Add("Station", 240);
        _stations.Columns.Add("Country", 120);
        _stations.Columns.Add("Codec", 70);
        _stations.Columns.Add("Bitrate", 70, HorizontalAlignment.Right);
        _stations.Columns.Add("Votes", 70, HorizontalAlignment.Right);
        _stations.ItemActivate += Stations_ItemActivate;
        _stations.MouseDoubleClick += Stations_ItemActivate;
        _stations.ContextMenuStrip = _stationMenu;
        _stations.MouseDown += Stations_MouseDown;

        _stationMenu.Items.Add("Play", null, (s, e) => PlaySelectedStation());
        _stationMenu.Items.Add(new ToolStripSeparator());
        _stationMenu.Items.Add("Add to Favourites", null, (s, e) => ToggleFavourite(true));
        _stationMenu.Items.Add("Remove from Favourites", null, (s, e) => ToggleFavourite(false));
        _stationMenu.Items.Add(new ToolStripSeparator());
        _stationMenu.Items.Add("Station Properties", null, (s, e) => ShowStationProperties());
        _stationMenu.Items.Add("Open Homepage", null, (s, e) => OpenHomepage());

        // ---- Bottom status ----
        _countLabel.Spring = true;
        _countLabel.TextAlign = ContentAlignment.MiddleLeft;
        _countLabel.ForeColor = DimText;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = DimText;
        _status.Items.Add(_countLabel);
        _status.Items.Add(_statusLabel);
        _status.BackColor = PanelDark;

        var treePanel = new Panel { Dock = DockStyle.Left, Width = 280, BackColor = PanelDark };
        treePanel.Controls.Add(_tree);
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = PanelDark };
        bottomPanel.Controls.Add(_status);

        Controls.Add(_stations);
        Controls.Add(treePanel);
        Controls.Add(bottomPanel);
    }

    /// <summary>Rebuilds the directory tree and refreshes the status line.</summary>
    public void RefreshDirectory()
    {
        BuildTree();
        var last = _radio.GetLastRefresh();
        _statusLabel.Text = last is { } dt
            ? $"Directory cached {dt.ToLocalTime():g}"
            : "Directory not loaded - use Directory > Load Directory";
    }

    // ---------- Tree ----------

    private void BuildTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        int total = _radio.StationCount();

        var all = new TreeNode($"All Stations ({total})") { Name = "view:all" };
        var favs = new TreeNode("Favourites") { Name = "view:favs" };
        var countries = new TreeNode("By Country") { Name = "view:countries" };
        var languages = new TreeNode("By Language") { Name = "view:languages" };
        var genres = new TreeNode("By Genre") { Name = "view:genres" };
        var popular = new TreeNode("Popular Stations") { Name = "view:popular" };
        var recent = new TreeNode("Recently Added") { Name = "view:recent" };
        var played = new TreeNode("Recently Played") { Name = "view:played" };

        _tree.Nodes.Add(all);
        _tree.Nodes.Add(favs);
        _tree.Nodes.Add(played);
        _tree.Nodes.Add(countries);
        _tree.Nodes.Add(languages);
        _tree.Nodes.Add(genres);
        _tree.Nodes.Add(popular);
        _tree.Nodes.Add(recent);

        if (total > 0)
        {
            // Continent -> Country
            foreach (var (continent, countryMap) in _radio.GetDirectoryTreeCounts()
                         .OrderBy(kv => kv.Key))
            {
                var contNode = new TreeNode($"{continent} ({countryMap.Values.Sum()})")
                { Name = $"continent:{continent}" };
                foreach (var (key, count) in countryMap.OrderByDescending(kv => kv.Value))
                {
                    // key = "CC|Country" or plain country name when code unknown
                    string cc = key.Contains('|') ? key.Split('|')[0] : "";
                    string name = key.Contains('|') ? key.Split('|', 2)[1] : key;
                    contNode.Nodes.Add(new TreeNode($"{name} ({count})")
                    { Name = $"country:{cc}|{name}", Tag = cc });
                }
                countries.Nodes.Add(contNode);
            }

            // Languages
            foreach (var (lang, count) in _radio.GetLanguageCounts()
                         .OrderByDescending(kv => kv.Value).Take(60))
                languages.Nodes.Add(new TreeNode($"{lang} ({count})")
                { Name = $"lang:{lang}", Tag = lang });

            // Genres (top tags by station count)
            foreach (var (tag, count) in _radio.GetTopGenres().Take(80))
                genres.Nodes.Add(new TreeNode($"{tag} ({count})")
                { Name = $"genre:{tag}", Tag = tag });
        }

        all.Expand();
        countries.Expand();
        _tree.SelectedNode = all;
        _tree.EndUpdate();
    }

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node is null) return;
        LoadForNode(e.Node);
    }

    private void LoadForNode(TreeNode node)
    {
        _stations.BeginUpdate();
        _stations.Items.Clear();
        _shown = new List<StationRecord>();
        string name = node.Name;

        if (name == "view:all")
            _shown = _radio.GetStations(10000);
        else if (name == "view:favs")
            _shown = _radio.GetFavourites();
        else if (name == "view:popular")
            _shown = _radio.GetStations(500);
        else if (name == "view:recent")
            _shown = _radio.GetRecentlyAdded(500);
        else if (name == "view:played")
            _shown = _radio.GetRecentlyPlayed(200);
        else if (name.StartsWith("country:", StringComparison.Ordinal))
        {
            string key = name["country:".Length..];
            string cc = key.Split('|')[0];
            _shown = _radio.GetStationsByCountry(string.IsNullOrEmpty(cc) ? null : cc);
        }
        else if (name.StartsWith("lang:", StringComparison.Ordinal))
            _shown = _radio.GetStationsByLanguage(node.Tag as string);
        else if (name.StartsWith("genre:", StringComparison.Ordinal))
            _shown = _radio.GetStationsByGenre(node.Tag as string ?? "");
        else
        {
            _stations.EndUpdate();
            return; // parent nodes (By Country etc.) - just expand
        }

        foreach (var s in _shown) AddStationItem(s);
        _stations.EndUpdate();
        _countLabel.Text = $"{_shown.Count} station(s) shown";
    }

    private void AddStationItem(StationRecord s)
    {
        var item = new ListViewItem(s.Name) { Tag = s, ToolTipText = $"{s.Name}\n{s.StreamUrl}" };
        item.SubItems.Add(s.Country ?? "");
        item.SubItems.Add(s.Codec ?? "");
        item.SubItems.Add(s.Bitrate?.ToString() ?? "");
        item.SubItems.Add(s.Votes.ToString("N0"));
        if (s.IsFavourite) item.ForeColor = Color.Gold;
        _stations.Items.Add(item);
    }

    private void Tree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.Node is not null)
            _tree.SelectedNode = e.Node;
    }

    // ---------- Station list ----------

    /// <summary>Filters the station list by free text (uses the DB search).</summary>
    public void SearchStations(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) { RefreshDirectory(); return; }
        _stations.BeginUpdate();
        _stations.Items.Clear();
        _shown = _radio.SearchStations(term.Trim());
        foreach (var s in _shown) AddStationItem(s);
        _stations.EndUpdate();
        _countLabel.Text = $"{_shown.Count} match(es) for '{term.Trim()}'";
    }

    /// <summary>Shows the full cached directory (used after Load Directory).</summary>
    public void ShowDirectory()
    {
        Visible = true;
        RefreshDirectory();
    }

    /// <summary>Selects a top-level directory view node (e.g. "view:countries").</summary>
    public void ShowView(string viewName)
    {
        Visible = true;
        RefreshDirectory();
        foreach (TreeNode node in _tree.Nodes)
            if (node.Name == viewName)
            {
                _tree.SelectedNode = node;
                return;
            }
    }

    private List<StationRecord> SelectedStations() =>
        _stations.SelectedItems.Cast<ListViewItem>()
            .Select(i => (StationRecord)i.Tag!).ToList();

    private void Stations_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var hit = _stations.HitTest(e.Location);
            if (hit.Item is not null && !_stations.SelectedItems.Contains(hit.Item))
            {
                _stations.SelectedItems.Clear();
                hit.Item.Selected = true;
            }
        }
    }

    private void Stations_ItemActivate(object? sender, EventArgs e) => PlaySelectedStation();

    // ---------- Actions ----------

    public void PlaySelectedStation()
    {
        foreach (var s in SelectedStations())
        {
            PlaybackService.Instance.PlayStation(s.Name, s.StreamUrl, s.Id);
            _countLabel.Text = $"Playing: {s.Name}";
            return; // single station at a time
        }
        if (_stations.Items.Count == 1 && _stations.Items[0] is { } only)
        {
            var s = (StationRecord)only.Tag!;
            PlaybackService.Instance.PlayStation(s.Name, s.StreamUrl, s.Id);
        }
    }

    public void ToggleFavourite(bool add)
    {
        foreach (var s in SelectedStations())
        {
            _radio.ToggleFavourite(s.Id);
            s.IsFavourite = !_radio.GetFavourites().Any(f => f.Id == s.Id);
        }
        // refresh colours
        foreach (ListViewItem item in _stations.Items)
            if (item.Tag is StationRecord rec)
                item.ForeColor = rec.IsFavourite ? Color.Gold : TextColor;
        _countLabel.Text = "Favourites updated";
    }

    public void ShowStationProperties()
    {
        var s = SelectedStations().FirstOrDefault();
        if (s is null) return;
        MessageBox.Show(
            $"Name: {s.Name}\nCountry: {s.Country} ({s.CountryCode})\nContinent: {s.Continent}\n" +
            $"Language: {s.Language}\nGenre tags: {s.GenreTags}\nCodec: {s.Codec}  Bitrate: {s.Bitrate}\n" +
            $"Votes: {s.Votes:N0}\nStream: {s.StreamUrl}\nHomepage: {s.HomepageUrl ?? "-"}",
            "Station Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void OpenHomepage()
    {
        var s = SelectedStations().FirstOrDefault();
        if (s?.HomepageUrl is { Length: > 0 } url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }
    }
}
