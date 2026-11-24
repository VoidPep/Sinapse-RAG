using System.Text.Json;
using Sinapse.Core;

namespace Sinapse.Tests;

public class GoldenChunksTests
{
    record GoldenChunk(string Heading, string Text);
    record GoldenNote(string Tipo, string Area, List<GoldenChunk> Chunks, List<string> Links);

    [Fact]
    public void Matches_prototype()
    {
        var golden = JsonSerializer.Deserialize<Dictionary<string, GoldenNote>>(
            File.ReadAllText(Path.Combine(Fixtures.Dir, "golden", "chunks.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEmpty(golden);

        foreach (var (rel, want) in golden)
        {
            var got = Chunker.Parse(File.ReadAllText(Path.Combine(Fixtures.Dir, "vault", rel)));
            Assert.Equal(want.Tipo, got.Meta.GetValueOrDefault("tipo", "outro"));
            Assert.Equal(want.Area, got.Meta.GetValueOrDefault("area", ""));
            Assert.Equal(want.Chunks.Select(c => (c.Heading, c.Text)), got.Chunks.Select(c => (c.Heading, c.Text)));
            Assert.Equal(want.Links, got.Links.OrderBy(l => l, StringComparer.Ordinal));
        }
    }
}
