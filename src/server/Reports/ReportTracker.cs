using System;
using System.Collections.Generic;
using System.Globalization;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Server.Reports;

/// <summary>
/// Family contract FC-RPT-05 (rvt-mcp AgentReportReminder, ipt-mcp ReportTracker parity): a response of a write that
/// changed the Navisworks document carries <c>_report</c> until the agent sends a report, the first in full and the rest
/// short. The writes since the last report are kept (bounded) and saved with the next report as its evidence.
/// </summary>
internal sealed class ReportTracker
{
    internal const string Text = "This job changed a Navisworks document. Before your final answer, call nwd_report_task_result once: "
        + "outcome completed, failed or cancelled, a one-line summary and an optional Markdown body. Not after each tool.";
    internal const string ShortText = "Report still due: call nwd_report_task_result once before your final answer.";
    internal const string ScriptText = "If this script or baked tool changed the document, call nwd_report_task_result once before your final answer. Not after each tool.";
    internal const int MaxCalls = 256;

    // Code whose effect the server cannot see: it may only have read.
    private static readonly HashSet<string> MayChange = new(StringComparer.Ordinal) { "send_code", "run_baked_tool" };

    private readonly object _gate = new();
    private List<JObject> _calls = new();
    private bool _truncated;
    private bool _changeReminded;
    private bool _scriptReminded;

    /// <summary>
    /// A write that changes the document: the commands that need a confirmed document (FC-DOC), except open_file,
    /// which replaces the document rather than changing it. Transient UI commands (D11) never count.
    /// </summary>
    internal static bool ChangesDocument(string command)
        => NwdCommandCatalog.TryGet(command, out var info)
            && DocumentTargetPolicy.NeedsConfirmedTarget(command, info.IsReadOnly)
            && !DocumentTargetPolicy.OpensOrReplacesDocument(command);

    /// <summary>Records a successful command and returns the reminder its response carries, or null.</summary>
    internal string? Record(string command, string requestId, string? documentTitle)
    {
        if (!ChangesDocument(command))
            return null;
        var script = MayChange.Contains(command);
        lock (_gate)
        {
            if (_calls.Count < MaxCalls)
            {
                var call = new JObject
                {
                    ["request_id"] = requestId, ["tool"] = "nwd_" + command,
                    ["utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["document_title"] = documentTitle,
                };
                if (script) call["unconfirmed_change"] = true;   // the server cannot tell whether the code changed the document
                _calls.Add(call);
            }
            else _truncated = true;
            if (script)
            {
                // One conditional reminder per report interval.
                if (_changeReminded || _scriptReminded) return null;
                _scriptReminded = true;
                return ScriptText;
            }
            var text = _changeReminded ? ShortText : Text;
            _changeReminded = true;
            return text;
        }
    }

    /// <summary>The writes since the last report; clears them and the reminder state.</summary>
    internal (JArray Calls, bool Truncated) TakeCalls()
    {
        lock (_gate)
        {
            var calls = new JArray(_calls);
            var truncated = _truncated;
            _calls = new List<JObject>();
            _truncated = false;
            _changeReminded = false;
            _scriptReminded = false;
            return (calls, truncated);
        }
    }
}
