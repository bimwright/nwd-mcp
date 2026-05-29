using System;
using System.IO;

namespace Bimwright.Nwd.Server.Bake;

public static class BakePaths
{
    private static string Root(NwdMcpConfig c) => c.BakeDirectory; // %LOCALAPPDATA%\Bimwright\nwd-mcp\baked
    public static string Db(NwdMcpConfig c) => Path.Combine(Root(c), "bake.db");
    public static string AuditLog(NwdMcpConfig c) => Path.Combine(Root(c), "audit.jsonl");
    public static void EnsureDir(NwdMcpConfig c) => Directory.CreateDirectory(Root(c));
}
