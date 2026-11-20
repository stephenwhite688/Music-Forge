using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AudioForge.UI.Data;

/// <summary>
/// SQLite database manager for AudioForge. Singleton — call
/// <see cref="Instance"/> then <see cref="Initialize"/> once at startup.
/// Database lives at %LOCALAPPDATA%\AudioForge\audioforge.db.
/// </summary>
public sealed class AudioForgeDatabase
{
    public static AudioForgeDatabase Instance { get; } = new AudioForgeDatabase();

    public string DatabasePath { get; }
    private readonly string _connectionString;

    private AudioForgeDatabase()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AudioForge");
        Directory.CreateDirectory(directory);

        DatabasePath = Path.Combine(directory, "audioforge.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Private cache: shared-cache mode uses table locks that cause
            // SQLITE_LOCKED "database table is locked" under concurrency.
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = 30 // seconds - wait instead of failing on brief locks
        };
        _connectionString = builder.ConnectionString;
    }

    /// <summary>Opens a new connection. Caller owns disposal.</summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // busy_timeout is per-connection: without this every new connection
        // fails instantly with SQLITE_BUSY when a writer (e.g. the library
        // scanner) holds the write lock. Wait up to 30s instead.
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA busy_timeout = 30000;";
            cmd.ExecuteNonQuery();
        }
        return connection;
    }

    /// <summary>Creates the database file, applies pragmas and the schema.</summary>
    public void Initialize()
    {
        using var connection = OpenConnection();

        // Performance pragmas: WAL for concurrent read/write, large cache,
        // memory-mapped I/O, memory temp store.
        Execute(connection, "PRAGMA journal_mode = WAL;");
        Execute(connection, "PRAGMA synchronous = NORMAL;");
        Execute(connection, "PRAGMA foreign_keys = ON;");
        Execute(connection, "PRAGMA cache_size = -64000;");   // ~64 MB page cache
        Execute(connection, "PRAGMA mmap_size = 268435456;");  // 256 MB memory map
        Execute(connection, "PRAGMA temp_store = MEMORY;");
        Execute(connection, "PRAGMA busy_timeout = 30000;");

        Execute(connection, Schema.Sql);
    }

    /// <summary>Runs a non-query command (DDL, INSERT/UPDATE/DELETE batches).</summary>
    public static int Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates a consistent backup of the whole database at the given path
    /// using SQLite's VACUUM INTO (safe while the app is running).
    /// </summary>
    public void Backup(string destinationPath)
    {
        string escaped = destinationPath.Replace("'", "''");
        using var connection = OpenConnection();
        Execute(connection, $"VACUUM INTO '{escaped}';");
    }

    /// <summary>
    /// Restores the database from a backup file produced by <see cref="Backup"/>.
    /// WARNING: replaces the entire database — existing data is lost.
    /// </summary>
    public void Restore(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Backup file not found.", sourcePath);

        using (var connection = OpenConnection())
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE);");

        File.Copy(sourcePath, DatabasePath, overwrite: true);

        // Remove stale WAL/SHM sidecars from the previous database
        foreach (string sidecar in new[] { DatabasePath + "-wal", DatabasePath + "-shm" })
        {
            if (File.Exists(sidecar))
                File.Delete(sidecar);
        }
    }
}
