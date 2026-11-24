using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Sinapse;

public static class McpHost
{
    public static async Task<int> RunAsync(string configPath)
    {
        McpTools.ConfigPath = Path.GetFullPath(configPath);

        var builder = Host.CreateApplicationBuilder();
        // stdout is the protocol channel: every log goes to stderr
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services
            .AddMcpServer(o => o.ServerInfo = new Implementation { Name = "sinapse_knowledge_base", Version = Cli.Version })
            .WithStdioServerTransport()
            .WithTools<McpTools>();

        await builder.Build().RunAsync();
        return 0;
    }
}
