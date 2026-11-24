using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Sinapse.Core;

public sealed record SearchHit(string Path, string Heading, string Tipo, string Text, double Score);

public sealed class IndexNotFoundException(string dbPath)
    : Exception($"Índice não encontrado: {dbPath}");

public static partial class Search
{
    [GeneratedRegex(@"\w{3,}")]
    private static partial Regex TermRx();

    public static IReadOnlyList<SearchHit> Run(string dbPath, string question, int k = 6, string? tipo = null, string? pasta = null)
    {
        if (!File.Exists(dbPath)) throw new IndexNotFoundException(dbPath);
        var terms = TermRx().Matches(question).Select(m => $"\"{m.Value}\"").ToList();
        if (terms.Count == 0) return [];

        var sql = "SELECT c.path, c.heading, c.tipo, c.text, bm25(fts,5,3,1) s FROM fts JOIN chunks c ON c.id=fts.rowid " +
                  "WHERE fts MATCH $m" + (string.IsNullOrEmpty(tipo) ? "" : " AND c.tipo=$tipo") +
                  (string.IsNullOrEmpty(pasta) ? "" : " AND c.path LIKE $pasta || '%'") +
                  " ORDER BY s, c.id LIMIT $k";

        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        using var con = new SqliteConnection(cs.ConnectionString); // closed per query so the indexer can replace the file
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$m", string.Join(" OR ", terms));
        if (!string.IsNullOrEmpty(tipo)) cmd.Parameters.AddWithValue("$tipo", tipo);
        if (!string.IsNullOrEmpty(pasta)) cmd.Parameters.AddWithValue("$pasta", pasta);
        cmd.Parameters.AddWithValue("$k", Math.Clamp(k, 1, 20));

        var hits = new List<SearchHit>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            hits.Add(new SearchHit(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), Math.Round(-r.GetDouble(4), 2)));
        return hits;
    }
}
