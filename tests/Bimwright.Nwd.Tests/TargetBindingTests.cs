using Bimwright.Nwd.Server;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>Family contract FC-TGT-01..06 / BW-TARGET-001 §6 against a fake process table.</summary>
public sealed class TargetBindingTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);
    private readonly List<TargetDescriptor> _live = new();
    private readonly Dictionary<int, ProcessProbe> _processes = new();

    private TargetDescriptor Host(int pid, int year = 2025, string? title = null, DateTime? start = null)
    {
        var started = start ?? T0.AddMinutes(pid);
        _processes[pid] = new ProcessProbe(ProcessState.Alive, started);
        var d = new TargetDescriptor
        {
            TargetId = $"navis-{year}-{pid}", HostYear = year, NavisworksYear = year, Pid = pid, ProcessId = pid,
            ProcessStartUtc = started, ObservedStartUtc = started, IdentityVerified = true,
            HostApp = "navisworks", Port = 1000 + pid, AuthToken = "t" + pid, DocumentTitle = title
        };
        _live.Add(d);
        return d;
    }

    private void Kill(int pid)
    {
        _processes[pid] = new ProcessProbe(ProcessState.Gone, null);
        _live.RemoveAll(d => d.ProcessIdNormalized == pid);
    }

    private TargetBinding Binding(string? selector = null)
    {
        Assert.True(TargetSelector.TryParse(selector, out var parsed, out _));
        return new TargetBinding(parsed, () => _live.ToList(),
            pid => _processes.TryGetValue(pid, out var p) ? p : new ProcessProbe(ProcessState.Gone, null),
            pid => "Navisworks " + pid);
    }

    /// <summary>One wire command that reaches the instance: capture, validate, write (commit).</summary>
    private static TargetCall Send(TargetBinding binding, long? pinned = null)
    {
        var call = binding.Capture(pinned);
        binding.Validate(call);
        binding.Commit(call);
        return call;
    }

    private static NwdGatewayException Refused(Action action, string code)
    {
        var ex = Assert.Throws<NwdGatewayException>(action);
        Assert.Equal(code, ex.Code);
        Assert.Equal(code, (string?)ex.Target!["code"]);
        return ex;
    }

    [Fact]
    public void NoLiveInstanceIsNoTarget()
        => Refused(() => Binding().Capture(null), "NO_TARGET");

    [Fact]
    public void SingleInstanceIsSelectedOnFirstCommand()
    {
        Host(10);
        var binding = Binding();
        var call = binding.Capture(null);
        Assert.Equal("navis-2025-10", call.Descriptor.TargetId);
        Assert.Equal(1, call.Generation);
    }

    [Fact]
    public void TwoMatchingInstancesWithNoChoiceAreAmbiguous() // FC-TGT-02
    {
        Host(10, 2025); Host(11, 2026);
        var binding = Binding();
        var ex = Refused(() => binding.Capture(null), "AMBIGUOUS_TARGET");
        Assert.False((bool)ex.Target!["sent"]!);
        Assert.Equal(2, ((JArray)ex.Target["candidates"]!).Count);
        Assert.Equal("none", (string?)binding.Snapshot()["binding_state"]);
    }

    [Fact]
    public void YearSelectorChoosesOnlyThatYear()
    {
        Host(10, 2025); Host(11, 2026);
        Assert.Equal("navis-2025-10", Binding("2025").Capture(null).Descriptor.TargetId);
    }

    [Fact]
    public void LostImplicitInstanceNeverFallsThroughToAnother() // FC-TGT-01, §6.5
    {
        Host(10);
        var binding = Binding();
        Send(binding);
        Host(11);
        Kill(10);

        var ex = Refused(() => binding.Capture(null), "TARGET_CHANGED");
        Assert.Equal("not_started", (string?)ex.Target!["outcome"]);
        Assert.Equal("navis-2025-11", (string?)ex.Target["current"]!["target_id"]);
        Refused(() => binding.Capture(null), "TARGET_CHANGED"); // stays lost until confirmed
        var state = binding.Snapshot();
        Assert.Equal("lost", (string?)state["binding_state"]);
        Assert.True((bool)state["confirmation_required"]!);
        Assert.Equal("navis-2025-11", (string?)state["next_target_id"]);

        binding.Switch("pid:11", rebind: false);
        var call = binding.Capture(null);
        Assert.Equal("navis-2025-11", call.Descriptor.TargetId);
        Assert.Equal(2, call.Generation);
    }

    [Fact]
    public void LostImplicitInstanceWithTwoOthersIsAmbiguous()
    {
        Host(10);
        var binding = Binding();
        Send(binding);
        Host(11); Host(12);
        Kill(10);
        Refused(() => binding.Capture(null), "AMBIGUOUS_TARGET");
    }

    [Fact]
    public void LostExplicitInstanceIsTargetLost()
    {
        Host(10); Host(11);
        var binding = Binding("pid:10");
        Send(binding);
        Kill(10);
        var ex = Refused(() => binding.Capture(null), "TARGET_LOST");
        Assert.Single((JArray)ex.Target!["candidates"]!);
    }

    [Fact]
    public void ReusedPidIsDetectedByProcessStartTime() // FC-TGT-04
    {
        Host(10);
        var binding = Binding();
        Send(binding);
        _processes[10] = new ProcessProbe(ProcessState.Alive, T0.AddHours(5)); // same PID, new process
        _live.Clear();
        Refused(() => binding.Capture(null), "NO_TARGET");
        Assert.Equal("lost", (string?)binding.Snapshot()["binding_state"]);
    }

    [Fact]
    public void RunningInstanceWithoutDescriptorIsUnavailableAndKeepsBinding()
    {
        Host(10);
        var binding = Binding();
        Send(binding);
        _live.Clear(); // listener stopped, process alive
        Refused(() => binding.Capture(null), "TARGET_UNAVAILABLE");
        Assert.Equal("bound", (string?)binding.Snapshot()["binding_state"]);
    }

    [Fact]
    public void ListenerRestartRefreshesEndpointForTheSameInstance()
    {
        var host = Host(10);
        var binding = Binding();
        Send(binding);
        host.Port = 4242; host.AuthToken = "fresh";
        var call = binding.Capture(null);
        Assert.Equal(4242, call.Descriptor.Port);
        Assert.Equal(1, call.Generation);
    }

    [Fact]
    public void ImplicitSelectorCannotMoveAnExistingBinding() // FC-TGT-03
    {
        Host(10, 2025);
        var binding = Binding();
        Send(binding);
        Host(11, 2026);
        Refused(() => binding.Switch("2026", rebind: false), "TARGET_REBIND_REQUIRED");
        Refused(() => binding.Switch("auto", rebind: false), "AMBIGUOUS_TARGET");
        Assert.Equal("navis-2025-10", (string?)binding.Snapshot()["bound_target_id"]);

        binding.Switch("pid:11", rebind: false); // first choice was implicit, so no rebind needed
        Assert.Equal("navis-2026-11", (string?)binding.Snapshot()["bound_target_id"]);
    }

    [Fact]
    public void LeavingALiveExplicitChoiceNeedsRebind() // FC-TGT-03
    {
        Host(10); Host(11);
        var binding = Binding();
        binding.Switch("pid:10", rebind: false);
        Refused(() => binding.Switch("pid:11", rebind: false), "TARGET_REBIND_REQUIRED");
        Assert.Equal(1, binding.Generation);

        var result = binding.Switch("id:navis-2025-11", rebind: true);
        Assert.True((bool)result["ok"]!);
        Assert.Equal("navis-2025-10", (string?)result["previous"]!["target_id"]);
        Assert.Equal(2, binding.Generation);
        Assert.Equal("navis-2025-11", (string?)binding.Snapshot()["last_switch"]!["to"]);
    }

    [Fact]
    public void SwitchingToTheSameInstanceKeepsTheGeneration()
    {
        Host(10);
        var binding = Binding();
        Send(binding);
        binding.Switch("navis-2025-10", rebind: false);
        Assert.Equal(1, binding.Generation);
        Assert.Equal("explicit", (string?)binding.Snapshot()["binding"]);
    }

    [Theory] // FC-TGT-06
    [InlineData("R24")]
    [InlineData("2030")]
    [InlineData("pid:abc")]
    [InlineData("pid:0")]
    [InlineData("id:")]
    [InlineData("revit-2025-1")]
    public void InvalidSelectorIsRefusedAndKeepsTheBinding(string selector)
    {
        Host(10);
        var binding = Binding();
        Send(binding);
        Refused(() => binding.Switch(selector, rebind: false), "INVALID_TARGET_SELECTOR");
        Assert.Equal("navis-2025-10", (string?)binding.Snapshot()["bound_target_id"]);
    }

    [Fact]
    public void SwitchToUnknownInstanceIsNoTargetAndKeepsTheBinding()
    {
        Host(10);
        var binding = Binding();
        Send(binding);
        Refused(() => binding.Switch("pid:99", rebind: false), "NO_TARGET");
        Assert.Equal(1, binding.Generation);
    }

    [Fact]
    public void ListAndGetNeverSelect() // FC-TGT-05
    {
        Host(10);
        var binding = Binding();
        var rows = binding.ListRows();
        var state = binding.Snapshot();
        Assert.False((bool)rows[0]["is_current"]!);
        Assert.Equal("Navisworks 10", (string?)rows[0]["window_title"]);
        Assert.Equal("none", (string?)state["binding_state"]);
        Assert.Equal(0, (long)state["generation"]!);
        Assert.Equal("navis-2025-10", (string?)state["next_target_id"]);
        Assert.Equal(0, binding.Generation);
    }

    [Fact]
    public void CallPinnedToAnOldGenerationIsNotSent() // §6.6
    {
        Host(10);
        var binding = Binding();
        var first = Send(binding, binding.Generation); // pinned at none (0): may make the first choice
        Assert.Equal(1, first.Generation);
        Host(11);
        binding.Switch("pid:11", rebind: false);

        Refused(() => binding.Capture(1), "TARGET_CHANGED");
        Refused(() => binding.Validate(first), "TARGET_CHANGED");
        Assert.Equal("navis-2025-11", binding.Capture(2).Descriptor.TargetId);
    }

    [Fact]
    public void CallPinnedBeforeAnyBindingIsNotSentAfterTheBindingMoved() // review P1, §6.6
    {
        Host(10);
        var binding = Binding();
        const long arrivedAtNone = 0;           // e.g. delayed by a SQLite read before its first wire command
        Send(binding, arrivedAtNone);           // another call makes the first binding: A, generation 1
        Assert.Equal("navis-2025-10", binding.Capture(arrivedAtNone).Descriptor.TargetId); // still the first binding
        Host(11);
        binding.Switch("pid:11", rebind: true); // A -> B, generation 2

        Refused(() => binding.Capture(arrivedAtNone), "TARGET_CHANGED");
    }

    [Fact]
    public void FirstSelectionIsNotBoundUntilTheRequestIsWritten() // review P2, §6.3-6.4
    {
        Host(10);
        var binding = Binding();
        var call = binding.Capture(null);       // connect to A fails after this: nothing written
        Assert.True(call.Tentative);
        Assert.Equal("none", (string?)binding.Snapshot()["binding_state"]);

        Kill(10); Host(11);                     // A goes away, B appears
        var next = Send(binding);
        Assert.Equal("navis-2025-11", next.Descriptor.TargetId); // first selection, not TARGET_LOST
        Assert.Equal("bound", (string?)binding.Snapshot()["binding_state"]);
        Assert.Equal(1, binding.Generation);
    }

    [Fact]
    public void ConcurrentFirstCallsToTheSameInstanceBothGoOut()
    {
        Host(10);
        var binding = Binding();
        var a = binding.Capture(0);
        var b = binding.Capture(0);
        binding.Validate(a); binding.Commit(a);
        binding.Validate(b); binding.Commit(b);  // same instance, nothing changed since the first binding
        Assert.Equal(1, binding.Generation);
    }

    [Fact]
    public void TentativeCallIsRefusedWhenASwitchBoundAnotherInstanceFirst()
    {
        Host(10); Host(11, 2026);
        var binding = Binding("2025");
        var call = binding.Capture(0);           // tentative: A
        binding.Switch("pid:11", rebind: false); // the agent pins B before the call is written
        Refused(() => binding.Validate(call), "TARGET_CHANGED");
    }

    private TargetBinding SandboxedBinding(string? selector = null)
    {
        Assert.True(TargetSelector.TryParse(selector, out var parsed, out _));
        return new TargetBinding(parsed, () => _live.ToList(),
            pid => _processes.TryGetValue(pid, out var p) ? p : new ProcessProbe(ProcessState.Gone, null),
            pid => "Navisworks " + pid,
            () => new JObject { ["detected"] = true, ["kind"] = "appcontainer" });
    }

    [Fact]
    public void FC_TGT_07_SandboxedLostBindingWithNoLiveInstanceIsClientSandboxed()
    {
        Host(10);
        var binding = SandboxedBinding();
        Send(binding);
        Kill(10);
        var ex = Refused(() => binding.Capture(null), "CLIENT_SANDBOXED");
        Assert.Equal(ClientSandbox.Message, ex.Message);
        Assert.Equal("appcontainer", (string?)ex.Target!["sandbox"]?["kind"]);
    }

    [Fact]
    public void FC_TGT_07_SandboxedButOtherInstancesLiveKeepsNoTarget()
    {
        Host(10, year: 2025);
        Refused(() => SandboxedBinding("2027").Capture(null), "NO_TARGET");
    }

    [Theory]
    [InlineData(null, "auto")]
    [InlineData(" AUTO ", "auto")]
    [InlineData("2027", "2027")]
    [InlineData("PID:42", "pid:42")]
    [InlineData("id:navis-2025-7", "id:navis-2025-7")]
    [InlineData("navis-2025-7", "id:navis-2025-7")]
    public void SelectorsNormalize(string? raw, string expected)
    {
        Assert.True(TargetSelector.TryParse(raw, out var selector, out var error), error);
        Assert.Equal(expected, selector.Text);
    }
}
