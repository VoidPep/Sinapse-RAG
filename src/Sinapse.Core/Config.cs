using System.Text.Json;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Sinapse.Core;

public sealed class ConfigException(string message) : Exception(message);

public sealed record SinapseConfig(
    string Name, string BaseDir, IReadOnlyList<string> Folders,
    IReadOnlyList<string> Include, IReadOnlyList<string> Exclude, string DataDir)
{
    public string DbPath => Path.Combine(DataDir, "index.db");
    public string SavingsPath => Path.Combine(DataDir, "savings.jsonl");
}

public sealed record Note(string FullPath, string RelPath);

public static class Config
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    static readonly StringComparison PathCmp =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    sealed class Dto
    {
        public string? Name { get; set; }
        public string? Base { get; set; }
        public string[]? Folders { get; set; }
        public string[]? Include { get; set; }
        public string[]? Exclude { get; set; }
        public string? DataDir { get; set; }
    }

    public static string DefaultPath() => Path.Combine(AppContext.BaseDirectory, "sinapse.json");

    public static SinapseConfig Load(string path)
    {
        if (!File.Exists(path)) throw new ConfigException($"Config não encontrada: {path}");
        Dto dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path), Json) ?? new Dto();
        }
        catch (JsonException e)
        {
            throw new ConfigException($"JSON inválido em {path}: {e.Message}");
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string Resolve(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(dir, p.Replace('\\', '/'))));

        var baseDir = Resolve(dto.Base ?? ".");
        var folders = new List<string>();
        foreach (var f in dto.Folders ?? [])
        {
            var full = Resolve(f);
            if (!Directory.Exists(full)) throw new ConfigException($"Pasta não encontrada: {f}");
            folders.Add(full);
        }

        return new SinapseConfig(
            dto.Name ?? Path.GetFileName(dir), baseDir, folders,
            dto.Include ?? ["**/*.md"], dto.Exclude ?? [], Resolve(dto.DataDir ?? ".sinapse"));
    }

    public static void WriteDefault(string path)
    {
        var full = Path.GetFullPath(path);
        var dto = new Dto
        {
            Name = Path.GetFileName(Path.GetDirectoryName(full)),
            Base = ".", Folders = [], Include = ["**/*.md"],
            Exclude = ["**/.obsidian/**", "**/.trash/**"], DataDir = ".sinapse",
        };
        File.WriteAllText(full, JsonSerializer.Serialize(dto, Json));
    }

    public static IReadOnlyList<Note> EnumerateNotes(SinapseConfig c)
    {
        var matcher = new Matcher();
        matcher.AddIncludePatterns(c.Include);
        matcher.AddExcludePatterns(c.Exclude);

        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var notes = new List<Note>();
        foreach (var folder in c.Folders.Count == 0 ? [c.BaseDir] : c.Folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var full in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }))
            {
                if (full.StartsWith(c.DataDir + Path.DirectorySeparatorChar, PathCmp)) continue;
                var rel = Path.GetRelativePath(c.BaseDir, full).Replace('\\', '/');
                var inside = !Path.IsPathRooted(rel) && rel != ".." && !rel.StartsWith("../", StringComparison.Ordinal);
                // Files outside base are globbed relative to the folder they were found in.
                var globPath = inside ? rel : Path.GetRelativePath(folder, full).Replace('\\', '/');
                if (!matcher.Match(globPath).HasMatches || !seen.Add(full)) continue;
                notes.Add(new Note(full, inside ? rel : full.Replace('\\', '/')));
            }
        }
        notes.Sort((a, b) => string.CompareOrdinal(a.RelPath, b.RelPath));
        return notes;
    }
}
