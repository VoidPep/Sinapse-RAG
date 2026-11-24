using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sinapse.Core;

namespace Sinapse;

public static class Cli
{
    const string Usage = "uso: sinapse <index|mcp|report> [--config <arquivo>] [--json] | sinapse --version";
    static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Version =>
        (typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    /// <summary>Parsed command line: command, --config value, --json, --version.</summary>
    public static (string? Command, string? ConfigPath, bool Json, bool ShowVersion, string? Error) Parse(string[] args)
    {
        string? command = null, config = null;
        bool json = false, version = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json": json = true; break;
                case "--version": version = true; break;
                case "--config":
                    if (++i >= args.Length) return (null, null, json, false, "--config exige um caminho");
                    config = args[i];
                    break;
                default:
                    if (args[i].StartsWith("--") || command != null) return (null, null, json, false, $"argumento inesperado: {args[i]}");
                    command = args[i];
                    break;
            }
        }
        return (command, config, json, version, null);
    }

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var (command, configPath, json, showVersion, error) = Parse(args);

        int Ok(string text, params (string Key, JsonNode Value)[] fields)
        {
            var payload = new JsonObject { ["ok"] = true };
            foreach (var (k, v) in fields) payload[k] = v;
            stdout.WriteLine(json ? payload.ToJsonString(JsonOpts) : text);
            return 0;
        }

        int Fail(int code, string message)
        {
            if (json) stdout.WriteLine(new JsonObject { ["ok"] = false, ["error"] = message }.ToJsonString(JsonOpts));
            else stderr.WriteLine(message);
            return code;
        }

        if (error != null) return Fail(1, $"{error}\n{Usage}");
        if (showVersion) return Ok($"sinapse {Version}", ("version", Version));
        if (command is not ("index" or "report")) return Fail(1, command == null ? Usage : $"comando desconhecido: {command}\n{Usage}");

        try
        {
            var path = configPath ?? Config.DefaultPath();
            if (command == "index" && configPath == null && !File.Exists(path))
            {
                Config.WriteDefault(path);
                stderr.WriteLine($"Config não encontrada; criei uma padrão em {path}. Ajuste 'folders' se necessário.");
            }
            var cfg = Config.Load(path);

            if (command == "index")
            {
                var r = Indexer.Build(cfg);
                return Ok($"Indexado: {r.Files} arquivos, {r.Chunks} chunks em {r.ElapsedMs} ms",
                    ("files", r.Files), ("chunks", r.Chunks), ("ms", r.ElapsedMs));
            }

            var html = SavingsReport.Write(cfg.SavingsPath, Path.Combine(cfg.DataDir, "savings.html"));
            return Ok(html, ("path", html));
        }
        catch (ConfigException e) { return Fail(2, e.Message); }
        catch (Exception e) { return Fail(1, e.Message); }
    }
}
