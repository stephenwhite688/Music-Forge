using AudioForge.Analysis;
using AudioForge.IO.Codecs;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MusicForge.UI.Shared;

namespace MusicForge.UI.Dialogs;

/// <summary>
/// Track properties editor: reads/writes tags through the engine's
/// <see cref="AudioTags"/> API, computes ReplayGain via
/// <see cref="ReplayGainCalculator"/>, and detects BPM via
/// <see cref="BpmDetector"/>. All analysis/metadata work is engine-side.
/// </summary>
public sealed class TrackPropertiesDialog : Form
{
    private static readonly Color BgDark = Color.FromArgb(28, 28, 30);
    private static readonly Color PanelDark = Color.FromArgb(38, 38, 40);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);

    private readonly string _path;
    private readonly AudioTag _tag = new();

    private readonly TextBox _title = MkText();
    private readonly TextBox _artist = MkText();
    private readonly TextBox _album = MkText();
    private readonly TextBox _albumArtist = MkText();
    private readonly TextBox _genre = MkText();
    private readonly TextBox _year = MkText();
    private readonly TextBox _track = MkText();
    private readonly TextBox _disc = MkText();
    private readonly TextBox _composer = MkText();
    private readonly TextBox _comment = MkText();
    private readonly TextBox _rgInfo = MkText(140);
    private readonly TextBox _bpmInfo = MkText(140);
    private readonly Button _save = new();
    private readonly Button _rgBtn = new();
    private readonly Button _bpmBtn = new();

    private static TextBox MkText(int width = 300) => new()
    {
        Width = width, BackColor = PanelDark, ForeColor = TextColor,
        BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9F)
    };

    public TrackPropertiesDialog(string path)
    {
        _path = path;
        Text = $"Track Properties - {Path.GetFileName(path)}";
        Size = new Size(560, 560);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgDark;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9F);

        int y = 14;
        void Row(string label, Control ctl)
        {
            Controls.Add(new Label
            {
                Text = label, AutoSize = true,
                Location = new Point(14, y + 4), ForeColor = TextColor
            });
            ctl.Location = new Point(140, y);
            Controls.Add(ctl);
            y += 32;
        }

        try
        {
            var read = AudioTags.Read(path);
            _tag.Title = read.Title; _tag.Artists = read.Artists;
            _tag.Album = read.Album; _tag.AlbumArtists = read.AlbumArtists;
            _tag.Genres = read.Genres; _tag.Year = read.Year;
            _tag.Track = read.Track; _tag.Disc = read.Disc;
            _tag.Composer = read.Composer; _tag.Comment = read.Comment;
        }
        catch { /* file may have no readable tags */ }

        _title.Text = _tag.Title ?? "";
        _artist.Text = string.Join("; ", _tag.Artists);
        _album.Text = _tag.Album ?? "";
        _albumArtist.Text = string.Join("; ", _tag.AlbumArtists);
        _genre.Text = string.Join("; ", _tag.Genres);
        _year.Text = _tag.Year > 0 ? _tag.Year.ToString() : "";
        _track.Text = _tag.Track > 0 ? _tag.Track.ToString() : "";
        _disc.Text = _tag.Disc > 0 ? _tag.Disc.ToString() : "";
        _composer.Text = _tag.Composer ?? "";
        _comment.Text = _tag.Comment ?? "";
        _comment.Multiline = true; _comment.Height = 48;

        Row("Title", _title); Row("Artist", _artist); Row("Album", _album);
        Row("Album Artist", _albumArtist); Row("Genre", _genre);
        Row("Year", _year); Row("Track #", _track); Row("Disc #", _disc);
        Row("Composer", _composer); Row("Comment", _comment);
        y += 8;

        // ReplayGain row
        Controls.Add(new Label { Text = "ReplayGain:", AutoSize = true, Location = new Point(14, y + 4), ForeColor = TextColor });
        _rgInfo.Location = new Point(140, y);
        _rgInfo.ReadOnly = true;
        _rgBtn.Text = "Compute"; _rgBtn.Size = new Size(90, 26);
        _rgBtn.FlatStyle = FlatStyle.Flat; _rgBtn.ForeColor = TextColor; _rgBtn.BackColor = PanelDark;
        _rgBtn.Location = new Point(450, y - 1);
        _rgBtn.Click += RgClicked;
        Controls.Add(_rgInfo); Controls.Add(_rgBtn);
        RefreshRgInfo();
        y += 36;

        Controls.Add(new Label { Text = "BPM:", AutoSize = true, Location = new Point(14, y + 4), ForeColor = TextColor });
        _bpmInfo.Location = new Point(140, y);
        _bpmInfo.ReadOnly = true;
        _bpmBtn.Text = "Detect"; _bpmBtn.Size = new Size(90, 26);
        _bpmBtn.FlatStyle = FlatStyle.Flat; _bpmBtn.ForeColor = TextColor; _bpmBtn.BackColor = PanelDark;
        _bpmBtn.Location = new Point(450, y - 1);
        _bpmBtn.Click += BpmClicked;
        Controls.Add(_bpmInfo); Controls.Add(_bpmBtn);
        y += 36;

        _save.Text = "Save Tags"; _save.Size = new Size(120, 30);
        _save.Location = new Point(140, y + 6);
        _save.FlatStyle = FlatStyle.Flat; _save.BackColor = Color.FromArgb(0, 122, 204);
        _save.ForeColor = Color.White;
        _save.Click += SaveClicked;
        Controls.Add(_save);

        Controls.Add(new Label
        {
            Text = Path.GetFileName(path), AutoSize = true,
            Location = new Point(14, 8), ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8F)
        });
    }

    private void RefreshRgInfo()
    {
        try
        {
            var rg = ReplayGainTagIO.Read(_path);
            _rgInfo.Text = rg.IsEmpty ? "(none)"
                : $"track {rg.TrackGainDb?.ToString("F1") ?? "-"} dB, album {rg.AlbumGainDb?.ToString("F1") ?? "-"} dB";
        }
        catch { _rgInfo.Text = "(unreadable)"; }
    }

    private async void RgClicked(object? sender, EventArgs e)
    {
        _rgBtn.Enabled = false; _rgInfo.Text = "computing…";
        try
        {
            var tags = await ReplayGainCalculator.ComputeAsync(_path, writeTags: true);
            _rgInfo.Text = $"track {tags.TrackGainDb?.ToString("F1")} dB, album {tags.AlbumGainDb?.ToString("F1")} dB - written";
        }
        catch (Exception ex) { _rgInfo.Text = $"failed: {ex.Message}"; }
        finally { _rgBtn.Enabled = true; }
    }

    private async void BpmClicked(object? sender, EventArgs e)
    {
        _bpmBtn.Enabled = false; _bpmInfo.Text = "analysing…";
        try
        {
            double bpm = await Task.Run(() => DetectBpm(_path));
            _bpmInfo.Text = bpm > 0 ? $"{bpm:F1} BPM" : "could not detect";
        }
        catch (Exception ex) { _bpmInfo.Text = $"failed: {ex.Message}"; }
        finally { _bpmBtn.Enabled = true; }
    }

    /// <summary>Decodes up to ~2 minutes and runs the engine BPM detector.</summary>
    private static double DetectBpm(string path)
    {
        using var stream = CodecManager.ReadAsync(path).GetAwaiter().GetResult();
        if (stream is null) return 0;
        int maxFrames = Math.Min((int)stream.TotalFrames > 0 ? (int)stream.TotalFrames : int.MaxValue,
                                 stream.SampleRate * 120);
        var buf = new float[Math.Min(maxFrames, stream.SampleRate * 30) * stream.Channels];
        int total = 0;
        while (total < maxFrames)
        {
            int want = Math.Min(buf.Length / stream.Channels, maxFrames - total);
            int got = stream.ReadFramesInto(buf, want);
            if (got <= 0) break;
            total += got;
        }
        if (total == 0) return 0;
        return BpmDetector.DetectBpm(buf.AsSpan(0, total * stream.Channels),
            stream.SampleRate, stream.Channels);
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        _tag.Title = NullIfEmpty(_title.Text);
        _tag.Artists = Split(_artist.Text);
        _tag.Album = NullIfEmpty(_album.Text);
        _tag.AlbumArtists = Split(_albumArtist.Text);
        _tag.Genres = Split(_genre.Text);
        _tag.Year = int.TryParse(_year.Text, out var y) ? y : 0;
        _tag.Track = int.TryParse(_track.Text, out var t) ? t : 0;
        _tag.Disc = int.TryParse(_disc.Text, out var d) ? d : 0;
        _tag.Composer = NullIfEmpty(_composer.Text);
        _tag.Comment = NullIfEmpty(_comment.Text);
        try
        {
            AudioTags.Write(_path, _tag);
            AppMessageBox.Show("Tags saved.", "Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppMessageBox.Show($"Could not save tags:\n{ex.Message}", "Properties",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string[] Split(string s) =>
        s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
