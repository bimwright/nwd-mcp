using System.Security.Cryptography;
using System.Text;
using Bimwright.Nwd.Shared.Logging;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class NwdCallLogTests : IDisposable
{
    private readonly string _dir;

    public NwdCallLogTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "nwd-mcp-log-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        NwdCallLog.DirectoryOverride = _dir;
        NwdCallLog.SetEnabled(false);
    }

    [Fact]
    public void Off_writes_nothing()
    {
        NwdCallLog.Record("get_document_info", "{}", true, 4, null, "{}");
        Assert.False(File.Exists(Path.Combine(_dir, NwdCallLog.FileName)));
    }

    [Fact]
    public void On_records_every_command_and_redacts_send_code()
    {
        NwdCallLog.SetEnabled(true);
        NwdCallLog.Record("health_check", "{}", true, 1, null, "{\"ok\":true}");
        const string script = "secret-script";
        NwdCallLog.Record("send_code", "{\"code\":\"" + script + "\"}", true, 12, null, "{\"ok\":true}");
        NwdCallLog.SetEnabled(false);
        NwdCallLog.Record("hide_items", "{\"ids\":[1]}", true, 3, null, "{}");

        var lines = File.ReadAllLines(Path.Combine(_dir, NwdCallLog.FileName));
        Assert.Equal(2, lines.Length);
        Assert.Equal("health_check", JObject.Parse(lines[0])["tool"]!.Value<string>());

        var send = JObject.Parse(lines[1]);
        var text = send.ToString();
        Assert.DoesNotContain(script, text);
        Assert.Equal(script.Length, send["params"]!["code_length"]!.Value<int>());
        Assert.Equal(Sha256(script), send["params"]!["code_sha256"]!.Value<string>());
    }

    public void Dispose()
    {
        NwdCallLog.SetEnabled(false);
        NwdCallLog.DirectoryOverride = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string Sha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
