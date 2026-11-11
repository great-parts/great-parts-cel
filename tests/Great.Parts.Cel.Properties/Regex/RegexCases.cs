using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CsCheck;

namespace Great.Parts.Cel.Properties.RegexEngine;

// The patterns and subjects of the properties of the engine. A pattern is built as a tree of its own — literals, the dot, classes
// of letters, \w and \pL, the anchors, groups, alternatives, repeats — and printed as text; the tree has at most 24 nodes and
// depth 4. The tree is also searched by the naive search below, which is simple and independent of the parser and the engine of
// the library: it computes the set of the positions where each node can end.
internal static class RegexCases
{
    private const int MostNodes = 24;
    private const int Depth = 4;
    private const int Line = 0x0A;
    private const int CarriageReturn = 0x0D;

    // The letters of patterns, and the code points of subjects: the letters, their upper-case forms, the Kelvin sign and the long s,
    // which fold into k and s, the line feed and the carriage return, an emoji outside the BMP, and characters that are not letters.
    private static readonly int[] PatternLetters = ['a', 'b', 'c', 'k', 's', 0xE9];

    private static readonly int[] SubjectCodePoints =
        ['a', 'b', 'c', 'A', 'B', 'k', 'K', 0x212A, 's', 'S', 0x17F, 0xE9, 0xC9, 0x1F600, Line, CarriageReturn, '_', '-'];

    // The orbits of simple case folding of the letters above.
    private static readonly int[][] Orbits =
    [
        ['a', 'A'], ['b', 'B'], ['c', 'C'], ['k', 'K', 0x212A], ['s', 'S', 0x17F], [0xE9, 0xC9],
    ];

    private static readonly Gen<RegexNode> Leaf = Gen.Frequency(
        (6, Gen.OneOfConst(PatternLetters).Select(letter => (RegexNode)new LiteralNode(letter))),
        (1, Gen.Const((RegexNode)new LiteralNode(0x1F600))),
        (2, Gen.Const((RegexNode)new AnyNode())),
        (2, Gen.OneOfConst(PatternLetters).Array[1, 3].Select(members => (RegexNode)new SetNode(members, false))),
        (1, Gen.OneOfConst(PatternLetters).Array[1, 2].Select(members => (RegexNode)new SetNode(members, true))),
        (1, Gen.Bool.Select(negated => (RegexNode)new WordNode(negated))),
        (1, Gen.Bool.Select(negated => (RegexNode)new LetterNode(negated))),
        (2, Gen.OneOfConst(Enum.GetValues<AnchorKind>()).Select(kind => (RegexNode)new AnchorNode(kind))));

    private static readonly Gen<RegexNode> Tree = Levels();

    private static readonly Gen<string> FlagsPrefix = Gen.OneOfConst(
        string.Empty, string.Empty, "(?i)", "(?s)", "(?m)", "(?is)", "(?im)", "(?ims)");

    internal static readonly Gen<RegexCase> Case =
        from root in Tree
        from flags in FlagsPrefix
        from subject in Gen.OneOfConst(SubjectCodePoints).Array[0, 8]
        select new RegexCase(root, flags, subject);

    // The patterns that are about lines: letters, the dot, the anchors of a line and of the text, in the mode of lines or not, over
    // subjects of two letters, the line feed and the carriage return, which is not the end of a line.
    private static readonly int[] LineSubjectCodePoints = ['a', 'b', Line, CarriageReturn];

    private static readonly Gen<RegexNode> LineItem = Gen.Frequency(
        (3, Gen.OneOfConst('a', 'b').Select(letter => (RegexNode)new LiteralNode(letter))),
        (2, Gen.Const((RegexNode)new AnyNode())),
        (3, Gen.OneOfConst(AnchorKind.BeginLine, AnchorKind.EndLine, AnchorKind.BeginText, AnchorKind.EndText).Select(kind => (RegexNode)new AnchorNode(kind))),
        (1, Gen.OneOfConst(AnchorKind.BeginLine, AnchorKind.EndLine).Select(kind => (RegexNode)new RepeatNode(new AnchorNode(kind), 0, 1))));

    internal static readonly Gen<RegexCase> LineCase =
        from items in LineItem.Array[1, 4]
        from flags in Gen.OneOfConst("(?m)", "(?m)", "(?ms)", string.Empty)
        from subject in Gen.OneOfConst(LineSubjectCodePoints).Array[0, 6]
        select new RegexCase(new ConcatNode(items), flags, subject);

    // The same patterns with the long subjects of the test of linearity.
    internal static Gen<RegexCase> CaseWithSubjectOfUpTo(int length)
    {
        return from root in Tree
               from flags in FlagsPrefix
               from subject in Gen.OneOfConst(SubjectCodePoints).Array[0, length]
               select new RegexCase(root, flags, subject);
    }

    // Whether the naive search finds a match of the pattern in the subject.
    internal static bool Matches(RegexCase input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = new Context(input.Subject, input.Flags.Contains('i', StringComparison.Ordinal), input.Flags.Contains('s', StringComparison.Ordinal), input.Flags.Contains('m', StringComparison.Ordinal));
        for (int start = 0; start <= input.Subject.Length; start++)
        {
            if (Ends(context, input.Root, start) != 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static string Show(RegexCase input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return string.Create(CultureInfo.InvariantCulture, $"pattern \"{input.Pattern}\", subject \"{input.Text}\"");
    }

    // The tree of depth 4 over the leaves, built level by level, with at most 24 nodes.
    private static Gen<RegexNode> Levels()
    {
        Gen<RegexNode> level = Leaf;
        for (int depth = 0; depth < Depth; depth++)
        {
            level = Above(level);
        }

        return level.Where(node => Count(node) <= MostNodes);
    }

    private static Gen<RegexNode> Above(Gen<RegexNode> child)
    {
        return Gen.Frequency(
            (3, Leaf),
            (2, child.Array[2, 3].Select(items => (RegexNode)new ConcatNode(items))),
            (2, child.Array[2, 3].Select(items => (RegexNode)new AltNode(items))),
            (1, child.Select(item => (RegexNode)new RepeatNode(item, 0, -1))),
            (1, child.Select(item => (RegexNode)new RepeatNode(item, 1, -1))),
            (1, child.Select(item => (RegexNode)new RepeatNode(item, 0, 1))),
            (2, Counted(child)),
            (1, child.Select(item => (RegexNode)new GroupNode(item, true))),
            (1, child.Select(item => (RegexNode)new GroupNode(item, false))));
    }

    // {n,m} with counts up to 3: {n}, {n,m} and {n,}.
    private static Gen<RegexNode> Counted(Gen<RegexNode> child)
    {
        return from item in child
               from min in Gen.Int[0, 3]
               from extra in Gen.Int[-1, 3]
               select (RegexNode)new RepeatNode(item, min, extra < 0 ? -1 : Math.Min(3, min + extra));
    }

    private static int Count(RegexNode node)
    {
        int count = 1;
        foreach (RegexNode child in node.Children())
        {
            count += Count(child);
        }

        return count;
    }

    // The set of the positions where a node can end when it starts at a position, as a mask of bits.
    private static uint Ends(Context context, RegexNode node, int start)
    {
        switch (node)
        {
            case LiteralNode literal:
                return Consumes(context, start, point => context.Same(point, literal.CodePoint));
            case AnyNode:
                return Consumes(context, start, point => context.DotNewline || point != Line);
            case SetNode set:
                return Consumes(context, start, point => set.Negated != context.AnyOf(point, set.Members.Contains));
            case WordNode word:
                return Consumes(context, start, point => word.Negated != context.AnyOf(point, IsWord));
            case LetterNode letter:
                return Consumes(context, start, point => letter.Negated != IsLetter(point));
            case AnchorNode anchor:
                return context.Holds(anchor.Kind, start) ? 1u << start : 0;
            case GroupNode group:
                return Ends(context, group.Child, start);
            case ConcatNode concat:
                return concat.Items.Aggregate(1u << start, (mask, item) => Step(context, item, mask));
            case AltNode alternative:
                return alternative.Items.Aggregate(0u, (mask, item) => mask | Ends(context, item, start));
            default:
                return EndsOfRepeat(context, (RepeatNode)node, start);
        }
    }

    // One code point that a test accepts, or nothing.
    private static uint Consumes(Context context, int start, Func<int, bool> accepts)
    {
        return start < context.Subject.Length && accepts(context.Subject[start]) ? 1u << (start + 1) : 0;
    }

    // The positions where a node can end when it starts at any of the positions of a mask.
    private static uint Step(Context context, RegexNode node, uint from)
    {
        uint result = 0;
        for (int position = 0; position <= context.Subject.Length; position++)
        {
            if ((from & (1u << position)) != 0)
            {
                result |= Ends(context, node, position);
            }
        }

        return result;
    }

    // The union of k applications of the node for k from Min to Max; with no upper end, until the set of positions is one seen.
    private static uint EndsOfRepeat(Context context, RepeatNode repeat, int start)
    {
        uint positions = 1u << start;
        uint result = 0;
        for (int k = 0; k < repeat.Min; k++)
        {
            positions = Step(context, repeat.Child, positions);
        }

        if (repeat.Max >= 0)
        {
            for (int k = repeat.Min; k <= repeat.Max; k++)
            {
                result |= positions;
                positions = Step(context, repeat.Child, positions);
            }

            return result;
        }

        var seen = new HashSet<uint>();
        while (seen.Add(positions))
        {
            result |= positions;
            positions = Step(context, repeat.Child, positions);
        }

        return result;
    }

    private static bool IsWord(int point) => point is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';

    private static bool IsLetter(int point) => Orbits.Any(orbit => orbit.Contains(point));

    internal static int[] OrbitOf(int point) => Orbits.FirstOrDefault(orbit => orbit.Contains(point)) ?? [point];

    private sealed class Context(int[] subject, bool foldCase, bool dotNewline, bool multiLine)
    {
        internal int[] Subject { get; } = subject;

        internal bool DotNewline { get; } = dotNewline;

        // Whether two code points are the same under the flags: equal, or in one orbit under (?i).
        internal bool Same(int point, int other) => point == other || (foldCase && OrbitOf(point).Contains(other));

        // Whether a test holds for the code point or, under (?i), for a member of its orbit.
        internal bool AnyOf(int point, Func<int, bool> test) => foldCase ? OrbitOf(point).Any(test) : test(point);

        internal bool Holds(AnchorKind kind, int position)
        {
            bool atStart = position == 0;
            bool atEnd = position == Subject.Length;
            return kind switch
            {
                AnchorKind.BeginText => atStart,
                AnchorKind.EndText => atEnd,
                AnchorKind.BeginLine => atStart || (multiLine && Subject[position - 1] == Line),
                AnchorKind.EndLine => atEnd || (multiLine && Subject[position] == Line),
                AnchorKind.WordBoundary => WordAt(position - 1) != WordAt(position),
                _ => WordAt(position - 1) == WordAt(position),
            };
        }

        private bool WordAt(int position) => position >= 0 && position < Subject.Length && IsWord(Subject[position]);
    }
}

// The anchors of the generator: ^ and $ are the beginning and the end of a line under (?m) and of the subject otherwise.
internal enum AnchorKind
{
    BeginLine,
    EndLine,
    BeginText,
    EndText,
    WordBoundary,
    NoWordBoundary,
}

internal abstract record RegexNode
{
    internal abstract IEnumerable<RegexNode> Children();

    // The pattern of the node as text.
    internal abstract void Print(StringBuilder text);
}

internal sealed record LiteralNode(int CodePoint) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [];

    internal override void Print(StringBuilder text) => text.Append(char.ConvertFromUtf32(CodePoint));
}

internal sealed record AnyNode : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [];

    internal override void Print(StringBuilder text) => text.Append('.');
}

internal sealed record SetNode(int[] Members, bool Negated) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [];

    internal override void Print(StringBuilder text)
    {
        text.Append(Negated ? "[^" : "[");
        foreach (int member in Members)
        {
            text.Append(char.ConvertFromUtf32(member));
        }

        text.Append(']');
    }
}

internal sealed record WordNode(bool Negated) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [];

    internal override void Print(StringBuilder text) => text.Append(Negated ? "\\W" : "\\w");
}

internal sealed record LetterNode(bool Negated) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [];

    internal override void Print(StringBuilder text) => text.Append(Negated ? "\\PL" : "\\pL");
}

internal sealed record AnchorNode(AnchorKind Kind) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [];

    internal override void Print(StringBuilder text)
    {
        text.Append(
            Kind switch
            {
                AnchorKind.BeginLine => "^",
                AnchorKind.EndLine => "$",
                AnchorKind.BeginText => "\\A",
                AnchorKind.EndText => "\\z",
                AnchorKind.WordBoundary => "\\b",
                _ => "\\B",
            });
    }
}

internal sealed record GroupNode(RegexNode Child, bool Capturing) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [Child];

    internal override void Print(StringBuilder text)
    {
        text.Append(Capturing ? "(" : "(?:");
        Child.Print(text);
        text.Append(')');
    }
}

internal sealed record ConcatNode(RegexNode[] Items) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => Items;

    // An alternative inside a concatenation is a group of its own.
    internal override void Print(StringBuilder text)
    {
        foreach (RegexNode item in Items)
        {
            if (item is AltNode)
            {
                text.Append("(?:");
                item.Print(text);
                text.Append(')');
            }
            else
            {
                item.Print(text);
            }
        }
    }
}

internal sealed record AltNode(RegexNode[] Items) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => Items;

    internal override void Print(StringBuilder text)
    {
        for (int i = 0; i < Items.Length; i++)
        {
            if (i > 0)
            {
                text.Append('|');
            }

            Items[i].Print(text);
        }
    }
}

// A repeat of a node; Max is -1 when it has no upper end.
internal sealed record RepeatNode(RegexNode Child, int Min, int Max) : RegexNode
{
    internal override IEnumerable<RegexNode> Children() => [Child];

    // The operand is one unit: a node that is more than a character or a group is wrapped, so a repeat never follows a repeat.
    internal override void Print(StringBuilder text)
    {
        bool unit = Child is LiteralNode or AnyNode or SetNode or WordNode or LetterNode or GroupNode;
        text.Append(unit ? string.Empty : "(?:");
        Child.Print(text);
        text.Append(unit ? string.Empty : ")");
        text.Append(Suffix());
    }

    private string Suffix()
    {
        return (Min, Max) switch
        {
            (0, -1) => "*",
            (1, -1) => "+",
            (0, 1) => "?",
            (_, -1) => string.Create(CultureInfo.InvariantCulture, $"{{{Min},}}"),
            _ when Min == Max => string.Create(CultureInfo.InvariantCulture, $"{{{Min}}}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{{{Min},{Max}}}"),
        };
    }
}

// A case of the properties: the tree of a pattern, the flags it starts with, and a subject as code points.
internal sealed record RegexCase(RegexNode Root, string Flags, int[] Subject)
{
    // The pattern as text, with the flags in front of it.
    internal string Pattern
    {
        get
        {
            var text = new StringBuilder(Flags);
            Root.Print(text);
            return text.ToString();
        }
    }

    // The subject as a string: a code point outside the BMP is a pair of surrogates.
    internal string Text => string.Concat(Subject.Select(char.ConvertFromUtf32));
}
