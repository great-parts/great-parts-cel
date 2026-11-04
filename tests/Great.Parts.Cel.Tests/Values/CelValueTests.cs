using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

public sealed class CelValueTests
{
    [Fact]
    public static void Layout_HasAnExplicitKindIn32Bytes()
    {
        Assert.Equal(32, Unsafe.SizeOf<CelValue>());
    }

    // "<sample> → <kind> <what the TryGet… methods read>": only the method of the kind reads the value; the others
    // give false and their default.
    private const string Samples = """
        default → Null
        null → Null
        false → Bool bool False
        true → Bool bool True
        int-min → Int int -9223372036854775808
        int-max → Int int 9223372036854775807
        uint-max → UInt uint 18446744073709551615
        double-negative-zero → Double double 8000000000000000
        double-nan → Double double 7FF8000000000001
        string-empty → String string ""
        string → String string "a😀"
        bytes-default → Bytes bytes []
        bytes-array → Bytes bytes [0203]
        """;

    public static TheoryData<string, string> SampleCases => CaseTable.Parse(Samples);

    [Theory]
    [MemberData(nameof(SampleCases))]
    public static void Value_IsReadOnlyByTheMethodOfItsKind(string sample, string expected)
    {
        Assert.Equal(expected, ReadAll(Sample(sample)));
    }

    [Fact]
    public static void Bytes_OverAnArray_AreNotCopied()
    {
        byte[] array = [1, 2, 3, 4];

        Assert.True(CelValue.FromBytes(array.AsMemory(1, 2)).TryGetBytes(out ReadOnlyMemory<byte> bytes));

        Assert.Equal(2, bytes.Length);
        Assert.True(Unsafe.AreSame(ref array[1], ref MemoryMarshal.GetReference(bytes.Span)));
    }

    [Fact]
    public static void Bytes_OverAMemoryManager_AreNotCopied()
    {
        byte[] array = [1, 2, 3, 4, 5];
        using var manager = new ArrayMemoryManager(array);

        Assert.True(CelValue.FromBytes(manager.Memory.Slice(2, 2)).TryGetBytes(out ReadOnlyMemory<byte> bytes));

        Assert.Equal("0304", Convert.ToHexString(bytes.Span));
        Assert.True(Unsafe.AreSame(ref array[2], ref MemoryMarshal.GetReference(bytes.Span)));
    }

    [Fact]
    public static void String_IsNotCopied()
    {
        string text = new('x', 3);

        Assert.True(CelValue.FromString(text).TryGetString(out string? read));

        Assert.Same(text, read);
    }

    [Fact]
    public static void FromString_WithNull_Throws()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => CelValue.FromString(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public static void List_IsTheSameInstanceAndReadOnlyByTryGetList()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1));

        CelValue value = CelValue.FromList(list);

        Assert.Equal(CelValueKind.List, value.Kind);
        Assert.True(value.TryGetList(out CelList? read));
        Assert.Same(list, read);
        Assert.Equal("List", ReadReferences(value));
    }

    [Fact]
    public static void Map_IsTheSameInstanceAndReadOnlyByTryGetMap()
    {
        CelMap map = CelMap.Create([]);

        CelValue value = CelValue.FromMap(map);

        Assert.Equal(CelValueKind.Map, value.Kind);
        Assert.True(value.TryGetMap(out CelMap? read));
        Assert.Same(map, read);
        Assert.Equal("Map", ReadReferences(value));
    }

    [Fact]
    public static void Error_IsTheSameInstanceAndReadOnlyByTryGetError()
    {
        var error = new CelError(CelErrorCode.InternalError, CelErrorStage.Evaluate, "failed", default, null, null);

        CelValue value = CelValue.FromError(error);

        Assert.Equal(CelValueKind.Error, value.Kind);
        Assert.True(value.TryGetError(out CelError? read));
        Assert.Same(error, read);
        Assert.Equal("Error", ReadReferences(value));
    }

    [Fact]
    public static void Type_IsTheSameInstanceAndReadOnlyByTryGetType()
    {
        CelType type = CelType.MapOf(CelType.String, CelType.Bool);

        CelValue value = CelValue.FromType(type);

        Assert.Equal(CelValueKind.Type, value.Kind);
        Assert.True(value.TryGetType(out CelType? read));
        Assert.Same(type, read);
        Assert.Equal("", ReadReferences(value));
        Assert.Equal("Type", ReadAll(value));
        Assert.False(CelValue.FromInt64(1).TryGetType(out CelType? none));
        Assert.Null(none);
    }

    [Fact]
    public static void FromType_OfATypeParameterOrNull_Throws()
    {
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => CelValue.FromType(CelType.TypeParameter("T"))).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => CelValue.FromType(null!)).ParamName);
    }

    [Theory]
    [MemberData(nameof(SampleCases))]
    public static void ValueOfAnotherKind_IsNoListMapOrError(string sample, string expected)
    {
        CelValue value = Sample(sample);

        Assert.StartsWith(value.Kind.ToString(), expected, StringComparison.Ordinal);
        Assert.Equal("", ReadReferences(value));
    }

    [Fact]
    public static void FromListAndFromMap_WithNull_Throw()
    {
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => CelValue.FromList(null!)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => CelValue.FromMap(null!)).ParamName);
    }

    // The names of the kinds whose TryGet… method of a reference read the value; the others give false and null.
    private static string ReadReferences(CelValue value)
    {
        var parts = new List<string>();
        if (value.TryGetList(out CelList? list))
        {
            parts.Add("List");
        }
        else
        {
            Assert.Null(list);
        }

        if (value.TryGetMap(out CelMap? map))
        {
            parts.Add("Map");
        }
        else
        {
            Assert.Null(map);
        }

        if (value.TryGetError(out CelError? error))
        {
            parts.Add("Error");
        }
        else
        {
            Assert.Null(error);
        }

        return string.Join(' ', parts);
    }

    private static CelValue Sample(string name) => name switch
    {
        "null" => CelValue.Null,
        "false" => CelValue.FromBoolean(false),
        "true" => CelValue.FromBoolean(true),
        "int-min" => CelValue.FromInt64(long.MinValue),
        "int-max" => CelValue.FromInt64(long.MaxValue),
        "uint-max" => CelValue.FromUInt64(ulong.MaxValue),
        "double-negative-zero" => CelValue.FromDouble(-0.0),
        "double-nan" => CelValue.FromDouble(BitConverter.UInt64BitsToDouble(0x7FF8000000000001)),
        "string-empty" => CelValue.FromString(""),
        "string" => CelValue.FromString("a\U0001F600"),
        "bytes-default" => CelValue.FromBytes(default),
        "bytes-array" => CelValue.FromBytes(new byte[] { 1, 2, 3, 4 }.AsMemory(1, 2)),
        _ => default,
    };

    // The kind, then each TryGet… method that read the value with what it read; a double as its bits.
    private static string ReadAll(CelValue value)
    {
        var parts = new List<string> { value.Kind.ToString() };
        if (value.TryGetBoolean(out bool b))
        {
            parts.Add("bool " + (b ? "True" : "False"));
        }
        else
        {
            Assert.False(b);
        }

        if (value.TryGetInt64(out long i))
        {
            parts.Add("int " + i.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            Assert.Equal(0, i);
        }

        if (value.TryGetUInt64(out ulong u))
        {
            parts.Add("uint " + u.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            Assert.Equal(0UL, u);
        }

        if (value.TryGetDouble(out double d))
        {
            parts.Add("double " + BitConverter.DoubleToUInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture));
        }
        else
        {
            Assert.Equal(0UL, BitConverter.DoubleToUInt64Bits(d));
        }

        if (value.TryGetString(out string? s))
        {
            parts.Add("string \"" + s + "\"");
        }
        else
        {
            Assert.Null(s);
        }

        if (value.TryGetBytes(out ReadOnlyMemory<byte> m))
        {
            parts.Add("bytes [" + Convert.ToHexString(m.Span) + "]");
        }
        else
        {
            Assert.True(m.Equals(default));
        }

        return string.Join(' ', parts);
    }
}
