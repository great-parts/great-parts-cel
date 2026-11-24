using System;
using System.Collections.Generic;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Evaluation.PartialTestSupport;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Patterns of unknown data: an access that agrees with a pattern as far as both go is unknown, with the trail of the access up to
// the length of the pattern. request = {"auth": {"user": "alice", "claims": {"x-id": "1"}}, "time": 7}, l = [10, 20], d = [10, 20]
// of the type dyn, x = 1.
public sealed class AttributePatternTests
{
    private static readonly CelBindings Data = new CelBindings()
        .Add("x", 1L)
        .Add("y", 2L)
        .Add("request", Map(("auth", Map(("user", Str("alice")), ("claims", Map(("x-id", Str("1")))))), ("time", Int(7))))
        .Add("l", IntList(10, 20))
        .Add("d", IntList(10, 20));

    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("request.auth.user", "request.auth")]
    [InlineData("request", "request")]
    [InlineData("size(request)", "request")]
    [InlineData("has(request.auth)", "request.auth")]
    [InlineData("request.?auth.orValue({})", "request.auth")]
    [InlineData("request[\"auth\"].user", "request.auth")]
    public static void AccessThatAgreesWithAPattern_IsUnknownWithTheTrailUpToItsLength(string expression, string trail)
    {
        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P("request", "auth")], Data);

        Assert.True(result.IsUnknown);
        Assert.Equal(trail, Trails(result));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void UnknownAttribute_HasTheIdOfTheNodeWhereTheAccessReachedTheLengthOfThePattern()
    {
        CelEnvironment env = Env;
        var longer = Assert.IsType<CelSelectNode>(env.Parse("request.auth.user").GetParsedExprOrThrow().Root);
        var shorter = env.Parse("request").GetParsedExprOrThrow().Root;

        Assert.Equal(longer.Operand.Id, Assert.Single(Evaluate("request.auth.user", [P("request", "auth")], Data).Unknowns).ExprId);
        Assert.Equal(shorter.Id, Assert.Single(Evaluate("request", [P("request", "auth")], Data).Unknowns).ExprId);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void AccessWithAnotherQualifier_IsKnown()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("request.time", [P("request", "auth")], Data);

        Assert.True(result.IsSuccess);
        Assert.Equal("7", result.GetValueOrThrow().ToString());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void QualifierThatMatchesAny_MatchesAKeyAndTakesItIntoTheTrail()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("request.auth.claims[\"x-id\"]", [P("request", "auth", Any)], Data);

        Assert.Equal("request.auth.claims", Trails(result));
        Assert.Equal("request.auth.claims[\"x-id\"]", Trails(Evaluate("request.auth.claims[\"x-id\"]", [P("request", "auth", Any, Any)], Data)));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void QualifierOfAnotherKey_LeavesTheAccessKnown()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("request.auth.claims[\"x-id\"]", [P("request", "auth", "user")], Data);

        Assert.True(result.IsSuccess);
        Assert.True(result.GetValueOrThrow().TryGetString(out string? value));
        Assert.Equal("1", value);
    }

    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("l[1]", "l[1]")]
    [InlineData("d[1u]", "d[1]")]
    [InlineData("l[x]", "l[1]")]
    public static void IndexThatEqualsTheQualifier_IsUnknown(string expression, string trail)
    {
        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P(expression[..1], 1L)], Data);

        Assert.Equal(trail, Trails(result));
    }

    // The key of the run of difftest 633c7ad, case 11024: mx[x2] with x2 = -9223372036854775808.0 under the pattern mx[*].
    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("d[1.0]", "d[1]")]
    [InlineData("d[-9223372036854775808.0]", "d[-9223372036854775808]")]
    [InlineData("d[18446744073709549568.0]", "d[18446744073709549568u]")]
    public static void DoubleKeyThatIsAWholeNumber_IsTheIntOrUintOfItsValueInTheTrail(string expression, string trail)
    {
        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P("d", Any)], Data);

        Assert.True(result.IsUnknown);
        Assert.Equal(trail, Trails(result));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void IndexThatDiffersFromTheQualifier_IsKnown()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("l[0]", [P("l", 1L)], Data);

        Assert.Equal("10", result.GetValueOrThrow().ToString());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void UnknownKey_IsTheResult()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("l[x]", [P("l", 1L), P("x")], Data);

        Assert.Equal("x", Trails(result));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void FirstMatchingPattern_GivesTheTrail()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("request.time", [P("request", "time"), P("request")], Data);

        Assert.Equal("request.time", Trails(result));
    }

    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("[1].exists(request, request > 0)", "true")]
    [InlineData("cel.bind(request, 1, request + 1)", "2")]
    public static void VariableOfAComprehensionOrOfABind_DoesNotMatch(string expression, string expected)
    {
        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P("request")], Data);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.GetValueOrThrow().ToString());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void NameOfThePattern_IsTheFullNameOfTheVariableInTheContainer()
    {
        CelEnvironment env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .UseContainer("acme")
            .AddVariable<long>("acme.limit")
            .Build()
            .GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("acme.limit", 5L);

        CelPartialEvalResult<CelValue> unknown = Evaluate("limit", [P("acme.limit")], bindings, environment: env);
        CelPartialEvalResult<CelValue> known = Evaluate("limit", [P("limit")], bindings, environment: env);

        Assert.Equal("acme.limit", Trails(unknown));
        Assert.Equal("5", known.GetValueOrThrow().ToString());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Pattern_OfAnEmptyVariable_IsRefused()
    {
        _ = Assert.Throws<ArgumentException>(() => new CelAttributePattern(string.Empty));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Pattern_AsText_ShowsTheQualifiers()
    {
        CelAttributePattern pattern = P("a", "b", Any, 1L, 2UL, true, "x-y");

        Assert.Equal("a.b[*][1][2u][true][\"x-y\"]", pattern.ToString());
        Assert.Equal(6, pattern.Qualifiers.Length);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Pattern_IsImmutable_AndQualifyGivesANewOne()
    {
        var pattern = new CelAttributePattern("a");

        CelAttributePattern longer = pattern.Qualify("b");

        Assert.Empty(pattern.Qualifiers);
        Assert.Single(longer.Qualifiers);
        Assert.Equal("a", longer.Variable);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void PresenceTest_OfAFieldOfADynVariable_IsAnAttribute()
    {
        CelEnvironment env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("d", CelType.Dyn)
            .Build()
            .GetEnvironmentOrThrow();
        List<CelAttributePattern> patterns = [P("d", "f")];

        CelPartialEvalResult<CelValue> result = Evaluate("has(d.f)", patterns, new CelBindings().Add("d", Map(("f", Int(1)))), environment: env);

        Assert.Equal("d.f", Trails(result));
    }
}
