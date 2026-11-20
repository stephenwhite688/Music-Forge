using AudioForge.UI.Data;
using AudioForge.UI.Dialogs;
using AudioForge.UI.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow;

/// <summary>
/// Foobar-style media library panel: Album List tree (Artist -> Album ->
/// tracks, with counts) on the left, playlist-style track list on the right.
/// Hidden by default - toggled from the Media ribbon's Library group.
/// Right-click any tree node or track -> "Add to Playlist".
/// </summary>
public sealed class LibraryBrowserControl : UserControl
{
    private static readonly Color BgDark = Color.FromArgb(24, 24, 24);
    private static readonly Color PanelDark = Color.FromArgb(32, 32, 32);
    private static readonly Color LineColor = Color.FromArgb(60, 60, 60);
    private static readonly Color TextColor = Color.FromArgb(212, 212, 212);
    private static readonly Color DimText = Color.FromArgb(140, 140, 140);

    private const string UnknownArtist = "[unknown artist]";
    private const string UnknownAlbum = "[unknown album]";

    private readonly LibraryRepository _library = new();
    private readonly TreeView _tree = new();
    private readonly ListView _tracks = new();
    private readonly Panel _leftPanel = new();
    private readonly ContextMenuStrip _nodeMenu = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    private List<LibraryRepository.LibraryTrack> _allTracks = new();
    private bool _suppressTreeSelect;

    public LibraryBrowserControl()
    {
        Dock = DockStyle.Fill;
        BackColor = BgDark;
        Visible = false; // hidden until toggled from the ribbon

        // ---- Left: Album List tree (fills panel; track list is detail view) ----
        _leftPanel.Dock = DockStyle.Fill;
        _leftPanel.BackColor = PanelDark;
        _tree.Dock = DockStyle.Fill;
        _tree.BackColor = PanelDark;
        _tree.ForeColor = TextColor;
        _tree.LineColor = LineColor;
        _tree.HotTracking = true;
        _tree.ShowLines = true;
        _tree.ShowPlusMinus = true;
        _tree.HideSelection = false;
        _tree.AfterSelect += Tree_AfterSelect;
        _tree.NodeMouseClick += Tree_NodeMouseClick;

        _nodeMenu.Items.Add("Add to Playlist…", null, (s, e) => AddNodeToPlaylist(false));
        _nodeMenu.Items.Add("Add to Playlist and Play", null, (s, e) => AddNodeToPlaylist(true));
        _nodeMenu.Items.Add(new ToolStripSeparator());
        _nodeMenu.Items.Add("Play", null, (s, e) => PlayNode());

        // ---- Right: track list (foobar playlist columns) ----
        _tracks.Dock = DockStyle.Fill;
        _tracks.View = View.Details;
        _tracks.FullRowSelect = true;
        _tracks.BackColor = BgDark;
        _tracks.ForeColor = TextColor;
        _tracks.BorderStyle = BorderStyle.None;
        _tracks.Columns.Add("#", 40, HorizontalAlignment.Right);
        _tracks.Columns.Add("Title", 280);
        _tracks.Columns.Add("Artist", 200);
        _tracks.Columns.Add("Album", 200);
        _tracks.Columns.Add("Time", 70, HorizontalAlignment.Right);
        _tracks.SelectedIndexChanged += Tracks_SelectedIndexChanged;
        _tracks.ContextMenuStrip = _nodeMenu;
        _tracks.MouseDown += Tracks_MouseDown;

        // ---- Bottom: status bar ----
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = DimText;
        _status.Items.Add(_statusLabel);
        _status.BackColor = PanelDark;

        _leftPanel.Controls.Add(_tree);
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = PanelDark };
        bottomPanel.Controls.Add(_status);

        Controls.Add(_tracks);
        Controls.Add(_leftPanel);
        Controls.Add(bottomPanel);

        // Tree-only by default (matches SourceBrowser); the track list appears
        // only for search results.
        SetDetailVisible(false);
    }

    /// <summary>Shows/hides the right-hand track list; tree takes full width when hidden.</summary>
    private void SetDetailVisible(bool visible)
    {
        _tracks.Visible = visible;
        _leftPanel.Dock = visible ? DockStyle.Left : DockStyle.Fill;
        if (visible) _leftPanel.Width = 300;
    }

    /// <summary>Reloads the tree and track list from the database.</summary>
    public void RefreshLibrary()
    {
        _allTracks = _library.GetAllTracks();
        BuildTree();
        ShowAllTracks();
        _statusLabel.Text = $"{_allTracks.Count} tracks in library";
    }

    // ---------- Tree (Album List) ----------

    private void BuildTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        var root = new TreeNode($"Media Library ({_allTracks.Count})") { Name = "__all__" };
        _tree.Nodes.Add(root);

        // Artist -> Album -> track counts
        var artistNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        var albumNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        var albumCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in _allTracks)
        {
            string artist = t.ArtistName ?? UnknownArtist;
            string album = t.AlbumName
                ?? Path.GetFileName(Path.GetDirectoryName(t.FilePath) ?? "")
                ?? UnknownAlbum;
            if (string.IsNullOrWhiteSpace(album)) album = UnknownAlbum;

            string albumKey = artist + "|" + album;
            albumCounts[albumKey] = albumCounts.GetValueOrDefault(albumKey) + 1;
            artistCounts[artist] = artistCounts.GetValueOrDefault(artist) + 1;

            if (!artistNodes.TryGetValue(artist, out var artistNode))
            {
                artistNode = new TreeNode(artist) { Name = $"artist:{artist}" };
                artistNodes[artist] = artistNode;
                root.Nodes.Add(artistNode);
            }
            if (!albumNodes.TryGetValue(albumKey, out var albumNode))
            {
                albumNode = new TreeNode(album) { Name = $"album:{albumKey}" };
                albumNodes[albumKey] = albumNode;
                artistNode.Nodes.Add(albumNode);
            }
            albumNode.Nodes.Add(new TreeNode(TrackNodeText(t))
            {
                Name = $"track:{t.Id}",
                Tag = t,
                ToolTipText = $"{t.Title}\n{t.FilePath}"
            });
        }

        // stamp counts
        foreach (var (key, node) in albumNodes)
            node.Text = $"{node.Text}  ({albumCounts[key]})";
        foreach (var (artist, node) in artistNodes)
            node.Text = $"{artist}  ({artistCounts[artist]})";

        root.Expand();
        _tree.EndUpdate();
    }

    private static string TrackNodeText(LibraryRepository.LibraryTrack t) =>
        t.TrackNumber is { } n ? $"{n,2}. {t.Title}" : t.Title;

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (_suppressTreeSelect || e.Node is null) return;
        if (e.Node.Name == "__all__") { ShowAllTracks(); return; }

        var ids = new HashSet<int>();
        CollectTrackIds(e.Node, ids);
        if (ids.Count == 0) return;

        _tracks.BeginUpdate();
        _tracks.Items.Clear();
        int i = 0;
        foreach (var t in _allTracks.Where(t => ids.Contains(t.Id)))
            AddTrackItem(t, ++i);
        _tracks.EndUpdate();
        UpdateStatus();
    }

    private static void CollectTrackIds(TreeNode node, HashSet<int> ids)
    {
        foreach (TreeNode child in node.Nodes)
        {
            if (child.Name.StartsWith("track:"))
                ids.Add(((LibraryRepository.LibraryTrack)child.Tag!).Id);
            else
                CollectTrackIds(child, ids);
        }
    }

    private void Tree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Node is null) return;
        _tree.SelectedNode = e.Node;
        _nodeMenu.Show(_tree, e.Location);
    }

    // ---------- Track list ----------

    /// <summary>Shows only tracks matching the search text (title/artist/album/path).</summary>
    public void FilterTracks(string text)
    {
        var term = text.Trim();
        _tracks.BeginUpdate();
        _tracks.Items.Clear();
        int i = 0;
        foreach (var t in _allTracks.Where(t =>
            t.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (t.ArtistName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (t.AlbumName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
            t.FilePath.Contains(term, StringComparison.OrdinalIgnoreCase)))
            AddTrackItem(t, ++i);
        _tracks.EndUpdate();
        _statusLabel.Text = $"{_tracks.Items.Count} match(es) for '{term}'";
        SetDetailVisible(true); // search results need the detail list
    }

    /// <summary>Shows every track in the library.</summary>
    public void ShowAllTracks()
    {
        _tracks.BeginUpdate();
        _tracks.Items.Clear();
        int i = 0;
        foreach (var t in _allTracks)
            AddTrackItem(t, ++i);
        _tracks.EndUpdate();
        UpdateStatus();
        SetDetailVisible(false); // back to tree-only view
    }

    private void AddTrackItem(LibraryRepository.LibraryTrack t, int index)
    {
        var item = new ListViewItem(index.ToString()) { Tag = t, ToolTipText = t.FilePath };
        item.SubItems.Add(t.Title);
        item.SubItems.Add(t.ArtistName ?? "");
        item.SubItems.Add(t.AlbumName ?? "");
        item.SubItems.Add(FormatTime(t.DurationSeconds));
        _tracks.Items.Add(item);
    }

    private void Tracks_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var hit = _tracks.HitTest(e.Location);
            if (hit.Item is not null && !_tracks.SelectedItems.Contains(hit.Item))
            {
                _tracks.SelectedItems.Clear();
                hit.Item.Selected = true;
            }
        }
    }

    private void Tracks_SelectedIndexChanged(object? sender, EventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        int sel = _tracks.SelectedItems.Count;
        _statusLabel.Text = sel > 0
            ? $"{sel} selected of {_tracks.Items.Count} shown ({_allTracks.Count} total)"
            : $"{_tracks.Items.Count} shown ({_allTracks.Count} total)";
    }

    // ---------- Actions ----------

    /// <summary>Adds all tracks under the selected tree node (or selected list tracks) to a playlist.</summary>
    private void AddNodeToPlaylist(bool playAfter)
    {
        List<int> ids;
        if (_tracks.Focused && _tracks.SelectedItems.Count > 0)
        {
            ids = _tracks.SelectedItems
                .Cast<ListViewItem>()
                .Select(i => ((LibraryRepository.LibraryTrack)i.Tag!).Id)
                .Distinct().ToList();
        }
        else if (_tree.SelectedNode is { } node)
        {
            var set = new HashSet<int>();
            CollectTrackIds(node, set);
            // Track leaf nodes have no children - include the node itself.
            if (node.Name.StartsWith("track:") && node.Tag is LibraryRepository.LibraryTrack self)
                set.Add(self.Id);
            ids = set.ToList();
        }
        else return;

        if (ids.Count == 0) return;

        using var picker = new PlaylistPickerDialog("Add tracks to playlist");
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedPlaylistId < 0) return;

        foreach (int id in ids)
            _library.AddTrackToPlaylist(picker.SelectedPlaylistId, id);
        _statusLabel.Text = $"Added {ids.Count} track(s) to playlist";

        if (playAfter)
            _ = PlaybackService.Instance.PlayPlaylistAsync(picker.SelectedPlaylistId);
    }

    /// <summary>
    /// Plays the context: a single track node plays just that file; an
    /// album/artist/root node plays all its tracks; selected detail-list
    /// tracks play those. Replaces whatever is playing - no playlist created.
    /// </summary>
    private void PlayNode()
    {
        List<LibraryRepository.LibraryTrack> tracks;
        if (_tracks.Focused && _tracks.SelectedItems.Count > 0)
        {
            tracks = _tracks.SelectedItems
                .Cast<ListViewItem>()
                .Select(i => (LibraryRepository.LibraryTrack)i.Tag!)
                .ToList();
        }
        else if (_tree.SelectedNode?.Name.StartsWith("track:") == true)
        {
            tracks = new List<LibraryRepository.LibraryTrack>
            {
                (LibraryRepository.LibraryTrack)_tree.SelectedNode.Tag!
            };
        }
        else if (_tree.SelectedNode is { } node)
        {
            var ids = new HashSet<int>();
            CollectTrackIds(node, ids);
            tracks = _allTracks.Where(t => ids.Contains(t.Id)).ToList();
        }
        else return;

        if (tracks.Count == 0) return;
        _statusLabel.Text = $"Playing {tracks.Count} track(s)";
        _ = PlaybackService.Instance.PlayQueueAsync(tracks.Select(t => t.FilePath).ToList());
    }

    private static string FormatTime(double? seconds) =>
        seconds is { } s && s > 0
            ? $"{(int)(s / 60)}:{(int)(s % 60):D2}"
            : "";

    private static string FormatSize(long bytes) => bytes switch
    {
        <= 0 => "",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
