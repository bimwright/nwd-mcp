using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server;

internal enum SelectorKind { Auto, Year, Pid, Id }

/// <summary>Target selector (BW-TARGET-001 §6.1): <c>auto | 2022..2027 | pid:&lt;n&gt; | id:&lt;target_id&gt;</c>, or a bare <c>navis-…</c> target id.</summary>
internal sealed class TargetSelector
{
    private static readonly Regex RCode = new(@"^R\d{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private TargetSelector(SelectorKind kind, string text, int year = 0, int pid = 0, string? id = null)
    { Kind = kind; Text = text; Year = year; Pid = pid; Id = id; }

    public SelectorKind Kind { get; }
    public string Text { get; }
    public int Year { get; }
    public int Pid { get; }
    public string? Id { get; }
    public bool IsExplicit => Kind is SelectorKind.Pid or SelectorKind.Id;

    public static TargetSelector Auto { get; } = new(SelectorKind.Auto, "auto");

    public static bool TryParse(string? raw, out TargetSelector selector, out string? error)
    {
        selector = Auto;
        error = null;
        var text = (raw ?? "").Trim();
        if (text.Length == 0 || text.Equals("auto", StringComparison.OrdinalIgnoreCase)) return true;
        if (RCode.IsMatch(text))
        {
            error = $"'{text}' is a legacy R-code. Use a 4-digit Navisworks year (2022..2027), pid:<n> or id:<target_id> from nwd_list_available_targets.";
            return false;
        }
        if (text.All(char.IsDigit))
        {
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 2022 and <= 2027)
            {
                selector = new TargetSelector(SelectorKind.Year, year.ToString(CultureInfo.InvariantCulture), year: year);
                return true;
            }
            error = $"'{text}' is not a supported Navisworks year (2022..2027). Use pid:<n> for a process id.";
            return false;
        }
        if (text.StartsWith("pid:", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(text.Substring(4).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0)
            {
                selector = new TargetSelector(SelectorKind.Pid, "pid:" + pid.ToString(CultureInfo.InvariantCulture), pid: pid);
                return true;
            }
            error = $"'{text}' is not a valid pid:<n> selector.";
            return false;
        }
        var id = text.StartsWith("id:", StringComparison.OrdinalIgnoreCase) ? text.Substring(3).Trim()
            : text.StartsWith("navis-", StringComparison.OrdinalIgnoreCase) ? text : null;
        if (!string.IsNullOrEmpty(id))
        {
            selector = new TargetSelector(SelectorKind.Id, "id:" + id, id: id);
            return true;
        }
        error = $"'{text}' is not a target selector. Use auto, a year 2022..2027, pid:<n> or id:<target_id>.";
        return false;
    }

    public bool Matches(TargetDescriptor d) => Kind switch
    {
        SelectorKind.Year => d.Year == Year,
        SelectorKind.Pid => d.ProcessIdNormalized == Pid,
        SelectorKind.Id => string.Equals(d.TargetId, Id, StringComparison.OrdinalIgnoreCase),
        _ => true
    };
}

/// <summary>The resolved instance one wire command is sent to, and the binding generation it belongs to.</summary>
internal sealed class TargetCall
{
    public TargetCall(TargetDescriptor descriptor, long generation, bool tentative = false)
    { Descriptor = descriptor; Generation = generation; Tentative = tentative; }
    public TargetDescriptor Descriptor { get; }
    public long Generation { get; }

    /// <summary>
    /// First selection not yet committed (state <c>none</c>, BW-TARGET-001 §6.3-6.4): the binding is set only once
    /// the request bytes are written (<see cref="TargetBinding.Commit"/>). A failed connect keeps state <c>none</c>.
    /// </summary>
    public bool Tentative { get; }
}

/// <summary>
/// Per tool call state (BW-TARGET-001 §8): the binding generation seen when the call arrived, and
/// the target refusal payload the call filter merges into the tool's error.
/// </summary>
internal static class TargetCallContext
{
    internal sealed class State
    {
        /// <summary>0 = the call arrived before the first binding; set to the first generation once that binding is made.</summary>
        public long PinnedGeneration { get; set; }
        public string? FailureCode { get; set; }
        public JObject? Failure { get; set; }
    }

    private static readonly AsyncLocal<State?> Current = new();

    public static State? Value => Current.Value;

    public static IDisposable Begin(long generation)
    {
        var previous = Current.Value;
        Current.Value = new State { PinnedGeneration = generation };
        return new Scope(previous);
    }

    public static void Record(NwdGatewayException ex)
    {
        if (Current.Value is { } state && ex.Target is { } target)
        {
            state.FailureCode = ex.Code;
            state.Failure = target;
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly State? _previous;
        public Scope(State? previous) => _previous = previous;
        public void Dispose() => Current.Value = _previous;
    }
}

/// <summary>
/// One gateway lifetime owns one binding (BW-TARGET-001 §6, family contract FC-TGT-01..06).
/// Discovery is used for the first selection and for <c>nwd_switch_target</c> only; a lost instance
/// never falls through to another one, and an implicit selector never moves an existing binding.
/// </summary>
internal sealed class TargetBinding
{
    private const string SwitchTool = "nwd_switch_target";
    /// <summary>Generation of the first binding of the server session (none → bound).</summary>
    private const long FirstGeneration = 1;
    private readonly object _gate = new();
    private readonly Func<IReadOnlyList<TargetDescriptor>> _list;
    private readonly Func<int, ProcessProbe> _probe;
    private readonly Func<int, string?> _windowTitle;
    // FC-TGT-07: the sandbox object when the gateway runs in a client AppContainer, else null.
    private readonly Func<JObject?> _sandbox;
    private TargetSelector _selector;
    private string _state = "none";
    private long _generation;
    private TargetDescriptor? _bound;
    private DateTime? _observedStart;
    private bool _confirmed;
    private JObject? _lastSwitch;
    // document_target_v1 (FC-DOC): the document confirmed with verify=true, or moved by open_file (Q-B).
    private string? _documentId;
    private string? _documentTitle;

    public TargetBinding(TargetSelector startup, Func<IReadOnlyList<TargetDescriptor>> list,
        Func<int, ProcessProbe>? probe = null, Func<int, string?>? windowTitle = null, Func<JObject?>? sandbox = null)
    {
        _selector = startup;
        _list = list;
        _probe = probe ?? ProcessProbe.Read;
        _windowTitle = windowTitle ?? ReadWindowTitle;
        _sandbox = sandbox ?? (() => null);
    }

    public long Generation { get { lock (_gate) return _generation; } }

    /// <summary>Resolves the instance for one wire command. <paramref name="pinned"/> is the generation seen when the tool call arrived.</summary>
    public TargetCall Capture(long? pinned)
    {
        lock (_gate)
        {
            // BW-TARGET-001 §6.6: a call pinned to an older generation is not sent. A call that arrived before any
            // binding (pin 0) may use only the first binding; once the binding moves past it, the call is refused too.
            if (pinned is { } pin && pin != _generation && !(pin == 0 && _generation <= FirstGeneration))
                throw Refusal("TARGET_CHANGED", "The binding changed while this call was pending (nwd_switch_target or a lost instance); the command was not sent.",
                    Live(), recommended: null);

            if (_state == "none")
            {
                var live = Live();
                var candidates = live.Where(_selector.Matches).ToList();
                if (candidates.Count == 0)
                    throw NoTarget(NoTargetMessage(), live, recommended: "nwd_list_available_targets");
                if (candidates.Count > 1)
                    throw Refusal("AMBIGUOUS_TARGET", $"{candidates.Count} Navisworks instances match selector '{_selector.Text}'. The command was not sent. Call nwd_list_available_targets and pin one with nwd_switch_target(\"pid:<n>\").",
                        candidates);
                // §6.3-6.4: not bound yet; Commit binds once the request is written, a failed connect keeps none.
                return new TargetCall(candidates[0], FirstGeneration, tentative: true);
            }

            if (_state == "bound")
            {
                var bound = _bound!;
                if (IsDead(bound))
                    _state = "lost";
                else
                {
                    var refreshed = Live().FirstOrDefault(SameInstanceAsBound);
                    if (refreshed is null)
                        throw Refusal("TARGET_UNAVAILABLE", $"The bound Navisworks ({Identity(bound)}) is running but has no readable descriptor (MCP listener stopped or restarting). The command was not sent; the binding is kept.",
                            Live());
                    _bound = refreshed;
                    return new TargetCall(refreshed, _generation);
                }
            }

            throw LostRefusal();
        }
    }

    /// <summary>Called immediately before the request bytes are written.</summary>
    public void Validate(TargetCall call)
    {
        lock (_gate)
        {
            if (call.Tentative ? !StillFirstSelection(call) : call.Generation != _generation || _state != "bound")
                throw Refusal("TARGET_CHANGED", "The binding changed while this call was connecting; the command was not sent.", Live());
        }
    }

    /// <summary>
    /// Called once the request bytes are written: a tentative first selection becomes the binding (BW-TARGET-001 §6.4,
    /// dwg/nwd/ipt: the instance a command reached is the binding, even if its response is lost).
    /// </summary>
    public void Commit(TargetCall call)
    {
        lock (_gate)
            if (call.Tentative && _state == "none")
                Bind(call.Descriptor, _selector, confirmed: false);
    }

    // A tentative call may still go out while nothing is bound, or when a concurrent first call already bound the
    // same instance and nothing changed since.
    private bool StillFirstSelection(TargetCall call)
        => _state == "none"
            || (_state == "bound" && _generation == FirstGeneration && SameInstance(_bound!, _observedStart, call.Descriptor));

    public JObject Switch(string? raw, bool rebind)
    {
        lock (_gate)
        {
            if (!TargetSelector.TryParse(raw, out var selector, out var error))
                throw Refusal("INVALID_TARGET_SELECTOR", error! + " The binding was not changed.", Live(), recommended: "nwd_list_available_targets");
            var live = Live();
            var matches = live.Where(selector.Matches).ToList();
            if (matches.Count == 0)
                throw NoTarget($"No live Navisworks Manage instance matches '{selector.Text}'. The binding was not changed.", live,
                    recommended: "nwd_list_available_targets");
            if (matches.Count > 1)
                throw Refusal("AMBIGUOUS_TARGET", $"{matches.Count} instances match '{selector.Text}'. Pin one explicitly with pid:<n> or id:<target_id>. The binding was not changed.", matches,
                    recommended: SwitchTool);

            var candidate = matches[0];
            var previous = _bound is null ? null : Brief(_bound);
            if (_bound != null && SameInstance(_bound, _observedStart, candidate))
            {
                _selector = selector;
                _confirmed = true;
                _bound = candidate;
                _state = "bound";
                return SwitchResult(previous);
            }
            if (_bound != null && !selector.IsExplicit)
                throw Refusal("TARGET_REBIND_REQUIRED", $"An automatic or year selector cannot move an existing binding ({Identity(_bound)}) to another instance ({Identity(candidate)}). Inspect that instance, then select it with {SwitchTool}(\"pid:{candidate.ProcessIdNormalized}\"). The binding was not changed.",
                    matches, recommended: SwitchTool);
            if (_state == "bound" && _confirmed && !rebind && !IsDead(_bound!))
                throw Refusal("TARGET_REBIND_REQUIRED", $"The current Navisworks ({Identity(_bound!)}) is still live and was selected explicitly. Only if you intend to leave it, call {SwitchTool}(\"{selector.Text}\", rebind=true). The binding was not changed.",
                    matches, recommended: SwitchTool);

            Bind(candidate, selector, confirmed: true);
            _lastSwitch = new JObject
            {
                ["from"] = previous?["target_id"],
                ["to"] = candidate.TargetId,
                ["at_utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            return SwitchResult(previous);
        }
    }

    /// <summary>The confirmed document of the binding <paramref name="call"/> was captured under, or null.</summary>
    public (string Id, string? Title)? PinnedDocument(TargetCall call)
    {
        lock (_gate)
            return call.Generation == _generation && _state == "bound" && _documentId is { } id ? (id, _documentTitle) : null;
    }

    /// <summary>Pins the document writes must run on; ignored when the binding moved since <paramref name="call"/>.</summary>
    public bool PinDocument(TargetCall call, string documentId, string? title)
    {
        lock (_gate)
        {
            if (call.Generation != _generation || _state != "bound") return false;
            _documentId = documentId;
            _documentTitle = title;
            return true;
        }
    }

    /// <summary>State for nwd_get_current_target (BW-TARGET-001 §6.9). Never selects or connects.</summary>
    public JObject Snapshot()
    {
        lock (_gate)
        {
            var live = Live();
            var state = _state == "bound" && IsDead(_bound!) ? "lost" : _state;
            string? next = null;
            if (state == "none")
            {
                var matches = live.Where(_selector.Matches).ToList();
                next = matches.Count == 1 ? matches[0].TargetId : null;
            }
            else if (state == "lost" && !_selector.IsExplicit)
            {
                var matches = live.Where(_selector.Matches).ToList();
                next = matches.Count == 1 ? matches[0].TargetId : null;
            }
            var bound = _bound;
            return new JObject
            {
                // Legacy fields kept for older agents.
                ["TargetId"] = bound?.TargetId,
                ["NavisworksYear"] = bound?.Year,
                ["DocumentTitle"] = bound?.DocumentTitle,
                ["selector"] = _selector.Text,
                ["binding"] = bound is null ? "none" : _selector.IsExplicit ? "explicit" : "implicit",
                ["binding_state"] = state,
                ["generation"] = _generation,
                ["selection_confirmed"] = _confirmed,
                ["bound_target_id"] = bound?.TargetId,
                ["bound_pid"] = bound?.ProcessIdNormalized,
                ["bound_host_year"] = bound?.Year,
                ["process_start_utc"] = Iso(_observedStart ?? bound?.ProcessStartUtc),
                ["identity_verified"] = bound?.IdentityVerified ?? false,
                ["window_title"] = bound is null || state == "lost" ? null : _windowTitle(bound.ProcessIdNormalized),
                ["next_target_id"] = next,
                ["candidate_count"] = live.Count(_selector.Matches),
                ["confirmation_required"] = state == "lost",
                ["document_confirmed"] = state == "bound" && _documentId != null,
                ["confirmed_document_id"] = state == "bound" ? _documentId : null,
                ["confirmed_document_title"] = state == "bound" ? _documentTitle : null,
                ["last_switch"] = _lastSwitch?.DeepClone()
            };
        }
    }

    /// <summary>Rows for nwd_list_available_targets. Never selects or connects.</summary>
    public JArray ListRows()
    {
        lock (_gate)
        {
            var rows = new JArray();
            foreach (var t in Live())
            {
                var row = Row(t);
                row["is_current"] = _bound != null && _state == "bound" && SameInstance(_bound, _observedStart, t);
                rows.Add(row);
            }
            return rows;
        }
    }

    private IReadOnlyList<TargetDescriptor> Live() => TargetRegistry.Sort(_list());

    private void Bind(TargetDescriptor target, TargetSelector selector, bool confirmed)
    {
        _bound = target;
        _observedStart = target.ObservedStartUtc ?? _probe(target.ProcessIdNormalized).StartUtc;
        _selector = selector;
        _confirmed = confirmed;
        _state = "bound";
        _generation++;
        _documentId = null;      // a new binding starts unconfirmed (FC-DOC-03)
        _documentTitle = null;
    }

    private bool IsDead(TargetDescriptor bound)
    {
        var probe = _probe(bound.ProcessIdNormalized);
        if (probe.State == ProcessState.Gone) return true;
        return _observedStart is { } expected && probe.StartUtc is { } actual
            && (actual - expected).Duration() > TimeSpan.FromSeconds(1);
    }

    private bool SameInstanceAsBound(TargetDescriptor d) => SameInstance(_bound!, _observedStart, d);

    private static bool SameInstance(TargetDescriptor bound, DateTime? boundStart, TargetDescriptor candidate)
    {
        if (bound.ProcessIdNormalized != candidate.ProcessIdNormalized) return false;
        var candidateStart = candidate.ObservedStartUtc ?? candidate.ProcessStartUtc;
        return boundStart is null || candidateStart is null || (candidateStart.Value - boundStart.Value).Duration() <= TimeSpan.FromSeconds(1);
    }

    private NwdGatewayException LostRefusal()
    {
        var live = Live();
        var bound = _bound!;
        if (_selector.IsExplicit)
            return Refusal("TARGET_LOST", $"The explicitly selected Navisworks ({Identity(bound)}) has exited. No other instance was selected and the command was not sent. Inspect the live instances, then call {SwitchTool}(\"pid:<n>\") for the intended one.",
                live, recommended: live.Count > 0 ? SwitchTool : "nwd_list_available_targets");
        var matches = live.Where(_selector.Matches).ToList();
        if (matches.Count == 0)
            return NoTarget($"The bound Navisworks ({Identity(bound)}) has exited and no live instance matches '{_selector.Text}'. The command was not sent.", live,
                recommended: "nwd_list_available_targets");
        if (matches.Count == 1)
            return Refusal("TARGET_CHANGED", $"The bound Navisworks ({Identity(bound)}) has exited. One other instance matches: {Identity(matches[0])}. It was NOT selected and the command was not sent. Check that model, call {SwitchTool}(\"pid:{matches[0].ProcessIdNormalized}\") to confirm it, then call again if still intended.",
                matches, recommended: SwitchTool, current: matches[0]);
        return Refusal("AMBIGUOUS_TARGET", $"The bound Navisworks ({Identity(bound)}) has exited and {matches.Count} instances match '{_selector.Text}'. None was selected and the command was not sent. Pin one with {SwitchTool}(\"pid:<n>\").",
            matches, recommended: SwitchTool);
    }

    /// <summary>
    /// NO_TARGET, or CLIENT_SANDBOXED (FC-TGT-07) when discovery found no live instance at all and the gateway runs
    /// in a client AppContainer, where it can neither read descriptors nor reach Navisworks.
    /// </summary>
    private NwdGatewayException NoTarget(string message, IReadOnlyList<TargetDescriptor> live, string? recommended)
    {
        if (live.Count == 0 && _sandbox() is { } sandbox)
        {
            var ex = Refusal(ClientSandbox.Code, ClientSandbox.Message, live, recommended: null);
            ex.Target!["sandbox"] = sandbox;
            return ex;
        }
        return Refusal("NO_TARGET", message, live, recommended);
    }

    private NwdGatewayException Refusal(string code, string message, IEnumerable<TargetDescriptor> candidates,
        string? recommended = "nwd_list_available_targets", TargetDescriptor? current = null)
    {
        var target = new JObject
        {
            ["code"] = code,
            ["sent"] = false,
            ["outcome"] = "not_started",
            ["selector"] = _selector.Text,
            ["binding"] = _bound is null ? "none" : _selector.IsExplicit ? "explicit" : "implicit",
            ["binding_state"] = _state,
            ["previous"] = _bound is null ? null : Brief(_bound),
            ["current"] = current is null ? null : Row(current),
            ["candidates"] = new JArray(candidates.Select(c => (JToken)Row(c))),
            ["recommended_next_tool"] = recommended,
            ["next_step"] = message
        };
        return new NwdGatewayException(code, message) { Target = target };
    }

    private JObject SwitchResult(JObject? previous) => new()
    {
        ["ok"] = true,
        ["previous"] = previous,
        ["binding"] = Snapshot()
    };

    private JObject Row(TargetDescriptor t) => new()
    {
        ["TargetId"] = t.TargetId,
        ["NavisworksYear"] = t.Year,
        ["DocumentTitle"] = t.DocumentTitle,
        ["target_id"] = t.TargetId,
        ["host_year"] = t.Year,
        ["pid"] = t.ProcessIdNormalized,
        ["process_start_utc"] = Iso(t.ObservedStartUtc ?? t.ProcessStartUtc),
        ["identity_verified"] = t.IdentityVerified,
        ["transport"] = t.Transport ?? "tcp",
        ["source"] = "per_instance",
        ["window_title"] = _windowTitle(t.ProcessIdNormalized),
        ["last_heartbeat_utc"] = t.LastHeartbeatUtc == default ? null : t.LastHeartbeatUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
    };

    private static JObject Brief(TargetDescriptor t) => new()
    {
        ["target_id"] = t.TargetId,
        ["pid"] = t.ProcessIdNormalized,
        ["host_year"] = t.Year
    };

    private string NoTargetMessage() => _selector.Kind == SelectorKind.Auto
        ? "No live Navisworks Manage target. Start Navisworks with the nwd plug-in loaded."
        : $"No live Navisworks Manage instance matches startup selector '{_selector.Text}'. Call nwd_list_available_targets.";

    private static string Identity(TargetDescriptor t)
        => $"{t.TargetId}, pid {t.ProcessIdNormalized}, document {(string.IsNullOrEmpty(t.DocumentTitle) ? "none" : "'" + t.DocumentTitle + "'")}";

    private static string? Iso(DateTime? value)
        => value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static string? ReadWindowTitle(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var title = process.MainWindowTitle;
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch { return null; }
    }
}
