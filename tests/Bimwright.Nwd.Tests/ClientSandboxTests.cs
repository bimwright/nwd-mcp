using Bimwright.Nwd.Server;

namespace Bimwright.Nwd.Tests;

/// <summary>FC-TGT-07: host-free classifier for a gateway launched inside a client AppContainer.</summary>
public sealed class ClientSandboxTests
{
    private const string Normal = @"C:\Users\x\AppData\Local";
    private const string Container = @"C:\Users\x\AppData\Local\Packages\OpenAI.Codex_2p2nqsd0c76g0\AC";

    [Fact]
    public void FC_TGT_07_AppContainerTokenAloneIsDetected()
    {
        var r = ClientSandbox.Classify(true, Normal);
        Assert.True(r.Detected);
        Assert.Equal("appcontainer", r.Kind);
        Assert.Equal(new[] { "token_is_appcontainer" }, r.Evidence);
    }

    [Fact]
    public void FC_TGT_07_PackagesAcPathAloneIsDetected()
    {
        var r = ClientSandbox.Classify(false, Container + @"\#!sandbox.{0F1E2D3C-0000-0000-0000-000000000000}");
        Assert.True(r.Detected);
        Assert.Equal("appcontainer", r.Kind);
        Assert.Equal(new[] { "local_app_data_under_packages_ac" }, r.Evidence);
    }

    [Fact]
    public void FC_TGT_07_TokenAndPathReportBothEvidence()
    {
        var r = ClientSandbox.Classify(true, Container);
        Assert.Equal(new[] { "token_is_appcontainer", "local_app_data_under_packages_ac" }, r.Evidence);
    }

    [Fact]
    public void FC_TGT_07_NeitherSignalIsNotDetected()
    {
        var r = ClientSandbox.Classify(false, Normal, Normal + @"\Bimwright\nwd-mcp");
        Assert.False(r.Detected);
        Assert.Null(r.Kind);
        Assert.Empty(r.Evidence);
    }

    [Fact]
    public void FC_TGT_07_UnknownTokenFallsBackToThePath()
    {
        Assert.False(ClientSandbox.Classify(null, Normal).Detected);
        Assert.True(ClientSandbox.Classify(null, Container).Detected);
    }

    [Fact]
    public void FC_TGT_07_EitherPathMayMatch()
    {
        var r = ClientSandbox.Classify(false, Normal, Container + @"\Bimwright\nwd-mcp");
        Assert.True(r.Detected);
        Assert.Equal(new[] { "local_app_data_under_packages_ac" }, r.Evidence);
    }

    [Theory]
    [InlineData(Container + @"\")]
    [InlineData(@"C:/Users/x/AppData/Local/Packages/OpenAI.Codex_2p2nqsd0c76g0/AC")]
    [InlineData(@"C:/Users/x/AppData/Local/Packages/OpenAI.Codex_2p2nqsd0c76g0/AC/")]
    [InlineData(@"c:\users\x\appdata\local\packages\openai.codex_2p2nqsd0c76g0\ac")]
    public void FC_TGT_07_TrailingSeparatorsForwardSlashesAndCaseAreTolerated(string path)
        => Assert.True(ClientSandbox.IsUnderPackagesAc(path));

    [Theory]
    [InlineData(Normal)]
    [InlineData(@"C:\Users\x\AppData\Local\Packages\Foo\ACX")]
    [InlineData(@"C:\Users\x\AppData\Local\PackagesX\Foo\AC")]
    [InlineData(@"C:\Users\x\AppData\Local\Packages\AC")]
    [InlineData(@"C:\Users\x\AppData\Local\Packages\Foo\Bar\AC")]
    [InlineData("")]
    [InlineData(null)]
    public void FC_TGT_07_LookalikePathsAreNotDetected(string? path)
        => Assert.False(ClientSandbox.IsUnderPackagesAc(path));

    [Fact]
    public void FC_TGT_07_TokenProbeNeverThrowsAndIsCached()
    {
        var first = ClientSandbox.ProcessTokenIsAppContainer();
        Assert.Equal(first, ClientSandbox.ProcessTokenIsAppContainer());
        if (OperatingSystem.IsWindows()) Assert.False(first);   // the test runner is not in an AppContainer
    }
}
