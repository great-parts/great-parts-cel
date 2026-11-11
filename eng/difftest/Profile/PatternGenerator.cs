using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CsCheck;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The fault that a refused pattern has (docs/design/eng-difftest.md, "Profile: <c>matches</c> and <c>matches_string</c>"): one
/// construct of the list NOT SUPPORTED, <c>\C</c>, <c>a**</c>, a count or a product of counts over 1 000, a group or a class that
/// is not closed, a lone <c>)</c>, an unknown <c>\p</c> name or a bad range.
/// </summary>
internal enum PatternFault
{
    /// <summary>No fault: the pattern is in the grammar.</summary>
    None,

    /// <summary><c>(?=</c>.</summary>
    LookAhead,

    /// <summary><c>(?!</c>.</summary>
    NegativeLookAhead,

    /// <summary><c>(?&lt;=</c>.</summary>
    LookBehind,

    /// <summary><c>(?&gt;</c>.</summary>
    AtomicGroup,

    /// <summary><c>x*+</c>.</summary>
    PossessiveRepetition,

    /// <summary><c>\1</c>.</summary>
    BackReference,

    /// <summary><c>\Z</c>.</summary>
    EndOfTextAlias,

    /// <summary><c>\G</c>.</summary>
    SearchStart,

    /// <summary><c>\K</c>.</summary>
    MatchReset,

    /// <summary><c>\X</c>.</summary>
    GraphemeCluster,

    /// <summary><c>(?R)</c>.</summary>
    Recursion,

    /// <summary><c>(*FAIL)</c>.</summary>
    ControlVerb,

    /// <summary><c>\C</c>.</summary>
    AnyByte,

    /// <summary><c>a**</c>.</summary>
    NestedRepetition,

    /// <summary><c>a{1001}</c>.</summary>
    CountOverLimit,

    /// <summary><c>(a{32}){32}</c>.</summary>
    CountProductOverLimit,

    /// <summary>A <c>(</c> that is never closed.</summary>
    UnclosedGroup,

    /// <summary>A <c>[</c> that is never closed.</summary>
    UnclosedClass,

    /// <summary>A <c>)</c> that closes nothing.</summary>
    LoneClose,

    /// <summary>An unknown <c>\p</c> name.</summary>
    UnknownName,

    /// <summary>A range whose end is below its start.</summary>
    BadRange,
}

/// <summary>A generated pattern.</summary>
/// <param name="Text">The text of the pattern.</param>
/// <param name="Fault">The fault the generator put in it, or <see cref="PatternFault.None"/>.</param>
/// <param name="Constructs">The constructs of the grammar that the pattern holds, as <see cref="PatternGenerator.Constructs"/> names them.</param>
/// <param name="SizeBound">An upper bound of the size of the program of the pattern, instructions and ranges that classes own.</param>
/// <param name="CountProduct">The greatest product of counts on a path of nested repetitions, as Go checks it.</param>
/// <param name="Atoms">The number of atoms of the grammar that the pattern was made of, without the fault.</param>
/// <param name="Depth">The greatest depth of nested groups, without the fault.</param>
internal sealed record GeneratedPattern(
    string Text,
    PatternFault Fault,
    IReadOnlySet<string> Constructs,
    int SizeBound,
    int CountProduct,
    int Atoms,
    int Depth);

/// <summary>
/// Patterns by the grammar of RE2 as <c>syntax.Perl</c> reads it (docs/design/eng-difftest.md, "Profile: <c>matches</c> and
/// <c>matches_string</c>"): groups to depth 4, at most 20 atoms, counts of 5 at most and now and then a count up to 1 000 on one
/// atom that no other count encloses. The size of the program stays below <see cref="SizeLimit"/> by construction: the generator
/// keeps an upper bound and leaves out a repetition that would pass it. One pattern in twelve is refused, by one fault put in at a
/// boundary of the top level, which is the same error whatever stands around it.
/// </summary>
internal sealed class PatternGenerator
{
    /// <summary>The largest depth of nested groups.</summary>
    internal const int MaxDepth = 4;

    /// <summary>The largest number of atoms.</summary>
    internal const int MaxAtoms = 20;

    /// <summary>The largest count of a repetition that another count may enclose.</summary>
    internal const int MaxSmallCount = 5;

    /// <summary>The largest count that the grammar allows, and the largest product of counts.</summary>
    internal const int CountLimit = 1000;

    /// <summary>The bound that a pattern keeps for the size of its program.</summary>
    internal const int SizeLimit = 6000;

    // One pattern in this many is refused.
    private const int RefusedOneIn = 12;

    // A pattern has a count up to 1 000, on an atom that allows it, with a chance of one in this.
    private const int BigCountOneIn = 24;

    private readonly PCG pcg;
    private readonly HashSet<string> constructs = new(StringComparer.Ordinal);
    private int atoms;
    private int ranges;
    private int names;
    private bool bigCountAvailable;
    private int maxProduct = 1;
    private int maxDepth;

    private PatternGenerator(PCG pcg) => this.pcg = pcg;

    /// <summary>A form of atom: its text, the ranges that it owns in the worst case, and whether a count of 1 000 on it stays small.</summary>
    private sealed record AtomForm(string Text, int Ranges, bool Plain, int Weight);

    private readonly record struct Piece(string Text, int Size, int Product, bool Repeatable);

    private static readonly AtomForm[] AtomForms =
    [
        new("a", 0, true, 3), new("b", 0, true, 3), new("c", 0, true, 3), new("7", 0, true, 2), new("_", 0, true, 1), new(" ", 0, true, 1),
        new("é", 0, true, 1), new("ſ", 0, true, 2), new("K", 0, true, 2), new("ß", 0, true, 1), new("\U0001F600", 0, true, 1), new(".", 0, true, 2),
        new("\\.", 0, true, 1), new("\\*", 0, true, 1), new("\\+", 0, true, 1), new("\\?", 0, true, 1), new("\\(", 0, true, 1), new("\\)", 0, true, 1),
        new("\\[", 0, true, 1), new("\\]", 0, true, 1), new("\\{", 0, true, 1), new("\\}", 0, true, 1), new("\\|", 0, true, 1), new("\\^", 0, true, 1),
        new("\\$", 0, true, 1), new("\\\\", 0, true, 1),
        new("\\d", 10, true, 2), new("\\D", 10, true, 1), new("\\s", 10, true, 1), new("\\S", 10, true, 1), new("\\w", 10, true, 2), new("\\W", 10, true, 1),
        new("\\pL", 700, false, 2), new("\\pN", 150, false, 1), new("\\PL", 700, false, 1), new("\\p{Greek}", 60, false, 1),
        new("\\p{^Lu}", 700, false, 1), new("\\p{Any}", 2, true, 1),
        new("[a-c]", 10, true, 2), new("[^ab]", 10, true, 1), new("[[:alpha:]]", 10, true, 1), new("[\\d_]", 10, true, 1),
        new("[\\pL_]", 700, false, 1), new("[é-ſ]", 10, true, 1),
    ];

    private static readonly string[] Anchors = ["^", "$", "\\A", "\\z", "\\b", "\\B"];

    private static readonly string[] BareFlags = ["(?i)", "(?s)", "(?m)"];

    private static readonly string[] FlagGroups = ["(?i:", "(?s:", "(?m:", "(?i-s:"];

    private static readonly (PatternFault Fault, string Text)[] FaultTexts =
    [
        (PatternFault.LookAhead, "(?=a)"), (PatternFault.NegativeLookAhead, "(?!a)"), (PatternFault.LookBehind, "(?<=a)"),
        (PatternFault.AtomicGroup, "(?>a)"), (PatternFault.PossessiveRepetition, "a*+"), (PatternFault.BackReference, "\\1(?:)"),
        (PatternFault.EndOfTextAlias, "\\Z"), (PatternFault.SearchStart, "\\G"), (PatternFault.MatchReset, "\\K"),
        (PatternFault.GraphemeCluster, "\\X"), (PatternFault.Recursion, "(?R)"), (PatternFault.ControlVerb, "(*FAIL)"),
        (PatternFault.AnyByte, "\\C"), (PatternFault.NestedRepetition, "a**"), (PatternFault.CountOverLimit, "a{1001}"),
        (PatternFault.CountProductOverLimit, "(a{32}){32}"), (PatternFault.UnclosedGroup, "(a"), (PatternFault.UnclosedClass, "[ab"),
        (PatternFault.LoneClose, ")"), (PatternFault.UnknownName, "\\p{Foo}"), (PatternFault.BadRange, "[z-a]"),
    ];

    // The code points of the strings that a pattern is matched against: the letters of the atoms in both cases, a digit, _, a
    // space, the characters that fold together and apart (é, ſ, K, İ, ß, ẞ), a code point outside the Basic Multilingual Plane,
    // a line feed, and the characters that make a pattern: ( \ ^.
    private static readonly string[] SubjectUnits =
    [
        "a", "b", "c", "a", "b", "c", "A", "B", "C", "k", "K", "7", "_", " ", "é", "ſ", "K", "İ", "ß", "ẞ", "\U0001F600", "\n", "(", "\\", "^",
    ];

    /// <summary>
    /// The constructs of the grammar that a pattern may hold, by name: an atom as its text; an anchor, a group, a flag and an
    /// alternative as <c>anchor:…</c>, <c>group:…</c>, <c>flags:…</c> and <c>alternative</c>; a repetition as <c>repeat:…</c> and
    /// <c>lazy:…</c>, <c>big count</c> for a count over 5.
    /// </summary>
    internal static IReadOnlyList<string> Constructs { get; } =
    [
        .. AtomForms.Select(form => form.Text),
        .. Anchors.Select(anchor => "anchor:" + anchor),
        "group:(", "group:(?:", "group:(?P<n>",
        .. BareFlags.Select(flag => "flags:" + flag),
        .. FlagGroups.Select(flag => "flags:" + flag),
        "repeat:*", "repeat:+", "repeat:?", "repeat:{n}", "repeat:{n,}", "repeat:{n,m}",
        "lazy:*", "lazy:+", "lazy:?", "lazy:{n}", "lazy:{n,}", "lazy:{n,m}",
        "big count", "alternative", "empty alternative", "nested groups",
    ];

    /// <summary>The kinds of fault that a refused pattern has.</summary>
    internal static IReadOnlyList<PatternFault> Faults { get; } = [.. FaultTexts.Select(fault => fault.Fault)];

    /// <summary>The fragment that a fault puts into a pattern.</summary>
    /// <param name="fault">The fault, not <see cref="PatternFault.None"/>.</param>
    internal static string FaultText(PatternFault fault) => FaultTexts.Single(entry => entry.Fault == fault).Text;

    /// <summary>A pattern.</summary>
    /// <param name="pcg">The random source.</param>
    internal static GeneratedPattern Generate(PCG pcg)
    {
        // A pattern whose bound passes the limit is made again, from the next draws of the same source; a pattern of a few atoms
        // always keeps the bound, so the loop ends.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            GeneratedPattern pattern = new PatternGenerator(pcg).Make();
            if (pattern.SizeBound <= SizeLimit)
            {
                return pattern;
            }
        }

        return new GeneratedPattern("a", PatternFault.None, new HashSet<string>(StringComparer.Ordinal) { "a" }, 2, 1, 1, 0);
    }

    /// <summary>A string to match, of at most <see cref="ProfileTables.MaxCodePoints"/> code points of the alphabet of subjects.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string Subject(PCG pcg)
    {
        StringBuilder text = new();
        int count = pcg.Below(ProfileTables.MaxCodePoints + 1);
        for (int index = 0; index < count; index++)
        {
            text.Append(pcg.Pick(SubjectUnits));
        }

        return text.ToString();
    }

    private GeneratedPattern Make()
    {
        bigCountAvailable = pcg.OneIn(BigCountOneIn);
        (List<List<Piece>> branches, int size, _) = Alternation(0);
        PatternFault fault = pcg.OneIn(RefusedOneIn) ? pcg.Pick(Faults) : PatternFault.None;
        if (fault != PatternFault.None)
        {
            Piece piece = new(FaultText(fault), 0, 1, false);
            if (fault == PatternFault.UnclosedClass)
            {
                // A class that is not closed ends the pattern: a `]` later in the text would close it.
                branches[^1].Add(piece);
            }
            else
            {
                List<Piece> branch = pcg.Pick(branches);
                branch.Insert(pcg.Below(branch.Count + 1), piece);
            }
        }

        string text = string.Join('|', branches.Select(branch => string.Concat(branch.Select(piece => piece.Text))));
        return new GeneratedPattern(text, fault, constructs, size + ranges + 1, maxProduct, atoms, maxDepth);
    }

    // Alternatives of 1 to 3 branches, an empty one now and then; the groups stand one level deeper.
    private (List<List<Piece>> Branches, int Size, int Product) Alternation(int depth)
    {
        int count = pcg.OneIn(4) ? 2 + pcg.Below(2) : 1;
        List<List<Piece>> branches = [];
        int size = count - 1;
        int product = 1;
        for (int index = 0; index < count; index++)
        {
            List<Piece> pieces = count > 1 && pcg.OneIn(RefusedOneIn) ? [] : Concat(depth);
            if (pieces.Count == 0 && count > 1)
            {
                constructs.Add("empty alternative");
            }

            size += pieces.Sum(piece => piece.Size);
            product = Math.Max(product, pieces.Select(piece => piece.Product).DefaultIfEmpty(1).Max());
            branches.Add(pieces);
        }

        if (count > 1)
        {
            constructs.Add("alternative");
        }

        return (branches, size, product);
    }

    // 1 to 4 pieces; a piece that cannot be made because the atoms or the size are used up ends the branch.
    private List<Piece> Concat(int depth)
    {
        List<Piece> pieces = [];
        int count = 1 + pcg.Below(4);
        for (int index = 0; index < count; index++)
        {
            if (NextPiece(depth) is not { } piece)
            {
                break;
            }

            pieces.Add(piece);
        }

        return pieces;
    }

    private Piece? NextPiece(int depth)
    {
        if (atoms >= MaxAtoms)
        {
            return null;
        }

        int roll = pcg.Below(20);
        if (roll < 12 || (roll < 18 && depth >= MaxDepth))
        {
            return AtomPiece();
        }

        if (roll < 14)
        {
            string anchor = pcg.Pick(Anchors);
            constructs.Add("anchor:" + anchor);
            return new Piece(anchor, 1, 1, false);
        }

        if (roll < 18)
        {
            return GroupPiece(depth);
        }

        string flag = pcg.Pick(BareFlags);
        constructs.Add("flags:" + flag);
        return new Piece(flag, 0, 1, false);
    }

    private Piece AtomPiece()
    {
        AtomForm form = PickAtom();
        atoms++;
        ranges += form.Ranges;
        constructs.Add(form.Text);
        return Repeat(new Piece(form.Text, 1, 1, true), form.Plain);
    }

    // An atom by weight; one that owns many ranges gives way to a letter when the bound would pass the limit.
    private AtomForm PickAtom()
    {
        int total = AtomForms.Sum(form => form.Weight);
        int draw = pcg.Below(total);
        foreach (AtomForm form in AtomForms)
        {
            if (draw < form.Weight)
            {
                return ranges + form.Ranges + 2 * MaxAtoms > SizeLimit ? AtomForms[0] : form;
            }

            draw -= form.Weight;
        }

        return AtomForms[0];
    }

    private Piece? GroupPiece(int depth)
    {
        int kind = pcg.Below(10);
        string open;
        if (kind < 3)
        {
            open = "(";
            constructs.Add("group:(");
        }
        else if (kind < 6)
        {
            open = "(?:";
            constructs.Add("group:(?:");
        }
        else if (kind < 8)
        {
            names++;
            open = string.Create(CultureInfo.InvariantCulture, $"(?P<n{names}>");
            constructs.Add("group:(?P<n>");
        }
        else
        {
            open = pcg.Pick(FlagGroups);
            constructs.Add("flags:" + open);
        }

        maxDepth = Math.Max(maxDepth, depth + 1);
        (List<List<Piece>> branches, int size, int product) = Alternation(depth + 1);
        if (depth + 1 >= 3)
        {
            constructs.Add("nested groups");
        }

        string inner = string.Join('|', branches.Select(branch => string.Concat(branch.Select(piece => piece.Text))));
        return Repeat(new Piece(open + inner + ")", size, product, true), plain: false);
    }

    // A repetition after a piece: now and then a count up to 1 000 on a plain atom, otherwise a quarter of the pieces get one of
    // *, +, ?, {n}, {n,}, {n,m}, with counts of 5 at most, and a quarter of those are lazy. A count that would make the product of
    // nested counts pass 1 000 or the bound of the size pass the limit is left out.
    private Piece Repeat(Piece piece, bool plain)
    {
        if (!piece.Repeatable)
        {
            return piece;
        }

        if (bigCountAvailable && plain && pcg.OneIn(2))
        {
            bigCountAvailable = false;
            int max = 6 + pcg.Below(CountLimit - 5);
            int min = pcg.OneIn(3) ? 0 : pcg.Below(max + 1);
            Piece counted = pcg.Below(3) switch
            {
                0 => Counted(piece, $"{{{max}}}", "{n}", max, max),
                1 => Counted(piece, $"{{{max},}}", "{n,}", max, max),
                _ => Counted(piece, $"{{{min},{max}}}", "{n,m}", max, max),
            };
            if (counted.Text != piece.Text)
            {
                constructs.Add("big count");
            }

            return counted;
        }

        if (!pcg.OneIn(4))
        {
            return piece;
        }

        switch (pcg.Below(10))
        {
            case 0 or 1 or 2:
                return Operator(piece, "*", "*");
            case 3 or 4:
                return Operator(piece, "+", "+");
            case 5:
                return Operator(piece, "?", "?");
            case 6:
                int count = 1 + pcg.Below(MaxSmallCount);
                return Counted(piece, $"{{{count}}}", "{n}", count, count);
            case 7:
                int least = 1 + pcg.Below(MaxSmallCount);
                return Counted(piece, $"{{{least},}}", "{n,}", least, least);
            default:
                int upper = 1 + pcg.Below(MaxSmallCount);
                int lower = pcg.Below(upper + 1);
                return Counted(piece, $"{{{lower},{upper}}}", "{n,m}", upper, upper);
        }
    }

    private Piece Operator(Piece piece, string text, string key)
    {
        string suffix = text;
        if (pcg.OneIn(4))
        {
            suffix += "?";
            constructs.Add("lazy:" + key);
        }
        else
        {
            constructs.Add("repeat:" + key);
        }

        return piece with { Text = piece.Text + suffix, Size = piece.Size + 1, Repeatable = false };
    }

    // A counted repetition; the bound of the size grows to (count + 1) copies and the product of counts by the count.
    private Piece Counted(Piece piece, string text, string key, int copies, int factor)
    {
        int size = (piece.Size + 1) * (copies + 1);
        long product = (long)piece.Product * Math.Max(factor, 1);
        if (product > CountLimit || size + ranges + 2 * MaxAtoms > SizeLimit)
        {
            return piece;
        }

        string suffix = text;
        if (pcg.OneIn(4))
        {
            suffix += "?";
            constructs.Add("lazy:" + key);
        }
        else
        {
            constructs.Add("repeat:" + key);
        }

        maxProduct = Math.Max(maxProduct, (int)product);
        return piece with { Text = piece.Text + suffix, Size = size, Product = (int)product, Repeatable = false };
    }
}
