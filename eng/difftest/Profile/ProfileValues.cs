using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    // Texts of conversions to int and uint: signs, spaces, _, prefixes, a suffix, the empty string, and numbers out of range.
    private static readonly string[] IntegerTexts = ["0", "-0", "+5", "-12", "007", "1_000", "0x10", "1u", " 1", "1 ", "", "-", "1e3", "99999999", "-9999999", "1.0"];

    // Texts of conversions to double: the forms of the standard library page and near them.
    private static readonly string[] DoubleTexts =
    [
        "1.5", "-0.0", "1e3", "1E-3", "1_000.5", ".5", "5.", "0x1p-2", "0x1.8p1", "0x10", "inf", "-inf", "+Inf",
        "Infinity", "infinity", "nan", "NaN", "-nan", " 1", "1 ", "", "1e400", "-1e400", "1e-400", "1__0", "_1", "1_",
    ];

    // Texts of the conversion to bool: the twelve strings of the standard library page and near them.
    private static readonly string[] BoolTexts =
        ["1", "t", "T", "TRUE", "true", "True", "0", "f", "F", "FALSE", "false", "False", "tRUE", "yes", " true", ""];

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

    /// <summary>
    /// A binding of a variable of the manifest. A typed list gets 0–3 elements of its element type, <c>list&lt;dyn&gt;</c> scalars
    /// and <c>null</c>; a typed map gets 0–3 entries with distinct keys — <c>map&lt;string, …&gt;</c> the names of
    /// <see cref="ProfileTables.FieldNames"/>, so selections and presence tests find them — and values of its value type; a
    /// wrapper gets a value or, one time in four, <c>null</c>. Other variables are bound as by <see cref="Binding(PCG, ProfileType)"/>.
    /// </summary>
    /// <param name="pcg">The random source.</param>
    /// <param name="variable">The variable.</param>
    internal static Value Binding(PCG pcg, ProfileVariable variable) => variable switch
    {
        { Wrapper: true } when pcg.OneIn(4) => new Value { NullValue = NullValue.NullValue },
        { Type: ProfileType.ListType, Declared.Element: { } element } => TypedListBinding(pcg, element),
        { Type: ProfileType.MapType, Declared: { Key: { } key, Value: { } value } } => TypedMapBinding(pcg, key, value),
        _ => Binding(pcg, variable.Type),
    };

    /// <summary>
    /// A binding of a variable: a value of its type; for <c>dyn</c> a scalar, <c>null</c>, a list of 0–3 scalars or a map of
    /// 0–3 entries whose keys are as in map literals and whose values are scalars.
    /// </summary>
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
        ProfileType.ListType => ListBinding(pcg),
        ProfileType.MapType => MapBinding(pcg),
        ProfileType.DynType => Binding(pcg, pcg.Pick(ProfileTables.DynKinds)),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No variable has this type."),
    };

    /// <summary>
    /// The text of a number or a <c>bool</c>, the argument of a conversion from a string, in the forms of the standard library
    /// page and near them: signs, <c>_</c>, hexadecimal, a binary exponent, <c>inf</c>, <c>nan</c>, spaces, and the letters
    /// of <c>bool</c>; at most <see cref="ProfileTables.MaxCodePoints"/> code points.
    /// </summary>
    /// <param name="pcg">The random source.</param>
    /// <param name="type">The result of the conversion: <c>int</c>, <c>uint</c>, <c>double</c> or <c>bool</c>.</param>
    internal static string NumberText(PCG pcg, ProfileType type)
    {
        if (type == ProfileType.BoolType)
        {
            return pcg.Pick(BoolTexts);
        }

        if (pcg.OneIn(2))
        {
            return pcg.Pick(type == ProfileType.DoubleType ? DoubleTexts : IntegerTexts);
        }

        string digits = type == ProfileType.DoubleType
            ? (pcg.Below(2001) / Math.Pow(10, pcg.Below(4))).ToString("R", CultureInfo.InvariantCulture)
            : pcg.Below(100_000).ToString(CultureInfo.InvariantCulture);
        string text = pcg.Below(6) switch
        {
            0 => "-" + digits,
            1 => "+" + digits,
            2 => " " + digits,
            3 when digits.Length > 1 => digits[..1] + "_" + digits[1..],
            _ => digits,
        };
        return text.EnumerateRunes().Count() <= ProfileTables.MaxCodePoints ? text : digits;
    }

    // A value of the element type of a typed collection: a value of its kind, a scalar or null for dyn.
    private static Value ElementBinding(PCG pcg, StaticType type) =>
        Binding(pcg, type.IsDyn ? pcg.Pick(ProfileTables.ScalarKinds) : type.Kind);

    // A bound list of 0–3 elements of one type.
    private static Value TypedListBinding(PCG pcg, StaticType element)
    {
        ListValue list = new();
        int count = pcg.Below(ProfileTables.MaxElements + 1);
        for (int i = 0; i < count; i++)
        {
            list.Values.Add(ElementBinding(pcg, element));
        }

        return new Value { ListValue = list };
    }

    // A bound map of 0–3 entries with distinct keys of one type: the names of the fields for string keys.
    private static Value TypedMapBinding(PCG pcg, StaticType key, StaticType value)
    {
        MapValue map = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        int count = pcg.Below(ProfileTables.MaxElements + 1);
        for (int i = 0; i < count; i++)
        {
            Value entryKey = key.Kind == ProfileType.StringType
                ? new Value { StringValue = pcg.Pick(ProfileTables.FieldNames) }
                : new Value { Int64Value = IntValue(pcg) };
            if (keys.Add(entryKey.KindCase == Value.KindOneofCase.StringValue ? entryKey.StringValue : entryKey.Int64Value.ToString(CultureInfo.InvariantCulture)))
            {
                map.Entries.Add(new MapValue.Types.Entry { Key = entryKey, Value = ElementBinding(pcg, value) });
            }
        }

        return new Value { MapValue = map };
    }

    // A bound list of 0–3 scalars or null.
    private static Value ListBinding(PCG pcg)
    {
        ListValue list = new();
        int count = pcg.Below(ProfileTables.MaxElements + 1);
        for (int i = 0; i < count; i++)
        {
            list.Values.Add(Binding(pcg, pcg.Pick(ProfileTables.ScalarKinds)));
        }

        return new Value { ListValue = list };
    }

    // A bound map of 0–3 entries: keys of int, uint, bool and string, pairwise unequal by CEL equality, a string key a field
    // name half the time; values scalars or null.
    private static Value MapBinding(PCG pcg)
    {
        MapValue map = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        int count = pcg.Below(ProfileTables.MaxElements + 1);
        for (int i = 0; i < count; i++)
        {
            Value key = pcg.Pick(ProfileTables.KeyTypes) switch
            {
                ProfileType.StringType => new Value { StringValue = pcg.OneIn(2) ? pcg.Pick(ProfileTables.FieldNames) : StringValue(pcg) },
                ProfileType.BoolType => new Value { BoolValue = pcg.OneIn(2) },
                ProfileType.UintType => new Value { Uint64Value = UintValue(pcg) },
                _ => new Value { Int64Value = IntValue(pcg) },
            };
            string identity = key.KindCase switch
            {
                Value.KindOneofCase.StringValue => "string " + key.StringValue,
                Value.KindOneofCase.BoolValue => key.BoolValue ? "bool true" : "bool false",
                Value.KindOneofCase.Uint64Value => "number " + key.Uint64Value.ToString(CultureInfo.InvariantCulture),
                _ => "number " + key.Int64Value.ToString(CultureInfo.InvariantCulture),
            };
            if (keys.Add(identity))
            {
                map.Entries.Add(new MapValue.Types.Entry { Key = key, Value = Binding(pcg, pcg.Pick(ProfileTables.ScalarKinds)) });
            }
        }

        return new Value { MapValue = map };
    }

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
