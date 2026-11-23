using Microsoft.Data.Sqlite;
using System;

namespace MusicForge.UI.Data;

/// <summary>
/// Key/value access to the single-row GeneralSettings table
/// (StartupMode, SelectedTheme, InterfaceLayout, LastOpenedMode).
/// </summary>
public sealed class SettingsRepository
{
    private const string RowId = "1";

    public string? Get(string column)
    {
        if (column is not ("StartupMode" or "SelectedTheme" or "InterfaceLayout" or "LastOpenedMode"))
            throw new ArgumentException($"Unknown settings column: {column}", nameof(column));

        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {column} FROM GeneralSettings WHERE Id = {RowId} LIMIT 1;";
        return cmd.ExecuteScalar() as string;
    }

    public void Set(string column, string? value)
    {
        if (column is not ("StartupMode" or "SelectedTheme" or "InterfaceLayout" or "LastOpenedMode"))
            throw new ArgumentException($"Unknown settings column: {column}", nameof(column));

        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"UPDATE GeneralSettings SET {column} = $v WHERE Id = {RowId};";
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
