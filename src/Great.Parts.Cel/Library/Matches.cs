using System;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Regex;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// matches, (string, string) -> bool, and matches_string, string.matches(string) -> bool: whether some part of the subject matches the
// pattern, by RE2 syntax and semantics. One implementation serves both: the subject is the first argument, the receiver counting as one,
// and the pattern the second.
//
// A pattern that is a string literal is prepared when the expression is checked: its program is built once and arrives as the
// prepared object of the call site, and a call pays only the match. Any other pattern is prepared at evaluation, through the cache of
// patterns of the environment, and pays three charges, each before the work it pays for: the parse 1 + ⌈L/10⌉, the build ⌈m/4⌉ and the
// match 1 + ⌈(n + 1) · m/10⌉, with L and n the lengths of the pattern and the subject in UTF-16 code units and m the size of the program. A
// pattern that the parse refuses pays the parse alone. The cache changes no charge: a hit pays what a miss pays. The charge of the
// match comes before the build, since m is known after the parse, so that a limit that stops the call at the match leaves nothing in
// the cache.
internal static class Matches
{
    // The text of an invalid pattern, which the error of the check (2009) and the error of the evaluation (3021) share: the offset and
    // the reason are those of the RegexError that the error value holds by reference.
    private static readonly Func<CelError, string> InvalidRegexText = static error => Message((RegexError)error.First!);

    // The preparer of the constant argument at index 1 of both overloads: the program of a literal pattern, built once and kept in the
    // cache of the environment, or the text of the refusal. It charges nothing and does not throw for a pattern.
    internal static readonly ConstantPreparer Preparer = static (value, cache) =>
    {
        var patterns = cache as PatternCache;
        if (patterns is not null && patterns.TryGet(value, out PatternEntry? entry))
        {
            return Preparation(entry);
        }

        if (!PatternParser.TryParse(value, out PatternTree? tree, out RegexError? failure))
        {
            var refused = new PatternEntry(failure!);
            patterns?.Add(value, refused);
            return Preparation(refused);
        }

        CompiledPattern program;
        try
        {
            program = ProgramBuilder.Build(tree!);
        }
        finally
        {
            tree!.Dispose();
        }

        var built = new PatternEntry(program);
        patterns?.Add(value, built);
        return Preparation(built);
    };

    // The constant argument of both overloads: the pattern, at index 1.
    internal static ConstantArgument Constant { get; } = new(1, Preparer);

    // The implementation of matches and matches_string.
    internal static CelValue Implementation(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? subject) || !arguments[1].TryGetString(out string? pattern))
        {
            return site.NoMatchingOverload(arguments);
        }

        // A pattern that was a literal: nothing is parsed or built, the call pays the match only.
        if (site.Prepared is CompiledPattern prepared)
        {
            return Match(prepared, subject, in site);
        }

        // A pattern from data: the parse is charged before the cache is looked at or the pattern is read.
        if (site.HasLimit)
        {
            site.Charge(ParseUnits(pattern.Length));
        }

        var cache = site.Cache as PatternCache;
        if (cache is not null && cache.TryGet(pattern, out PatternEntry? known))
        {
            return known.Program is { } hit ? BuiltAndMatched(hit, subject, in site) : Refused(known.Error!, in site);
        }

        if (!PatternParser.TryParse(pattern, out PatternTree? tree, out RegexError? failure))
        {
            cache?.Add(pattern, new PatternEntry(failure!));
            return Refused(failure!, in site);
        }

        CompiledPattern program;
        try
        {
            // m is known after the parse; both charges come before the build, which a limit may therefore stop.
            int size = tree!.Size;
            if (site.HasLimit)
            {
                site.Charge(BuildUnits(size));
                site.Charge(MatchUnits(subject.Length, size));
            }

            program = ProgramBuilder.Build(tree);
        }
        finally
        {
            tree!.Dispose();
        }

        cache?.Add(pattern, new PatternEntry(program));
        return Run(program, subject);
    }

    // ⌈x/10⌉.
    private static ulong Tenths(ulong length) => (length + 9) / 10;

    // 1 + ⌈L/10⌉, the parse of a pattern of L code units.
    private static ulong ParseUnits(int length) => 1 + Tenths((ulong)length);

    // ⌈m/4⌉, the build of a program of size m.
    private static ulong BuildUnits(int size) => ((ulong)size + 3) / 4;

    // 1 + ⌈(n + 1) · m/10⌉, the match of a subject of n code units against a program of size m.
    private static ulong MatchUnits(int length, int size) => 1 + Tenths(((ulong)length + 1) * (ulong)size);

    private static string Message(RegexError error) =>
        "invalid regular expression at offset " + error.Offset.ToString(CultureInfo.InvariantCulture) + ": " + error.Reason;

    private static ConstantPreparation Preparation(PatternEntry entry) =>
        entry.Program is { } program ? ConstantPreparation.Success(program) : ConstantPreparation.Failure(Message(entry.Error!));

    // The program of an entry of the cache: the build and the match are charged as for a miss.
    private static CelValue BuiltAndMatched(CompiledPattern program, string subject, in CallSite site)
    {
        if (site.HasLimit)
        {
            site.Charge(BuildUnits(program.Size));
        }

        return Match(program, subject, in site);
    }

    private static CelValue Match(CompiledPattern program, string subject, in CallSite site)
    {
        if (site.HasLimit)
        {
            site.Charge(MatchUnits(subject.Length, program.Size));
        }

        return Run(program, subject);
    }

    private static CelValue Run(CompiledPattern program, string subject)
    {
        // The engine counts steps for the property test of linearity; the library has no use for the count.
        long steps = 0;
        return CelValue.FromBoolean(Matcher.IsMatch(program, subject, ref steps));
    }

    // EvalInvalidRegex (3021) at the call. The error keeps the RegexError by reference, and its message is formed when it is read.
    private static CelValue Refused(RegexError error, in CallSite site) =>
        site.Error(CelErrorCode.EvalInvalidRegex, InvalidRegexText, error);
}
