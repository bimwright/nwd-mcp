using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

/// <summary>Result of <see cref="ClientSandbox.Classify"/>.</summary>
internal sealed class ClientSandboxResult
{
    public static ClientSandboxResult None { get; } = new(false, null, Array.Empty<string>());

    public ClientSandboxResult(bool detected, string? kind, IReadOnlyList<string> evidence)
    { Detected = detected; Kind = kind; Evidence = evidence; }

    public bool Detected { get; }
    public string? Kind { get; }
    public IReadOnlyList<string> Evidence { get; }

    /// <summary>The <c>sandbox</c> object reported by list, health and CLIENT_SANDBOXED refusals.</summary>
    public JObject ToJson(string? dataDir) => new()
    {
        ["detected"] = Detected,
        ["kind"] = Kind,
        ["evidence"] = new JArray(Evidence),
        ["data_dir"] = dataDir
    };
}

/// <summary>
/// Family contract FC-TGT-07: an MCP client can launch the gateway inside a Windows AppContainer (rvt-mcp#71).
/// There the gateway cannot read the descriptors or reach Navisworks over loopback, so it reports
/// CLIENT_SANDBOXED instead of NO_TARGET. It never tries to leave the sandbox. Host-free: no Navisworks API.
/// </summary>
internal static class ClientSandbox
{
    public const string Code = "CLIENT_SANDBOXED";

    public const string Message =
        "CLIENT_SANDBOXED: nwd-mcp is running inside your MCP client's sandbox (a Windows AppContainer), so it cannot see or reach Navisworks on this computer. " +
        "The command was not sent. The sandbox is how your client protects your computer by limiting what agent tools can access; Navisworks and nwd-mcp are working normally. " +
        "To use nwd-mcp, a person must choose to let this one MCP server run outside the sandbox in the client's settings: prefer a per-server sandbox exception; " +
        "otherwise use a client permission mode that runs local MCP servers unsandboxed. Do not loosen file or folder permissions to work around it. " +
        "Agents: report this to the user and do not try to bypass the sandbox.";

    public const string EvidenceToken = "token_is_appcontainer";
    public const string EvidencePath = "local_app_data_under_packages_ac";

    // ...\Packages\<one segment>\AC followed by a separator or the end of the path.
    private static readonly Regex PackagesAc = new(@"[\\/]Packages[\\/][^\\/]+[\\/]AC(?:[\\/]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Pure classifier. <paramref name="paths"/>: LocalApplicationData as the OS resolves it, and the resolved descriptor directory.</summary>
    public static ClientSandboxResult Classify(bool? tokenIsAppContainer, params string?[] paths)
    {
        var evidence = new List<string>();
        if (tokenIsAppContainer == true) evidence.Add(EvidenceToken);
        foreach (var path in paths)
        {
            if (IsUnderPackagesAc(path))
            {
                evidence.Add(EvidencePath);
                break;
            }
        }
        return evidence.Count == 0 ? ClientSandboxResult.None : new ClientSandboxResult(true, "appcontainer", evidence);
    }

    public static bool IsUnderPackagesAc(string? path)
        => !string.IsNullOrWhiteSpace(path) && PackagesAc.IsMatch(path);

    /// <summary>Classifies this process: token probe plus the OS LocalApplicationData and <paramref name="descriptorDirectory"/>.</summary>
    public static ClientSandboxResult Detect(bool? tokenIsAppContainer, string? descriptorDirectory)
    {
        string? localAppData;
        try { localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        catch { localAppData = null; }
        return Classify(tokenIsAppContainer, localAppData, descriptorDirectory);
    }

    private static readonly Lazy<bool?> TokenProbe = new(ProbeToken);

    /// <summary>TokenIsAppContainer of the current process token, read once. Null off Windows or on failure; never throws.</summary>
    public static bool? ProcessTokenIsAppContainer() => TokenProbe.Value;

    private const uint TokenQuery = 0x0008;
    private const int TokenIsAppContainerClass = 29;

    private static bool? ProbeToken()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token)) return null;
            if (!GetTokenInformation(token, TokenIsAppContainerClass, out var value, sizeof(uint), out _)) return null;
            return value != 0;
        }
        catch { return null; }
        finally
        {
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, out uint tokenInformation,
        int tokenInformationLength, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
