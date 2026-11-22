using MusicForge.UI.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MusicForge.UI.Services;

/// <summary>Progress snapshot while the radio directory is being downloaded.</summary>
public sealed class RadioLoadProgress
{
    public int Fetched { get; set; }
    public int Saved { get; set; }
    public int EstimatedTotal { get; set; }
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// Downloads the Radio Browser station directory and caches it in the
/// Stations table. The API has no continent field - continent grouping is
/// derived from country codes at browse time via <see cref="CountryContinents"/>.
/// </summary>
public sealed class RadioBrowserService
{
    private const string ApiBase = "https://de1.api.radio-browser.info/json";
    private const int PageSize = 1000;
    private const int MaxStations = 60000; // safety cap - directory is ~50k

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly RadioRepository _repo = new();

    /// <summary>Total stations currently cached in the database.</summary>
    public int CachedStationCount => _repo.StationCount();

    /// <summary>Fetches the full station directory and upserts it into SQLite.</summary>
    /// <returns>Number of station rows written.</returns>
    public async Task<int> LoadDirectoryAsync(IProgress<RadioLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int estimatedTotal = await GetEstimatedTotalAsync(cancellationToken);
        int fetched = 0, saved = 0;

        for (int offset = 0; offset < MaxStations; offset += PageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string url = $"{ApiBase}/stations/search?hidebroken=true&order=votes&reverse=true&limit={PageSize}&offset={offset}";
            string json;
            try
            {
                json = await Http.GetStringAsync(url, cancellationToken);
            }
            catch (Exception ex) when (offset > 0)
            {
                // Mid-download failure - keep what we have rather than lose everything
                break;
            }

            var batch = ParseStations(json);
            if (batch.Count == 0) break;

            fetched += batch.Count;
            saved += _repo.UpsertStations(batch);

            progress?.Report(new RadioLoadProgress
            {
                Fetched = fetched,
                Saved = saved,
                EstimatedTotal = estimatedTotal,
                Elapsed = sw.Elapsed
            });

            if (batch.Count < PageSize) break; // last page
        }

        _repo.SetLastRefresh(DateTime.UtcNow);
        return saved;
    }

    /// <summary>Station count reported by the API's /stats endpoint (for ETA).</summary>
    private static async Task<int> GetEstimatedTotalAsync(CancellationToken ct)
    {
        try
        {
            string json = await Http.GetStringAsync($"{ApiBase}/stats", ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("stations", out var el) && el.TryGetInt32(out int n))
                return n;
        }
        catch { }
        return 30000; // fallback estimate
    }

    private static List<StationRecord> ParseStations(string json)
    {
        var list = new List<StationRecord>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            string? name = GetString(el, "name");
            string? streamUrl = GetString(el, "url_resolved") ?? GetString(el, "url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(streamUrl))
                continue;

            list.Add(new StationRecord
            {
                Name = name,
                StreamUrl = streamUrl,
                HomepageUrl = GetString(el, "homepage"),
                Country = GetString(el, "country"),
                CountryCode = GetString(el, "countrycode"),
                Language = GetString(el, "language"),
                GenreTags = GetString(el, "tags"),
                Codec = GetString(el, "codec"),
                Bitrate = GetInt(el, "bitrate"),
                Votes = GetDouble(el, "votes"),
                LogoUrl = GetString(el, "favicon")
            });
        }
        return list;
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() : null;

    private static int? GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int n)
            ? n : null;

    private static double GetDouble(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out double d)
            ? d : 0;
}
