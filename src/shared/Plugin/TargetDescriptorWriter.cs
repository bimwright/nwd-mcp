using System;
using System.IO;
using Bimwright.Nwd.Shared.Infrastructure;
using Bimwright.Nwd.Shared.Logging;
using Newtonsoft.Json;

namespace Bimwright.Nwd.Shared.Plugin;

public static class TargetDescriptorWriter
{
    public static string GetPath(string dir, int year, int pid)
        => Path.Combine(dir, $"navis-{year}-{pid}.json");

    public static bool Write(string dir, TargetDescriptor d, bool requireOwnership = false)
    {
        var path = GetPath(dir, d.NavisworksYear, d.ProcessId);
        return BoundedLogFile.WithLock(TargetRegistry.LockPath(dir), () =>
        {
            BoundedLogFile.RefuseLink(path);
            Directory.CreateDirectory(dir);
            if (requireOwnership && !Owns(path, d.AuthToken)) return false;
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(d, Formatting.Indented));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }, 250);
    }

    public static void Delete(string dir, int year, int pid, string authToken)
    {
        try
        {
            var path = GetPath(dir, year, pid);
            BoundedLogFile.WithLock(TargetRegistry.LockPath(dir), () =>
            {
                BoundedLogFile.RefuseLink(path);
                if (Owns(path, authToken)) File.Delete(path);
                return true;
            }, 250);
        }
        catch {}
    }

    private static bool Owns(string path, string token)
    {
        if (!File.Exists(path) || string.IsNullOrEmpty(token)) return false;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 64 * 1024) return false;
            using var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<TargetDescriptor>(reader.ReadToEnd())?.AuthToken == token;
        }
        catch { return false; }
    }
}
