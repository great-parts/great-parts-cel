using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Characters;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Values;

// Lists of DateOnly and dictionaries of Int128 as the type of a variable, of a binding and of a result, and as the member of a type
// that the generator described (Ledger of the sample BigNumbers): the elements are read one by one, so a value outside the range of
// int is ConversionRange at the element and not at the dictionary. Native AOT runs these in its own cell of the CI.
public sealed class DateAndWideCollectionTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Calendar(1))
        .AddVariable<IReadOnlyList<DateOnly>>("days")
        .AddVariable<IReadOnlyDictionary<string, Int128>>("ledger")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-24")]
    public static void ListOfDates_AsABinding_IsReadByElement()
    {
        CelBindings bindings = new CelBindings().Add<IReadOnlyList<DateOnly>>("days", [new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 25)]);

        Assert.True(Evaluate<bool>(Environment, "days.size() == 2 && days[0] < days[1] && days[1] == date('2026-12-25')", bindings));
        Assert.Equal(358, Evaluate<long>(Environment, "days[1] - days[0]", bindings));
        Assert.True(Evaluate<bool>(Environment, "days.all(d, d.getFullYear() == 2026)", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-24")]
    public static void ListOfDates_AsAResult_IsTheListOfDateOnly()
    {
        CelBindings bindings = new CelBindings().Add<IReadOnlyList<DateOnly>>("days", [new DateOnly(2026, 1, 1)]);

        IReadOnlyList<DateOnly> shifted = Evaluate<IReadOnlyList<DateOnly>>(Environment, "days.map(d, d.addDays(30))", bindings);
        IReadOnlyList<DateOnly> literal = Evaluate<IReadOnlyList<DateOnly>>(Environment, "[date(2026, 2, 28), date('0001-01-01'), date('9999-12-31')]", bindings);

        DateOnly[] expectedShifted = [new DateOnly(2026, 1, 31)];
        DateOnly[] expectedLiteral = [new DateOnly(2026, 2, 28), DateOnly.MinValue, DateOnly.MaxValue];

        Assert.Equal(expectedShifted, shifted);
        Assert.Equal(expectedLiteral, literal);
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public static void DictionaryOfInt128_AsABinding_IsReadByElement()
    {
        CelBindings bindings = new CelBindings().Add<IReadOnlyDictionary<string, Int128>>(
            "ledger",
            new Dictionary<string, Int128> { ["a"] = 40, ["b"] = long.MinValue, ["c"] = long.MaxValue });

        Assert.True(Evaluate<bool>(Environment, "ledger['a'] + 2 == 42 && ledger['b'] == -9223372036854775808 && ledger['c'] == 9223372036854775807", bindings));
        Assert.True(Evaluate<bool>(Environment, "ledger.size() == 3 && type(ledger['a']) == int", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public static void DictionaryOfInt128_WithAValueOutsideTheRange_IsConversionRangeAtTheElement()
    {
        CelBindings bindings = new CelBindings().Add<IReadOnlyDictionary<string, Int128>>(
            "ledger",
            new Dictionary<string, Int128> { ["a"] = 1, ["huge"] = Int128.MaxValue, ["low"] = (Int128)long.MinValue - Int128.One });

        Assert.True(Evaluate<bool>(Environment, "ledger['a'] == 1", bindings));
        CelError high = EvaluationError(Environment, "ledger['huge']", bindings);
        CelError low = EvaluationError(Environment, "ledger['low']", bindings);

        Assert.Equal((CelErrorCode.ConversionRange, "170141183460469231731687303715884105727 is out of the range of int"), (high.Code, high.Message));
        Assert.Equal((CelErrorCode.ConversionRange, "-9223372036854775809 is out of the range of int"), (low.Code, low.Message));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public static void DictionaryOfInt128_AsAResult_IsTheDictionary()
    {
        CelBindings bindings = new();

        IReadOnlyDictionary<string, Int128> result = Evaluate<IReadOnlyDictionary<string, Int128>>(
            Environment, "{'a': 1, 'b': -9223372036854775808, 'c': 9223372036854775807}", bindings);

        Assert.Equal(3, result.Count);
        Assert.Equal((Int128)1, result["a"]);
        Assert.Equal((Int128)long.MinValue, result["b"]);
        Assert.Equal((Int128)long.MaxValue, result["c"]);
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public static void DictionaryOfInt128_AsAResultOfAProgram_IsCompiledForTheType()
    {
        CelProgram<IReadOnlyDictionary<string, Int128>> program = Environment.Compile<IReadOnlyDictionary<string, Int128>>("ledger").GetProgramOrThrow();
        CelBindings bindings = new CelBindings().Add<IReadOnlyDictionary<string, Int128>>("ledger", new Dictionary<string, Int128> { ["x"] = 7 });

        IReadOnlyDictionary<string, Int128> result = program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.Equal((Int128)7, Assert.Single(result).Value);
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public static void DictionaryOfInt128_AsAMemberOfAGeneratedType_IsReadByElement()
    {
        CelEnvironment generated = Builder(CharactersContext.Default)
            .AddVariable<BigNumbers>("n")
            .Build()
            .GetEnvironmentOrThrow();
        var numbers = new BigNumbers { Ledger = new Dictionary<string, Int128> { ["a"] = 5, ["huge"] = Int128.MaxValue } };
        CelBindings bindings = new CelBindings().Add("n", numbers);

        Assert.True(Evaluate<bool>(generated, "n.ledger['a'] == 5 && n.ledger.size() == 2", bindings));
        CelError error = EvaluationError(generated, "n.ledger['huge']", bindings);

        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
    }
}
