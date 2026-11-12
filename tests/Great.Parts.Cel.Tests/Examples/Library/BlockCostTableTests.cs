using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost of the internal set block (the cost page, the table 6.7 for cel_block_list and the macros of the table 9),
// checked as the rows of ExtensionCostTableTests are: "row | expression | cost", the total cost is exact, so the evaluation succeeds with
// that cost as the limit and fails with CostLimitExceeded at one unit less. cel.@block costs 0 of its own; the
// list of the slots is not built, so it costs nothing, and a slot costs what its expression costs at the first read of
// @indexN and nothing at the others and when it is never read; the read of @indexN is the read of a variable, 1. A
// read of a slot costs 1 whichever cell serves it, so a read that goes on to an enclosing block costs 1 as
// well; a block that is in the body of a fold is entered at each step, and its slots are evaluated again at each step. cel.index,
// cel.iterVar and cel.accuVar are the reads of a variable, 1 each, so a fold with cel.iterVar costs what the same fold
// with a name costs, and cel.bind with cel.accuVar what it costs with a name. A row that costs 0 is no row (the evaluation
// at the limit one less cannot be asked). Variable: i = 41 (int).
[Trait("Requirement", "R-3")]
[Trait("Requirement", "F-1")]
public sealed class BlockCostTableTests
{
    private const string Rows = """
        cel_block_list, a slot read once | cel.block([1 + 1], cel.index(0)) | 2
        cel_block_list, a slot of a variable | cel.block([i + 1], cel.index(0)) | 3
        cel_block_list, a slot that is a variable | cel.block([i], cel.index(0)) | 2
        cel_block_list, a slot read twice, evaluated once | cel.block([i + 1], cel.index(0) + cel.index(0)) | 5
        cel_block_list, a slot that is not read | cel.block([i + 1, 2], cel.index(1)) | 1
        cel_block_list, slots that read slots | cel.block([i + 1, cel.index(0) * 2], cel.index(1) + cel.index(0)) | 7
        cel_block_list, a read that goes out to the outer block | cel.block([1], cel.block([cel.index(0) + 1], cel.index(0))) | 3
        cel_block_list, a block in the body of a fold | [1, 2].map(x, cel.block([x + 1], cel.index(0))) | 14
        cel.iterVar, a fold with the names | [1, 2].map(cel.iterVar(0, 0), cel.iterVar(0, 0)) | 10
        cel.accuVar, cel.bind with the name | cel.bind(cel.accuVar(0, 0), 1, cel.accuVar(0, 0) + 1) | 3
        """;

    private readonly CelEnvironment environment = BlockExample.Builder()
        .AddVariable<long>("i")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("i", 41L);

    [Fact]
    public void BlockRows_CostWhatTheCostPageSays() => Assert.Empty(CheckRows(Rows));

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
