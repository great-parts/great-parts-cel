using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Lists.ListsEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Lists;

// base64.decode and base64.encode of the set encoders on what the cases of the specification leave: the two attempts of the decoder
// with the line breaks, the padding and the bits that the last character leaves unused, the offsets of the errors, and the lengths
// of the data around the groups of three bytes and the chunks of the encoder. Each row of the first table is an expression that is
// true.
public sealed class Base64FunctionTests
{
    private const string Holds = """
        base64.decode('aGVsbG9=') == b'hello'
        base64.decode('') == b''
        base64.decode('\n') == b''
        base64.decode('YQ==') == b'a'
        base64.decode('YQ') == b'a'
        base64.decode('YWI=') == b'ab'
        base64.decode('YWI') == b'ab'
        base64.decode('YWJj') == b'abc'
        base64.decode('YW\nJj') == b'abc'
        base64.decode('YW\r\nJj\r\n') == b'abc'
        base64.decode('YQ=\r\n=') == b'a'
        base64.decode('YQ==\n') == b'a'
        base64.decode('+/+/') == b'\xfb\xff\xbf'
        base64.encode(b'') == ''
        base64.encode(b'a') == 'YQ=='
        base64.encode(b'ab') == 'YWI='
        base64.encode(b'\xfb\xff\xbf') == '+/+/'
        base64.decode(base64.encode(b'any carnal pleas')) == b'any carnal pleas'
        """;

    // "expression | offset": the second attempt, without padding, is the one whose error the call has.
    private const string Offsets = """
        base64.decode('a') | 0
        base64.decode('aGVsbG8==') | 7
        base64.decode('aGVs bG8=') | 4
        base64.decode('aGVsbG8=aGVsbG8=') | 7
        base64.decode('aGVsbG8*') | 7
        base64.decode('=') | 0
        base64.decode('YQ=') | 2
        base64.decode('-_-_') | 0
        base64.decode('é') | 0
        """;

    public static TheoryData<string> HoldingCases => HoldingData();

    public static TheoryData<string, string> OffsetCases => OffsetData();

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    [Theory]
    [MemberData(nameof(OffsetCases))]
    public static void IllegalData_NamesTheOffsetWhereTheSecondAttemptStopped(string expression, string offset)
    {
        CelError error = Assert.Single(Evaluate(expression).Errors);

        Assert.Equal(CelErrorCode.InvalidArgument, error.Code);
        Assert.Equal("invalid argument to 'base64.decode': illegal base64 data at input byte " + offset, error.Message);
    }

    // The encoder writes the same text as Convert for every length from 0 to 200, which crosses the groups of three bytes and the chunks
    // of 96 that the JSON encoder writes bytes in, and the decoder reads it back with and without its padding.
    [Fact]
    public static void EveryLength_RoundTripsWithAndWithoutPadding()
    {
        for (int length = 0; length <= 200; length++)
        {
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = (byte)((i * 37) + length);
            }

            string expected = Convert.ToBase64String(bytes);
            CelBindings data = new CelBindings().Add("x", CelValue.FromBytes(bytes));

            Assert.Equal(expected, Text("base64.encode(x)", data));
            Assert.Equal(bytes, Bytes("base64.decode(x)", new CelBindings().Add("x", expected)));
            Assert.Equal(bytes, Bytes("base64.decode(x)", new CelBindings().Add("x", expected.TrimEnd('='))));
        }
    }

    private static string Text(string expression, CelBindings bindings)
    {
        Assert.True(Evaluate(expression, bindings).GetValueOrThrow().TryGetString(out string? text));
        return text;
    }

    private static byte[] Bytes(string expression, CelBindings bindings)
    {
        Assert.True(Evaluate(expression, bindings).GetValueOrThrow().TryGetBytes(out ReadOnlyMemory<byte> bytes));
        return bytes.ToArray();
    }

    private static TheoryData<string> HoldingData()
    {
        TheoryData<string> data = [];
        foreach (string line in Lines(Holds))
        {
            data.Add(line);
        }

        return data;
    }

    private static TheoryData<string, string> OffsetData()
    {
        TheoryData<string, string> data = [];
        foreach (string line in Lines(Offsets))
        {
            string[] cells = line.Split(" | ");
            data.Add(cells[0], cells[1]);
        }

        return data;
    }

    private static string[] Lines(string table) => table.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
