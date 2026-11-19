using System.Threading;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The text of a value in CEL syntax: for logs and messages, not a contract. The values come from expressions, so each row
// shows the text of what an expression gives.
[Trait("Requirement", "F-13")]
public sealed class ValueTextTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .AddVariable<Order>("order")
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("'abc'", "\"abc\"")]
    [InlineData("'say \"hi\"\\n'", "\"say \\\"hi\\\"\\n\"")]
    [InlineData("1", "1")]
    [InlineData("-7", "-7")]
    [InlineData("1u", "1u")]
    [InlineData("2.5", "2.5")]
    [InlineData("2.0", "2.0")]
    [InlineData("true", "true")]
    [InlineData("null", "null")]
    [InlineData("b'\\x00a'", "b\"\\x00a\"")]
    [InlineData("[1, 'a', [2]]", "[1, \"a\", [2]]")]
    [InlineData("{'a': 1}", "{\"a\": 1}")]
    [InlineData("timestamp('2026-01-01T00:00:00Z')", "timestamp(\"2026-01-01T00:00:00Z\")")]
    [InlineData("duration('1.5s')", "duration(\"1.5s\")")]
    [InlineData("int", "int")]
    public static void Value_IsWrittenInCelSyntax(string expression, string text)
    {
        Assert.Equal(text, Evaluate(expression, new CelBindings()).ToString());
    }

    [Fact]
    public void Object_IsTheTypeNameAndThePresentFields()
    {
        var order = new Order { Id = "ORD-1", Status = "new", Total = 2.5 };

        string text = Evaluate("order", new CelBindings().Add("order", order)).ToString();

        Assert.StartsWith("shop.Order{", text, System.StringComparison.Ordinal);
        Assert.Contains("id: \"ORD-1\"", text, System.StringComparison.Ordinal);
        Assert.Contains("total: 2.5", text, System.StringComparison.Ordinal);
        Assert.EndsWith("}", text, System.StringComparison.Ordinal);
    }

    // Deeper than 32 levels, a nested value is written as "…"; the text has no length limit.
    [Fact]
    public void ValueDeeperThan32Levels_IsCut()
    {
        string expression = new string('[', 33) + "1" + new string(']', 33);

        string text = Evaluate(expression, new CelBindings()).ToString();

        Assert.Equal(new string('[', 32) + "…" + new string(']', 32), text);
    }

    private static CelValue Evaluate(string text, CelBindings bindings) =>
        Environment.Compile<CelValue>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
