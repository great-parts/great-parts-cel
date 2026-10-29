using System;
using System.Collections.Generic;
using System.Text;
using Cel.Expr;
using CsCheck;
using Google.Protobuf;
using NullValue = Google.Protobuf.WellKnownTypes.NullValue;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// Values of the profile: the values of literals and of bindings. Numbers favour boundaries — 0, ±1, the limits of
/// <c>int</c> and <c>uint</c>, powers of two; a bound <c>double</c> is also NaN, ±infinity or -0.0, while a literal is
/// finite. Strings have at most <see cref="ProfileTables.MaxCodePoints"/> code points, bytes at most <see cref="ProfileTables.MaxBytes"/> bytes.
/// </summary>
internal static class ProfileValues
{
    // A double binding is one of SpecialDoubles with a chance of one in this.
    private const int SpecialDoubleOneIn = 5;

    // The boundaries of double that only bindings take: NaN and the infinities, which no literal has, and -0.0.
    private static readonly double[] SpecialDoubles = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.0];

    // The code points that escapes treat specially: the character escapes and the quotes.
    private static readonly int[] SpecialCodePoints = [0x07, 0x08, 0x0C, 0x0A, 0x0D, 0x09, 0x0B, '\\', '\'', '"', '`', '?'];

    /// <summary>An <c>int</c> value.</summary>
    /// <param name="pcg">The random source.</param>
    internal static long IntValue(PCG pcg) => pcg.Below(4) switch
    {
        0 => pcg.Pick<long>([0, 1, -1, long.MaxValue, long.MinValue, long.MaxValue - 1, long.MinValue + 1]),
        1 => (pcg.OneIn(2) ? -1 : 1) * (1L << pcg.Below(63)),
        2 => pcg.Below(201) - 100,
        _ => (long)pcg.Next64(),
    };

    /// <summary>A <c>uint</c> value.</summary>
    /// <param name="pcg">The random source.</param>
    internal static ulong UintValue(PCG pcg) => pcg.Below(4) switch
    {
        0 => pcg.Pick<ulong>([0, 1, ulong.MaxValue, ulong.MaxValue - 1, long.MaxValue, (ulong)long.MaxValue + 1]),
        1 => 1UL << pcg.Below(64),
        2 => (ulong)pcg.Below(201),
        _ => pcg.Next64(),
    };

    /// <summary>A finite <c>double</c> value: the value of a literal, and most values of bindings.</summary>
    /// <param name="pcg">The random source.</param>
    internal static double DoubleValue(PCG pcg)
    {
        double magnitude = pcg.Below(4) switch
        {
            0 => pcg.Pick<double>([0, 1, long.MaxValue, ulong.MaxValue, double.MaxValue, double.Epsilon]),
            1 => Math.ScaleB(1, pcg.Below(2098) - 1074),
            2 => pcg.Below(100_001) / Math.Pow(10, pcg.Below(6)),
            _ => FiniteBits(pcg),
        };
        return pcg.OneIn(2) ? -magnitude : magnitude;
    }

    /// <summary>A <c>string</c> value of Unicode scalar values.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string StringValue(PCG pcg)
    {
        StringBuilder text = new();
        int count = pcg.Below(ProfileTables.MaxCodePoints + 1);
        for (int i = 0; i < count; i++)
        {
            text.Append(char.ConvertFromUtf32(CodePoint(pcg)));
        }

        return text.ToString();
    }

    /// <summary>A <c>bytes</c> value; it often holds the UTF-8 bytes of code points, which a literal may print as themselves.</summary>
    /// <param name="pcg">The random source.</param>
    internal static byte[] BytesValue(PCG pcg)
    {
        List<byte> bytes = [];
        int target = pcg.Below(ProfileTables.MaxBytes + 1);
        while (bytes.Count < target)
        {
            if (pcg.OneIn(3))
            {
                byte[] encoded = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(CodePoint(pcg)));
                if (bytes.Count + encoded.Length <= ProfileTables.MaxBytes)
                {
                    bytes.AddRange(encoded);
                }
            }
            else
            {
                bytes.Add(pcg.OneIn(2) ? (byte)pcg.Below(256) : (byte)CodePoint(pcg, ascii: true));
            }
        }

        return [.. bytes];
    }

    /// <summary>A binding of a variable: a value of its type, a value of any scalar kind or <c>null</c> for <c>dyn</c>.</summary>
    /// <param name="pcg">The random source.</param>
    /// <param name="type">The type of the variable.</param>
    internal static Value Binding(PCG pcg, ProfileType type) => type switch
    {
        ProfileType.IntType => new Value { Int64Value = IntValue(pcg) },
        ProfileType.UintType => new Value { Uint64Value = UintValue(pcg) },
        ProfileType.DoubleType => new Value { DoubleValue = BoundDoubleValue(pcg) },
        ProfileType.BoolType => new Value { BoolValue = pcg.OneIn(2) },
        ProfileType.StringType => new Value { StringValue = StringValue(pcg) },
        ProfileType.BytesType => new Value { BytesValue = ByteString.CopyFrom(BytesValue(pcg)) },
        ProfileType.NullType => new Value { NullValue = NullValue.NullValue },
        ProfileType.DynType => Binding(pcg, pcg.Pick(ProfileTables.DynKinds)),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No variable has this type."),
    };

    // The value of a double binding: a finite value, or sometimes NaN, ±infinity or -0.0.
    private static double BoundDoubleValue(PCG pcg) =>
        pcg.OneIn(SpecialDoubleOneIn) ? pcg.Pick(SpecialDoubles) : DoubleValue(pcg);

    private static double FiniteBits(PCG pcg)
    {
        double value = BitConverter.UInt64BitsToDouble(pcg.Next64() & long.MaxValue);
        return double.IsFinite(value) ? value : double.MaxValue;
    }

    private static int CodePoint(PCG pcg, bool ascii = false)
    {
        int kind = ascii ? pcg.Below(3) : pcg.Below(7);
        return kind switch
        {
            0 => 0x20 + pcg.Below(0x5F),
            1 => SpecialCodePoints[pcg.Below(SpecialCodePoints.Length)],
            2 => pcg.OneIn(2) ? pcg.Below(0x20) : 0x7F,
            3 => 0x80 + pcg.Below(0x80),
            4 or 5 => BmpCodePoint(pcg),
            _ => 0x10000 + pcg.Below(0x100000),
        };
    }

    // A code point of the Basic Multilingual Plane above U+00FF that is not a surrogate.
    private static int BmpCodePoint(PCG pcg)
    {
        int codePoint = 0x100 + pcg.Below(0xFF00 - 0x800);
        return codePoint >= 0xD800 ? codePoint + 0x800 : codePoint;
    }
}
