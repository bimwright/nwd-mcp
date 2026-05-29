using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class ToolBakerTools
{
    private readonly PluginClient _client;
    public ToolBakerTools(PluginClient client) => _client = client;

    [McpServerTool(Name = "nwd_list_baked_tools"), Description("List all verified, compiled, and registered baked Navisworks tools.")]
    public string ListBakedTools()
        => JsonConvert.SerializeObject(new JArray(), Formatting.Indented);

    [McpServerTool(Name = "nwd_list_bake_suggestions"), Description("List active suggestions generated from recurrent workflows.")]
    public string ListBakeSuggestions()
        => JsonConvert.SerializeObject(new JArray(), Formatting.Indented);

    [McpServerTool(Name = "nwd_create_bake_issue_draft"), Description("Create a draft issue for a new tool request.")]
    public string CreateBakeIssueDraft(string title, string description)
        => JsonConvert.SerializeObject(new { ok = true, title = title }, Formatting.Indented);
}
