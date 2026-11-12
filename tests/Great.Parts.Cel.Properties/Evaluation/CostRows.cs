using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

/// <summary>One row of the cost table that is measured: the text and the bindings of a size, whether it is an error, and the sizes.</summary>
/// <param name="Build">The text and the bindings of the row on a size.</param>
/// <param name="IsError">Whether the row gives an error value on every size.</param>
/// <param name="Sizes">The sizes of the data.</param>
internal sealed record CostRow(Func<int, (string Text, CelBindings Bindings)> Build, bool IsError, int[] Sizes);

/// <summary>
/// The rows of the extension sets, of time and of protobuf messages (docs/design/tests.md, "Cost and memory of the extension sets"):
/// each row is measured as the rows of <see cref="CostTests"/> are, on the sizes 0, 1, 10, 100 and 1 000 where it grows with the
/// data, and on both paths, through <c>Compile</c> and through <c>Planner.PlanWithoutTypeCheck</c>, so that a function is measured
/// when the checker chose its overload and when the overload is chosen at run time. A row that makes an error is an error on every
/// size and path, and keeps B per unit without the bytes of an error.
/// </summary>
internal static class CostRows
{
    /// <summary>The sizes of the data of a row.</summary>
    internal static readonly int[] Sizes = [0, 1, 10, 100, 1000];

    /// <summary>The names of the rows of a table, for a theory.</summary>
    /// <param name="rows">The table.</param>
    /// <returns>The names.</returns>
    internal static TheoryData<string> Names(Dictionary<string, CostRow> rows)
    {
        var names = new TheoryData<string>();
        foreach (string name in rows.Keys)
        {
            names.Add(name);
        }

        return names;
    }

    /// <summary>Adds a row to a table.</summary>
    /// <param name="rows">The table.</param>
    /// <param name="name">The name of the row, which the output of the test shows.</param>
    /// <param name="build">The text and the bindings of the row on a size.</param>
    /// <param name="isError">Whether the row gives an error on every size.</param>
    /// <param name="sizes">The sizes, or <see langword="null"/> for <see cref="Sizes"/>.</param>
    internal static void Add(Dictionary<string, CostRow> rows, string name, Func<int, (string, CelBindings)> build, bool isError = false, int[]? sizes = null) =>
        rows.Add(name, new CostRow(build, isError, sizes ?? Sizes));

    /// <summary>
    /// Measures a row on every size and on both paths and checks the bytes against the units. The largest bytes per unit is written
    /// to the output: the calibration of the row.
    /// </summary>
    /// <param name="environment">The environment of the rows.</param>
    /// <param name="row">The name of the row.</param>
    /// <param name="definition">The row.</param>
    internal static void Check(CelEnvironment environment, string row, CostRow definition)
    {
        double largest = 0;
        var failures = new List<string>();
        foreach (bool compiled in new[] { true, false })
        {
            foreach (int size in definition.Sizes)
            {
                string where = $"{row}, {(compiled ? "compiled" : "without type check")}, size {size}";
                (string text, CelBindings bindings) = definition.Build(size);
                (long bytes, ulong units, CelValue result) = CostTests.Measure(PlanOf(environment, text, compiled), bindings, ulong.MaxValue);
                bool isError = result.TryGetError(out CelError? error);
                if (isError != definition.IsError)
                {
                    failures.Add($"{where}: `{text}` gave {(isError ? $"the error {error?.Code}: {error?.Message}" : "a value")}, expected {(definition.IsError ? "an error" : "a value")}");
                }

                largest = Math.Max(largest, units == 0 ? bytes : (double)bytes / units);
                if ((ulong)bytes > units * CostTests.B)
                {
                    failures.Add(CostTests.Failure(where, bytes, units));
                }
            }
        }

        // The calibration is written before the verdict, so a row over B still gives its number.
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{row}: {largest:0.0} bytes/unit"));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    internal static (string, CelBindings) With(string text, CelValue x)
    {
        var bindings = new CelBindings();
        bindings.Add("x", x);
        return (text, bindings);
    }

    internal static (string, CelBindings) Two(string text, CelValue x, CelValue p)
    {
        var bindings = new CelBindings();
        bindings.Add("x", x);
        bindings.Add("p", p);
        return (text, bindings);
    }

    internal static CelValue Text(int size) => CelValue.FromString(new string('a', size));

    private static Plan PlanOf(CelEnvironment environment, string text, bool compiled)
    {
        if (compiled)
        {
            return environment.Compile<CelValue>(text).GetProgramOrThrow().Plan;
        }

        ParseResult parsed = environment.Parse(text);
        Assert.True(parsed.IsSuccess, text);
        return Planner.PlanWithoutTypeCheck(parsed.Expression!, environment.Declarations, environment.TimeZones);
    }
}
