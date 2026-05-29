#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Bimwright.Nwd.Shared.Infrastructure;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class SendCodeHandler : INwdCommand
{
    private const int ExecutionTimeoutMilliseconds = 30000;
    private const int AbortGraceMilliseconds = 5000;

    public string Name => "send_code";
    public bool IsReadOnly => false;

    public class Globals
    {
        public NW.Document doc = null!;
    }

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        if (doc is null)
            return NwdCommandResult.Fail(System.Guid.Empty, "NO_DOCUMENT", "no active Navisworks document", meta);

        var code = (string?)p["code"];
        if (string.IsNullOrWhiteSpace(code))
            return NwdCommandResult.Fail(System.Guid.Empty, "INVALID_ARGUMENT", "code parameter is required", meta);

        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);

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

            var globals = new Globals { doc = doc };

            Exception? executionError = null;
            using (var cts = new CancellationTokenSource())
            using (var completed = new ManualResetEventSlim(false))
            {
                var worker = new Thread(() =>
                {
                    try
                    {
                        CSharpScript.EvaluateAsync(code, options, globals, cancellationToken: cts.Token)
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (Exception ex)
                    {
                        executionError = ex;
                    }
                    finally
                    {
                        completed.Set();
                    }
                })
                {
                    IsBackground = true,
                    Name = "Bimwright.Nwd.SendCode"
                };

                worker.Start();

                if (!completed.Wait(ExecutionTimeoutMilliseconds))
                {
                    cts.Cancel();
                    try
                    {
                        worker.Abort();
                    }
                    catch (ThreadStateException) { }
                    catch (PlatformNotSupportedException) { }

                    if (!completed.Wait(AbortGraceMilliseconds))
                        return NwdCommandResult.Fail(System.Guid.Empty, "TIMEOUT", "execution timeout after 30s; script did not stop", meta);

                    return NwdCommandResult.Fail(System.Guid.Empty, "TIMEOUT", "execution cancelled after 30s", meta);
                }

                if (executionError != null)
                    throw executionError;
            }

            var data = new JObject
            {
                ["ok"] = true,
                ["stdout"] = captured.ToString(),
                ["error"] = null
            };
            return NwdCommandResult.Success(System.Guid.Empty, data, meta);
        }
        catch (CompilationErrorException ex)
        {
            var data = new JObject
            {
                ["ok"] = false,
                ["stdout"] = captured.ToString(),
                ["error"] = "compile error: " + string.Join("\n", ex.Diagnostics)
            };
            return NwdCommandResult.Success(System.Guid.Empty, data, meta);
        }
        catch (OperationCanceledException)
        {
            return NwdCommandResult.Fail(System.Guid.Empty, "TIMEOUT", "execution cancelled after 30s", meta);
        }
        catch (AggregateException ex) when (ex.InnerException != null)
        {
            var data = new JObject
            {
                ["ok"] = false,
                ["stdout"] = captured.ToString(),
                ["error"] = $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}"
            };
            return NwdCommandResult.Success(System.Guid.Empty, data, meta);
        }
        catch (Exception ex)
        {
            var data = new JObject
            {
                ["ok"] = false,
                ["stdout"] = captured.ToString(),
                ["error"] = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
            };
            return NwdCommandResult.Success(System.Guid.Empty, data, meta);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
#endif
