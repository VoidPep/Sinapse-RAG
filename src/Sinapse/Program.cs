using Sinapse;

var (command, configPath, _, _, error) = Cli.Parse(args);
if (command == "mcp" && error == null)
    return await McpHost.RunAsync(configPath ?? Sinapse.Core.Config.DefaultPath());

return Cli.Run(args, Console.Out, Console.Error);
