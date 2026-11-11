using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The helpers of the tests of the parser: the parse of a pattern, and its tree as text. The tree is written without recursion, as
// a prefix form: a literal as lit:text, a class as class[lo-hi,...] or table:name, a group as group(...) and the other nodes by
// their names with their children in brackets.
internal static class ParseHelper
{
    internal static PatternTree Parse(string pattern)
    {
        if (!PatternParser.TryParse(pattern, out PatternTree? tree, out RegexError? error))
        {
            throw new InvalidOperationException($"Pattern {Show(pattern)} is refused at {error!.Offset}: {error.Reason}");
        }

        return tree!;
    }

    internal static RegexError Refuse(string pattern)
    {
        if (PatternParser.TryParse(pattern, out PatternTree? tree, out RegexError? error))
        {
            tree!.Dispose();
            throw new InvalidOperationException($"Pattern {Show(pattern)} is accepted");
        }

        return error!;
    }

    internal static void AssertRefused(string pattern, int offset, string reason)
    {
        RegexError error = Refuse(pattern);

        Assert.Equal(offset, error.Offset);
        Assert.Equal(reason, error.Reason);
    }

    // The repeat that fails is the last one written, so the offset is that of the last brace.
    internal static void AssertRefusedAtTheLastBrace(string pattern, string reason)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        int brace = pattern.Length - 1;
        while (pattern[brace] != '{')
        {
            brace--;
        }

        AssertRefused(pattern, brace, reason);
    }

    // Appends the ranges written as lo-hi or lo, in hexadecimal and separated by commas, to a class node.
    internal static void AppendRanges(PatternTree tree, int node, string ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        foreach (string range in ranges.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] ends = range.Split('-');
            int lo = int.Parse(ends[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int hi = ends.Length == 1 ? lo : int.Parse(ends[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            ClassBuilder.AppendRange(tree, node, lo, hi);
        }
    }

    // The size m of a pattern.
    internal static int SizeOf(string pattern)
    {
        using PatternTree tree = Parse(pattern);
        return tree.Size;
    }

    // The tree of a pattern as text.
    internal static string Tree(string pattern)
    {
        using PatternTree tree = Parse(pattern);
        return Dump(tree);
    }

    internal static string Show(string text)
    {
        var builder = new StringBuilder("\"");
        foreach (char c in text)
        {
            builder.Append(c < 0x20 || c > 0x7E ? string.Create(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}") : c.ToString());
        }

        return builder.Append('"').ToString();
    }

    internal static string Dump(PatternTree tree, bool expandTables = false)
    {
        var builder = new StringBuilder();
        var stack = new Stack<int>();
        stack.Push(tree.Root);
        var children = new List<int>();
        while (stack.Count > 0)
        {
            int node = stack.Pop();
            if (node == PatternTree.Null)
            {
                builder.Append(')');
                continue;
            }

            builder.Append(Describe(tree, node, expandTables));
            children.Clear();
            for (int child = tree.FirstChild(node); child != PatternTree.Null; child = tree.NextSibling(child))
            {
                children.Add(child);
            }

            if (children.Count == 0)
            {
                continue;
            }

            builder.Append('(');
            stack.Push(PatternTree.Null);
            for (int i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }

        return builder.ToString();
    }

    private static string Describe(PatternTree tree, int node, bool expandTables)
    {
        string fold = (tree.Flags(node) & PatternFlags.FoldCase) != 0 ? "~" : string.Empty;
        string lazy = (tree.Flags(node) & PatternFlags.NonGreedy) != 0 ? "?" : string.Empty;
        switch (tree.Kind(node))
        {
            case NodeKind.Literal:
                var text = new StringBuilder();
                foreach (int codePoint in tree.LiteralRunes(node))
                {
                    text.Append(codePoint is < 0x20 or > 0x7E ? $"\\x{{{codePoint:X}}}" : char.ConvertFromUtf32(codePoint));
                }

                return $"lit{fold}:{text}";
            case NodeKind.Class:
                if (tree.IsStaticClass(node))
                {
                    return expandTables ? $"class{fold}[{Ranges(StaticRanges(tree, node))}]" : DescribeTable(tree, node);
                }

                return $"class{fold}[{Ranges(tree.ClassRanges(node))}]";
            case NodeKind.Repeat:
                return $"repeat{lazy}{{{tree.Min(node)},{tree.Max(node)}}}";
            default:
                return tree.Kind(node) + lazy;
        }
    }

    // The ranges that a class of a table holds, the complement computed for a negated one.
    internal static int[] StaticRanges(PatternTree tree, int node)
    {
        ReadOnlySpan<int> table = StaticTables.Ranges(tree.StaticTable(node));
        if (!tree.IsNegatedStatic(node))
        {
            return table.ToArray();
        }

        var result = new List<int>();
        int nextLo = 0;
        for (int i = 0; i < table.Length; i += 2)
        {
            if (nextLo <= table[i] - 1)
            {
                result.Add(nextLo);
                result.Add(table[i] - 1);
            }

            nextLo = table[i + 1] + 1;
        }

        if (nextLo <= StaticTables.MaxCodePoint)
        {
            result.Add(nextLo);
            result.Add(StaticTables.MaxCodePoint);
        }

        return [.. result];
    }

    private static string DescribeTable(PatternTree tree, int node)
    {
        int table = tree.StaticTable(node);
        string name = table switch
        {
            StaticTables.Digit => "digit",
            StaticTables.Space => "space",
            StaticTables.Word => "word",
            StaticTables.Any => "any",
            StaticTables.Ascii => "ascii",
            _ => "#" + table.ToString(CultureInfo.InvariantCulture),
        };
        return (tree.IsNegatedStatic(node) ? "table!:" : "table:") + name;
    }

    // Ranges as lo-hi in hexadecimal, comma-separated.
    internal static string Ranges(ReadOnlySpan<int> ranges)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < ranges.Length; i += 2)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(ranges[i].ToString("X", CultureInfo.InvariantCulture));
            if (ranges[i + 1] != ranges[i])
            {
                builder.Append('-').Append(ranges[i + 1].ToString("X", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    // Whether the single class of a pattern holds a code point, for a class that refers to a table or owns its ranges.
    internal static bool ClassHolds(string pattern, int codePoint)
    {
        using PatternTree tree = Parse(pattern);
        int root = tree.Root;
        Assert.Equal(NodeKind.Class, tree.Kind(root));
        if (tree.IsStaticClass(root))
        {
            return StaticTables.Contains(tree.StaticTable(root), codePoint) != tree.IsNegatedStatic(root);
        }

        return ClassBuilder.Contains(tree, root, codePoint);
    }
}
