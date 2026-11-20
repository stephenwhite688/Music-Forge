using AudioForge.UI.Data;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioForge.UI.Dialogs;

/// <summary>
/// Search the library and add selected tracks to a playlist.
/// </summary>
public sealed class TrackPickerDialog : Form
{
    private readonly LibraryRepository _library = new();
    private readonly int _playlistId;
    private readonly TextBox _searchBox = new();
    private readonly ListBox _results = new();
    private readonly Label _statusLabel = new();

    /// <summary>Creates the picker targeting the given playlist Id.</summary>
    public TrackPickerDialog(int playlistId)
    {
        _playlistId = playlistId;

        Text = "Add Tracks to Playlist";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 440);
        BackColor = Color.FromArgb(25, 25, 28);
        ForeColor = Color.White;

        _searchBox.Location = new Point(12, 12);
        _searchBox.Width = 460;
        _searchBox.BackColor = Color.FromArgb(30, 30, 32);
        _searchBox.ForeColor = Color.White;
        _searchBox.BorderStyle = BorderStyle.FixedSingle;

        var btnSearch = new Button { Text = "Search", Location = new Point(480, 10), Width = 70 };
        btnSearch.Click += (s, e) => DoSearch();

        _results.Location = new Point(12, 46);
        _results.Size = new Size(596, 340);
        _results.BackColor = Color.FromArgb(30, 30, 32);
        _results.ForeColor = Color.White;
        _results.BorderStyle = BorderStyle.FixedSingle;

        _statusLabel.Location = new Point(12, 392);
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.Gainsboro;

        var btnAdd = new Button { Text = "Add Selected", Location = new Point(400, 392), Width = 100 };
        btnAdd.Click += (s, e) => AddSelected();
        var btnClose = new Button { Text = "Close", Location = new Point(508, 392), Width = 88, DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[] { _searchBox, btnSearch, _results, _statusLabel, btnAdd, btnClose });
        CancelButton = btnClose;
        AcceptButton = btnSearch;
    }

    private sealed class TrackItem
    {
        public int TrackId;
        public string Title = "";
        public string FilePath = "";
        public override string ToString() => Title;
    }

    private void DoSearch()
    {
        string text = _searchBox.Text.Trim();
        if (text.Length == 0) return;
        _results.Items.Clear();
        foreach (var (trackId, title, path) in _library.SearchTracks(text))
            _results.Items.Add(new TrackItem { TrackId = trackId, Title = title, FilePath = path });
        _statusLabel.Text = $"{_results.Items.Count} result(s)";
    }

    private void AddSelected()
    {
        if (_results.SelectedItem is not TrackItem item) return;
        _library.AddTrackToPlaylist(_playlistId, item.TrackId);
        _statusLabel.Text = $"Added '{item.Title}'";
    }
}
