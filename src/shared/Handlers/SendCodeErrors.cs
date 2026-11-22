using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bimwright.Nwd.Shared.Security;

namespace Bimwright.Nwd.Shared.Handlers;

/// <summary>
/// send_code error text, rvt-mcp parity: compile errors list the first diagnostics, runtime errors give the cause's
/// type and message, never a stack trace. Every line goes through <see cref="ErrorSanitizer"/> (paths, secrets).
/// Host-free, so it is unit tested without Navisworks.
/// </summary>
public static class SendCodeErrors
{
    public const int MaxDiagnostics = 20;
    private const int MaxLength = 4000;

    /// <summary><c>compile error:</c> and one masked line per diagnostic, at most <see cref="MaxDiagnostics"/>.</summary>
    public static string Compile(IReadOnlyCollection<string> diagnostics)
    {
        var shown = diagnostics.Take(MaxDiagnostics).Select(ErrorSanitizer.Sanitize).ToList();
        if (diagnostics.Count > MaxDiagnostics) shown.Add($"… {diagnostics.Count - MaxDiagnostics} more");
        return Cap("compile error:\n" + string.Join("\n", shown));
    }

    /// <summary>The cause behind wrappers (aggregate, invocation, type initializer), as <c>Type: message</c>.</summary>
    public static string Runtime(Exception error)
    {
        var cause = Cause(error);
        return Cap("runtime error: " + cause.GetType().Name + ": " + ErrorSanitizer.Sanitize(cause));
    }

    internal static Exception Cause(Exception error)
    {
        while (true)
        {
            if (error is AggregateException aggregate && aggregate.InnerExceptions.Count == 1) error = aggregate.InnerException!;
            else if ((error is TargetInvocationException || error is TypeInitializationException) && error.InnerException != null) error = error.InnerException;
            else return error;
        }
    }

    private static string Cap(string text) => text.Length <= MaxLength ? text : text.Substring(0, MaxLength) + "…";
}
