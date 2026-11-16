using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

public sealed class NwdMcpConfig
{
    public bool ReadOnly { get; set; }
    public bool EnableSendCode { get; set; } = true;
    public bool EnableCallLog { get; set; }
    public bool EnableToolBaker { get; set; } = true;
    public bool EnableAdaptiveBake { get; set; }
    public int TimeoutMs { get; set; } = 30000;
    public int MaxResponseBytes { get; set; } = 10 * 1024 * 1024;

    // Agent-facing response guard (UTF-8 bytes). Off: no warnings and the budget falls back to
    // MaxResponseBytes, which stays active either way.
    public bool EnableResponseGuard { get; set; } = true;
    public int ResponseWarnBytes { get; set; } = 64 * 1024;
    public int ResponseStrongWarnBytes { get; set; } = 256 * 1024;
    public int ResponseBudgetBytes { get; set; } = 1024 * 1024;

    public const int DefaultSpillRetentionHours = 36;
    public const int MaxSpillRetentionHours = 24 * 365;
    /// <summary>Hours a spill file is kept. A missing or invalid value (outside 1..8760) uses 36.</summary>
    public int SpillRetentionHours { get; set; } = DefaultSpillRetentionHours;

    public string? TargetId { get; set; }
    public List<string> Toolsets { get; set; } = new();

    private static string DataDirectory => Path.Combine(
        Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } root
            ? root : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Bimwright", "nwd-mcp");
    public string DescriptorDirectory { get; set; } = DataDirectory;
    public string BakeDirectory { get; set; } = Path.Combine(DataDirectory, "baked");
    /// <summary>Saved agent reports (nwd_report_task_result, family contract FC-RPT-04).</summary>
    public string ReportDirectory { get; set; } = Path.Combine(DataDirectory, "reports");

    public static NwdMcpConfig Load(string[] args)
    {
        var config = new NwdMcpConfig();
        ApplyJson(config, args);   // lowest precedence
        ApplyEnv(config);          // middle
        ApplyCli(config, args);    // highest
        config.ValidateResponseLimits();
        return config;
    }

    /// <summary>Thresholds must be integers >= 1024 ordered warn &lt;= strong &lt;= budget &lt;= max.</summary>
    public void ValidateResponseLimits()
    {
        if (ResponseWarnBytes < 1024 || ResponseStrongWarnBytes < 1024 || ResponseBudgetBytes < 1024 || MaxResponseBytes < 1024
            || ResponseWarnBytes > ResponseStrongWarnBytes
            || ResponseStrongWarnBytes > ResponseBudgetBytes
            || ResponseBudgetBytes > MaxResponseBytes)
            throw new ArgumentException(
                "Response limits must be integers >= 1024 ordered responseWarnBytes <= responseStrongWarnBytes <= responseBudgetBytes <= maxResponseBytes " +
                $"(got {ResponseWarnBytes}, {ResponseStrongWarnBytes}, {ResponseBudgetBytes}, {MaxResponseBytes}).");
    }

    internal static int NormalizeSpillRetentionHours(string? text)
        => int.TryParse(text?.Trim(), out var hours) && hours >= 1 && hours <= MaxSpillRetentionHours
            ? hours : DefaultSpillRetentionHours;

    // --- precedence layer 1: JSON file (via --config <path>) ---
    private static void ApplyJson(NwdMcpConfig c, string[] args)
    {
        var path = ValueAfter(args, "--config");
        if (path is null || !File.Exists(path)) return;
        var o = JObject.Parse(File.ReadAllText(path));
        if (o["readOnly"] is { } ro) c.ReadOnly = ro.Value<bool>();
        if (o["enableSendCode"] is { } sc) c.EnableSendCode = sc.Value<bool>();
        if (o["enableCallLog"] is { } cl) c.EnableCallLog = cl.Value<bool>();
        if (o["enableToolBaker"] is { } tb) c.EnableToolBaker = tb.Value<bool>();
        if (o["timeoutMs"] is { } tm) c.TimeoutMs = tm.Value<int>();
        if (o["maxResponseBytes"] is { } mb) c.MaxResponseBytes = mb.Value<int>();
        if (o["enableResponseGuard"] is { } rg) c.EnableResponseGuard = rg.Value<bool>();
        if (o["responseWarnBytes"] is { } rw) c.ResponseWarnBytes = rw.Value<int>();
        if (o["responseStrongWarnBytes"] is { } rs) c.ResponseStrongWarnBytes = rs.Value<int>();
        if (o["responseBudgetBytes"] is { } rb) c.ResponseBudgetBytes = rb.Value<int>();
        if (o["spillRetentionHours"] is { } sr) c.SpillRetentionHours = NormalizeSpillRetentionHours(sr.ToString());
        if (o["target"] is { } tg) c.TargetId = tg.Value<string>();
        if (o["toolsets"] is JArray arr) c.Toolsets = arr.Select(x => x.Value<string>()!).ToList();
    }

    // --- precedence layer 2: environment variables ---
    private static void ApplyEnv(NwdMcpConfig c)
    {
        if (Bool("BIMWRIGHT_NWD_READ_ONLY") is { } ro) c.ReadOnly = ro;
        if (Bool("BIMWRIGHT_NWD_ENABLE_SEND_CODE") is { } sc) c.EnableSendCode = sc;
        if (Bool("BIMWRIGHT_NWD_ENABLE_CALL_LOG") is { } cl) c.EnableCallLog = cl;
        if (Bool("BIMWRIGHT_NWD_ENABLE_TOOLBAKER") is { } tb) c.EnableToolBaker = tb;
        if (Bool("BIMWRIGHT_NWD_ENABLE_ADAPTIVE_BAKE") is { } ab) c.EnableAdaptiveBake = ab;
        if (Int("BIMWRIGHT_NWD_TIMEOUT_MS") is { } tm) c.TimeoutMs = tm;
        if (Int("BIMWRIGHT_NWD_MAX_RESPONSE_BYTES") is { } mb) c.MaxResponseBytes = mb;
        if (Bool("BIMWRIGHT_NWD_ENABLE_RESPONSE_GUARD") is { } rg) c.EnableResponseGuard = rg;
        if (Int("BIMWRIGHT_NWD_RESPONSE_WARN_BYTES") is { } rw) c.ResponseWarnBytes = rw;
        if (Int("BIMWRIGHT_NWD_RESPONSE_STRONG_WARN_BYTES") is { } rs) c.ResponseStrongWarnBytes = rs;
        if (Int("BIMWRIGHT_NWD_RESPONSE_BUDGET_BYTES") is { } rb) c.ResponseBudgetBytes = rb;
        var sr = Environment.GetEnvironmentVariable("BIMWRIGHT_NWD_SPILL_RETENTION_HOURS");
        if (!string.IsNullOrWhiteSpace(sr)) c.SpillRetentionHours = NormalizeSpillRetentionHours(sr);
        var tg = Environment.GetEnvironmentVariable("BIMWRIGHT_NWD_TARGET");
        if (!string.IsNullOrWhiteSpace(tg)) c.TargetId = tg;
        var ts = Environment.GetEnvironmentVariable("BIMWRIGHT_NWD_TOOLSETS");
        if (!string.IsNullOrWhiteSpace(ts)) c.Toolsets = SplitCsv(ts);
    }

    // --- precedence layer 3: CLI flags (highest) ---
    private static void ApplyCli(NwdMcpConfig c, string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--read-only":            c.ReadOnly = NextBool(args, ref i, true); break;
                case "--enable-send-code":     c.EnableSendCode = true; break;
                case "--disable-send-code":    c.EnableSendCode = false; break;
                case "--enable-call-log":      c.EnableCallLog = true; break;
                case "--disable-call-log":     c.EnableCallLog = false; break;
                case "--disable-toolbaker":    c.EnableToolBaker = false; break;
                case "--enable-adaptive-bake": c.EnableAdaptiveBake = true; break;
                case "--toolsets":             c.Toolsets = SplitCsv(Next(args, ref i)); break;
                case "--target":               c.TargetId = Next(args, ref i); break;
                case "--timeout-ms":           if (int.TryParse(Next(args, ref i), out var t)) c.TimeoutMs = t; break;
                case "--max-response-bytes":   if (int.TryParse(Next(args, ref i), out var m)) c.MaxResponseBytes = m; break;
                case "--enable-response-guard":  c.EnableResponseGuard = true; break;
                case "--disable-response-guard": c.EnableResponseGuard = false; break;
                case "--response-warn-bytes":        if (int.TryParse(Next(args, ref i), out var rw)) c.ResponseWarnBytes = rw; break;
                case "--response-strong-warn-bytes": if (int.TryParse(Next(args, ref i), out var rs)) c.ResponseStrongWarnBytes = rs; break;
                case "--response-budget-bytes":      if (int.TryParse(Next(args, ref i), out var rb)) c.ResponseBudgetBytes = rb; break;
                case "--spill-retention-hours":      c.SpillRetentionHours = NormalizeSpillRetentionHours(Next(args, ref i)); break;
            }
        }
    }

    private static List<string> SplitCsv(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string? ValueAfter(string[] a, string flag)
    {
        var i = Array.IndexOf(a, flag);
        return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : null;
    }
    private static string Next(string[] a, ref int i) => (i + 1 < a.Length) ? a[++i] : "";
    private static bool NextBool(string[] a, ref int i, bool bareValue)
    {
        if (i + 1 < a.Length && bool.TryParse(a[i + 1], out var b)) { i++; return b; }
        return bareValue;
    }
    private static bool? Bool(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(v)) return null;
        return v.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
    }
    private static int? Int(string name)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : null;
}
