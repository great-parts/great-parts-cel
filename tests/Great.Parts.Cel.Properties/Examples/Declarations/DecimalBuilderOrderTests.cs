#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Examples.Declarations;

// The order of the calls of the builder when the environment has the set decimal (the property of the order of the calls, written for the
// set decimal and the literal option): the calls AddExtension with the set decimal, EnableDecimalLiterals, AddVariable of the type decimal
// and of an int, and AddExtension with the set optional, give the same environment in every order, with or without repeated calls —
// the same results of a fixed set of texts: compile results, values and errors. The variable of the type decimal is declared by the set,
// so the call that declares it may come before the call that adds the set; the option needs the set, and the literal is there
// whenever both calls are, wherever they are; without the call of the set every order gives the same errors, MissingExtensionDependency
// for the option and TypeNotDeclared for the variable, with the same texts, in the same order. A set whose version does not exist and the
// option give the same two errors in every order. The test makes every order of the calls of one environment (24 orders of four
// calls, 120 of five), which is a case of the property that the generator of calls of the property test draws at random.
[Trait("Requirement", "+R-8")]
[Trait("Requirement", "F-5")]
public sealed class DecimalBuilderOrderTests
{
    private static readonly string[] Texts =
    [
        "price > 1m",
        "price + 1.5m == 4m",
        "string(price + 1.50m) == '4.00'",
        "decimal(count) < price",
        "decimal('1.5') < price",
        "12.34m == 12.340m",
        "100m == 1e2m",
        "type(price) == decimal",
        "[price, 1.5m].size() == 2",
        "price + 1 == price",
        "price / 0m == price",
        "12.5mx",
        "price > 0",
    ];

    private static readonly Action<CelEnvironmentBuilder>[] Calls =
    [
        builder => builder.AddExtension(CelExtensions.Decimal(1)),
        builder => builder.EnableDecimalLiterals(),
        builder => builder.AddVariable("price", CelType.Decimal),
        builder => builder.AddVariable<long>("count"),
        builder => builder.AddExtension(CelExtensions.Optional(2)),
    ];

    private static readonly CelBindings Bindings = new CelBindings()
        .Add("price", CelValue.FromDecimal(Make(25, -1)))
        .Add("count", 2L);

    [Fact]
    public void EveryOrderOfTheCalls_GivesTheSameEnvironment()
    {
        int[] model = [0, 1, 2, 3, 4];
        string[] expected = Snapshot(model);
        Assert.Equal("built", expected[0]);
        Assert.Equal("price > 1m => True", expected[1]);

        var different = new List<string>();
        foreach (int[] order in Permutations(model))
        {
            string[] actual = Snapshot(order);
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                different.Add(string.Join(' ', order) + ": " + string.Join(" | ", actual.Except(expected)));
            }
        }

        Assert.Empty(different);
    }

    // A call made twice, and a set that is added again, change nothing.
    [Fact]
    public void RepeatedCalls_GiveTheSameEnvironment()
    {
        string[] expected = Snapshot([0, 1, 2, 3, 4]);

        Assert.Equal(expected, Snapshot([0, 0, 1, 1, 2, 3, 4]));
        Assert.Equal(expected, Snapshot([4, 3, 2, 1, 0, 0, 4, 1]));
        Assert.Equal(expected, Snapshot([1, 2, 0, 4, 3, 0, 1, 3]));
    }

    // Without the call of the set the option is MissingExtensionDependency and the variable TypeNotDeclared in every order of the other
    // calls, the same texts, no place (the error of a build has none in an expression) and the same order of the errors.
    [Fact]
    public void WithoutTheSet_EveryOrderGivesTheSameBuildErrors()
    {
        string[]? expected = null;
        foreach (int[] order in Permutations([1, 2, 3, 4]))
        {
            var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
            foreach (int index in order)
            {
                Calls[index](builder);
            }

            CelBuildResult result = builder.Build();
            Assert.False(result.IsSuccess);
            string[] actual = [.. result.Errors.Select(error => (int)error.Code + " " + error.Message)];

            expected ??= actual;
            Assert.Equal(expected, actual);
        }

        Assert.Equal(2, expected!.Length);
        Assert.Contains(expected, line => line.StartsWith("4005 the decimal literal option requires extension 'decimal'", StringComparison.Ordinal));
        Assert.Contains(expected, line => line.StartsWith("4008 variable 'price' has type 'decimal'", StringComparison.Ordinal));
    }

    // A set whose version does not exist, with the option: the same two errors, 4004 and 4005, in every order of the two calls.
    [Fact]
    public void SetThatHasNoSuchVersion_EveryOrderGivesTheSameBuildErrors()
    {
        string[]? expected = null;
        foreach (int[] order in Permutations([0, 1]))
        {
            var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
            foreach (int index in order)
            {
                if (index == 0)
                {
                    builder.AddExtension(CelExtensions.Decimal(2));
                }
                else
                {
                    Calls[index](builder);
                }
            }

            CelBuildResult result = builder.Build();
            Assert.False(result.IsSuccess);
            string[] actual = [.. result.Errors.Select(error => (int)error.Code + " " + error.Message)];

            expected ??= actual;
            Assert.Equal(expected, actual);
        }

        Assert.Contains(expected!, line => line.StartsWith("4004 extension 'decimal' has no version 2; available versions: 1", StringComparison.Ordinal));
        Assert.Contains(expected!, line => line.StartsWith("4005 ", StringComparison.Ordinal));
    }

    // What the environment of the calls gives: the result of Build, then each text with its compile errors or its value or error.
    private static string[] Snapshot(int[] order)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        foreach (int index in order)
        {
            Calls[index](builder);
        }

        CelBuildResult result = builder.Build();
        if (!result.IsSuccess)
        {
            return [.. result.Errors.Select(error => (int)error.Code + " " + error.Message)];
        }

        CelEnvironment environment = result.GetEnvironmentOrThrow();
        var lines = new List<string> { "built" };
        foreach (string text in Texts)
        {
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(text);
            if (!compiled.IsSuccess)
            {
                lines.Add(text + " => compile " + string.Join(", ", compiled.Errors.Select(Shown)));
                continue;
            }

            CelEvalResult<CelValue> evaluated = compiled.GetProgramOrThrow().Evaluate(Bindings, cancellationToken: CancellationToken.None);
            lines.Add(text + " => " + (evaluated.IsSuccess ? Shown(evaluated.GetValueOrThrow()) : "eval " + string.Join(", ", evaluated.Errors.Select(Shown))));
        }

        return [.. lines];
    }

    // A decimal of the given coefficient and exponent by the bits of its binary integer encoding (the sign in bit 127, the exponent plus 6176
    // in bits 113 to 126, the coefficient in bits 0 to 112).
    private static Decimal128 Make(ulong coefficient, int exponent) =>
        Decimal128.DecodeBinary(((UInt128)(uint)(exponent + 6176) << 113) | coefficient);

    private static string Shown(CelValue value) =>
        value.TryGetBoolean(out bool flag) ? (flag ? "True" : "False") : value.Kind.ToString();

    private static string Shown(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)error.Code}@{error.Location.Offset} {error.Message}");

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (int index = 0; index < items.Length; index++)
        {
            int[] rest = [.. items.Where((_, position) => position != index)];
            foreach (int[] tail in Permutations(rest))
            {
                yield return [items[index], .. tail];
            }
        }
    }
}
#endif
