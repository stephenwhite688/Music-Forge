using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioForge.UI.Data;

/// <summary>A station record as stored in the Stations table.</summary>
public sealed class StationRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string StreamUrl { get; set; } = "";
    public string? HomepageUrl { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string Continent => CountryContinents.ForCountryCode(CountryCode);
    public string? Language { get; set; }
    public string? GenreTags { get; set; }
    public string? Codec { get; set; }
    public int? Bitrate { get; set; }
    public double Votes { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsFavourite { get; set; }
}

/// <summary>
/// Repository for the cached internet radio directory (Stations table).
/// The Radio Browser API dataset is far too large to keep in memory, so it is
/// persisted here once via "Load Directory" and browsed locally afterwards.
/// </summary>
public sealed class RadioRepository
{
    public RadioRepository()
    {
        EnsureSchema();
    }

    private static void EnsureSchema()
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        // Stations table predates country codes - add the column if missing
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA table_info(Stations);";
            using var reader = check.ExecuteReader();
            bool hasCountryCode = false;
            while (reader.Read())
                if (string.Equals(reader.GetString(1), "CountryCode", StringComparison.OrdinalIgnoreCase))
                    hasCountryCode = true;
            if (!hasCountryCode)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Stations ADD COLUMN CountryCode TEXT;";
                alter.ExecuteNonQuery();
            }
        }
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS RadioDirectoryMeta (
                                    Key TEXT PRIMARY KEY,
                                    Value TEXT);
                                CREATE TABLE IF NOT EXISTS RadioPlayHistory (
                                    Id INTEGER PRIMARY KEY,
                                    StationId INTEGER NOT NULL,
                                    StationName TEXT NOT NULL,
                                    StreamUrl TEXT NOT NULL,
                                    PlayedAt TEXT NOT NULL);
                                CREATE INDEX IF NOT EXISTS IX_RadioPlayHistory_Station ON RadioPlayHistory(StationId);
                                CREATE UNIQUE INDEX IF NOT EXISTS IX_Stations_StreamUrl
                                    ON Stations(StreamUrl);";
            cmd.ExecuteNonQuery();
        }
    }

    // ---------- Directory metadata ----------

    public DateTime? GetLastRefresh()
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM RadioDirectoryMeta WHERE Key = 'LastRefresh';";
        var raw = cmd.ExecuteScalar() as string;
        return raw is not null && DateTime.TryParse(raw, out var dt) ? dt.ToUniversalTime() : null;
    }

    public void SetLastRefresh(DateTime utc)
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO RadioDirectoryMeta (Key, Value) VALUES ('LastRefresh', $v)
                             ON CONFLICT(Key) DO UPDATE SET Value = $v;";
        cmd.Parameters.AddWithValue("$v", utc.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    // ---------- Station persistence ----------

    /// <summary>Inserts or updates a station keyed on StreamUrl + StationName.</summary>
    public void UpsertStation(StationRecord s)
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO Stations
            (StationName, StreamUrl, HomepageUrl, Country, CountryCode, Language, GenreTags, Codec, Bitrate, ReliabilityScore, LogoUrl)
            VALUES ($name,$url,$home,$country,$cc,$lang,$tags,$codec,$bitrate,$votes,$logo)
            ON CONFLICT(StreamUrl) DO UPDATE SET
                StationName=excluded.StationName, HomepageUrl=excluded.HomepageUrl,
                Country=excluded.Country, CountryCode=excluded.CountryCode, Language=excluded.Language,
                GenreTags=excluded.GenreTags, Codec=excluded.Codec, Bitrate=excluded.Bitrate,
                ReliabilityScore=excluded.ReliabilityScore, LogoUrl=excluded.LogoUrl;";
        cmd.Parameters.AddWithValue("$name", s.Name);
        cmd.Parameters.AddWithValue("$url", s.StreamUrl);
        cmd.Parameters.AddWithValue("$home", (object?)s.HomepageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$country", (object?)s.Country ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cc", (object?)s.CountryCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lang", (object?)s.Language ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tags", (object?)s.GenreTags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$codec", (object?)s.Codec ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$bitrate", (object?)s.Bitrate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$votes", s.Votes);
        cmd.Parameters.AddWithValue("$logo", (object?)s.LogoUrl ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Bulk-upserts stations inside one transaction. Returns rows written.</summary>
    public int UpsertStations(IEnumerable<StationRecord> stations)
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var tx = connection.BeginTransaction();
        int count = 0;
        foreach (var s in stations)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO Stations
                (StationName, StreamUrl, HomepageUrl, Country, CountryCode, Language, GenreTags, Codec, Bitrate, ReliabilityScore, LogoUrl)
                VALUES ($name,$url,$home,$country,$cc,$lang,$tags,$codec,$bitrate,$votes,$logo)
                ON CONFLICT(StreamUrl) DO UPDATE SET
                    StationName=excluded.StationName, HomepageUrl=excluded.HomepageUrl,
                    Country=excluded.Country, CountryCode=excluded.CountryCode, Language=excluded.Language,
                    GenreTags=excluded.GenreTags, Codec=excluded.Codec, Bitrate=excluded.Bitrate,
                    ReliabilityScore=excluded.ReliabilityScore, LogoUrl=excluded.LogoUrl;";
            cmd.Parameters.AddWithValue("$name", s.Name);
            cmd.Parameters.AddWithValue("$url", s.StreamUrl);
            cmd.Parameters.AddWithValue("$home", (object?)s.HomepageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$country", (object?)s.Country ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$cc", (object?)s.CountryCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lang", (object?)s.Language ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tags", (object?)s.GenreTags ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$codec", (object?)s.Codec ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bitrate", (object?)s.Bitrate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$votes", s.Votes);
            cmd.Parameters.AddWithValue("$logo", (object?)s.LogoUrl ?? DBNull.Value);
            count += cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return count;
    }

    // ---------- Queries ----------

    private const string StationSelect = @"SELECT s.Id, s.StationName, s.StreamUrl, s.HomepageUrl,
        s.Country, s.CountryCode, s.Language, s.GenreTags, s.Codec, s.Bitrate,
        s.ReliabilityScore, s.LogoUrl,
        CASE WHEN f.StationId IS NULL THEN 0 ELSE 1 END AS IsFav
        FROM Stations s LEFT JOIN RadioFavourites f ON f.StationId = s.Id";

    private static StationRecord ReadStation(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Name = r.GetString(1),
        StreamUrl = r.GetString(2),
        HomepageUrl = r.IsDBNull(3) ? null : r.GetString(3),
        Country = r.IsDBNull(4) ? null : r.GetString(4),
        CountryCode = r.IsDBNull(5) ? null : r.GetString(5),
        Language = r.IsDBNull(6) ? null : r.GetString(6),
        GenreTags = r.IsDBNull(7) ? null : r.GetString(7),
        Codec = r.IsDBNull(8) ? null : r.GetString(8),
        Bitrate = r.IsDBNull(9) ? null : r.GetInt32(9),
        Votes = r.IsDBNull(10) ? 0 : r.GetDouble(10),
        LogoUrl = r.IsDBNull(11) ? null : r.GetString(11),
        IsFavourite = r.GetInt32(12) != 0
    };

    /// <summary>All stations ordered by votes (popularity). Capped by <paramref name="limit"/>.</summary>
    public List<StationRecord> GetStations(int limit = 5000)
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + " ORDER BY s.ReliabilityScore DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        return list;
    }

    /// <summary>Stations for one country (by ISO code), most popular first.</summary>
    public List<StationRecord> GetStationsByCountry(string? countryCode, int limit = 2000)
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + " WHERE s.CountryCode = $cc ORDER BY s.ReliabilityScore DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$cc", countryCode ?? "");
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        return list;
    }

    /// <summary>Text search across station name, country, genre tags and codec.</summary>
    public List<StationRecord> SearchStations(string term, int limit = 1000)
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + @" WHERE s.StationName LIKE $q OR s.Country LIKE $q
            OR s.GenreTags LIKE $q OR s.Codec LIKE $q OR s.Language LIKE $q
            ORDER BY s.ReliabilityScore DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$q", "%" + term + "%");
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        return list;
    }

    /// <summary>Stations for one language, most popular first.</summary>
    public List<StationRecord> GetStationsByLanguage(string? language, int limit = 2000)
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + " WHERE s.Language = $lang ORDER BY s.ReliabilityScore DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$lang", language ?? "");
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        return list;
    }

    /// <summary>Stations whose genre tag list contains the given tag.</summary>
    public List<StationRecord> GetStationsByGenre(string tag, int limit = 2000)
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + " WHERE s.GenreTags LIKE $tag ORDER BY s.ReliabilityScore DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$tag", "%" + tag + "%");
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        return list;
    }

    /// <summary>Most recently cached stations (highest row Id first).</summary>
    public List<StationRecord> GetRecentlyAdded(int limit = 500)
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + " ORDER BY s.Id DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        return list;
    }

    /// <summary>Language -> station count for the directory tree.</summary>
    public Dictionary<string, int> GetLanguageCounts()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(NULLIF(Language,''),'(unknown)'), COUNT(*) FROM Stations GROUP BY Language;";
        using var r = cmd.ExecuteReader();
        while (r.Read()) result[r.GetString(0)] = r.GetInt32(1);
        return result;
    }

    /// <summary>Top genre tags by station count (tags are comma-separated lists).</summary>
    public Dictionary<string, int> GetTopGenres(int maxGenres = 100)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT GenreTags FROM Stations WHERE GenreTags IS NOT NULL AND GenreTags <> '';";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            foreach (var raw in r.GetString(0).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tag = raw.Trim();
                if (tag.Length < 2 || tag.Length > 30) continue;
                counts[tag] = counts.GetValueOrDefault(tag) + 1;
            }
        }
        return counts.OrderByDescending(kv => kv.Value).Take(maxGenres)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Favourited stations.</summary>
    public List<StationRecord> GetFavourites()
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + " WHERE f.StationId IS NOT NULL ORDER BY s.StationName;";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        return list;
    }

    /// <summary>Continent -> country code -> station count, for the directory tree.</summary>
    public Dictionary<string, Dictionary<string, int>> GetDirectoryTreeCounts()
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(NULLIF(CountryCode,''),'--'), COALESCE(NULLIF(Country,''),'(unknown)'), COUNT(*)
                             FROM Stations GROUP BY CountryCode, Country;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string cc = r.GetString(0);
            string country = r.GetString(1);
            int count = r.GetInt32(2);
            string continent = CountryContinents.ForCountryCode(cc == "--" ? null : cc);
            if (!result.TryGetValue(continent, out var countries))
                result[continent] = countries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // key by "CC|Country" so same-named countries in different regions stay distinct
            string key = cc == "--" ? country : $"{cc}|{country}";
            countries[key] = count;
        }
        return result;
    }

    public int StationCount()
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Stations;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ---------- Favourites ----------

    public void ToggleFavourite(int stationId)
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM RadioFavourites WHERE StationId = $id;";
        cmd.Parameters.AddWithValue("$id", stationId);
        bool exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        using var cmd2 = connection.CreateCommand();
        if (exists)
        {
            cmd2.CommandText = "DELETE FROM RadioFavourites WHERE StationId = $id;";
            cmd2.Parameters.AddWithValue("$id", stationId);
        }
        else
        {
            cmd2.CommandText = "INSERT INTO RadioFavourites (StationId, DateAdded) VALUES ($id, $date);";
            cmd2.Parameters.AddWithValue("$id", stationId);
            cmd2.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
        }
        cmd2.ExecuteNonQuery();
    }

    // ---------- Play history ----------

    /// <summary>Records a station play. Keeps at most 1000 rows (oldest pruned).</summary>
    public void RecordPlay(int stationId, string name, string streamUrl)
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO RadioPlayHistory (StationId, StationName, StreamUrl, PlayedAt)
                             VALUES ($id, $name, $url, $at);
                             DELETE FROM RadioPlayHistory WHERE Id NOT IN
                                 (SELECT Id FROM RadioPlayHistory ORDER BY Id DESC LIMIT 1000);";
        cmd.Parameters.AddWithValue("$id", stationId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$url", streamUrl);
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Recently played stations, most recent first (unique per station).</summary>
    public List<StationRecord> GetRecentlyPlayed(int limit = 200)
    {
        var list = new List<StationRecord>();
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = StationSelect + @" WHERE s.Id IN (
                                SELECT StationId FROM RadioPlayHistory GROUP BY StationId
                                ORDER BY MAX(PlayedAt) DESC LIMIT $limit)
                            ORDER BY s.StationName;";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadStation(r));
        // Re-sort by most recent play
        var lastPlayed = new Dictionary<int, DateTime>();
        using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "SELECT StationId, MAX(PlayedAt) FROM RadioPlayHistory GROUP BY StationId;";
        using (var r2 = cmd2.ExecuteReader())
            while (r2.Read())
                if (DateTime.TryParse(r2.GetString(1), out var dt))
                    lastPlayed[r2.GetInt32(0)] = dt;
        return list.OrderByDescending(s => lastPlayed.GetValueOrDefault(s.Id)).ToList();
    }

    /// <summary>Deletes all play history.</summary>
    public void ClearPlayHistory()
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM RadioPlayHistory;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>Returns the CountryCode for a station Id (for lookups).</summary>
    public string? GetStationCountryCode(int stationId)
    {
        using var connection = AudioForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CountryCode FROM Stations WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", stationId);
        return cmd.ExecuteScalar() as string;
    }
}
