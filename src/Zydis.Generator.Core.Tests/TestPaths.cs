using System;
using System.IO;

namespace Zydis.Generator.Core.Tests;

internal static class TestPaths
{
    public static string RepoRoot { get; } = Locate();

    private static string Locate()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Zydis.Generator.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("repo root not found");
    }
}
