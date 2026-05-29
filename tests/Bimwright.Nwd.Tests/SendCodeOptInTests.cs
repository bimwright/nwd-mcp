using System;
using System.Reflection;
using System.Linq;
using Bimwright.Nwd.Server;
using ModelContextProtocol.Server;

namespace Bimwright.Nwd.Tests;

public sealed class SendCodeOptInTests
{
    private static string[] Names(NwdMcpConfig c)
        => Program.ResolveToolTypesForRegistration(c)
                  .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                  .Select(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false)
                                .Cast<McpServerToolAttribute>().FirstOrDefault()?.Name)
                  .Where(n => n is not null).Select(n => n!).ToArray();

    [Fact] public void DefaultExcludesSendCode()
        => Assert.DoesNotContain("nwd_send_code", Names(new NwdMcpConfig()));

    [Fact] public void EnableSendCodeIncludesIt()
        => Assert.Contains("nwd_send_code", Names(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true }));

    [Fact] public void ReadOnlyDropsSendCodeEvenWhenEnabled()
        => Assert.DoesNotContain("nwd_send_code", Names(new NwdMcpConfig { Toolsets = new() { "all" }, EnableSendCode = true, ReadOnly = true }));
}
