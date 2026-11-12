#if NET11_0_OR_GREATER
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost of the set decimal, version 1 (the page of the cost, the table of the set decimal and the equality row of the class of
// comparison): the cost of an overload of the set is 1, except decimal(double), which is 4, decimal(string), which is 1 + the
// length of the string over 10 rounded up, and string() of a decimal, which is 1 + the length of the text it makes over 10 rounded
// up (that text has at most 42 characters, so at most 5 for it). Rows are checked as in OptionalCostTableTests: "row | expression |
// cost", the total cost of the expression is exact, so the evaluation succeeds with that cost as the limit and fails with
// CostLimitExceeded at one unit less. The total is the cost of the row plus the nodes around it: a literal of any kind, the decimal
// literals too, 0; a read of a variable 1; a list of n elements 1 + n. The equality of two decimals, and of a decimal with a value
// of another kind, costs 1 (class of comparison of numbers), like the four orderings; in over a list of n elements costs 1 + the
// comparisons made, until the first equal element. The macros are charged by their expansion: the range, the step and the body
// of each step that is run, the empty list that map and filter start from, and the elements they add. Variables: i = 41 (int),
// u = 7u, f = 1.5 (double), d = 1.5 (decimal), t = '123.45' (6 units of text), s26 = a text of 26 units.
[Trait("Requirement", "R-3")]
[Trait("Requirement", "F-5")]
public sealed class DecimalCostTableTests
{
    private const string Max34 = DecimalExample.Max34;

    // The operators and the four orderings: 1 each, plus what the operands cost.
    private const string Operators = """
        add_decimal | 1.5m + 2.5m | 1
        add_decimal, a variable on each side | d + d | 3
        subtract_decimal | 2.5m - 1.5m | 1
        multiply_decimal | 2m * 3m | 1
        divide_decimal | 6m / 3m | 1
        negate_decimal | -(1.5m) | 1
        negate_decimal, a variable | -(d) | 2
        less_decimal | 1.5m < 2.5m | 1
        less_decimal, a variable | d < 2.5m | 2
        less_equals_decimal | 1.5m <= 2.5m | 1
        greater_decimal | 2.5m > 1.5m | 1
        greater_equals_decimal | 2.5m >= 1.5m | 1
        """;

    // The conversions of numbers: 1, and 4 for a double.
    private const string Conversions = """
        int64_to_decimal | decimal(5) | 1
        int64_to_decimal, a variable | decimal(i) | 2
        uint64_to_decimal | decimal(5u) | 1
        uint64_to_decimal, a variable | decimal(u) | 2
        double_to_decimal | decimal(1.5) | 4
        double_to_decimal, a variable | decimal(f) | 5
        decimal_to_decimal | decimal(1.5m) | 1
        decimal_to_decimal, a variable | decimal(d) | 2
        decimal_to_int64 | int(1.5m) | 1
        decimal_to_int64, a variable | int(d) | 2
        decimal_to_uint64 | uint(1.5m) | 1
        decimal_to_double | double(1.5m) | 1
        """;

    // The text: the string that decimal() reads and the string that string() makes, 1 + the length over 10 rounded up. The edge is at
    // the multiples of 10.
    private const string Texts = $$"""
        string_to_decimal, 3 units | decimal('1.5') | 2
        string_to_decimal, 10 units | decimal('1234567890') | 2
        string_to_decimal, 11 units | decimal('12345678901') | 3
        string_to_decimal, a variable of 6 units | decimal(t) | 3
        string_to_decimal, a variable of 26 units | decimal(s26) | 5
        decimal_to_string, a text of 3 | string(1.5m) | 2
        decimal_to_string, a text of 10 | string(1234567.89m) | 2
        decimal_to_string, a text of 11 | string(12345678.90m) | 3
        decimal_to_string, a scientific text of 4 | string(1e3m) | 2
        decimal_to_string, a negative zero | string(-0m) | 2
        decimal_to_string, the longest positive text, 41 | string({{Max34}}e6111m) | 6
        decimal_to_string, the longest text, 42 | string(-{{Max34}}e6111m) | 6
        decimal_to_string, a variable | string(d) | 3
        """;

    // Table of the comparison class: equality of two decimals, and of kinds that differ, costs 1; in is 1 + the comparisons made.
    private const string Equality = """
        equals of decimals | 1.5m == 1.50m | 1
        equals of decimals, variables | d == d | 3
        not_equals of decimals, a variable | d != 2.5m | 2
        equals under dyn, kinds that differ | dyn(1.5m) == 1 | 2
        list of decimals | [1.5m, 2.5m] | 3
        in_list, found at the first element | 1.5m in [1.5m, 2.5m] | 5
        in_list, found at the second element | 2.5m in [1.5m, 2.5m] | 6
        in_list, not found | 3m in [1.5m, 2.5m] | 6
        """;

    // The macros: the range 3, then for each step run 1 and the body (a read of the variable 1 and the operator 1); all stops at the
    // first false, map and filter start from the empty list 1 and add 1 for each element they put in the result.
    private const string Macros = """
        all, 2 steps | [1.5m, 2.5m].all(x, x > 1m) | 9
        all, stopped at the first step | [1.5m, 2.5m].all(x, x > 2m) | 6
        map, 2 steps and 2 elements | [1.5m, 2.5m].map(x, x * 2m) | 12
        filter, 2 steps and 1 element | [1.5m, 2.5m].filter(x, x > 2m) | 11
        """;

    private readonly CelEnvironment environment = DecimalExample.Builder()
        .AddVariable<long>("i")
        .AddVariable<ulong>("u")
        .AddVariable<double>("f")
        .AddVariable<Decimal128>("d")
        .AddVariable<string>("t")
        .AddVariable<string>("s26")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("i", 41L)
        .Add("u", 7UL)
        .Add("f", 1.5)
        .Add("d", DecimalExample.Make(15, -1))
        .Add("t", "123.45")
        .Add("s26", "12345678901234567890123456");

    [Fact]
    public void OperatorRows_CostOnePlusTheOperands() => Assert.Empty(CheckRows(Operators));

    [Fact]
    public void ConversionRows_CostOneAndFourForADouble() => Assert.Empty(CheckRows(Conversions));

    [Fact]
    public void TextRows_CostOnePlusTheLengthOverTenRoundedUp() => Assert.Empty(CheckRows(Texts));

    [Fact]
    public void EqualityRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Equality));

    [Fact]
    public void MacroRows_CostTheirExpansion() => Assert.Empty(CheckRows(Macros));

    private List<string> CheckRows(string rows)
    {
        var failures = new List<string>();
        foreach (string row in ExtensionRows.Lines(rows))
        {
            string[] cells = row.Split(" | ");
            string? failure = Check(cells[1], ulong.Parse(cells[2], CultureInfo.InvariantCulture));
            if (failure is not null)
            {
                failures.Add(row + ": " + failure);
            }
        }

        return failures;
    }

    private string? Check(string expression, ulong cost)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
        }

        CelProgram<CelValue> program = compiled.GetProgramOrThrow();
        CelEvalResult<CelValue> atCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None);
        if (!atCost.IsSuccess)
        {
            return "limit " + cost.ToString(CultureInfo.InvariantCulture) + ": " + atCost.Errors[0].Code + " at " + atCost.Errors[0].Location.Offset.ToString(CultureInfo.InvariantCulture);
        }

        CelEvalResult<CelValue> belowCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
        return belowCost.Errors.Length == 1 && belowCost.Errors[0].Code == CelErrorCode.CostLimitExceeded
            ? null
            : "limit " + (cost - 1).ToString(CultureInfo.InvariantCulture) + " is enough";
    }
}
#endif
