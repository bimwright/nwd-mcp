using Bimwright.Nwd.Shared.Infrastructure;

namespace Bimwright.Nwd.Tests;

public sealed class CommandDeadlineTests
{
    [Fact]
    public void DeadlineLeavesAReplyMarginAndAFloor()
    {
        var t0 = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(t0.AddMilliseconds(28000), NwdCommandContext.DeadlineFor(t0, 30000));
        Assert.Equal(t0.AddMilliseconds(298000), NwdCommandContext.DeadlineFor(t0, 300000));
        Assert.Equal(t0.AddMilliseconds(1000), NwdCommandContext.DeadlineFor(t0, 500));
    }

    [Fact]
    public void DefaultContextHasNoDeadline()
    {
        Assert.False(new NwdCommandContext().PastDeadline);
        Assert.True(new NwdCommandContext { DeadlineUtc = DateTime.UtcNow.AddSeconds(-1) }.PastDeadline);
    }
}
