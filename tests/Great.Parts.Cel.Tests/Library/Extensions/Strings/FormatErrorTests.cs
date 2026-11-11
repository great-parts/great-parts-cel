using System;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.FormatEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The errors of the format string at evaluation: InvalidArgument at the call, "invalid argument to 'format': " and the detail, which is the
// text of cel-go except for the order of the first two checks and the code point of an unknown letter. The values are integers, which
// every letter that is not unknown takes; a list that is not a literal is not checked at compilation.
public sealed class FormatErrorTests
{
    private const string Clause = "could not parse formatting clause: ";

    // "format string | number of elements | detail"
    private const string Rows = """
        %z | 1 | could not parse formatting clause: unrecognized formatting clause "z"
        a%qb | 1 | could not parse formatting clause: unrecognized formatting clause "q"
        %5d | 1 | could not parse formatting clause: unrecognized formatting clause "5"
        % d | 1 | could not parse formatting clause: unrecognized formatting clause " "
        %-5d | 1 | could not parse formatting clause: unrecognized formatting clause "-"
        %😀 | 1 | could not parse formatting clause: unrecognized formatting clause "😀"
        %é | 1 | could not parse formatting clause: unrecognized formatting clause "é"
        % | 1 | unexpected end of string
        abc% | 1 | unexpected end of string
        %d% | 1 | unexpected end of string
        % | 0 | unexpected end of string
        %d %d | 1 | index 1 out of range
        %s%s%s | 2 | index 2 out of range
        %s | 0 | index 0 out of range
        %z | 0 | index 0 out of range
        %.101f | 0 | index 0 out of range
        %d%z | 1 | index 1 out of range
        %.101f | 1 | could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        %.1000s | 1 | could not parse formatting clause: error while parsing precision: precision 1000 exceeds maximum allowed precision 100
        %.101e | 1 | could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        %.101d | 1 | could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        %.9223372036854775807d | 1 | could not parse formatting clause: error while parsing precision: precision 9223372036854775807 exceeds maximum allowed precision 100
        %.9223372036854775808d | 1 | could not parse formatting clause: error while parsing precision: error while converting precision to integer: value out of range
        %.f | 1 | could not parse formatting clause: error while parsing precision: error while converting precision to integer: invalid syntax
        %.s | 1 | could not parse formatting clause: error while parsing precision: error while converting precision to integer: invalid syntax
        %. | 1 | could not parse formatting clause: error while parsing precision: could not find end of precision specifier
        %.5 | 1 | could not parse formatting clause: error while parsing precision: could not find end of precision specifier
        %.101z | 1 | could not parse formatting clause: error while parsing precision: precision 101 exceeds maximum allowed precision 100
        """;

    public static TheoryData<string, string, string> ErrorRows => RowData();

    [Theory]
    [MemberData(nameof(ErrorRows))]
    public static void Format_WithAnError_IsInvalidArgumentAtTheCall(string format, string count, string detail)
    {
        const string Call = "f.format(items)";
        CelValue[] values = new CelValue[int.Parse(count, CultureInfo.InvariantCulture)];
        Array.Fill(values, CelValue.FromInt64(1));

        CelError error = Assert.Single(Evaluate(Call, Bindings(format, values)).Errors);

        Assert.Equal(
            (CelErrorCode.InvalidArgument, CelErrorStage.Evaluate, "invalid argument to 'format': " + detail, Call.IndexOf('(', StringComparison.Ordinal)),
            (error.Code, error.Stage, error.Message, error.Location.Offset));
    }

    // A letter that is a lone surrogate, which only a string of the application can hold, is named by its unit.
    [Fact]
    public static void UnknownLetter_OfALoneSurrogate_IsNamedByItsUnit()
    {
        string letter = ((char)0xD83D).ToString();

        CelError error = Assert.Single(Evaluate("f.format(items)", Bindings("%" + letter, CelValue.FromInt64(1))).Errors);

        Assert.Equal("invalid argument to 'format': " + Clause + "unrecognized formatting clause \"" + letter + "\"", error.Message);
    }

    // The text before the clause that fails is written and charged, but the call has no result: the error is the value, and it is the
    // first one, so a later clause is not read.
    [Fact]
    public static void FirstError_EndsTheCall()
    {
        CelError error = Assert.Single(Evaluate("f.format(items)", Bindings("%d%z%.101f", CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3))).Errors);

        Assert.Equal("invalid argument to 'format': " + Clause + "unrecognized formatting clause \"z\"", error.Message);
    }

    // An error of a clause on a kind comes in the order of the clauses: the first clause that fails is the error.
    [Fact]
    public static void FirstClauseThatFails_IsTheError()
    {
        CelError error = Assert.Single(Evaluate("f.format(items)", Bindings("%d %d %d", CelValue.FromInt64(1), CelValue.FromString("a"), CelValue.FromBoolean(true))).Errors);

        Assert.Equal(
            "invalid argument to 'format': error during formatting: decimal clause can only be used on ints, uints, and doubles, was given string",
            error.Message);
    }

    // A value that is not a list or a string is no matching overload at evaluation, as for any function of a dyn argument.
    [Fact]
    public static void ListThatIsNotAList_IsNoMatchingOverload()
    {
        CelBindings bindings = new CelBindings().Add("v", CelValue.FromInt64(1));

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(Evaluate("'%d'.format(v)", bindings).Errors).Code);
    }

    // The error value holds a number and references, not a text, so the message is formed when it is read, and it is the same
    // each time.
    [Fact]
    public static void Message_IsTheSameEachTimeItIsRead()
    {
        CelError error = Assert.Single(Evaluate("f.format(items)", Bindings("%.101f", CelValue.FromInt64(1))).Errors);

        string first = error.Message;
        string second = error.Message;

        Assert.Equal(first, second);
        Assert.Contains("precision 101 exceeds", first, StringComparison.Ordinal);
    }

    private static TheoryData<string, string, string> RowData()
    {
        TheoryData<string, string, string> data = [];
        foreach (string row in Rows.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1], cells[2]);
        }

        return data;
    }
}
