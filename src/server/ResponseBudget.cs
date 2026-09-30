using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

/// <summary>
/// Server-side response-size policy applied to every tool result as a CallTool filter:
/// explicit output=file and oversized arbitrary-code output spill to a local file first,
/// then remaining text is warned (64/256 KiB), rejected past the 1 MiB budget, or — for a
/// completed mutation — compacted into a truthful summary. rvt-mcp parity.
/// </summary>
public sealed class ResponseBudget
{
    private const string SendCodeTool = "nwd_send_code";
    private const string RunBakedTool = "nwd_run_baked_tool";

    /// <summary>Tools whose <c>output=file</c> argument opts into a spill artifact.</summary>
    private static readonly IReadOnlyDictionary<string, ResponseSpillFormat> OptInFormats =
        new Dictionary<string, ResponseSpillFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["nwd_get_model_tree"] = ResponseSpillFormat.Json,
            ["nwd_batch_get_properties"] = ResponseSpillFormat.Sqlite,
            ["nwd_find_items_by_name"] = ResponseSpillFormat.Ndjson,
            [RunBakedTool] = ResponseSpillFormat.Auto
        };

    private readonly ResponseSpillWriter _writer;
    private readonly IReadOnlyDictionary<string, bool> _toolIsWrite;

    public ResponseBudget(ResponseSpillWriter writer, IReadOnlyDictionary<string, bool> toolIsWrite)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _toolIsWrite = toolIsWrite ?? throw new ArgumentNullException(nameof(toolIsWrite));
    }

    /// <summary>Default map: every [McpServerTool] in this assembly, isWrite = ReadOnly != true.</summary>
    public static ResponseBudget CreateDefault(ResponseSpillWriter? writer = null)
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var type in typeof(ResponseBudget).Assembly.GetTypes())
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            var attribute = method.GetCustomAttribute<McpServerToolAttribute>();
            if (attribute?.Name is { } name)
                map[name] = attribute.ReadOnly != true;
        }
        return new ResponseBudget(writer ?? new ResponseSpillWriter(), map);
    }

    public bool IsWrite(string? toolName)
        => _toolIsWrite.TryGetValue(toolName ?? string.Empty, out var write) && write;

    /// <summary>Tools whose mutation outcome cannot be judged from the result payload.</summary>
    internal static bool IsMutationOutcomeIndeterminate(string? toolName)
        => string.Equals(toolName, SendCodeTool, StringComparison.Ordinal)
            || string.Equals(toolName, RunBakedTool, StringComparison.Ordinal);

    /// <summary>Validates the shared output=inline|file argument; null means accepted.</summary>
    public static string? InvalidOutput(string? output)
    {
        if (string.Equals(output, "inline", StringComparison.OrdinalIgnoreCase)
            || string.Equals(output, "file", StringComparison.OrdinalIgnoreCase))
            return null;
        return JsonConvert.SerializeObject(new
        {
            ok = false,
            error = new
            {
                code = "INVALID_ARGUMENT",
                message = $"output must be 'inline' or 'file' (got '{output}')."
            }
        }, Formatting.None);
    }

    public CallToolResult Apply(string? toolName, IDictionary<string, JsonElement>? arguments, CallToolResult result)
    {
        var blocks = result.Content;
        if (blocks is null || blocks.Count != 1 || blocks[0] is not TextContentBlock block)
            return result;

        var text = block.Text ?? string.Empty;
        var originalFailure = result.IsError == true || ToolErrorFlag.IsFailure(text);

        text = ApplySpill(toolName, arguments, text, originalFailure, block);

        var decision = ResponseSizePolicy.Evaluate(toolName, text);
        if (decision.Reject)
        {
            Console.Error.WriteLine(
                $"[S4] Oversized response: tool={toolName} bytes={decision.ByteCount} level=reject");
            block.Text = IsWrite(toolName) && !originalFailure
                ? CompactedText(toolName, text, decision)
                : RejectedText(decision);
        }
        else if (decision.AgentWarning != null)
        {
            Console.Error.WriteLine(
                $"[S4] Oversized response: tool={toolName} bytes={decision.ByteCount} level={decision.Level}");
            if (TryParse(text) is JObject obj)
            {
                obj["_response_warning"] = decision.AgentWarning;
                var candidate = obj.ToString(Formatting.None);
                if (Encoding.UTF8.GetByteCount(candidate) <= ResponseSizePolicy.BudgetBytes)
                    block.Text = candidate;
            }
        }

        return result;
    }

    private string ApplySpill(
        string? toolName,
        IDictionary<string, JsonElement>? arguments,
        string text,
        bool failure,
        TextContentBlock block)
    {
        if (failure
            || (!OptInFormats.ContainsKey(toolName ?? string.Empty) && !IsMutationOutcomeIndeterminate(toolName))
            || TryParse(text) is not { } data)
            return text;

        var originalBytes = Encoding.UTF8.GetByteCount(text);
        var format = ResponseSpillFormat.Json;
        var automatic = false;

        if (OptInFormats.TryGetValue(toolName ?? string.Empty, out var optIn)
            && string.Equals(ReadOutputArgument(arguments), "file", StringComparison.OrdinalIgnoreCase))
        {
            format = optIn;
        }
        else if (IsMutationOutcomeIndeterminate(toolName) && originalBytes > ResponseSizePolicy.BudgetBytes)
        {
            format = ResponseSpillFormat.Auto;
            automatic = true;
        }
        else
        {
            return text;
        }

        try
        {
            var spill = _writer.Write(toolName ?? "response", data, format);
            var envelope = spill.Envelope;
            envelope["tool"] = toolName ?? string.Empty;
            envelope["automatic"] = automatic;
            envelope["original_response_byte_count"] = originalBytes;
            if (automatic)
            {
                envelope["note"] = "Oversized arbitrary output auto-spilled to a local same-machine file. " +
                    "Query it locally; do not re-run the executed command for the full result.";
            }
            if (IsMutationOutcomeIndeterminate(toolName))
            {
                // Keep the script's ok/error scalars visible alongside the artifact path.
                envelope["summary"] = MutationResponseCompactor.Compact(data, originalBytes)["summary"];
            }
            EnsureEnvelopeBudget(envelope);
            var envelopeText = envelope.ToString(Formatting.None);
            block.Text = envelopeText;
            return envelopeText;
        }
        catch (Exception ex)
        {
            var failed = new JObject
            {
                ["ok"] = false,
                ["error"] = new JObject
                {
                    ["code"] = "SPILL_FAILED",
                    ["message"] = "Failed to write spill file: " + ex.Message
                },
                ["operation_completed"] = true,
                ["mutation_applied"] = MutationAppliedToken(toolName),
                ["original_response_byte_count"] = originalBytes,
                ["note"] = "The command already completed but its oversized output could not be persisted. Do not blindly retry a mutation."
            };
            var failureText = failed.ToString(Formatting.None);
            block.Text = failureText;
            return failureText;
        }
    }

    private JToken MutationAppliedToken(string? toolName)
    {
        if (IsMutationOutcomeIndeterminate(toolName))
            return JValue.CreateNull();
        return new JValue(IsWrite(toolName));
    }

    private string CompactedText(string? toolName, string text, ResponseSizePolicy.Decision decision)
    {
        var compacted = MutationResponseCompactor.Compact(TryParse(text) ?? new JValue(text), decision.ByteCount);
        if (IsMutationOutcomeIndeterminate(toolName))
            compacted["mutation_applied"] = JValue.CreateNull();
        var compactText = compacted.ToString(Formatting.None);
        if (Encoding.UTF8.GetByteCount(compactText) <= ResponseSizePolicy.BudgetBytes)
            return compactText;

        // Defensive fallback: compaction itself must never breach the budget.
        return new JObject
        {
            ["ok"] = true,
            ["mutation_applied"] = compacted["mutation_applied"],
            ["response_compacted"] = true,
            ["original_byte_count"] = decision.ByteCount,
            ["warning"] = "Command completed successfully; oversized response detail was omitted. Inspect the summary before another call."
        }.ToString(Formatting.None);
    }

    private static string RejectedText(ResponseSizePolicy.Decision decision)
        => new JObject
        {
            ["ok"] = false,
            ["error"] = new JObject
            {
                ["code"] = "RESPONSE_TOO_LARGE",
                ["message"] = decision.RejectMessage ?? "Response exceeded the response budget."
            }
        }.ToString(Formatting.None);

    private static string? ReadOutputArgument(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments != null
            && arguments.TryGetValue("output", out var element)
            && element.ValueKind == JsonValueKind.String)
            return element.GetString();
        return null;
    }

    private static JToken? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
            return null;
        try { return JToken.Parse(text); }
        catch (Newtonsoft.Json.JsonException) { return null; }
    }

    private static void EnsureEnvelopeBudget(JObject envelope)
    {
        if (Encoding.UTF8.GetByteCount(envelope.ToString(Formatting.None)) <= ResponseSizePolicy.BudgetBytes)
            return;

        envelope["preview"] = string.Empty;
        envelope["preview_truncated"] = true;
        envelope["schema"] = new JObject
        {
            ["_truncated"] = "Schema omitted from envelope to preserve the response budget; inspect the local artifact directly."
        };
        envelope["schema_truncated"] = true;
    }
}
