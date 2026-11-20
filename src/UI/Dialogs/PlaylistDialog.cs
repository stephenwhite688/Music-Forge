using AudioForge.UI.Data;
using AudioForge.UI.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioForge.UI.Dialogs;

/// <summary>
/// Shows a playlist's tracks with add/remove support. Add opens the
/// <see cref="TrackPickerDialog"/> search picker.
/// </summary>
public sealed class PlaylistDialog : Form
{
    private readonly LibraryRepository _library = new();
    private readonly int _playlistId;
    private readonly string _playlistName;
    private readonly ListBox _tracks = new();
    private readonly Label _countLabel = new();

    /// <summary>Creates the dialog for the given playlist.</summary>
    public PlaylistDialog(int playlistId, string playlistName)
    {
        _playlistId = playlistId;
        _playlistName = playlistName;

        Text = $"Playlist - {playlistName}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 400);
        BackColor = Color.FromArgb(25, 25, 28);
        ForeColor = Color.White;

        var titleLabel = new Label
        {
            Text = playlistName,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(12, 10),
            AutoSize = true
        };
        _countLabel.Location = new Point(12, 36);
        _countLabel.AutoSize = true;
        _countLabel.ForeColor = Color.Gainsboro;

        _tracks.Location = new Point(12, 60);
        _tracks.Size = new Size(536, 290);
        _tracks.BackColor = Color.FromArgb(30, 30, 32);
        _tracks.ForeColor = Color.White;
        _tracks.BorderStyle = BorderStyle.FixedSingle;

        var btnPlay = new Button { Text = "Play", Location = new Point(12, 360), Width = 90 };
        var btnAdd = new Button { Text = "Add Tracks…", Location = new Point(110, 360), Width = 110 };
        var btnRemove = new Button { Text = "Remove Selected", Location = new Point(228, 360), Width = 120 };
        var btnClose = new Button { Text = "Close", Location = new Point(460, 360), Width = 88, DialogResult = DialogResult.Cancel };

        btnPlay.Click += async (s, e) => await PlaybackService.Instance.PlayPlaylistAsync(_playlistId);
        btnAdd.Click += (s, e) => AddTracks();
        btnRemove.Click += (s, e) => RemoveSelected();

        Controls.AddRange(new Control[] { titleLabel, _countLabel, _tracks, btnPlay, btnAdd, btnRemove, btnClose });
        CancelButton = btnClose;

        RefreshTracks();
    }

    private sealed class TrackItem
    {
        public int TrackId;
        public string Title = "";
        public string FilePath = "";
        public override string ToString() => Title;
    }

    private void RefreshTracks()
    {
        _tracks.Items.Clear();
        foreach (var (trackId, title, path) in _library.GetPlaylistTracks(_playlistId))
            _tracks.Items.Add(new TrackItem { TrackId = trackId, Title = title, FilePath = path });
        _countLabel.Text = $"{_tracks.Items.Count} track(s)";
    }

    private void AddTracks()
    {
        using var picker = new TrackPickerDialog(_playlistId);
        picker.ShowDialog(this);
        RefreshTracks();
    }

    private void RemoveSelected()
    {
        if (_tracks.SelectedItem is not TrackItem item) return;
        _library.RemoveTrackFromPlaylist(_playlistId, item.TrackId);
        RefreshTracks();
    }
}
