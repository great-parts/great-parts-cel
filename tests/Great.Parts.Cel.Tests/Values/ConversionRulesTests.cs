using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The input rows (a .NET value of a simple type to a CEL value) and the result rows (a CEL value to the requested T).
public sealed class ConversionRulesTests
{
    // "<.NET sample> → <CEL type of the row> <value read back>".
    private const string InputRows = """
        long-min → int Int -9223372036854775808
        int-min → int Int -2147483648
        short-min → int Int -32768
        sbyte-min → int Int -128
        nint-minus-one → int Int -1
        ulong-max → uint UInt 18446744073709551615
        uint-max → uint UInt 4294967295
        ushort-max → uint UInt 65535
        byte-max → uint UInt 255
        nuint-max-uint → uint UInt 4294967295
        double-nan → double Double NaN
        float-tenth → double Double 0.10000000149011612
        half-max → double Double 65504
        bool-true → bool Bool True
        string → string String "text"
        string-null → string Null
        guid → string String "0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9"
        bytes-array → bytes Bytes [0102]
        bytes-null → bytes Null
        bytes-memory → bytes Bytes [02]
        """;

    // "<CEL sample> as <T> → <result> | refused".
    private const string ResultRows = """
        bool true as bool → True
        int 42 as long → 42
        int -9223372036854775808 as long → -9223372036854775808
        int 2147483647 as int → 2147483647
        int -2147483648 as int → -2147483648
        int 2147483648 as int → refused
        int -2147483649 as int → refused
        int 1 as ulong → refused
        int 1 as double → refused
        int 1 as bool → refused
        uint 18446744073709551615 as ulong → 18446744073709551615
        uint 4294967295 as uint → 4294967295
        uint 4294967296 as uint → refused
        uint 1 as long → refused
        uint 1 as int → refused
        double 0.5 as double → 0.5
        double 1 as long → refused
        string a as string → "a"
        string true as bool → refused
        bytes as bytes-memory → [0102]
        bytes as bytes-array → [0102]
        bytes as string → refused
        null as string → null
        null as bytes-array → null
        null as long → refused
        null as bytes-memory → refused
        null as bool → refused
        null as value → Null
        list as value → List
        list as string → refused
        """;

    public static TheoryData<string, string> InputRowCases => CaseTable.Parse(InputRows);

    public static TheoryData<string, string> ResultRowCases => CaseTable.Parse(ResultRows);

    [Theory]
    [MemberData(nameof(InputRowCases))]
    public static void InputRow_GivesItsCelType(string sample, string expected)
    {
        (CelType? type, bool converted, CelValue value) = Input(sample);

        Assert.True(converted);
        Assert.Equal(expected, type?.Name + " " + Read(value));
    }

    [Theory]
    [MemberData(nameof(InputRowCases))]
    public static void AddVariable_DeclaresTheTypeOfTheInputRow(string sample, string expected)
    {
        CelEnvironmentBuilder builder = AddVariable(new CelEnvironmentBuilder(CelStandardLibrary.V1), sample);

        CelVariableDecl declaration = Assert.Single(builder.VariableDeclarations);
        Assert.Equal("v", declaration.Name);
        Assert.StartsWith(declaration.Type.Name + " ", expected, StringComparison.Ordinal);
    }

    [Fact]
    public void TypeOutsideTheInputRows_IsNoInputType()
    {
        Assert.Null(ConversionRules.InputType<decimal>());
        Assert.False(ConversionRules.TryFromClr(1m, out CelValue value));
        Assert.Equal(CelValue.Null, value);
        Assert.Null(ConversionRules.InputType<object>());
        Assert.Null(ConversionRules.InputType<CelValue>());
        Assert.Throws<NotImplementedException>(() => new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<DateTime>("v"));
    }

    [Fact]
    public void AddVariable_WithAnInvalidName_Throws()
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);

        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => builder.AddVariable<long>(null!)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => builder.AddVariable<long>("")).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => builder.AddVariable<decimal>(null!)).ParamName);
    }

    [Fact]
    public void BytesInput_IsNotCopied()
    {
        byte[] array = [1, 2, 3];

        Assert.True(ConversionRules.TryFromClr(array, out CelValue value));

        Assert.True(value.TryGetBytes(out ReadOnlyMemory<byte> bytes));
        Assert.True(Unsafe.AreSame(ref array[0], ref MemoryMarshal.GetReference(bytes.Span)));
    }

    [Theory]
    [MemberData(nameof(ResultRowCases))]
    public static void ResultRow_ConvertsOnlyTheValuesOfItsType(string conversion, string expected)
    {
        string[] parts = KeyRulesTests.Split(conversion, " as ");

        Assert.Equal(expected, Result(KeyRulesTests.Value(parts[0]), parts[1]));
    }

    [Fact]
    public void BytesResult_AsMemoryIsNotCopiedAndAsArrayIsACopy()
    {
        byte[] array = [1, 2];
        CelValue value = CelValue.FromBytes(array);

        Assert.True(ConversionRules.TryToClr(value, out ReadOnlyMemory<byte> memory));
        Assert.True(ConversionRules.TryToClr(value, out byte[]? copy));

        Assert.True(Unsafe.AreSame(ref array[0], ref MemoryMarshal.GetReference(memory.Span)));
        Assert.NotSame(array, copy);
        Assert.Equal(array, copy);
    }

    [Fact]
    public void ValueResult_IsTheValueAsItIs()
    {
        CelList list = CelList.Create();

        Assert.True(ConversionRules.TryToClr(CelValue.FromList(list), out CelValue result));

        Assert.True(result.TryGetList(out CelList? read));
        Assert.Same(list, read);
    }

    [Fact]
    public void IsResultType_IsTrueOnlyForTheTypesOfTheRows()
    {
        bool[] rows =
        [
            ConversionRules.IsResultType<bool>(), ConversionRules.IsResultType<long>(), ConversionRules.IsResultType<int>(),
            ConversionRules.IsResultType<ulong>(), ConversionRules.IsResultType<uint>(), ConversionRules.IsResultType<double>(),
            ConversionRules.IsResultType<string>(), ConversionRules.IsResultType<ReadOnlyMemory<byte>>(),
            ConversionRules.IsResultType<byte[]>(), ConversionRules.IsResultType<CelValue>(),
        ];
        bool[] others =
        [
            ConversionRules.IsResultType<short>(), ConversionRules.IsResultType<float>(), ConversionRules.IsResultType<object>(),
            ConversionRules.IsResultType<decimal>(), ConversionRules.IsResultType<long?>(), ConversionRules.IsResultType<CelList>(),
        ];

        Assert.All(rows, Assert.True);
        Assert.All(others, Assert.False);
    }

    [Fact]
    public void ResultConversionError_NamesBothTypesAtTheRoot()
    {
        CelError error = ConversionRules.ResultConversionError<long>(CelValue.FromUInt64(1), new CelSourceLocation(2, 1, 3));
        CelError bytes = ConversionRules.ResultConversionError<ReadOnlyMemory<byte>>(CelValue.Null, default);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal((2, 1, 3), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Equal("result of type 'uint' cannot be converted to .NET type 'System.Int64'", error.Message);
        Assert.Equal("result of type 'null_type' cannot be converted to .NET type 'System.ReadOnlyMemory<System.Byte>'", bytes.Message);
        Assert.Null(error.SourceLocation);
        Assert.Null(error.Exception);
    }

    private static (CelType? Type, bool Converted, CelValue Value) Input(string sample) => sample switch
    {
        "long-min" => Row(long.MinValue),
        "int-min" => Row(int.MinValue),
        "short-min" => Row(short.MinValue),
        "sbyte-min" => Row(sbyte.MinValue),
        "nint-minus-one" => Row((nint)(-1)),
        "ulong-max" => Row(ulong.MaxValue),
        "uint-max" => Row(uint.MaxValue),
        "ushort-max" => Row(ushort.MaxValue),
        "byte-max" => Row(byte.MaxValue),
        "nuint-max-uint" => Row((nuint)uint.MaxValue),
        "double-nan" => Row(double.NaN),
        "float-tenth" => Row(0.1f),
        "half-max" => Row(Half.MaxValue),
        "bool-true" => Row(true),
        "string" => Row("text"),
        "string-null" => Row((string?)null),
        "guid" => Row(new Guid("0A1B2C3D-4E5F-6071-8293-A4B5C6D7E8F9")),
        "bytes-array" => Row(new byte[] { 1, 2 }),
        "bytes-null" => Row((byte[]?)null),
        "bytes-memory" => Row<ReadOnlyMemory<byte>>(new byte[] { 1, 2 }.AsMemory(1, 1)),
        _ => throw new ArgumentOutOfRangeException(nameof(sample), sample, null),
    };

    // The variable "v" of the .NET type of the sample.
    private static CelEnvironmentBuilder AddVariable(CelEnvironmentBuilder builder, string sample) => sample switch
    {
        "long-min" => builder.AddVariable<long>("v"),
        "int-min" => builder.AddVariable<int>("v"),
        "short-min" => builder.AddVariable<short>("v"),
        "sbyte-min" => builder.AddVariable<sbyte>("v"),
        "nint-minus-one" => builder.AddVariable<nint>("v"),
        "ulong-max" => builder.AddVariable<ulong>("v"),
        "uint-max" => builder.AddVariable<uint>("v"),
        "ushort-max" => builder.AddVariable<ushort>("v"),
        "byte-max" => builder.AddVariable<byte>("v"),
        "nuint-max-uint" => builder.AddVariable<nuint>("v"),
        "double-nan" => builder.AddVariable<double>("v"),
        "float-tenth" => builder.AddVariable<float>("v"),
        "half-max" => builder.AddVariable<Half>("v"),
        "bool-true" => builder.AddVariable<bool>("v"),
        "string" or "string-null" => builder.AddVariable<string>("v"),
        "guid" => builder.AddVariable<Guid>("v"),
        "bytes-memory" => builder.AddVariable<ReadOnlyMemory<byte>>("v"),
        _ => builder.AddVariable<byte[]>("v"),
    };

    private static (CelType? Type, bool Converted, CelValue Value) Row<T>(T value)
    {
        bool converted = ConversionRules.TryFromClr(value, out CelValue result);
        return (ConversionRules.InputType<T>(), converted, result);
    }

    // The result of the conversion to the T that <type> names, as text; "refused" when the row does not convert the value.
    private static string Result(CelValue value, string type) => type switch
    {
        "bool" => Format<bool>(value, result => result ? "True" : "False"),
        "long" => Format<long>(value, result => result.ToString(CultureInfo.InvariantCulture)),
        "int" => Format<int>(value, result => result.ToString(CultureInfo.InvariantCulture)),
        "ulong" => Format<ulong>(value, result => result.ToString(CultureInfo.InvariantCulture)),
        "uint" => Format<uint>(value, result => result.ToString(CultureInfo.InvariantCulture)),
        "double" => Format<double>(value, result => result.ToString("R", CultureInfo.InvariantCulture)),
        "string" => Format<string?>(value, result => result is null ? "null" : "\"" + result + "\""),
        "bytes-memory" => Format<ReadOnlyMemory<byte>>(value, result => "[" + Convert.ToHexString(result.Span) + "]"),
        "bytes-array" => Format<byte[]?>(value, result => result is null ? "null" : "[" + Convert.ToHexString(result) + "]"),
        _ => Format<CelValue>(value, result => result.Kind.ToString()),
    };

    private static string Format<T>(CelValue value, Func<T?, string> format)
    {
        bool converted = ConversionRules.TryToClr(value, out T? result);
        return converted ? format(result) : "refused";
    }

    // "<kind> <value>" as the result rows read it back: the bits of a double in the shortest round-trip form.
    private static string Read(CelValue value)
    {
        string text = value.Kind switch
        {
            CelValueKind.Int when value.TryGetInt64(out long i) => i.ToString(CultureInfo.InvariantCulture),
            CelValueKind.UInt when value.TryGetUInt64(out ulong u) => u.ToString(CultureInfo.InvariantCulture),
            CelValueKind.Double when value.TryGetDouble(out double d) => d.ToString("R", CultureInfo.InvariantCulture),
            CelValueKind.Bool when value.TryGetBoolean(out bool b) => b ? "True" : "False",
            CelValueKind.String when value.TryGetString(out string? s) => "\"" + s + "\"",
            CelValueKind.Bytes when value.TryGetBytes(out ReadOnlyMemory<byte> m) => "[" + Convert.ToHexString(m.Span) + "]",
            _ => "",
        };
        return text.Length == 0 ? value.Kind.ToString() : value.Kind + " " + text;
    }
}
