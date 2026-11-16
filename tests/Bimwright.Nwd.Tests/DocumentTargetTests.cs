using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bimwright.Nwd.Server;
using Bimwright.Nwd.Server.Tools;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Tests.Helpers;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Tests;

/// <summary>Family contract FC-DOC-01..06 and FC-PAIR-01..03 (ALIGN-03), owner decisions Q-B and Q-C.</summary>
public sealed class DocumentTargetTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static DocumentTargetEnvelope Envelope(string? id) => new() { ExpectedDocumentId = id };
    private static DocumentStamp Doc(string id) => new(id, id + ".nwd");

    // ---- shared policy ----

    [Theory]
    [InlineData("get_document_info", true)]
    [InlineData("goto_viewpoint", false)]          // D11: transient UI state
    [InlineData("clear_selection", false)]
    [InlineData("select_items_by_search", false)]
    [InlineData("apply_bake", false)]              // changes no Navisworks document
    public void Reads_and_transient_ui_need_no_confirmation(string command, bool isReadOnly)
        => Assert.Null(DocumentTargetPolicy.Check(command, isReadOnly, target: null, Doc("a")));

    [Fact]
    public void Write_from_a_gateway_without_the_envelope_is_pairing_required()
        => Assert.Equal("PAIRING_REQUIRED", DocumentTargetPolicy.Check("hide_items", false, null, Doc("a"))!.Value.Code);

    [Fact]
    public void Write_without_a_confirmed_document_is_unconfirmed()
        => Assert.Equal("DOCUMENT_UNCONFIRMED", DocumentTargetPolicy.Check("hide_items", false, Envelope(null), Doc("a"))!.Value.Code);

    [Fact]
    public void Write_on_another_document_is_document_changed()
    {
        var refused = DocumentTargetPolicy.Check("save_viewpoint", false, Envelope("a"), Doc("b"))!.Value;
        Assert.Equal("DOCUMENT_CHANGED", refused.Code);
        Assert.Contains("was not started", refused.Message);
        Assert.Contains("b.nwd", refused.Message);
    }

    [Fact]
    public void Write_on_the_confirmed_document_runs()
        => Assert.Null(DocumentTargetPolicy.Check("unhide_all", false, Envelope("a"), Doc("a")));

    [Fact]
    public void Open_file_skips_the_id_check_but_still_needs_pairing()
    {
        Assert.Null(DocumentTargetPolicy.Check("open_file", false, Envelope(DocumentTargetPolicy.NoDocument), Doc("a")));
        Assert.Equal("PAIRING_REQUIRED", DocumentTargetPolicy.Check("open_file", false, null, Doc("a"))!.Value.Code);
    }

    [Fact]
    public void Repin_moves_only_from_the_confirmed_document_and_only_for_open_file()
    {
        Assert.Equal("b", DocumentTargetPolicy.Repin("open_file", Envelope("a"), Doc("a"), Doc("b")));
        Assert.Null(DocumentTargetPolicy.Repin("open_file", Envelope("a"), Doc("x"), Doc("b")));   // user opened x in between
        Assert.Null(DocumentTargetPolicy.Repin("open_file", Envelope(null), Doc("a"), Doc("b")));
        Assert.Null(DocumentTargetPolicy.Repin("import_model", Envelope("a"), Doc("a"), Doc("b")));
        Assert.Equal("b", DocumentTargetPolicy.Repin("open_file", Envelope(DocumentTargetPolicy.NoDocument), DocumentStamp.None, Doc("b")));
    }

    // ---- plug-in dispatcher (authoritative check before the handler) ----

    private sealed class Cmd(string name, bool isReadOnly, Action? run = null) : INwdCommand
    {
        public string Name => name;
        public bool IsReadOnly => isReadOnly;
        public int Runs { get; private set; }
        public NwdCommandResult Execute(NwdCommandContext context, JObject parameters)
        {
            Runs++;
            run?.Invoke();
            return NwdCommandResult.Success(Guid.Empty, new JObject(), new NwdResponseMeta());
        }
    }

    [Fact]
    public void Dispatcher_refuses_a_write_on_another_document_before_the_handler()
    {
        var cmd = new Cmd("hide_items", false);
        var dispatcher = new CommandDispatcher(new[] { cmd }.ToDictionary(c => c.Name, c => (INwdCommand)c), 1 << 20);
        var result = dispatcher.Dispatch(
            new NwdCommandContext { DocumentTarget = Envelope("a"), ReadDocument = () => Doc("b") },
            new NwdCommandEnvelope { Command = "hide_items" });

        Assert.False(result.Ok);
        Assert.Equal("DOCUMENT_CHANGED", result.Error!.Code);
        Assert.Equal(0, cmd.Runs);
        Assert.Equal("b", result.Meta.DocumentId);
    }

    [Fact]
    public void Dispatcher_reports_the_document_and_repins_after_open_file()
    {
        var open = "a";
        var cmd = new Cmd("open_file", false, () => open = "b");
        var dispatcher = new CommandDispatcher(new[] { cmd }.ToDictionary(c => c.Name, c => (INwdCommand)c), 1 << 20);
        var result = dispatcher.Dispatch(
            new NwdCommandContext { DocumentTarget = Envelope("a"), ReadDocument = () => Doc(open) },
            new NwdCommandEnvelope { Command = "open_file" });

        Assert.True(result.Ok);
        Assert.Equal("b", result.Meta.DocumentId);
        Assert.Equal("b", result.Meta.RepinDocumentId);
    }

    [Fact]
    public void Dispatcher_refuses_writes_from_an_old_gateway_but_serves_reads()
    {
        var write = new Cmd("save_viewpoint", false);
        var read = new Cmd("list_viewpoints", true);
        var dispatcher = new CommandDispatcher(new INwdCommand[] { write, read }.ToDictionary(c => c.Name), 1 << 20);
        var ctx = new NwdCommandContext { ReadDocument = () => Doc("a") };   // no document_target envelope

        Assert.Equal("PAIRING_REQUIRED", dispatcher.Dispatch(ctx, new NwdCommandEnvelope { Command = "save_viewpoint" }).Error!.Code);
        Assert.True(dispatcher.Dispatch(ctx, new NwdCommandEnvelope { Command = "list_viewpoints" }).Ok);
        Assert.Equal(0, write.Runs);
    }

    // ---- gateway (early refusal, nothing sent) ----

    [Fact]
    public async Task Write_is_not_sent_until_the_document_is_confirmed()
    {
        using var plugin = new FakePlugin(confirmed: false);
        using var call = TargetCallContext.Begin(plugin.Client.Generation);

        var ex = await Assert.ThrowsAsync<NwdGatewayException>(() => plugin.Client.SendAsync("hide_items", new JObject(), Ct));

        Assert.Equal("TARGET_UNCONFIRMED", ex.Code);
        Assert.False((bool?)ex.Target!["sent"]);
        Assert.Equal("nwd_switch_target", (string?)ex.Target["recommended_next_tool"]);
        Assert.Contains("verify=true", ex.Message);
        Assert.Empty(plugin.Received);
        Assert.Equal("TARGET_UNCONFIRMED", TargetCallContext.Value!.FailureCode);
    }

    [Fact]
    public async Task Reads_and_transient_ui_are_sent_without_confirmation_and_carry_the_envelope()
    {
        using var plugin = new FakePlugin(confirmed: false);
        await plugin.Client.SendAsync("get_document_info", new JObject(), Ct);
        await plugin.Client.SendAsync("goto_viewpoint", new JObject(), Ct);

        var sent = plugin.Received.ToArray();
        Assert.Equal(new[] { "get_document_info", "goto_viewpoint" }, sent.Select(e => e.Command));
        Assert.All(sent, e =>
        {
            Assert.Equal(DocumentTargetPolicy.Protocol, e.DocumentTarget?.Protocol);
            Assert.Null(e.DocumentTarget!.ExpectedDocumentId);
        });
    }

    [Fact]
    public async Task Verify_pins_the_open_document_and_writes_carry_it()
    {
        using var plugin = new FakePlugin(confirmed: false);
        var meta = new MetaTools(plugin.Client);

        var switched = JObject.Parse(await meta.SwitchTarget("pid:" + Environment.ProcessId, verify: true));
        await plugin.Client.SendAsync("hide_items", new JObject(), Ct);

        Assert.True((bool?)switched["document_confirmed"]);
        Assert.Equal(FakePlugin.DocumentId, (string?)switched["confirmed_document_id"]);
        Assert.Equal("fake.nwd", (string?)switched["confirmed_document_title"]);
        var write = plugin.Received.Last();
        Assert.Equal("hide_items", write.Command);
        Assert.Equal(FakePlugin.DocumentId, write.DocumentTarget!.ExpectedDocumentId);
    }

    [Fact]
    public async Task Switch_without_verify_reports_the_next_step()
    {
        using var plugin = new FakePlugin(confirmed: false);
        var switched = JObject.Parse(await new MetaTools(plugin.Client).SwitchTarget("pid:" + Environment.ProcessId));

        Assert.False((bool?)switched["document_confirmed"]);
        Assert.Contains("verify=true", (string?)switched["document_next_step"]);
        Assert.Empty(plugin.Received);
    }

    [Fact]
    public async Task Selecting_the_same_instance_again_keeps_the_confirmed_document()
    {
        using var plugin = new FakePlugin();
        plugin.Client.SwitchTarget("pid:" + Environment.ProcessId);
        Assert.True((bool?)plugin.Client.CurrentTargetState()["document_confirmed"]);
        await plugin.Client.SendAsync("unhide_all", new JObject(), Ct);
        Assert.Equal("unhide_all", plugin.Single().Command);
    }

    [Fact]
    public async Task Open_file_moves_the_confirmation_to_the_new_file()
    {
        using var plugin = new FakePlugin();
        plugin.Reply = env => NwdCommandResult.Success(env.Id, new JObject(), env.Command == "open_file"
            ? new NwdResponseMeta { DocumentId = "doc-2", DocumentTitle = "next.nwd", RepinDocumentId = "doc-2" }
            : new NwdResponseMeta { DocumentId = "doc-2", DocumentTitle = "next.nwd" });

        await plugin.Client.SendAsync("open_file", new JObject(), Ct);
        await plugin.Client.SendAsync("hide_items", new JObject(), Ct);

        var state = plugin.Client.CurrentTargetState();
        Assert.Equal("doc-2", (string?)state["confirmed_document_id"]);
        Assert.Equal("next.nwd", (string?)state["confirmed_document_title"]);
        Assert.Equal("doc-2", plugin.Received.Last().DocumentTarget!.ExpectedDocumentId);
    }

    [Fact]
    public async Task Document_refusal_from_the_plugin_is_not_started_with_a_target_payload()
    {
        using var plugin = new FakePlugin();
        plugin.Reply = env => NwdCommandResult.Fail(env.Id, "DOCUMENT_CHANGED", "hide_items was not started: the open document changed.",
            new NwdResponseMeta { DocumentId = "doc-9", DocumentTitle = "other.nwd" });
        using var call = TargetCallContext.Begin(plugin.Client.Generation);

        var ex = await Assert.ThrowsAsync<NwdGatewayException>(() => plugin.Client.SendAsync("hide_items", new JObject(), Ct));

        Assert.Equal("DOCUMENT_CHANGED", ex.Code);
        Assert.Equal("not_started", (string?)ex.Target!["outcome"]);
        Assert.Equal("other.nwd", (string?)ex.Target["document_title"]);
        Assert.Equal("nwd_switch_target", (string?)ex.Target["recommended_next_tool"]);
    }

    [Fact]
    public async Task Old_plugin_gets_reads_but_writes_and_verify_are_pairing_required()
    {
        using var plugin = new FakePlugin(documentTarget: false);
        await plugin.Client.SendAsync("list_viewpoints", new JObject(), Ct);

        var write = await Assert.ThrowsAsync<NwdGatewayException>(() => plugin.Client.SendAsync("save_viewpoint", new JObject(), Ct));
        var verify = JObject.Parse(await new MetaTools(plugin.Client).SwitchTarget("pid:" + Environment.ProcessId, verify: true));

        Assert.Equal("PAIRING_REQUIRED", write.Code);
        Assert.False((bool?)write.Target!["sent"]);
        Assert.Equal("PAIRING_REQUIRED", (string?)verify["error"]?["code"]);
        Assert.DoesNotContain(plugin.Received, e => e.Command == "save_viewpoint");
    }
}
