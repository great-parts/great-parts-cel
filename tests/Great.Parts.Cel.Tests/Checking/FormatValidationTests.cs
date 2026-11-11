using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.FormatEnvironment;

namespace Great.Parts.Cel.Tests.Checking;

// The check of a call of format at compilation: for a format string that is a string literal and a list literal as its argument,
// InvalidFormatString with the detail of the page of the function, at the call or at the element that does not fit its clause. A call whose
// format string or list is anything else is left to evaluation. The kinds that each letter takes are the same as at evaluation, read from
// the static types, and a type that is dyn takes every letter.
public sealed class FormatValidationTests
{
    private const string Prefix = "invalid format string: ";

    private const string Formatting = "error during formatting: ";

    // What each letter says when it does not take the type, up to its name; the letters of the columns of the matrix, in order.
    private static readonly string[] Letters = ["s", "d", "f", "e", "b", "o", "x", "X"];

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

    // "static type | the result for %s %d %f %e %b %o %x %X": ok, or ! where the letter does not take the type, and then the name of
    // the type in the error follows the last cell.
    private const string Matrix = """
        null | ok | ! | ! | ! | ! | ! | ! | ! | null_type
        bool | ok | ! | ! | ! | ok | ! | ! | ! | bool
        int | ok | ok | ok | ok | ok | ok | ok | ok | int
        uint | ok | ok | ok | ok | ok | ok | ok | ok | uint
        double | ok | ok | ok | ok | ! | ! | ! | ! | double
        string | ok | ! | ! | ! | ! | ! | ok | ok | string
        bytes | ok | ! | ! | ! | ! | ! | ok | ok | bytes
        list | ok | ! | ! | ! | ! | ! | ! | ! | list
        map | ok | ! | ! | ! | ! | ! | ! | ! | map
        type | ok | ! | ! | ! | ! | ! | ! | ! | type
        timestamp | ok | ! | ! | ! | ! | ! | ! | ! | google.protobuf.Timestamp
        duration | ok | ! | ! | ! | ! | ! | ! | ! | google.protobuf.Duration
        dyn | ok | ok | ok | ok | ok | ok | ok | ok | dyn
        wrapped_int | ok | ok | ok | ok | ok | ok | ok | ok | int
        wrapped_string | ok | ! | ! | ! | ! | ! | ok | ok | string
        wrapped_double | ok | ok | ok | ok | ! | ! | ! | ! | double
        """;

    public static TheoryData<string, string, string, string> MatrixCells => MatrixData();

    [Theory]
    [MemberData(nameof(MatrixCells))]
    public static void StaticType_OfTheElement_FitsTheLetterOrIsRefused(string type, string letter, string expected, string typeName)
    {
        string expression = "'%" + letter + "'.format([v_" + type + "])";
        ImmutableArray<CelError> errors = Setup.Compile<CelValue>(expression).Errors;

        if (expected == "ok")
        {
            Assert.Empty(errors);
            return;
        }

        CelError error = Assert.Single(errors);
        Assert.Equal(
            (CelErrorCode.InvalidFormatString, Prefix + Formatting + KindTexts[Array.IndexOf(Letters, letter)] + typeName, expression.IndexOf("v_", StringComparison.Ordinal)),
            (error.Code, error.Message, error.Location.Offset));
    }

    // %s looks into the literals: an element of a list literal, or a key or a value of a map literal, that no clause takes is the error
    // at that element, although the list or the map is a kind that %s takes. The first one in the order of the text is reported.
    [Theory]
    [InlineData("'%s'.format([ip('1.2.3.4')])", "ip(")]
    [InlineData("'%s'.format([[1, ip('1.2.3.4')]])", "ip(")]
    [InlineData("'%s'.format([[[ip('1.2.3.4')]]])", "ip(")]
    [InlineData("'%s'.format([{'a': ip('1.2.3.4')}])", "ip(")]
    [InlineData("'%s'.format([{'a': [ip('1.2.3.4'), ip('::1')]}])", "ip(")]
    public static void TextClause_LooksIntoTheLiterals(string expression, string where)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(where);
        CelError error = Assert.Single(Setup.Compile<CelValue>(expression).Errors);

        int start = expression.IndexOf(where, StringComparison.Ordinal);
        Assert.Equal((CelErrorCode.InvalidFormatString, Prefix + Formatting + KindTexts[0] + "net.IP"), (error.Code, error.Message));
        Assert.InRange(error.Location.Offset, start, start + where.Length);
    }

    // The same literals with elements that %s takes, and a value that the checker cannot see into, are not errors.
    [Theory]
    [InlineData("'%s'.format([[1, 'a', 1.5, true, null, b'x', [2, [3]], {'k': [4]}]])")]
    [InlineData("'%s'.format([{1: 'a', 2: 'b'}])")]
    [InlineData("'%s'.format([v])")]
    [InlineData("'%s'.format([[v, v_dyn]])")]
    [InlineData("'%s%s%s%s%s%s'.format([v_list, v_map, v_type, v_timestamp, v_duration, v_null])")]
    public static void TextClause_OfValuesItTakes_Compiles(string expression)
    {
        Assert.Empty(Setup.Compile<CelValue>(expression).Errors);
    }

    // The checked call changes nothing at evaluation: it gives the value it would without the check.
    [Fact]
    public static void CheckedCall_ThatIsRight_IsEvaluatedAsAnyOther()
    {
        CelValue value = Evaluate("'%s-%d %.2f'.format(['a', 1, 3.14159])").GetValueOrThrow();

        Assert.True(value.TryGetString(out string? text) && text == "a-1 3.14");
    }

    private static TheoryData<string, string, string, string> MatrixData()
    {
        TheoryData<string, string, string, string> data = [];
        foreach (string row in Matrix.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] cells = row.Split(" | ");
            for (int i = 0; i < Letters.Length; i++)
            {
                data.Add(cells[0], Letters[i], cells[i + 1], cells[^1]);
            }
        }

        return data;
    }
}
