using Sinapse.Core;

namespace Sinapse.Tests;

public sealed class SearchTests : IDisposable
{
    readonly string vault = Fixtures.CopyVault();
    public void Dispose() => Directory.Delete(vault, true);

    string Db()
    {
        var c = Fixtures.Cfg(vault);
        Indexer.Build(c);
        return c.DbPath;
    }

    [Fact]
    public void Search_missing_index_throws_IndexNotFound_and_creates_nothing()
    {
        var db = Path.Combine(vault, "nope", "index.db");
        Assert.Throws<IndexNotFoundException>(() => Search.Run(db, "prazo"));
        Assert.False(File.Exists(db));
        Assert.False(Directory.Exists(Path.GetDirectoryName(db)));
    }

    [Fact]
    public void Search_with_fts_syntax_does_not_throw() =>
        Assert.NotNull(Search.Run(Db(), "foo\" OR bar* -baz AND NEAR("));

    [Fact]
    public void Search_with_only_short_words_returns_empty() =>
        Assert.Empty(Search.Run(Db(), "de a o"));

    [Fact]
    public void Search_is_accent_insensitive() =>
        Assert.Contains(Search.Run(Db(), "acao"), h => h.Path.EndsWith("configuração.md"));

    [Fact]
    public void Search_tipo_and_pasta_filter()
    {
        var db = Db();
        var all = Search.Run(db, "prazo multa");
        Assert.True(all.Select(h => h.Tipo).Distinct().Count() > 1);
        Assert.All(Search.Run(db, "prazo multa", tipo: "regra"), h => Assert.Equal("regra", h.Tipo));
        var inFolder = Search.Run(db, "prazo multa", pasta: "catalogo/multas");
        Assert.NotEmpty(inFolder);
        Assert.All(inFolder, h => Assert.StartsWith("catalogo/multas", h.Path));
    }

    [Fact]
    public void Search_k_is_clamped_to_20()
    {
        for (var i = 0; i < 30; i++)
            File.WriteAllText(Path.Combine(vault, $"n{i}.md"), $"# N{i}\n\nO tema zebra aparece neste texto número {i} de teste.");
        var db = Db();
        Assert.Equal(20, Search.Run(db, "zebra", k: 100).Count);
        Assert.Single(Search.Run(db, "zebra", k: 0));
    }
}
