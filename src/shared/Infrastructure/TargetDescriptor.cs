using System;
using Newtonsoft.Json;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class TargetDescriptor
{
    [JsonProperty("target_id")] public string TargetId { get; set; } = "";
    [JsonProperty("navisworks_year")] public int NavisworksYear { get; set; }
    [JsonProperty("process_id")] public int ProcessId { get; set; }
    [JsonProperty("host_product")] public string HostProduct { get; set; } = "";
    [JsonProperty("port")] public int Port { get; set; }
    [JsonProperty("auth_token")] public string AuthToken { get; set; } = "";
    [JsonProperty("document_title")] public string? DocumentTitle { get; set; }
    [JsonProperty("document_path")] public string? DocumentPath { get; set; }
    [JsonProperty("last_heartbeat_utc")] public DateTimeOffset LastHeartbeatUtc { get; set; }
}
