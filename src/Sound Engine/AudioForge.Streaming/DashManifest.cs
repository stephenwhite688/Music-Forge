using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace AudioForge.Streaming;

/// <summary>
/// Parsed DASH MPD manifest: periods, adaptation sets, and resolved segment
/// URLs for the first audio representation.
/// </summary>
public sealed class DashManifest
{
    public sealed class Segment
    {
        public required string Url { get; init; }
        public double DurationSeconds { get; init; }
    }

    public sealed class AudioRepresentation
    {
        public required string MimeType { get; init; }
        public required int Width { get; init; } // unused; kept for symmetry
        public required IReadOnlyList<Segment> Segments { get; init; }
        public string? InitializationUrl { get; init; }
        public double Timescale { get; init; } = 1;
        public double DurationSeconds => Segments.Sum(s => s.DurationSeconds);
    }

    /// <summary>Fetches and parses an MPD. Returns null if no audio rep found.</summary>
    public static async Task<DashManifest?> ParseAsync(string manifestUrl, HttpClient http, CancellationToken ct = default)
    {
        string xml;
        using (var resp = await http.GetAsync(manifestUrl, ct))
        {
            resp.EnsureSuccessStatusCode();
            xml = await resp.Content.ReadAsStringAsync(ct);
        }
        return Parse(xml, manifestUrl);
    }

    public static DashManifest? Parse(string xml, string manifestUrl)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch { return null; }

        XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
        var mpd = doc.Root;
        if (mpd == null || mpd.Name.LocalName != "MPD") return null;

        var baseUri = new Uri(manifestUrl, UriKind.Absolute);
        var baseUris = mpd.Elements(ns + "BaseURL").Select(e => e.Value.Trim()).ToList();
        string Resolve(string u)
        {
            if (Uri.TryCreate(u, UriKind.Absolute, out _)) return u;
            if (Uri.TryCreate(baseUri, u, out var abs)) return abs!.ToString();
            foreach (var b in baseUris)
            {
                var combined = b.TrimEnd('/') + "/" + u.TrimStart('/');
                if (Uri.TryCreate(baseUri, combined, out var r2))
                    return r2!.ToString();
            }
            return u;
        }

        // Find first audio AdaptationSet.
        foreach (var period in mpd.Elements(ns + "Period"))
        {
            foreach (var aset in period.Elements(ns + "AdaptationSet"))
            {
                string? mime = aset.Attribute("mimeType")?.Value ?? "";
                string? contentType = aset.Attribute("contentType")?.Value ?? "";
                if (!mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) &&
                    !contentType.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    continue;

                var rep = aset.Elements(ns + "Representation").FirstOrDefault();
                if (rep == null) continue;

                var segments = new List<Segment>();
                string? initUrl = null;
                double timescale = 1;

                // SegmentTemplate with $Number$ or $Time$.
                var st = aset.Element(ns + "SegmentTemplate") ?? rep.Element(ns + "SegmentTemplate");
                if (st != null)
                {
                    string? media = st.Attribute("media")?.Value;
                    string? init = st.Attribute("initialization")?.Value;
                    string? timescaleStr = st.Attribute("timescale")?.Value;
                    if (timescaleStr != null && double.TryParse(timescaleStr, out double ts)) timescale = ts;
                    if (init != null) initUrl = Resolve(ExpandTemplate(init, rep, 0));
                    if (media != null)
                    {
                        int startNum = ParseInt(st.Attribute("startNumber")?.Value, 1);
                        // duration in timescale units
                        double? dur = ParseDouble(st.Attribute("duration")?.Value);
                        int? count = ParseInt(st.Attribute("availabilityTimeOffset")?.Value, (int?)null);
                        // For VOD, SegmentTimeline may bound the count.
                        var timeline = st.Element(ns + "SegmentTimeline");
                        if (timeline != null)
                        {
                            foreach (var s in timeline.Elements(ns + "S"))
                            {
                                int num = ParseInt(s.Attribute("t")?.Value, startNum);
                                double d = (ParseDouble(s.Attribute("d")?.Value) ?? 0) / timescale;
                                segments.Add(new Segment { Url = Resolve(ExpandTemplate(media, rep, num)), DurationSeconds = d });
                            }
                        }
                        else if (dur.HasValue && dur.Value > 0)
                        {
                            // mediaPresentationDuration is ISO-8601 (PT..S).
                            double? total = ParseIso8601Duration(
                                mpd.Attribute("mediaPresentationDuration")?.Value);
                            double segDurSec = dur.Value / timescale;
                            int n = total.HasValue ? (int)Math.Ceiling(total.Value / segDurSec) : 0;
                            if (n > 0)
                            {
                                for (int i = 0; i < n; i++)
                                    segments.Add(new Segment
                                    {
                                        Url = Resolve(ExpandTemplate(media, rep, startNum + i)),
                                        DurationSeconds = segDurSec
                                    });
                            }
                        }
                    }
                }

                // SegmentList with explicit SegmentURLs.
                var sl = aset.Element(ns + "SegmentList") ?? rep.Element(ns + "SegmentList");
                if (sl != null)
                {
                    if (initUrl == null)
                        initUrl = sl.Element(ns + "Initialization")?.Attribute("sourceURL")?.Value is { } iu
                            ? Resolve(iu) : null;
                    double? dur = ParseDouble(sl.Attribute("duration")?.Value);
                    if (dur.HasValue && timescale != 1) dur = dur / timescale;
                    foreach (var su in sl.Elements(ns + "SegmentURL"))
                    {
                        var m = su.Attribute("media")?.Value;
                        if (m == null) continue;
                        segments.Add(new Segment
                        {
                            Url = Resolve(m),
                            DurationSeconds = dur ?? 0
                        });
                    }
                }

                // Single-file BaseURL (progressive MP4/AAC).
                if (segments.Count == 0)
                {
                    var baseUrl = rep.Element(ns + "BaseURL")?.Value ?? aset.Element(ns + "BaseURL")?.Value;
                    if (!string.IsNullOrEmpty(baseUrl))
                        segments.Add(new Segment { Url = Resolve(baseUrl), DurationSeconds = 0 });
                }

                if (segments.Count == 0) continue;

                return new DashManifest
                {
                    Representation = new AudioRepresentation
                    {
                        MimeType = mime,
                        Width = 0,
                        Segments = segments,
                        InitializationUrl = initUrl,
                        Timescale = timescale
                    }
                };
            }
        }
        return null;
    }

    private static string ExpandTemplate(string template, XElement rep, int number)
    {
        string t = template;
        t = t.Replace("$RepresentationID$", rep.Attribute("id")?.Value ?? rep.Attribute("bandwidth")?.Value ?? "1");
        t = t.Replace("$Number$", number.ToString());
        t = t.Replace("$Bandwidth$", rep.Attribute("bandwidth")?.Value ?? "0");
        return t;
    }

    private static int ParseInt(string? s, int fallback) =>
        int.TryParse(s, out int v) ? v : fallback;
    private static int? ParseInt(string? s, int? fallback) =>
        int.TryParse(s, out int v) ? v : fallback;
    private static double? ParseDouble(string? s) =>
        double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;

    /// <summary>Parses ISO-8601 durations like PT634.566S, PT1M30S, P1DT2H.</summary>
    public static double? ParseIso8601Duration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s[0] != 'P') return null;
        double total = 0;
        bool inTime = false;
        int i = 1;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == 'T') { inTime = true; i++; continue; }
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i == start || i >= s.Length) return null;
            if (!double.TryParse(s.AsSpan(start, i - start),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
                return null;
            double factor = s[i] switch
            {
                'D' => 86400,
                'H' when inTime => 3600,
                'M' when inTime => 60,
                'S' when inTime => 1,
                _ => -1
            };
            if (factor < 0) return null;
            total += v * factor;
            i++;
        }
        return total;
    }

    public required AudioRepresentation Representation { get; init; }
}
