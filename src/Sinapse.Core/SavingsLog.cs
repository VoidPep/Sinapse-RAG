using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace Sinapse.Core;

/// <summary>
/// One JSON line per search, same shape as the prototype's savings.jsonl.
/// Token counts are approximate (chars / 4); the baseline is "reading the whole notes once".
/// </summary>
public static class SavingsLog
{
    static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Never throws: a logging failure must not break the search.</summary>
    public static void Record(string logPath, string dbPath, string question, IReadOnlyList<SearchHit> hits)
    {
        try
        {
            var paths = hits.Select(h => h.Path).Distinct().Order(StringComparer.Ordinal).ToList();
            long manual = 0;
            if (paths.Count > 0)
            {
                var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
                using var con = new SqliteConnection(cs.ConnectionString);
                con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = $"SELECT sum(length(text)) FROM chunks WHERE path IN ({string.Join(",", paths.Select((_, i) => "$p" + i))})";
                for (var i = 0; i < paths.Count; i++) cmd.Parameters.AddWithValue("$p" + i, paths[i]);
                manual = cmd.ExecuteScalar() is long l ? l : 0;
            }
            var used = hits.Sum(h => (long)h.Text.Length);
            var rec = new JsonObject
            {
                ["ts"] = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                ["pergunta"] = question,
                ["notas"] = paths.Count,
                ["returned_tokens"] = used / 4,
                ["manual_tokens"] = manual / 4,
                ["saved_tokens"] = Math.Max(0, manual - used) / 4,
            };
            File.AppendAllText(logPath, rec.ToJsonString(Json) + "\n");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"savings log falhou: {e.GetType().Name}: {e.Message}");
        }
    }

    public static string RegisterFallback(string logPath, string question, int manualTokens)
    {
        var lines = File.Exists(logPath) ? File.ReadAllLines(logPath).Where(l => l.Trim().Length > 0).ToList() : [];
        var recs = lines.Select(l =>
        {
            try { return JsonNode.Parse(l) as JsonObject; }
            catch (JsonException) { return null; } // keep corrupt lines untouched
        }).ToList();
        var i = recs.FindLastIndex(r => r?["pergunta"]?.GetValue<string>() == question);
        if (i < 0) return "Nenhuma busca com essa pergunta no log.";

        var hit = recs[i]!;
        var saved = -(hit["returned_tokens"]?.GetValue<long>() ?? 0);
        hit["fallback"] = true;
        hit["manual_tokens"] = manualTokens;
        hit["saved_tokens"] = saved;
        lines[i] = hit.ToJsonString(Json);
        var tmp = logPath + ".tmp";
        File.WriteAllText(tmp, string.Concat(lines.Select(l => l + "\n")));
        File.Move(tmp, logPath, overwrite: true);
        return $"Fallback registrado: economia desta busca = {saved} tokens.";
    }
}
