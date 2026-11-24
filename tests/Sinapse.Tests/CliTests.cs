using System.Text.Json;
using System.Text.RegularExpressions;
using Sinapse;

namespace Sinapse.Tests;

public sealed class CliTests : IDisposable
{
    readonly string vault = Fixtures.CopyVault();
    public void Dispose() => Directory.Delete(vault, true);

    string Cfg(string json = """{"exclude":["**/.obsidian/**"]}""")
    {
        var p = Path.Combine(vault, "sinapse.json");
        File.WriteAllText(p, json);
        return p;
    }

    static (int Code, string Out, string Err) Run(params string[] args)
    {
        var (o, e) = (new StringWriter(), new StringWriter());
        return (Cli.Run(args, o, e), o.ToString(), e.ToString());
    }

    [Fact]
    public void Index_json_reports_counts()
    {
        var (code, o, _) = Run("index", "--config", Cfg(), "--json");
        Assert.Equal(0, code);
        var j = JsonDocument.Parse(o).RootElement;
        Assert.True(j.GetProperty("ok").GetBoolean());
        Assert.True(j.GetProperty("files").GetInt32() > 0);
        Assert.True(j.GetProperty("chunks").GetInt32() > 0);
    }

    [Fact]
    public void Index_text_output()
    {
        var (code, o, _) = Run("index", "--config", Cfg());
        Assert.Equal(0, code);
        Assert.Matches(@"^Indexado: \d+ arquivos, \d+ chunks em \d+ ms", o);
    }

    [Fact]
    public void Index_with_missing_explicit_config_exits_2_and_creates_nothing()
    {
        var p = Path.Combine(vault, "typo.json");
        Assert.Equal(2, Run("index", "--config", p).Code);
        Assert.False(File.Exists(p));
    }

    [Fact]
    public void Index_with_invalid_config_exits_2_and_json_error()
    {
        var (code, o, _) = Run("index", "--config", Cfg("{oops"), "--json");
        Assert.Equal(2, code);
        Assert.False(JsonDocument.Parse(o).RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void Report_without_log_exits_1() =>
        Assert.Equal(1, Run("report", "--config", Cfg()).Code);

    [Fact]
    public void Report_with_log_prints_html_path()
    {
        var cfg = Cfg();
        Directory.CreateDirectory(Path.Combine(vault, ".sinapse"));
        File.WriteAllText(Path.Combine(vault, ".sinapse", "savings.jsonl"),
            """{"ts":"2026-01-02T03:04:05","pergunta":"q","notas":0,"returned_tokens":0,"manual_tokens":0,"saved_tokens":0}""" + "\n");
        var (code, o, _) = Run("report", "--config", cfg, "--json");
        Assert.Equal(0, code);
        Assert.True(File.Exists(JsonDocument.Parse(o).RootElement.GetProperty("path").GetString()));
    }

    [Fact]
    public void Version_json()
    {
        var (code, o, _) = Run("--version", "--json");
        Assert.Equal(0, code);
        Assert.Matches(@"^\d+\.\d+\.\d+", JsonDocument.Parse(o).RootElement.GetProperty("version").GetString()!);
        Assert.Matches(@"^sinapse \d+\.\d+\.\d+", Run("--version").Out);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("--wat")]
    [InlineData("index", "--config")]
    public void Bad_usage_exits_1(params string[] args) => Assert.Equal(1, Run(args).Code);
}
