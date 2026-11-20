using AudioForge.UI.Data;
using System.Drawing;
using System.Windows.Forms;

namespace AudioForge.UI.Dialogs;

/// <summary>Simple dialog that lets the user pick one of their playlists.</summary>
public sealed class PlaylistPickerDialog : Form
{
    private readonly ListBox _list = new();

    public int SelectedPlaylistId { get; private set; } = -1;

    public PlaylistPickerDialog(string title = "Select Playlist")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(320, 300);

        _list.Dock = DockStyle.Fill;
        _list.DisplayMember = "Name";
        _list.ValueMember = "Id";

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 30 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Bottom, Height = 30 };

        ok.Click += (s, e) =>
        {
            if (_list.SelectedItem is PlaylistItem item) SelectedPlaylistId = item.Id;
        };

        Controls.Add(_list);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        foreach (var p in new LibraryRepository().GetPlaylists())
            _list.Items.Add(new PlaylistItem(p.Id, p.Name));
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private sealed class PlaylistItem
    {
        public int Id { get; }
        public string Name { get; }
        public PlaylistItem(int id, string name) { Id = id; Name = name; }
        public override string ToString() => Name;
    }
}
