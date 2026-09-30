using System.IO;
using Bimwright.Nwd.Shared.Files;

namespace Bimwright.Nwd.Tests;

public sealed class RecentFilesTests
{
    [Fact]
    public void Series_matches_the_manage_registry_year()
    {
        Assert.Equal(19, NavisworksRecentFiles.Series(2022));
        Assert.Equal(22, NavisworksRecentFiles.Series(2025));
        Assert.Equal(24, NavisworksRecentFiles.Series(2027));
        Assert.Contains("22.0\\Recent File List", NavisworksRecentFiles.RegistrySubKey(2025));
    }

    [Fact]
    public void Read_keeps_menu_order_skips_blanks_and_honors_limit()
    {
        var opened = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            new RecentFileRaw { Index = 4, Path = "D:\\d.nwd", DisplayName = "D" },
            new RecentFileRaw { Index = 2, Path = "  D:\\b.nwd  ", DisplayName = "B", Pinned = true, LastOpenedFileTime = opened.ToFileTimeUtc() },
            new RecentFileRaw { Index = 1, Path = "   ", DisplayName = "blank" },
            new RecentFileRaw { Index = 3, Path = "D:\\c.nwc", DisplayName = "  ", LastOpenedFileTime = 0 }
        };

        var files = NavisworksRecentFiles.Read(2025, 1, _ => rows, path => path.EndsWith("b.nwd", StringComparison.Ordinal));

        var only = Assert.Single(files);
        Assert.Equal(2, only.Index);
        Assert.Equal("D:\\b.nwd", only.Path);
        Assert.Equal("B", only.DisplayName);
        Assert.True(only.Pinned);
        Assert.True(only.Exists);
        Assert.Equal(opened, only.LastOpenedUtc);
    }

    [Fact]
    public void Unsupported_year_returns_nothing()
    {
        var called = false;
        var files = NavisworksRecentFiles.Read(2019, 10, _ => { called = true; return Array.Empty<RecentFileRaw>(); }, _ => true);
        Assert.Empty(files);
        Assert.False(called);
    }

    [Fact]
    public void Limit_clamps_to_one_through_fifty()
    {
        Assert.Equal(1, NavisworksRecentFiles.ClampLimit(0));
        Assert.Equal(50, NavisworksRecentFiles.ClampLimit(99));
    }
}

public sealed class LocalModelFileTests : IDisposable
{
    private readonly string _file;

    public LocalModelFileTests()
    {
        _file = Path.Combine(Path.GetTempPath(), "nwd-mcp-open-" + Guid.NewGuid().ToString("n") + ".nwd");
        File.WriteAllText(_file, "x");
    }

    [Fact]
    public void Existing_file_returns_the_full_path()
    {
        var check = LocalModelFile.Check("  " + _file + "  ");
        Assert.True(check.Ok);
        Assert.Equal(Path.GetFullPath(_file), check.FullPath);
    }

    [Fact]
    public void Missing_path_is_invalid()
    {
        var check = LocalModelFile.Check("  ");
        Assert.False(check.Ok);
        Assert.Equal("INVALID_ARGUMENT", check.Code);
    }

    [Fact]
    public void Absent_file_is_not_found()
    {
        var check = LocalModelFile.Check(Path.Combine(Path.GetTempPath(), "nwd-mcp-missing-" + Guid.NewGuid().ToString("n") + ".nwd"));
        Assert.False(check.Ok);
        Assert.Equal("FILE_NOT_FOUND", check.Code);
    }

    public void Dispose()
    {
        try { File.Delete(_file); } catch { }
    }
}
