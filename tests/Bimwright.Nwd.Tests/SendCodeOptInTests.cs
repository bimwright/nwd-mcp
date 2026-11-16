using System;
using System.Reflection;
using System.Linq;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;

namespace Bimwright.Nwd.Tests;

public sealed class SendCodeOptInTests
{
    private static string[] Names(NwdMcpConfig c)
        => Program.ResolveToolMethodsForRegistration(c)
                  .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name!)
                  .ToArray();

    [Fact] public void DefaultIncludesSendCode()
        => Assert.Contains("nwd_send_code", Names(new NwdMcpConfig()));

    [Fact] public void DisableSendCodeRemovesTheTool()
        => Assert.DoesNotContain("nwd_send_code", Names(new NwdMcpConfig { EnableSendCode = false }));

    [Fact] public void EnableSendCodeIncludesIt()
        => Assert.Contains("nwd_send_code", Names(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true }));

    [Fact] public void ReadOnlyDropsSendCodeEvenWhenEnabled()
        => Assert.DoesNotContain("nwd_send_code", Names(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true, ReadOnly = true }));
}
