using System;
using Bimwright.Nwd.Shared.ToolBaker;

namespace Bimwright.Nwd.Tests;

public sealed class ToolBakerRuntimeTests
{
    [Fact]
    public void DispatchAuthorizerValidatesTargetCommands()
    {
        // allowed domain tools
        Assert.True(BakedToolDispatchAuthorizer.IsAllowed("find_items"));
        Assert.True(BakedToolDispatchAuthorizer.IsAllowed("execute_search_set"));

        // disallowed target commands
        Assert.False(BakedToolDispatchAuthorizer.IsAllowed("send_code"));
        Assert.False(BakedToolDispatchAuthorizer.IsAllowed("batch_execute"));
        Assert.False(BakedToolDispatchAuthorizer.IsAllowed("run_baked_tool"));
        Assert.False(BakedToolDispatchAuthorizer.IsAllowed("unknown_command"));
        Assert.False(BakedToolDispatchAuthorizer.IsAllowed(""));
    }

    [Fact]
    public void CompilerPolicyValidatesForbiddenTokens()
    {
        Assert.True(BakeCompilerPolicy.ValidateSource("var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;").Ok);

        Assert.False(BakeCompilerPolicy.ValidateSource("System.IO.File.Delete(path);").Ok);
        Assert.False(BakeCompilerPolicy.ValidateSource("System.Diagnostics.Process.Start(info);").Ok);
        Assert.False(BakeCompilerPolicy.ValidateSource("Environment.GetEnvironmentVariable(\"key\");").Ok);
        Assert.False(BakeCompilerPolicy.ValidateSource("var client = new System.Net.Http.HttpClient();").Ok);
    }
}
