using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Tests.Syntax;
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

    private sealed class ArrayMemoryManager(byte[] bytes) : MemoryManager<byte>
    {
        public override Span<byte> GetSpan() => bytes;

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
