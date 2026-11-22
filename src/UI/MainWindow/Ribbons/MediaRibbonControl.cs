using AudioForge.UI.Data;
using AudioForge.UI.Dialogs;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow.Ribbons;

public partial class MediaRibbonControl : RibbonStripControl
{
    private static readonly LibraryRepository _library = new();
    private readonly LibraryBrowserControl _libraryBrowser = new();
    private readonly SourceBrowserControl _sourceBrowser = new();
    private readonly PlaylistViewControl _playlistView = new();
    private readonly ArtworkPanelControl _artworkPanel = new();
    private readonly Panel _tabHost = new() { Dock = DockStyle.Fill };

    public override Control? TabContent => _tabHost;

    public MediaRibbonControl()
    {
        InitializeComponent();
        BuildRibbon();
        _tabHost.Controls.Add(_libraryBrowser);
        _tabHost.Controls.Add(_sourceBrowser);
        _tabHost.Controls.Add(_playlistView);
        _tabHost.Controls.Add(_artworkPanel); // docked right - added last so it claims the edge
        _sourceBrowser.Visible = false;
        _libraryBrowser.Visible = false;
        _playlistView.Visible = false;
    }

    private void BuildRibbon()
    {
        var sourceGroup = BuildSourceGroup();
        var libraryGroup = BuildLibraryGroup();
        var playlistGroup = BuildPlaylistGroup();
        var artworkGroup = BuildArtworkGroup();
        var searchGroup = BuildSearchGroup();
        var toolsGroup = BuildToolsGroup();
        var outputGroup = BuildOutputGroup();

        // WinForms docks later-added controls closer to the edge, so add in
        // reverse order to keep Source leftmost and Tools rightmost.
        var groups = new[] { toolsGroup, outputGroup, searchGroup, artworkGroup, playlistGroup, libraryGroup, sourceGroup };
        foreach (var group in groups)
        {
            group.Dock = DockStyle.Left;
            group.Caption = string.Empty; // single ribbon-wide caption below
            RibbonPanel.Controls.Add(group);
        }

        RibbonPanel.Controls.Add(new Label
        {
            Text = "Music Selection",
            Dock = DockStyle.Bottom,
            Height = 13,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(51, 51, 55),
            Font = new Font("Segoe UI", 7.5F)
        });
    }

    private static ContextMenuStrip BuildMenu(params string[] items) => RibbonMenuFactory.Create(items);

    private RibbonGroupPanel BuildSourceGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "Select Drive…", SelectDriveSource);
        AddMenuItem(menu, "Select Folder…", SelectFolderSource);
        menu.Items.Add(BuildRecentSourcesItem());
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Clear Sources", ClearSources);

        var group = new RibbonGroupPanel("Source") { ContextMenuStrip = menu };
        group.AddButton("Source", menu);
        return group;
    }

    private ToolStripMenuItem BuildRecentSourcesItem()
    {
        var item = new ToolStripMenuItem("Recent Sources");
        foreach (string path in _library.GetSourcePaths())
        {
            var child = new ToolStripMenuItem(path) { ToolTipText = path };
            child.Click += (s, e) => MessageBox.Show($"Source:\n{path}", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            item.DropDownItems.Add(child);
        }
        if (item.DropDownItems.Count == 0)
            item.DropDownItems.Add(new ToolStripMenuItem("(none)") { Enabled = false });
        return item;
    }

    private void ClearSources()
    {
        var paths = _library.GetSourcePaths();
        if (paths.Count == 0)
        {
            MessageBox.Show("No sources configured.", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var result = MessageBox.Show(
            $"Remove all {paths.Count} source(s)?\nTracks and playlists are kept.\n\n" +
            string.Join("\n", paths.Take(5)) + (paths.Count > 5 ? "\n…" : ""),
            "Clear Sources", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _library.ClearSources();
        MessageBox.Show("Sources cleared.", "Media", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SelectFolderSource()
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Select a media folder",
            UseDescriptionForTitle = true
        };
        if (picker.ShowDialog(FindForm()) == DialogResult.OK)
            ShowSourceBrowser(picker.SelectedPath);
    }

    private void ShowSourceBrowser(string rootPath)
    {
        _sourceBrowser.ShowRoot(rootPath);
        ShowOnly(_sourceBrowser);
    }

    /// <summary>Shows exactly one tab view, hiding the others.</summary>
    private void ShowOnly(Control view)
    {
        _libraryBrowser.Visible = view == _libraryBrowser;
        _sourceBrowser.Visible = view == _sourceBrowser;
        _playlistView.Visible = view == _playlistView;
    }

    private void SelectDriveSource()
    {
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
        if (drives.Count == 0)
        {
            MessageBox.Show("No ready drives found.", "Media", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (var drive in drives)
        {
            string root = drive.RootDirectory.FullName;
            string label = $"{drive.Name}  {drive.VolumeLabel}";
            var item = new ToolStripMenuItem(label) { Tag = root };
            item.Click += (s, e) => ShowSourceBrowser(root);
            menu.Items.Add(item);
        }
        menu.Show(Cursor.Position);
    }

    private void AddSourcePath(string path)
    {
        try
        {
            _library.AddSource(path);
            MessageBox.Show($"Source added:\n{path}", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not add source:\n{ex.Message}", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private RibbonGroupPanel BuildLibraryGroup()
    {
        // Single button -> one dropdown with every library command.
        // (The DB persists automatically, so "Save/Load Library" stubs are gone.)
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "Show / Hide Library", ToggleLibraryView);
        AddMenuItem(menu, "Scan Library", ScanLibrary);
        AddMenuItem(menu, "Rescan Library", ScanLibrary);
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Library Properties", ShowLibraryProperties);
        AddMenuItem(menu, "Export Backup…", ExportBackup);

        var group = new RibbonGroupPanel("Library") { ContextMenuStrip = menu };
        group.AddButton("Library", menu);
        return group;
    }

    private static void AddMenuItem(ContextMenuStrip menu, string text, Action handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) => handler();
        menu.Items.Add(item);
    }

    private void ToggleLibraryView()
    {
        if (_libraryBrowser.Visible)
        {
            _libraryBrowser.Visible = false;
            _playlistView.Visible = false;
            _sourceBrowser.Visible = _sourceBrowser.HasRoot;
        }
        else
        {
            _playlistView.Visible = false;
            _libraryBrowser.Visible = true;
            _sourceBrowser.Visible = false;
            _libraryBrowser.RefreshLibrary();
        }
    }

    private void ShowLibraryProperties()
    {
        int sources = _library.GetSourcePaths().Count;
        int tracks = _library.GetTrackCount();
        int playlists = _library.GetPlaylists().Count;
        MessageBox.Show(
            $"Library: Default Library\n\nSources: {sources}\nTracks: {tracks}\nPlaylists: {playlists}\n\nDatabase: {AudioForgeDatabase.Instance.DatabasePath}",
            "Library Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportBackup()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export library backup",
            Filter = "SQLite backup (*.db)|*.db|All files (*.*)|*.*",
            FileName = $"audioforge-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            AudioForgeDatabase.Instance.Backup(dialog.FileName);
            MessageBox.Show($"Backup saved to:\n{dialog.FileName}", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Backup failed:\n{ex.Message}", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void ScanLibrary()
    {
        try
        {
            int added = await _library.ScanLibraryAsync();
            int total = _library.GetTrackCount();
            _libraryBrowser.RefreshLibrary();
            MessageBox.Show($"Scan complete.\nNew tracks: {added}\nLibrary total: {total}",
                "Media", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Library scan failed:\n{ex.Message}", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private RibbonGroupPanel BuildPlaylistGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "New Playlist…", NewPlaylist);
        AddMenuItem(menu, "Load Playlist…", LoadPlaylist);
        AddMenuItem(menu, "Play Playlist…", PlayPlaylist);
        AddMenuItem(menu, "Close Playlist", ClosePlaylistView);
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Delete Playlist…", DeletePlaylist);

        var group = new RibbonGroupPanel("Playlist") { ContextMenuStrip = menu };
        group.AddButton("Playlist", menu);
        return group;
    }

    private void PlayPlaylist()
    {
        var playlists = _library.GetPlaylists();
        if (playlists.Count == 0)
        {
            MessageBox.Show("No playlists yet. Create one with 'New Playlist'.", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var picker = new PlaylistPickerDialog("Play playlist");
        if (picker.ShowDialog(FindForm()) == DialogResult.OK && picker.SelectedPlaylistId >= 0)
            _ = AudioForge.UI.Services.PlaybackService.Instance.PlayPlaylistAsync(picker.SelectedPlaylistId);
    }

    private void DeletePlaylist()
    {
        var playlists = _library.GetPlaylists();
        if (playlists.Count == 0)
        {
            MessageBox.Show("No playlists to delete.", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var picker = new PlaylistPickerDialog("Delete playlist");
        if (picker.ShowDialog(FindForm()) != DialogResult.OK || picker.SelectedPlaylistId < 0) return;

        string? name = playlists.FirstOrDefault(p => p.Id == picker.SelectedPlaylistId).Name;
        var result = MessageBox.Show($"Delete playlist '{name}' and all its track entries?",
            "Delete Playlist", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _library.DeletePlaylist(picker.SelectedPlaylistId);
        MessageBox.Show("Playlist deleted.", "Media", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void NewPlaylist()
    {
        string? name = InputDialog.Show(FindForm(), "Enter playlist name:", "New Playlist");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            _library.CreatePlaylist(name.Trim());
            MessageBox.Show($"Playlist '{name.Trim()}' created.", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not create playlist:\n{ex.Message}", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadPlaylist()
    {
        var playlists = _library.GetPlaylists();
        if (playlists.Count == 0)
        {
            MessageBox.Show("No playlists yet. Create one with 'New Playlist'.", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (var (id, name) in playlists)
        {
            int playlistId = id;
            string label = name;
            var item = new ToolStripMenuItem(label);
            item.Click += (s, e) => OpenPlaylist(playlistId, label);
            menu.Items.Add(item);
        }
        menu.Show(Cursor.Position);
    }

    private void OpenPlaylist(int playlistId, string name)
    {
        _playlistView.LoadPlaylist(playlistId, name);
        ShowOnly(_playlistView);
    }

    private void ClosePlaylistView()
    {
        _playlistView.Visible = false;
        _sourceBrowser.Visible = _sourceBrowser.HasRoot;
    }

    private RibbonGroupPanel BuildArtworkGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "View Artwork", ShowArtwork);
        AddMenuItem(menu, "Remove Artwork…", RemoveArtwork);
        var group = new RibbonGroupPanel("Artwork") { ContextMenuStrip = menu };
        group.AddButton("Artwork", menu);
        return group;
    }

    private void ShowArtwork()
    {
        if (_artworkPanel.Visible) _artworkPanel.HidePanel();
        else _artworkPanel.ShowPanel();
    }

    /// <summary>Strips embedded pictures from the currently playing track's file.</summary>
    private void RemoveArtwork()
    {
        string? path = AudioForge.UI.Services.PlaybackService.Instance.CurrentTrackPath;
        if (path is null)
        {
            MessageBox.Show("No track is playing.", "Artwork",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var result = MessageBox.Show(
            $"Remove embedded artwork from:\n{path}\n\nThis modifies the file permanently.",
            "Remove Artwork", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        try
        {
            using var file = TagLib.File.Create(path);
            file.Tag.Pictures = Array.Empty<TagLib.IPicture>();
            file.Save();
            if (_artworkPanel.Visible) _artworkPanel.RefreshArtwork();
            MessageBox.Show("Artwork removed.", "Artwork",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not remove artwork:\n{ex.Message}", "Artwork",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private RibbonGroupPanel BuildSearchGroup()
    {
        var group = new RibbonGroupPanel("Search");

        var searchBox = new TextBox
        {
            Size = new Size(150, 24),
            BackColor = System.Drawing.Color.FromArgb(30, 30, 32),
            ForeColor = System.Drawing.Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new System.Drawing.Font("Segoe UI", 8F)
        };
        group.AddControl(searchBox);

        group.AddButton("Search", onClick: () => DoSearch(searchBox.Text));
        group.AddButton("Clear", onClick: () => { searchBox.Clear(); _libraryBrowser.ShowAllTracks(); });
        return group;
    }

    private void DoSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { _libraryBrowser.ShowAllTracks(); return; }
        if (!_libraryBrowser.Visible) ToggleLibraryView();
        _libraryBrowser.FilterTracks(text);
    }

    /// <summary>Output endpoint picker - any Windows render device (BT, USB, HDMI...).</summary>
    private RibbonGroupPanel BuildOutputGroup()
    {
        var group = new RibbonGroupPanel("Output");
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 190,
            BackColor = System.Drawing.Color.FromArgb(30, 30, 32),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new System.Drawing.Font("Segoe UI", 8F)
        };
        RefreshOutputDevices(combo);
        combo.SelectedIndexChanged += (_, _) =>
        {
            var pb = AudioForge.UI.Services.PlaybackService.Instance;
            pb.OutputDeviceId = combo.SelectedItem is OutputDeviceItem item ? item.Id : null;
        };
        group.AddControl(combo);
        return group;
    }

    private static void RefreshOutputDevices(ComboBox combo)
    {
        var current = AudioForge.UI.Services.PlaybackService.Instance.OutputDeviceId;
        combo.Items.Clear();
        combo.Items.Add(new OutputDeviceItem(null, "System Default"));
        foreach (var (id, name) in AudioForge.UI.Services.PlaybackService.Instance.OutputDevices)
            combo.Items.Add(new OutputDeviceItem(id, name));
        int sel = 0;
        if (current is not null)
        {
            var devices = AudioForge.UI.Services.PlaybackService.Instance.OutputDevices;
            for (int i = 0; i < devices.Count; i++)
                if (devices[i].Id == current) { sel = i + 1; break; }
        }
        combo.SelectedIndex = Math.Min(sel, combo.Items.Count - 1);
    }

    private sealed class OutputDeviceItem
    {
        public string? Id { get; }
        private readonly string _name;
        public OutputDeviceItem(string? id, string name) { Id = id; _name = name; }
        public override string ToString() => _name;
    }

    private RibbonGroupPanel BuildToolsGroup()
    {
        var menu = new ContextMenuStrip();
        AddMenuItem(menu, "Open Stream URL…", OpenStreamUrl);
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Convert / Export Audio…", ShowConvertDialog);
        AddMenuItem(menu, "Track Properties…", ShowTrackProperties);
        AddMenuItem(menu, "MIDI Devices…", ShowMidiDevices);
        menu.Items.Add(new ToolStripSeparator());
        AddMenuItem(menu, "Library Statistics", ShowStatistics);
        AddMenuItem(menu, "Compact Database", CompactDatabase);
        AddMenuItem(menu, "Backup Library…", ExportBackup);
        AddMenuItem(menu, "Open Options", OpenOptionsTab);

        var group = new RibbonGroupPanel("Tools") { ContextMenuStrip = menu };
        group.AddButton("Tools", menu);
        return group;
    }

    private void OpenStreamUrl()
    {
        string? url = InputDialog.Show(FindForm(),
            "Enter stream URL (.m3u8 HLS, .mpd DASH, or direct stream):", "Open Stream");
        if (string.IsNullOrWhiteSpace(url)) return;
        url = url.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Only http/https stream URLs are supported.", "Media",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _ = AudioForge.UI.Services.PlaybackService.Instance.PlayUrlAsync(url);
    }

    private void ShowConvertDialog()
    {
        using var dlg = new ConvertDialog();
        dlg.ShowDialog(FindForm());
    }

    private void ShowTrackProperties()
    {
        string? path = AudioForge.UI.Services.PlaybackService.Instance.CurrentTrackPath;
        if (path is null)
        {
            MessageBox.Show("No track is playing.", "Properties",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new TrackPropertiesDialog(path);
        dlg.ShowDialog(FindForm());
    }

    private void ShowMidiDevices()
    {
        using var dlg = new MidiDevicesDialog();
        dlg.ShowDialog(FindForm());
    }

    private void ShowStatistics()
    {
        int sources = _library.GetSourcePaths().Count;
        int tracks = _library.GetTrackCount();
        int playlists = _library.GetPlaylists().Count;
        long dbSize = 0;
        try { dbSize = new FileInfo(AudioForgeDatabase.Instance.DatabasePath).Length; }
        catch { }
        MessageBox.Show(
            $"Sources: {sources}\nTracks: {tracks}\nPlaylists: {playlists}\n" +
            $"Database size: {dbSize / 1024.0 / 1024:F1} MB\n\n{AudioForgeDatabase.Instance.DatabasePath}",
            "Library Statistics", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CompactDatabase()
    {
        try
        {
            using var connection = AudioForgeDatabase.Instance.OpenConnection();
            AudioForgeDatabase.Execute(connection, "VACUUM;");
            MessageBox.Show("Database compacted.", "Tools",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Compact failed:\n{ex.Message}", "Tools",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenOptionsTab()
    {
        // Walk up to the TabControl and select the Options page.
        for (Control? c = this; c is not null; c = c.Parent)
        {
            if (c is TabControl tabs)
            {
                foreach (TabPage page in tabs.TabPages)
                    if (page.Text.IndexOf("option", StringComparison.OrdinalIgnoreCase) >= 0)
                    { tabs.SelectedTab = page; return; }
                tabs.SelectedIndex = tabs.TabPages.Count - 1;
                return;
            }
        }
    }
}
