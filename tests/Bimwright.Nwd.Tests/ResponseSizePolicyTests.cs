using Bimwright.Nwd.Server;

namespace Bimwright.Nwd.Tests;

public sealed class ResponseSizePolicyTests
{
    private static string TextOf(int bytes) => new('x', bytes);

    [Fact]
    public void Under64KiBIsNone()
    {
        var d = ResponseSizePolicy.Evaluate("nwd_health_check", TextOf(ResponseSizePolicy.WarnBytes - 1));
        Assert.Equal("none", d.Level);
        Assert.False(d.Reject);
        Assert.Null(d.AgentWarning);
    }

    [Fact]
    public void At64KiBWarns()
    {
        var d = ResponseSizePolicy.Evaluate("nwd_health_check", TextOf(ResponseSizePolicy.WarnBytes));
        Assert.Equal("warning", d.Level);
        Assert.False(d.Reject);
        Assert.Contains("warning", d.AgentWarning);
    }

    [Fact]
    public void Above256KiBIsStrongWarning()
    {
        var d = ResponseSizePolicy.Evaluate("nwd_health_check", TextOf(ResponseSizePolicy.StrongWarnBytes + 1));
        Assert.Equal("strong_warning", d.Level);
        Assert.False(d.Reject);
        Assert.Contains("strong warning", d.AgentWarning);
    }

    [Fact]
    public void Over1MiBRejectsWithToolSpecificHint()
    {
        var d = ResponseSizePolicy.Evaluate("nwd_get_model_tree", TextOf(ResponseSizePolicy.BudgetBytes + 1));
        Assert.True(d.Reject);
        Assert.Contains("max_depth", d.RejectMessage);
        Assert.Contains("output=file", d.RejectMessage);
        Assert.Contains("Do not retry the same unscoped request.", d.RejectMessage);
    }

    [Fact]
    public void UnknownToolFallsBackToGenericHint()
    {
        var d = ResponseSizePolicy.Evaluate("nwd_nope", TextOf(ResponseSizePolicy.BudgetBytes + 1));
        Assert.True(d.Reject);
        Assert.Contains("more selective filter", d.RejectMessage);
    }
}
