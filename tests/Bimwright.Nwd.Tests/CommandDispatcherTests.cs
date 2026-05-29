using System;
using System.Collections.Generic;
using System.Linq;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class CommandDispatcherTests
{
    private sealed class FakeCmd : INwdCommand
    {
        public string Name { get; init; } = "";
        public bool IsReadOnly { get; init; }
        public Func<NwdCommandResult>? Body { get; init; }
        public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
            => Body!();
    }

    private static CommandDispatcher Make(params INwdCommand[] cmds)
        => new(cmds.ToDictionary(c => c.Name), 10 * 1024 * 1024);

    [Fact]
    public void UnknownCommandIsInvalidArgument()
    {
        var d = Make();
        var r = d.Dispatch(new NwdCommandContext(), new NwdCommandEnvelope { Command = "nope" });
        Assert.Equal("INVALID_ARGUMENT", r.Error!.Code);
    }

    [Fact]
    public void WriteCommandBlockedInReadOnly()
    {
        var d = Make(new FakeCmd { Name = "w", IsReadOnly = false, Body = () => throw new Exception("should not run") });
        var r = d.Dispatch(new NwdCommandContext { ReadOnly = true }, new NwdCommandEnvelope { Command = "w" });
        Assert.Equal("READ_ONLY", r.Error!.Code);
    }

    [Fact]
    public void HandlerExceptionBecomesSanitizedApiError()
    {
        var d = Make(new FakeCmd { Name = "boom", IsReadOnly = true, Body = () => throw new InvalidOperationException(@"fail at C:\secret\path.cs") });
        var r = d.Dispatch(new NwdCommandContext(), new NwdCommandEnvelope { Command = "boom" });
        Assert.Equal("API_ERROR", r.Error!.Code);
        Assert.DoesNotContain(@"C:\secret", r.Error!.Message);   // path stripped
    }

    [Fact]
    public void SendCodeBlockedUnlessEnabled()
    {
        var d = Make(new FakeCmd { Name = "send_code", IsReadOnly = false, Body = () => throw new Exception("should not run") });
        var r = d.Dispatch(new NwdCommandContext { EnableSendCode = false }, new NwdCommandEnvelope { Command = "send_code" });
        Assert.Equal("SEND_CODE_DISABLED", r.Error!.Code);
    }

    [Fact]
    public void SuccessfulHandlerResponseKeepsEnvelopeId()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var d = Make(new FakeCmd
        {
            Name = "ping",
            IsReadOnly = true,
            Body = () => NwdCommandResult.Success(Guid.Empty, new JObject { ["ok"] = true }, new NwdResponseMeta())
        });

        var r = d.Dispatch(new NwdCommandContext(), new NwdCommandEnvelope { Id = id, Command = "ping" });

        Assert.Equal(id, r.Id);
    }
}
