using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Sinapse.Core;

public sealed record IndexResult(int Files, int Chunks, long ElapsedMs);

public static class Indexer
{
    const string Schema = """
        CREATE TABLE chunks(id INTEGER PRIMARY KEY, path TEXT, title TEXT, tipo TEXT, area TEXT, heading TEXT, text TEXT);
        CREATE VIRTUAL TABLE fts USING fts5(title, heading, text, content='chunks', content_rowid='id',
                                            tokenize='unicode61 remove_diacritics 2');
        CREATE TABLE links(src TEXT, dst TEXT);
        """;

    /// <summary>
    /// Builds into index.db.tmp, then swaps it over index.db. Other processes may hold the old
    /// db open: if the swap keeps failing the old index stays intact and IOException is thrown.
    /// </summary>
    public static IndexResult Build(SinapseConfig c)
    {
        var sw = Stopwatch.StartNew();
        var notes = Config.EnumerateNotes(c);
        Directory.CreateDirectory(c.DataDir);
        var tmp = c.DbPath + ".tmp";
        DeleteQuietly(tmp);

        try
        {
            var chunks = Fill(tmp, notes);
            Swap(tmp, c.DbPath);
            return new IndexResult(notes.Count, chunks, sw.ElapsedMilliseconds);
        }
        finally
        {
            DeleteQuietly(tmp);
        }
    }

    static int Fill(string dbFile, IReadOnlyList<Note> notes)
    {
        var n = 0;
        using (var con = new SqliteConnection($"Data Source={dbFile};Pooling=False"))
        {
            con.Open();
            Exec(con, "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF;");
            Exec(con, Schema);

            using var tx = con.BeginTransaction();
            using var insChunk = con.CreateCommand();
            insChunk.CommandText = "INSERT INTO chunks(path,title,tipo,area,heading,text) VALUES($p,$ti,$tp,$a,$h,$x)";
            using var insLink = con.CreateCommand();
            insLink.CommandText = "INSERT INTO links VALUES($s,$d)";
            var (p, ti, tp, a, h, x) = (Param(insChunk, "$p"), Param(insChunk, "$ti"), Param(insChunk, "$tp"),
                                        Param(insChunk, "$a"), Param(insChunk, "$h"), Param(insChunk, "$x"));
            var (s, d) = (Param(insLink, "$s"), Param(insLink, "$d"));

            foreach (var note in notes)
            {
                var parsed = Chunker.Parse(File.ReadAllText(note.FullPath));
                foreach (var dst in parsed.Links) { s.Value = note.RelPath; d.Value = dst; insLink.ExecuteNonQuery(); }
                foreach (var piece in parsed.Chunks)
                {
                    p.Value = note.RelPath;
                    ti.Value = Path.GetFileNameWithoutExtension(note.FullPath);
                    tp.Value = parsed.Meta.GetValueOrDefault("tipo", "outro");
                    a.Value = parsed.Meta.GetValueOrDefault("area", "");
                    h.Value = piece.Heading;
                    x.Value = piece.Text;
                    insChunk.ExecuteNonQuery();
                    n++;
                }
            }
            tx.Commit();

            Exec(con, "INSERT INTO fts(rowid,title,heading,text) SELECT id,title,heading,text FROM chunks");
            Exec(con, "INSERT INTO fts(fts) VALUES('optimize')");
            Exec(con, "CREATE INDEX ix_links_dst ON links(dst); CREATE INDEX ix_chunks_path ON chunks(path);");
        }
        return n;
    }

    static void Swap(string tmp, string db)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(tmp, db, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt == 5) throw new IOException($"Não foi possível substituir o índice (em uso?): {db}", e);
                Thread.Sleep(200);
            }
        }
    }

    static SqliteParameter Param(SqliteCommand cmd, string name)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        cmd.Parameters.Add(p);
        return p;
    }

    static void Exec(SqliteConnection con, string sql)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    static void DeleteQuietly(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
