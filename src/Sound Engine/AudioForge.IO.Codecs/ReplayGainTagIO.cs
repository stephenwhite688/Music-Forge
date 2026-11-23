using System;
using System.Globalization;
using System.Linq;

namespace AudioForge.IO.Codecs;

/// <summary>
/// ReplayGain tag values (track/album gain in dB, peaks in dB).
/// </summary>
public sealed record ReplayGainTags(
    double? TrackGainDb,
    double? TrackPeakDb,
    double? AlbumGainDb,
    double? AlbumPeakDb)
{
    public static readonly ReplayGainTags Empty = new(null, null, null, null);
    public bool IsEmpty => TrackGainDb == null && TrackPeakDb == null && AlbumGainDb == null && AlbumPeakDb == null;
}

/// <summary>
/// Reads and writes ReplayGain tags via TagLibSharp. Vorbis-comment formats
/// (OGG/FLAC/Opus) use native REPLAYGAIN_* fields; ID3v2 formats (MP3/AAC)
/// use TXXX user-text frames. MP4 (m4a) freeform atoms are not supported —
/// <see cref="Write"/> returns false for those.
/// </summary>
public static class ReplayGainTagIO
{
    private const string TrackGainKey = "REPLAYGAIN_TRACK_GAIN";
    private const string TrackPeakKey = "REPLAYGAIN_TRACK_PEAK";
    private const string AlbumGainKey = "REPLAYGAIN_ALBUM_GAIN";
    private const string AlbumPeakKey = "REPLAYGAIN_ALBUM_PEAK";

    /// <summary>Reads ReplayGain tags from an audio file.</summary>
    public static ReplayGainTags Read(string path)
    {
        using var file = TagLib.File.Create(path);
        string? tg = GetField(file, TrackGainKey);
        string? tp = GetField(file, TrackPeakKey);
        string? ag = GetField(file, AlbumGainKey);
        string? ap = GetField(file, AlbumPeakKey);
        return new ReplayGainTags(ParseDb(tg), ParseDb(tp), ParseDb(ag), ParseDb(ap));
    }

    /// <summary>
    /// Writes ReplayGain tags. Returns true if at least one tag container
    /// accepted the values.
    /// </summary>
    public static bool Write(string path, ReplayGainTags tags)
    {
        using var file = TagLib.File.Create(path);
        bool wrote = false;

        if (file.GetTag(TagLib.TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph)
        {
            SetXiph(xiph, TrackGainKey, tags.TrackGainDb);
            SetXiph(xiph, TrackPeakKey, tags.TrackPeakDb);
            SetXiph(xiph, AlbumGainKey, tags.AlbumGainDb);
            SetXiph(xiph, AlbumPeakKey, tags.AlbumPeakDb);
            wrote = true;
        }

        if (file.GetTag(TagLib.TagTypes.Id3v2, true) is TagLib.Id3v2.Tag id3)
        {
            SetId3(id3, TrackGainKey, tags.TrackGainDb);
            SetId3(id3, TrackPeakKey, tags.TrackPeakDb);
            SetId3(id3, AlbumGainKey, tags.AlbumGainDb);
            SetId3(id3, AlbumPeakKey, tags.AlbumPeakDb);
            wrote = true;
        }

        if (!wrote) return false;
        file.Save();
        return true;
    }

    private static string? GetField(TagLib.File file, string key)
    {
        if (file.GetTag(TagLib.TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph)
        {
            var v = xiph.GetFirstField(key);
            if (!string.IsNullOrEmpty(v)) return v;
        }
        if (file.GetTag(TagLib.TagTypes.Id3v2) is TagLib.Id3v2.Tag id3)
        {
            var frame = id3.GetFrames<TagLib.Id3v2.UserTextInformationFrame>()
                .FirstOrDefault(f => string.Equals(f.Description, key, StringComparison.OrdinalIgnoreCase));
            var text = frame?.Text;
            if (text != null && text.Length > 0) return text[0];
        }
        return null;
    }

    private static void SetXiph(TagLib.Ogg.XiphComment comment, string key, double? value)
    {
        if (value.HasValue)
            comment.SetField(key, FormatDb(value.Value));
        else
            comment.RemoveField(key);
    }

    private static void SetId3(TagLib.Id3v2.Tag tag, string key, double? value)
    {
        if (value.HasValue)
            tag.SetUserTextAsString(key, FormatDb(value.Value));
        else
            tag.RemoveFrames(key);
    }

    private static string FormatDb(double db) =>
        db.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + " dB";

    private static double? ParseDb(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // Values look like "+1.23 dB" or "-0.50".
        var span = s.AsSpan().Trim();
        int end = span.Length;
        while (end > 0 && (char.IsLetter(span[end - 1]) || span[end - 1] == ' ')) end--;
        if (double.TryParse(span[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return v;
        return null;
    }
}
