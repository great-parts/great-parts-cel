using System;

namespace Great.Parts.Cel.Library.Regex;

// The parser of a pattern: a port of the parser of regexp/syntax of Go 1.27.3 with the flags syntax.Perl, step by step, from UTF-16
// text and without recursion. A parse reads the text once, left to right, on a stack of nodes of the tree; operators, groups and
// alternatives are folded into the nodes below them as they are closed, so the stack never holds more than the open groups and
// their operands. The parts of the parser are in the files of this class: the stack and the folding of nodes (PatternParser.Stack),
// the factoring of alternatives (PatternParser.Factor), escapes (PatternParser.Escapes) and classes (PatternParser.Classes).
//
// An error is not an exception: the first error is kept in the parser, every step stops when there is one, and the parse returns
// it. A parse that has failed leaves nothing behind but the rented arrays, which the caller returns with the tree.
internal ref partial struct PatternParser
{
    private readonly ReadOnlySpan<char> text;
    private readonly PatternTree tree;
    private int pos;
    private int flags;

    // The offset of the repetition operator that the previous step read, or -1: a second operator in a row is an error.
    private int lastRepeat;

    // The same for the step that is running.
    private int thisRepeat;

    private RegexError? error;

    // The top of the used part of the work array of the factoring (PatternParser.Factor).
    private int workTop;

    // The text position from which there is no ":]" in the rest of the text; once known, no later search of the text is made.
    private int noColonBracketFrom;

    private PatternParser(ReadOnlySpan<char> text, PatternTree tree)
    {
        this.text = text;
        this.tree = tree;
        flags = PatternFlags.Initial;
        lastRepeat = -1;
        thisRepeat = -1;
        error = null;
        workTop = 0;
        noColonBracketFrom = int.MaxValue;
    }

    // Parses a pattern. On success the tree holds the finished pattern and Root is its root; the caller disposes the tree. On an
    // error the tree is disposed here and the error says where and why.
    internal static bool TryParse(ReadOnlySpan<char> pattern, out PatternTree? parsed, out RegexError? failure)
    {
        var tree = new PatternTree();
        var parser = new PatternParser(pattern, tree);
        parser.Run();
        if (parser.error is null && (tree.RangesExceeded || tree.Size > PatternSize.MaxProgramSize))
        {
            // The size is known only now, so an error of syntax anywhere in the text has won over it.
            parser.Fail(0, RegexError.TooLarge);
        }

        if (parser.error is not null)
        {
            tree.Dispose();
            parsed = null;
            failure = parser.error;
            return false;
        }

        parsed = tree;
        failure = null;
        return true;
    }

    private bool Failed => error is not null;

    private void Run()
    {
        while (pos < text.Length && error is null)
        {
            Step();
        }

        if (error is not null)
        {
            return;
        }

        Concat();
        if (!Failed && SwapVerticalBar())
        {
            tree.StackCount--;
        }

        if (!Failed)
        {
            Alternate();
        }

        if (Failed)
        {
            return;
        }

        if (tree.StackCount != 1)
        {
            Fail(0, RegexError.MissingParen);
            return;
        }

        tree.Root = tree.StackAt(0);
    }

    // One token of the text: a literal, an operator, a group, a class or an escape.
    private void Step()
    {
        thisRepeat = -1;
        switch (text[pos])
        {
            case '(':
                ParseOpenParen();
                break;
            case '|':
                ParseVerticalBar();
                pos++;
                break;
            case ')':
                ParseRightParen();
                pos++;
                break;
            case '[':
                ParseClass();
                break;
            case '*':
            case '+':
            case '?':
                ParseRepeatOperator();
                break;
            case '{':
                ParseCount();
                break;
            case '\\':
                ParseBackslash();
                break;
            default:
                ParsePlainToken();
                break;
        }

        lastRepeat = thisRepeat;
    }

    private bool Fail(int offset, string reason)
    {
        error ??= new RegexError(offset, reason);
        return false;
    }

    // Reads the code point at an index and moves the index past it. A lone surrogate is an error at its offset.
    private bool ReadRune(ref int index, out int codePoint)
    {
        char first = text[index];
        if (!char.IsSurrogate(first))
        {
            codePoint = first;
            index++;
            return true;
        }

        if (char.IsHighSurrogate(first) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            codePoint = char.ConvertToUtf32(first, text[index + 1]);
            index += 2;
            return true;
        }

        codePoint = 0;
        return Fail(index, RegexError.InvalidUtf16);
    }

    // The check that Go makes of a stretch of text it is about to name in an error: a lone surrogate in it is the error instead.
    private bool CheckUtf16(int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < to && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return Fail(i, RegexError.InvalidUtf16);
            }
        }

        return true;
    }

    private int IndexOf(char value, int from)
    {
        for (int i = from; i < text.Length; i++)
        {
            if (text[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    // The anchors ^ and $, the dot, and any other character as a literal.
    private void ParsePlainToken()
    {
        bool oneLine = (flags & PatternFlags.OneLine) != 0;
        NodeKind kind = text[pos] switch
        {
            '^' => oneLine ? NodeKind.BeginText : NodeKind.BeginLine,
            '$' => oneLine ? NodeKind.EndText : NodeKind.EndLine,
            '.' => (flags & PatternFlags.DotNewline) != 0 ? NodeKind.AnyChar : NodeKind.AnyCharNotNewline,
            _ => NodeKind.None,
        };
        if (kind != NodeKind.None)
        {
            Op(kind);
            pos++;
        }
        else if (ReadRune(ref pos, out int c))
        {
            Literal(c);
        }
    }

    private void ParseOpenParen()
    {
        if (pos + 1 < text.Length && text[pos + 1] == '?')
        {
            ParsePerlFlags();
            return;
        }

        OpenGroup(true);
        pos++;
    }

    // Pushes the mark of an open group; a capturing one is a node of the finished tree, a non-capturing one is not.
    private void OpenGroup(bool captures)
    {
        int node = tree.NewNode(NodeKind.LeftParen, flags);
        tree.SetCounts(node, captures ? 1 : 0, 0);
        Push(node);
    }

    private void ParseVerticalBar()
    {
        Concat();
        if (!Failed && !SwapVerticalBar())
        {
            Op(NodeKind.VerticalBar);
        }
    }

    // A ) closes the open group: the stack above the mark becomes one node, which the group wraps or not.
    private void ParseRightParen()
    {
        Concat();
        if (Failed)
        {
            return;
        }

        if (SwapVerticalBar())
        {
            tree.StackCount--;
        }

        Alternate();
        if (Failed)
        {
            return;
        }

        int n = tree.StackCount;
        if (n < 2)
        {
            Fail(0, RegexError.UnexpectedParen);
            return;
        }

        int inner = tree.StackAt(n - 1);
        int mark = tree.StackAt(n - 2);
        tree.StackCount = n - 2;
        if (tree.Kind(mark) != NodeKind.LeftParen)
        {
            Fail(0, RegexError.UnexpectedParen);
            return;
        }

        flags = tree.Flags(mark);
        if (tree.Min(mark) == 0)
        {
            Push(inner);
            tree.Free(mark);
            return;
        }

        tree.SetKind(mark, NodeKind.Capture);
        tree.AddChild(mark, inner);
        tree.Finish(mark);
        Push(mark);
    }

    // The flags and groups that begin with (?: (?i) (?i:...) (?P<name>...) and the like; pos is at the (.
    private void ParsePerlFlags()
    {
        int s = pos;
        bool startsWithP = text.Length - s > 4 && text[s + 2] == 'P' && text[s + 3] == '<';
        bool startsWithName = text.Length - s > 3 && text[s + 2] == '<';
        if (startsWithP || startsWithName)
        {
            ParseNamedGroup(s, startsWithName ? 3 : 4);
            return;
        }

        int t = s + 2;
        int newFlags = flags;
        int sign = 1;
        bool sawFlag = false;
        while (t < text.Length)
        {
            if (!ReadRune(ref t, out int c))
            {
                return;
            }

            FlagStep step = ApplyFlag(c, ref newFlags, ref sign, ref sawFlag);
            if (step == FlagStep.Invalid)
            {
                break;
            }

            if (step == FlagStep.End)
            {
                if (c == ':')
                {
                    OpenGroup(false);
                }

                flags = newFlags & (PatternFlags.FoldCase | PatternFlags.DotNewline | PatternFlags.OneLine | PatternFlags.NonGreedy);
                pos = t;
                return;
            }
        }

        Fail(s, RegexError.UnsupportedPerlSyntax + text[s..t].ToString());
    }

    private enum FlagStep
    {
        Next,
        End,
        Invalid,
    }

    // One character of a flag group: a flag letter, the minus, or the end of the flags (: or ). After the minus the flags are
    // inverted, so that the letters clear them; the end inverts them back.
    private static FlagStep ApplyFlag(int c, ref int newFlags, ref int sign, ref bool sawFlag)
    {
        int flag = c switch
        {
            'i' => PatternFlags.FoldCase,
            'm' => -PatternFlags.OneLine,
            's' => PatternFlags.DotNewline,
            'U' => PatternFlags.NonGreedy,
            _ => 0,
        };
        if (flag != 0)
        {
            newFlags = flag > 0 ? newFlags | flag : newFlags & ~-flag;
            sawFlag = true;
            return FlagStep.Next;
        }

        if (c == '-' && sign > 0)
        {
            sign = -1;
            newFlags = ~newFlags;
            sawFlag = false;
            return FlagStep.Next;
        }

        if (c is not (':' or ')') || (sign < 0 && !sawFlag))
        {
            return FlagStep.Invalid;
        }

        if (sign < 0)
        {
            newFlags = ~newFlags;
        }

        return FlagStep.End;
    }

    // (?P<name>...) and (?<name>...); nameStart is the offset of the name from the (.
    private void ParseNamedGroup(int s, int nameStart)
    {
        int end = IndexOf('>', s);
        if (end < 0)
        {
            if (CheckUtf16(s, text.Length))
            {
                Fail(s, RegexError.InvalidNamedCapture);
            }

            return;
        }

        if (!CheckUtf16(s + nameStart, end))
        {
            return;
        }

        if (!IsValidGroupName(s + nameStart, end))
        {
            Fail(s, RegexError.InvalidNamedCapture);
            return;
        }

        OpenGroup(true);
        pos = end + 1;
    }

    private bool IsValidGroupName(int from, int to)
    {
        if (from >= to)
        {
            return false;
        }

        for (int i = from; i < to; i++)
        {
            char c = text[i];
            if (c != '_' && !IsAsciiAlphanumeric(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiAlphanumeric(int c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    // * + ? with the lazy form: the operator applies to the node on the top of the stack.
    private void ParseRepeatOperator()
    {
        int before = pos;
        NodeKind op = text[pos] switch
        {
            '*' => NodeKind.Star,
            '+' => NodeKind.Plus,
            _ => NodeKind.Quest,
        };
        int after = pos + 1;
        if (Repeat(op, 0, 0, before, ref after))
        {
            thisRepeat = before;
            pos = after;
        }
    }

    // {n}, {n,}, {n,m}; anything else that begins with { is an ordinary character.
    private void ParseCount()
    {
        int before = pos;
        if (!TryReadCount(out int min, out int max, out int after))
        {
            Literal('{');
            pos++;
            return;
        }

        if (min < 0 || min > PatternTree.MaxCount || max > PatternTree.MaxCount || (max >= 0 && min > max))
        {
            Fail(before, RegexError.InvalidRepeatCount);
            return;
        }

        if (Repeat(NodeKind.Repeat, min, max, before, ref after))
        {
            thisRepeat = before;
            pos = after;
        }
    }

    // Reads {min}, {min,} or {min,max} at pos. A number too big to hold makes min -1.
    private bool TryReadCount(out int min, out int max, out int after)
    {
        max = 0;
        after = 0;
        int t = pos + 1;
        if (!TryReadInt(ref t, out min) || t >= text.Length)
        {
            return false;
        }

        if (text[t] != ',')
        {
            max = min;
        }
        else
        {
            t++;
            if (t >= text.Length)
            {
                return false;
            }

            if (text[t] == '}')
            {
                max = -1;
            }
            else if (!TryReadInt(ref t, out max))
            {
                return false;
            }
            else if (max < 0)
            {
                min = -1;
            }
        }

        if (t >= text.Length || text[t] != '}')
        {
            return false;
        }

        after = t + 1;
        return true;
    }

    // A decimal integer without a leading zero; -1 for one of 8 digits or more of value.
    private bool TryReadInt(ref int index, out int value)
    {
        value = 0;
        int t = index;
        if (t >= text.Length || text[t] < '0' || text[t] > '9')
        {
            return false;
        }

        if (t + 1 < text.Length && text[t] == '0' && text[t + 1] >= '0' && text[t + 1] <= '9')
        {
            return false;
        }

        int start = t;
        while (t < text.Length && text[t] >= '0' && text[t] <= '9')
        {
            t++;
        }

        index = t;
        for (int i = start; i < t; i++)
        {
            if (value >= 100_000_000)
            {
                value = -1;
                break;
            }

            value = (value * 10) + (text[i] - '0');
        }

        return true;
    }

    // Makes the node on the top of the stack the operand of a new repeat node. The height is checked before the counts, as in Go.
    private bool Repeat(NodeKind op, int min, int max, int before, ref int after)
    {
        int repeatFlags = flags;
        if (after < text.Length && text[after] == '?')
        {
            after++;
            repeatFlags ^= PatternFlags.NonGreedy;
        }

        if (lastRepeat >= 0)
        {
            return Fail(lastRepeat, RegexError.InvalidRepeatOperator);
        }

        int n = tree.StackCount;
        if (n == 0 || tree.Kind(tree.StackAt(n - 1)) >= NodeKind.LeftParen)
        {
            return Fail(before, RegexError.MissingRepeatArgument);
        }

        int operand = tree.StackAt(n - 1);
        int node = tree.NewNode(op, repeatFlags);
        tree.SetCounts(node, min, max);
        tree.AddChild(node, operand);
        tree.Finish(node);
        tree.SetStackAt(n - 1, node);
        if (!CheckLimits(node))
        {
            return false;
        }

        if (op == NodeKind.Repeat && (min >= 2 || max >= 2) && tree.Product(node) > PatternTree.MaxCount)
        {
            return Fail(before, RegexError.InvalidRepeatCount);
        }

        return true;
    }
}
