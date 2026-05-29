using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Infrastructure;

public sealed class NwdCommandResult
{
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("ok")] public bool Ok { get; set; }
    [JsonProperty("data")] public JToken? Data { get; set; }
    [JsonProperty("error")] public NwdError? Error { get; set; }
    [JsonProperty("meta")] public NwdResponseMeta Meta { get; set; } = new();

    public static NwdCommandResult Success(Guid id, JToken? data, NwdResponseMeta meta)
        => new() { Id = id, Ok = true, Data = data, Meta = meta };

    public static NwdCommandResult Fail(Guid id, string code, string message, NwdResponseMeta meta)
        => new() { Id = id, Ok = false, Error = new NwdError { Code = code, Message = message }, Meta = meta };
}

public sealed class NwdError
{
    [JsonProperty("code")] public string Code { get; set; } = "";
    [JsonProperty("message")] public string Message { get; set; } = "";
}

public sealed class NwdResponseMeta
{
    [JsonProperty("target_id")] public string? TargetId { get; set; }
    [JsonProperty("navisworks_year")] public int? NavisworksYear { get; set; }
    [JsonProperty("duration_ms")] public long DurationMs { get; set; }
    [JsonProperty("read_only_enforced")] public bool? ReadOnlyEnforced { get; set; }
}
