using Bimwright.Nwd.Server;

namespace Bimwright.Nwd.Tests;

public sealed class NwdMcpConfigTests
{
    [Fact]
    public void CliOverridesEnvironmentAndJson()
    {
        using var scope = new EnvScope()
            .Set("BIMWRIGHT_NWD_READ_ONLY", "true")
            .Set("BIMWRIGHT_NWD_TOOLSETS", "query,meta");

        var path = TempJson("""
        { "readOnly": false, "toolsets": ["query"], "timeoutMs": 1000, "maxResponseBytes": 2048 }
        """);

        var config = NwdMcpConfig.Load(new[]
        {
            "--config", path,
            "--toolsets", "all",
            "--timeout-ms", "30000",
            "--max-response-bytes", "10485760"
        });

        Assert.Contains("all", config.Toolsets);          // CLI wins over env+json
        Assert.Equal(30000, config.TimeoutMs);
        Assert.Equal(10485760, config.MaxResponseBytes);
    }

    [Fact]
    public void EnvOverridesJson()
    {
        using var scope = new EnvScope().Set("BIMWRIGHT_NWD_READ_ONLY", "true");
        var path = TempJson("""{ "readOnly": false }""");
        var config = NwdMcpConfig.Load(new[] { "--config", path });
        Assert.True(config.ReadOnly);                      // env beats json
    }

    [Fact]
    public void SendCodeIsDisabledByDefault()
        => Assert.False(NwdMcpConfig.Load(Array.Empty<string>()).EnableSendCode);

    [Fact]
    public void ToolBakerIsEnabledByDefault()
        => Assert.True(NwdMcpConfig.Load(Array.Empty<string>()).EnableToolBaker);

    [Fact]
    public void DefaultsAreSafe()
    {
        var c = NwdMcpConfig.Load(Array.Empty<string>());
        Assert.False(c.ReadOnly);
        Assert.Equal(30000, c.TimeoutMs);
        Assert.Equal(10 * 1024 * 1024, c.MaxResponseBytes);
        Assert.Empty(c.Toolsets);
    }

    private static string TempJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "nwd-mcp-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }
}

internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> previous = new(StringComparer.OrdinalIgnoreCase);
    public EnvScope Set(string name, string value)
    {
        previous[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        return this;
    }
    public void Dispose()
    {
        foreach (var pair in previous)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }
}
