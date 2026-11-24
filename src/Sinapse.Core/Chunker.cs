using System.Text.RegularExpressions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Sinapse.Tests")]

namespace Sinapse.Core;

public sealed record ChunkPiece(string Heading, string Text);

public sealed record NoteChunks(
    IReadOnlyDictionary<string, string> Meta, IReadOnlyList<ChunkPiece> Chunks, IReadOnlyList<string> Links);

/// <summary>
/// Markdown to chunks. Lengths are UTF-16 code units (the prototype counted code points);
/// they only differ for characters outside the BMP, e.g. emoji.
/// </summary>
public static partial class Chunker
{
    public const int Max = 1200;
    public const int MinChunk = 30;

    [GeneratedRegex(@"\A---\n(.*?)\n---\n", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRx();

    [GeneratedRegex(@"^(#{1,3}) +(.*)$", RegexOptions.Multiline)]
    private static partial Regex HeadRx();

    [GeneratedRegex(@"\[\[([^\]|#]+)(?:[#|][^\]]*)?\]\]")]
    private static partial Regex LinkRx();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphRx();

    public static NoteChunks Parse(string rawText)
    {
        var txt = rawText.TrimStart('﻿').Replace("\r\n", "\n");
        var (meta, body) = Frontmatter(txt);

        var links = LinkRx().Matches(body)
            .Select(m => m.Groups[1].Value.Trim().Split('/')[^1])
            .Distinct().ToList();

        var merged = new List<(string Heading, string Text)>();
        foreach (var (heading, sec) in Sections(body))
        {
            if (merged.Count > 0 && merged[^1].Text.Length + sec.Length < Max)
                merged[^1] = (merged[^1].Heading, merged[^1].Text + "\n\n" + sec);
            else
                merged.Add((heading, sec));
        }

        var chunks = new List<ChunkPiece>();
        foreach (var (heading, sec) in merged)
            foreach (var piece in Split(sec))
                if (piece.Length >= MinChunk) chunks.Add(new ChunkPiece(heading, piece));

        return new NoteChunks(meta, chunks, links);
    }

    internal static (Dictionary<string, string> Meta, string Body) Frontmatter(string txt)
    {
        var meta = new Dictionary<string, string>();
        var m = FrontmatterRx().Match(txt);
        if (!m.Success) return (meta, txt);
        foreach (var line in m.Groups[1].Value.Split('\n'))
        {
            var i = line.IndexOf(':');
            if (i >= 0) meta[line[..i].Trim()] = line[(i + 1)..].Trim();
        }
        return (meta, txt[m.Length..]);
    }

    internal static IEnumerable<(string Heading, string Text)> Sections(string body)
    {
        var pos = HeadRx().Matches(body).Select(m => (m.Index, Level: m.Groups[1].Length, Title: m.Groups[2].Value.Trim())).ToList();
        if (pos.Count == 0 || pos[0].Index > 0) pos.Insert(0, (0, 0, ""));

        var path = new SortedDictionary<int, string>();
        for (var i = 0; i < pos.Count; i++)
        {
            var (s, lvl, title) = pos[i];
            var e = i + 1 < pos.Count ? pos[i + 1].Index : body.Length;
            foreach (var k in path.Keys.Where(k => k >= lvl).ToList()) path.Remove(k);
            if (lvl > 0) path[lvl] = title;
            yield return (string.Join(" > ", path.Values), body[s..e].Trim());
        }
    }

    internal static List<string> Split(string text)
    {
        var output = new List<string>();
        var cur = "";
        foreach (var para in ParagraphRx().Split(text))
        {
            var p = para;
            while (p.Length > Max)
            {
                if (cur.Length > 0) { output.Add(cur); cur = ""; }
                output.Add(p[..Max]);
                p = p[Max..];
            }
            if (cur.Length + p.Length + 2 > Max && cur.Length > 0) { output.Add(cur); cur = ""; }
            cur = cur.Length > 0 ? cur + "\n\n" + p : p;
        }
        if (cur.Length > 0) output.Add(cur);
        return output;
    }
}
