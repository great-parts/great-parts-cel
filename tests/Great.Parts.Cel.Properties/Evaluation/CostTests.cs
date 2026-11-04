using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

// The cost limit bounds memory too (docs/design/tests.md, "Properties of evaluation"): with the limit ulong.MaxValue the bytes
// allocated beyond the frame are at most the charged units times B. The bytes are counted from after CreateFrame to the end of
// Run, after a first evaluation, so that one-time initialization is not counted. Under the JIT the runtime itself now and then
// allocates on the thread of the evaluation while it moves methods between tiers, a few kilobytes in one evaluation and not in
// the next; an evaluation allocates the same bytes every time, so the count is the smallest of three evaluations.
public sealed class CostTests
{
    // Bytes per unit.
    private const ulong B = 64;
    private const ulong Seed = 20261109;
    private const int Cases = 1500;

    // The evaluations after the first whose bytes are counted.
    private const int Counted = 3;

    private static readonly int[] Sizes = [0, 1, 10, 100, 1000];

    // The cost limits that stop map and filter over a list of 1 000 elements early in the loop.
    private static readonly ulong[] Limits = [2, 3, 10, 100];

    // The bytes of the stop itself, whatever the data: the exception that leaves the evaluation (its stack trace grows with the
    // depth of the nodes) and the error value. A program that allocates nothing from data, size(x) + size(x) + size(x) + size(x)
    // with the limits 1 and 2, allocates 1 376 bytes; the largest count in a row below is 1 408.
    private const ulong StopBytes = 2048;

    // The variables of the rows are of type dyn, so that the check of a binding charges nothing. The application functions
    // of the rows of copies return the count or the length of their argument without reading its elements.
    private static readonly CelEnvironment RowEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("x", CelType.Dyn)
        .AddVariable("p", CelType.Dyn)
        .AddFunction(new CelFunctionDecl("count_list", CelOverloadDecl.Global("count_list_int", [CelType.ListOf(CelType.Int)], CelType.Int)))
        .AddFunctionImplementation("count_list_int", (IReadOnlyList<int> list) => (long)list.Count)
        .AddFunction(new CelFunctionDecl("count_map", CelOverloadDecl.Global("count_map_string_int", [CelType.MapOf(CelType.String, CelType.Int)], CelType.Int)))
        .AddFunctionImplementation("count_map_string_int", (IReadOnlyDictionary<string, int> map) => (long)map.Count)
        .AddFunction("length", (byte[] bytes) => (long)bytes.Length)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "R-3")]
    public void AllocationWithinCost() =>
        PropertyLoop.Check(nameof(AllocationWithinCost), TypedExpressions.WellTyped, Seed, Cases, Violation, testCase => testCase.ToString());

    // One row of the cost table whose memory grows with the data, or a row of an error value, on data of sizes 0, 1,
    // 10, 100 and 1 000. The literals have literal elements, keys and values, which cost 0: the row charges only the literal itself.
    // The largest bytes per unit is written to the output: the calibration of the row.
    [Theory]
    [Trait("Requirement", "R-3")]
    [InlineData("list literal")]
    [InlineData("map literal")]
    [InlineData("add_list")]
    [InlineData("add_string")]
    [InlineData("add_bytes")]
    [InlineData("string_to_bytes")]
    [InlineData("bytes_to_string")]
    [InlineData("int64_to_string")]
    [InlineData("uint64_to_string")]
    [InlineData("double_to_string")]
    [InlineData("bool_to_string")]
    [InlineData("string_to_int64")]
    [InlineData("string_to_uint64")]
    [InlineData("string_to_double")]
    [InlineData("map")]
    [InlineData("filter")]
    [InlineData("contains, pattern over 256")]
    [InlineData("error, 1 / 0")]
    [InlineData("error, int(x)")]
    [InlineData("copy, list(int)")]
    [InlineData("copy, map(string, int)")]
    [InlineData("copy, bytes")]
    public static void Row_AllocatesWithinCost(string row)
    {
        double largest = 0;
        string? failure = null;
        foreach (int size in Sizes)
        {
            (string text, CelBindings bindings) = Row(row, size);
            (long bytes, ulong units, _) = Measure(RowEnvironment.Compile<CelValue>(text).GetProgramOrThrow(), bindings, ulong.MaxValue);
            largest = Math.Max(largest, units == 0 ? bytes : (double)bytes / units);
            if ((ulong)bytes > units * B)
            {
                failure ??= Failure($"{row}, size {size}", bytes, units);
            }
        }

        // The calibration is written before the verdict, so a row over B still gives its number.
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{row}: {largest:0.0} bytes/unit"));
        Assert.True(failure is null, failure);
    }

    // map and filter over a bound list of 1 000 elements, stopped by a cost limit: the loop stops before the operation that
    // would exceed the limit, so the array of the result must not have been allocated for the whole range at the first units.
    // The bytes of an evaluation are at most B per unit charged at the stop and the bytes of the stop itself.
    [Theory]
    [Trait("Requirement", "R-3")]
    [InlineData("map")]
    [InlineData("filter")]
    public static void Limit_StopsAllocationWithinCost(string row)
    {
        string? failure = null;
        double largest = 0;
        foreach (ulong limit in Limits)
        {
            (string text, CelBindings bindings) = Row(row, 1000);
            (long bytes, ulong units, CelValue result) = Measure(RowEnvironment.Compile<CelValue>(text).GetProgramOrThrow(), bindings, limit);
            Assert.True(result.TryGetError(out CelError? error) && error.Code == CelErrorCode.CostLimitExceeded, $"{row}, limit {limit}: the evaluation was not stopped by the limit");
            Assert.True(units >= 1, $"{row}, limit {limit}: no unit charged at the stop");
            largest = Math.Max(largest, (double)bytes / units);
            if ((ulong)bytes > units * B + StopBytes)
            {
                failure ??= Failure($"{row}, limit {limit}", bytes, units);
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{row}, stopped by a limit: {largest:0.0} bytes/unit, with the stop"));
        Assert.True(failure is null, failure);
    }

    private static string? Violation(TypedCase testCase)
    {
        CelCompileResult<CelValue> compiled = TypedExpressions.Environment.Compile<CelValue>(testCase.Text);
        if (!compiled.IsSuccess)
        {
            return $"compile error {compiled.Errors[0].Code}: {compiled.Errors[0].Message}";
        }

        (long bytes, ulong units, _) = Measure(compiled.Program, testCase.Bindings, ulong.MaxValue);
        return (ulong)bytes <= units * B ? null : Failure("case", bytes, units);
    }

    // The smallest bytes allocated by Run in the evaluations after the first, the units it charged (at the stop, when the limit
    // stops it) and its result.
    private static (long Bytes, ulong Units, CelValue Result) Measure(CelProgram<CelValue> program, CelBindings bindings, ulong limit)
    {
        IReadOnlyDictionary<string, CelValue> values = bindings.Values;
        Plan plan = program.Plan;
        _ = plan.Run(plan.CreateFrame(values, limit, CancellationToken.None));
        long bytes = long.MaxValue;
        ulong units = 0;
        CelValue result = default;
        for (int i = 0; i < Counted; i++)
        {
            Frame frame = plan.CreateFrame(values, limit, CancellationToken.None);
            long before = GC.GetAllocatedBytesForCurrentThread();
            result = plan.Run(frame);
            bytes = Math.Min(bytes, GC.GetAllocatedBytesForCurrentThread() - before);
            units = frame.ChargedUnits;
        }

        return (bytes, units, result);
    }

    private static string Failure(string what, long bytes, ulong units) =>
        string.Create(CultureInfo.InvariantCulture, $"{what}: {bytes} bytes for {units} units ({B} per unit)");

    // The text of a row and its data of the size: a literal of the size, or a variable of type dyn bound to data of the size.
    private static (string Text, CelBindings Bindings) Row(string row, int size)
    {
        var bindings = new CelBindings();
        string text = row switch
        {
            "list literal" => "[" + Join("1", size) + "]",
            "map literal" => "{" + Join(index => index.ToString(CultureInfo.InvariantCulture) + ": 1", size) + "}",
            "add_list" => Bound(bindings, "x + x", IntList(size)),
            "add_string" => Bound(bindings, "x + x", CelValue.FromString(new string('a', size))),
            "add_bytes" => Bound(bindings, "x + x", CelValue.FromBytes(new byte[size])),
            "string_to_bytes" => Bound(bindings, "bytes(x)", CelValue.FromString(new string('é', size))),
            "bytes_to_string" => Bound(bindings, "string(x)", CelValue.FromBytes(Ascii(size))),
            "int64_to_string" => Bound(bindings, "string(x)", CelValue.FromInt64(-(long)size * 1_000_000_007L)),
            "uint64_to_string" => Bound(bindings, "string(x)", CelValue.FromUInt64((ulong)size * 1_000_000_007UL)),
            "double_to_string" => Bound(bindings, "string(x)", CelValue.FromDouble(size / 3.0)),
            "bool_to_string" => Bound(bindings, "string(x)", CelValue.FromBoolean(size % 2 == 0)),
            "string_to_int64" => Bound(bindings, "int(x)", CelValue.FromString(Digits(size))),
            "string_to_uint64" => Bound(bindings, "uint(x)", CelValue.FromString(Digits(size))),
            "string_to_double" => Bound(bindings, "double(x)", CelValue.FromString(Digits(size) + ".5")),
            "map" => Bound(bindings, "x.map(v, v + 1)", IntList(size)),
            "filter" => Bound(bindings, "x.filter(v, v >= 0)", IntList(size)),
            "contains, pattern over 256" => Contains(bindings, size),

            // An operation of 1 unit that fails, the same on every size; a text of the size that does not parse, which the
            // message holds.
            "error, 1 / 0" => "1 / 0",
            "error, int(x)" => Bound(bindings, "int(x)", CelValue.FromString(new string('a', size))),

            // The copy of an argument of an application function by a typed delegate.
            "copy, list(int)" => Bound(bindings, "count_list(x)", IntList(size)),
            "copy, map(string, int)" => Bound(bindings, "count_map(x)", StringKeyMap(size)),
            "copy, bytes" => Bound(bindings, "length(x)", CelValue.FromBytes(new byte[size])),
            _ => throw new ArgumentException($"No row {row}.", nameof(row)),
        };

        return (text, bindings);
    }

    private static string Bound(CelBindings bindings, string text, CelValue value)
    {
        bindings.Add("x", value);
        return text;
    }

    private static string Contains(CelBindings bindings, int size)
    {
        bindings.Add("x", CelValue.FromString(new string('a', size)));
        bindings.Add("p", CelValue.FromString(new string('a', 300) + "b"));
        return "x.contains(p)";
    }

    private static CelValue IntList(int size)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromInt64(index);
        }

        return CelValue.FromList(CelList.Create(items));
    }

    // The keys "0", "1", … of the size, each with the value 1.
    private static CelValue StringKeyMap(int size)
    {
        var entries = new KeyValuePair<CelValue, CelValue>[size];
        for (int index = 0; index < size; index++)
        {
            entries[index] = new(CelValue.FromString(index.ToString(CultureInfo.InvariantCulture)), CelValue.FromInt64(1));
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    private static byte[] Ascii(int size)
    {
        byte[] bytes = new byte[size];
        bytes.AsSpan().Fill((byte)'a');
        return bytes;
    }

    // The decimal text of the size, a number that every conversion takes.
    private static string Digits(int size) => size.ToString(CultureInfo.InvariantCulture);

    private static string Join(string element, int count) => Join(_ => element, count);

    private static string Join(Func<int, string> element, int count)
    {
        var parts = new string[count];
        for (int index = 0; index < count; index++)
        {
            parts[index] = element(index);
        }

        return string.Join(", ", parts);
    }
}
