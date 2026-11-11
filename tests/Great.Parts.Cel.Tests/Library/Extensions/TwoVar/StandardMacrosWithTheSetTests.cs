using System;
using System.Threading;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.TwoVar;

// The standard macros of one variable in an environment that has the set of two-variable comprehensions: the set adds the forms of three
// and four arguments and leaves the others alone, so each standard form gives the value it gave and costs the units it cost without
// the set. The cost is found as the least limit at which the evaluation passes, in both environments, so that the test holds no table
// of units of its own.
public sealed class StandardMacrosWithTheSetTests
{
    private const ulong Greatest = 400;

    // Each row is an expression that is true.
    private const string Rows = """
        [1, 2, 3].all(x, x > 0)
        ![1, 2, 3].all(x, x > 1)
        [1, 2, 3].exists(x, x > 2)
        ![1, 2, 3].exists(x, x > 3)
        [1, 2, 3].exists_one(x, x > 2)
        ![1, 2, 3].exists_one(x, x > 1)
        [1, 2, 3].map(x, x * 2) == [2, 4, 6]
        [1, 2, 3].map(x, x > 1, x) == [2, 3]
        [1, 2, 3].filter(x, x > 1) == [2, 3]
        {'a': 1}.all(k, k == 'a')
        {'a': 1, 'b': 2}.map(k, k) == ['a', 'b']
        [1, 2].map(x, [3, 4].all(y, y > x)) == [true, true]
        [[1], [2, 3]].map(x, x.all(y, y > 0)) == [true, true]
        """;

    public static TheoryData<string> Expressions
    {
        get
        {
            TheoryData<string> data = [];
            foreach (string row in Rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                data.Add(row.Trim());
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Expressions))]
    public static void StandardForm_HasTheValueAndTheCostItHasWithoutTheSet(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        ulong with = Least(TwoVarEnvironment.Environment, expression);
        ulong without = Least(TwoVarEnvironment.WithoutTheSet, expression);

        Assert.True(with > 0);
        Assert.Equal(without, with);
    }

    // The least limit at which the expression passes and is true; the expression fails below it. An expression that is not true at
    // any limit up to Greatest fails the test.
    private static ulong Least(CelEnvironment environment, string expression)
    {
        CelProgram<CelValue> program = environment.Compile<CelValue>(expression).GetProgramOrThrow();
        for (ulong limit = 0; limit <= Greatest; limit++)
        {
            CelEvalResult<CelValue> result = program.Evaluate(new CelBindings(), new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
            if (result.IsSuccess)
            {
                Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
                return limit;
            }
        }

        throw new InvalidOperationException(expression + " does not pass at " + Greatest);
    }
}
