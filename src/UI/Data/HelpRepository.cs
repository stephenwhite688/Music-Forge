using Microsoft.Data.Sqlite;

namespace MusicForge.UI.Data;

/// <summary>
/// Reads help articles from the HelpTopics table, seeding the built-in
/// articles on first run. Articles persist so future builds can add or
/// update them in the database.
/// </summary>
public sealed class HelpRepository
{
    public sealed record Topic(int Id, string Key, string Title, string Category, int SortOrder, string Content);

    /// <summary>Seeds built-in articles if the table is empty, then returns all topics ordered for display.</summary>
    public List<Topic> GetTopics()
    {
        EnsureSeeded();
        var list = new List<Topic>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, TopicKey, Title, Category, SortOrder, Content FROM HelpTopics ORDER BY SortOrder, Title;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Topic(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetString(5)));
        return list;
    }

    /// <summary>Returns topics whose title, category or content matches the query.</summary>
    public List<Topic> Search(string query)
    {
        var all = GetTopics();
        if (string.IsNullOrWhiteSpace(query)) return all;
        var q = query.Trim();
        return all.Where(t =>
            t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            t.Content.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Upserts the built-in articles so doc updates ship with new versions.</summary>
    private void EnsureSeeded()
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        foreach (var a in HelpArticles.All)
        {
            using var ins = connection.CreateCommand();
            ins.CommandText = """
                INSERT INTO HelpTopics (TopicKey, Title, Category, SortOrder, Content)
                VALUES ($k, $t, $c, $s, $b)
                ON CONFLICT(TopicKey) DO UPDATE SET
                    Title = excluded.Title,
                    Category = excluded.Category,
                    SortOrder = excluded.SortOrder,
                    Content = excluded.Content;
                """;
            ins.Parameters.AddWithValue("$k", a.Key);
            ins.Parameters.AddWithValue("$t", a.Title);
            ins.Parameters.AddWithValue("$c", a.Category);
            ins.Parameters.AddWithValue("$s", a.SortOrder);
            ins.Parameters.AddWithValue("$b", a.Content);
            ins.ExecuteNonQuery();
        }
    }
}
