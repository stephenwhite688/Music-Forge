using AudioForge.UI.Services;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow;

/// <summary>
/// Right-side artwork pane for the Media tab. Follows the currently playing
/// track (PlaybackService) - independent of which view (tree/library/
/// playlist) is showing. Toggled from the Artwork ribbon menu.
/// </summary>
public sealed class ArtworkPanelControl : UserControl
{
    private static readonly Color PanelDark = Color.FromArgb(32, 32, 32);
    private static readonly Color DimText = Color.FromArgb(140, 140, 140);

    private readonly PictureBox _artwork = new();
    private readonly Label _label = new();
    private readonly Label _titleLabel = new();
    private MemoryStream? _imageStream; // must stay alive - GDI+ reads lazily

    public ArtworkPanelControl()
    {
        Dock = DockStyle.Right;
        Width = 220;
        BackColor = PanelDark;
        Visible = false;

        _artwork.Dock = DockStyle.Fill;
        _artwork.SizeMode = PictureBoxSizeMode.Zoom;
        _artwork.BackColor = Color.FromArgb(20, 20, 20);

        _label.Dock = DockStyle.Bottom;
        _label.Height = 40;
        _label.TextAlign = ContentAlignment.TopCenter;
        _label.ForeColor = DimText;
        _label.Text = "No track playing";

        _titleLabel.Dock = DockStyle.Top;
        _titleLabel.Height = 20;
        _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        _titleLabel.ForeColor = Color.FromArgb(212, 212, 212);
        _titleLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _titleLabel.Text = "ARTWORK";

        Controls.Add(_artwork);
        Controls.Add(_label);
        Controls.Add(_titleLabel);

        // Refresh when the track changes (or stops) while the pane is visible
        var playback = PlaybackService.Instance;
        playback.TrackChanged += (s, e) => { if (Visible) RefreshArtwork(); };
        playback.PlaybackStopped += (s, e) => { if (Visible) RefreshArtwork(); };
    }

    /// <summary>Shows the pane and loads artwork for the current track.</summary>
    public void ShowPanel()
    {
        Visible = true;
        RefreshArtwork();
    }

    /// <summary>Hides the pane.</summary>
    public void HidePanel() => Visible = false;

    /// <summary>Loads embedded artwork for the currently playing track.</summary>
    public void RefreshArtwork()
    {
        string? path = PlaybackService.Instance.CurrentTrackPath;
        if (path is null)
        {
            _artwork.Image = null;
            _label.Text = "No track playing";
            return;
        }

        _label.Text = Path.GetFileName(path);
        try
        {
            using var file = TagLib.File.Create(path);
            var pic = file.Tag.Pictures.FirstOrDefault();
            if (pic?.Data?.Data is { Length: > 0 } bytes)
            {
                // GDI+ requires the stream to outlive the Image - keep it in a field
                _imageStream?.Dispose();
                _imageStream = new MemoryStream(bytes);
                _artwork.Image?.Dispose();
                _artwork.Image = Image.FromStream(_imageStream);
                return;
            }
        }
        catch { }
        _artwork.Image = null;
        _imageStream?.Dispose();
        _imageStream = null;
        _label.Text = "No embedded artwork";
    }
}
