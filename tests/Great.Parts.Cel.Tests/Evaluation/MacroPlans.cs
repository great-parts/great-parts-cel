using System.Collections.Immutable;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Plans of expressions with macros and the special forms, checked against the table of the library, which
// declares the functions that the expansions call. The variables are xs: list(dyn), ys: list(int),
// m: map(string, int), d: dyn and n: int.
internal static class MacroPlans
{
    private static readonly ImmutableArray<CelVariableDecl> Variables =
    [
        new("xs", CelType.ListOf(CelType.Dyn)),
        new("ys", CelType.ListOf(CelType.Int)),
        new("m", CelType.MapOf(CelType.String, CelType.Int)),
        new("d", CelType.Dyn),
        new("n", CelType.Int),
    ];

    internal static Plan Checked(string expression)
    {
        DeclarationIndex index = DeclarationIndex.Create(StandardLibraryV1.Overloads, Variables);
        CheckResult checkedResult = Checker.Check(Parse(expression), index);
        Assert.True(checkedResult.IsSuccess, expression);
        return Planner.Plan(checkedResult.Expression, index);
    }

    internal static Plan WithoutTypeCheck(string expression) =>
        Planner.PlanWithoutTypeCheck(Parse(expression), DeclarationIndex.Create(StandardLibraryV1.Overloads, Variables));

    // A list of the integers 1 to count.
    internal static CelValue Integers(int count)
    {
        var items = new CelValue[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = CelValue.FromInt64(i + 1);
        }

        return CelValue.FromList(CelList.Create(items));
    }

    private static ParsedExpression Parse(string expression)
    {
        ParseResult parsed = Plans.Environment.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);
        return parsed.Expression;
    }
}
