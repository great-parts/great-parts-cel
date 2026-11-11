using System;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.StringsEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The functions of the set strings on their edges: indexes in code points over surrogate pairs, the empty pattern and the offsets,
// the limit n of replace and split, the checks of ranges and their errors, and the strings that only bindings can hold. Each row of
// the first table is an expression that is true.
public sealed class StringFunctionTests
{
    private static readonly string LoneHigh = ((char)0xD83D).ToString();
    private static readonly string LoneLow = ((char)0xDE00).ToString();

    private const string Holds = """
        'a😀b'.charAt(0) == 'a'
        'a😀b'.charAt(1) == '😀'
        'a😀b'.charAt(2) == 'b'
        'a😀b'.charAt(3) == ''
        ''.charAt(0) == ''
        'a😀b😀c'.indexOf('😀') == 1
        'a😀b😀c'.indexOf('😀', 2) == 3
        'a😀b😀c'.indexOf('b') == 2
        'a😀b😀c'.indexOf('c', 4) == 4
        'a😀b😀c'.indexOf('c', 5) == -1
        'a😀b😀c'.indexOf('', 5) == 5
        'a😀b😀c'.indexOf('😀😀') == -1
        'a😀b😀c'.indexOf('b😀c') == 2
        'abc'.indexOf('') == 0
        ''.indexOf('') == 0
        ''.indexOf('a') == -1
        'abc'.indexOf('abcd') == -1
        'aaab'.indexOf('aab') == 1
        'abcabc'.lastIndexOf('bc') == 4
        'abcabc'.lastIndexOf('abc', 2) == 0
        'abcabc'.lastIndexOf('abc', 3) == 3
        'abcabc'.lastIndexOf('') == 6
        'abcabc'.lastIndexOf('', 4) == 4
        'abcabc'.lastIndexOf('c', 6) == -1
        'abcabc'.lastIndexOf('c', 5) == 5
        'abcabc'.lastIndexOf('x') == -1
        'a😀b😀'.lastIndexOf('😀') == 3
        'a😀b😀'.lastIndexOf('😀', 2) == 1
        'aaaa'.lastIndexOf('aa') == 2
        'aaaa'.lastIndexOf('aa', 1) == 1
        'a😀b😀'.replace('😀', '-') == 'a-b-'
        'aaa'.replace('aa', 'b') == 'ba'
        'aaaa'.replace('aa', 'b') == 'bb'
        'abc'.replace('', '-') == '-a-b-c-'
        'a😀'.replace('', '|') == '|a|😀|'
        ''.replace('', 'x') == 'x'
        'abc'.replace('', '-', 2) == '-a-bc'
        'abab'.replace('a', 'X', 0) == 'abab'
        'abab'.replace('a', 'X', 1) == 'Xbab'
        'abab'.replace('a', 'X', -1) == 'XbXb'
        'abc'.replace('abcd', 'x') == 'abc'
        'abc'.replace('b', '') == 'ac'
        'abc'.replace('b', 'bbb') == 'abbbc'
        'abcabc'.replace('abc', 'abc', 1) == 'abcabc'
        'a,b,,c'.split(',') == ['a', 'b', '', 'c']
        ',a,'.split(',') == ['', 'a', '']
        ''.split(',') == ['']
        'abc'.split('') == ['a', 'b', 'c']
        ''.split('') == []
        'a😀b'.split('') == ['a', '😀', 'b']
        'a,b,c'.split(',', 2) == ['a', 'b,c']
        'a,b,c'.split(',', 1) == ['a,b,c']
        'a,b,c'.split(',', 0) == []
        'a,b,c'.split(',', -1) == ['a', 'b', 'c']
        'a,b,c'.split(',', 5) == ['a', 'b', 'c']
        'héé'.split('', 2) == ['h', 'éé']
        ''.split('', 1) == []
        ''.split(',', 1) == ['']
        'a😀b😀c'.split('😀') == ['a', 'b', 'c']
        'aaa'.split('aa') == ['', 'a']
        'abc'.split('abcd') == ['abc']
        'a😀b😀c'.substring(1, 4) == '😀b😀'
        'a😀b😀c'.substring(2) == 'b😀c'
        'a😀b😀c'.substring(5) == ''
        'abc'.substring(1, 1) == ''
        'abc'.substring(0, 3) == 'abc'
        'abc'.substring(3, 3) == ''
        '  a b  '.trim() == 'a b'
        '\t\n a \r\v\f'.trim() == 'a'
        ''.trim() == ''
        '   '.trim() == ''
        'a'.trim() == 'a'
        'ÀBc😀'.upperAscii() == 'ÀBC😀'
        'AbÉ😀'.lowerAscii() == 'abÉ😀'
        'abc'.upperAscii() == 'ABC'
        'ABC'.lowerAscii() == 'abc'
        ''.lowerAscii() == ''
        'xyz-@[`{'.upperAscii() == 'XYZ-@[`{'
        '@AZ['.lowerAscii() == '@az['
        ['a', 'b', 'c'].join() == 'abc'
        ['a', 'b', 'c'].join('-') == 'a-b-c'
        ['😀'].join('x') == '😀'
        [].join('-') == ''
        ['', ''].join(',') == ','
        ['ab'].join() == 'ab'
        'ab😀'.reverse() == '😀ba'
        ''.reverse() == ''
        'a'.reverse() == 'a'
        'a😀b😁'.reverse() == '😁b😀a'
        """;

    // "expression | code | message": the error has the code and the message at the `(` of the call.
    private const string Errors = """
        'abc'.charAt(4) | IndexOutOfRange | index out of range: 4
        'abc'.charAt(-1) | IndexOutOfRange | index out of range: -1
        'abc'.charAt(9223372036854775807) | IndexOutOfRange | index out of range: 9223372036854775807
        'a😀'.charAt(3) | IndexOutOfRange | index out of range: 3
        'abc'.indexOf('a', 4) | IndexOutOfRange | index out of range: 4
        'abc'.indexOf('a', -1) | IndexOutOfRange | index out of range: -1
        'abc'.indexOf('', 4) | IndexOutOfRange | index out of range: 4
        'abc'.lastIndexOf('a', 4) | IndexOutOfRange | index out of range: 4
        'abc'.lastIndexOf('', 4) | IndexOutOfRange | index out of range: 4
        'abc'.lastIndexOf('a', -2) | IndexOutOfRange | index out of range: -2
        'abc'.substring(4) | IndexOutOfRange | index out of range: 4
        'abc'.substring(-1) | IndexOutOfRange | index out of range: -1
        'abc'.substring(1, 5) | IndexOutOfRange | index out of range: 5
        'abc'.substring(-1, 2) | IndexOutOfRange | index out of range: -1
        'abc'.substring(-3, -2) | IndexOutOfRange | index out of range: -3
        'abc'.substring(2, 1) | InvalidArgument | invalid argument to 'substring': invalid substring range: start index 2 must be less than or equal to the end index
        'abc'.substring(5, 1) | InvalidArgument | invalid argument to 'substring': invalid substring range: start index 5 must be less than or equal to the end index
        """;

    public static TheoryData<string> HoldingCases => HoldingData();

    public static TheoryData<string, string, string> ErrorCases => ErrorData();

    public static TheoryData<int> SpaceCodes => Codes(
        0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x0020, 0x0085, 0x00A0, 0x1680, 0x2000, 0x2005, 0x200A, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000);

    public static TheoryData<int> NotSpaceCodes => Codes(0x0008, 0x000E, 0x001F, 0x180E, 0x200B, 0x200C, 0x200D, 0x2060, 0xFEFF);

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Error_HasItsCodeAndMessageAtTheCall(string expression, string code, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelError error = Assert.Single(Evaluate(expression).Errors);

        Assert.Equal((Enum.Parse<CelErrorCode>(code), message, expression.IndexOf('(', StringComparison.Ordinal)), (error.Code, error.Message, error.Location.Offset));
    }

    // The code points of a string that holds a lone surrogate are counted one each, as size counts them.
    [Fact]
    public static void LoneSurrogate_IsOneCodePoint()
    {
        string text = "a" + LoneHigh + "b";
        CelBindings bindings = new CelBindings().Add("text", text);

        Assert.Equal(LoneHigh, EvaluateString("text.charAt(1)", bindings));
        Assert.Equal(LoneHigh + "b", EvaluateString("text.substring(1, 3)", bindings));
        Assert.Equal(2, EvaluateInt("text.indexOf('b')", bindings));
        Assert.Equal(3, EvaluateInt("size(text)", bindings));
        Assert.Equal("b" + LoneHigh + "a", EvaluateString("text.reverse()", bindings));
    }

    // A low surrogate that stands alone is found, and one that is the second half of a pair is not.
    [Fact]
    public static void LonePatternOfALowSurrogate_IsFoundAtACodePointBoundary()
    {
        CelBindings alone = new CelBindings().Add("text", "x" + LoneLow + "y").Add("pattern", LoneLow);
        CelBindings pair = new CelBindings().Add("text", "😀😀").Add("pattern", LoneLow);

        Assert.Equal(1, EvaluateInt("text.indexOf(pattern)", alone));
        Assert.Equal(1, EvaluateInt("text.lastIndexOf(pattern)", alone));
        Assert.Equal("x-y", EvaluateString("text.replace(pattern, '-')", alone));
        Assert.Equal(-1, EvaluateInt("text.indexOf(pattern)", pair));
        Assert.Equal(-1, EvaluateInt("text.lastIndexOf(pattern)", pair));
        Assert.Equal("😀😀", EvaluateString("text.replace(pattern, '-')", pair));
        Assert.Equal("😀😀", string.Join("|", Strings("text.split(pattern)", pair)));
    }

    // A high surrogate that ends the pattern does not match the first half of a pair; the search goes on to the one that stands alone.
    [Fact]
    public static void LonePatternOfAHighSurrogate_SkipsTheHalfOfAPair()
    {
        CelBindings bindings = new CelBindings().Add("text", "x😀" + LoneHigh + "y").Add("pattern", LoneHigh);

        Assert.Equal(2, EvaluateInt("text.indexOf(pattern)", bindings));
        Assert.Equal(2, EvaluateInt("text.lastIndexOf(pattern)", bindings));
        Assert.Equal("x😀-y", EvaluateString("text.replace(pattern, '-')", bindings));
        Assert.Equal("x😀|y", string.Join("|", Strings("text.split(pattern)", bindings)));
    }

    // The two lone surrogates in this order reverse into a pair, since the reverse is by code points.
    [Fact]
    public static void Reverse_OfALowAndAHighSurrogate_IsAPair()
    {
        CelBindings bindings = new CelBindings().Add("text", LoneLow + LoneHigh);

        Assert.Equal(LoneHigh + LoneLow, EvaluateString("text.reverse()", bindings));
    }

    [Theory]
    [MemberData(nameof(SpaceCodes))]
    public static void Trim_RemovesTheWhiteSpaceCodePoints(int code)
    {
        string space = ((char)code).ToString();
        CelBindings bindings = new CelBindings().Add("text", space + space + "x" + space);

        Assert.Equal("x", EvaluateString("text.trim()", bindings));
    }

    [Theory]
    [MemberData(nameof(NotSpaceCodes))]
    public static void Trim_KeepsTheOtherCodePoints(int code)
    {
        string other = ((char)code).ToString();
        string text = other + "x" + other;
        CelBindings bindings = new CelBindings().Add("text", text);

        Assert.Equal(text, EvaluateString("text.trim()", bindings));
    }

    [Theory]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("a\nb\tc\rd", "\"a\\nb\\tc\\rd\"")]
    [InlineData("ok", "\"ok\"")]
    [InlineData("", "\"\"")]
    [InlineData("é😀", "\"é😀\"")]
    public static void Quote_EscapesTheEightCharactersAndTheQuote(string text, string expected)
    {
        CelBindings bindings = new CelBindings().Add("text", text);

        Assert.Equal(expected, EvaluateString("strings.quote(text)", bindings));
    }

    // The remaining escapes are written with their code, and the other control characters stay as they are.
    [Fact]
    public static void Quote_EscapesTheBellTheBackspaceTheFormFeedAndTheVerticalTab()
    {
        string text = string.Concat((char)7, (char)8, (char)12, (char)11, (char)1, (char)0x7F, (char)0x2028);
        CelBindings bindings = new CelBindings().Add("text", text);

        Assert.Equal("\"\\a\\b\\f\\v" + (char)1 + (char)0x7F + (char)0x2028 + "\"", EvaluateString("strings.quote(text)", bindings));
    }

    [Fact]
    public static void Quote_WritesALoneSurrogateAsTheReplacementCharacter()
    {
        CelBindings bindings = new CelBindings().Add("text", LoneHigh + "a" + LoneLow + "😀");
        string replacement = ((char)0xFFFD).ToString();

        Assert.Equal("\"" + replacement + "a" + replacement + "😀\"", EvaluateString("strings.quote(text)", bindings));
    }

    // Static types that say dyn promise nothing: a value of another kind than the overload takes is no matching overload.
    [Theory]
    [InlineData("x.charAt(1)", "5")]
    [InlineData("'abc'.charAt(x)", "'z'")]
    [InlineData("'abc'.indexOf(x)", "5")]
    [InlineData("'abc'.substring(x, 2)", "'z'")]
    [InlineData("'abc'.replace('a', x)", "5")]
    [InlineData("'abc'.split(x)", "5")]
    [InlineData("x.join()", "['a', 1]")]
    [InlineData("x.join('-')", "'a'")]
    [InlineData("strings.quote(x)", "1")]
    [InlineData("x.trim()", "[1]")]
    [InlineData("x.reverse()", "1.5")]
    public static void ArgumentOfAnotherKind_IsNoMatchingOverload(string expression, string value)
    {
        CelBindings bindings = new CelBindings().Add("x", Evaluate(value).GetValueOrThrow());

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(Evaluate(expression, bindings).Errors).Code);
    }

    // An error value that an element of the list is comes out as the value of the call.
    [Fact]
    public static void JoinOfAListWithAnErrorElement_IsThatError()
    {
        CelList view = CelList.CreateView<string>(
            ["a", "b"],
            static element => element == "b" ? throw new InvalidOperationException("unreadable") : CelValue.FromString(element));
        CelBindings bindings = new CelBindings().Add("x", CelValue.FromList(view));

        Assert.Equal(CelErrorCode.ApplicationException, Assert.Single(Evaluate("x.join()", bindings).Errors).Code);
    }

    public static TheoryData<string> HoldingData()
    {
        TheoryData<string> data = [];
        foreach (string line in Lines(Holds))
        {
            data.Add(line);
        }

        return data;
    }

    public static TheoryData<string, string, string> ErrorData()
    {
        TheoryData<string, string, string> data = [];
        foreach (string line in Lines(Errors))
        {
            string[] cells = line.Split(" | ");
            data.Add(cells[0], cells[1], cells[2]);
        }

        return data;
    }

    private static string[] Lines(string table) => table.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static TheoryData<int> Codes(params int[] codes)
    {
        TheoryData<int> data = [];
        foreach (int code in codes)
        {
            data.Add(code);
        }

        return data;
    }

    // The elements of a list of strings that an expression gives.
    private static string[] Strings(string expression, CelBindings bindings)
    {
        Assert.True(Evaluate(expression, bindings).GetValueOrThrow().TryGetList(out CelList? list));
        return [.. list.Select(static item => item.TryGetString(out string? text) ? text : "?")];
    }

    private static long EvaluateInt(string expression, CelBindings bindings) =>
        Evaluate(expression, bindings).GetValueOrThrow().TryGetInt64(out long value) ? value : throw new InvalidOperationException(expression);
}
