using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using CsCheck;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Syntax;

// Bytes of an untrusted message (docs/design/tests.md, "Tests of the public tree and the protobuf form"): random bytes and the bytes of
// valid messages with a changed, deleted, inserted or repeated byte, or cut short, give a result and no exception through each entry
// point that reads bytes, and no error is InternalError; a nesting of 100 000 levels is NestingTooDeep without a stack overflow; a
// message of 64 KB is read within 1 s on the reference machine, as WorstInputTests judges it.
public sealed class UntrustedBytesTests
{
    private const ulong DamagedBytesSeed = 20261123;
    private const int DamagedBytesCases = 1000;
    private const int NestingDepth = 100_000;
    private const int MessageLimit = 65536;

    private const byte ParsedExprExpressionTag = 0x12;
    private const byte CheckedExprExpressionTag = 0x22;

    private static readonly CelEnvironment Environment = TypedExpressions.Environment;

    private static readonly SyntaxOptions WithOptionalSyntax = SyntaxOptions.Standard with { OptionalSyntax = true };

    private static readonly string[] Kinds = ["changed byte", "deleted byte", "inserted byte", "repeated byte", "cut short"];

    // Random bytes, short and up to 64 KB; the bytes of a grammatical parsed expression and of a well-typed checked one, damaged.
    private static readonly Gen<BytesCase> DamagedBytesCase = Gen.Frequency(
        (3, Gen.Byte.Array[0, 200].Select(bytes => new BytesCase(bytes, null, "random bytes"))),
        (1, Gen.Byte.Array[0, MessageLimit].Select(bytes => new BytesCase(bytes, null, "random bytes"))),
        (4, Gen.Select(Grammar.Expression, Gen.Int[0, int.MaxValue], Gen.Int[0, 4], Gen.Byte, ParsedMessage)),
        (3, Gen.Select(TypedExpressions.WellTyped, Gen.Int[0, int.MaxValue], Gen.Int[0, 4], Gen.Byte, CheckedMessage)));

    // Every entry point that reads bytes returns a result: no exception escapes, no error is InternalError, every error lies within
    // the text when there is one, and a tree that loaded compiles to a program or to errors of the same kind.
    [Fact]
    [Trait("Requirement", "R-10")]
    [Trait("Requirement", "F-20")]
    public void DamagedBytes_GiveAResultAndNoException() =>
        PropertyLoop.Check(nameof(DamagedBytes_GiveAResultAndNoException), DamagedBytesCase, DamagedBytesSeed, DamagedBytesCases, Violation, Show);

    [Theory]
    [Trait("Requirement", "R-10")]
    [Trait("Requirement", "F-20")]
    [InlineData(ParsedExprExpressionTag)]
    [InlineData(CheckedExprExpressionTag)]
    public static void NestingOf100000Levels_IsNestingTooDeepWithoutAStackOverflow(byte expressionTag)
    {
        byte[] bytes = NestedNots(NestingDepth, expressionTag);

        CelParseResult result = expressionTag == ParsedExprExpressionTag
            ? CelParsedExpr.FromProtobuf(bytes)
            : CelParsedExpr.FromCheckedExprProtobuf(bytes);

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.NestingTooDeep, CelErrorStage.Parse), (error.Code, error.Stage));
    }

    [Theory]
    [Trait("Requirement", "R-10")]
    [InlineData("random bytes 64 KB")]
    [InlineData("list 64 KB")]
    public static void MessageOf64Kilobytes_IsReadWithinOneSecond(string input)
    {
        (byte[] bytes, string? text) = Message(input);
        Assert.True(bytes.Length <= MessageLimit, $"{input}: {bytes.Length} bytes");

        double slowest = 0;
        foreach ((string reader, Func<CelParseResult> read) in Readers(bytes, text))
        {
            _ = read();
            var stopwatch = Stopwatch.StartNew();
            CelParseResult result = read();
            double seconds = stopwatch.Elapsed.TotalSeconds;

            Assert.DoesNotContain(result.Errors, error => error.Code == CelErrorCode.InternalError);
            TestContext.Current.TestOutputHelper?.WriteLine(WorstInputTests.Time(input + " by " + reader, seconds));
            slowest = Math.Max(slowest, seconds);
        }

        if (WorstInputTests.Verdict(input, slowest, WorstInputTests.IsReferenceMachine(System.Environment.GetEnvironmentVariable)) is { } failure)
        {
            Assert.Fail(failure);
        }
    }

    // Random bytes from a fixed seed, and a valid message of a list of constants that fills the limit.
    private static (byte[] Bytes, string? Text) Message(string input)
    {
        if (input == "random bytes 64 KB")
        {
            PCG random = new(0, DamagedBytesSeed);
            return ([.. Enumerable.Range(0, MessageLimit).Select(_ => (byte)random.Next(256u))], null);
        }

        // The size of an element is measured on a short list; the long list takes the number of elements that leaves 1000 bytes of room.
        (byte[] sample, _) = ListOfOnes(1000);
        int count = (int)((MessageLimit - 1000) / ((double)sample.Length / 1000));
        return ListOfOnes(count);
    }

    private static (byte[] Bytes, string? Text) ListOfOnes(int count)
    {
        string text = "[" + string.Join(", ", Enumerable.Repeat("1", count)) + "]";
        return (Environment.Parse(text, WithOptionalSyntax).GetParsedExprOrThrow().ToProtobuf(), text);
    }

    private static BytesCase ParsedMessage(string text, int position, int kind, byte value)
    {
        CelParseResult parsed = Environment.Parse(text, WithOptionalSyntax);
        byte[] bytes = parsed.IsSuccess ? parsed.GetParsedExprOrThrow().ToProtobuf() : [];
        return new BytesCase(Damaged(bytes, position, kind, value), text, "parsed expression, " + Kinds[kind]);
    }

    private static BytesCase CheckedMessage(TypedCase testCase, int position, int kind, byte value)
    {
        CelCheckedExpr? checkedExpr = Environment.Validate<CelValue>(testCase.Text).CheckedExpr;
        byte[] bytes = checkedExpr?.ToProtobuf() ?? [];
        return new BytesCase(Damaged(bytes, position, kind, value), testCase.Text, "checked expression, " + Kinds[kind]);
    }

    // One byte of the message changed, deleted, inserted or repeated, or the message cut short.
    private static byte[] Damaged(byte[] bytes, int position, int kind, byte value)
    {
        if (bytes.Length == 0)
        {
            return bytes;
        }

        int at = position % bytes.Length;
        return kind switch
        {
            0 => [.. bytes[..at], (byte)(bytes[at] ^ (value | 1)), .. bytes[(at + 1)..]],
            1 => [.. bytes[..at], .. bytes[(at + 1)..]],
            2 => [.. bytes[..at], value, .. bytes[at..]],
            3 => [.. bytes[..(at + 1)], .. bytes[at..]],
            _ => bytes[..at],
        };
    }

    private static IEnumerable<(string Name, Func<CelParseResult> Read)> Readers(byte[] bytes, string? text)
    {
        yield return ("FromProtobuf", () => CelParsedExpr.FromProtobuf(bytes));
        yield return ("FromCheckedExprProtobuf", () => CelParsedExpr.FromCheckedExprProtobuf(bytes));
        if (text is not null)
        {
            yield return ("FromProtobuf with the text", () => CelParsedExpr.FromProtobuf(bytes, text));
            yield return ("FromCheckedExprProtobuf with the text", () => CelParsedExpr.FromCheckedExprProtobuf(bytes, text));
        }
    }

    private static string? Violation(BytesCase testCase)
    {
        foreach ((string reader, Func<CelParseResult> read) in Readers(testCase.Bytes, testCase.Text))
        {
            CelParseResult result;
            try
            {
                result = read();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return $"{reader}: {exception.GetType().Name} escaped: {exception.Message}";
            }

            string? reason = Invalid(result, testCase.Text) ?? Compiled(result, testCase.Text);
            if (reason is not null)
            {
                return $"{reader}: {reason}";
            }
        }

        return null;
    }

    // The result of a read: a tree or errors; no error is InternalError; every error is of the stage Parse and lies within the text.
    private static string? Invalid(CelParseResult result, string? text)
    {
        if (result.IsSuccess != result.Errors.IsEmpty)
        {
            return "the result has both a tree and errors, or neither";
        }

        foreach (CelError error in result.Errors)
        {
            string? reason = Wrong(error, text);
            if (reason is not null || error.Stage != CelErrorStage.Parse)
            {
                return reason ?? $"{error.Code} is of the stage {error.Stage}";
            }
        }

        return null;
    }

    // A tree that loaded compiles in the environment of the generator: no exception, and no error is InternalError.
    private static string? Compiled(CelParseResult result, string? text)
    {
        if (!result.IsSuccess)
        {
            return null;
        }

        try
        {
            CelCompileResult<CelValue> compiled = Environment.CompileParsed<CelValue>(result.GetParsedExprOrThrow());
            return compiled.Errors.Select(error => Wrong(error, text)).FirstOrDefault(reason => reason is not null) is { } reason
                ? "CompileParsed: " + reason
                : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"CompileParsed: {exception.GetType().Name} escaped: {exception.Message}";
        }
    }

    private static string? Wrong(CelError error, string? text)
    {
        if (error.Code == CelErrorCode.InternalError)
        {
            return $"InternalError: {error.Message}";
        }

        return error.Location.Offset < 0 || (text is not null && error.Location.Offset > text.Length)
            ? string.Create(CultureInfo.InvariantCulture, $"{error.Code} at {error.Location.Offset} is outside the text")
            : null;
    }

    private static string Show(BytesCase testCase) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{testCase.Kind}, {testCase.Bytes.Length} bytes {Convert.ToHexString(testCase.Bytes.AsSpan(0, Math.Min(testCase.Bytes.Length, 400)))}, text {testCase.Text}");

    // The message ParsedExpr or CheckedExpr whose expression is `depth` nested calls of !_ around an identifier, written from the bytes
    // without recursion: the sizes of the messages come from the inside out and the bytes from the outside in.
    private static byte[] NestedNots(int depth, byte expressionTag)
    {
        long[] sizes = new long[depth + 1];
        byte[] leaf = [0x22, 0x03, 0x0A, 0x01, (byte)'x'];
        sizes[0] = leaf.Length;
        for (int level = 1; level <= depth; level++)
        {
            long call = 4 + 1 + VarintSize(sizes[level - 1]) + sizes[level - 1];
            sizes[level] = 1 + VarintSize(call) + call;
        }

        var bytes = new List<byte>((int)(sizes[depth] + 8)) { expressionTag };
        AddVarint(bytes, sizes[depth]);
        for (int level = depth; level >= 1; level--)
        {
            long call = 4 + 1 + VarintSize(sizes[level - 1]) + sizes[level - 1];
            bytes.Add(0x32);
            AddVarint(bytes, call);
            bytes.AddRange([0x12, 0x02, (byte)'!', (byte)'_', 0x1A]);
            AddVarint(bytes, sizes[level - 1]);
        }

        bytes.AddRange(leaf);
        return [.. bytes];
    }

    private static int VarintSize(long value)
    {
        int size = 1;
        for (ulong rest = (ulong)value; rest >= 0x80; rest >>= 7)
        {
            size++;
        }

        return size;
    }

    private static void AddVarint(List<byte> bytes, long value)
    {
        ulong rest = (ulong)value;
        while (rest >= 0x80)
        {
            bytes.Add((byte)(rest | 0x80));
            rest >>= 7;
        }

        bytes.Add((byte)rest);
    }

    /// <summary>The bytes of a case, the text they were made from, if any, and what was done to them.</summary>
    /// <param name="Bytes">The bytes.</param>
    /// <param name="Text">The text of the expression, or <see langword="null"/> for bytes that come from no text.</param>
    /// <param name="Kind">What the case is, for the failure message.</param>
    private sealed record BytesCase(byte[] Bytes, string? Text, string Kind);
}
