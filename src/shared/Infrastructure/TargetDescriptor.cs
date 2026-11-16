using System;
using Newtonsoft.Json;

namespace Bimwright.Nwd.Shared.Infrastructure;

/// <summary>
/// Per-instance discovery descriptor (BW-TARGET-001 §5.2, schema v3). The legacy fields
/// (<c>navisworks_year</c>, <c>process_id</c>, <c>host_product</c>, heartbeat) keep their old meaning so
/// older gateways still read files written by a newer plug-in.
/// </summary>
public sealed class TargetDescriptor
{
    public const int CurrentSchemaVersion = 3;

    [JsonProperty("schema_version")] public int SchemaVersion { get; set; }
    [JsonProperty("host_app")] public string? HostApp { get; set; }
    [JsonProperty("host_year")] public int HostYear { get; set; }
    [JsonProperty("pid")] public int Pid { get; set; }
    /// <summary>Host process start time (UTC). Missing on descriptors from older plug-ins.</summary>
    [JsonProperty("process_start_utc")] public DateTime? ProcessStartUtc { get; set; }
    [JsonProperty("transport")] public string? Transport { get; set; }
    [JsonProperty("capabilities")] public string[]? Capabilities { get; set; }

    [JsonProperty("target_id")] public string TargetId { get; set; } = "";
    [JsonProperty("navisworks_year")] public int NavisworksYear { get; set; }
    [JsonProperty("process_id")] public int ProcessId { get; set; }
    [JsonProperty("host_product")] public string HostProduct { get; set; } = "";
    [JsonProperty("port")] public int Port { get; set; }
    [JsonProperty("auth_token")] public string AuthToken { get; set; } = "";
    [JsonProperty("document_title")] public string? DocumentTitle { get; set; }
    [JsonProperty("document_path")] public string? DocumentPath { get; set; }
    /// <summary>Display only; never used for liveness (BW-TARGET-001 §5.6).</summary>
    [JsonProperty("last_heartbeat_utc")] public DateTimeOffset LastHeartbeatUtc { get; set; }

    /// <summary>Normalized year: <c>host_year</c>, else legacy <c>navisworks_year</c>.</summary>
    [JsonIgnore] public int Year => HostYear > 0 ? HostYear : NavisworksYear;
    /// <summary>Normalized PID: <c>pid</c>, else legacy <c>process_id</c>.</summary>
    [JsonIgnore] public int ProcessIdNormalized => Pid > 0 ? Pid : ProcessId;

    /// <summary>Start time read from the OS by the reader (null when unreadable).</summary>
    [JsonIgnore] public DateTime? ObservedStartUtc { get; set; }
    /// <summary>True when the descriptor's process_start_utc matched the observed process start.</summary>
    [JsonIgnore] public bool IdentityVerified { get; set; }
}
