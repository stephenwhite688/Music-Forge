using MusicForge.UI.Data;
using MusicForge.UI.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicForge.UI.MainWindow;

/// <summary>
/// Playlist contents shown in the Media tab space (replaces the folder tree /
/// album list while open). Right-click a track to remove it; double-click to play.
/// </summary>
public sealed class PlaylistViewControl : UserControl
{
    private static readonly Color BgDark = Color.FromArgb(24, 24, 24);
    private static readonly Color PanelDark = Color.FromArgb(32, 32, 32);
    private static readonly Color TextColor = Color.FromArgb(212, 212, 212);
    private static readonly Color DimText = Color.FromArgb(140, 140, 140);

    private readonly LibraryRepository _library = new();
    private readonly Label _title = new();
    private readonly Label _count = new();
    private readonly ListView _tracks = new();
    private int _playlistId = -1;

    public PlaylistViewControl()
    {
        Dock = DockStyle.Fill;
        BackColor = BgDark;
        Visible = false;

        _title.AutoSize = true;
        _title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _title.ForeColor = TextColor;
        _title.Location = new Point(12, 10);

        _count.AutoSize = true;
        _count.ForeColor = DimText;
        _count.Location = new Point(12, 38);

        _tracks.Dock = DockStyle.Fill;
        _tracks.View = View.Details;
        _tracks.FullRowSelect = true;
        _tracks.BackColor = Color.FromArgb(30, 30, 32);
        _tracks.ForeColor = TextColor;
        _tracks.BorderStyle = BorderStyle.None;
        _tracks.Columns.Add("#", 40, HorizontalAlignment.Right);
        _tracks.Columns.Add("Title", 280);
        _tracks.Columns.Add("Artist", 200);
        _tracks.Columns.Add("Album", 200);
        _tracks.Columns.Add("Time", 70, HorizontalAlignment.Right);

        // Right-click a track -> remove it from the playlist
        var trackMenu = new ContextMenuStrip();
        var propsItem = new ToolStripMenuItem("Properties…");
        propsItem.Click += (s, e) => ShowProperties();
        trackMenu.Items.Add(propsItem);
        trackMenu.Items.Add(new ToolStripSeparator());
        var removeItem = new ToolStripMenuItem("Remove from Playlist");
        removeItem.Click += (s, e) => RemoveSelected();
        trackMenu.Items.Add(removeItem);
        _tracks.ContextMenuStrip = trackMenu;

        // Double-click a track -> play the playlist starting at that track
        _tracks.DoubleClick += async (s, e) =>
        {
            int start = _tracks.SelectedItems.Count > 0 ? _tracks.SelectedItems[0].Index : 0;
            await PlaybackService.Instance.PlayPlaylistAsync(_playlistId, start);
        };

        var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = PanelDark };
        header.Controls.Add(_title);
        header.Controls.Add(_count);

        Controls.Add(_tracks);
        Controls.Add(header);
    }

    /// <summary>Loads a playlist's tracks into the view.</summary>
    public void LoadPlaylist(int playlistId, string playlistName)
    {
        _playlistId = playlistId;
        _title.Text = playlistName;
        RefreshTracks();
        Visible = true;
    }

    private void RefreshTracks()
    {
        _tracks.BeginUpdate();
        _tracks.Items.Clear();
        int i = 0;
        foreach (var t in _library.GetPlaylistTrackDetails(_playlistId))
        {
            var item = new ListViewItem((++i).ToString()) { Tag = t };
            item.SubItems.Add(t.Title);
            item.SubItems.Add(t.ArtistName ?? "");
            item.SubItems.Add(t.AlbumName ?? "");
            item.SubItems.Add(FormatTime(t.DurationSeconds));
            _tracks.Items.Add(item);
        }
        _tracks.EndUpdate();
        _count.Text = $"{_tracks.Items.Count} track(s)";
    }

    private void ShowProperties()
    {
        if (_tracks.SelectedItems.Count == 0) return;
        if (_tracks.SelectedItems[0].Tag is not LibraryRepository.LibraryTrack t) return;
        using var dlg = new Dialogs.TrackPropertiesDialog(t.FilePath);
        dlg.ShowDialog(FindForm());
    }

    private void RemoveSelected()
    {
        if (_tracks.SelectedItems.Count == 0) return;
        foreach (ListViewItem item in _tracks.SelectedItems)
            if (item.Tag is LibraryRepository.LibraryTrack t)
                _library.RemoveTrackFromPlaylist(_playlistId, t.Id);
        RefreshTracks();
    }

    private static string FormatTime(double? seconds) =>
        seconds is { } s && s > 0
            ? $"{(int)(s / 60)}:{(int)(s % 60):D2}"
            : "";
}
