using System.Diagnostics;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>FC-TST-03: the gateway sends clientInfo.name with every command as client_name.</summary>
public sealed class ClientNameWireTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nwd-clientname-" + Guid.NewGuid().ToString("N"));

    // FC-TST-03: envelope shape. Legacy gateways omit the field and the plug-in must tolerate that.
    [Fact]
    public void Client_name_round_trips_when_present_and_is_omitted_when_null()
    {
        var named = new NwdCommandEnvelope { Command = "health_check", ClientName = "claude-code" };
        var json = JObject.Parse(JsonConvert.SerializeObject(named));
        Assert.Equal("claude-code", (string?)json["client_name"]);
        Assert.Equal("claude-code", JsonConvert.DeserializeObject<NwdCommandEnvelope>(json.ToString())!.ClientName);

        var anonymous = new NwdCommandEnvelope { Command = "health_check" };
        Assert.DoesNotContain("client_name", JsonConvert.SerializeObject(anonymous));

        var legacy = JsonConvert.DeserializeObject<NwdCommandEnvelope>("{\"command\":\"health_check\"}")!;
        Assert.Null(legacy.ClientName);
    }

    // FC-TST-03: the real MCP SDK (1.1.0) hands clientInfo to the real call filter, and the name reaches the wire.
    [Fact]
    public async Task Gateway_sends_the_stdio_client_name_to_the_plugin_on_every_command()
    {
        using var plugin = new FakePlugin();
        var data = Path.Combine(_root, "Bimwright", "nwd-mcp");
        Directory.CreateDirectory(data);
        File.Copy(Path.Combine(plugin.Config.DescriptorDirectory, plugin.TargetId + ".json"),
            Path.Combine(data, plugin.TargetId + ".json"));
        var serverAssembly = typeof(Program).Assembly.Location;
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(serverAssembly)!, UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(serverAssembly);
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("BIMWRIGHT_NWD_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(key);
        start.Environment["LOCALAPPDATA"] = _root;
        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await Send(process, new JObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize",
                ["params"] = new JObject { ["protocolVersion"] = "2024-11-05", ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = "claude-code", ["version"] = "2.1.330" } }
            });
            await Read(process);
            await Send(process, new JObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
            for (var id = 2; id <= 3; id++)
            {
                await Send(process, new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = "tools/call",
                    ["params"] = new JObject { ["name"] = "nwd_get_document_info", ["arguments"] = new JObject() } });
                Assert.False((await Read(process))["result"]!.Value<bool>("isError"));
            }
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(process.ExitCode == 0, await stderr);
            var envelopes = plugin.Received.ToArray();
            Assert.NotEmpty(envelopes);
            Assert.All(envelopes, e => Assert.Equal("claude-code", e.ClientName));
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }

    private static Task Send(Process process, JObject request)
        => process.StandardInput.WriteLineAsync(request.ToString(Formatting.None));
    private static async Task<JObject> Read(Process process)
        => JObject.Parse(await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException("Server closed stdout."));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
