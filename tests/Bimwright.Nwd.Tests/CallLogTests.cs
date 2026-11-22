using System.Security.Cryptography;
using System.Text;
using Bimwright.Nwd.Shared.Logging;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

[Collection("Call log")]
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

    [Fact]
    public void Send_code_data_is_logged_as_size_and_hash_and_errors_and_results_are_masked()
    {
        NwdCallLog.SetEnabled(true);
        const string data = "{\"client\":\"Synthetic Co\",\"ids\":[1,2]}";
        const string secret = "QWxhZGRpbjpvcGVuIHNlc2FtZQQWxhZGRpbjpvcGVu";
        var paramsJson = new JObject { ["code"] = "x", ["data_json"] = data }.ToString(Newtonsoft.Json.Formatting.None);
        NwdCallLog.Record("send_code", paramsJson, false, 5,
            @"runtime error: InvalidOperationException: C:\Users\someone\Documents\model.nwd is locked; Bearer " + secret,
            new JObject { ["ok"] = false, ["result"] = @"D:\Projects\Synthetic\out.csv", ["token"] = secret }.ToString(Newtonsoft.Json.Formatting.None));
        NwdCallLog.SetEnabled(false);

        var entry = JObject.Parse(Assert.Single(File.ReadAllLines(Path.Combine(_dir, NwdCallLog.FileName))));
        var text = entry.ToString();
        Assert.DoesNotContain("Synthetic Co", text);
        Assert.Null(entry["params"]!["data_json"]);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(data), entry["params"]!["data_json_bytes"]!.Value<int>());
        Assert.Equal(Sha256(data), entry["params"]!["data_json_sha256"]!.Value<string>());
        Assert.DoesNotContain("someone", text);
        Assert.DoesNotContain(@"Projects\\Synthetic", text);
        Assert.DoesNotContain(secret, text);
        Assert.Contains("<path>", entry.Value<string>("error"));
        Assert.Contains("InvalidOperationException", entry.Value<string>("error"));
        Assert.Contains("is locked", entry.Value<string>("error"));
        Assert.Contains("<path>", entry.Value<string>("result"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public void Request_override_wins_without_changing_host_toggle(bool? requestEnabled, bool hostEnabled)
    {
        NwdCallLog.SetEnabled(hostEnabled);
        NwdCallLog.Record("get_document_info", "{}", true, 1, null, "{}", requestEnabled);
        Assert.Equal(requestEnabled ?? hostEnabled, File.Exists(Path.Combine(_dir, NwdCallLog.FileName)));
        Assert.Equal(hostEnabled, NwdCallLog.Enabled);
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
