using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Sinapse.Core;

namespace Sinapse.Tests;

public sealed class SavingsTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("sinapse-sav-").FullName;
    public void Dispose() => Directory.Delete(dir, true);

    string Log => Path.Combine(dir, "savings.jsonl");
    string Db => Path.Combine(dir, "index.db");

    void MakeDb(params (string Path, int Len)[] rows)
    {
        using var con = new SqliteConnection($"Data Source={Db};Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "CREATE TABLE chunks(id INTEGER PRIMARY KEY, path TEXT, text TEXT)";
        cmd.ExecuteNonQuery();
        foreach (var (p, len) in rows)
        {
            cmd.CommandText = "INSERT INTO chunks(path,text) VALUES($p,$t)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$p", p);
            cmd.Parameters.AddWithValue("$t", new string('x', len));
            cmd.ExecuteNonQuery();
        }
    }

    static SearchHit Hit(string path, int len) => new(path, "H", "t", new string('y', len), 1.0);

    [Fact]
    public void Record_writes_prototype_line_format()
    {
        MakeDb(("a.md", 2000), ("a.md", 2000), ("b.md", 9999));
        SavingsLog.Record(Log, Db, "pergunta de ação", [Hit("a.md", 500), Hit("a.md", 500)]);

        var line = File.ReadAllLines(Log).Single();
        Assert.Contains("ação", line);
        var o = JsonDocument.Parse(line).RootElement;
        Assert.Equal(["ts", "pergunta", "notas", "returned_tokens", "manual_tokens", "saved_tokens"],
            o.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(1, o.GetProperty("notas").GetInt32());
        Assert.Equal(250, o.GetProperty("returned_tokens").GetInt32());
        Assert.Equal(1000, o.GetProperty("manual_tokens").GetInt32());
        Assert.Equal(750, o.GetProperty("saved_tokens").GetInt32());
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d$", o.GetProperty("ts").GetString()!);
    }

    [Fact]
    public void Record_with_no_hits_writes_zero_line()
    {
        SavingsLog.Record(Log, Db, "nada", []);
        var o = JsonDocument.Parse(File.ReadAllLines(Log).Single()).RootElement;
        Assert.Equal(0, o.GetProperty("notas").GetInt32());
        Assert.Equal(0, o.GetProperty("saved_tokens").GetInt32());
    }

    [Fact]
    public void Record_never_throws_when_log_dir_or_db_missing()
    {
        SavingsLog.Record(Path.Combine(dir, "nao", "existe", "s.jsonl"), Db, "q", []);
        SavingsLog.Record(Log, Path.Combine(dir, "sem.db"), "q", [Hit("a.md", 10)]);
        Assert.False(File.Exists(Log));
    }

    [Fact]
    public void RegisterFallback_updates_last_matching_line()
    {
        MakeDb(("a.md", 2000));
        SavingsLog.Record(Log, Db, "mesma", [Hit("a.md", 1000)]);
        SavingsLog.Record(Log, Db, "mesma", [Hit("a.md", 1000)]);
        var msg = SavingsLog.RegisterFallback(Log, "mesma", 4321);

        Assert.Equal("Fallback registrado: economia desta busca = -250 tokens.", msg);
        var lines = File.ReadAllLines(Log).Select(l => JsonDocument.Parse(l).RootElement).ToArray();
        Assert.False(lines[0].TryGetProperty("fallback", out _));
        Assert.True(lines[1].GetProperty("fallback").GetBoolean());
        Assert.Equal(4321, lines[1].GetProperty("manual_tokens").GetInt32());
        Assert.Equal(-250, lines[1].GetProperty("saved_tokens").GetInt32());
    }

    [Fact]
    public void RegisterFallback_unknown_question()
    {
        Assert.Equal("Nenhuma busca com essa pergunta no log.", SavingsLog.RegisterFallback(Log, "x", 1));
        SavingsLog.Record(Log, Db, "outra", []);
        Assert.Equal("Nenhuma busca com essa pergunta no log.", SavingsLog.RegisterFallback(Log, "x", 1));
    }

    [Fact]
    public void Old_lines_without_fallback_key_are_read()
    {
        File.WriteAllText(Log,
            "{\"ts\": \"2026-01-02T03:04:05\", \"pergunta\": \"velha </script> ação\", \"notas\": 2, " +
            "\"returned_tokens\": 100, \"manual_tokens\": 900, \"saved_tokens\": 800}\n");
        var html = Path.Combine(dir, "savings.html");
        Assert.Equal(html, SavingsReport.Write(Log, html));

        var text = File.ReadAllText(html);
        Assert.Contains("2026-01-02T03:04:05", text);
        Assert.Contains("ação", text);
        Assert.DoesNotContain("/*DATA*/", text);
        Assert.Equal(2, Regex.Matches(text, "</script>").Count); // the question's "</script>" was escaped
    }

    [Fact]
    public void Corrupt_line_is_tolerated_by_fallback_and_report()
    {
        File.WriteAllLines(Log, [
            "{truncated",
            """{"ts":"2026-01-02T03:04:05","pergunta":"q","notas":1,"returned_tokens":10,"manual_tokens":20,"saved_tokens":10}"""]);
        Assert.StartsWith("Fallback registrado", SavingsLog.RegisterFallback(Log, "q", 5));
        Assert.Equal("{truncated", File.ReadAllLines(Log)[0]);
        SavingsReport.Write(Log, Path.Combine(dir, "r.html"));
    }

    [Fact]
    public void Report_without_log_throws() =>
        Assert.Throws<FileNotFoundException>(() => SavingsReport.Write(Log, Path.Combine(dir, "x.html")));
}
