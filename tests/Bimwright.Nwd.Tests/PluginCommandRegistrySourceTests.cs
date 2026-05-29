using System.IO;

namespace Bimwright.Nwd.Tests;

public sealed class PluginCommandRegistrySourceTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void RegistryBuildIncludesToolBakerRuntimeCommands()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "shared", "Plugin", "NwdCommandRegistry.cs"));

        Assert.Contains("Add(new RunBakedToolHandler());", source);
        Assert.Contains("Add(new ApplyBakeHandler());", source);
    }

    [Fact]
    public void RuntimeHandlerFilesDeclareExpectedCommandNames()
    {
        var handlersDir = Path.Combine(RepoRoot(), "src", "shared", "Handlers");
        var run = File.ReadAllText(Path.Combine(handlersDir, "RunBakedToolHandler.cs"));
        var apply = File.ReadAllText(Path.Combine(handlersDir, "ApplyBakeHandler.cs"));

        Assert.Contains("Name => \"run_baked_tool\"", run);
        Assert.Contains("Name => \"apply_bake\"", apply);
    }
}
