using System;
using System.IO;
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json;

namespace Bimwright.Nwd.Shared.Plugin;

public static class TargetDescriptorWriter
{
    public static string GetPath(string dir, int year, int pid)
        => Path.Combine(dir, $"navis-{year}-{pid}.json");

    public static void Write(string dir, TargetDescriptor d)
    {
        Directory.CreateDirectory(dir);
        var path = GetPath(dir, d.NavisworksYear, d.ProcessId);
        File.WriteAllText(path, JsonConvert.SerializeObject(d, Formatting.Indented));
    }

    public static void Delete(string dir, int year, int pid)
    {
        try
        {
            var path = GetPath(dir, year, pid);
            if (File.Exists(path)) File.Delete(path);
        }
        catch {}
    }
}
