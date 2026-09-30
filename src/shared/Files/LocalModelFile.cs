using System;
using System.IO;

namespace Bimwright.Nwd.Shared.Files;

public readonly struct LocalModelFileCheck
{
    public LocalModelFileCheck(bool ok, string fullPath, string code, string message)
    {
        Ok = ok;
        FullPath = fullPath ?? "";
        Code = code ?? "";
        Message = message ?? "";
    }

    public bool Ok { get; }
    public string FullPath { get; }
    public string Code { get; }
    public string Message { get; }
}

public static class LocalModelFile
{
    public static LocalModelFileCheck Check(string? path)
    {
        if (path == null || path.Trim().Length == 0)
            return new LocalModelFileCheck(false, "", "INVALID_ARGUMENT", "path is required");

        string full;
        try
        {
            full = Path.GetFullPath(path.Trim());
        }
        catch (Exception)
        {
            return new LocalModelFileCheck(false, "", "INVALID_ARGUMENT", "path is not a valid file path");
        }

        if (!File.Exists(full))
            return new LocalModelFileCheck(false, full, "FILE_NOT_FOUND", "file not found");

        return new LocalModelFileCheck(true, full, "", "");
    }
}
