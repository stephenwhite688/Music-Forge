using Microsoft.Data.Sqlite;
using MusicForge.UI.Shared;
using System;

namespace MusicForge.UI.Data;

/// <summary>
/// Persists the active UI theme as JSON in the Themes table
/// (ColourScheme column holds the serialised <see cref="ThemeModel"/>).
/// </summary>
public sealed class ThemeRepository
{
    private const string CurrentThemeName = "__current__";

    /// <summary>Saves the current theme. Called whenever the theme changes.</summary>
    public void SaveCurrent()
    {
        string json = ThemeSerializer.ToJson(ThemeManager.Current);
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT Id FROM Themes WHERE ThemeName = $name LIMIT 1;";
            check.Parameters.AddWithValue("$name", CurrentThemeName);
            if (check.ExecuteScalar() is long existing)
            {
                using var upd = connection.CreateCommand();
                upd.CommandText = "UPDATE Themes SET ColourScheme = $json WHERE Id = $id;";
                upd.Parameters.AddWithValue("$json", json);
                upd.Parameters.AddWithValue("$id", (int)existing);
                upd.ExecuteNonQuery();
                return;
            }
        }
        using var ins = connection.CreateCommand();
        ins.CommandText = "INSERT INTO Themes (ThemeName, ColourScheme) VALUES ($name, $json);";
        ins.Parameters.AddWithValue("$name", CurrentThemeName);
        ins.Parameters.AddWithValue("$json", json);
        ins.ExecuteNonQuery();
    }

    /// <summary>Loads the saved theme, or null if none exists / it's invalid.</summary>
    public ThemeModel? LoadCurrent()
    {
        try
        {
            using var connection = MusicForgeDatabase.Instance.OpenConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT ColourScheme FROM Themes WHERE ThemeName = $name LIMIT 1;";
            cmd.Parameters.AddWithValue("$name", CurrentThemeName);
            var json = cmd.ExecuteScalar() as string;
            return json is null ? null : ThemeSerializer.FromJson(json);
        }
        catch { return null; }
    }

    /// <summary>Lists saved named themes (user-exported ones).</summary>
    public System.Collections.Generic.List<(int Id, string Name)> ListThemes()
    {
        var list = new System.Collections.Generic.List<(int, string)>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, ThemeName FROM Themes WHERE ThemeName <> $cur ORDER BY ThemeName;";
        cmd.Parameters.AddWithValue("$cur", CurrentThemeName);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetInt32(0), r.GetString(1)));
        return list;
    }
}
