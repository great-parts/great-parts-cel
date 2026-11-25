using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
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

    // A key that a string, an int, a uint or a bool does not name — a double that is no whole number, NaN, an infinity, bytes — cannot
    // be in the trail, which the public pattern holds with those four kinds: the trail stops before it, and the unknown is the data
    // under the qualifiers before it.
    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("d[1.5]")]
    [InlineData("d[0.0 / 0.0]")]
    [InlineData("d[1.0 / 0.0]")]
    [InlineData("d[b'a']")]
    public static void KeyThatNoQualifierNames_StopsTheTrailBeforeIt(string expression)
    {
        CelPartialEvalResult<CelValue> result = Evaluate(expression, [P("d", Any)], Data);

        Assert.True(result.IsUnknown);
        Assert.Equal("d", Trails(result));
        Assert.Empty(Assert.Single(result.Unknowns).Attribute!.Qualifiers);
    }

    // The run of difftest 4cba15e, case 168328: x2.b[x2.a] under the pattern x2.a[*]. The key is an unknown value that matches no pattern
    // with the access, and the field before it is not there: an error, which a strict call gives precedence over an unknown value, and
    // which cel-go reports too. With the field there, the unknown key is the result. Where a pattern matches the access, the pattern
    // names the whole variable here, the chain is not evaluated and the unknown key is the result.
    [Fact]
    [Trait("Requirement", "F-22")]
    public void UnknownKey_IsTheResult_UnlessNoPatternMatchesAndTheChainFailsBeforeIt()
    {
        CelPartialEvalResult<CelValue> failed = Evaluate("request.nothing[request.auth.user]", [P("request", "auth", "user")], Data);
        CelPartialEvalResult<CelValue> unknown = Evaluate("request.auth[request.auth.user]", [P("request", "auth", "user")], Data);
        CelPartialEvalResult<CelValue> matched = Evaluate("request.nothing[request.auth.user]", [P("request")], Data);

        Assert.False(failed.IsUnknown);
        Assert.Equal(CelErrorCode.EvalNoSuchField, Assert.Single(failed.Errors).Code);
        Assert.Equal("request.auth.user", Trails(unknown));
        Assert.Equal("request", Trails(matched));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void KeyThatNoQualifierNames_StopsTheTrailAfterTheQualifiersBeforeIt()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("request.auth.claims[1.5]", [P("request", "auth", Any, Any)], Data);

        Assert.True(result.IsUnknown);
        Assert.Equal("request.auth.claims", Trails(result));
        Assert.Equal(2, Assert.Single(result.Unknowns).Attribute!.Qualifiers.Length);
    }

    // The same node of a loop is unknown with a trail for each run, and the result lists them all, as cel-go does: the run of
    // difftest 03465a2, case 2726, the unknowns lx[0] and lx[1] of lx[i].
    [Fact]
    [Trait("Requirement", "F-22")]
    public void NodeOfALoop_IsUnknownWithTheTrailOfEveryRun()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("[0, 1].map(i, d[i])", [P("d", Any)], Data);

        Assert.True(result.IsUnknown);
        Assert.Equal("d[0],d[1]", Trails(result));
        Assert.Equal(2, result.Unknowns.Length);
        Assert.Equal(result.Unknowns[0].ExprId, result.Unknowns[1].ExprId);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void NodeOfALoop_IsUnknownOnceForARunWithTheSameTrail()
    {
        CelPartialEvalResult<CelValue> result = Evaluate("[0, 1, 0, 1, 1].map(i, d[i])", [P("d", Any)], Data);

        Assert.Equal("d[0],d[1]", Trails(result));
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
