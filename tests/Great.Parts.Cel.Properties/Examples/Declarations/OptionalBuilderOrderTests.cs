using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Examples.Declarations;

// The order of the calls of the builder when the environment has the set optional (the property of the order of the
// calls, written for the set optional): the calls AddExtension with the set optional and with the sets bindings and lists,
// and AddVariable of the type optional_type(int) and of a map, give the same environment in every order, with or without
// repeated calls — the same results of a fixed set of texts: compile results, values and errors. The variable of an optional
// type is declared by the set, so the call that declares it may come before the call that adds the set, and the syntax of the set
// (.?, [?, ? before an element) and its macros are there whenever the call of the set is, wherever it is; without the call of
// the set every order gives the same errors, TypeNotDeclared at Build for the variable, in the same order and with the same
// texts. The test makes every order of the calls of one environment (120 orders of five calls), which is a case of the property
// that the generator of calls of the property test draws at random.
[Trait("Requirement", "+R-8")]
[Trait("Requirement", "F-3")]
public sealed class OptionalBuilderOrderTests
{
    private static readonly string[] Texts =
    [
        "o.hasValue()",
        "o.orValue(0) == 5",
        "o.value() == 5",
        "m.?a.orValue(0) == 1",
        "m[?'b'].orValue(-1) == -1",
        "[1, ?o, ?optional.none()].size() == 2",
        "{?'k': o}.size() == 1",
        "o.optMap(x, x * 2).value() == 10",
        "o.optFlatMap(x, optional.of(x)).hasValue()",
        "cel.bind(v, o, v.hasValue())",
        "[3, 1, 2].sort().first().orValue(0) == 1",
        "[1].last().optFlatMap(x, optional.of(x)) == optional.of(1)",
        "[1, 2].map(x, optional.of(x)).unwrapOpt().size() == 2",
        "optional.none().value() == 1",
        "o.optMap(1, 2) == o",
        "m.?a",
    ];

    private static readonly Action<CelEnvironmentBuilder>[] Calls =
    [
        builder => builder.AddExtension(CelExtensions.Optional(2)),
        builder => builder.AddVariable("o", CelType.OptionalOf(CelType.Int)),
        builder => builder.AddVariable("m", CelType.MapOf(CelType.String, CelType.Int)),
        builder => builder.AddExtension(CelExtensions.Bindings(1)),
        builder => builder.AddExtension(CelExtensions.Lists(4)),
    ];

    private static readonly CelBindings Bindings = new CelBindings()
        .Add("o", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(5))))
        .Add("m", CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromInt64(1))]));

    [Fact]
    public void EveryOrderOfTheCalls_GivesTheSameEnvironment()
    {
        int[] model = [0, 1, 2, 3, 4];
        string[] expected = Snapshot(model);
        Assert.Equal("built", expected[0]);
        Assert.Equal("o.hasValue() => True", expected[1]);

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

        Assert.Equal(expected, Snapshot([0, 0, 1, 2, 3, 4]));
        Assert.Equal(expected, Snapshot([4, 3, 2, 1, 0, 0, 4, 1]));
        Assert.Equal(expected, Snapshot([1, 0, 2, 4, 3, 0, 0, 3]));
    }

    // Without the call of the set the variable o is TypeNotDeclared in every order of the other calls, the same text, the same
    // place (none: the error of a build has no place in an expression) and the same order of the errors.
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

        Assert.Equal(CelErrorCode.TypeNotDeclared, (CelErrorCode)int.Parse(expected![0].Split(' ')[0], CultureInfo.InvariantCulture));
        Assert.StartsWith("4008 variable 'o' has type 'optional_type", expected[0], StringComparison.Ordinal);
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
