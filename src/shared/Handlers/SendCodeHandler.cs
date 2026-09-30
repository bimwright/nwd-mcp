#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Plugin;
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
    }

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };

        var code = (string?)p["code"];
        if (string.IsNullOrWhiteSpace(code))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "code parameter is required", meta);

        ScriptRunner<object> runner;
        try
        {
            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .ToArray();
            var options = ScriptOptions.Default
                .WithReferences(refs)
                .WithImports(
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "Autodesk.Navisworks.Api");

            var script = CSharpScript.Create<object>(code, options, typeof(Globals));
            var errors = script.Compile().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0)
                return ScriptResult(meta, false, "", null, "compile error: " + string.Join("\n", errors.Select(d => d.ToString())));
            runner = script.CreateDelegate();
        }
        catch (Exception ex)
        {
            return ScriptResult(meta, false, "", null, Describe(ex));
        }

        object? value = null;
        Exception? error = null;
        var noDocument = false;
        var captured = new StringWriter();
        NavisworksUiThreadInvoker.Invoke(() =>
        {
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
                value = runner(new Globals { doc = doc }).GetAwaiter().GetResult();
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
        });

        if (noDocument)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);
        if (error != null)
            return ScriptResult(meta, false, captured.ToString(), null, Describe(error));
        return ScriptResult(meta, true, captured.ToString(), ToToken(value), null);
    }

    private static NwdCommandResult ScriptResult(NwdResponseMeta meta, bool ok, string stdout, JToken? result, string? error)
    {
        var data = new JObject
        {
            ["ok"] = ok,
            ["result"] = result ?? JValue.CreateNull(),
            ["stdout"] = stdout,
            ["error"] = error
        };
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }

    /// <summary>Primitive results come back as JSON values; anything else as its ToString().</summary>
    private static JToken? ToToken(object? value)
    {
        if (value == null)
            return null;
        if (value is JToken token)
            return token;
        if (value is string || value is decimal || value is DateTime || value.GetType().IsPrimitive || value.GetType().IsEnum)
            return JToken.FromObject(value is Enum ? value.ToString()! : value);
        return value.ToString();
    }

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
