using System;

namespace Great.Parts.Cel.Library.Regex;

// The parse stack and the folding of nodes: the pushes that merge literals, the concatenation and the alternation of what is above an
// open group, and the merge of single-character alternatives into one class.
internal ref partial struct PatternParser
{
    // The height is checked when a node is built, so the error comes at the first node over the limit, as in Go. The size is not
    // checked here: it is known only when the text has been read to its end.
    private bool CheckLimits(int node)
    {
        if (tree.Height(node) > PatternTree.MaxHeight)
        {
            return Fail(0, RegexError.NestingDepth);
        }

        return true;
    }

    // Puts a node on the stack. A class of one code point, or of the two of one orbit, is a literal; a literal pushed after another
    // is merged with the literal below the top: the top one stays alone, since an operator may still take it (ab* is a and b*).
    private void Push(int node)
    {
        if (tree.Kind(node) == NodeKind.Class && TryAsLiteral(node, out int codePoint, out int literalFlags))
        {
            if (MaybeConcat(codePoint, literalFlags))
            {
                tree.Free(node);
                return;
            }

            tree.SetLiteral(node, codePoint, literalFlags);
        }
        else
        {
            MaybeConcat(-1, 0);
        }

        tree.PushStack(node);
        CheckLimits(node);
    }

    private bool TryAsLiteral(int node, out int codePoint, out int literalFlags)
    {
        Span<int> buffer = stackalloc int[8];
        ReadOnlySpan<int> ranges = buffer[..CopySmallClassRanges(node, buffer)];
        codePoint = 0;
        literalFlags = 0;
        if (ClassBuilder.IsSinglePoint(ranges))
        {
            codePoint = ranges[0];
            literalFlags = flags & ~PatternFlags.FoldCase;
            return true;
        }

        if (ClassBuilder.IsFoldPair(ranges))
        {
            codePoint = ranges[0];
            literalFlags = flags | PatternFlags.FoldCase;
            return true;
        }

        return false;
    }

    // Copies the ranges of a class to a buffer when there are at most two, which is all that can make a class a literal; the number
    // of values copied, 0 when there are more.
    private int CopySmallClassRanges(int node, scoped Span<int> buffer)
    {
        if (!tree.IsStaticClass(node))
        {
            return CopyIfSmall(tree.ClassRanges(node), buffer);
        }

        ReadOnlySpan<int> table = StaticTables.Ranges(tree.StaticTable(node));
        if (!tree.IsNegatedStatic(node))
        {
            return CopyIfSmall(table, buffer);
        }

        if (table.Length > 6)
        {
            return 0;
        }

        int written = 0;
        int nextLo = 0;
        for (int i = 0; i < table.Length; i += 2)
        {
            if (nextLo <= table[i] - 1)
            {
                buffer[written++] = nextLo;
                buffer[written++] = table[i] - 1;
            }

            nextLo = table[i + 1] + 1;
        }

        if (nextLo <= StaticTables.MaxCodePoint)
        {
            buffer[written++] = nextLo;
            buffer[written++] = StaticTables.MaxCodePoint;
        }

        return written <= 4 ? written : 0;
    }

    private static int CopyIfSmall(ReadOnlySpan<int> ranges, Span<int> buffer)
    {
        if (ranges.Length > 4)
        {
            return 0;
        }

        ranges.CopyTo(buffer);
        return ranges.Length;
    }

    // Merges the literal on the top of the stack into the one below it when both have the same case folding. With a code point
    // (r >= 0) the node of the top literal is reused for it and the call reports true; otherwise the top node is freed.
    private bool MaybeConcat(int codePoint, int literalFlags)
    {
        int n = tree.StackCount;
        if (n < 2)
        {
            return false;
        }

        int top = tree.StackAt(n - 1);
        int below = tree.StackAt(n - 2);
        if (tree.Kind(top) != NodeKind.Literal || tree.Kind(below) != NodeKind.Literal
            || (tree.Flags(top) & PatternFlags.FoldCase) != (tree.Flags(below) & PatternFlags.FoldCase))
        {
            return false;
        }

        AppendRunes(below, top);
        tree.Finish(below);
        if (codePoint >= 0)
        {
            tree.SetLiteral(top, codePoint, literalFlags);
            return true;
        }

        tree.StackCount = n - 1;
        tree.Free(top);
        return false;
    }

    // Appends the code points of one literal to another.
    private void AppendRunes(int target, int source)
    {
        int count = tree.DataCount(source);
        Span<int> room = tree.AppendRoom(target, count);
        tree.DataOf(source).CopyTo(room);
    }

    // A literal of one code point on the stack; under (?i) it is the smallest code point of its orbit, with the flag.
    private void Literal(int codePoint)
    {
        int node = tree.NewNode(NodeKind.Literal, flags);
        int stored = (flags & PatternFlags.FoldCase) != 0 ? ClassBuilder.MinFold(codePoint) : codePoint;
        tree.AppendRoom(node, 1)[0] = stored;
        tree.Finish(node);
        Push(node);
    }

    // A node of a kind with no operands on the stack: an anchor, a mark.
    private void Op(NodeKind kind)
    {
        int node = tree.NewNode(kind, flags);
        tree.Finish(node);
        Push(node);
    }

    // Replaces what is on the stack above the topmost mark with its concatenation.
    private void Concat()
    {
        MaybeConcat(-1, 0);
        int from = StackTopOfOperands();
        int count = tree.StackCount - from;
        tree.StackCount = from;
        if (count == 0)
        {
            int empty = tree.NewNode(NodeKind.EmptyMatch, 0);
            tree.Finish(empty);
            Push(empty);
            return;
        }

        Push(CollapseConcat(from, count));
    }

    // Replaces what is on the stack above the topmost open group with its alternation. There is no bar above the group: the bars
    // have been folded as they came.
    private void Alternate()
    {
        int from = StackTopOfOperands();
        int count = tree.StackCount - from;
        tree.StackCount = from;
        if (count > 0)
        {
            CleanAlt(tree.StackAt(from + count - 1));
        }

        if (count == 0)
        {
            int none = tree.NewNode(NodeKind.NoMatch, 0);
            tree.Finish(none);
            Push(none);
            return;
        }

        int node = CollapseAlternate(from, count);
        if (!Failed)
        {
            Push(node);
        }
    }

    // The index of the first node above the topmost mark of the stack.
    private int StackTopOfOperands()
    {
        int i = tree.StackCount;
        while (i > 0 && tree.Kind(tree.StackAt(i - 1)) < NodeKind.LeftParen)
        {
            i--;
        }

        return i;
    }

    // The concatenation of nodes of the stack, from an index; the concatenations among them are hoisted, so there is never a
    // concatenation of a concatenation.
    private int CollapseConcat(int from, int count)
    {
        if (count == 1)
        {
            return tree.StackAt(from);
        }

        int node = tree.NewNode(NodeKind.Concat, 0);
        for (int i = 0; i < count; i++)
        {
            int operand = tree.StackAt(from + i);
            if (tree.Kind(operand) == NodeKind.Concat)
            {
                tree.MoveChildren(node, operand);
                tree.Free(operand);
            }
            else
            {
                tree.AddChild(node, operand);
            }
        }

        tree.Finish(node);
        return node;
    }

    // A | after a node that is a single character, below which is a single character and a bar: the two merge into one class.
    private bool SwapVerticalBar()
    {
        int n = tree.StackCount;
        if (n >= 3 && tree.Kind(tree.StackAt(n - 2)) == NodeKind.VerticalBar && IsCharClass(tree.StackAt(n - 1)) && IsCharClass(tree.StackAt(n - 3)))
        {
            int source = tree.StackAt(n - 1);
            int target = tree.StackAt(n - 3);
            if (tree.Kind(source) > tree.Kind(target))
            {
                (source, target) = (target, source);
                tree.SetStackAt(n - 3, target);
            }

            MergeCharClass(target, source);
            tree.Free(source);
            tree.StackCount = n - 1;
            return true;
        }

        if (n >= 2)
        {
            int top = tree.StackAt(n - 1);
            int bar = tree.StackAt(n - 2);
            if (tree.Kind(bar) == NodeKind.VerticalBar)
            {
                if (n >= 3)
                {
                    CleanAlt(tree.StackAt(n - 3));
                }

                tree.SetStackAt(n - 2, top);
                tree.SetStackAt(n - 1, bar);
                return true;
            }
        }

        return false;
    }

    // A literal of one code point, a class, or a character: what can be a member of a class.
    private bool IsCharClass(int node)
    {
        NodeKind kind = tree.Kind(node);
        return (kind == NodeKind.Literal && tree.DataCount(node) == 1)
            || kind == NodeKind.Class
            || kind == NodeKind.AnyCharNotNewline
            || kind == NodeKind.AnyChar;
    }

    // Cleans a node that is about to be a member of an alternation: a class is sorted and merged, and one that holds every code point
    // or every code point but the line feed is the character.
    private void CleanAlt(int node)
    {
        if (tree.Kind(node) != NodeKind.Class)
        {
            return;
        }

        if (tree.IsStaticClass(node))
        {
            if (tree.StaticTable(node) == StaticTables.Any && !tree.IsNegatedStatic(node))
            {
                MakeAnyChar(node, NodeKind.AnyChar);
            }

            return;
        }

        ClassBuilder.Clean(tree, node);
        ReadOnlySpan<int> ranges = tree.ClassRanges(node);
        if (ranges.Length == 2 && ranges[0] == 0 && ranges[1] == StaticTables.MaxCodePoint)
        {
            MakeAnyChar(node, NodeKind.AnyChar);
        }
        else if (ranges.Length == 4 && ranges[0] == 0 && ranges[1] == '\n' - 1 && ranges[2] == '\n' + 1 && ranges[3] == StaticTables.MaxCodePoint)
        {
            MakeAnyChar(node, NodeKind.AnyCharNotNewline);
        }
    }

    private void MakeAnyChar(int node, NodeKind kind)
    {
        tree.SetStaticClass(node, 0, false);
        tree.SetKind(node, kind);
        tree.Finish(node);
    }

    // dst = dst | src, for a destination that is at least as complex as the source (a literal, a class, a character).
    private void MergeCharClass(int target, int source)
    {
        switch (tree.Kind(target))
        {
            case NodeKind.AnyCharNotNewline:
                if (MatchRune(source, '\n'))
                {
                    tree.SetKind(target, NodeKind.AnyChar);
                }

                break;
            case NodeKind.Class:
                MergeIntoClass(target, source);
                break;
            case NodeKind.Literal:
                MergeIntoLiteral(target, source);
                break;
        }

        tree.Finish(target);
    }

    private void MergeIntoClass(int target, int source)
    {
        ClassBuilder.MakeOwned(tree, target);
        if (tree.Kind(source) == NodeKind.Literal)
        {
            ClassBuilder.AppendLiteral(tree, target, tree.LiteralRunes(source)[0], tree.Flags(source));
        }
        else if (!tree.IsStaticClass(source))
        {
            ClassBuilder.AppendNodeClass(tree, target, source, false);
        }
        else if (tree.IsNegatedStatic(source))
        {
            ClassBuilder.AppendNegatedClass(tree, target, StaticTables.Ranges(tree.StaticTable(source)));
        }
        else
        {
            ClassBuilder.AppendClass(tree, target, StaticTables.Ranges(tree.StaticTable(source)));
        }
    }

    private void MergeIntoLiteral(int target, int source)
    {
        int targetPoint = tree.LiteralRunes(target)[0];
        int sourcePoint = tree.LiteralRunes(source)[0];
        if (sourcePoint == targetPoint && tree.Flags(source) == tree.Flags(target))
        {
            return;
        }

        int targetFlags = tree.Flags(target);
        int sourceFlags = tree.Flags(source);
        tree.SetClass(target);
        ClassBuilder.AppendLiteral(tree, target, targetPoint, targetFlags);
        ClassBuilder.AppendLiteral(tree, target, sourcePoint, sourceFlags);
    }

    // Whether a character or class holds a code point.
    private bool MatchRune(int node, int codePoint)
    {
        switch (tree.Kind(node))
        {
            case NodeKind.Literal:
                return tree.DataCount(node) == 1 && tree.LiteralRunes(node)[0] == codePoint;
            case NodeKind.Class:
                return tree.IsStaticClass(node)
                    ? StaticTables.Contains(tree.StaticTable(node), codePoint) != tree.IsNegatedStatic(node)
                    : ClassBuilder.Contains(tree, node, codePoint);
            case NodeKind.AnyCharNotNewline:
                return codePoint != '\n';
            case NodeKind.AnyChar:
                return true;
            default:
                return false;
        }
    }
}
