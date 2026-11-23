using System.Text;

namespace AudioForge.Streaming;

/// <summary>A parsed HLS media playlist segment.</summary>
public sealed record HlsSegment(
    string Uri,
    double DurationSeconds,
    bool HasDiscontinuity,
    long SequenceNumber = -1);

/// <summary>Encryption parameters from EXT-X-KEY.</summary>
public sealed record HlsKeyInfo(string Method, string? Uri, string? Iv);

/// <summary>A parsed HLS playlist (master or media).</summary>
public sealed class HlsPlaylist
{
    public bool IsMaster { get; }
    public bool IsLive { get; }
    public int MediaSequence { get; }
    public double TargetDuration { get; }
    public string? PlaylistType { get; }
    public HlsKeyInfo? Key { get; }
    public List<HlsSegment> Segments { get; } = new();
    public List<HlsVariant> Variants { get; } = new();

    public sealed record HlsVariant(string Uri, int Bandwidth, int? Width, int? Height, string? Codecs);

    private HlsPlaylist(bool isMaster, bool isLive, int mediaSequence,
        double targetDuration, string? playlistType, HlsKeyInfo? key)
    {
        IsMaster = isMaster; IsLive = isLive; MediaSequence = mediaSequence;
        TargetDuration = targetDuration; PlaylistType = playlistType; Key = key;
    }

    /// <summary>Parses an m3u8 playlist body. Throws on non-HLS content.</summary>
    public static HlsPlaylist Parse(string text)
    {
        if (!text.TrimStart().StartsWith("#EXTM3U", StringComparison.Ordinal))
            throw new InvalidDataException("Not an HLS playlist (missing #EXTM3U).");

        bool isMaster = text.Contains("#EXT-X-STREAM-INF", StringComparison.Ordinal);
        bool isLive = !text.Contains("#EXT-X-ENDLIST", StringComparison.Ordinal);
        int mediaSeq = 0;
        double targetDur = 0;
        string? ptype = null;
        HlsKeyInfo? key = null;
        var segs = new List<HlsSegment>();
        var vars = new List<HlsVariant>();

        string? pendingUri = null;
        double pendingDur = 0;
        bool pendingDisc = false;
        int pendingBw = 0; int? pendingW = null, pendingH = null; string? pendingCodecs = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXT-X-MEDIA-SEQUENCE:", StringComparison.Ordinal))
                int.TryParse(line[22..], out mediaSeq);
            else if (line.StartsWith("#EXT-X-TARGETDURATION:", StringComparison.Ordinal))
                double.TryParse(line[22..], out targetDur);
            else if (line.StartsWith("#EXT-X-PLAYLIST-TYPE:", StringComparison.Ordinal))
                ptype = line[21..];
            else if (line.StartsWith("#EXT-X-KEY:", StringComparison.Ordinal))
                key = ParseKey(line[10..]);
            else if (line.StartsWith("#EXT-X-DISCONTINUITY", StringComparison.Ordinal))
                pendingDisc = true;
            else if (line.StartsWith("#EXTINF:", StringComparison.Ordinal))
            {
                var rest = line[8..];
                int comma = rest.IndexOf(',');
                if (comma >= 0 && double.TryParse(rest[..comma], out double d)) pendingDur = d;
            }
            else if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal))
            {
                // Parse attributes BANDWIDTH, RESOLUTION, CODECS.
                foreach (var attr in line[18..].Split(','))
                {
                    var kv = attr.Split('=', 2);
                    if (kv.Length != 2) continue;
                    var v = kv[1].Trim().Trim('"');
                    if (kv[0].Trim() == "BANDWIDTH") int.TryParse(v, out pendingBw);
                    else if (kv[0].Trim() == "RESOLUTION")
                    {
                        var wh = v.Split('x');
                        if (wh.Length == 2) { int.TryParse(wh[0], out int w); int.TryParse(wh[1], out int h); pendingW = w; pendingH = h; }
                    }
                    else if (kv[0].Trim() == "CODECS") pendingCodecs = v;
                }
            }
            else if (!line.StartsWith('#'))
            {
                if (isMaster && pendingBw > 0)
                {
                    vars.Add(new HlsVariant(line, pendingBw, pendingW, pendingH, pendingCodecs));
                    pendingBw = 0; pendingW = pendingH = null; pendingCodecs = null;
                }
                else
                {
                    segs.Add(new HlsSegment(line, pendingDur, pendingDisc));
                    pendingDur = 0; pendingDisc = false;
                }
                pendingUri = line;
            }
        }

        var pl = new HlsPlaylist(isMaster, isLive, mediaSeq, targetDur, ptype, key);
        pl.Segments.AddRange(segs);
        pl.Variants.AddRange(vars);
        // Assign absolute media sequence numbers (used for AES-128 IVs).
        for (int i = 0; i < pl.Segments.Count; i++)
            pl.Segments[i] = pl.Segments[i] with { SequenceNumber = mediaSeq + i };
        return pl;
    }

    private static HlsKeyInfo ParseKey(string attrs)
    {
        string? method = null, uri = null, iv = null;
        foreach (var attr in attrs.Split(','))
        {
            var kv = attr.Split('=', 2);
            if (kv.Length != 2) continue;
            var v = kv[1].Trim().Trim('"');
            switch (kv[0].Trim())
            {
                case "METHOD": method = v; break;
                case "URI": uri = v; break;
                case "IV": iv = v; break;
            }
        }
        return new HlsKeyInfo(method ?? "NONE", uri, iv);
    }
}
