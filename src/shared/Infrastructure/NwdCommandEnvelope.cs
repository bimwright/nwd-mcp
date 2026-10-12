using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class NwdCommandEnvelope
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("command")] public string Command { get; set; } = "";
    [JsonProperty("params")] public JObject Params { get; set; } = new();
    [JsonProperty("timeout_ms")] public int TimeoutMs { get; set; } = 30000;
    [JsonProperty("auth_token")] public string? AuthToken { get; set; }
    // Missing on legacy gateways: retain their host-side Record toggle.
    [JsonProperty("record_calls", NullValueHandling = NullValueHandling.Ignore)] public bool? RecordCalls { get; set; }
}
