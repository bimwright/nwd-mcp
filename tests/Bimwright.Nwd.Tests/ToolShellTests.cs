using System.Reflection;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Tools;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Tests.Helpers;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>
/// One shell case per MCP tool: the server-side wrapper is called against a loopback fake plug-in,
/// and the NDJSON envelope it sends (command, snake_case params, timeout) is pinned. Tools that never
/// reach the plug-in (targets, ToolBaker bookkeeping) are pinned by their local result instead.
/// Live timings for the same tools are recorded in the private smoke notes.
/// </summary>
public sealed class ToolShellTests
{
    private const int DefaultTimeoutMs = 30000;
    private const int FileTimeoutMs = PluginClient.FileOperationTimeoutMs;
    private const string Nwc = @"C:\models\site.nwc";

    private sealed record Case(string Tool, string Command, Func<FakePlugin, Task<string>> Call, string ParamsJson, int TimeoutMs = DefaultTimeoutMs);

    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly Case[] PluginCases =
    {
        // files / files_write
        new("nwd_list_recent_files", "list_recent_files", p => new FileTools(p.Client).ListRecentFiles(10, Ct), """{"limit":10}"""),
        new("nwd_open_file", "open_file", p => new FileWriteTools(p.Client).OpenFile(Nwc, false, Ct), """{"path":"C:\\models\\site.nwc","discard_changes":false}""", FileTimeoutMs),
        new("nwd_import_model", "import_model", p => new FileWriteTools(p.Client).ImportModel(Nwc, "merge", Ct), """{"path":"C:\\models\\site.nwc","mode":"merge"}""", FileTimeoutMs),
        // query
        new("nwd_health_check", "health_check", p => new QueryTools(p.Client).HealthCheck(Ct), "{}"),
        new("nwd_get_document_info", "get_document_info", p => new QueryTools(p.Client).GetDocumentInfo(Ct), "{}"),
        new("nwd_get_model_statistics", "get_model_statistics", p => new QueryTools(p.Client).GetModelStatistics(Ct), "{}"),
        new("nwd_get_model_tree", "get_model_tree", p => new QueryTools(p.Client).GetModelTree(3, 50, Ct), """{"max_depth":3,"max_items":50}"""),
        new("nwd_get_item_properties", "get_item_properties", p => new QueryTools(p.Client).GetItemProperties("0:0:6", Ct), """{"item_id":"0:0:6"}"""),
        new("nwd_batch_get_properties", "batch_get_properties", p => new QueryTools(p.Client).BatchGetProperties(new[] { "0:0", "0:1" }, 2, Ct), """{"item_ids":["0:0","0:1"],"max_items":2}"""),
        new("nwd_find_items", "find_items", p => new QueryTools(p.Client).FindItems("""{"category":"Item","property":"Name","operator":"contains","value":"Pipe"}""", 5, Ct), """{"filters":{"category":"Item","property":"Name","operator":"contains","value":"Pipe"},"max_items":5}"""),
        new("nwd_find_items_by_name", "find_items_by_name", p => new QueryTools(p.Client).FindItemsByName("Pipe", true, 5, Ct), """{"name":"Pipe","exact":true,"max_items":5}"""),
        // selection / selection_write
        new("nwd_get_current_selection", "get_current_selection", p => new SelectionTools(p.Client).GetCurrentSelection(Ct), "{}"),
        new("nwd_clear_selection", "clear_selection", p => new SelectionWriteTools(p.Client).ClearSelection(Ct), "{}"),
        new("nwd_select_items_by_search", "select_items_by_search", p => new SelectionWriteTools(p.Client).SelectItemsBySearch("""[{"value":"Pipe"}]""", Ct), """{"filters":[{"value":"Pipe"}]}"""),
        // sets
        new("nwd_list_sets", "list_sets", p => new SetsTools(p.Client, new ServerState(p.Config)).ListSets(Ct), "{}"),
        new("nwd_get_selection_set_items", "get_selection_set_items", p => new SetsTools(p.Client, new ServerState(p.Config)).GetSelectionSetItems("Clash/Pipes", Ct), """{"set_id":"Clash/Pipes"}"""),
        new("nwd_execute_search_set", "execute_search_set", p => new SetsTools(p.Client, new ServerState(p.Config)).ExecuteSearchSet("Clash/Pipes", true, Ct), """{"set_id":"Clash/Pipes","select":true}"""),
        // view / view_write
        new("nwd_list_viewpoints", "list_viewpoints", p => new ViewTools(p.Client).ListViewpoints(Ct), "{}"),
        new("nwd_get_current_viewpoint", "get_current_viewpoint", p => new ViewTools(p.Client).GetCurrentViewpoint(Ct), "{}"),
        new("nwd_goto_viewpoint", "goto_viewpoint", p => new ViewWriteTools(p.Client).GotoViewpoint("3D View/{3D}", Ct), """{"viewpoint_id":"3D View/{3D}"}"""),
        new("nwd_save_viewpoint", "save_viewpoint", p => new ViewWriteTools(p.Client).SaveViewpoint("review-1", Ct), """{"name":"review-1"}"""),
        // visibility
        new("nwd_hide_items", "hide_items", p => new VisibilityWriteTools(p.Client).HideItems(new[] { "0:0:6" }, false, Ct), """{"item_ids":["0:0:6"],"hide":false}"""),
        new("nwd_unhide_all", "unhide_all", p => new VisibilityWriteTools(p.Client).UnhideAll(Ct), "{}"),
        // code
        new("nwd_send_code", "send_code", p => new CodeTools(p.Client).SendCode("Console.WriteLine(1);", Ct), """{"code":"Console.WriteLine(1);"}"""),
    };

    /// <summary>Tools answered by the server alone; each has its own fact below.</summary>
    private static readonly string[] LocalTools =
    {
        "nwd_list_available_targets", "nwd_get_current_target", "nwd_switch_target",
        "nwd_list_baked_tools", "nwd_list_bake_suggestions", "nwd_create_bake_issue_draft",
        "nwd_run_baked_tool", "nwd_accept_bake_suggestion", "nwd_dismiss_bake_suggestion",
    };

    public static IEnumerable<object[]> PluginCaseNames() => PluginCases.Select(c => new object[] { c.Tool });

    [Fact]
    public void EveryToolHasAShellCase()
    {
        var surface = Program.ResolveToolTypesForRegistration(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(n => n != null)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var covered = PluginCases.Select(c => c.Tool).Concat(LocalTools).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(surface, covered);
    }

    [Theory]
    [MemberData(nameof(PluginCaseNames))]
    public async Task PluginToolSendsPinnedEnvelope(string tool)
    {
        var c = PluginCases.Single(x => x.Tool == tool);
        using var plugin = new FakePlugin();

        var text = await c.Call(plugin);

        var env = plugin.Single();
        Assert.Equal(c.Command, env.Command);
        Assert.True(JToken.DeepEquals(JObject.Parse(c.ParamsJson), env.Params), $"{tool} params: {env.Params.ToString(Newtonsoft.Json.Formatting.None)}");
        Assert.Equal(c.TimeoutMs, env.TimeoutMs);
        Assert.Equal("token", env.AuthToken);
        Assert.Equal(c.Command, (string?)JObject.Parse(text)["echo"]);
    }

    [Theory]
    [MemberData(nameof(PluginCaseNames))]
    public async Task PluginFailureComesBackAsErrorJson(string tool)
    {
        var c = PluginCases.Single(x => x.Tool == tool);
        using var plugin = new FakePlugin
        {
            Reply = env => NwdCommandResult.Fail(env.Id, "NO_DOCUMENT", "no active Navisworks document", new NwdResponseMeta())
        };

        var result = JObject.Parse(await c.Call(plugin));

        Assert.False((bool?)result["ok"]);
        Assert.Equal("NO_DOCUMENT", (string?)result["error"]?["code"]);
    }

    [Fact]
    public async Task NoLiveTargetFailsWithoutThrowing()
    {
        using var plugin = new FakePlugin();
        var client = new PluginClient(new NwdMcpConfig { DescriptorDirectory = Path.Combine(Path.GetTempPath(), "nwd-none-" + Guid.NewGuid().ToString("N")) });

        var result = JObject.Parse(await new QueryTools(client).HealthCheck(Ct));

        Assert.Equal("NO_TARGET", (string?)result["error"]?["code"]);
        Assert.Empty(plugin.Received);
    }

    [Fact]
    public async Task ExecuteSearchSetForcesSelectOffInReadOnlyMode()
    {
        using var plugin = new FakePlugin(readOnly: true);

        await new SetsTools(plugin.Client, new ServerState(plugin.Config)).ExecuteSearchSet("Clash/Pipes", true, Ct);

        var env = plugin.Single();
        Assert.False((bool?)env.Params["select"]);
        Assert.True((bool?)env.Params["read_only_enforced"]);
    }

    [Fact]
    public void MetaToolsReadTheDescriptorWithoutCallingThePlugin()
    {
        using var plugin = new FakePlugin();
        var meta = new MetaTools(plugin.Client);

        var targets = JArray.Parse(meta.ListAvailableTargets());
        var current = JObject.Parse(meta.GetCurrentTarget());
        var switched = JObject.Parse(meta.SwitchTarget(plugin.TargetId));
        var unknown = JObject.Parse(meta.SwitchTarget("navis-9999-1"));

        Assert.Equal(plugin.TargetId, (string?)Assert.Single(targets)["TargetId"]);
        Assert.Equal(2025, (int?)current["NavisworksYear"]);
        Assert.True((bool?)switched["ok"]);
        Assert.False((bool?)unknown["ok"]);
        Assert.Empty(plugin.Received);
    }

    [Fact]
    public async Task ToolBakerToolsUseTheLocalStoreAndRejectUnknownIds()
    {
        using var plugin = new FakePlugin();
        var read = new ToolBakerTools(plugin.Config);
        var write = new ToolBakerWriteTools(plugin.Client, plugin.Config);

        Assert.Empty((JArray)JObject.Parse(read.ListBakedTools())["tools"]!);
        Assert.Empty((JArray)JObject.Parse(read.ListBakeSuggestions())["suggestions"]!);
        Assert.Equal("not_found", (string?)JObject.Parse(read.CreateBakeIssueDraft("missing"))["error_code"]);
        Assert.Equal("INVALID_ARGUMENT", (string?)JObject.Parse(await write.RunBakedTool("missing", "{}", Ct))["error"]?["code"]);
        Assert.Equal("INVALID_ARGUMENT", (string?)JObject.Parse(await write.RunBakedTool("missing", "not json", Ct))["error"]?["code"]);
        Assert.Equal("not_found", (string?)JObject.Parse(await write.AcceptBakeSuggestion("missing", "my_tool", Ct))["error_code"]);
        Assert.Equal("not_found", (string?)JObject.Parse(await write.DismissBakeSuggestion("missing", Ct))["error_code"]);
        Assert.Empty(plugin.Received);
    }
}
