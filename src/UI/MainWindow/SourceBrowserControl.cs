using AudioForge.UI.Data;
using AudioForge.UI.Dialogs;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow;

/// <summary>
/// Explorer-style folder tree + audio file list. Right-click a folder to add
/// all its audio (recursively) to a playlist or save it as a library source;
/// right-click a file to add just that track.
/// </summary>
public sealed class SourceBrowserControl : UserControl
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".ogg", ".oga", ".wav", ".wave", ".m4a", ".aac", ".adts",
        ".wma", ".alac", ".caf", ".opus", ".dsf", ".dff"
    };

    private readonly LibraryRepository _library = new();
    private readonly TreeView _tree = new();
    private readonly ListView _files = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ContextMenuStrip _folderMenu = new();
    private readonly ContextMenuStrip _treeFileMenu = new();
    private readonly ContextMenuStrip _fileMenu = new();

    private const string ComputerRoot = "__computer__";

    /// <param name="rootPath">Folder to root the tree at, or null for "Computer" (all drives).</param>
    public SourceBrowserControl(string? rootPath = null)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(24, 24, 24);

        // ---- Folder tree (fills the whole panel - no file list) ----
        _tree.Dock = DockStyle.Fill;
        _tree.BackColor = Color.FromArgb(32, 32, 32);
        _tree.ForeColor = Color.FromArgb(212, 212, 212);
        _tree.LineColor = Color.FromArgb(60, 60, 60);
        _tree.HideSelection = false;
        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.AfterSelect += Tree_AfterSelect;
        _tree.NodeMouseClick += Tree_NodeMouseClick;

        _folderMenu.Items.Add("Add to Library", null, (s, e) => AddAsSource());
        _folderMenu.Items.Add("Add Folder to Playlist…", null, (s, e) => AddFolderToPlaylist());
        _tree.ContextMenuStrip = _folderMenu;

        _treeFileMenu.Items.Add("Add to Playlist…", null, (s, e) => AddTreeFileToPlaylist());
        _treeFileMenu.Items.Add("Add File's Folder to Playlist…", null, (s, e) => AddTreeFileFolderToPlaylist());

        // ---- File list ----
        _files.Dock = DockStyle.Fill;
        _files.View = View.Details;
        _files.FullRowSelect = true;
        _files.BackColor = Color.FromArgb(24, 24, 24);
        _files.ForeColor = Color.FromArgb(212, 212, 212);
        _files.BorderStyle = BorderStyle.None;
        _files.Columns.Add("Title", 300);
        _files.Columns.Add("File", 380);
        _files.Columns.Add("Size", 90, HorizontalAlignment.Right);
        _files.MouseDown += Files_MouseDown;

        _fileMenu.Items.Add("Add to Playlist…", null, (s, e) => AddFileToPlaylist());
        _files.ContextMenuStrip = _fileMenu;
        _files.Visible = false; // tree-only view - file list hidden per UI spec

        // ---- Status bar ----
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Color.FromArgb(140, 140, 140);
        _status.Items.Add(_statusLabel);
        _status.BackColor = Color.FromArgb(32, 32, 32);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.FromArgb(32, 32, 32) };
        bottom.Controls.Add(_status);

        Controls.Add(_tree);   // fill
        Controls.Add(bottom);  // bottom

        if (rootPath != null) BuildRoot(rootPath);
    }

    // ---------- Tree ----------

    /// <summary>True once a root folder/drive has been loaded.</summary>
    public bool HasRoot => _tree.Nodes.Count > 0;

    /// <summary>(Re)roots the tree at the given folder, or "Computer" if null.</summary>
    public void ShowRoot(string? rootPath = null) => BuildRoot(rootPath);

    private void BuildRoot(string? rootPath)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
        {
            var root = new TreeNode(Path.GetFileName(rootPath.TrimEnd('\\')) is { Length: > 0 } n ? n : rootPath)
            {
                Name = rootPath,
                Tag = rootPath
            };
            AddPlaceholder(root);
            _tree.Nodes.Add(root);
            root.Expand();
        }
        else
        {
            var computer = new TreeNode("Computer") { Name = ComputerRoot };
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var node = new TreeNode($"{drive.Name} {drive.VolumeLabel}")
                {
                    Name = drive.RootDirectory.FullName,
                    Tag = drive.RootDirectory.FullName
                };
                AddPlaceholder(node);
                computer.Nodes.Add(node);
            }
            _tree.Nodes.Add(computer);
            computer.Expand();
        }
        _tree.EndUpdate();
    }

    private static void AddPlaceholder(TreeNode node)
    {
        node.Nodes.Add(new TreeNode("…") { Name = "__loading__" });
    }

    private static bool IsLoaded(TreeNode node) =>
        node.Nodes.Count == 0 || node.Nodes[0].Name != "__loading__";

    private void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e) => PopulateChildren(e.Node);

    private void PopulateChildren(TreeNode node)
    {
        if (node.Name == ComputerRoot || node.Name == "__loading__") return;
        if (IsLoaded(node)) return;

        node.Nodes.Clear();
        try
        {
            var dirs = Directory.EnumerateDirectories(node.Name)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (string dir in dirs)
            {
                var child = new TreeNode(Path.GetFileName(dir)) { Name = dir, Tag = dir };
                AddPlaceholder(child);
                node.Nodes.Add(child);
            }

            // Show audio files directly in the tree so folders containing
            // only tracks don't appear "(empty)".
            foreach (string file in Directory.EnumerateFiles(node.Name)
                .Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                node.Nodes.Add(new TreeNode($"{Path.GetFileNameWithoutExtension(file)}  ({FormatSize(info.Length)})")
                {
                    Name = file,
                    Tag = file,
                    ForeColor = Color.FromArgb(170, 195, 230)
                });
            }
        }
        catch { /* inaccessible */ }

        if (node.Nodes.Count == 0)
            node.Nodes.Add(new TreeNode("(empty)") { Name = "__empty__", ForeColor = Color.Gray });
    }

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not string path) { _files.Items.Clear(); return; }
        if (Directory.Exists(path)) LoadFiles(path);
        else
        {
            _files.Items.Clear();
            _statusLabel.Text = path;
        }
    }

    private void LoadFiles(string dir)
    {
        _files.BeginUpdate();
        _files.Items.Clear();
        try
        {
            foreach (string file in Directory.EnumerateFiles(dir)
                .Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                var item = new ListViewItem(Path.GetFileNameWithoutExtension(file))
                {
                    Tag = file,
                    ToolTipText = file
                };
                item.SubItems.Add(file);
                item.SubItems.Add(FormatSize(info.Length));
                _files.Items.Add(item);
            }
        }
        catch { }
        _files.EndUpdate();
        _statusLabel.Text = $"{dir}  —  {_files.Items.Count} audio file(s)";
    }

    private void Tree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Node?.Tag is null) return;
        _tree.SelectedNode = e.Node;
        if (e.Node.Tag is string p && !Directory.Exists(p))
            _treeFileMenu.Show(_tree, e.Location);
        else
            _folderMenu.Show(_tree, e.Location);
    }

    private void Files_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var hit = _files.HitTest(e.Location);
            if (hit.Item is not null && !_files.SelectedItems.Contains(hit.Item))
            {
                _files.SelectedItems.Clear();
                hit.Item.Selected = true;
            }
            if (hit.Item is not null) _fileMenu.Show(_files, e.Location);
        }
    }

    // ---------- Actions ----------

    /// <summary>
    /// Returns a playlist Id to add tracks to: picks an existing playlist, or
    /// if none exist prompts to create one. -1 = cancelled.
    /// </summary>
    private int PickOrCreatePlaylist()
    {
        var playlists = _library.GetPlaylists();
        if (playlists.Count > 0)
        {
            using var picker = new PlaylistPickerDialog("Add to playlist");
            return picker.ShowDialog(FindForm()) == DialogResult.OK ? picker.SelectedPlaylistId : -1;
        }

        string? name = InputDialog.Show(this, "No playlists yet.\nEnter a name for a new playlist:",
            "Create Playlist");
        if (string.IsNullOrWhiteSpace(name)) return -1;
        return _library.CreatePlaylist(name.Trim());
    }

    private void AddFolderToPlaylist()
    {
        if (_tree.SelectedNode?.Tag is not string folder) return;
        int playlistId = PickOrCreatePlaylist();
        if (playlistId < 0) return;

        _statusLabel.Text = "Adding folder…";
        Cursor = Cursors.WaitCursor;
        try
        {
            int count = _library.AddFolderToPlaylist(playlistId, folder);
            _statusLabel.Text = $"Added {count} track(s) from '{folder}'";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not add folder:\n{ex.Message}", "Browse Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void AddFileToPlaylist()
    {
        var files = _files.SelectedItems.Cast<ListViewItem>()
            .Select(i => (string)i.Tag!)
            .ToList();
        if (files.Count == 0) return;

        int playlistId = PickOrCreatePlaylist();
        if (playlistId < 0) return;

        int added = 0;
        foreach (string file in files)
        {
            try { _library.AddFileToPlaylist(playlistId, file); added++; }
            catch { }
        }
        _statusLabel.Text = $"Added {added} track(s) to playlist";
    }

    private void AddTreeFileToPlaylist()
    {
        if (_tree.SelectedNode?.Tag is not string file || !File.Exists(file)) return;
        int playlistId = PickOrCreatePlaylist();
        if (playlistId < 0) return;
        try
        {
            _library.AddFileToPlaylist(playlistId, file);
            _statusLabel.Text = $"Added '{Path.GetFileName(file)}' to playlist";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not add file:\n{ex.Message}", "Browse Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddTreeFileFolderToPlaylist()
    {
        if (_tree.SelectedNode?.Tag is not string file || !File.Exists(file)) return;
        string? folder = Path.GetDirectoryName(file);
        if (folder is null) return;
        int playlistId = PickOrCreatePlaylist();
        if (playlistId < 0) return;
        _statusLabel.Text = "Adding folder…";
        Cursor = Cursors.WaitCursor;
        try
        {
            int count = _library.AddFolderToPlaylist(playlistId, folder);
            _statusLabel.Text = $"Added {count} track(s) from '{folder}'";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not add folder:\n{ex.Message}", "Browse Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void AddAsSource()
    {
        if (_tree.SelectedNode?.Tag is not string folder) return;
        try
        {
            _library.AddSource(folder);
            _statusLabel.Text = $"Source saved: {folder}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not add source:\n{ex.Message}", "Browse Media",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

