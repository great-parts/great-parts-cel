using System;
using Great.Parts.Cel.Library.Unicode;

namespace Great.Parts.Cel.Library.Regex;

// Classes: [...] with its items, the POSIX classes, \d \s \w, and \p{...} with the names of Unicode.
internal ref partial struct PatternParser
{
    // [...]: the items are added to the ranges of a new class node, which is then sorted, merged, negated and pushed.
    private void ParseClass()
    {
        int s = pos;
        int t = s + 1;
        int node = tree.NewNode(NodeKind.Class, flags);
        bool negated = t < text.Length && text[t] == '^';
        if (negated)
        {
            t++;
        }

        bool first = true;
        while (t >= text.Length || text[t] != ']' || first)
        {
            first = false;
            if (!ParseClassItem(s, ref t, node))
            {
                tree.Free(node);
                return;
            }
        }

        pos = t + 1;
        ClassBuilder.Clean(tree, node);
        if (negated)
        {
            ClassBuilder.Negate(tree, node);
        }

        tree.Finish(node);
        Push(node);
    }

    // One item of a class: a POSIX class, a \p class, a Perl class, a character or a range. t is moved past it.
    private bool ParseClassItem(int classStart, ref int t, int node)
    {
        if (text.Length - t > 2 && text[t] == '[' && text[t + 1] == ':')
        {
            if (!TryNamedClass(ref t, node, out bool matched))
            {
                return false;
            }

            if (matched)
            {
                return true;
            }
        }

        if (t + 1 < text.Length && text[t] == '\\')
        {
            char next = text[t + 1];
            if (next == 'p' || next == 'P')
            {
                bool ok = ParseUnicodeClass(t, node, false, out int after);
                t = after;
                return ok;
            }

            if (next is 'd' or 'D' or 's' or 'S' or 'w' or 'W')
            {
                AddPerlClass(node, next);
                t += 2;
                return true;
            }
        }

        return ParseClassRange(classStart, ref t, node);
    }

    // A character or a range lo-hi, with its case folding when (?i) is on.
    private bool ParseClassRange(int classStart, ref int t, int node)
    {
        int rangeStart = t;
        if (!ParseClassChar(classStart, ref t, out int lo))
        {
            return false;
        }

        int hi = lo;

        // [a-] is a and -, so a - is a range only when no ] follows it.
        if (text.Length - t >= 2 && text[t] == '-' && text[t + 1] != ']')
        {
            t++;
            if (!ParseClassChar(classStart, ref t, out hi))
            {
                return false;
            }

            if (hi < lo)
            {
                return Fail(rangeStart, RegexError.InvalidCharRange);
            }
        }

        if ((flags & PatternFlags.FoldCase) == 0)
        {
            ClassBuilder.AppendRange(tree, node, lo, hi);
        }
        else
        {
            ClassBuilder.AppendFoldedRange(tree, node, lo, hi);
        }

        return true;
    }

    private bool ParseClassChar(int classStart, ref int t, out int codePoint)
    {
        codePoint = 0;
        if (t >= text.Length)
        {
            return Fail(classStart, RegexError.MissingBracket);
        }

        if (text[t] == '\\')
        {
            if (!ParseEscape(t, out int after, out codePoint))
            {
                return false;
            }

            t = after;
            return true;
        }

        return ReadRune(ref t, out codePoint);
    }

    // [:alpha:] and [:^alpha:] at t. A "[:" that is not closed by ":]" is no POSIX class, and the "[" is an ordinary character.
    private bool TryNamedClass(ref int t, int node, out bool matched)
    {
        matched = false;
        int end = IndexOfColonBracket(t + 2);
        if (end < 0)
        {
            return true;
        }

        int nameStart = t + 2;
        bool negated = text[nameStart] == '^';
        ReadOnlySpan<int> ranges = PosixRanges(text[(negated ? nameStart + 1 : nameStart)..end]);
        if (ranges.IsEmpty)
        {
            return Fail(t, RegexError.InvalidCharRange);
        }

        AddGroup(node, ranges, negated);
        t = end + 2;
        matched = true;
        return true;
    }

    // The index of the first ":]" from an index; -1 when there is none.
    private int IndexOfColonBracket(int from)
    {
        if (from >= noColonBracketFrom)
        {
            return -1;
        }

        for (int i = from; i + 1 < text.Length; i++)
        {
            if (text[i] == ':' && text[i + 1] == ']')
            {
                return i;
            }
        }

        noColonBracketFrom = from;
        return -1;
    }

    private static ReadOnlySpan<int> PosixRanges(ReadOnlySpan<char> name)
    {
        return name switch
        {
            "alnum" => [0x30, 0x39, 0x41, 0x5A, 0x61, 0x7A],
            "alpha" => [0x41, 0x5A, 0x61, 0x7A],
            "ascii" => [0x00, 0x7F],
            "blank" => [0x09, 0x09, 0x20, 0x20],
            "cntrl" => [0x00, 0x1F, 0x7F, 0x7F],
            "digit" => [0x30, 0x39],
            "graph" => [0x21, 0x7E],
            "lower" => [0x61, 0x7A],
            "print" => [0x20, 0x7E],
            "punct" => [0x21, 0x2F, 0x3A, 0x40, 0x5B, 0x60, 0x7B, 0x7E],
            "space" => [0x09, 0x0D, 0x20, 0x20],
            "upper" => [0x41, 0x5A],
            "word" => [0x30, 0x39, 0x41, 0x5A, 0x5F, 0x5F, 0x61, 0x7A],
            "xdigit" => [0x30, 0x39, 0x41, 0x46, 0x61, 0x66],
            _ => default,
        };
    }

    // Adds a group of ranges, or its complement, to a class; under (?i) the group is folded first and the complement is of the fold.
    private void AddGroup(int node, ReadOnlySpan<int> ranges, bool negated)
    {
        if ((flags & PatternFlags.FoldCase) == 0)
        {
            if (negated)
            {
                ClassBuilder.AppendNegatedClass(tree, node, ranges);
            }
            else
            {
                ClassBuilder.AppendClass(tree, node, ranges);
            }

            return;
        }

        int scratch = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendFoldedClass(tree, scratch, ranges);
        ClassBuilder.Clean(tree, scratch);
        ClassBuilder.AppendNodeClass(tree, node, scratch, negated);
        tree.Free(scratch);
    }

    // \d \s \w and their complements inside a class.
    private void AddPerlClass(int node, char letter)
    {
        AddGroup(node, StaticTables.Ranges(PerlTable(letter)), IsComplementLetter(letter));
    }

    private static int PerlTable(char letter)
    {
        return letter switch
        {
            'd' or 'D' => StaticTables.Digit,
            's' or 'S' => StaticTables.Space,
            _ => StaticTables.Word,
        };
    }

    private static bool IsComplementLetter(char letter) => letter is 'D' or 'S' or 'W';

    // \d \s \w and their complements as a node: a class of a table, or the class that owns the folded ranges under (?i).
    private void ParseStandalonePerlClass(char letter)
    {
        int node = tree.NewNode(NodeKind.Class, flags);
        if ((flags & PatternFlags.FoldCase) == 0)
        {
            tree.SetStaticClass(node, PerlTable(letter), IsComplementLetter(letter));
        }
        else
        {
            AddPerlClass(node, letter);
        }

        pos += 2;
        tree.Finish(node);
        Push(node);
    }

    private void ParseStandaloneUnicodeClass()
    {
        int node = tree.NewNode(NodeKind.Class, flags);
        if (!ParseUnicodeClass(pos, node, true, out int after))
        {
            tree.Free(node);
            return;
        }

        pos = after;
        tree.Finish(node);
        Push(node);
    }

    // \pN, \p{Name}, \PN, \P{Name}, \p{^Name} at s. Standing alone, the class refers to the table when it can; in brackets, and
    // under (?i) when the table has a fold table, the ranges are added to the node.
    private bool ParseUnicodeClass(int s, int node, bool standalone, out int after)
    {
        after = s;
        int sign = text[s + 1] == 'P' ? -1 : 1;
        int t = s + 2;
        int nameStart = t;
        int nameEnd = t;
        if (t < text.Length)
        {
            if (!ReadRune(ref t, out int c))
            {
                return false;
            }

            nameEnd = t;
            if (c == '{')
            {
                int close = IndexOf('}', s);
                if (close < 0)
                {
                    return CheckUtf16(s, text.Length) && Fail(s, RegexError.InvalidCharRange);
                }

                nameStart = s + 3;
                nameEnd = close;
                t = close + 1;
                if (!CheckUtf16(nameStart, nameEnd))
                {
                    return false;
                }
            }
        }

        if (nameStart < nameEnd && text[nameStart] == '^')
        {
            sign = -sign;
            nameStart++;
        }

        if (!TryResolveName(text[nameStart..nameEnd], out int table, out int foldTable, out int tableSign))
        {
            return Fail(s, RegexError.InvalidCharRange);
        }

        if (tableSign < 0)
        {
            sign = -sign;
        }

        AddTable(node, standalone, table, foldTable, sign < 0);
        after = t;
        return true;
    }

    private void AddTable(int node, bool standalone, int table, int foldTable, bool negated)
    {
        if ((flags & PatternFlags.FoldCase) == 0 || foldTable < 0)
        {
            if (standalone)
            {
                tree.SetStaticClass(node, table, negated);
            }
            else if (negated)
            {
                ClassBuilder.AppendNegatedClass(tree, node, StaticTables.Ranges(table));
            }
            else
            {
                ClassBuilder.AppendClass(tree, node, StaticTables.Ranges(table));
            }

            return;
        }

        // The table and the code points that fold into it, merged, and then the complement of that when the class is negated.
        int scratch = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendClass(tree, scratch, StaticTables.Ranges(table));
        ClassBuilder.AppendClass(tree, scratch, StaticTables.Ranges(foldTable));
        ClassBuilder.Clean(tree, scratch);
        ClassBuilder.AppendNodeClass(tree, node, scratch, negated);
        tree.Free(scratch);
    }

    // The table of a name as unicodeTable of Go resolves it: the name is made canonical first.
    private static bool TryResolveName(ReadOnlySpan<char> name, out int table, out int foldTable, out int sign)
    {
        table = 0;
        foldTable = -1;
        sign = 1;
        Span<char> buffer = stackalloc char[CanonicalNameLength];
        if (!TryCanonicalName(name, buffer, out int length))
        {
            return false;
        }

        ReadOnlySpan<char> canonical = buffer[..length];
        if (canonical is "Any")
        {
            table = StaticTables.Any;
            return true;
        }

        if (canonical is "Ascii")
        {
            table = StaticTables.Ascii;
            foldTable = StaticTables.AsciiFold;
            return true;
        }

        if (!UnicodeTables.TryFindTable(canonical, out TableName found))
        {
            return false;
        }

        table = found.Table;
        foldTable = found.FoldTable;
        sign = found.IsComplement ? -1 : 1;
        return true;
    }

    private const int CanonicalNameLength = 64;

    // canonicalName of Go: the first letter upper-case, the other ASCII letters lower-case, and no underscore, hyphen or space.
    // A name that is longer than any name of a table is not one, whatever its characters.
    private static bool TryCanonicalName(ReadOnlySpan<char> name, Span<char> buffer, out int length)
    {
        length = 0;
        bool first = true;
        foreach (char c in name)
        {
            if (c is '_' or '-' or ' ')
            {
                continue;
            }

            char converted = c;
            if (first)
            {
                converted = c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
                first = false;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                converted = (char)(c + 32);
            }

            if (length == buffer.Length)
            {
                return false;
            }

            buffer[length++] = converted;
        }

        return true;
    }
}
