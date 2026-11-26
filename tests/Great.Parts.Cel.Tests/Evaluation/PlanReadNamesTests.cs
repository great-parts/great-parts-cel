using System;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The names of a plan that evaluation can look up in the bindings (Plan.ReadNames): the names of the variable nodes and the candidates
// of the name nodes, without the variables of a comprehension, which are slots of the frame and not bindings.
public sealed class PlanReadNamesTests
{
    private static string[] Sorted(string[]? names) => [.. Assert.IsType<string[]>(names).Order(StringComparer.Ordinal)];

    // Every variable is listed once, whatever the number of reads and the depth of the chain of selections over it.
    [Theory]
    [InlineData("n + 1", "n")]
    [InlineData("n + n", "n")]
    [InlineData("a || b", "a b")]
    [InlineData("flag && (n > 0 || a)", "a flag n")]
    [InlineData("any.f.g == 1", "any")]
    [InlineData("has(any.f)", "any")]
    [InlineData("any[n] == 1", "any n")]
    public static void CheckedPlan_ListsTheVariablesItReads(string expression, string names)
    {
        ArgumentNullException.ThrowIfNull(names);

        Assert.Equal(names.Split(' '), Sorted(Plans.Checked(expression).ReadNames));
    }

    // The variable of a comprehension and its accumulator are locals; a variable that the body reads is listed.
    [Theory]
    [InlineData("[1, 2].all(i, i > n)", "n")]
    [InlineData("any.exists(i, i == n)", "any n")]
    [InlineData("[1, 2].all(i, [3].exists(j, i < j && j < n))", "n")]
    public static void CheckedPlan_ListsTheVariablesOfTheBodyOfAComprehension(string expression, string names)
    {
        ArgumentNullException.ThrowIfNull(names);

        Assert.Equal(names.Split(' '), Sorted(Plans.Checked(expression).ReadNames));
    }

    // A variable of the environment that a comprehension variable hides is read from the slot of the comprehension, not from the
    // bindings, so the plan does not list it: the set is empty, which is not the same as unknown.
    [Fact]
    public static void CheckedPlan_DoesNotListAVariableThatAComprehensionVariableHides()
    {
        string[]? names = Plans.Checked("[1, 2].all(n, n > 0)").ReadNames;

        Assert.NotNull(names);
        Assert.Empty(names);
    }

    [Fact]
    public static void CheckedPlan_OfALiteral_HasAnEmptySetThatIsNotNull()
    {
        string[]? names = Plans.Checked("1 + 2 == 3").ReadNames;

        Assert.NotNull(names);
        Assert.Empty(names);
    }

    // Without type checking a name node looks up every candidate: the identifier and the qualified names of its chain, the longest first.
    [Fact]
    public static void PlanWithoutTypeCheck_ListsTheCandidatesOfEachNameNode()
    {
        Assert.Equal(["x", "y", "y.z"], Sorted(Plans.WithoutTypeCheck("x + y.z").ReadNames));
    }

    [Fact]
    public static void PlanWithoutTypeCheck_DoesNotListTheVariableOfAComprehension()
    {
        Assert.Equal(["x"], Sorted(Plans.WithoutTypeCheck("[1, 2].all(i, i > x)").ReadNames));
    }

    // The set has at most eight names: a program that reads more has no set, and the bindings take every name.
    [Fact]
    public static void PlanWithoutTypeCheck_OfEightNames_HasTheSet()
    {
        string[]? names = Plans.WithoutTypeCheck("v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8").ReadNames;

        Assert.Equal(["v1", "v2", "v3", "v4", "v5", "v6", "v7", "v8"], Sorted(names));
    }

    [Fact]
    public static void PlanWithoutTypeCheck_OfNineNames_HasNoSet()
    {
        Assert.Null(Plans.WithoutTypeCheck("v1 + v2 + v3 + v4 + v5 + v6 + v7 + v8 + v9").ReadNames);
    }

    // A plan for partial evaluation has attribute nodes of its own, so its set is not known.
    [Fact]
    public static void PartialPlan_HasNoSet()
    {
        CelParseResult parsed = Plans.Environment.Parse("n + 1");
        CheckResult checkedResult = Plans.Environment.Check(parsed.ParsedExpr!);
        Assert.True(checkedResult.IsSuccess);

        Plan plan = Planner.For(checkedResult.Expression, Plans.Environment.Declarations, null, partial: true).Run();

        Assert.Null(plan.ReadNames);
    }
}
