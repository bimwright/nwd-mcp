using System;
using System.ComponentModel;
using System.Linq;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Bake;
using Bimwright.Nwd.Server.Handlers;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Tools;

[McpServerToolType]
public sealed class ToolBakerTools
{
    private readonly NwdMcpConfig _config;

    public ToolBakerTools(NwdMcpConfig config)
    {
        _config = config;
    }

    [McpServerTool(Name = "nwd_list_baked_tools"), Description("List all verified, compiled, and registered baked Navisworks tools.")]
    public string ListBakedTools()
    {
        BakePaths.EnsureDir(_config);
        using var db = new BakeDb(BakePaths.Db(_config));
        db.Migrate();
        var tools = db.ReadRegistryRecords()
            .Select(record => new
            {
                name = record.Name,
                description = record.Description,
                source = record.Source,
                handler_tool = record.HandlerTool,
                usage_count = record.UsageCount,
                created_at = record.CreatedAt
            })
            .ToArray();
        return JsonConvert.SerializeObject(new { tools }, Formatting.Indented);
    }

    [McpServerTool(Name = "nwd_list_bake_suggestions"), Description("List active suggestions generated from recurrent workflows.")]
    public string ListBakeSuggestions()
    {
        BakePaths.EnsureDir(_config);
        using var db = new BakeDb(BakePaths.Db(_config));
        db.Migrate();
        return ListBakeSuggestionsHandler.Handle(db);
    }

    [McpServerTool(Name = "nwd_create_bake_issue_draft"), Description("Create a GitHub issue draft for a ToolBaker suggestion without submitting it.")]
    public string CreateBakeIssueDraft([Description("Suggestion id from nwd_list_bake_suggestions.")] string id)
    {
        BakePaths.EnsureDir(_config);
        using var db = new BakeDb(BakePaths.Db(_config));
        db.Migrate();
        var suggestion = db.GetSuggestion(id);
        if (suggestion == null)
        {
            return JsonConvert.SerializeObject(new { ok = false, error_code = "not_found", message = "Bake suggestion was not found." });
        }

        var title = "[ToolBaker] " + (suggestion.Title ?? suggestion.Id);
        var body = string.Join("\n", new[]
        {
            "## Summary",
            suggestion.Description ?? "Repeated Navisworks workflow detected.",
            "",
            "## Suggestion",
            "- id: `" + suggestion.Id + "`",
            "- source: `" + suggestion.Source + "`",
            "- score: `" + suggestion.Score + "`",
            "",
            "## Payload",
            "```json",
            suggestion.PayloadJson ?? "{}",
            "```"
        });

        return JsonConvert.SerializeObject(new
        {
            ok = true,
            issue = new { title, body }
        }, Formatting.Indented);
    }
}
