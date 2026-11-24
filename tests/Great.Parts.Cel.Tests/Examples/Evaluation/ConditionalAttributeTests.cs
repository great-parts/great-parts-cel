using System.Threading;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Evaluation.PartialTestSupport;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// A chain of attributes that starts at a conditional expression, (c ? a : b).f: cel-go appends the qualifiers of the chain to both
// branches, so a pattern is matched against the chosen branch with the qualifiers after it. b = true or false, s = "time", x = 1,
// d = {"f": 1} of the type dyn, request = {"auth": {"user": "alice"}, "time": 7} and resource = {"auth": {"user": "bob"}, "time": 9}.
public sealed class ConditionalAttributeTests
{
    private static CelBindings Data(bool condition) => new CelBindings()
        .Add("b", condition)
        .Add("s", "time")
        .Add("x", 1L)
        .Add("d", Map(("f", Int(1))))
        .Add("request", Map(("auth", Map(("user", Str("alice")))), ("time", Int(7))))
        .Add("resource", Map(("auth", Map(("user", Str("bob")))), ("time", Int(9))));

    // The run of difftest 633c7ad, case 48040: has((false ? mx[0x64] : mx).f) under the pattern mx[100]. The access mx.f differs
    // from the pattern in its first qualifier; the branch mx alone would be shorter than the pattern and unknown.
    [Fact]
    [Trait("Requirement", "F-22")]
    public void QualifierAfterTheBranch_DecidesWhetherThePatternMatches()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("has((false ? d[100] : d).f)", [P("d", 100L)], Data(true));

        Assert.True(result.IsSuccess);
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool present));
        Assert.True(present);
    }

    // The run of difftest 633c7ad, case 48361: has((false ? lx[0] : x2).b) under the patterns lx[0] and x2[*]. The trail has the
    // qualifier after the branch.
    [Fact]
    [Trait("Requirement", "F-22")]
    public void QualifierAfterTheBranch_IsInTheTrail()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("has((false ? d[0] : d).b)", [P("d", 0L), P("d", Any)], Data(true));

        Assert.True(result.IsUnknown);
        Assert.Equal("d.b", Trails(result));
    }

    // The pattern is a variable and string qualifiers separated by dots. Only the branch that the condition chooses is matched, with
    // the keys that are expressions, the nested conditional expressions and the qualifiers of the branches in the access.
    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("(b ? request : resource).auth.user", "resource.auth.user", true, "alice")]
    [InlineData("(b ? request : resource).auth.user", "resource.auth.user", false, "unknown resource.auth.user")]
    [InlineData("(b ? request : resource)[s]", "resource.time", true, "7")]
    [InlineData("(b ? request : resource)[s]", "resource.time", false, "unknown resource.time")]
    [InlineData("(b ? (false ? resource : request) : resource).time", "request.time", true, "unknown request.time")]
    [InlineData("(b ? (false ? resource : request) : resource).time", "request.time", false, "9")]
    [InlineData("(b ? (x < 0 ? resource : request).auth : resource.auth).user", "request.auth.user", true, "unknown request.auth.user")]
    [InlineData("(b ? (x < 0 ? resource : request).auth : resource.auth).user", "request.auth.user", false, "bob")]
    [InlineData("(b ? request : resource).time", "b", true, "unknown b")]
    public static void PatternIsMatchedAgainstTheChosenBranchAndTheQualifiersAfterIt(
        string expression,
        string pattern,
        bool condition,
        string expected)
    {
        string[] parts = pattern.Split('.');

        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P(parts[0], parts[1..])], Data(condition));

        Assert.Equal(expected, Show(result));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void UnknownAttribute_HasTheIdOfTheNodeWhereTheAccessReachedTheLengthOfThePattern()
    {
        const string Text = "(false ? resource : request).time";
        var selection = Assert.IsType<CelSelectNode>(Env.Parse(Text).GetParsedExprOrThrow().Root);
        var conditional = Assert.IsType<CelCallNode>(selection.Operand);

        Assert.Equal(selection.Id, Assert.Single(Evaluate(Text, [P("request", "time")], Data(true)).Unknowns).ExprId);
        Assert.Equal(conditional.Arguments[2].Id, Assert.Single(Evaluate(Text, [P("request")], Data(true)).Unknowns).ExprId);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void ConditionAndKey_AreEvaluatedAndChargedOnce()
    {
        const string Text = "(b ? request : resource)[s]";
        var options = new CelEvalOptions { CostLimit = 1_000 };

        CelPartialEvalResult<CelValue> partial = Evaluate(Text, [P("request", "auth")], Data(true), options);
        CelEvalResult<CelValue> plain = Env.Compile<CelValue>(Text).GetProgramOrThrow().Evaluate(Data(true), options, CancellationToken.None);

        Assert.Equal("7", partial.GetValueOrThrow().ToString());
        Assert.Equal(plain.Cost, partial.Cost);
    }

    // Without an unknown value the plan evaluates as before: the same expressions, evaluated in full and by a partial program with no
    // pattern or with a pattern for another variable, give the values of the language.
    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("has((false ? d[100] : d).f)", "true")]
    [InlineData("(b ? request : resource).time", "7")]
    [InlineData("(b ? request : resource)[s]", "7")]
    [InlineData("size((x < 0 ? resource : request).auth.user)", "5")]
    [InlineData("(b ? (false ? resource : request) : resource).time", "7")]
    public static void EvaluationWithoutUnknownData_IsTheSameInFullAndInPartial(string expression, string expected)
    {
        CelValue full = Env.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(Data(true), cancellationToken: CancellationToken.None).GetValueOrThrow();
        CelValue withoutPatterns = Evaluate(expression, [], Data(true)).GetValueOrThrow();
        CelValue otherVariable = Evaluate(expression, [P("m")], Data(true)).GetValueOrThrow();

        Assert.Equal(expected, full.ToString());
        Assert.Equal(expected, withoutPatterns.ToString());
        Assert.Equal(expected, otherVariable.ToString());
    }

    // The trails of an unknown result, or the value as text: a string without quotes.
    private static string Show(CelPartialEvalResult<CelValue> result)
    {
        if (result.IsUnknown)
        {
            return "unknown " + Trails(result);
        }

        CelValue value = result.GetValueOrThrow();
        return value.TryGetString(out string? text) && text is not null ? text : value.ToString();
    }
}
