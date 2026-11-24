using Microsoft.Data.Sqlite;

namespace Sinapse.Tests;

public class SqliteFts5Tests
{
    [Fact]
    public void Fts5_and_bm25_are_available()
    {
        using var con = new SqliteConnection("Data Source=:memory:");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            CREATE VIRTUAL TABLE t USING fts5(a, tokenize='unicode61 remove_diacritics 2');
            INSERT INTO t(a) VALUES ('ação rápida');
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT bm25(t) FROM t WHERE t MATCH '\"acao\"'";
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        Assert.False(r.Read());
    }
}
