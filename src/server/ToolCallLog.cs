using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Bimwright.Nwd.Shared.Logging;
using ModelContextProtocol.Protocol;

namespace Bimwright.Nwd.Server;

/// <summary>Records the final MCP outcome, including tools that do not reach the plug-in.</summary>
internal sealed class ToolCallLog(bool enabled)
{
    internal const string FileName = "nwd-mcp-calls.jsonl";

    public async ValueTask<CallToolResult> InvokeAsync(string? name,
        IDictionary<string, JsonElement>? arguments, Func<ValueTask<CallToolResult>> next)
    {
        if (!enabled) return await next();
        var clock = Stopwatch.StartNew();
        CallToolResult? result = null;
        string? error = null;
        try
        {
            result = await next();
            return result;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            throw;
        }
        finally
        {
            // Recording is best effort and must never replace the tool's outcome.
            try
            {
                NwdCallLog.Record(name, JsonSerializer.Serialize(arguments),
                    result is not null && result.IsError != true, clock.ElapsedMilliseconds,
                    error, result is null ? null : JsonSerializer.Serialize(result),
                    enabled: true, fileName: FileName);
            }
            catch { }
        }
    }
}
