using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The set decimal, version 2: round and quantize (the page of the decimal type, "Rounding"). round(n) gives n digits after the
// point, n below zero to tens, hundreds and so on, and adds no zeros; quantize(q) gives the exponent of q, whatever the value of q.
// Both take the mode of the rounding as a string: half_even, which is the default, half_up (a tie away from zero), half_down,
// down (toward zero), up (away from zero), floor and ceiling; another string is InvalidArgument at evaluation, a constant too. A
// row that shows the digits is written as string(x). The rows without a directive have the same outcome on both targets; the
// exponent of a result is kept above 0 on net11.0 and folded into the coefficient on net10.0, and a result of more digits than
// the platform holds is Overflow.
[Trait("Requirement", "F-33")]
public sealed class DecimalRoundingTests
{
    private const string HalfEven = """
        string((1.245m).round(2)) => '1.24'
        string((1.255m).round(2)) => '1.26'
        string((1.245m).round(2, 'half_even')) => '1.24'
        string((1.5m).round(3)) => '1.5'
        string((9.99m).round(1)) => '10.0'
        string((-0.4m).round(0)) => '-0'
        string((1.5m).round(9223372036854775807)) => '1.5'
        string((1.5m).round(0)) => '2'
        string((0.5m).round(0)) => '0'
        string((-1.5m).round(0)) => '-2'
        """;

    private const string Modes = """
        string((1.245m).round(2, 'half_up')) => '1.25'
        string((-1.245m).round(2, 'half_up')) => '-1.25'
        string((2.5m).round(0)) => '2'
        string((2.5m).round(0, 'half_even')) => '2'
        string((2.5m).round(0, 'half_up')) => '3'
        string((2.5m).round(0, 'half_down')) => '2'
        string((2.5m).round(0, 'down')) => '2'
        string((2.5m).round(0, 'up')) => '3'
        string((2.5m).round(0, 'floor')) => '2'
        string((2.5m).round(0, 'ceiling')) => '3'
        string((-2.5m).round(0, 'floor')) => '-3'
        string((-2.5m).round(0, 'ceiling')) => '-2'
        string((-2.5m).round(0, 'down')) => '-2'
        string((-2.5m).round(0, 'up')) => '-3'
        string((-2.5m).round(0, 'half_up')) => '-3'
        string((-2.5m).round(0, 'half_down')) => '-2'
        string((2.51m).round(0, 'half_down')) => '3'
        string((2.49m).round(0, 'half_up')) => '2'
        string((2.01m).round(0, 'up')) => '3'
        string((2.01m).round(0, 'ceiling')) => '3'
        string((2.01m).round(0, 'floor')) => '2'
        string((-0.4m).round(0, 'floor')) => '-1'
        string((-0.4m).round(0, 'ceiling')) => '-0'
        string((0.4m).round(0, 'floor')) => '0'
        string((0.4m).round(0, 'ceiling')) => '1'
        string((0.00m).round(0, 'up')) => '0'
        string((-0.00m).round(0, 'up')) => '-0'
        """;

    private const string Quantize = """
        string((1.5m).quantize(0.001m)) => '1.500'
        string((1.2345m).quantize(0.01m)) => '1.23'
        string((1.2345m).quantize(0.05m)) => '1.23'
        string((1.2355m).quantize(0.001m, 'down')) => '1.235'
        string((1.2355m).quantize(0.001m, 'up')) => '1.236'
        string((1.5m).quantize(1m)) => '2'
        string((1.5m).quantize(1m, 'half_down')) => '1'
        string((1.5m).quantize(1.0m)) => '1.5'
        string((1.50m).quantize(0.1m)) => '1.5'
        string((-1.5m).quantize(0.001m)) => '-1.500'
        string((0m).quantize(0.01m)) => '0.00'
        string((-0m).quantize(0.01m)) => '-0.00'
        string((0.00m).quantize(1m)) => '0'
        (1.5m).quantize(0.001m) == 1.5m => true
        """;

    // A negative place: the value up to tens and hundreds. Equal as numbers on both targets, written differently.
    private const string NegativePlaces = """
        (1234m).round(-2) == 1200m => true
        (1250m).round(-2) == 1200m => true
        (1350m).round(-2) == 1400m => true
        (1250m).round(-2, 'half_up') == 1300m => true
        (1201m).round(-2, 'up') == 1300m => true
        (-1201m).round(-2, 'floor') == -1300m => true
        (1234m).round(-4) == 0m => true
        (1234m).round(-4, 'up') == 10000m => true
        """;

#if NET11_0_OR_GREATER
    // 33 zeros: the 34 digits of the coefficient 10^33.
    private const string Zeros33 = "00000000000" + "00000000000" + "00000000000";

    private const string TargetRows = $$"""
        string((1234m).round(-2)) => '1.2E+3'
        string((1250m).round(-2)) => '1.2E+3'
        string((1234m).round(-4)) => '0E+4'
        string((1.5m).round(-100)) => '0E+100'
        string((-1.5m).round(-100)) => '-0E+100'
        string((1.5m).round(-100, 'up')) => '1E+100'
        string((1.5m).round(-6111, 'up')) => '1E+6111'
        string((1.5m).round(-6112, 'up')) => '1.0E+6112'
        string((1.5m).round(-6144, 'up')) => '1.{{Zeros33}}E+6144'
        string((1.5m).round(-7000)) => '0E+6111'
        string((1.5m).round(-9223372036854775807 - 1)) => '0E+6111'
        string(decimal('12345678901234567890').quantize(decimal('1e-10'))) => '12345678901234567890.0000000000'
        string(decimal('{{DecimalExample.Max34}}').quantize(decimal('1'))) => '{{DecimalExample.Max34}}'
        string(decimal('{{DecimalExample.Max34}}').quantize(decimal('1e1'))) => '1.{{Zeros33}}E+34'
        string(decimal('123456789012345678901234567890123').quantize(decimal('0.1'))) => '123456789012345678901234567890123.0'
        string((1e6111m).quantize(1e6111m)) => '1E+6111'
        string((1.5m).quantize(1e100m)) => '0E+100'
        string((7e100m).quantize(1e100m)) => '7E+100'
        string((8e100m).quantize(1e99m)) => '8.0E+100'
        """;

    private const string TargetErrors = $$"""
        Overflow | (1.5m).round(-9223372036854775807 - 1, 'up') | decimal overflow
        Overflow | (1e30m).quantize(1e-10m) | decimal overflow
        Overflow | decimal('{{DecimalExample.Max34}}').quantize(decimal('0.1')) | decimal overflow
        Overflow | decimal('1234567890123456789012345678901234').quantize(decimal('0.1')) | decimal overflow
        Overflow | decimal('1').quantize(decimal('1e-6176')) | decimal overflow
        Overflow | (1.5m).round(-6145, 'up') | decimal overflow
        """;
#else
    // 28 zeros: the scale 28 of System.Decimal, and the coefficient 10^28 that has 29 digits.
    private const string Zeros28 = "0000000000" + "0000000000" + "00000000";

    private const string TargetRows = $$"""
        string((1234m).round(-2)) => '1200'
        string((1250m).round(-2)) => '1200'
        string((1234m).round(-4)) => '0'
        string((1.5m).round(-100)) => '0'
        string((-1.5m).round(-100)) => '-0'
        string((1.5m).round(-28, 'up')) => '1{{Zeros28}}'
        string((1.5m).round(-9223372036854775807 - 1)) => '0'
        string(decimal('7922816251426433759354395033.5').round(0, 'half_up')) => '7922816251426433759354395034'
        string(decimal('7922816251426433759354395033.5').round(0)) => '7922816251426433759354395034'
        string(decimal('79228162514264337593543950335').quantize(decimal('1'))) => '79228162514264337593543950335'
        string(decimal('7922816251426433759354395033').quantize(decimal('0.1'))) => '7922816251426433759354395033.0'
        string(decimal('1').quantize(decimal('1e-28'))) => '1.{{Zeros28}}'
        string(decimal('0.0000000000000000000000000001').quantize(decimal('1'))) => '0'
        string(decimal('0.0000000000000000000000000001').quantize(decimal('1'), 'up')) => '1'
        string(decimal('-0.0000000000000000000000000001').quantize(decimal('1'))) => '-0'
        """;

    private const string TargetErrors = """
        Overflow | (1.5m).round(-100, 'up') | decimal overflow
        Overflow | (1.5m).round(-29, 'up') | decimal overflow
        Overflow | (1.5m).round(-9223372036854775807 - 1, 'up') | decimal overflow
        Overflow | decimal('12345678901234567890').quantize(decimal('1e-10')) | decimal overflow
        Overflow | decimal('79228162514264337593543950335').quantize(decimal('0.1')) | decimal overflow
        Overflow | decimal('7922816251426433759354395034').quantize(decimal('0.1')) | decimal overflow
        """;
#endif

    private const string ModeErrors = """
        InvalidArgument | (1.5m).round(2, 'banker') | invalid argument to 'round': unknown rounding mode "banker"
        InvalidArgument | (1.5m).round(2, 'HALF_UP') | invalid argument to 'round': unknown rounding mode "HALF_UP"
        InvalidArgument | (1.5m).round(2, '') | invalid argument to 'round': unknown rounding mode ""
        InvalidArgument | (1.5m).round(2, 'half_even ') | invalid argument to 'round': unknown rounding mode "half_even "
        InvalidArgument | (1.5m).round(2, 'half_up_xyz') | invalid argument to 'round': unknown rounding mode "half_up_xyz"
        InvalidArgument | (1.5m).quantize(0.01m, 'banker') | invalid argument to 'quantize': unknown rounding mode "banker"
        InvalidArgument | (1.5m).quantize(0.01m, 'Down') | invalid argument to 'quantize': unknown rounding mode "Down"
        InvalidArgument | (1.5m).round(2, mode) | invalid argument to 'round': unknown rounding mode "banker"
        InvalidArgument | (1.5m).quantize(0.01m, mode) | invalid argument to 'quantize': unknown rounding mode "banker"
        InvalidArgument | (1.5m).round(2, dyn('banker')) | invalid argument to 'round': unknown rounding mode "banker"
        """;

    private const string ModeValues = """
        string((2.5m).round(0, mode)) => '2'
        string((2.5m).round(0, dyn('half_up'))) => '3'
        string((1.2355m).quantize(0.001m, dyn('up'))) => '1.236'
        string(dyn(2.5m).round(0, 'half_up')) => '3'
        string(dyn(1.2355m).quantize(dyn(0.001m), dyn('down'))) => '1.235'
        [2.5m, 3.5m].map(x, x.round(0)) == [2m, 4m] => true
        [2.5m, 3.5m].map(x, x.round(0, 'half_up')) == [3m, 4m] => true
        """;

    private const string NoMatchingOverloads = """
        EvalNoMatchingOverload | dyn(1).round(0)
        EvalNoMatchingOverload | dyn(1.5m).round(dyn('x'))
        EvalNoMatchingOverload | dyn(1.5m).round(dyn(0), dyn(1))
        EvalNoMatchingOverload | dyn(1.5m).quantize(dyn(1))
        EvalNoMatchingOverload | dyn(1).quantize(dyn(1.5m))
        EvalNoMatchingOverload | dyn(1.5m).quantize(dyn(1.5m), dyn(1))
        """;

    private readonly CelEnvironment environment = Builder().Build().GetEnvironmentOrThrow();

    [Fact]
    public void Round_ToANumberOfDigits_IsHalfEvenAndAddsNoZeros() => Assert.Empty(ExtensionRows.CheckValues(environment, HalfEven));

    [Fact]
    public void Round_ByEachMode_RoundsTheTieAndTheRestAsTheModeSays() => Assert.Empty(ExtensionRows.CheckValues(environment, Modes));

    [Fact]
    public void Quantize_GivesTheExponentOfTheQuantum_NotAStep() => Assert.Empty(ExtensionRows.CheckValues(environment, Quantize));

    [Fact]
    public void Round_ToANegativePlace_IsTheSameNumberOnBothTargets() => Assert.Empty(ExtensionRows.CheckValues(environment, NegativePlaces));

    [Fact]
    public void Rounding_AtTheLimitsOfThePlatform_IsTheTextAndTheErrorOfTheTarget() => Assert.Empty(ExtensionRows.CheckValues(environment, TargetRows));

    [Fact]
    public void Rounding_ThatTheTargetCannotHold_IsOverflow() => Assert.Empty(CheckErrors(TargetErrors));

    [Fact]
    public void ModeThatIsNoMode_IsInvalidArgumentNamingTheFunctionAndTheString()
    {
        Assert.Empty(CheckErrors(ModeErrors));

        // A constant that is no mode is not an error of the check: the program is made, and the call refuses it each time.
        CelProgram<CelValue> program = environment.Compile<CelValue>("(1.5m).round(2, 'banker')").GetProgramOrThrow();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            CelEvalResult<CelValue> result = program.Evaluate(new CelBindings().Add("mode", "banker"), cancellationToken: CancellationToken.None);

            Assert.False(result.IsSuccess);
            CelError error = Assert.Single(result.Errors);
            Assert.Equal((CelErrorCode.InvalidArgument, "round", CelErrorStage.Evaluate), (error.Code, error.Subject, error.Stage));
        }
    }

    [Fact]
    public void Mode_OfAVariableOrOfDyn_IsReadAtEvaluation() =>
        Assert.Empty(ExtensionRows.CheckValues(environment, ModeValues, new CelBindings().Add("mode", "half_even")));

    [Fact]
    public void ArgumentOfAnotherKind_IsEvalNoMatchingOverload()
    {
        foreach (string row in ExtensionRows.Lines(NoMatchingOverloads))
        {
            string[] cells = row.Split(" | ");
            CelEvalResult<CelValue> result = environment.Compile<CelValue>(cells[1]).GetProgramOrThrow()
                .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(cells[0], Assert.Single(result.Errors).Code.ToString());
        }
    }

    // Version 1 has no such function, as an environment without the set has no function of it.
    [Theory]
    [InlineData("(1.5m).round(0)")]
    [InlineData("(1.5m).round(0, 'half_up')")]
    [InlineData("(1.5m).quantize(0.1m)")]
    [InlineData("(1.5m).quantize(0.1m, 'down')")]
    public static void VersionOne_HasNeitherRoundNorQuantize(string expression)
    {
        CelEnvironment versionOne = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Decimal(1))
            .EnableDecimalLiterals()
            .Build()
            .GetEnvironmentOrThrow();

        CelCompileResult<CelValue> compiled = versionOne.Compile<CelValue>(expression);

        Assert.False(compiled.IsSuccess);
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(compiled.Errors).Code);
    }

    // The overloads of the set cost 1 each; the literals cost nothing, and a mode that is a variable costs its read.
    [Theory]
    [InlineData("(1.5m).round(0)", 1UL)]
    [InlineData("(1.5m).round(0, 'half_up')", 1UL)]
    [InlineData("(1.5m).quantize(0.1m)", 1UL)]
    [InlineData("(1.5m).quantize(0.1m, 'down')", 1UL)]
    [InlineData("(1.5m).round(0, mode)", 2UL)]
    [InlineData("string((1.5m).round(0))", 3UL)]
    public void Cost_IsOnePerOverload(string expression, ulong cost)
    {
        CelProgram<CelValue> program = environment.Compile<CelValue>(expression).GetProgramOrThrow();
        var bindings = new CelBindings().Add("mode", "half_even");

        Assert.True(program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None).IsSuccess);
        CelEvalResult<CelValue> below = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(below.Errors).Code);
    }

    [Fact]
    public void Round_OnTheValueOfAVariable_IsTheValueOfTheLiteral()
    {
        CelValue result = environment.Compile<CelValue>("string(d.round(1)) + string(d.quantize(0.001m))").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("d", 2.345m), cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.True(result.TryGetString(out string? text));
        Assert.Equal("2.32.345", text);
    }

    private static CelEnvironmentBuilder Builder() =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Decimal(2))
            .EnableDecimalLiterals()
            .AddVariable<string>("mode")
            .AddVariable<decimal>("d");

    // "Code | expression | message": the expression compiles and its evaluation has exactly that error; "-" is no check of the text.
    private List<string> CheckErrors(string rows)
    {
        var failures = new List<string>();
        foreach (string row in ExtensionRows.Lines(rows))
        {
            string[] cells = row.Split(" | ");
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(cells[1]);
            if (!compiled.IsSuccess)
            {
                failures.Add(row + ": compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message);
                continue;
            }

            CelEvalResult<CelValue> result = compiled.GetProgramOrThrow()
                .Evaluate(new CelBindings().Add("mode", "banker").Add("d", 1m), cancellationToken: CancellationToken.None);
            if (result.IsSuccess)
            {
                failures.Add(row + ": a value");
            }
            else if (result.Errors.Length != 1 || result.Errors[0].Code.ToString() != cells[0] || result.Errors[0].Stage != CelErrorStage.Evaluate)
            {
                failures.Add(row + ": errors " + result.Errors.Length.ToString(CultureInfo.InvariantCulture) + " " + result.Errors[0].Code);
            }
            else if (cells[2] != "-" && result.Errors[0].Message != cells[2])
            {
                failures.Add(row + ": message " + result.Errors[0].Message);
            }
        }

        return failures;
    }
}
