using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sinapse.Core;

namespace Sinapse;

/// <summary>
/// MCP tools. Names and parameters are kept from the original prototype so existing
/// instructions keep working. Config is loaded per call: a missing or invalid config
/// becomes a tool error instead of a startup crash.
/// </summary>
[McpServerToolType]
public sealed class McpTools
{
    internal static string ConfigPath { get; set; } = Config.DefaultPath();

    static CallToolResult Text(string text, bool isError = false) =>
        new() { IsError = isError, Content = [new TextContentBlock { Text = text }] };

    static CallToolResult Run(Func<SinapseConfig, string> body)
    {
        try
        {
            return Text(body(Config.Load(ConfigPath)));
        }
        catch (IndexNotFoundException)
        {
            return Text("Índice não encontrado. Rode: sinapse index", isError: true);
        }
        catch (Exception e)
        {
            return Text($"erro: {e.Message}", isError: true);
        }
    }

    [McpServerTool(Name = "buscar_conhecimento")]
    [Description("Busca trechos (chunks) na base de conhecimento das notas configuradas (BM25). Retorna caminho da nota, heading e texto. " +
                 "Para ler a nota inteira, abra o caminho retornado.")]
    public static CallToolResult Buscar(
        string pergunta,
        [Description("máximo 20")] int k = 6,
        [Description("filtro opcional pelo campo tipo do frontmatter")] string? tipo = null,
        [Description("prefixo de caminho opcional")] string? pasta = null) =>
        Run(c =>
        {
            var hits = Search.Run(c.DbPath, pergunta, k, tipo, pasta);
            SavingsLog.Record(c.SavingsPath, c.DbPath, pergunta, hits);
            return hits.Count == 0
                ? "Nenhum chunk encontrado."
                : string.Join("\n\n", hits.Select((h, i) =>
                    $"[{i + 1}] {h.Path} › {h.Heading} ({h.Tipo}, score {h.Score.ToString("0.0#", CultureInfo.InvariantCulture)})\n{h.Text}"));
        });

    // Parameter name is snake_case on purpose: it is the wire name from the prototype.
    [McpServerTool(Name = "registrar_fallback")]
    [Description("Chame quando os chunks de buscar_conhecimento NÃO responderam a pergunta e você foi para busca manual. " +
                 "Corrige o log de economia da última busca com essa pergunta (economia = -tokens desperdiçados).")]
    public static CallToolResult RegistrarFallback(
        [Description("a mesma pergunta usada em buscar_conhecimento")] string pergunta,
        [Description("tokens aprox. gastos na busca manual (chars/4)")] int manual_tokens) =>
        Run(c => SavingsLog.RegisterFallback(c.SavingsPath, pergunta, manual_tokens));

    [McpServerTool(Name = "reindexar")]
    [Description("Refaz o índice a partir das notas atuais do vault (use após editar notas).")]
    public static CallToolResult Reindexar() =>
        Run(c =>
        {
            var r = Indexer.Build(c);
            return $"Índice refeito: {r.Files} arquivos, {r.Chunks} chunks.";
        });
}
