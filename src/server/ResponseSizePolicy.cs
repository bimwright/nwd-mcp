using System.Collections.Generic;
using System.Text;

namespace Bimwright.Nwd.Server;

/// <summary>
/// Agent-facing size policy for tool result text (UTF-8 bytes of the returned JSON).
/// Defaults: warn from 64 KiB, warn strongly above 256 KiB, reject past the 1 MiB budget
/// (configurable, and the guard can be switched off; see <see cref="Limits"/>).
/// nwd output is already compact JSON, so the budget applies to the delivered text
/// directly with no pretty-print headroom. rvt-mcp parity, applied server-side.
/// </summary>
public static class ResponseSizePolicy
{
    public const int WarnBytes = 64 * 1024;
    public const int StrongWarnBytes = 256 * 1024;

    /// <summary>Delivered-size ceiling the agent's context must stay under.</summary>
    public const int BudgetBytes = 1024 * 1024;

    /// <summary>Effective thresholds. Guard off: no warnings, budget = transport cap.</summary>
    public sealed class Limits
    {
        public int WarnBytes { get; init; } = ResponseSizePolicy.WarnBytes;
        public int StrongWarnBytes { get; init; } = ResponseSizePolicy.StrongWarnBytes;
        public int BudgetBytes { get; init; } = ResponseSizePolicy.BudgetBytes;

        public static Limits Default { get; } = new();

        public static Limits From(NwdMcpConfig config) => config.EnableResponseGuard
            ? new Limits
            {
                WarnBytes = config.ResponseWarnBytes,
                StrongWarnBytes = config.ResponseStrongWarnBytes,
                BudgetBytes = config.ResponseBudgetBytes
            }
            : new Limits
            {
                WarnBytes = int.MaxValue,
                StrongWarnBytes = int.MaxValue,
                BudgetBytes = config.MaxResponseBytes
            };
    }

    private const string FallbackHint =
        "Retry with a smaller explicit ID list or a more selective filter supported by this tool.";

    private static readonly IReadOnlyDictionary<string, string> NarrowingHints =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["nwd_get_model_tree"] = "Retry with a lower max_depth or max_items, or use output=file for the full tree.",
            ["nwd_batch_get_properties"] = "Retry with fewer item_ids or a smaller max_items, or use output=file.",
            ["nwd_find_items_by_name"] = "Retry with exact=true or a smaller max_items, or use output=file.",
            ["nwd_find_items"] = "Retry with more selective filtersJson filters or a smaller max_items.",
            ["nwd_get_item_properties"] = "Retry against a smaller item, or read properties for fewer items via nwd_batch_get_properties.",
            ["nwd_get_current_selection"] = "Reduce the active selection in Navisworks and retry.",
            ["nwd_get_selection_set_items"] = "Retry with a smaller selection or search set (set_id).",
            ["nwd_execute_search_set"] = "Retry with a narrower search set (set_id) or select=false.",
            ["nwd_list_viewpoints"] = "Retry after narrowing the saved-viewpoint set.",
            ["nwd_list_recent_files"] = "Retry with a smaller limit.",
            ["nwd_send_code"] = "Return less data from the script; output above the budget auto-spills to a file.",
            ["nwd_run_baked_tool"] = "Use output=file or narrow the baked tool's paramsJson; oversized inline output auto-spills.",
        };

    public sealed class Decision
    {
        public int ByteCount { get; set; }

        /// <summary>none | warning | strong_warning. Reject decisions leave this at "none".</summary>
        public string Level { get; set; } = "none";
        public string? AgentWarning { get; set; }
        public bool Reject { get; set; }
        public string? RejectMessage { get; set; }
    }

    public static string GetNarrowingHint(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return FallbackHint;
        return NarrowingHints.TryGetValue(toolName!, out var hint) ? hint : FallbackHint;
    }

    public static Decision Evaluate(string? toolName, string? text)
        => Evaluate(toolName, text, Limits.Default);

    public static Decision Evaluate(string? toolName, string? text, Limits limits)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text ?? string.Empty);
        var decision = new Decision { ByteCount = byteCount };
        var hint = GetNarrowingHint(toolName);

        if (byteCount > limits.BudgetBytes)
        {
            decision.Reject = true;
            decision.RejectMessage =
                $"Response exceeded the {limits.BudgetBytes}-byte response budget ({byteCount} bytes) for {toolName}. " +
                $"{hint} Do not retry the same unscoped request.";
            return decision;
        }

        if (byteCount >= limits.WarnBytes)
        {
            var strong = byteCount > limits.StrongWarnBytes;
            decision.Level = strong ? "strong_warning" : "warning";
            decision.AgentWarning = strong
                ? $"Oversized response strong warning: {byteCount} bytes. {hint}"
                : $"Oversized response warning: {byteCount} bytes. Consider narrowing the next request. {hint}";
        }

        return decision;
    }
}
