using Bimwright.Nwd.Server;
using ModelContextProtocol.Protocol;

namespace Bimwright.Nwd.Tests;

public sealed class ToolErrorFlagTests
{
    private static CallToolResult Text(string text)
        => new() { Content = new List<ContentBlock> { new TextContentBlock { Text = text } } };

    [Theory]
    [InlineData("""{ "ok": false, "error": { "code": "NO_TARGET", "message": "x" } }""", true)]
    [InlineData("""{"ok":false,"error_code":"not_found"}""", true)]
    [InlineData("""{"ok":false,"stdout":"","error":"compile error"}""", true)]
    [InlineData("""{"ok":true,"result":1}""", false)]
    [InlineData("""{"item_ids":[]}""", false)]
    [InlineData("""[{"TargetId":"navis-2025-1"}]""", false)]
    [InlineData("not json", false)]
    [InlineData("", false)]
    public void MarksOkFalseAsError(string text, bool isError)
        => Assert.Equal(isError, ToolErrorFlag.Apply(Text(text)).IsError == true);

    [Fact]
    public void KeepsAnExistingErrorFlag()
    {
        var r = Text("""{"ok":true}""");
        r.IsError = true;
        Assert.True(ToolErrorFlag.Apply(r).IsError);
    }
}
