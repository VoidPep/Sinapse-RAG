using Sinapse.Core;

namespace Sinapse.Tests;

public sealed class ConfigTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("sinapse-cfg-").FullName;
    public void Dispose() => Directory.Delete(dir, true);

    string Cfg(string json)
    {
        var p = Path.Combine(dir, "sinapse.json");
        File.WriteAllText(p, json);
        return p;
    }

    void Touch(string rel)
    {
        var p = Path.Combine(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, "x");
    }

    static string[] Rels(SinapseConfig c) => Config.EnumerateNotes(c).Select(n => n.RelPath).ToArray();

    [Fact]
    public void Load_resolves_relative_paths_against_config_dir()
    {
        var c = Config.Load(Cfg("""{"base":".","dataDir":".sinapse"}"""));
        Assert.Equal(Path.TrimEndingDirectorySeparator(dir), c.BaseDir);
        Assert.Equal(Path.Combine(dir, ".sinapse"), c.DataDir);
        Assert.Equal(Path.Combine(dir, ".sinapse", "index.db"), c.DbPath);
    }

    [Fact]
    public void Load_accepts_both_separators()
    {
        Directory.CreateDirectory(Path.Combine(dir, "a", "b"));
        Directory.CreateDirectory(Path.Combine(dir, "c", "d"));
        var c = Config.Load(Cfg("""{"folders":["a\\b","c/d"]}"""));
        Assert.Equal([Path.Combine(dir, "a", "b"), Path.Combine(dir, "c", "d")], c.Folders);
    }

    [Fact]
    public void Load_missing_file_throws_ConfigException() =>
        Assert.Throws<ConfigException>(() => Config.Load(Path.Combine(dir, "nope.json")));

    [Fact]
    public void Load_invalid_json_throws_ConfigException() =>
        Assert.Throws<ConfigException>(() => Config.Load(Cfg("{oops")));

    [Fact]
    public void Load_nonexistent_folder_throws_ConfigException()
    {
        var e = Assert.Throws<ConfigException>(() => Config.Load(Cfg("""{"folders":["ghost"]}""")));
        Assert.Contains("ghost", e.Message);
    }

    [Fact]
    public void Enumerate_excludes_obsidian_at_any_depth()
    {
        Touch("x.md"); Touch(".obsidian/a.md"); Touch("sub/.obsidian/b.md");
        var c = Config.Load(Cfg("""{"exclude":["**/.obsidian/**"]}"""));
        Assert.Equal(["x.md"], Rels(c));
    }

    [Fact]
    public void Enumerate_always_excludes_dataDir()
    {
        Touch("n.md"); Touch(".sinapse/n.md");
        var c = Config.Load(Cfg("""{"exclude":[]}"""));
        Assert.Equal(["n.md"], Rels(c));
    }

    [Fact]
    public void Enumerate_is_ordinal_sorted()
    {
        Touch("b.md"); Touch("A.md"); Touch("a/z.md");
        Assert.Equal(["A.md", "a/z.md", "b.md"], Rels(Config.Load(Cfg("{}"))));
    }

    [Fact]
    public void Enumerate_folder_outside_base_uses_absolute_path()
    {
        Touch("vault/in.md"); Touch("outside/área comum/ação.md");
        var c = Config.Load(Cfg("""{"base":"vault","folders":["vault","outside"]}"""));
        var notes = Config.EnumerateNotes(c);
        Assert.Contains(notes, n => n.RelPath == "in.md");
        var abs = notes.Single(n => n.RelPath != "in.md");
        Assert.True(Path.IsPathRooted(abs.RelPath));
        Assert.EndsWith("área comum/ação.md", abs.RelPath);
        Assert.DoesNotContain('\\', abs.RelPath);
        Assert.True(File.Exists(abs.FullPath));
    }

    [Fact]
    public void Enumerate_path_with_space_and_accent_is_relative()
    {
        Touch("área comum/ação.md");
        Assert.Equal(["área comum/ação.md"], Rels(Config.Load(Cfg("{}"))));
    }

    [Fact]
    public void WriteDefault_then_Load_roundtrips()
    {
        var p = Path.Combine(dir, "sinapse.json");
        Config.WriteDefault(p);
        Touch("a.md");
        var c = Config.Load(p);
        Assert.Empty(c.Folders);
        Assert.Equal(["a.md"], Rels(c));
    }
}
