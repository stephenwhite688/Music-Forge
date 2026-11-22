using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MusicForge.UI.Data;

/// <summary>
/// Repository for the media library: libraries, sources, tracks, playlists.
/// All SQL lives here - UI code calls these methods, never raw SQL.
/// </summary>
public sealed class LibraryRepository
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".ogg", ".oga", ".wav", ".wave", ".m4a", ".aac", ".adts",
        ".wma", ".alac", ".caf", ".opus", ".dsf", ".dff"
    };

    private const string DefaultLibraryName = "Default Library";

    // ---------- Libraries & Sources ----------

    /// <summary>Returns the Id of the default library, creating it if needed.</summary>
    public int EnsureDefaultLibrary()
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Libraries WHERE LibraryName = $name LIMIT 1;";
            cmd.Parameters.AddWithValue("$name", DefaultLibraryName);
            var existing = cmd.ExecuteScalar();
            if (existing is long id) return (int)id;
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO Libraries (LibraryName, CreationDate, LibraryVersion)
                                 VALUES ($name, $date, 1); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$name", DefaultLibraryName);
            cmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
            return (int)(long)cmd.ExecuteScalar()!;
        }
    }

    /// <summary>Adds a folder source to the default library. Returns the new source Id.</summary>
    public int AddSource(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path required.", nameof(folderPath));

        int libraryId = EnsureDefaultLibrary();
        string fullPath = Path.GetFullPath(folderPath);
        string drive = Path.GetPathRoot(fullPath) ?? string.Empty;

        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO LibrarySources (LibraryId, DriveLetter, FolderPath, SourceType, DateAdded)
                            VALUES ($lib, $drive, $path, 'Folder', $date);
                            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$lib", libraryId);
        cmd.Parameters.AddWithValue("$drive", drive);
        cmd.Parameters.AddWithValue("$path", fullPath);
        cmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
        return (int)(long)cmd.ExecuteScalar()!;
    }

    /// <summary>Removes ALL configured source folders (tracks/playlists are kept).</summary>
    public void ClearSources()
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM LibrarySources;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>All configured source folder paths.</summary>
    public List<string> GetSourcePaths()
    {
        var paths = new List<string>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT s.FolderPath FROM LibrarySources s
                            JOIN Libraries l ON l.Id = s.LibraryId
                            WHERE l.LibraryName = $name;";
        cmd.Parameters.AddWithValue("$name", DefaultLibraryName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            paths.Add(reader.GetString(0));
        return paths;
    }

    // ---------- Tracks ----------

    /// <summary>
    /// Scans all configured sources for audio files and inserts/updates tracks
    /// with metadata read from file tags (title, artist, album, genre, year,
    /// track number, duration). Returns the number of newly added tracks.
    /// Runs on a background thread.
    /// </summary>
    public Task<int> ScanLibraryAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sources = GetSourcePaths();

            // Phase 1: enumerate files and read tags with NO write transaction
            // held - TagLib file I/O is slow and must not keep the DB locked.
            var pending = new List<(string Path, TrackMeta Meta, long Size)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var readConnection = MusicForgeDatabase.Instance.OpenConnection())
            {
                foreach (string source in sources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Directory.Exists(source)) continue;

                    IEnumerable<string> files;
                    try
                    {
                        var options = new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                            AttributesToSkip = FileAttributes.ReparsePoint
                        };
                        files = Directory.EnumerateFiles(source, "*", options)
                            .Where(f => AudioExtensions.Contains(Path.GetExtension(f)));
                    }
                    catch (Exception) { continue; } // inaccessible folder - skip

                    foreach (string file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            if (!seen.Add(file) || TrackExists(readConnection, null, file)) continue;
                            pending.Add((file, ReadTags(file), new FileInfo(file).Length));
                        }
                        catch { /* skip unreadable file */ }
                    }
                }
            }

            // Phase 2: write in small batches so UI writes get windows.
            int added = 0;
            using var connection = MusicForgeDatabase.Instance.OpenConnection();
            const int BatchSize = 200;
            int sinceCommit = 0;
            var tx = connection.BeginTransaction();
            try
            {
            foreach (var (path, meta, size) in pending)
            {
                try
                {
                    EnsureTrackRow(connection, tx, path, meta, size);
                    added++;
                }
                catch { /* skip unreadable file */ }

                if (++sinceCommit >= BatchSize)
                {
                    tx.Commit();
                    tx.Dispose();
                    tx = connection.BeginTransaction();
                    sinceCommit = 0;
                }
            }

            // Stamp the library's last scan date
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE Libraries SET LastScanDate = $date WHERE LibraryName = $name;";
                cmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("$name", DefaultLibraryName);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return added;
            }
            finally { tx.Dispose(); }
        }, cancellationToken);
    }

    // ---------- Tag reading & metadata upserts (used by ScanLibraryAsync) ----------

    private sealed class TrackMeta
    {
        public string? Title, Artist, Album, Genre;
        public int? Year, TrackNumber;
        public double? DurationSeconds;
    }

    /// <summary>Reads ID3/Vorbis/FLAC/etc tags via TagLibSharp. Never throws.</summary>
    private static TrackMeta ReadTags(string path)
    {
        var meta = new TrackMeta();
        try
        {
            using var file = TagLib.File.Create(path);
            var tag = file.Tag;
            meta.Title = NullIfEmpty(tag.Title);
            meta.Artist = NullIfEmpty(tag.FirstPerformer)
                ?? NullIfEmpty(tag.AlbumArtists.Length > 0 ? string.Join(", ", tag.AlbumArtists) : null);
            meta.Album = NullIfEmpty(tag.Album);
            meta.Genre = NullIfEmpty(tag.FirstGenre);
            meta.Year = tag.Year > 0 ? (int)tag.Year : null;
            meta.TrackNumber = tag.Track > 0 ? (int)tag.Track : null;
            var dur = file.Properties.Duration;
            meta.DurationSeconds = dur > TimeSpan.Zero ? dur.TotalSeconds : null;
        }
        catch { /* unreadable/unsupported tags - filename fallback applies */ }
        return meta;
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Upserts a single audio file into Tracks (with tag metadata) and returns
    /// its track Id. Used by scan and by ad-hoc folder/file additions.
    /// </summary>
    private static int EnsureTrackRow(SqliteConnection connection, SqliteTransaction tx, string file,
        TrackMeta? preReadMeta = null, long? preReadSize = null)
    {
        var meta = preReadMeta ?? ReadTags(file);
        long size = preReadSize ?? new FileInfo(file).Length;

        int? artistId = UpsertArtist(connection, tx, meta.Artist);
        int? albumId = UpsertAlbum(connection, tx, meta.Album, artistId, meta.Year, meta.Genre);

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO Tracks
            (Title, ArtistId, AlbumId, Genre, Year, TrackNumber, DurationSeconds,
             FilePath, FileSize, DateAdded)
            VALUES ($title, $artist, $album, $genre, $year, $trackno, $duration,
                    $path, $size, $date)
            ON CONFLICT(FilePath) DO UPDATE SET
                Title = excluded.Title,
                ArtistId = excluded.ArtistId,
                AlbumId = excluded.AlbumId,
                Genre = excluded.Genre,
                Year = excluded.Year,
                TrackNumber = excluded.TrackNumber,
                DurationSeconds = excluded.DurationSeconds,
                FileSize = excluded.FileSize;";
        cmd.Parameters.AddWithValue("$title", meta.Title ?? Path.GetFileNameWithoutExtension(file));
        cmd.Parameters.AddWithValue("$artist", (object?)artistId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$album", (object?)albumId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$genre", (object?)meta.Genre ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$year", (object?)meta.Year ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$trackno", (object?)meta.TrackNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$duration", (object?)meta.DurationSeconds ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$path", file);
        cmd.Parameters.AddWithValue("$size", size);
        cmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        using var idCmd = connection.CreateCommand();
        idCmd.Transaction = tx;
        idCmd.CommandText = "SELECT Id FROM Tracks WHERE FilePath = $path;";
        idCmd.Parameters.AddWithValue("$path", file);
        return (int)(long)idCmd.ExecuteScalar()!;
    }

    /// <summary>Ensures a single audio file exists in Tracks; returns its Id.</summary>
    public int EnsureTrackId(string filePath)
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var tx = connection.BeginTransaction();
        int id = EnsureTrackRow(connection, tx, filePath);
        tx.Commit();
        return id;
    }

    /// <summary>
    /// Recursively adds every audio file under <paramref name="folderPath"/>
    /// to the library (with tags) and appends them to the playlist.
    /// Returns the number of tracks added.
    /// </summary>
    public int AddFolderToPlaylist(int playlistId, string folderPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        var files = Directory.EnumerateFiles(folderPath, "*", options)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var tx = connection.BeginTransaction();
        int added = 0;
        foreach (string file in files)
        {
            try
            {
                int trackId = EnsureTrackRow(connection, tx, file);
                AddTrackToPlaylistOn(connection, tx, playlistId, trackId);
                added++;
            }
            catch { /* skip unreadable file */ }
        }
        tx.Commit();
        return added;
    }

    /// <summary>Adds a single audio file to the library and playlist.</summary>
    public int AddFileToPlaylist(int playlistId, string filePath)
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var tx = connection.BeginTransaction();
        int trackId = EnsureTrackRow(connection, tx, filePath);
        AddTrackToPlaylistOn(connection, tx, playlistId, trackId);
        tx.Commit();
        return trackId;
    }

    private static void AddTrackToPlaylistOn(SqliteConnection connection, SqliteTransaction tx,
        int playlistId, int trackId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(MAX(PlaylistPosition), -1) + 1 FROM PlaylistTracks WHERE PlaylistId = $pid;";
        cmd.Parameters.AddWithValue("$pid", playlistId);
        int position = Convert.ToInt32(cmd.ExecuteScalar());

        using var ins = connection.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = @"INSERT OR IGNORE INTO PlaylistTracks (PlaylistId, TrackId, PlaylistPosition)
            VALUES ($pid, $tid, $pos);";
        ins.Parameters.AddWithValue("$pid", playlistId);
        ins.Parameters.AddWithValue("$tid", trackId);
        ins.Parameters.AddWithValue("$pos", position);
        ins.ExecuteNonQuery();
    }

    private static bool TrackExists(SqliteConnection connection, SqliteTransaction? tx, string path)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM Tracks WHERE FilePath = $path LIMIT 1;";
        cmd.Parameters.AddWithValue("$path", path);
        return cmd.ExecuteScalar() is not null;
    }

    private static int? UpsertArtist(SqliteConnection connection, SqliteTransaction tx, string? name)
    {
        if (name is null) return null;
        int? id = FindByName(connection, tx, "Artists", "ArtistName", name);
        if (id is not null) return id;
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Artists (ArtistName) VALUES ($n); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", name);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    private static int? UpsertAlbum(SqliteConnection connection, SqliteTransaction tx,
        string? name, int? artistId, int? year, string? genre)
    {
        if (name is null) return null;
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"SELECT Id FROM Albums WHERE AlbumName = $n
                AND (ArtistId IS $artist OR (ArtistId IS NULL AND $artist IS NULL)) LIMIT 1;";
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$artist", (object?)artistId ?? DBNull.Value);
            var found = cmd.ExecuteScalar();
            if (found is long fid) return (int)fid;
        }
        using var ins = connection.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = @"INSERT INTO Albums (AlbumName, ArtistId, Year, Genre)
            VALUES ($n, $artist, $year, $genre); SELECT last_insert_rowid();";
        ins.Parameters.AddWithValue("$n", name);
        ins.Parameters.AddWithValue("$artist", (object?)artistId ?? DBNull.Value);
        ins.Parameters.AddWithValue("$year", (object?)year ?? DBNull.Value);
        ins.Parameters.AddWithValue("$genre", (object?)genre ?? DBNull.Value);
        return (int)(long)ins.ExecuteScalar()!;
    }

    private static int? FindByName(SqliteConnection connection, SqliteTransaction tx,
        string table, string column, string name)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT Id FROM {table} WHERE {column} = $n LIMIT 1;";
        cmd.Parameters.AddWithValue("$n", name);
        var found = cmd.ExecuteScalar();
        return found is long id ? (int)id : null;
    }

    /// <summary>Total track count in the library.</summary>
    public int GetTrackCount()
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Tracks;";
        return (int)(long)cmd.ExecuteScalar()!;
    }

    /// <summary>First N track file paths (for quick playback testing).</summary>
    /// <summary>
    /// Records a play of a file-backed track: increments PlayCount and sets
    /// LastPlayed. No-op when the path isn't in the library.
    /// </summary>
    public void RecordPlay(string filePath)
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"UPDATE Tracks SET PlayCount = PlayCount + 1, LastPlayed = $now
                            WHERE FilePath = $path;";
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$path", filePath);
        cmd.ExecuteNonQuery();
    }

    public List<string> GetTrackPaths(int limit = 100)
    {
        var paths = new List<string>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FilePath FROM Tracks ORDER BY Title LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            paths.Add(reader.GetString(0));
        return paths;
    }

    /// <summary>All tracks in the library with metadata, ordered by album/track/title.</summary>
    public List<LibraryTrack> GetAllTracks()
    {
        var tracks = new List<LibraryTrack>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT t.Id, t.Title, t.FilePath, t.FileSize,
                t.DurationSeconds, t.TrackNumber, t.Year,
                ar.ArtistName, al.AlbumName
            FROM Tracks t
            LEFT JOIN Artists ar ON ar.Id = t.ArtistId
            LEFT JOIN Albums al ON al.Id = t.AlbumId
            ORDER BY ar.ArtistName, al.AlbumName, t.TrackNumber, t.Title;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tracks.Add(new LibraryTrack(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return tracks;
    }

    /// <summary>A library track with its joined artist/album metadata.</summary>
    public sealed record LibraryTrack(
        int Id, string Title, string FilePath, long FileSize,
        double? DurationSeconds, int? TrackNumber, int? Year,
        string? ArtistName, string? AlbumName);

    // ---------- Playlists ----------

    /// <summary>Creates a playlist. Returns the new playlist Id.</summary>
    public int CreatePlaylist(string name)
    {
        string now = DateTime.UtcNow.ToString("o");
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO Playlists (PlaylistName, CreationDate, ModifiedDate)
                            VALUES ($name, $now, $now);
                            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$now", now);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    /// <summary>Deletes a playlist and its track entries.</summary>
    public void DeletePlaylist(int playlistId)
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var tx = connection.BeginTransaction();
        foreach (var sql in new[]
        {
            "DELETE FROM PlaylistTracks WHERE PlaylistId = $id;",
            "DELETE FROM Playlists WHERE Id = $id;"
        })
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$id", playlistId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>All playlist names with their Ids.</summary>
    public List<(int Id, string Name)> GetPlaylists()
    {
        var list = new List<(int, string)>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, PlaylistName FROM Playlists ORDER BY PlaylistName;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    /// <summary>Adds a track to the end of a playlist.</summary>
    public void AddTrackToPlaylist(int playlistId, int trackId)
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var tx = connection.BeginTransaction();

        int position;
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT COALESCE(MAX(PlaylistPosition), -1) + 1 FROM PlaylistTracks WHERE PlaylistId = $pid;";
            cmd.Parameters.AddWithValue("$pid", playlistId);
            position = (int)(long)cmd.ExecuteScalar()!;
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT OR IGNORE INTO PlaylistTracks (PlaylistId, TrackId, PlaylistPosition)
                                VALUES ($pid, $tid, $pos);";
            cmd.Parameters.AddWithValue("$pid", playlistId);
            cmd.Parameters.AddWithValue("$tid", trackId);
            cmd.Parameters.AddWithValue("$pos", position);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE Playlists SET ModifiedDate = $now WHERE Id = $pid;";
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$pid", playlistId);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    /// <summary>Track file paths in playlist order - ready for playback.</summary>
    public List<string> GetPlaylistTrackPaths(int playlistId)
    {
        var paths = new List<string>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT t.FilePath FROM PlaylistTracks pt
                            JOIN Tracks t ON t.Id = pt.TrackId
                            WHERE pt.PlaylistId = $pid
                            ORDER BY pt.PlaylistPosition;";
        cmd.Parameters.AddWithValue("$pid", playlistId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            paths.Add(reader.GetString(0));
        return paths;
    }

    /// <summary>Tracks in a playlist with IDs, in playlist order.</summary>
    public List<(int TrackId, string Title, string FilePath)> GetPlaylistTracks(int playlistId)
    {
        var tracks = new List<(int, string, string)>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT t.Id, t.Title, t.FilePath FROM PlaylistTracks pt
                            JOIN Tracks t ON t.Id = pt.TrackId
                            WHERE pt.PlaylistId = $pid
                            ORDER BY pt.PlaylistPosition;";
        cmd.Parameters.AddWithValue("$pid", playlistId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tracks.Add((reader.GetInt32(0), reader.GetString(1) ?? "", reader.GetString(2)));
        return tracks;
    }

    /// <summary>Playlist tracks with full metadata (artist/album/duration), in playlist order.</summary>
    public List<LibraryTrack> GetPlaylistTrackDetails(int playlistId)
    {
        var tracks = new List<LibraryTrack>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT t.Id, t.Title, t.FilePath, t.FileSize,
                t.DurationSeconds, t.TrackNumber, t.Year,
                ar.ArtistName, al.AlbumName
            FROM PlaylistTracks pt
            JOIN Tracks t ON t.Id = pt.TrackId
            LEFT JOIN Artists ar ON ar.Id = t.ArtistId
            LEFT JOIN Albums al ON al.Id = t.AlbumId
            WHERE pt.PlaylistId = $pid
            ORDER BY pt.PlaylistPosition;";
        cmd.Parameters.AddWithValue("$pid", playlistId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tracks.Add(new LibraryTrack(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return tracks;
    }

    /// <summary>Removes a track from a playlist and re-packs positions.</summary>
    public void RemoveTrackFromPlaylist(int playlistId, int trackId)
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var tx = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM PlaylistTracks WHERE PlaylistId = $pid AND TrackId = $tid;";
            cmd.Parameters.AddWithValue("$pid", playlistId);
            cmd.Parameters.AddWithValue("$tid", trackId);
            cmd.ExecuteNonQuery();
        }

        // Re-pack positions so they stay contiguous
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"UPDATE PlaylistTracks SET PlaylistPosition = (
                                    SELECT COUNT(*) FROM PlaylistTracks p2
                                    WHERE p2.PlaylistId = PlaylistTracks.PlaylistId
                                      AND p2.PlaylistPosition < PlaylistTracks.PlaylistPosition)
                                WHERE PlaylistId = $pid;";
            cmd.Parameters.AddWithValue("$pid", playlistId);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE Playlists SET ModifiedDate = $now WHERE Id = $pid;";
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$pid", playlistId);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    /// <summary>Track Ids matching a title search (for adding to playlists).</summary>
    public List<(int Id, string Title, string FilePath)> SearchTracks(string text, int limit = 200)
    {
        var results = new List<(int, string, string)>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT Id, Title, FilePath FROM Tracks
                            WHERE Title LIKE $q OR FilePath LIKE $q
                            ORDER BY Title LIMIT $limit;";
        cmd.Parameters.AddWithValue("$q", "%" + text + "%");
        cmd.Parameters.AddWithValue("$limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetInt32(0), reader.GetString(1) ?? "", reader.GetString(2)));
        return results;
    }
}
