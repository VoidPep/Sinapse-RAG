using System.Text.Json;
using Sinapse.Core;

namespace Sinapse.Tests;

public sealed class GoldenSearchTests : IDisposable
{
    readonly string vault = Fixtures.CopyVault();
    public void Dispose() => Directory.Delete(vault, true);

    record GoldenHit(string Path, string Heading, double Score);
    record GoldenQuery(string Pergunta, int K, string? Tipo, string? Pasta, List<GoldenHit> Hits);

    /// <summary>Consecutive hits with the same score form one unordered group.</summary>
    static List<(double Score, HashSet<(string, string)> Set)> Group(IEnumerable<GoldenHit> hits)
    {
        var groups = new List<(double, HashSet<(string, string)>)>();
        foreach (var h in hits)
        {
            if (groups.Count > 0 && Math.Abs(groups[^1].Item1 - h.Score) < 1e-9) groups[^1].Item2.Add((h.Path, h.Heading));
            else groups.Add((h.Score, [(h.Path, h.Heading)]));
        }
        return groups;
    }

    [Fact]
    public void Matches_prototype()
    {
        var queries = JsonSerializer.Deserialize<List<GoldenQuery>>(
            File.ReadAllText(Path.Combine(Fixtures.Dir, "golden", "search.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(8, queries.Count);

        var cfg = Fixtures.Cfg(vault);
        Indexer.Build(cfg);
        foreach (var q in queries)
        {
            var got = Search.Run(cfg.DbPath, q.Pergunta, q.K, q.Tipo, q.Pasta)
                .Select(h => new GoldenHit(h.Path, h.Heading, h.Score));
            var (want, have) = (Group(q.Hits), Group(got));
            Assert.True(want.Count == have.Count, $"'{q.Pergunta}': group count");
            for (var i = 0; i < want.Count; i++)
            {
                Assert.True(Math.Abs(want[i].Score - have[i].Score) <= 0.01, $"'{q.Pergunta}': score of group {i}");
                Assert.True(want[i].Set.SetEquals(have[i].Set), $"'{q.Pergunta}': members of group {i}");
            }
        }
    }
}
