using Microsoft.Data.Sqlite;
using Sinapse.Core;

namespace Sinapse.Tests;

public sealed class IndexerTests : IDisposable
{
    readonly string vault = Fixtures.CopyVault();
    public void Dispose() => Directory.Delete(vault, true);

    SinapseConfig Cfg => Fixtures.Cfg(vault);

    static List<string> Query(string db, string sql)
    {
        using var con = new SqliteConnection($"Data Source={db};Mode=ReadOnly;Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var rows = new List<string>();
        while (r.Read()) rows.Add(r.GetString(0));
        return rows;
    }

    void NoTemps() => Assert.Empty(Directory.GetFiles(Cfg.DataDir, "index.db-*.tmp"));

    [Fact]
    public void Build_creates_schema_and_counts()
    {
        var res = Indexer.Build(Cfg);
        Assert.Equal(Config.EnumerateNotes(Cfg).Count, res.Files);
        Assert.Equal(12, res.Files);
        Assert.True(res.Chunks > 0);
        var names = Query(Cfg.DbPath, "SELECT name FROM sqlite_master");
        foreach (var n in new[] { "chunks", "fts", "links", "ix_links_dst", "ix_chunks_path" }) Assert.Contains(n, names);
        Assert.Equal(res.Chunks.ToString(), Query(Cfg.DbPath, "SELECT CAST(count(*) AS TEXT) FROM chunks")[0]);
    }

    [Fact]
    public void Build_inserts_in_ordinal_path_order()
    {
        Indexer.Build(Cfg);
        var paths = Query(Cfg.DbPath, "SELECT path FROM chunks ORDER BY id");
        Assert.Equal(paths.OrderBy(p => p, StringComparer.Ordinal), paths);
    }

    [Fact]
    public void Build_creates_dataDir_if_missing()
    {
        Assert.False(Directory.Exists(Cfg.DataDir));
        Indexer.Build(Cfg);
        Assert.True(File.Exists(Cfg.DbPath));
        NoTemps();
    }

    [Fact]
    public void Build_while_db_open_keeps_old_index_on_failure()
    {
        if (!OperatingSystem.IsWindows()) return; // Linux renames over open files, which is the desired behaviour there
        Indexer.Build(Cfg);
        var before = Search.Run(Cfg.DbPath, "prazo");
        Assert.NotEmpty(before);

        using (new FileStream(Cfg.DbPath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => Indexer.Build(Cfg));

        NoTemps();
        Assert.Equal(before, Search.Run(Cfg.DbPath, "prazo"));
    }

    [Fact]
    public void Build_replaces_db_when_reader_closed_between_queries()
    {
        Indexer.Build(Cfg);
        Search.Run(Cfg.DbPath, "prazo");
        File.WriteAllText(Path.Combine(vault, "novo.md"), "# Novo\n\nconteúdo novíssimo sobre empréstimo de equipamentos.");
        var res = Indexer.Build(Cfg);
        Assert.Equal(13, res.Files);
        Assert.NotEmpty(Search.Run(Cfg.DbPath, "novíssimo"));
    }

    [Fact]
    public async Task Concurrent_builds_both_succeed_and_index_is_valid()
    {
        var cfg = Cfg;
        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            try { Indexer.Build(cfg); return true; }
            catch (IOException) { return false; } // Windows: swap retries may be exhausted
        })).ToArray();
        var ok = await Task.WhenAll(tasks);
        Assert.Contains(true, ok);
        Assert.NotEmpty(Search.Run(cfg.DbPath, "prazo"));
        NoTemps();
    }

    [Fact]
    public void Build_with_scan_root_removed_after_load_throws_and_keeps_old_index()
    {
        var sub = Path.Combine(vault, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "n.md"), "# N\n\nprazo de entrega.");
        var cfg = Cfg with { Folders = [vault, sub] };
        Indexer.Build(cfg);
        var before = Search.Run(cfg.DbPath, "prazo");
        Assert.NotEmpty(before);

        Directory.Delete(sub, true);
        Assert.Throws<ConfigException>(() => Indexer.Build(cfg));

        Assert.Equal(before, Search.Run(cfg.DbPath, "prazo"));
        NoTemps();
    }
}
