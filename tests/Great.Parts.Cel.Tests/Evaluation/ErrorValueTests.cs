using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The error values of evaluation (docs/design/evaluation.md, "Errors as values"): creating one allocates at most 64
// bytes, the message and the position are formed when they are read, and every read gives the same text.
public sealed class ErrorValueTests
{
    // The bound on the bytes of creating an error value.
    private const long Bound = 64;

    private const int Offset = 4;

    private static readonly LineTable Lines = new("0123456789");

    private static readonly CelList Three = CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3));

    private static readonly CelMap OneKey = CelMap.Create([new(CelValue.FromString("a"), CelValue.FromInt64(1))]);

    private static readonly OverloadBinding Application = new(
        "f", new CelOverloadDecl("f_int", false, [CelType.Int], CelType.Int), null, 1, null, "test", isApplication: true);

    private static readonly OverloadBinding ListResult = new(
        "f", new CelOverloadDecl("f_list", false, [], CelType.ListOf(CelType.Int)), null, 1, null, "test", isApplication: true);

    private static readonly InvalidOperationException Thrown = new("failed");

    private enum Wide : ulong
    {
        Top = ulong.MaxValue,
    }

    [Theory]
    [InlineData("Unbound", "no value is bound to variable 'name'")]
    [InlineData("BindingTypeMismatch, kind", "value of variable 'ys' has type 'string', which does not match the declared type 'list(int)'")]
    [InlineData("BindingTypeMismatch, element", "value of variable 'ys' has type 'list(bool)', which does not match the declared type 'list(int)'")]
    [InlineData("AtRead", "18446744073709551615 is out of the range of int")]
    [InlineData("CostLimitExceeded", "evaluation cost limit of 18446744073709551615 exceeded")]
    [InlineData("ApplicationException", "application code threw System.InvalidOperationException in overload 'f_int' of function 'f'")]
    [InlineData("ApplicationException, cost function", "application code threw System.InvalidOperationException in the cost function of overload 'f_int' of function 'f'")]
    [InlineData("ImplementationResultMismatch", "implementation of overload 'f_list' of function 'f' returned a value of type 'list(string)', expected 'list(int)'")]
    [InlineData("NoSuchField", "no such field 'g' in map")]
    [InlineData("FieldSelectionNotSupported", "value of type 'int' does not support field selection of 'g'")]
    [InlineData("InvalidComprehensionRange", "value of type 'int' cannot be the range of a comprehension (must be list or map)")]
    [InlineData("DuplicateMapKey, string", "duplicate map key: \"a\\\"b\\n\"")]
    [InlineData("DuplicateMapKey, int", "duplicate map key: -3")]
    [InlineData("DuplicateMapKey, uint", "duplicate map key: 7u")]
    [InlineData("DuplicateMapKey, bool", "duplicate map key: true")]
    [InlineData("UnsupportedMapKeyType", "unsupported map key type 'double'")]
    [InlineData("InsufficientStack", "insufficient execution stack to continue evaluation")]
    [InlineData("InternalError", "internal error during evaluation; the original exception is in CelError.Exception")]
    [InlineData("Given message", "division by zero")]
    [InlineData("NoMatchingOverload, 16 arguments", "no such overload: f(null_type, bool, int, uint, double, string, bytes, list, map, error, int, int, int, int, int, error)")]
    [InlineData("NoMatchingOverload, 17 arguments", "no such overload: f(int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, bool)")]
    [InlineData("IndexOutOfRange, int", "index 5 out of range [0, 3)")]
    [InlineData("IndexOutOfRange, uint", "index 7 out of range [0, 3)")]
    [InlineData("InvalidArgument, index", "invalid argument to 'f': index 1.5 is not an integer")]
    [InlineData("NoSuchKey, string", "no such key: \"k\"")]
    [InlineData("NoSuchKey, double", "no such key: 1.5")]
    [InlineData("NoSuchKey, list", "no such key: list")]
    [InlineData("InvalidArgument, int", "invalid argument to 'int': cannot parse aaa as int")]
    [InlineData("InvalidArgument, bool", "invalid argument to 'bool': cannot parse maybe as bool")]
    [InlineData("InvalidArgument, UTF-8", "invalid argument to 'string': invalid UTF-8 at byte 1")]
    [InlineData("ConversionRange, text", "99999999999999999999 is out of the range of int")]
    [InlineData("ConversionRange, uint", "18446744073709551615 is out of the range of int")]
    [InlineData("ConversionRange, int", "-1 is out of the range of uint")]
    [InlineData("ConversionRange, double", "1e+300 is out of the range of int")]
    [InlineData("ArgumentConversion", "argument '1' of overload 'narrow_int' of function 'f' cannot be converted from 'int' to .NET type 'System.Int32'")]
    [InlineData("Enumeration above int", "18446744073709551615 is out of the range of int")]
    public static void ErrorValue_AllocatesAtMost64Bytes_AndKeepsItsMessage(string name, string message)
    {
        (Func<CelValue> create, long allowance) = Case(name);
        _ = create();

        long before = GC.GetAllocatedBytesForCurrentThread();
        CelValue value = create();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(bytes <= Bound + allowance, $"{name}: {bytes} bytes");
        Assert.True(value.TryGetError(out CelError? error));
        Assert.Equal((CelErrorStage.Evaluate, message), (error.Stage, error.Message));
        Assert.Equal(name == "Enumeration above int" ? 0 : Offset, error.Location.Offset);
    }

    // Each read forms the text again; the texts are equal.
    [Theory]
    [InlineData("BindingTypeMismatch, element")]
    [InlineData("NoMatchingOverload, 16 arguments")]
    [InlineData("NoSuchKey, string")]
    [InlineData("InvalidArgument, int")]
    public static void Message_ReadTwice_IsTheSame(string name)
    {
        CelError error = ErrorOf(Case(name).Create());

        Assert.Equal(error.Message, error.Message);
        Assert.Equal(error.Location, error.Location);
    }

    // The error is immutable: eight threads that read its message and position at once all get the same.
    [Theory]
    [InlineData("BindingTypeMismatch, element")]
    [InlineData("NoMatchingOverload, 17 arguments")]
    [InlineData("IndexOutOfRange, int")]
    [InlineData("ApplicationException")]
    public static void Message_ReadFromEightThreads_IsTheSame(string name)
    {
        CelError error = ErrorOf(Case(name).Create());
        string expected = error.Message;
        CelSourceLocation location = error.Location;
        const int Threads = 8;
        var results = new (string Message, CelSourceLocation Location)[Threads];
        using var start = new Barrier(Threads);
        var threads = new Thread[Threads];
        for (int i = 0; i < Threads; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                start.SignalAndWait();
                results[index] = (error.Message, error.Location);
            });
            threads[i].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Assert.All(results, result => Assert.Equal((expected, location), result));
    }

    // The error value that evaluation returns has the text of the error of the result.
    [Theory]
    [InlineData("1 / 0")]
    [InlineData("{'a': 1}['k']")]
    [InlineData("int('aaa')")]
    [InlineData("[1, 2][5]")]
    public static void ErrorValueOfTheEvaluation_HasTheTextOfTheResult(string expression)
    {
        CelProgram<CelValue> program = Plans.Environment.Compile<CelValue>(expression).GetProgramOrThrow();

        CelError value = ErrorOf(program.Plan.Evaluate(new Dictionary<string, CelValue>(), null, CancellationToken.None));
        CelError result = Assert.Single(program.Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((result.Code, result.Message, result.Location), (value.Code, value.Message, value.Location));
    }

    // The kinds of the arguments of NoMatchingOverload are packed 4 bits each.
    [Fact]
    public static void EveryValueKind_FitsFourBits()
    {
        Assert.All(Enum.GetValues<CelValueKind>(), kind => Assert.InRange((int)kind, 0, 15));
    }

    private static CelError ErrorOf(CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error));
        return error;
    }

    // The creation of the error value of the case, and the bytes it may allocate beyond the bound.
    private static (Func<CelValue> Create, long Allowance) Case(string name)
    {
        var site = new CallSite("f", Offset, Lines);
        CelValue[] sixteen =
        [
            CelValue.Null, CelValue.FromBoolean(true), CelValue.FromInt64(1), CelValue.FromUInt64(1), CelValue.FromDouble(1),
            CelValue.FromString("s"), CelValue.FromBytes(new byte[] { 1 }), CelValue.FromList(Three), CelValue.FromMap(OneKey),
            site.Error(CelErrorCode.DivisionByZero, "division by zero"), CelValue.FromInt64(1), CelValue.FromInt64(1),
            CelValue.FromInt64(1), CelValue.FromInt64(1), CelValue.FromInt64(1), site.Error(CelErrorCode.DivisionByZero, "division by zero"),
        ];
        CelValue invalidUtf8 = CelValue.FromBytes(new byte[] { 0x61, 0xFF });
        CelValue[] seventeen = new CelValue[17];
        Array.Fill(seventeen, CelValue.FromInt64(1));
        seventeen[16] = CelValue.FromBoolean(true);
        CelValue elements = CelValue.FromList(CelList.Create(CelValue.FromInt64(4), CelValue.FromBoolean(true)));
        CelValue mixed = CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromString("z")));
        var variable = new VariablePlanNode(Offset, 0, "ys", CelType.ListOf(CelType.Int), TypeIndex.Empty);
        var unplaced = new CelError(CelErrorCode.ConversionRange, CelErrorStage.Evaluate, "18446744073709551615 is out of the range of int", default, null, null);
        OverloadImplementation narrow = TypedImplementation.Of<int, int>("narrow_int", value => value);
        return name switch
        {
            "Unbound" => (() => EvaluationErrors.Unbound("name", Lines, Offset), 0),
            "BindingTypeMismatch, kind" => (() => EvaluationErrors.BindingTypeMismatch(variable, CelValue.FromString("x"), Lines, Offset), 0),
            "BindingTypeMismatch, element" => (() => EvaluationErrors.BindingTypeMismatch(variable, elements, Lines, Offset), 0),
            "AtRead" => (() => EvaluationErrors.AtRead(unplaced, Lines, Offset), 0),
            "CostLimitExceeded" => (() => EvaluationErrors.CostLimitExceeded(ulong.MaxValue, Lines, Offset), 0),
            "ApplicationException" => (() => EvaluationErrors.ApplicationException(Thrown, Application, false, Lines, Offset), 0),
            "ApplicationException, cost function" => (() => EvaluationErrors.ApplicationException(Thrown, Application, true, Lines, Offset), 0),
            "ImplementationResultMismatch" => (() => EvaluationErrors.ImplementationResultMismatch(ListResult, mixed, TypeIndex.Empty, Lines, Offset), 0),
            "NoSuchField" => (() => EvaluationErrors.NoSuchField("g", CelValueKind.Map, Lines, Offset), 0),
            "FieldSelectionNotSupported" => (() => EvaluationErrors.FieldSelectionNotSupported(CelValueKind.Int, "g", Lines, Offset), 0),
            "InvalidComprehensionRange" => (() => EvaluationErrors.InvalidComprehensionRange(CelValue.FromInt64(1), TypeIndex.Empty, Lines, Offset), 0),
            "DuplicateMapKey, string" => (() => EvaluationErrors.DuplicateMapKey(CelValue.FromString("a\"b\n"), Lines, Offset), 0),
            "DuplicateMapKey, int" => (() => EvaluationErrors.DuplicateMapKey(CelValue.FromInt64(-3), Lines, Offset), 0),
            "DuplicateMapKey, uint" => (() => EvaluationErrors.DuplicateMapKey(CelValue.FromUInt64(7), Lines, Offset), 0),
            "DuplicateMapKey, bool" => (() => EvaluationErrors.DuplicateMapKey(CelValue.FromBoolean(true), Lines, Offset), 0),
            "UnsupportedMapKeyType" => (() => EvaluationErrors.UnsupportedMapKeyType(CelValue.FromDouble(1.5), TypeIndex.Empty, Lines, Offset), 0),
            "InsufficientStack" => (() => EvaluationErrors.InsufficientStack(Lines, Offset), 0),
            "InternalError" => (() => EvaluationErrors.InternalError(Thrown, Lines, Offset), 0),
            "Given message" => (() => site.Error(CelErrorCode.DivisionByZero, "division by zero"), 0),
            "NoMatchingOverload, 16 arguments" => (() => site.NoMatchingOverload(sixteen), 0),

            // The one allowance: an array of one byte per argument, 24 bytes of header and 17 bytes rounded up to 8.
            "NoMatchingOverload, 17 arguments" => (() => site.NoMatchingOverload(seventeen), 48),
            "IndexOutOfRange, int" => (() => Collections.IndexList([CelValue.FromList(Three), CelValue.FromInt64(5)], in site), 0),
            "IndexOutOfRange, uint" => (() => Collections.IndexList([CelValue.FromList(Three), CelValue.FromUInt64(7)], in site), 0),
            "InvalidArgument, index" => (() => Collections.IndexList([CelValue.FromList(Three), CelValue.FromDouble(1.5)], in site), 0),
            "NoSuchKey, string" => (() => Collections.IndexMap([CelValue.FromMap(OneKey), CelValue.FromString("k")], in site), 0),
            "NoSuchKey, double" => (() => Collections.IndexMap([CelValue.FromMap(OneKey), CelValue.FromDouble(1.5)], in site), 0),
            "NoSuchKey, list" => (() => Collections.IndexMap([CelValue.FromMap(OneKey), CelValue.FromList(Three)], in site), 0),
            "InvalidArgument, int" => (() => Conversions.StringToInt64([CelValue.FromString("aaa")], new CallSite("int", Offset, Lines)), 0),
            "InvalidArgument, bool" => (() => Conversions.StringToBool([CelValue.FromString("maybe")], new CallSite("bool", Offset, Lines)), 0),
            "InvalidArgument, UTF-8" => (() => Conversions.BytesToString([invalidUtf8], new CallSite("string", Offset, Lines)), 0),
            "ConversionRange, text" => (() => Conversions.StringToInt64([CelValue.FromString("99999999999999999999")], in site), 0),
            "ConversionRange, uint" => (() => Conversions.UInt64ToInt64([CelValue.FromUInt64(ulong.MaxValue)], in site), 0),
            "ConversionRange, int" => (() => Conversions.Int64ToUInt64([CelValue.FromInt64(-1)], in site), 0),
            "ConversionRange, double" => (() => Conversions.DoubleToInt64([CelValue.FromDouble(1e300)], in site), 0),
            "ArgumentConversion" => (() => narrow([CelValue.FromInt64(1L << 40)], in site), 0),
            "Enumeration above int" => (() => ConversionRules.TryFromClr(Wide.Top, out CelValue result) ? result : default, 0),
            _ => throw new ArgumentException($"No case {name}.", nameof(name)),
        };
    }
}
