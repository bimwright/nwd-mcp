using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Tools;
using Bimwright.Nwd.Tests.Helpers;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>
/// FC-TGT-07: inside a client AppContainer with no live target, the real server tools report CLIENT_SANDBOXED
/// with guidance instead of NO_TARGET. The token probe is forced through <see cref="PluginClient.AppContainerProbe"/>.
/// </summary>
public sealed class ClientSandboxToolTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly string _empty = Path.Combine(Path.GetTempPath(), "nwd-sandbox-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_empty)) Directory.Delete(_empty, true);
    }

    private PluginClient EmptyClient(bool sandboxed)
    {
        Directory.CreateDirectory(_empty);
        return new PluginClient(new NwdMcpConfig
        {
            DescriptorDirectory = _empty,
            BakeDirectory = Path.Combine(_empty, "baked"),
            ReportDirectory = Path.Combine(_empty, "reports")
        })
        { AppContainerProbe = () => sandboxed };
    }

    private void AssertSandboxBlock(JToken? sandbox)
    {
        Assert.NotNull(sandbox);
        Assert.True((bool?)sandbox!["detected"]);
        Assert.Equal("appcontainer", (string?)sandbox["kind"]);
        Assert.Contains("token_is_appcontainer", sandbox["evidence"]!.Values<string>());
        Assert.Equal(_empty, (string?)sandbox["data_dir"]);
    }

    [Fact]
    public void FC_TGT_07_ListAvailableTargetsCarriesSandboxAndGuidance()
    {
        var result = JObject.Parse(new MetaTools(EmptyClient(sandboxed: true)).ListAvailableTargets());

        Assert.Empty((JArray)result["targets"]!);
        AssertSandboxBlock(result["sandbox"]);
        Assert.Equal(ClientSandbox.Message, (string?)result["note"]);
        Assert.StartsWith("CLIENT_SANDBOXED: nwd-mcp is running inside your MCP client's sandbox", (string?)result["note"]);
    }

    [Fact]
    public async Task FC_TGT_07_TargetToolReturnsClientSandboxedAndSendsNothing()
    {
        using var plugin = new FakePlugin();   // a live instance the sandboxed gateway cannot see
        var client = EmptyClient(sandboxed: true);

        var result = JObject.Parse(await new QueryTools(client).GetDocumentInfo(Ct));

        Assert.False((bool?)result["ok"]);
        Assert.Equal("CLIENT_SANDBOXED", (string?)result["error"]?["code"]);
        Assert.Equal(ClientSandbox.Message, (string?)result["error"]?["message"]);
        Assert.True(ToolErrorFlag.IsFailure(result.ToString()));   // isError=true on the MCP result
        Assert.Empty(plugin.Received);
    }

    [Fact]
    public async Task FC_TGT_07_SwitchTargetReturnsClientSandboxedWithSandboxPayload()
    {
        var result = JObject.Parse(await new MetaTools(EmptyClient(sandboxed: true)).SwitchTarget("pid:4242"));

        Assert.False((bool?)result["ok"]);
        Assert.Equal("CLIENT_SANDBOXED", (string?)result["error"]?["code"]);
        Assert.False((bool?)result["error"]?["target"]?["sent"]);
        AssertSandboxBlock(result["error"]?["target"]?["sandbox"]);
    }

    [Fact]
    public async Task FC_TGT_07_HealthReportsClientSandboxedAndSandboxBlock()
    {
        var data = JObject.Parse(await new QueryTools(EmptyClient(sandboxed: true)).HealthCheck(Ct));

        Assert.False((bool?)data["ok"]);
        Assert.False((bool?)data["host_connected"]);
        Assert.Equal("CLIENT_SANDBOXED", (string?)data["error"]?["code"]);
        AssertSandboxBlock(data["sandbox"]);
    }

    [Fact]
    public async Task FC_TGT_07_SandboxedWithALiveTargetBehavesNormally()
    {
        using var plugin = new FakePlugin();
        plugin.Client.AppContainerProbe = () => true;

        var targets = JArray.Parse(new MetaTools(plugin.Client).ListAvailableTargets());
        var info = JObject.Parse(await new QueryTools(plugin.Client).GetDocumentInfo(Ct));
        var health = JObject.Parse(await new QueryTools(plugin.Client).HealthCheck(Ct));

        Assert.Equal(plugin.TargetId, (string?)Assert.Single(targets)["target_id"]);
        Assert.Equal("get_document_info", (string?)info["echo"]);
        Assert.True((bool?)health["host_connected"]);
        Assert.True((bool?)health["sandbox"]?["detected"]);   // health still reports what it saw
    }

    [Fact]
    public async Task FC_TGT_07_NotSandboxedKeepsNoTargetUnchanged()
    {
        var client = EmptyClient(sandboxed: false);

        var list = JArray.Parse(new MetaTools(client).ListAvailableTargets());
        var info = JObject.Parse(await new QueryTools(client).GetDocumentInfo(Ct));
        var health = JObject.Parse(await new QueryTools(client).HealthCheck(Ct));

        Assert.Empty(list);
        Assert.Equal("NO_TARGET", (string?)info["error"]?["code"]);
        Assert.Equal("NO_TARGET", (string?)health["error"]?["code"]);
        Assert.Null(health["sandbox"]);
    }
}
