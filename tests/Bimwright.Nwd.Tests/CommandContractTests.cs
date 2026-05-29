using System;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class CommandContractTests
{
    [Fact]
    public void EnvelopeRoundTripsSnakeCase()
    {
        var env = new NwdCommandEnvelope
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Command = "get_document_info",
            Params = new JObject(),
            TimeoutMs = 30000,
            AuthToken = "secret"
        };
        var json = JObject.Parse(JsonConvert.SerializeObject(env));
        Assert.Equal("get_document_info", (string?)json["command"]);
        Assert.NotNull(json["timeout_ms"]);   // snake_case on the wire
        Assert.NotNull(json["auth_token"]);
    }

    [Fact]
    public void ResultSerializesErrorAndMeta()
    {
        var meta = new NwdResponseMeta { TargetId = "navis-2026-1", NavisworksYear = 2026, DurationMs = 5 };
        var r = NwdCommandResult.Fail(Guid.Empty, "NO_DOCUMENT", "no active document", meta);
        var json = JObject.Parse(JsonConvert.SerializeObject(r));
        Assert.False((bool)json["ok"]!);
        Assert.Equal("NO_DOCUMENT", (string?)json["error"]!["code"]);
        Assert.Equal(2026, (int?)json["meta"]!["navisworks_year"]);
    }
}
