using System.Reflection;
using Bimwright.Nwd.Shared.Handlers;

namespace Bimwright.Nwd.Tests;

/// <summary>send_code error text matches rvt-mcp: first diagnostics, the cause's message, masked, no stack trace.</summary>
public sealed class SendCodeErrorsTests
{
    [Fact]
    public void Compile_lists_the_first_20_diagnostics_masked_and_counts_the_rest()
    {
        var diagnostics = Enumerable.Range(1, 25)
            .Select(i => $@"({i},1): error CS0103: The name 'x{i}' does not exist; see C:\Users\someone\notes.txt").ToArray();

        var text = SendCodeErrors.Compile(diagnostics);

        Assert.StartsWith("compile error:", text);
        Assert.Contains("x20'", text);
        Assert.DoesNotContain("x21'", text);
        Assert.Contains("… 5 more", text);
        Assert.DoesNotContain("someone", text);
        Assert.Contains("<path>", text);
    }

    [Fact]
    public void Runtime_gives_the_cause_type_and_masked_message_without_a_stack_trace()
    {
        Exception thrown;
        try { throw new InvalidOperationException(@"Cannot open C:\Users\someone\model.nwd"); }
        catch (Exception ex) { thrown = ex; }
        var wrapped = new AggregateException(new TargetInvocationException(new TypeInitializationException("Synthetic.Type", thrown)));

        var text = SendCodeErrors.Runtime(wrapped);

        Assert.Equal("runtime error: InvalidOperationException: Cannot open <path>", text);
        Assert.DoesNotContain(" at ", text);
    }

    [Fact]
    public void Long_identifiers_stay_readable_while_secrets_are_masked()
    {
        const string name = "selectedModelItemCollectionForClashReview";
        var compile = SendCodeErrors.Compile(new[] { $"(3,5): error CS0103: The name '{name}' does not exist in the current context" });
        var runtime = SendCodeErrors.Runtime(new KeyNotFoundException($"{name} not found; Bearer abcdefghijklmnopqrstuvwxyz0123456789"));

        Assert.Contains(name, compile);
        Assert.Contains(name, runtime);
        Assert.StartsWith("runtime error: KeyNotFoundException:", runtime);
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz0123456789", runtime);
    }

    [Fact]
    public void Runtime_keeps_an_aggregate_with_several_causes_as_is()
    {
        var several = new AggregateException(new Exception("a"), new Exception("b"));
        Assert.StartsWith("runtime error: AggregateException:", SendCodeErrors.Runtime(several));
    }
}
