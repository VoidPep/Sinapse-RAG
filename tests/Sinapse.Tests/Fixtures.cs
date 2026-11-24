using Sinapse.Core;

namespace Sinapse.Tests;

static class Fixtures
{
    /// <summary>tests/fixtures, found by walking up from the test binary.</summary>
    public static readonly string Dir = Find();

    static string Find()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "tests", "fixtures");
            if (Directory.Exists(p)) return p;
        }
        throw new DirectoryNotFoundException("tests/fixtures not found");
    }

    /// <summary>Copies the fixture vault into a fresh temp dir and returns it.</summary>
    public static string CopyVault()
    {
        var dest = Directory.CreateTempSubdirectory("sinapse-vault-").FullName;
        CopyDir(Path.Combine(Dir, "vault"), dest);
        return dest;
    }

    public static SinapseConfig Cfg(string vault) =>
        new("t", vault, [], ["**/*.md"], ["**/.obsidian/**"], Path.Combine(vault, ".sinapse"));

    static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }
}
