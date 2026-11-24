using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sinapse.Core;

namespace Sinapse.Tests;

public sealed class McpSmokeTests : IDisposable
{
    readonly string vault = Fixtures.CopyVault();
    readonly string cfgPath;
    SinapseConfig Cfg => Fixtures.Cfg(vault);

    public McpSmokeTests()
    {
        cfgPath = Path.Combine(vault, "sinapse.json");
        File.WriteAllText(cfgPath, """{"exclude":["**/.obsidian/**"]}""");
        Indexer.Build(Cfg);
    }

    public void Dispose() => Directory.Delete(vault, true);

    async Task<McpClient> Start()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "sinapse.dll");
        return await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "sinapse-test",
            Command = "dotnet",
            Arguments = [dll, "mcp", "--config", cfgPath],
        }));
    }

    static string TextOf(CallToolResult r) => string.Concat(r.Content.OfType<TextContentBlock>().Select(b => b.Text));

    static Dictionary<string, object?> Args(string pergunta) => new() { ["pergunta"] = pergunta };

    [Fact]
    public async Task Lists_three_tools()
    {
        await using var client = await Start();
        var names = (await client.ListToolsAsync()).Select(t => t.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["buscar_conhecimento", "registrar_fallback", "reindexar"], names);
        Assert.Equal("sinapse_knowledge_base", client.ServerInfo?.Name);
    }

    [Fact]
    public async Task Buscar_returns_prototype_format()
    {
        await using var client = await Start();
        var r = await client.CallToolAsync("buscar_conhecimento", Args("prazo de empréstimo"));
        Assert.NotEqual(true, r.IsError);
        var text = TextOf(r);
        Assert.StartsWith("[1] ", text);
        Assert.Contains(" › ", text);
        Assert.Contains("score ", text);
        Assert.Single(File.ReadAllLines(Cfg.SavingsPath));
    }

    [Fact]
    public async Task Buscar_no_match_returns_message()
    {
        await using var client = await Start();
        Assert.Equal("Nenhum chunk encontrado.", TextOf(await client.CallToolAsync("buscar_conhecimento", Args("zzzqqq"))));
    }

    [Fact]
    public async Task Buscar_without_index_returns_error_not_crash()
    {
        File.Delete(Cfg.DbPath);
        await using var client = await Start();
        var r = await client.CallToolAsync("buscar_conhecimento", Args("prazo"));
        Assert.True(r.IsError);
        Assert.Contains("sinapse index", TextOf(r));
        Assert.False(File.Exists(Cfg.DbPath));
        // server is still alive
        Assert.True((await client.CallToolAsync("buscar_conhecimento", Args("prazo"))).IsError);
    }

    [Fact]
    public async Task Reindexar_returns_counts()
    {
        await using var client = await Start();
        var r = await client.CallToolAsync("reindexar", new Dictionary<string, object?>());
        Assert.StartsWith("Índice refeito:", TextOf(r));
    }

    [Fact]
    public async Task Registrar_fallback_updates_log()
    {
        await using var client = await Start();
        await client.CallToolAsync("buscar_conhecimento", Args("prazo"));
        var r = await client.CallToolAsync("registrar_fallback",
            new Dictionary<string, object?> { ["pergunta"] = "prazo", ["manual_tokens"] = 500 });
        Assert.StartsWith("Fallback registrado:", TextOf(r));
    }
}
