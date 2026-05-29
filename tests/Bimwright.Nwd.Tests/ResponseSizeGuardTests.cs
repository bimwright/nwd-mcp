using Bimwright.Nwd.Shared.Infrastructure;

namespace Bimwright.Nwd.Tests;

public sealed class ResponseSizeGuardTests
{
    [Fact]
    public void UnderLimitPassesThrough()
        => Assert.True(ResponseSizeGuard.Check("small", 1024, out _));

    [Fact]
    public void OverLimitReportsCodeAndByteLimit()
    {
        var ok = ResponseSizeGuard.Check(new string('x', 5000), 2048, out var error);
        Assert.False(ok);
        Assert.Equal("RESPONSE_TOO_LARGE", error!.Code);
        Assert.Contains("2048", error.Message);
    }
}
