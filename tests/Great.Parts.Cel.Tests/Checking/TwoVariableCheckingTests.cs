using System;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Library.Extensions.TwoVar;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// Type checking of the comprehensions of two variables: the first variable is the index of a list or the key of a map, the second the
// element or the value; a range of type dyn gives dyn twice; another range is InvalidComprehensionRange; both names shadow the
// declarations for the body only; and the result type is that of the accumulator after the step of the macro.
public sealed class TwoVariableCheckingTests
{
    // "<expression> → <type of the expression>" in the environment of the set, whose variables are i and j (int), ints (list(int)),
    // counts (map(string, int)) and d (dyn).
    private const string Types = """
        ints.all(i, v, v > i) → bool
        ints.exists(i, v, i == v) → bool
        ints.existsOne(i, v, v > 0) → bool
        ints.exists_one(i, v, v > 0) → bool
        ints.transformList(i, v, i) → list(int)
        ints.transformList(i, v, v > 10, v * 2) → list(int)
        counts.transformList(k, v, k) → list(string)
        counts.transformList(k, v, v) → list(int)
        counts.transformMap(k, v, v + 1) → map(string, int)
        ints.transformMap(i, v, v > 10, v) → map(int, int)
        counts.transformMapEntry(k, v, {v: k}) → map(int, string)
        ints.transformMapEntry(i, v, v > 10, {i: v}) → map(int, int)
        d.transformList(i, v, v) → list(dyn)
        d.transformMap(i, v, v) → map(dyn, dyn)
        d.all(i, v, i == 0) → bool
        ints.all(i, v, ints.exists(k, w, k < i)) → bool
        counts.all(i, j, i == 'a' || j > 0) → bool
        counts.all(i, j, i == 'a') && i == 1 → bool
        ints.all(i, v, v > j) → bool
        """;

    public static TheoryData<string, string> TypeCases => Rows(Types);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public static void Expression_HasTheTypeOfItsRule(string expression, string type)
    {
        CheckResult result = Check(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.Equal(type, result.Expression.ResultType.ToString());
    }

    // The variables are references to the locals of the comprehension, typed by the range: the key and the value of a map, and in the
    // body the second name is the value type.
    [Fact]
    public static void VariablesOfAMapRange_AreLocalsWithTheTypeOfTheKeyAndOfTheValue()
    {
        CheckResult result = Check("counts.all(k, v, v > 0 && k != 'x')");

        Assert.True(result.IsSuccess);
        var loop = (ComprehensionNode)result.Expression.Parsed.Root;
        var both = (CallNode)((CallNode)loop.LoopStep).Arguments[1];
        var value = (IdentNode)((CallNode)both.Arguments[0]).Arguments[0];
        var key = (IdentNode)((CallNode)both.Arguments[1]).Arguments[0];
        Assert.Equal("v", result.Expression.References[value.Id].Local);
        Assert.Equal(CelType.Int, result.Expression.Types[value.Id]);
        Assert.Equal("k", result.Expression.References[key.Id].Local);
        Assert.Equal(CelType.String, result.Expression.Types[key.Id]);
    }

    // The index of a list is an int whatever the type of its elements.
    [Fact]
    public static void IndexOfAListRange_IsAnIntAndTheElementIsTheElementType()
    {
        CheckResult result = Check("[[1], [2]].all(n, e, n >= 0 && e.size() >= 1)");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
    }

    // A mismatch in the body is the error of its operator, with the types of the variables in its message: the key of the map is a
    // string and its value an int.
    [Fact]
    public static void MismatchOfTheTwoTypes_IsNoMatchingOverloadAtTheComparison()
    {
        const string Text = "counts.all(k, v, k == v)";

        CelError error = Assert.Single(Check(Text).Errors);

        Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code);
        Assert.Equal(Text.IndexOf("==", StringComparison.Ordinal), error.Location.Offset);
        Assert.Contains("(string, int)", error.Message, StringComparison.Ordinal);
    }

    // A range that is no list, no map and no dyn is InvalidComprehensionRange at the range, once, and the body is still checked with
    // the variables of the type error, so it gives no second error.
    [Theory]
    [InlineData("i.all(a, b, a > b)")]
    [InlineData("'abc'.transformList(a, b, a)")]
    [InlineData("true.transformMap(a, b, b)")]
    public static void RangeThatIsNoCollection_IsInvalidComprehensionRange(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        CelError error = Assert.Single(Check(text).Errors);

        Assert.Equal(CelErrorCode.InvalidComprehensionRange, error.Code);
        Assert.Equal(0, error.Location.Offset);
    }

    // The body sees the shadowed names, and outside the macro the declarations come back.
    [Fact]
    public static void ShadowedNames_AreThoseOfTheDeclarationsOutsideTheBody()
    {
        Assert.True(Check("counts.all(i, j, i == 'a') && i == 1 && j == 2").IsSuccess);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(Check("ints.all(i, v, true) && i == 'a'").Errors).Code);
    }

    private static CheckResult Check(string expression)
    {
        ParseResult parsed = TwoVarEnvironment.Environment.Parse(expression);
        Assert.True(parsed.IsSuccess, expression + ": " + string.Join("; ", parsed.Errors));
        return TwoVarEnvironment.Environment.Check(parsed.Expression);
    }

    private static TheoryData<string, string> Rows(string rows)
    {
        TheoryData<string, string> data = [];
        foreach (string row in rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = row.LastIndexOf(" → ", StringComparison.Ordinal);
            data.Add(row[..separator].Trim(), row[(separator + 3)..].Trim());
        }

        return data;
    }
}
