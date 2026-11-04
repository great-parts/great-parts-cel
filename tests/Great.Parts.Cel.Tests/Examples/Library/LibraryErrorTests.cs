using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The evaluation errors of the standard library of this part, each with its text and place. Each row is
// "expression | code | offset | message"; the expressions are on one line, so the line is 1 and the
// column the offset plus 1. The place of a call is the operator or the "(" after the name; for an index
// it is the "[" of the index. A value in a message is written as string() writes it; a map key as a CEL
// literal of its kind; a string argument as its text. cel-go v0.32.0 gives an error for every row.
// i is 41, u is 7u, max and min are the bounds of int, umax that of uint; dm is dyn and holds {'k': 1}.
public sealed class LibraryErrorTests
{
    private const string Rows = """
        {'a': 1}['b'] | NoSuchKey | 8 | no such key: "b"
        dm[1] | NoSuchKey | 2 | no such key: 1
        {1: 'a'}[dyn(2u)] | NoSuchKey | 8 | no such key: 2u
        {1: 'a'}[dyn(1.5)] | NoSuchKey | 8 | no such key: 1.5
        [1, 2][2] | IndexOutOfRange | 6 | index 2 out of range [0, 2)
        [1, 2][-1] | IndexOutOfRange | 6 | index -1 out of range [0, 2)
        [1][dyn(5u)] | IndexOutOfRange | 3 | index 5 out of range [0, 1)
        [1, 2][dyn(2.0)] | IndexOutOfRange | 6 | index 2 out of range [0, 2)
        [1, 2][dyn(1.5)] | InvalidArgument | 6 | invalid argument to '_[_]': index 1.5 is not an integer
        [1][dyn(double('NaN'))] | InvalidArgument | 3 | invalid argument to '_[_]': index NaN is not an integer
        5u / 0u | DivisionByZero | 3 | division by zero
        i % 0 | ModulusByZero | 2 | modulus by zero
        5u % 0u | ModulusByZero | 3 | modulus by zero
        max * 2 | Overflow | 4 | integer overflow
        min - 1 | Overflow | 4 | integer overflow
        min % -1 | Overflow | 4 | integer overflow
        umax + 1u | Overflow | 5 | unsigned integer overflow
        0u - 1u | Overflow | 3 | unsigned integer overflow
        u - 8u | Overflow | 2 | unsigned integer overflow
        umax * 2u | Overflow | 5 | unsigned integer overflow
        int(1e19) | ConversionRange | 3 | 1e+19 is out of the range of int
        int(-1e19) | ConversionRange | 3 | -1e+19 is out of the range of int
        int(9223372036854775807.0) | ConversionRange | 3 | 9.223372036854776e+18 is out of the range of int
        int(double('NaN')) | ConversionRange | 3 | NaN is out of the range of int
        int(double('inf')) | ConversionRange | 3 | +Inf is out of the range of int
        uint(-1) | ConversionRange | 4 | -1 is out of the range of uint
        uint(-0.5) | ConversionRange | 4 | -0.5 is out of the range of uint
        uint(1.8446744073709552e19) | ConversionRange | 4 | 1.8446744073709552e+19 is out of the range of uint
        int(18446744073709551615u) | ConversionRange | 3 | 18446744073709551615 is out of the range of int
        int('9223372036854775808') | ConversionRange | 3 | 9223372036854775808 is out of the range of int
        uint('18446744073709551616') | ConversionRange | 4 | 18446744073709551616 is out of the range of uint
        double('1e400') | ConversionRange | 6 | 1e400 is out of the range of double
        int('abc') | InvalidArgument | 3 | invalid argument to 'int': cannot parse abc as int
        int('1_000') | InvalidArgument | 3 | invalid argument to 'int': cannot parse 1_000 as int
        uint('+5') | InvalidArgument | 4 | invalid argument to 'uint': cannot parse +5 as uint
        uint('-1') | InvalidArgument | 4 | invalid argument to 'uint': cannot parse -1 as uint
        double('abc') | InvalidArgument | 6 | invalid argument to 'double': cannot parse abc as double
        bool('yes') | InvalidArgument | 4 | invalid argument to 'bool': cannot parse yes as bool
        string(b'\xff') | InvalidArgument | 6 | invalid argument to 'string': invalid UTF-8 at byte 0
        string(b'ab\xff') | InvalidArgument | 6 | invalid argument to 'string': invalid UTF-8 at byte 2
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("i")
        .AddVariable<ulong>("u")
        .AddVariable<long>("max")
        .AddVariable<long>("min")
        .AddVariable<ulong>("umax")
        .AddVariable("dm", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("i", 41L)
        .Add("u", 7UL)
        .Add("max", long.MaxValue)
        .Add("min", long.MinValue)
        .Add("umax", ulong.MaxValue)
        .Add("dm", CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("k"), CelValue.FromInt64(1))])));

    [Fact]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    public void EveryRow_IsItsErrorAtItsPlace()
    {
        var failures = new List<string>();
        foreach (string row in Rows.Split('\n'))
        {
            string[] cells = row.Trim().Split(" | ");
            string? failure = Check(cells[0], Enum.Parse<CelErrorCode>(cells[1]), int.Parse(cells[2], CultureInfo.InvariantCulture), cells[3]);
            if (failure is not null)
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    private string? Check(string expression, CelErrorCode code, int offset, string message)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
        }

        CelEvalResult<CelValue> result = compiled.GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
        if (result.IsSuccess)
        {
            return "no error";
        }

        CelError error = result.Errors[0];
        bool same = result.Errors.Length == 1
            && error.Code == code
            && error.Stage == CelErrorStage.Evaluate
            && error.Message == message
            && (error.Location.Offset, error.Location.Line, error.Location.Column) == (offset, 1, offset + 1);
        return same
            ? null
            : error.Code + " at " + error.Location.Offset.ToString(CultureInfo.InvariantCulture) + ": " + error.Message;
    }
}
