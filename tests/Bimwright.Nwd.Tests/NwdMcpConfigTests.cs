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
    public void SendCodeIsEnabledByDefault()
    {
        using var scope = new EnvScope().Set("BIMWRIGHT_NWD_ENABLE_SEND_CODE", null);
        Assert.True(NwdMcpConfig.Load(Array.Empty<string>()).EnableSendCode);
    }

    [Fact]
    public void DisableSendCodeFlagTurnsItOff()
    {
        using var scope = new EnvScope().Set("BIMWRIGHT_NWD_ENABLE_SEND_CODE", null);
        Assert.False(NwdMcpConfig.Load(new[] { "--disable-send-code" }).EnableSendCode);
    }

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

    [Fact]
    public void CallLogDefaultsOffAndRespectsJsonEnvironmentAndCliPrecedence()
    {
        using var scope = new EnvScope().Set("BIMWRIGHT_NWD_ENABLE_CALL_LOG", null);
        Assert.False(NwdMcpConfig.Load(Array.Empty<string>()).EnableCallLog);
        var path = TempJson("""{ "enableCallLog": true }""");
        try
        {
            Assert.True(NwdMcpConfig.Load(new[] { "--config", path }).EnableCallLog);
            scope.Set("BIMWRIGHT_NWD_ENABLE_CALL_LOG", "false");
            Assert.False(NwdMcpConfig.Load(new[] { "--config", path }).EnableCallLog);
            Assert.True(NwdMcpConfig.Load(new[] { "--config", path, "--enable-call-log" }).EnableCallLog);
            scope.Set("BIMWRIGHT_NWD_ENABLE_CALL_LOG", "true");
            Assert.False(NwdMcpConfig.Load(new[] { "--config", path, "--disable-call-log" }).EnableCallLog);
            Assert.False(NwdMcpConfig.Load(new[] { "--enable-call-log", "--disable-call-log" }).EnableCallLog);
            Assert.True(NwdMcpConfig.Load(new[] { "--disable-call-log", "--enable-call-log" }).EnableCallLog);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DataPathsRespectDisposableLocalAppData()
    {
        var root = Path.Combine(Path.GetTempPath(), "nwd-data-" + Guid.NewGuid().ToString("N"));
        using var scope = new EnvScope().Set("LOCALAPPDATA", root);
        var config = new NwdMcpConfig();
        Assert.Equal(Path.Combine(root, "Bimwright", "nwd-mcp"), config.DescriptorDirectory);
        Assert.Equal(Path.Combine(config.DescriptorDirectory, "baked"), config.BakeDirectory);
    }

    private static EnvScope ClearResponseEnv() => new EnvScope()
        .Set("BIMWRIGHT_NWD_ENABLE_RESPONSE_GUARD", null)
        .Set("BIMWRIGHT_NWD_RESPONSE_WARN_BYTES", null)
        .Set("BIMWRIGHT_NWD_RESPONSE_STRONG_WARN_BYTES", null)
        .Set("BIMWRIGHT_NWD_RESPONSE_BUDGET_BYTES", null)
        .Set("BIMWRIGHT_NWD_MAX_RESPONSE_BYTES", null)
        .Set("BIMWRIGHT_NWD_SPILL_RETENTION_HOURS", null);

    [Fact]
    public void ResponseGuardIsOnWithFamilyDefaultsAndSpillKeeps36Hours()
    {
        using var scope = ClearResponseEnv();
        var config = NwdMcpConfig.Load(Array.Empty<string>());
        Assert.True(config.EnableResponseGuard);
        Assert.Equal(64 * 1024, config.ResponseWarnBytes);
        Assert.Equal(256 * 1024, config.ResponseStrongWarnBytes);
        Assert.Equal(1024 * 1024, config.ResponseBudgetBytes);
        Assert.Equal(36, config.SpillRetentionHours);
    }

    [Fact]
    public void ResponseGuardAndSpillSettingsFollowCliEnvJsonPrecedence()
    {
        using var scope = ClearResponseEnv();
        var path = TempJson("""
        { "enableResponseGuard": false, "responseWarnBytes": 2048, "responseStrongWarnBytes": 4096,
          "responseBudgetBytes": 8192, "spillRetentionHours": 12 }
        """);
        try
        {
            var json = NwdMcpConfig.Load(new[] { "--config", path });
            Assert.False(json.EnableResponseGuard);
            Assert.Equal(2048, json.ResponseWarnBytes);
            Assert.Equal(4096, json.ResponseStrongWarnBytes);
            Assert.Equal(8192, json.ResponseBudgetBytes);
            Assert.Equal(12, json.SpillRetentionHours);

            scope.Set("BIMWRIGHT_NWD_ENABLE_RESPONSE_GUARD", "1")
                .Set("BIMWRIGHT_NWD_RESPONSE_BUDGET_BYTES", "16384")
                .Set("BIMWRIGHT_NWD_SPILL_RETENTION_HOURS", "24");
            var env = NwdMcpConfig.Load(new[] { "--config", path });
            Assert.True(env.EnableResponseGuard);
            Assert.Equal(16384, env.ResponseBudgetBytes);
            Assert.Equal(24, env.SpillRetentionHours);

            var cli = NwdMcpConfig.Load(new[]
            {
                "--config", path, "--disable-response-guard", "--response-warn-bytes", "1024",
                "--response-strong-warn-bytes", "3000", "--response-budget-bytes", "32768",
                "--spill-retention-hours", "48"
            });
            Assert.False(cli.EnableResponseGuard);
            Assert.Equal(1024, cli.ResponseWarnBytes);
            Assert.Equal(3000, cli.ResponseStrongWarnBytes);
            Assert.Equal(32768, cli.ResponseBudgetBytes);
            Assert.Equal(48, cli.SpillRetentionHours);
            scope.Set("BIMWRIGHT_NWD_RESPONSE_BUDGET_BYTES", null);
            Assert.True(NwdMcpConfig.Load(new[] { "--disable-response-guard", "--enable-response-guard" }).EnableResponseGuard);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("8761")]
    [InlineData("")]
    public void InvalidSpillRetentionFallsBackTo36(string value)
    {
        using var scope = ClearResponseEnv();
        Assert.Equal(36, NwdMcpConfig.Load(new[] { "--spill-retention-hours", value }).SpillRetentionHours);
        scope.Set("BIMWRIGHT_NWD_SPILL_RETENTION_HOURS", value);
        Assert.Equal(36, NwdMcpConfig.Load(Array.Empty<string>()).SpillRetentionHours);
    }

    [Theory]
    [InlineData("--response-warn-bytes", "300000")]      // above the strong warning
    [InlineData("--response-budget-bytes", "20000000")]  // above maxResponseBytes
    [InlineData("--response-warn-bytes", "512")]         // below 1024
    public void MisorderedResponseLimitsFailAtStartup(string flag, string value)
    {
        using var scope = ClearResponseEnv();
        var ex = Assert.Throws<ArgumentException>(() => NwdMcpConfig.Load(new[] { flag, value }));
        Assert.Contains("responseWarnBytes <= responseStrongWarnBytes", ex.Message);
    }
}

internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> previous = new(StringComparer.OrdinalIgnoreCase);
    public EnvScope Set(string name, string? value)
    {
        if (!previous.ContainsKey(name)) previous[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        return this;
    }
    public void Dispose()
    {
        foreach (var pair in previous)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }
}
