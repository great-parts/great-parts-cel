using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The two limits of the extension sets (the page of the limits, "Extension sets"): the length of the result
// of lists.range, 0 to 1 000 000, and the precision of format of the strings set, version 5, at most 100. Each
// has an example at the limit, which gives its value, and one over, which is an error. The errors of an evaluation are
// InvalidArgument at the call, the "(" after the name; the precision of a format string that is a constant
// in a call with a list literal is refused earlier, at compilation (Checking/FormatCheckTests). The values and
// the texts are those of cel-go v0.32.0, checked by a probe of the oracle on 2026-11-09, the texts of the page
// of the limits in place of the Go prefix "lists.range:". An argument that is invalid and plain to see is an
// error before the cost of the call is charged, so the call costs the constant 1 of its formula and no more
// (the page of the cost, rule 2.10).
[Trait("Requirement", "F-1")]
[Trait("Requirement", "R-3")]
public sealed class ExtensionLimitTests
{
    private const string RangeValues = """
        size(lists.range(1000000)) => 1000000
        lists.range(1000000)[0] => 0
        lists.range(1000000)[999999] => 999999
        size(lists.range(n)) => 1000000
        lists.range(0) => []
        lists.range(1) => [0]
        lists.range(3) => [0, 1, 2]
        lists.range(3).size() => 3
        """;

    private const string RangeErrors = """
        Evaluate | lists.range^(1000001) | InvalidArgument | invalid argument to 'lists.range': size 1000001 exceeds maximum allowed (1000000)
        Evaluate | size(lists.range^(1000001)) | InvalidArgument | invalid argument to 'lists.range': size 1000001 exceeds maximum allowed (1000000)
        Evaluate | lists.range^(9223372036854775807) | InvalidArgument | invalid argument to 'lists.range': size 9223372036854775807 exceeds maximum allowed (1000000)
        Evaluate | lists.range^(-1) | InvalidArgument | invalid argument to 'lists.range': size must be non-negative, got -1
        Evaluate | lists.range^(-9223372036854775808) | InvalidArgument | invalid argument to 'lists.range': size must be non-negative, got -9223372036854775808
        Evaluate | lists.range^(over) | InvalidArgument | invalid argument to 'lists.range': size 1000001 exceeds maximum allowed (1000000)
        Evaluate | lists.range^(negative) | InvalidArgument | invalid argument to 'lists.range': size must be non-negative, got -5
        """;

    // The format string in a variable is read when the call is evaluated, whatever the list: the precision of 100 is
    // formatted, one more is InvalidArgument; the precision is read for every specifier and used by %f and %e.
    private const string PrecisionValues = """
        size(f100.format([1.5])) => 102
        f100.format([1.5]).replace('0', '') => '1.5'
        f100.format([1.5]).startsWith('1.5000') => true
        size(e100.format([1.5])) => 106
        e100.format([1.5]).endsWith('0e+00') => true
        s100.format(['a']) => 'a'
        s100.format([1.5]) => '1.5'
        d100.format([1]) => '1'
        f0.format([1.5]) => '2'
        f0.format([2.5]) => '2'
        '%.100f'.format(items) => f100.format([1.5])
        size('%.100f'.format(items)) => 102
        size('%.100f'.format([x])) => 102
        """;

    private const string PrecisionErrors = """
        Evaluate | f101.format^([1.5]) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        Evaluate | e101.format^([1.5]) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        Evaluate | s101.format^(['a']) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        Evaluate | d101.format^([1]) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        Evaluate | f1000.format^([1]) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: error while parsing precision: precision 1000 exceeds maximum allowed precision 100
        Evaluate | '%.101f'.format^(items) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        Evaluate | f101.format^(items) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Lists(4))
        .AddVariable<long>("n")
        .AddVariable<long>("over")
        .AddVariable<long>("negative")
        .AddVariable<string>("f100")
        .AddVariable<string>("f101")
        .AddVariable<string>("f1000")
        .AddVariable<string>("f0")
        .AddVariable<string>("e100")
        .AddVariable<string>("e101")
        .AddVariable<string>("s100")
        .AddVariable<string>("s101")
        .AddVariable<string>("d100")
        .AddVariable<string>("d101")
        .AddVariable("items", CelType.ListOf(CelType.Dyn))
        .AddVariable("x", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("n", 1_000_000L)
        .Add("over", 1_000_001L)
        .Add("negative", -5L)
        .Add("f100", "%.100f")
        .Add("f101", "%.101f")
        .Add("f1000", "%.1000f")
        .Add("f0", "%.0f")
        .Add("e100", "%.100e")
        .Add("e101", "%.101e")
        .Add("s100", "%.100s")
        .Add("s101", "%.101s")
        .Add("d100", "%.100d")
        .Add("d101", "%.101d")
        .Add("items", CelList.Create(CelValue.FromDouble(1.5)))
        .Add("x", 1.5);

    [Fact]
    public void ListsRange_OfTheLimit_GivesTheList()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, RangeValues, bindings));
    }

    [Fact]
    public void ListsRange_OverTheLimitOrBelowZero_IsInvalidArgument()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, RangeErrors, bindings));
    }

    [Fact]
    public void FormatPrecision_Of100_IsFormatted()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, PrecisionValues, bindings));
    }

    [Fact]
    public void FormatPrecision_Of101_IsInvalidArgumentAtEvaluation()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, PrecisionErrors, bindings));
    }

    // The call of lists.range with an argument over the limit or below zero stops before its cost is charged, so
    // the limit of cost 1, the constant of the formula, is enough for the error of the argument and a limit of 0 is
    // not; the call at the limit costs 1 + 1 000 000, the argument of a literal costing 0.
    [Fact]
    public void ListsRange_RefusesTheArgumentBeforeItChargesTheFormula()
    {
        Assert.Equal(CelErrorCode.InvalidArgument, SingleCode("lists.range(-1)", cost: 1));
        Assert.Equal(CelErrorCode.CostLimitExceeded, SingleCode("lists.range(-1)", cost: 0));
        Assert.Equal(CelErrorCode.InvalidArgument, SingleCode("lists.range(1000001)", cost: 1));
        Assert.Equal(CelErrorCode.CostLimitExceeded, SingleCode("lists.range(1000001)", cost: 0));
        Assert.Equal(CelErrorCode.InvalidArgument, SingleCode("lists.range(over)", cost: 2));
        Assert.Equal(CelErrorCode.CostLimitExceeded, SingleCode("lists.range(over)", cost: 1));
        Assert.Equal(CelErrorCode.CostLimitExceeded, SingleCode("lists.range(1000000)", cost: 1_000_000));
        Assert.Null(SingleCode("lists.range(1000000)", cost: 1_000_001));
        Assert.Null(SingleCode("size(lists.range(1000000))", cost: 1_000_002));
        Assert.Equal(CelErrorCode.CostLimitExceeded, SingleCode("size(lists.range(1000000))", cost: 1_000_001));
    }

    // The code of the only error of the evaluation with the limit of cost, or null for a value.
    private CelErrorCode? SingleCode(string expression, ulong cost)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None);

        return result.IsSuccess ? null : Assert.Single(result.Errors).Code;
    }
}
