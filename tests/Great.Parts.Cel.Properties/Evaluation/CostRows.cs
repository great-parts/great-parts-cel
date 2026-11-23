using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

/// <summary>One row of the cost table that is measured: the text and the bindings of a size, whether it is an error, and the sizes.</summary>
/// <param name="Build">The text and the bindings of the row on a size.</param>
/// <param name="IsError">Whether the row gives an error value on every size.</param>
/// <param name="Sizes">The sizes of the data.</param>
/// <param name="OptimizedOnly">Whether the row is measured only on the optimized build of the library that its calibration is made on.</param>
internal sealed record CostRow(Func<int, (string Text, CelBindings Bindings)> Build, bool IsError, int[] Sizes, bool OptimizedOnly);

/// <summary>The rows of one table, in the order they are added, and the environment they are measured in.</summary>
/// <param name="environment">The environment of the rows.</param>
internal sealed class CostTable(CelEnvironment environment)
{
    private readonly Dictionary<string, CostRow> _rows = new(StringComparer.Ordinal);

    /// <summary>Adds a row to the table.</summary>
    /// <param name="name">The name of the row, which the output of the test shows.</param>
    /// <param name="build">The text and the bindings of the row on a size.</param>
    /// <param name="isError">Whether the row gives an error on every size.</param>
    /// <param name="sizes">The sizes, or <see langword="null"/> for <see cref="CostRows.Sizes"/>.</param>
    /// <param name="optimizedOnly">
    /// Whether the row is skipped on a build of the library that is not optimized or is instrumented by Stryker.NET
    /// (<see cref="CostRows.IsCalibratedBuild"/>): for a row whose calibration is within a few bytes of B.
    /// </param>
    /// <returns>The table, so that the rows are added in one chain.</returns>
    internal CostTable Add(string name, Func<int, (string, CelBindings)> build, bool isError = false, int[]? sizes = null, bool optimizedOnly = false)
    {
        _rows.Add(name, new CostRow(build, isError, sizes ?? CostRows.Sizes, optimizedOnly));
        return this;
    }

    /// <summary>Adds rows whose variable <c>x</c> is a list of the size and that are measured on the sizes of <see cref="CostRows.Sizes"/>.</summary>
    /// <param name="rows">The name of each row and its text.</param>
    /// <returns>The table, so that the rows are added in one chain.</returns>
    internal CostTable AddOverList(params (string Name, string Text)[] rows)
    {
        foreach ((string name, string text) in rows)
        {
            Add(name, size => CostRows.With(text, CostTests.IntList(size)));
        }

        return this;
    }

    /// <summary>The names of the rows, for a theory.</summary>
    /// <returns>The names.</returns>
    internal TheoryData<string> Names() => [.. _rows.Keys];

    /// <summary>Measures the row on every size and on both paths and checks its bytes against its units.</summary>
    /// <param name="row">The name of the row.</param>
    internal void Check(string row) => CostRows.Check(environment, row, _rows[row]);
}

/// <summary>
/// The rows of the extension sets, of time and of protobuf messages (docs/design/tests.md, "Cost and memory of the extension sets"):
/// each row is measured as the rows of <see cref="CostTests"/> are, on the sizes 0, 1, 10, 100 and 1 000 where it grows with the
/// data, and on both paths, through <c>Compile</c> and through <c>Planner.PlanWithoutTypeCheck</c>, so that a function is measured
/// when the checker chose its overload and when the overload is chosen at run time. A row that makes an error is an error on every
/// size and path, and keeps B per unit without the bytes of an error.
/// </summary>
internal static class CostRows
{
    /// <summary>The environment variable that holds the path of the file of the calibration lines, when a run collects them.</summary>
    internal const string CalibrationFileVariable = "CEL_CALIBRATION_FILE";

    /// <summary>The sizes of the data of a row.</summary>
    internal static readonly int[] Sizes = [0, 1, 10, 100, 1000];

    private static readonly object CalibrationLock = new();

    /// <summary>
    /// Whether the library under test is the build that the bytes per unit are calibrated on: optimized (the JIT optimizer is not
    /// disabled by the compiler) and without the code that Stryker.NET injects, <c>MutantControl</c>. Stryker.NET builds the solution
    /// in Debug and compiles the mutated project again with its own code in every branch, so a row of a few bytes of margin
    /// (<c>docs/cost.md</c>) can fail in its initial run, and one failed test of the initial run stops the whole run.
    /// </summary>
    internal static bool IsCalibratedBuild { get; } = IsOptimizedAndNotInstrumented(typeof(Plan).Assembly);

    /// <summary>Builds the environment of a table whose variables <c>x</c> and <c>p</c> are of type <c>dyn</c>, so that the check of a binding charges nothing.</summary>
    /// <param name="builder">The builder with the library, the sets and the types of the table.</param>
    /// <returns>The environment.</returns>
    internal static CelEnvironment Dynamic(CelEnvironmentBuilder builder) => builder
        .AddVariable("x", CelType.Dyn)
        .AddVariable("p", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    /// <summary>
    /// Measures a row on every size and on both paths and checks the bytes against the units. The largest bytes per unit is written
    /// to the output: the calibration of the row.
    /// </summary>
    /// <param name="environment">The environment of the rows.</param>
    /// <param name="row">The name of the row.</param>
    /// <param name="definition">The row.</param>
    internal static void Check(CelEnvironment environment, string row, CostRow definition)
    {
        Assert.SkipWhen(
            definition.OptimizedOnly && !IsCalibratedBuild,
            $"{row}: the bytes per unit of this row are calibrated on the optimized build of the library; this build is not optimized or is instrumented by Stryker.NET.");

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
        Report(string.Create(CultureInfo.InvariantCulture, $"{row}: {largest:0.0} bytes/unit"));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Writes the calibration line of a row to the output of the test and, when the environment variable named by
    /// <see cref="CalibrationFileVariable"/> holds a path, appends it to that file: the numbers of a whole run in one place.
    /// </summary>
    /// <param name="line">The line: the name of the row and its largest bytes per unit.</param>
    internal static void Report(string line)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(line);
        if (System.Environment.GetEnvironmentVariable(CalibrationFileVariable) is { Length: > 0 } path)
        {
            lock (CalibrationLock)
            {
                File.AppendAllText(path, line + System.Environment.NewLine);
            }
        }
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

    private static bool IsOptimizedAndNotInstrumented(Assembly library) =>
        library.GetCustomAttribute<DebuggableAttribute>() is not { IsJITOptimizerDisabled: true }
        && !Array.Exists(library.GetTypes(), static type => type.Name == "MutantControl");

    private static Plan PlanOf(CelEnvironment environment, string text, bool compiled)
    {
        if (compiled)
        {
            return environment.Compile<CelValue>(text).GetProgramOrThrow().Plan;
        }

        CelParseResult parsed = environment.Parse(text);
        Assert.True(parsed.IsSuccess, text);
        return Planner.PlanWithoutTypeCheck(parsed.ParsedExpr!, environment.Declarations, environment.TimeZones);
    }
}
