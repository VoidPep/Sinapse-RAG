using Sinapse.Core;

namespace Sinapse.Tests;

public class ChunkerTests
{
    const string Filler = " conteúdo suficiente para passar do mínimo de caracteres.";

    [Fact]
    public void Crlf_is_normalized()
    {
        var r = Chunker.Parse("---\r\ntipo: x\r\n---\r\n# H\r\ntexto" + Filler + "\r\n\r\noutro" + Filler);
        Assert.Equal("x", r.Meta["tipo"]);
        Assert.All(r.Chunks, c => Assert.DoesNotContain('\r', c.Text));
        Assert.NotEmpty(r.Chunks);
    }

    [Fact]
    public void Bom_is_stripped_and_frontmatter_recognized()
    {
        var r = Chunker.Parse("﻿---\ntipo: regra\n---\n# H\ntexto" + Filler);
        Assert.Equal("regra", r.Meta["tipo"]);
        Assert.DoesNotContain("---", r.Chunks[0].Text);
    }

    [Fact]
    public void Frontmatter_splits_on_first_colon() =>
        Assert.Equal("http://a", Chunker.Parse("---\nurl: http://a\n---\n# H\n" + Filler).Meta["url"]);

    [Fact]
    public void Heading_path_joins_levels()
    {
        var big = new string('x', 700);
        var r = Chunker.Parse($"# A\n{big}\n## B\n{big}\n### C\n{big}\n## D\n{big}\n");
        Assert.Equal(["A", "A > B", "A > B > C", "A > D"],r.Chunks.Select(c => c.Heading).ToArray());
    }

    [Fact]
    public void Text_before_first_heading_has_empty_heading()
    {
        var r = Chunker.Parse("antes do título" + Filler + "\n\n# H\n" + new string('y', 1300));
        Assert.Equal("", r.Chunks[0].Heading);
        Assert.StartsWith("antes", r.Chunks[0].Text);
    }

    [Fact]
    public void Small_sections_merge_keeping_first_heading()
    {
        var r = Chunker.Parse("# A\numa seção" + Filler + "\n# B\noutra seção" + Filler);
        var c = Assert.Single(r.Chunks);
        Assert.Equal("A", c.Heading);
        Assert.Contains("# B", c.Text);
    }

    [Fact]
    public void Long_paragraph_is_hard_cut_at_1200()
    {
        var r = Chunker.Parse(new string('a', 2500));
        Assert.Equal([1200, 1200, 100], r.Chunks.Select(c => c.Text.Length).ToArray());
    }

    [Fact]
    public void Pieces_under_30_chars_are_dropped()
    {
        Assert.Empty(Chunker.Parse("curto demais").Chunks);
        Assert.Equal([1200], Chunker.Parse(new string('a', 1210)).Chunks.Select(c => c.Text.Length).ToArray());
    }

    [Fact]
    public void Links_take_last_segment_and_ignore_alias_and_anchor() =>
        Assert.Equal(["y", "w"], Chunker.Parse("[[x/y|z]] [[w#s]] " + Filler).Links.ToArray());
}
