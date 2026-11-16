#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

/// <summary>
/// Compiles on the calling (TCP) thread, because Roslyn is slow and never touches the Navisworks API,
/// then runs the script on the Navisworks UI thread, where API objects are valid.
/// The script sees <c>doc</c> (active document); its last expression comes back as <c>result</c>,
/// Console output as <c>stdout</c>.
/// </summary>
public sealed class SendCodeHandler : INwdCommand
{
    private const int MaxErrorLength = 4000;

    public string Name => "send_code";
    public bool IsReadOnly => false;

    public class Globals
    {
        public NW.Document doc = null!;
        /// <summary>
        /// Caller-supplied <c>data</c> as JSON text. A string, not a JToken: the host may load its own Newtonsoft.Json,
        /// and a JToken field binds to whichever copy the script resolves (ipt-mcp hit MissingFieldException).
        /// <see cref="SendCodeTask.DataPrelude"/> parses it with the script's own copy.
        /// </summary>
        public string? __data_json;
    }

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };

        var code = (string?)p["code"];
        if (string.IsNullOrWhiteSpace(code))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "code parameter is required", meta);
        if (!SendCodeTask.TryReadContract(p["result_contract"], out var contract, out var contractError))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", contractError!, meta);
        var dataJson = p["data_json"]?.Type == JTokenType.String ? (string?)p["data_json"] : null;
        var source = SendCodeTask.BuildSource(code!, dataJson);

        ScriptRunner<object> runner;
        var skipped = Array.Empty<SkippedScriptReference>();
        try
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            // rvt-mcp #38: a loaded DLL whose metadata Roslyn cannot read is left out instead of failing every compile with CS0009.
            var refs = ScriptReferences.FromPaths(loaded, out skipped);
            var imports = new List<string> { "System", "System.Collections.Generic", "System.Linq", "Autodesk.Navisworks.Api" };
            if (contract == SendCodeTask.TaskV1) imports.Add(SendCodeTask.ScriptNamespace);
            var options = ScriptOptions.Default.WithReferences(refs).WithImports(imports);

            var script = CSharpScript.Create<object>(source, options, typeof(Globals));
            var errors = script.Compile(ctx.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0)
                return ScriptResult(meta, false, "", null, "compile error: " + string.Join("\n", errors.Select(d => d.ToString())) + SkippedNote(skipped),
                    contract, completed: false, returned: null, skipped);
            runner = script.CreateDelegate();
        }
        catch (OperationCanceledException) when (ctx.CancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            return ScriptResult(meta, false, "", null, Describe(ex) + SkippedNote(skipped), contract, completed: false, returned: null, skipped);
        }

        object? value = null;
        Exception? error = null;
        var noDocument = false;
        (string Code, string Message)? refused = null;
        var captured = new StringWriter();
        // FC-BUSY-01: a modal dialog waits for the user; refuse before queueing the script.
        if (ModalDialogProbe.IsOpen(out var dialog))
            return NwdCommandResult.Fail(System.Guid.Empty, HostBusyPolicy.Code, HostBusyPolicy.ModalMessage(Name, dialog), meta);
        var started = NavisworksUiThreadInvoker.TryInvoke(() =>
        {
            // A dialog's message loop runs queued work: never start the script inside it.
            if (ModalDialogProbe.IsOpen(out var uiDialog))
            {
                refused = (HostBusyPolicy.Code, HostBusyPolicy.ModalMessage(Name, uiDialog));
                return;
            }
            // FC-DOC-04 on the UI thread, right before the script runs (it compiled off this thread).
            if (ctx.ReadDocument != null)
            {
                var open = ctx.ReadDocument();
                meta.DocumentId = open.Id;
                meta.DocumentTitle = open.Title;
                refused = DocumentTargetPolicy.Check(Name, IsReadOnly, ctx.DocumentTarget, open);
                if (refused != null) return;
            }
            var doc = NW.Application.ActiveDocument;
            if (doc is null)
            {
                noDocument = true;
                return;
            }
            var originalOut = Console.Out;
            var previous = SynchronizationContext.Current;
            Console.SetOut(captured);
            try
            {
                // Without the UI context an awaited continuation cannot deadlock the UI thread.
                SynchronizationContext.SetSynchronizationContext(null);
                value = runner(new Globals { doc = doc, __data_json = dataJson }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
                Console.SetOut(originalOut);
            }
        }, ctx.DeadlineUtc, ctx.CancellationToken);

        if (!started)
            return NwdCommandResult.Fail(System.Guid.Empty, HostBusyPolicy.Code, HostBusyPolicy.ExpiredMessage(Name, ctx.TimeoutMs), meta);
        if (refused is { } r)
            return NwdCommandResult.Fail(System.Guid.Empty, r.Code, r.Message, meta);
        if (noDocument)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);
        if (error != null)
            return ScriptResult(meta, false, captured.ToString(), null, Describe(error), contract, completed: false, returned: null, skipped);
        // Objects come back as JSON (ipt-mcp parity); a Navisworks API object fails the call with a DTO hint.
        var result = ScriptResultToken.ToResultToken(SendCodeTask.ResultValue(contract, value), out var resultError);
        return ScriptResult(meta, resultError == null, captured.ToString(), result, resultError,
            contract, completed: true, returned: value, skipped);
    }

    private static NwdCommandResult ScriptResult(NwdResponseMeta meta, bool ok, string stdout, JToken? result, string? error,
        string contract, bool completed, object? returned, SkippedScriptReference[] skipped)
    {
        var data = new JObject
        {
            ["ok"] = ok,
            ["result"] = result ?? JValue.CreateNull(),
            ["stdout"] = stdout,
            ["error"] = error
        };
        // task_v1: _task{contract, status, error}; a failed or missing McpTaskResult sets ok=false (rvt/ipt parity).
        SendCodeTask.Apply(data, contract, completed, returned);
        if (skipped.Length > 0)
            data["skipped_references"] = new JArray(skipped.Select(s =>
                new JObject { ["name"] = s.Name, ["reason"] = ErrorSanitizer.Sanitize(s.Reason) }));
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    private static string SkippedNote(SkippedScriptReference[] skipped) =>
        skipped.Length == 0 ? string.Empty
            : "\nSkipped references (unreadable metadata): " + string.Join(", ", skipped.Select(s => s.Name));

    /// <summary>Full chain, so a load or type-initializer failure shows its real cause.</summary>
    private static string Describe(Exception ex)
    {
        if (ex is AggregateException agg && agg.InnerExceptions.Count == 1)
            ex = agg.InnerException!;
        var text = ex.ToString();
        return text.Length <= MaxErrorLength ? text : text.Substring(0, MaxErrorLength) + "…";
    }
}
#endif
