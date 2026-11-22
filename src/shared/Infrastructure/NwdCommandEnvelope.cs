using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class NwdCommandEnvelope
{
    // Includes parameters and envelope metadata; large batches must be split.
    public const int MaxSerializedBytes = 10 * 1024 * 1024;
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("command")] public string Command { get; set; } = "";
    [JsonProperty("params")] public JObject Params { get; set; } = new();
    [JsonProperty("timeout_ms")] public int TimeoutMs { get; set; } = 30000;
    [JsonProperty("auth_token")] public string? AuthToken { get; set; }
    // Missing on legacy gateways: retain their host-side Record toggle.
    [JsonProperty("record_calls", NullValueHandling = NullValueHandling.Ignore)] public bool? RecordCalls { get; set; }
    /// <summary>Present from gateways that confirm documents; the plug-in refuses writes without it (PAIRING_REQUIRED).</summary>
    [JsonProperty("document_target", NullValueHandling = NullValueHandling.Ignore)] public DocumentTargetEnvelope? DocumentTarget { get; set; }
    /// <summary>The MCP client's clientInfo.name; older gateways omit it and the toast then shows no agent.</summary>
    [JsonProperty("client_name", NullValueHandling = NullValueHandling.Ignore)] public string? ClientName { get; set; }
}
