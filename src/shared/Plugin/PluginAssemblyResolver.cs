using System;
using System.IO;
using System.Reflection;

namespace Bimwright.Nwd.Shared.Plugin;

/// <summary>
/// Navisworks has no binding redirects for our dependencies. Roslyn and System.Memory ask for
/// older versions of System.Runtime.CompilerServices.Unsafe, System.Memory, etc. than the ones we ship,
/// the bind fails, and the first span-based code throws TypeInitializationException (PerTypeValues`1).
/// When a bind fails for an assembly that sits next to the plug-in, load that copy instead.
/// </summary>
internal static class PluginAssemblyResolver
{
    private static string? _dir;

    public static void Register()
    {
        if (_dir != null)
            return;
        _dir = Path.GetDirectoryName(typeof(PluginAssemblyResolver).Assembly.Location);
        AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
    }

    private static Assembly? OnResolve(object? sender, ResolveEventArgs e)
    {
        var dir = _dir;
        if (string.IsNullOrEmpty(dir))
            return null;
        var name = new AssemblyName(e.Name).Name;
        if (string.IsNullOrEmpty(name))
            return null;

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!loaded.IsDynamic && string.Equals(loaded.GetName().Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetDirectoryName(SafeLocation(loaded)), dir, StringComparison.OrdinalIgnoreCase))
                return loaded;
        }

        var path = Path.Combine(dir, name + ".dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }

    private static string SafeLocation(Assembly a)
    {
        try { return a.Location; }
        catch { return string.Empty; }
    }
}
