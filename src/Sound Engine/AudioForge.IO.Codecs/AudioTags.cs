using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TagLib;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Format-agnostic audio metadata. Populated from ID3v2 (MP3), Vorbis comments
/// (FLAC/OGG/Opus), MP4 atoms (.m4a/.mp4), and RIFF INFO (WAV) via TagLibSharp.
/// </summary>
public sealed class AudioTag
{
    public string? Title { get; set; }
    public string[] Artists { get; set; } = Array.Empty<string>();
    public string[] AlbumArtists { get; set; } = Array.Empty<string>();
    public string? Album { get; set; }
    public string[] Genres { get; set; } = Array.Empty<string>();
    public int Year { get; set; }
    public int Track { get; set; }
    public int Disc { get; set; }
    public string? Composer { get; set; }
    public string? Comment { get; set; }
    public string? Lyrics { get; set; }
    public string? Copyright { get; set; }

    /// <summary>Embedded pictures (cover art), with MIME type.</summary>
    public List<AudioPicture> Pictures { get; } = new();

    /// <summary>Container duration in seconds, when known.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>Container bitrate in bits/sec, when known.</summary>
    public int Bitrate { get; set; }

    /// <summary>True if no tag fields were present.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title) && Artists.Length == 0 &&
        string.IsNullOrWhiteSpace(Album) && string.IsNullOrWhiteSpace(Comment);
}

/// <summary>An embedded picture with MIME type and raw image bytes.</summary>
public sealed class AudioPicture
{
    public string MimeType { get; set; } = "image/jpeg";
    public PictureType Type { get; set; } = PictureType.CoverFront;
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public enum PictureType
    {
        Other = 0, FileIcon, OtherFileIcon, CoverFront, CoverBack,
        Leaflet, Media, LeadArtist, Artist, Conductor, Band, Composer,
        Lyricist, RecordingLocation, DuringRecording, DuringPerformance,
        MovieScreenCapture, BrightColouredFish, Illustration, BandLogo, PublisherLogo
    }
}

/// <summary>
/// Engine-side tag reader/writer. Any application (AudioForge, DAW,
/// forensics tool) can read/write metadata without depending on UI code.
/// </summary>
public static class AudioTags
{
    /// <summary>Reads tags from an audio file. Returns an empty AudioTag on failure.</summary>
    public static AudioTag Read(string path)
    {
        var tag = new AudioTag();
        try
        {
            using var file = TagLib.File.Create(path);
            var t = file.Tag;
            tag.Title = NullIfEmpty(t.Title);
            tag.Artists = Clean(t.Performers);
            tag.AlbumArtists = Clean(t.AlbumArtists);
            tag.Album = NullIfEmpty(t.Album);
            tag.Genres = Clean(t.Genres);
            tag.Year = (int)t.Year;
            tag.Track = (int)t.Track;
            tag.Disc = (int)t.Disc;
            tag.Composer = NullIfEmpty(t.Composers.Length > 0 ? string.Join("; ", t.Composers) : null);
            tag.Comment = NullIfEmpty(t.Comment);
            tag.Lyrics = NullIfEmpty(t.Lyrics);
            tag.Copyright = NullIfEmpty(t.Copyright);
            foreach (var p in t.Pictures)
                tag.Pictures.Add(new AudioPicture
                {
                    MimeType = p.MimeType,
                    Type = (AudioPicture.PictureType)(int)p.Type,
                    Data = p.Data.Data.ToArray()
                });
            try
            {
                tag.DurationSeconds = file.Properties.Duration.TotalSeconds;
                tag.Bitrate = (int)file.Properties.AudioBitrate;
            }
            catch { /* properties unavailable for some containers */ }
        }
        catch { /* unreadable tags - caller gets an empty tag */ }
        return tag;
    }

    /// <summary>Writes tag fields to an audio file. Picture data replaces all existing pictures.</summary>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public static void Write(string path, AudioTag tag, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var file = TagLib.File.Create(path);
        var t = file.Tag;
        if (tag.Title is not null) t.Title = tag.Title;
        t.Performers = tag.Artists;
        t.AlbumArtists = tag.AlbumArtists;
        if (tag.Album is not null) t.Album = tag.Album;
        t.Genres = tag.Genres;
        t.Year = (uint)Math.Max(0, tag.Year);
        t.Track = (uint)Math.Max(0, tag.Track);
        t.Disc = (uint)Math.Max(0, tag.Disc);
        if (tag.Composer is not null)
            t.Composers = tag.Composer.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();
        if (tag.Comment is not null) t.Comment = tag.Comment;
        if (tag.Lyrics is not null) t.Lyrics = tag.Lyrics;
        if (tag.Copyright is not null) t.Copyright = tag.Copyright;
        t.Pictures = tag.Pictures
            .Where(p => p.Data.Length > 0)
            .Select(p => new Picture(new ByteVector(p.Data))
            {
                Type = (PictureType)(int)p.Type,
                MimeType = p.MimeType,
                Description = string.Empty
            })
            .ToArray();
        file.Save();
    }

    /// <summary>Async convenience wrapper for <see cref="Read"/>.</summary>
    public static Task<AudioTag> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(path), cancellationToken);

    /// <summary>Async convenience wrapper for <see cref="Write"/>.</summary>
    public static Task WriteAsync(string path, AudioTag tag, CancellationToken cancellationToken = default) =>
        Task.Run(() => Write(path, tag, cancellationToken), cancellationToken);

    private static string[] Clean(string[] values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
