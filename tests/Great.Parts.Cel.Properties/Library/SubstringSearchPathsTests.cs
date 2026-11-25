using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Library;

// The search for a substring of contains and of the functions indexOf, lastIndexOf, replace and split of the set strings
// (docs/design/tests.md, "Substring search of the functions of strings"): a pattern that is a literal of the expression is searched
// by the values that compilation prepared, a pattern from the data by the ordinal search, and on each input both ways give the answer
// that a search written here over code points gives and cost what the formula says: 1 + ceil(La / 10) · q and the charge of the result,
// with q = max(1, ceil(m / 64)), m the length of the pattern in UTF-16 code units, and the read of each variable 1. A match counts only
// at a code point boundary. The inputs are random strings over small alphabets, with lone surrogates among them, patterns cut from
// the text and not, from none to about 1 100 code units, and the strings on which a search of .NET is slowest. No time is measured.
[Trait("Requirement", "R-3")]
[Trait("Requirement", "F-1")]
public sealed class SubstringSearchPathsTests
{
    private const ulong Seed = 20261125;
    private const int RandomCases = 300;
    private const int LongCases = 30;

    // What the reference gives for an offset outside the string, which the function reports as an error.
    private const long OutOfRange = -2;

    private static readonly string LoneHigh = ((char)0xD83D).ToString();
    private static readonly string LoneLow = ((char)0xDE00).ToString();

    // The alphabets of the random strings: two and three code units, pairs, and lone surrogates beside pairs.
    private static readonly string[][] Alphabets =
    [
        ["a", "b"],
        ["a", "b", "c"],
        ["a", "😀"],
        ["😀", "😁", "a"],
        ["a", LoneLow, "b"],
        ["a", LoneHigh, "😀"],
    ];

    private static readonly Gen<SearchCase> RandomCase =
        from tokens in Gen.OneOf(Alphabets)
        from text in Gen.OneOf(tokens).Array[0, 120]
        from replacement in Gen.OneOf("zz", string.Empty, "é😀", "a")
        from kind in Gen.Int[0, 3]
        from start in Gen.Int[0, 120]
        from length in Gen.Int[0, 8]
        from other in Gen.OneOf(tokens).Array[0, 6]
        select new SearchCase("random", string.Concat(text), ShortPattern(text, other, kind, start, length), replacement);

    // Long texts over two units, with a pattern cut from the text or the same pattern with its last unit made one that the text lacks.
    private static readonly Gen<SearchCase> LongCase =
        from tokens in Gen.OneOf(Alphabets[0], Alphabets[2])
        from text in Gen.OneOf(tokens).Array[100, 1300]
        from replacement in Gen.OneOf("zz", string.Empty)
        from start in Gen.Int[0, 300]
        from length in Gen.Int[0, 1100]
        from absent in Gen.Bool
        select new SearchCase("long", string.Concat(text), LongPattern(text, start, length, absent), replacement);

    private static readonly CelEnvironment SearchEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddVariable<string>("s")
        .AddVariable<string>("t")
        .AddVariable<string>("r")
        .AddVariable<long>("n")
        .Build()
        .GetEnvironmentOrThrow();

    // The programs that read the pattern from a variable, which the cases share; a program with a literal pattern is made for its case.
    private static readonly ConcurrentDictionary<string, CelProgram<CelValue>> Programs = new(StringComparer.Ordinal);

    // The limits of replace and split: all, 0, 1, 2 and more than the matches. They come before the forms, which read them.
    private static readonly long[] LimitValues = [-1, 0, 1, 2, 1_000_000_000];

    // The tail of the call, the number of variables it reads, whether its pattern and result are a string and what its optional
    // number is: an offset or a limit.
    private static readonly Form[] Forms =
    [
        new("contains", string.Empty, 2, false, null, static (input, _) => new Reference(ReferenceIndexOf(input.Text, input.Pattern, 0) >= 0 ? "true" : "false", 0)),
        new("indexOf", string.Empty, 2, false, null, IndexOfReference),
        new("indexOf", ", n", 3, false, Offsets, IndexOfReference),
        new("lastIndexOf", string.Empty, 2, false, null, LastIndexOfReference),
        new("lastIndexOf", ", n", 3, false, Offsets, LastIndexOfReference),
        new("replace", ", r", 3, true, null, ReplaceReference),
        new("replace", ", r, n", 4, true, static _ => LimitValues, ReplaceReference),
        new("split", string.Empty, 2, false, null, SplitReference),
        new("split", ", n", 3, false, static _ => LimitValues, SplitReference),
    ];

    private static readonly int[] PairCounts = [30, 31, 32, 63, 64, 511, 512, 513];

    private static readonly int[] PatternLengths = [1, 63, 64, 65, 127, 128, 129, 1023, 1024, 1025, 1100];

    [Fact]
    public static void RandomInputs_BothPathsGiveTheReferenceAnswerAndTheCostOfTheFormula() =>
        Check("SubstringSearchPathsTests.RandomInputs", RandomCase, RandomCases);

    [Fact]
    public static void LongPatterns_BothPathsGiveTheReferenceAnswerAndTheCostOfTheFormula() =>
        Check("SubstringSearchPathsTests.LongPatterns", LongCase, LongCases);

    [Fact]
    public static void ChosenInputs_BothPathsGiveTheReferenceAnswerAndTheCostOfTheFormula()
    {
        foreach (SearchCase input in ChosenInputs())
        {
            if (InputViolation(input) is { } reason)
            {
                Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"SubstringSearchPathsTests.ChosenInputs: input {input.Name}: {reason}"));
            }
        }
    }

    private static void Check(string property, Gen<SearchCase> generator, int cases) =>
        PropertyLoop.Check(property, generator, Seed, cases, InputViolation, Show);

    // The inputs on which a search of .NET is slowest, patterns on both sides of 64, 128 and 1 024 code units, and the edges of a
    // code point.
    private static List<SearchCase> ChosenInputs()
    {
        string abab = Repeat("ab", 5000);
        string manyA = new('a', 3000);
        string pairs = Repeat("😀😁", 600);
        List<SearchCase> chosen =
        [
            new("an empty text and an empty pattern", string.Empty, string.Empty, "zz"),
            new("a pattern longer than the text", "abab", "ababa", "zz"),
            new("surrogate pairs", pairs, Repeat("😀😁", 40) + "😂", "zz"),
            new("a pair that ends the text", pairs, "😁😀😁", "zz"),
            new("a lone low surrogate against pairs", pairs, LoneLow, "zz"),
            new("a lone high surrogate against pairs", pairs, LoneHigh, "zz"),
            new("a lone low surrogate between the units of a pattern", pairs, "😀" + LoneLow, "zz"),
        ];

        foreach (int pairsOfUnits in PairCounts)
        {
            chosen.Add(new(string.Create(CultureInfo.InvariantCulture, $"(ab)^5000 with (ab)^{pairsOfUnits} bb"), abab, Repeat("ab", pairsOfUnits) + "bb", "zz"));
        }

        foreach (int length in PatternLengths)
        {
            chosen.Add(new(string.Create(CultureInfo.InvariantCulture, $"a^3000 with a^{length - 1} b"), manyA, new string('a', length - 1) + "b", "zz"));
            chosen.Add(new(string.Create(CultureInfo.InvariantCulture, $"a^3000 with a^{length}"), manyA, new string('a', length), "zz"));
        }

        return chosen;
    }

    // The reason why an input breaks the property, or null: every form of every function, with each of its numbers.
    private static string? InputViolation(SearchCase input)
    {
        foreach (Form form in Forms)
        {
            if (FormViolation(form, input) is { } reason)
            {
                return reason;
            }
        }

        return null;
    }

    private static string? FormViolation(Form form, SearchCase input)
    {
        CelProgram<CelValue> fromData = Programs.GetOrAdd("s." + form.Method + "(t" + form.Tail + ")", Compile);
        CelProgram<CelValue>? literal = HasLoneSurrogate(input.Pattern)
            ? null
            : Compile("s." + form.Method + "('" + input.Pattern + "'" + form.Tail + ")");
        long?[] numbers = form.Numbers is null ? [null] : [.. form.Numbers(input).Select(static number => (long?)number)];

        foreach (long? number in numbers)
        {
            Reference expected = form.Expected(input, number);
            ulong q = Math.Max(1UL, (((ulong)input.Pattern.Length) + 63) / 64);
            ulong strings = (ulong)(input.Text.Length + input.Pattern.Length) + (form.Replaces ? (ulong)input.Replacement.Length : 0);
            ulong cost = (ulong)form.Reads + 1 + (Tenths(strings) * q) + expected.Charge;
            CelBindings bindings = new CelBindings()
                .Add("s", input.Text)
                .Add("t", input.Pattern)
                .Add("r", input.Replacement)
                .Add("n", number ?? 0);

            string label = string.Create(CultureInfo.InvariantCulture, $"{form.Method}{form.Tail} with the number {number}");
            if (Verdict(label + ", the pattern from the data", Evaluate(fromData, bindings), expected, cost) is { } reason)
            {
                return reason;
            }

            // The pattern that is a literal is no variable: the call costs one read less.
            if (literal is not null && Verdict(label + ", the literal pattern", Evaluate(literal, bindings), expected, cost - 1) is { } literalReason)
            {
                return literalReason;
            }
        }

        return null;
    }

    private static CelProgram<CelValue> Compile(string expression) => SearchEnvironment.Compile<CelValue>(expression).GetProgramOrThrow();

    private static CelEvalResult<CelValue> Evaluate(CelProgram<CelValue> program, CelBindings bindings) =>
        program.Evaluate(bindings, new CelEvalOptions { CostLimit = ulong.MaxValue }, CancellationToken.None);

    // The reason why a result is not the reference's: an error where there should be none or the other way round, another value, or
    // another cost. An offset outside the string is the error IndexOutOfRange, whose cost this property does not look at.
    private static string? Verdict(string label, CelEvalResult<CelValue> actual, Reference expected, ulong cost)
    {
        if (expected.Rendered is null)
        {
            return !actual.IsSuccess && actual.Errors.Length == 1 && actual.Errors[0].Code == CelErrorCode.IndexOutOfRange
                ? null
                : label + ": the offset is outside the string, and the result is not the error IndexOutOfRange";
        }

        if (!actual.IsSuccess)
        {
            return label + ": the call fails with " + actual.Errors[0].Code;
        }

        string rendered = Render(actual.GetValueOrThrow());
        if (!string.Equals(rendered, expected.Rendered, StringComparison.Ordinal))
        {
            return label + ": the result is " + rendered + ", the reference gives " + expected.Rendered;
        }

        return actual.Cost == cost
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{label}: the cost is {actual.Cost}, the formula gives {cost}");
    }

    // The text of a result that tells a bool, an int, a string and a list of strings apart.
    private static string Render(CelValue value)
    {
        if (value.TryGetBoolean(out bool flag))
        {
            return flag ? "true" : "false";
        }

        if (value.TryGetInt64(out long number))
        {
            return RenderInt(number);
        }

        if (value.TryGetString(out string? text))
        {
            return RenderString(text);
        }

        if (value.TryGetList(out CelList? list))
        {
            var builder = new StringBuilder("[");
            for (int i = 0; i < list.Count; i++)
            {
                builder.Append(Render(list[i])).Append('|');
            }

            return builder.Append(']').ToString();
        }

        return "?";
    }

    private static string RenderInt(long number) => "int:" + number.ToString(CultureInfo.InvariantCulture);

    private static string RenderString(string text) => "string:" + text;

    private static string RenderList(IEnumerable<string> parts) => "[" + string.Concat(parts.Select(static part => RenderString(part) + "|")) + "]";

    // The offsets of indexOf and lastIndexOf: below 0, at the start, in the middle, at the end and past the end, in code points.
    private static long[] Offsets(SearchCase input)
    {
        long count = Decode(input.Text).Length;
        return [-1, 0, count / 2, count, count + 1];
    }

    private static Reference IndexOfReference(SearchCase input, long? number)
    {
        long index = ReferenceIndexOf(input.Text, input.Pattern, number ?? 0);
        return index == OutOfRange ? new(null, 0) : new(RenderInt(index), 0);
    }

    private static Reference LastIndexOfReference(SearchCase input, long? number)
    {
        long index = ReferenceLastIndexOf(input.Text, input.Pattern, number);
        return index == OutOfRange ? new(null, 0) : new(RenderInt(index), 0);
    }

    // The result of replace is charged by its length, ceil(L / 10), as it is built; a receiver that comes back is charged as a copy.
    private static Reference ReplaceReference(SearchCase input, long? number)
    {
        string result = ReferenceReplace(input.Text, input.Pattern, input.Replacement, number ?? -1);
        return new(RenderString(result), Tenths((ulong)result.Length));
    }

    // The result of split is charged 1 for each part and ceil(T / 10) for the units T of all the parts.
    private static Reference SplitReference(SearchCase input, long? number)
    {
        List<string> parts = ReferenceSplit(input.Text, input.Pattern, number ?? -1);
        return new(RenderList(parts), (ulong)parts.Count + Tenths((ulong)parts.Sum(static part => part.Length)));
    }

    private static ulong Tenths(ulong length) => (length + 9) / 10;

    // The index in code points of the first occurrence at or after the offset, as the page of the set says: an empty pattern is at the
    // offset, a non-empty one is not at the end, an offset outside the string is OutOfRange.
    private static long ReferenceIndexOf(string text, string pattern, long offset)
    {
        int[] t = Decode(text);
        int[] p = Decode(pattern);
        if (offset < 0 || offset > t.Length)
        {
            return OutOfRange;
        }

        if (p.Length == 0)
        {
            return offset;
        }

        for (int start = (int)offset; start + p.Length <= t.Length; start++)
        {
            if (Matches(t, start, p))
            {
                return start;
            }
        }

        return -1;
    }

    // The last occurrence: with an offset, the last that starts at or before it; the empty pattern gives the offset, or the number of
    // code points without one; at the offset equal to the length a non-empty pattern is not found.
    private static long ReferenceLastIndexOf(string text, string pattern, long? offset)
    {
        int[] t = Decode(text);
        int[] p = Decode(pattern);
        long from = t.Length - p.Length;
        if (offset is long given)
        {
            if (given < 0 || given > t.Length)
            {
                return OutOfRange;
            }

            if (p.Length == 0)
            {
                return given;
            }

            if (given == t.Length)
            {
                return -1;
            }

            from = Math.Min(given, from);
        }
        else if (p.Length == 0)
        {
            return t.Length;
        }

        for (long start = from; start >= 0; start--)
        {
            if (Matches(t, (int)start, p))
            {
                return start;
            }
        }

        return -1;
    }

    // Go's Replace: at most limit replacements, all below 0, none for 0; an empty old string is inserted before each code point and at the
    // end, up to the limit.
    private static string ReferenceReplace(string text, string oldText, string newText, long limit)
    {
        if (limit == 0)
        {
            return text;
        }

        int[] t = Decode(text);
        int[] o = Decode(oldText);
        StringBuilder result = new();
        if (o.Length == 0)
        {
            long inserted = 0;
            int position = 0;
            while (true)
            {
                if (limit > 0 && inserted == limit)
                {
                    AppendPoints(result, t, position, t.Length);
                    break;
                }

                result.Append(newText);
                inserted++;
                if (position == t.Length)
                {
                    break;
                }

                AppendPoints(result, t, position, position + 1);
                position++;
            }

            return result.ToString();
        }

        long replaced = 0;
        for (int i = 0; i < t.Length;)
        {
            if ((limit < 0 || replaced < limit) && Matches(t, i, o))
            {
                result.Append(newText);
                i += o.Length;
                replaced++;
            }
            else
            {
                AppendPoints(result, t, i, i + 1);
                i++;
            }
        }

        return result.ToString();
    }

    // Go's SplitN: at most limit parts, the last holding the rest, all of them below 0 and none for 0; an empty separator splits into code
    // points and an empty string gives no part.
    private static List<string> ReferenceSplit(string text, string separator, long limit)
    {
        List<string> parts = [];
        if (limit == 0)
        {
            return parts;
        }

        int[] t = Decode(text);
        int[] s = Decode(separator);
        if (s.Length == 0)
        {
            for (int i = 0; i < t.Length; i++)
            {
                if (limit > 0 && parts.Count == limit - 1)
                {
                    parts.Add(Encode(t, i, t.Length));
                    break;
                }

                parts.Add(Encode(t, i, i + 1));
            }

            return parts;
        }

        int start = 0;
        int index = 0;
        while (index + s.Length <= t.Length && (limit < 0 || parts.Count < limit - 1))
        {
            if (Matches(t, index, s))
            {
                parts.Add(Encode(t, start, index));
                index += s.Length;
                start = index;
            }
            else
            {
                index++;
            }
        }

        parts.Add(Encode(t, start, t.Length));
        return parts;
    }

    private static bool Matches(int[] text, int at, int[] pattern)
    {
        if (at + pattern.Length > text.Length)
        {
            return false;
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            if (text[at + i] != pattern[i])
            {
                return false;
            }
        }

        return true;
    }

    // The code points of a string: a pair is one, and every other code unit, a lone surrogate included, is one.
    private static int[] Decode(string text)
    {
        List<int> points = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                points.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                i++;
            }
            else
            {
                points.Add(text[i]);
            }
        }

        return [.. points];
    }

    private static string Encode(int[] points, int start, int end)
    {
        StringBuilder result = new();
        AppendPoints(result, points, start, end);
        return result.ToString();
    }

    private static void AppendPoints(StringBuilder result, int[] points, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (points[i] >= 0x10000)
            {
                result.Append(char.ConvertFromUtf32(points[i]));
            }
            else
            {
                result.Append((char)points[i]);
            }
        }
    }

    // Whether a string holds a surrogate that is not half of a pair: no literal of the expression can.
    private static bool HasLoneSurrogate(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    // A pattern of a random case: units cut from the text for the first two kinds, others for the rest.
    private static string ShortPattern(string[] text, string[] other, int kind, int start, int length) =>
        kind >= 2 || text.Length == 0
            ? string.Concat(other)
            : string.Concat(text.Skip(start % text.Length).Take(length));

    // A pattern of a long case: units cut from the text, with the last one replaced by one that the text lacks when absent is true.
    private static string LongPattern(string[] text, int start, int length, bool absent)
    {
        string[] cut = [.. text.Skip(start % text.Length).Take(length)];
        return absent && cut.Length > 0 ? string.Concat(cut[..^1]) + "c" : string.Concat(cut);
    }

    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static string Show(SearchCase input) =>
        string.Create(CultureInfo.InvariantCulture, $"text \"{input.Text}\", pattern \"{input.Pattern}\", replacement \"{input.Replacement}\"");

    private sealed record SearchCase(string Name, string Text, string Pattern, string Replacement);

    // What the reference gives: the text of the result, null for the error IndexOutOfRange, and the units that the result is charged.
    private sealed record Reference(string? Rendered, ulong Charge);

    // One form of a function: the method, the tail of its arguments after the pattern, the variables that the form reads when the pattern
    // is one, whether the replacement is an argument, the numbers to call it with, and the reference.
    private sealed record Form(
        string Method,
        string Tail,
        int Reads,
        bool Replaces,
        Func<SearchCase, long[]>? Numbers,
        Func<SearchCase, long?, Reference> Expected);

}
