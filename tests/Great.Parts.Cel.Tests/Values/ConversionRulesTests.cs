using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    [Flags]
    private enum Flags
    {
        Two = 2,
        Four = 4,
    }

    private enum Narrow : sbyte
    {
        Low = -2,
    }

    private enum Byteish : byte
    {
        High = 250,
    }

    private enum Short : short
    {
        Minus = -300,
    }

    private enum UShort : ushort
    {
        Big = 60000,
    }

    private enum UnsignedInt : uint
    {
        Large = 4_000_000_000,
    }

    private enum Wide : long
    {
        Five = 5,
    }

    private enum Unsigned : ulong
    {
        AtTheLimit = long.MaxValue,
        Above = (ulong)long.MaxValue + 1,
    }

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
        Assert.Null(ConversionRules.InputType<Int128?>());
        Assert.Null(ConversionRules.InputType<CelValue?>());
        Assert.False(ConversionRules.TryFromClr<Int128?>(null, out _));
        Assert.Equal(CelErrorCode.TypeNotDeclared, Assert.Single(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<Int128>("v").Build().Errors).Code);
        Assert.True(ConversionRules.TryFromClr<object>(new List<int>(), out CelValue collection));
        Assert.Equal(CelValueKind.Object, collection.Kind);
    }

#if !NET11_0_OR_GREATER
    // There is no decimal on .NET 10: a System.Decimal has no row, and in a place of type dyn it is a bare object value.
    [Fact]
    public void SystemDecimalOnNet10_IsNoInputType_AndABoxedOneIsABareObject()
    {
        Assert.Null(ConversionRules.InputType<decimal>());
        Assert.False(ConversionRules.TryFromClr(1m, out CelValue value));
        Assert.Equal(CelValue.Null, value);
        Assert.True(ConversionRules.TryFromClr<object>(1m, out CelValue boxed));
        Assert.Equal(CelValueKind.Object, boxed.Kind);
    }
#endif

    // An enumeration is int by its underlying value of any width, [Flags] included.
    [Fact]
    public void Enumeration_IsIntByItsUnderlyingValue()
    {
        Assert.Equal(CelType.Int, ConversionRules.InputType<Narrow>());
        Assert.Equal(CelType.Int, ConversionRules.InputType<Unsigned>());
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(-2)), Row(Narrow.Low));
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(250)), Row(Byteish.High));
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(4_000_000_000)), Row(UnsignedInt.Large));
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(6)), Row(Flags.Two | Flags.Four));
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(long.MaxValue)), Row(Unsigned.AtTheLimit));
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(5)), Row(Wide.Five));
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(-300)), Row(Short.Minus));
        Assert.Equal((CelType.Int, true, CelValue.FromInt64(60000)), Row(UShort.Big));
    }

    // Above the range of int an enumeration on ulong is the error value ConversionRange, without a place yet.
    [Fact]
    public void UnsignedEnumerationAboveTheRangeOfInt_IsConversionRange()
    {
        (CelValue direct, CelValue boxed, CelValue nullable) = (Row(Unsigned.Above).Value, Row<object>(Unsigned.Above).Value, Row<Unsigned?>(Unsigned.Above).Value);

        Assert.All([direct, boxed, nullable], value =>
        {
            Assert.True(value.TryGetError(out CelError? error));
            Assert.Equal(
                (CelErrorCode.ConversionRange, CelErrorStage.Evaluate, "9223372036854775808 is out of the range of int"),
                (error.Code, error.Stage, error.Message));
        });
    }

    // "<.NET sample> → <CEL type of the row> <value read back>" for the rows of nullable value types.
    private const string NullableRows = """
        long-five → wrapper(int) Int 5
        long-null → wrapper(int) Null
        int-five → wrapper(int) Int 5
        short-five → wrapper(int) Int 5
        sbyte-five → wrapper(int) Int 5
        nint-five → wrapper(int) Int 5
        ulong-five → wrapper(uint) UInt 5
        uint-five → wrapper(uint) UInt 5
        ushort-five → wrapper(uint) UInt 5
        byte-null → wrapper(uint) Null
        nuint-five → wrapper(uint) UInt 5
        double-half → wrapper(double) Double 0.5
        float-half → wrapper(double) Double 0.5
        half-half → wrapper(double) Double 0.5
        bool-false → wrapper(bool) Bool False
        guid → wrapper(string) String "00000000-0000-0000-0000-00000000000a"
        guid-null → wrapper(string) Null
        enum-low → wrapper(int) Int -2
        enum-null → wrapper(int) Null
        """;

    public static TheoryData<string, string> NullableRowCases => CaseTable.Parse(NullableRows);

    [Theory]
    [MemberData(nameof(NullableRowCases))]
    public static void NullableRow_IsTheWrapperOfItsValueType(string sample, string expected)
    {
        (CelType? type, bool converted, CelValue value) = sample switch
        {
            "long-five" => Row<long?>(5),
            "long-null" => Row<long?>(null),
            "int-five" => Row<int?>(5),
            "short-five" => Row<short?>(5),
            "sbyte-five" => Row<sbyte?>(5),
            "nint-five" => Row<nint?>(5),
            "ulong-five" => Row<ulong?>(5),
            "uint-five" => Row<uint?>(5),
            "ushort-five" => Row<ushort?>(5),
            "byte-null" => Row<byte?>(null),
            "nuint-five" => Row<nuint?>(5),
            "double-half" => Row<double?>(0.5),
            "float-half" => Row<float?>(0.5f),
            "half-half" => Row<Half?>((Half)0.5),
            "bool-false" => Row<bool?>(false),
            "guid" => Row<Guid?>(new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10)),
            "guid-null" => Row<Guid?>(null),
            "enum-low" => Row<Narrow?>(Narrow.Low),
            _ => Row<Narrow?>(null),
        };

        Assert.True(converted);
        Assert.Equal(expected, type + " " + Read(value));
    }

    // CelValue and object are dyn, CelList and CelMap a list and a map of dyn; the values are taken as they are, and an
    // object by the row of its runtime type.
    [Fact]
    public void CelValuesAndObjects_AreTakenAsTheyAre()
    {
        CelList list = CelList.Create(CelValue.FromBoolean(true));
        CelMap map = CelMap.Create([new(CelValue.FromInt64(1), CelValue.FromInt64(2))]);

        Assert.Equal((CelType.Dyn, true, CelValue.FromUInt64(3)), Row(CelValue.FromUInt64(3)));
        Assert.Equal((CelType.ListOf(CelType.Dyn), true, CelValue.FromList(list)), Row(list));
        Assert.Equal((CelType.MapOf(CelType.Dyn, CelType.Dyn), true, CelValue.FromMap(map)), Row(map));
        Assert.Equal((CelType.ListOf(CelType.Dyn), true, CelValue.Null), Row<CelList?>(null));
        Assert.Equal((CelType.Dyn, true, CelValue.Null), Row<object?>(null));
        Assert.Equal(CelValue.FromList(list), Row<object>(list).Value);
        Assert.Equal(CelValue.FromMap(map), Row<object>(map).Value);
        Assert.Equal(CelValue.FromString("in"), Row<object>(CelValue.FromString("in")).Value);
    }

    // "<boxed sample> → <value read back>": an object by the row of its runtime type.
    private const string ObjectRows = """
        long → Int -1
        int → Int -1
        short → Int -1
        sbyte → Int -1
        nint → Int -1
        ulong → UInt 1
        uint → UInt 1
        ushort → UInt 1
        byte → UInt 1
        nuint → UInt 1
        double → Double 1.5
        float → Double 1.5
        half → Double 1.5
        bool → Bool True
        string → String "o"
        guid → String "00000000-0000-0000-0000-000000000001"
        bytes-array → Bytes [01]
        bytes-memory → Bytes [02]
        enum → Int 250
        nullable-long → Int 7
        """;

    public static TheoryData<string, string> ObjectRowCases => CaseTable.Parse(ObjectRows);

    [Theory]
    [MemberData(nameof(ObjectRowCases))]
    public static void ObjectRow_IsTheRowOfTheRuntimeType(string sample, string expected)
    {
        object boxed = sample switch
        {
            "long" => -1L,
            "int" => -1,
            "short" => (short)-1,
            "sbyte" => (sbyte)-1,
            "nint" => (nint)(-1),
            "ulong" => 1UL,
            "uint" => 1U,
            "ushort" => (ushort)1,
            "byte" => (byte)1,
            "nuint" => (nuint)1,
            "double" => 1.5,
            "float" => 1.5f,
            "half" => (Half)1.5,
            "bool" => true,
            "string" => "o",
            "guid" => new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            "bytes-array" => new byte[] { 1 },
            "bytes-memory" => new ReadOnlyMemory<byte>([2]),
            "enum" => Byteish.High,
            _ => (long?)7,
        };

        (CelType? type, bool converted, CelValue value) = Row(boxed);

        Assert.Equal((CelType.Dyn, true), (type, converted));
        Assert.Equal(expected, Read(value));
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

    // A copy of an argument is charged first: a map of n pairs 1 + 2n and ⌈L/10⌉ for each string key of length L, also a
    // string as an object; a list of n elements 1 + n; a byte array of L bytes 1 + ⌈L/10⌉, also as a value of a copy. A
    // lazy view and ReadOnlyMemory<byte> are not charged.
    [Fact]
    public static void ParameterCopy_IsChargedByItsFormula()
    {
        CelValue stringKeys = Map(CelValue.FromString("a"), CelValue.FromString("0123456789"), CelValue.FromString("0123456789A"));
        CelValue bytesValue = CelValue.FromMap(CelMap.Create([new(CelValue.FromInt64(1), CelValue.FromBytes("0123456789A"u8.ToArray()))]));

        Assert.Equal(11UL, Charged<IReadOnlyDictionary<string, int>>(stringKeys));
        Assert.Equal(7UL, Charged<IReadOnlyDictionary<int, long>>(IntKeyMap(3)));
        Assert.Equal(5UL, Charged<IReadOnlyDictionary<object, int>>(Map(CelValue.FromString("0123456789A"))));
        Assert.Equal(4UL, Charged<IReadOnlyList<int>>(IntList(3)));
        Assert.Equal(4UL, Charged<byte[]>(CelValue.FromBytes(new byte[25])));
        Assert.Equal(4UL, Charged<object>(CelValue.FromBytes(new byte[25])));
        Assert.Equal(6UL, Charged<IReadOnlyDictionary<int, byte[]>>(bytesValue));
        Assert.Equal(0UL, Charged<ReadOnlyMemory<byte>>(CelValue.FromBytes(new byte[25])));
        Assert.Equal(0UL, Charged<IReadOnlyList<long>>(IntList(3)));
        Assert.Equal(0UL, Charged<IReadOnlyDictionary<string, long>>(stringKeys));
    }

    // A copy of a map keeps the order of the map and finds each key, without an index up to 8 entries and with one above.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(20)]
    public static void MapCopy_KeepsTheOrderAndFindsEachKey(int count)
    {
        Assert.True(ConversionRules.TryToParameter(IntKeyMap(count, descending: true), null, out IReadOnlyDictionary<int, long>? copy));

        int[] keys = new int[count];
        for (int index = 0; index < count; index++)
        {
            keys[index] = count - 1 - index;
            Assert.True(copy!.TryGetValue(keys[index], out long value));
            Assert.Equal(keys[index] + 1L, value);
            Assert.Equal(keys[index] + 1L, copy[keys[index]]);
            Assert.True(copy.ContainsKey(keys[index]));
        }

        Assert.Equal(count, copy!.Count);
        Assert.Equal(keys, copy.Keys);
        Assert.Equal(keys, copy.Select(entry => entry.Key));
        Assert.Equal(keys.Select(key => key + 1L), copy.Values);
        Assert.False(copy.TryGetValue(count, out _));
        Assert.False(copy.ContainsKey(-1));
        Assert.Throws<KeyNotFoundException>(() => copy[count]);
    }

    // The copy of an argument allocates at most 64 bytes per unit of its formula, 1 + 2n for a map whatever the length of
    // its keys, 1 + n for a list and 1 + ⌈L/10⌉ for bytes; an empty map is one instance (see docs/design/tests.md).
    [Fact]
    public static void ParameterCopy_AllocatesAtMost64BytesPerUnit()
    {
        Assert.Equal(0, Allocated<IReadOnlyDictionary<string, int>>(StringKeyMap(0)));
        foreach (int count in (int[])[0, 1, 2, 8, 9, 100, 1000])
        {
            long units = 1 + (2L * count);
            Assert.InRange(Allocated<IReadOnlyDictionary<string, int>>(StringKeyMap(count)), 0, 64 * units);
            Assert.InRange(Allocated<IReadOnlyDictionary<object, int>>(IntKeyMap(count)), 0, 64 * units);
            Assert.InRange(Allocated<IReadOnlyDictionary<int, ReadOnlyMemory<byte>?>>(BytesValueMap(count)), 0, 64 * units);
            Assert.InRange(Allocated<IReadOnlyList<int>>(IntList(count)), 0, 64 * (1L + count));
        }

        foreach (int length in (int[])[0, 1, 10, 1000])
        {
            Assert.InRange(Allocated<byte[]>(CelValue.FromBytes(new byte[length])), 0, 64 * (1L + ((length + 9) / 10)));
        }
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

    // The units that the conversion of an argument to T charges.
    private static ulong Charged<T>(CelValue value)
    {
        var meter = new CountingMeter();
        Assert.True(ConversionRules.TryToParameter(value, meter, out T? _));
        return meter.Total;
    }

    // The smallest bytes that the conversion of an argument to T allocates, of three conversions after the first.
    private static long Allocated<T>(CelValue value)
    {
        var meter = new CountingMeter();
        Assert.True(ConversionRules.TryToParameter(value, meter, out T? _));
        long smallest = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            _ = ConversionRules.TryToParameter(value, meter, out T? _);
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }

    private static CelValue Map(params CelValue[] keys)
    {
        var entries = new KeyValuePair<CelValue, CelValue>[keys.Length];
        for (int index = 0; index < keys.Length; index++)
        {
            entries[index] = new(keys[index], CelValue.FromInt64(1));
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    // The keys "0", "1", … of the size, each with the value 1.
    private static CelValue StringKeyMap(int count)
    {
        var keys = new CelValue[count];
        for (int index = 0; index < count; index++)
        {
            keys[index] = CelValue.FromString(index.ToString(CultureInfo.InvariantCulture));
        }

        return Map(keys);
    }

    // The keys 0 to count - 1, or count - 1 to 0, each with the value key + 1.
    private static CelValue IntKeyMap(int count, bool descending = false)
    {
        var entries = new KeyValuePair<CelValue, CelValue>[count];
        for (int index = 0; index < count; index++)
        {
            long key = descending ? count - 1 - index : index;
            entries[index] = new(CelValue.FromInt64(key), CelValue.FromInt64(key + 1));
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    // The keys 0 to count - 1, each with a bytes value, the widest entry of a copy.
    private static CelValue BytesValueMap(int count)
    {
        CelValue bytes = CelValue.FromBytes(new byte[] { 1 });
        var entries = new KeyValuePair<CelValue, CelValue>[count];
        for (int index = 0; index < count; index++)
        {
            entries[index] = new(CelValue.FromInt64(index), bytes);
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    private static CelValue IntList(int count)
    {
        var items = new CelValue[count];
        for (int index = 0; index < count; index++)
        {
            items[index] = CelValue.FromInt64(index);
        }

        return CelValue.FromList(CelList.Create(items));
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

    // A meter without a limit that only adds the units, so that it allocates nothing.
    private sealed class CountingMeter : IEvaluationMeter
    {
        internal ulong Total { get; private set; }

        public void Charge(ulong units) => Total += units;

        public void Event()
        {
        }
    }
}
