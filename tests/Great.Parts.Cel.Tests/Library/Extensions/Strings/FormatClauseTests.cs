using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.FormatEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The clauses of format at evaluation: each letter on each kind of value, with the text it gives or the error of the kind, the
// forms of numbers, the texts of lists and maps inside %s, and the errors of the format string with their texts. The values come in
// the list of a variable, so the call is not checked at compilation and an error is that of evaluation.
public sealed class FormatClauseTests
{
    private const string Prefix = "invalid argument to 'format': ";

    private const string Formatting = Prefix + "error during formatting: ";

    // The letters of the columns of the matrix, in order.
    private static readonly string[] Letters = ["s", "d", "f", "e", "b", "o", "x", "X"];

    // What each letter says when it does not take the value, up to the name of the type that was given.
    private static readonly string[] KindTexts =
    [
        "string clause can only be used on strings, bools, bytes, ints, doubles, maps, lists, types, durations, and timestamps, was given ",
        "decimal clause can only be used on ints, uints, and doubles, was given ",
        "fixed-point clause can only be used on ints, uints, and doubles, was given ",
        "scientific clause can only be used on ints, uints, and doubles, was given ",
        "only ints, uints, and bools can be formatted as binary, was given ",
        "octal clause can only be used on ints and uints, was given ",
        "only ints, uints, bytes, and strings can be formatted as hex, was given ",
        "only ints, uints, bytes, and strings can be formatted as hex, was given ",
    ];

    // "kind | the text for %s %d %f %e %b %o %x %X": the text, or ! where the letter does not take the kind. The values are those of
    // ValueOf; the type names in the errors are those of the runtime.
    private const string Matrix = """
        null | null | ! | ! | ! | ! | ! | ! | !
        bool | true | ! | ! | ! | 1 | ! | ! | !
        int | -42 | -42 | -42.000000 | -4.200000e+01 | -101010 | -52 | -2a | -2A
        uint | 42 | 42 | 42.000000 | 4.200000e+01 | 101010 | 52 | 2a | 2A
        double | 2.5 | 2.5 | 2.500000 | 2.500000e+00 | ! | ! | ! | !
        string | jk | ! | ! | ! | ! | ! | 6a6b | 6A6B
        bytes | jk | ! | ! | ! | ! | ! | 6a6b | 6A6B
        list | [1, a] | ! | ! | ! | ! | ! | ! | !
        map | {a: 1, b: 2} | ! | ! | ! | ! | ! | ! | !
        type | int | ! | ! | ! | ! | ! | ! | !
        timestamp | 1970-01-01T00:00:00Z | ! | ! | ! | ! | ! | ! | !
        duration | 90s | ! | ! | ! | ! | ! | ! | !
        """;

    public static TheoryData<string, string, string> MatrixCells => MatrixData();

    [Theory]
    [MemberData(nameof(MatrixCells))]
    public static void Clause_OnAKind_GivesItsTextOrTheErrorOfTheKind(string kind, string letter, string expected)
    {
        const string Call = "f.format(items)";
        CelEvalResult<CelValue> result = Evaluate(Call, Bindings("%" + letter, ValueOf(kind)));

        if (expected != "!")
        {
            Assert.Equal(expected, result.GetValueOrThrow().TryGetString(out string? text) ? text : "not a string");
            return;
        }

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.InvalidArgument, Formatting + KindTexts[Array.IndexOf(Letters, letter)] + TypeNameOf(kind), Call.IndexOf('(', StringComparison.Ordinal)),
            (error.Code, error.Message, error.Location.Offset));
    }

    [Fact]
    public static void Percent_IsOneWhenDoubled()
    {
        Assert.Equal("100% sure %d", Format("100%% sure %%d"));
        Assert.Equal("%%", Format("%%%%"));
        Assert.Equal("%1", Format("%%%d", CelValue.FromInt64(1)));
    }

    [Fact]
    public static void FormatWithoutClauses_IsItself_AndExtraElementsAreNotUsed()
    {
        Assert.Equal(string.Empty, Format(string.Empty));
        Assert.Equal("plain text 😀", Format("plain text 😀"));
        Assert.Equal("a", Format("%s", CelValue.FromString("a"), CelValue.FromInt64(2), CelValue.Null));
        Assert.Equal("x", Format("x", CelValue.FromInt64(1)));
    }

    // The clauses take the elements in order, one each, and the text between them is copied.
    [Fact]
    public static void Clauses_TakeTheElementsInOrder()
    {
        Assert.Equal(
            "a=1, b=0x2a, c=3.50",
            Format("a=%d, b=0x%x, c=%.2f", CelValue.FromInt64(1), CelValue.FromInt64(42), CelValue.FromDouble(3.5)));
    }

    [Fact]
    public static void Integers_AtTheEdgesOfTheirRange()
    {
        CelValue minimum = CelValue.FromInt64(long.MinValue);
        CelValue maximum = CelValue.FromInt64(long.MaxValue);
        CelValue unsignedMaximum = CelValue.FromUInt64(ulong.MaxValue);

        Assert.Equal("-9223372036854775808", Format("%d", minimum));
        Assert.Equal("-8000000000000000", Format("%x", minimum));
        Assert.Equal("-" + "1" + new string('0', 63), Format("%b", minimum));
        Assert.Equal("-1" + new string('0', 21), Format("%o", minimum));
        Assert.Equal("9223372036854775807 7fffffffffffffff 7FFFFFFFFFFFFFFF", Format("%d %x %X", maximum, maximum, maximum));
        Assert.Equal("18446744073709551615 ffffffffffffffff 1" + new string('7', 21), Format("%s %x %o", unsignedMaximum, unsignedMaximum, unsignedMaximum));
        Assert.Equal(new string('1', 64), Format("%b", unsignedMaximum));
        Assert.Equal("0 0 0 0", Format("%d %b %o %x", CelValue.FromInt64(0), CelValue.FromInt64(0), CelValue.FromInt64(0), CelValue.FromUInt64(0)));
    }

    [Fact]
    public static void IntegerAsDouble_IsRoundedToTheNearestWithTiesToEven()
    {
        Assert.Equal("9007199254740992.00", Format("%.2f", CelValue.FromInt64(9007199254740993)));
        Assert.Equal("9007199254740996.00", Format("%.2f", CelValue.FromInt64(9007199254740995)));
        Assert.Equal("18446744073709551616.0", Format("%.1f", CelValue.FromUInt64(ulong.MaxValue)));
        Assert.Equal("9.223372036854776e+18", Format("%.15e", CelValue.FromInt64(long.MaxValue)));
    }

    [Fact]
    public static void Double_InTheShortestForm_HasNoExponent()
    {
        Assert.Equal("1000000000000000000000", Format("%s", CelValue.FromDouble(1e21)));
        Assert.Equal("0.0000001", Format("%s", CelValue.FromDouble(1e-7)));
        Assert.Equal("0.1", Format("%d", CelValue.FromDouble(0.1)));
        Assert.Equal("-0", Format("%s", CelValue.FromDouble(-0.0)));
        Assert.Equal("0", Format("%s", CelValue.FromDouble(0.0)));
        Assert.Equal("123456789.125", Format("%s", CelValue.FromDouble(123456789.125)));
        Assert.Equal("-1.5", Format("%s", CelValue.FromDouble(-1.5)));
        Assert.Equal("100", Format("%s", CelValue.FromDouble(100)));
        Assert.Equal("1" + new string('0', 300), Format("%s", CelValue.FromDouble(1e300)));
        Assert.Equal("17976931348623157" + new string('0', 292), Format("%s", CelValue.FromDouble(double.MaxValue)));
        Assert.Equal("0." + new string('0', 323) + "5", Format("%s", CelValue.FromDouble(double.Epsilon)));
        Assert.Equal("0.30000000000000004", Format("%s", CelValue.FromDouble(0.1 + 0.2)));
    }

    [Fact]
    public static void Double_NotFinite_IsTheSameTextForEveryLetterThatTakesIt()
    {
        foreach (string clause in new[] { "%s", "%d", "%f", "%e", "%.0f", "%.100e" })
        {
            Assert.Equal("NaN", Format(clause, CelValue.FromDouble(double.NaN)));
            Assert.Equal("Infinity", Format(clause, CelValue.FromDouble(double.PositiveInfinity)));
            Assert.Equal("-Infinity", Format(clause, CelValue.FromDouble(double.NegativeInfinity)));
        }
    }

    [Theory]
    [InlineData("%f", 1234.5678, "1234.567800")]
    [InlineData("%.0f", 0.5, "0")]
    [InlineData("%.0f", 1.5, "2")]
    [InlineData("%.0f", 2.5, "2")]
    [InlineData("%.0f", 3.5, "4")]
    [InlineData("%.2f", 0.125, "0.12")]
    [InlineData("%.2f", 0.375, "0.38")]
    [InlineData("%.1f", 0.25, "0.2")]
    [InlineData("%.1f", 0.75, "0.8")]
    [InlineData("%.1f", 0.05, "0.1")]
    [InlineData("%.1f", 0.15, "0.1")]
    [InlineData("%.2f", 3.14159, "3.14")]
    [InlineData("%f", -0.0, "-0.000000")]
    [InlineData("%.2f", -0.001, "-0.00")]
    [InlineData("%.2f", 0.0, "0.00")]
    [InlineData("%.10f", 0.1, "0.1000000000")]
    [InlineData("%.20f", 0.1, "0.10000000000000000555")]
    [InlineData("%.0f", 1e21, "1000000000000000000000")]
    [InlineData("%f", 1e22, "10000000000000000000000.000000")]
    [InlineData("%.0f", 1e23, "99999999999999991611392")]
    [InlineData("%.3f", 999.9996, "1000.000")]
    [InlineData("%.3f", 0.0004, "0.000")]
    public static void Fixed_IsCorrectlyRoundedFromTheExactValue(string clause, double value, string expected)
    {
        Assert.Equal(expected, Format(clause, CelValue.FromDouble(value)));
    }

    [Theory]
    [InlineData("%e", 1234.5678, "1.234568e+03")]
    [InlineData("%e", 0.0, "0.000000e+00")]
    [InlineData("%e", -0.0, "-0.000000e+00")]
    [InlineData("%e", 1e-5, "1.000000e-05")]
    [InlineData("%e", 123456789.0, "1.234568e+08")]
    [InlineData("%.2e", 1e100, "1.00e+100")]
    [InlineData("%.2e", 1e-100, "1.00e-100")]
    [InlineData("%.0e", 12345.0, "1e+04")]
    [InlineData("%.3e", 123456.0, "1.235e+05")]
    [InlineData("%.0e", 0.25, "2e-01")]
    [InlineData("%.1e", 0.35, "3.5e-01")]
    [InlineData("%.2e", 1.125, "1.12e+00")]
    [InlineData("%.2e", 1.135, "1.14e+00")]
    [InlineData("%e", 9.9999995e9, "1.000000e+10")]
    [InlineData("%.0e", 9.5, "1e+01")]
    [InlineData("%.0e", 2.5, "2e+00")]
    [InlineData("%.3e", 5e-324, "4.941e-324")]
    public static void Scientific_HasTwoDigitsOfExponentAtLeast(string clause, double value, string expected)
    {
        Assert.Equal(expected, Format(clause, CelValue.FromDouble(value)));
    }

    // The precision acts for %f and %e only: the other letters read it and ignore it.
    [Fact]
    public static void Precision_ActsOnlyForFixedAndScientific()
    {
        Assert.Equal("abcdef", Format("%.3s", CelValue.FromString("abcdef")));
        Assert.Equal("42", Format("%.9d", CelValue.FromInt64(42)));
        Assert.Equal("2.5", Format("%.1s", CelValue.FromDouble(2.5)));
        Assert.Equal("101", Format("%.0b", CelValue.FromInt64(5)));
        Assert.Equal("6a", Format("%.2x", CelValue.FromString("j")));
    }

    [Fact]
    public static void Precision_OfTheLimit_IsFormatted_AndOneMoreIsAnError()
    {
        string hundred = Format("%.100f", CelValue.FromDouble(1.5));

        Assert.Equal(102, hundred.Length);
        Assert.StartsWith("1.5000", hundred, StringComparison.Ordinal);
        Assert.Equal("1.5", hundred.Replace("0", string.Empty, StringComparison.Ordinal));
        Assert.Equal(106, Format("%.100e", CelValue.FromDouble(1.5)).Length);
        Assert.Equal("a", Format("%.100s", CelValue.FromString("a")));
        Assert.Equal("1", Format("%.100d", CelValue.FromInt64(1)));
    }

    [Fact]
    public static void Strings_AreWrittenAsTheyAre()
    {
        Assert.Equal("a😀é", Format("%s", CelValue.FromString("a😀é")));
        string lone = ((char)0xD83D).ToString();
        Assert.Equal(lone, Format("%s", CelValue.FromString(lone)));
        Assert.Equal("a%sb", Format("%s", CelValue.FromString("a%sb")));
    }

    [Fact]
    public static void Hex_OfAStringIsOfItsUtf8Text()
    {
        Assert.Equal("c3a9f09f9880", Format("%x", CelValue.FromString("é😀")));
        Assert.Equal("C3A9F09F9880", Format("%X", CelValue.FromString("é😀")));
        Assert.Equal(string.Empty, Format("%x", CelValue.FromString(string.Empty)));
        Assert.Equal("efbfbd", Format("%x", CelValue.FromString(((char)0xD83D).ToString())));
        Assert.Equal("efbfbd61", Format("%x", CelValue.FromString(((char)0xDE00).ToString() + "a")));
    }

    // More than the stack buffer holds goes in pieces: a hex text of 300 bytes, an input that ends in a character that is cut by a piece.
    [Fact]
    public static void Hex_OfALongInput_GoesInPieces()
    {
        byte[] bytes = new byte[300];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)i;
        }

        Assert.Equal(Convert.ToHexStringLower(bytes), Format("%x", CelValue.FromBytes(bytes)));
        Assert.Equal(Convert.ToHexString(bytes), Format("%X", CelValue.FromBytes(bytes)));

        string text = new string('é', 100) + "😀" + new string('x', 70);
        Assert.Equal(Convert.ToHexStringLower(System.Text.Encoding.UTF8.GetBytes(text)), Format("%x", CelValue.FromString(text)));
    }

    // The bytes of %s are read as UTF-8, and an invalid sequence is U+FFFD, a replacement for each.
    [Fact]
    public static void Bytes_AsText_AreUtf8WithTheReplacementForAnInvalidSequence()
    {
        Assert.Equal("é😀", Format("%s", CelValue.FromBytes(System.Text.Encoding.UTF8.GetBytes("é😀"))));
        Assert.Equal("a�b", Format("%s", CelValue.FromBytes(new byte[] { 0x61, 0xFF, 0x62 })));
        Assert.Equal("�", Format("%s", CelValue.FromBytes(new byte[] { 0xC3 })));
        Assert.Equal(string.Empty, Format("%s", CelValue.FromBytes(Array.Empty<byte>())));

        byte[] long200 = new byte[200];
        Array.Fill(long200, (byte)0xC3, 0, 199);
        long200[199] = 0xA9;
        Assert.Equal(new string('�', 198) + "é", Format("%s", CelValue.FromBytes(long200)));

        byte[] manyAccents = new byte[400];
        for (int i = 0; i < manyAccents.Length; i += 2)
        {
            manyAccents[i] = 0xC3;
            manyAccents[i + 1] = 0xA9;
        }

        Assert.Equal(new string('é', 200), Format("%s", CelValue.FromBytes(manyAccents)));
    }

    [Fact]
    public static void Timestamp_AndDuration_AreTheTextsOfTheirConversions()
    {
        Assert.Equal("2021-03-04T05:06:07.5Z", Format("%s", CelValue.FromTimestamp(new CelTimestamp(1614834367, 500_000_000))));
        Assert.Equal("1.5s", Format("%s", CelValue.FromDuration(new CelDuration(1_500_000_000))));
        Assert.Equal("-3600s", Format("%s", CelValue.FromDuration(new CelDuration(-3_600_000_000_000))));
        Assert.Equal("0s", Format("%s", CelValue.FromDuration(new CelDuration(0))));
    }

    [Fact]
    public static void List_InText_HasItsElementsByTheSameRules()
    {
        CelValue list = CelValue.FromList(CelList.Create(
            CelValue.FromInt64(1),
            CelValue.FromString("a"),
            CelValue.FromDouble(1.5),
            CelValue.FromBoolean(true),
            CelValue.Null,
            CelValue.FromBytes(new byte[] { 0x62 }),
            CelValue.FromList(CelList.Create(CelValue.FromUInt64(2), CelValue.FromList(CelList.Create())))));

        Assert.Equal("[1, a, 1.5, true, null, b, [2, []]]", Format("%s", list));
        Assert.Equal("[]", Format("%s", CelValue.FromList(CelList.Create())));
        Assert.Equal("x[1]y", Format("x%sy", CelValue.FromList(CelList.Create(CelValue.FromInt64(1)))));
    }

    [Fact]
    public static void Map_InText_IsOrderedByTheTextOfTheKey()
    {
        CelValue map = MapOf(("b", 2), ("a", 1), ("B", 3), ("", 0), ("ab", 4));

        Assert.Equal("{: 0, B: 3, a: 1, ab: 4, b: 2}", Format("%s", map));
        Assert.Equal("{}", Format("%s", CelValue.FromMap(CelMap.Create([]))));
    }

    // The order is by code points, not by UTF-16 units: a character of U+10000 and above comes after one at U+E000 and above.
    [Fact]
    public static void Map_InText_IsOrderedByCodePoints()
    {
        CelValue map = MapOf(("😀", 2), ("￿", 1), ("a", 0));

        Assert.Equal("{a: 0, ￿: 1, 😀: 2}", Format("%s", map));
    }

    // The keys are written by the same rules and ordered by their texts: "10" before "100" before "9".
    [Fact]
    public static void Map_WithIntKeys_IsOrderedByTheirTexts()
    {
        CelValue map = CelValue.FromMap(CelMap.Create(
        [
            KeyValuePair.Create(CelValue.FromInt64(9), CelValue.FromString("b")),
            KeyValuePair.Create(CelValue.FromInt64(100), CelValue.FromString("c")),
            KeyValuePair.Create(CelValue.FromInt64(10), CelValue.FromString("a")),
        ]));

        Assert.Equal("{10: a, 100: c, 9: b}", Format("%s", map));
    }

    // More pairs than one run of the insertion sort, so that the merges run too; the keys come in a scrambled order, and each value is
    // the key's own number so that a pair that moved apart from its key would show.
    [Theory]
    [InlineData(13)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(100)]
    [InlineData(1000)]
    public static void Map_OfManyPairs_IsSorted(int count)
    {
        var entries = new List<KeyValuePair<CelValue, CelValue>>();
        for (int i = 0; i < count; i++)
        {
            int scrambled = (int)((long)i * 7919 % count);
            entries.Add(KeyValuePair.Create(
                CelValue.FromString("k" + scrambled.ToString("D4", CultureInfo.InvariantCulture)),
                CelValue.FromInt64(scrambled)));
        }

        var expected = new List<string>();
        for (int i = 0; i < count; i++)
        {
            expected.Add("k" + i.ToString("D4", CultureInfo.InvariantCulture) + ": " + i.ToString(CultureInfo.InvariantCulture));
        }

        Assert.Equal("{" + string.Join(", ", expected) + "}", Format("%s", CelValue.FromMap(CelMap.Create(entries))));
    }

    [Fact]
    public static void Collections_Nested_AreWrittenInPlace()
    {
        CelValue inner = MapOf(("y", 2), ("x", 1));
        CelValue outer = CelValue.FromMap(CelMap.Create(
        [
            KeyValuePair.Create(CelValue.FromString("z"), CelValue.FromList(CelList.Create(inner, CelValue.FromInt64(3)))),
            KeyValuePair.Create(CelValue.FromString("a"), inner),
        ]));

        Assert.Equal("<{a: {x: 1, y: 2}, z: [{x: 1, y: 2}, 3]}>", Format("<%s>", outer));
        Assert.Equal("[{x: 1, y: 2}] {x: 1, y: 2}", Format("%s %s", CelValue.FromList(CelList.Create(inner)), inner));
    }

    [Fact]
    public static void List_WithAnAbstractValue_IsAKindErrorWithItsTypeName()
    {
        CelValue map = CelValue.FromMap(CelMap.Create(
        [
            KeyValuePair.Create(CelValue.FromString("a"), CelValue.FromList(CelList.Create(CelValue.FromInt64(1)))),
        ]));
        CelValue bad = Evaluate("ip('1.2.3.4')").GetValueOrThrow();
        CelValue withAddress = CelValue.FromList(CelList.Create(CelValue.FromInt64(1), bad));

        CelError error = Assert.Single(Evaluate("f.format(items)", Bindings("%s", withAddress)).Errors);

        Assert.Equal(Formatting + KindTexts[0] + "net.IP", error.Message);
        Assert.Equal("{a: [1]}", Format("%s", map));
    }

    // The values of the matrix are those of the table above; each kind has one.
    private static CelValue ValueOf(string kind) => kind switch
    {
        "null" => CelValue.Null,
        "bool" => CelValue.FromBoolean(true),
        "int" => CelValue.FromInt64(-42),
        "uint" => CelValue.FromUInt64(42),
        "double" => CelValue.FromDouble(2.5),
        "string" => CelValue.FromString("jk"),
        "bytes" => CelValue.FromBytes(new byte[] { 0x6A, 0x6B }),
        "list" => CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a"))),
        "map" => MapOf(("b", 2), ("a", 1)),
        "type" => CelValue.FromType(CelType.Int),
        "timestamp" => CelValue.FromTimestamp(new CelTimestamp(0, 0)),
        "duration" => CelValue.FromDuration(new CelDuration(90_000_000_000)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // The name of the runtime type of the kind in the error.
    private static string TypeNameOf(string kind) => kind == "null" ? "null_type" : kind;

    private static CelValue MapOf(params (string Key, long Value)[] pairs)
    {
        var entries = new List<KeyValuePair<CelValue, CelValue>>();
        foreach ((string key, long value) in pairs)
        {
            entries.Add(KeyValuePair.Create(CelValue.FromString(key), CelValue.FromInt64(value)));
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    private static TheoryData<string, string, string> MatrixData()
    {
        TheoryData<string, string, string> data = [];
        foreach (string row in Matrix.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] cells = row.Split(" | ");
            for (int i = 0; i < Letters.Length; i++)
            {
                data.Add(cells[0], Letters[i], cells[i + 1]);
            }
        }

        return data;
    }
}
