using System;
using System.Buffers;

namespace Great.Parts.Cel.Library.Regex;

// The alternation of nodes, with the factoring that Go does when it builds one: runs of alternatives with a common literal prefix
// or a common leading character class become prefix(suffix1|suffix2|...), runs of single characters become one class, runs of
// empty matches one. Go does it by recursion on the suffixes; here it is an explicit stack of frames, each the list of alternatives
// of one level on the work array and the state of its four rounds, so the depth of the factoring costs no thread stack. The tree
// that comes out is the tree of Go, which is why the height and the size on its limits come out the same.
internal ref partial struct PatternParser
{
    private enum FactorStatus
    {
        RoundDone,
        NeedChild,
    }

    private struct FactorFrame
    {
        // The top of the work array when the frame began, which is restored when it ends.
        internal int Region;

        // The list of alternatives of the frame: Length nodes from Base on the work array.
        internal int Base;
        internal int Length;

        // The round (1 to 4; 5 is done) and the state of its loop, as the variables of the loops of Go.
        internal int Round;
        internal int Index;
        internal int Start;
        internal int Out;

        // Round 1: the common literal prefix of the run, as the slice of the data of the first literal, and its flags.
        internal int StrStart;
        internal int StrCount;
        internal int StrFlags;

        // Round 2: the common leading node of the run.
        internal int First;

        // A run that waits for the alternation of its suffixes: the prefix node, and the end of the run.
        internal int Prefix;
        internal int RunEnd;
    }

    // The alternation of the nodes of the stack from an index, factored.
    private int CollapseAlternate(int from, int count)
    {
        if (count == 1)
        {
            return tree.StackAt(from);
        }

        FactorFrame[] frames = ArrayPool<FactorFrame>.Shared.Rent(16);
        try
        {
            return RunFactor(ref frames, from, count);
        }
        finally
        {
            ArrayPool<FactorFrame>.Shared.Return(frames);
        }
    }

    private int RunFactor(ref FactorFrame[] frames, int from, int count)
    {
        int[] work = tree.EnsureWork(0, count);
        for (int i = 0; i < count; i++)
        {
            work[i] = tree.StackAt(from + i);
        }

        workTop = count;
        StartFrame(ref frames[0], 0, count);
        int depth = 0;
        while (true)
        {
            FactorStatus status = Advance(ref frames[depth]);
            if (Failed)
            {
                return PatternTree.Null;
            }

            if (status == FactorStatus.NeedChild)
            {
                if (depth + 2 > frames.Length)
                {
                    GrowFrames(ref frames);
                }

                int runStart = frames[depth].Base + frames[depth].Start;
                int runLength = frames[depth].RunEnd - frames[depth].Start;
                StartFrame(ref frames[depth + 1], runStart, runLength);
                depth++;
                continue;
            }

            int node = FinishFrame(ref frames[depth]);
            workTop = frames[depth].Region;
            if (depth == 0)
            {
                return node;
            }

            depth--;
            Resume(ref frames[depth], node);
        }
    }

    private static void GrowFrames(ref FactorFrame[] frames)
    {
        FactorFrame[] bigger = ArrayPool<FactorFrame>.Shared.Rent(frames.Length * 2);
        frames.AsSpan().CopyTo(bigger);
        ArrayPool<FactorFrame>.Shared.Return(frames);
        frames = bigger;
    }

    // Begins a frame on the alternatives at inputStart of the work array. Alternatives that are alternations are hoisted into the
    // list, which is put at the top of the work array.
    private void StartFrame(ref FactorFrame frame, int inputStart, int inputCount)
    {
        int[] work = tree.Work;
        int total = 0;
        for (int i = 0; i < inputCount; i++)
        {
            int node = work[inputStart + i];
            total += tree.Kind(node) == NodeKind.Alternate ? tree.ChildCount(node) : 1;
        }

        work = tree.EnsureWork(workTop, workTop + total);
        frame.Region = workTop;
        frame.Base = workTop;
        int at = workTop;
        for (int i = 0; i < inputCount; i++)
        {
            int node = work[inputStart + i];
            if (tree.Kind(node) != NodeKind.Alternate)
            {
                work[at++] = node;
                continue;
            }

            for (int child = tree.FirstChild(node); child != PatternTree.Null; child = tree.NextSibling(child))
            {
                work[at++] = child;
            }

            tree.Free(node);
        }

        workTop = at;
        frame.Length = total;
        frame.Round = total < 2 ? 5 : 1;
        ResetRound(ref frame);
    }

    private static void ResetRound(ref FactorFrame frame)
    {
        frame.Index = 0;
        frame.Start = 0;
        frame.Out = 0;
        frame.StrStart = 0;
        frame.StrCount = 0;
        frame.StrFlags = 0;
        frame.First = PatternTree.Null;
    }

    // Runs the rounds of a frame until it needs the alternation of a run of suffixes, or is done.
    private FactorStatus Advance(ref FactorFrame frame)
    {
        while (frame.Round < 5)
        {
            FactorStatus status = frame.Round switch
            {
                1 => Round1(ref frame),
                2 => Round2(ref frame),
                3 => Round3(ref frame),
                _ => Round4(ref frame),
            };
            if (status == FactorStatus.NeedChild || Failed)
            {
                return status;
            }

            frame.Length = frame.Out;
            frame.Round++;
            ResetRound(ref frame);
        }

        return FactorStatus.RoundDone;
    }

    // The node a finished frame stands for: the only alternative, or the alternation of the list.
    private int FinishFrame(ref FactorFrame frame)
    {
        int[] work = tree.Work;
        if (frame.Length == 1)
        {
            return work[frame.Base];
        }

        int node = tree.NewNode(NodeKind.Alternate, 0);
        for (int i = 0; i < frame.Length; i++)
        {
            tree.AddChild(node, work[frame.Base + i]);
        }

        tree.Finish(node);
        return node;
    }

    // The alternation of the suffixes of a run is made: the concatenation of the prefix and it goes to the output of the round,
    // and the loop goes on after the run.
    private void Resume(ref FactorFrame frame, int suffix)
    {
        int node = tree.NewNode(NodeKind.Concat, 0);
        tree.AddChild(node, frame.Prefix);
        tree.AddChild(node, suffix);
        tree.Finish(node);
        int[] work = tree.Work;
        work[frame.Base + frame.Out++] = node;
        int end = frame.RunEnd;
        frame.Start = end;
        if (frame.Round == 1)
        {
            if (end < frame.Length)
            {
                LeadingString(work[frame.Base + end], out frame.StrStart, out frame.StrCount, out frame.StrFlags);
            }
            else
            {
                frame.StrStart = 0;
                frame.StrCount = 0;
                frame.StrFlags = 0;
            }
        }
        else
        {
            frame.First = end < frame.Length ? LeadingRegexp(work[frame.Base + end]) : PatternTree.Null;
        }

        frame.Index = end + 1;
    }

    // Round 1: runs of alternatives that begin with the same literal string.
    private FactorStatus Round1(ref FactorFrame frame)
    {
        for (; frame.Index <= frame.Length; frame.Index++)
        {
            int i = frame.Index;
            int strStart = 0;
            int strCount = 0;
            int strFlags = 0;
            if (i < frame.Length)
            {
                LeadingString(tree.Work[frame.Base + i], out strStart, out strCount, out strFlags);
                if (strFlags == frame.StrFlags)
                {
                    int same = CommonPrefix(frame.StrStart, frame.StrCount, strStart, strCount);
                    if (same > 0)
                    {
                        frame.StrCount = same;
                        continue;
                    }
                }
            }

            if (EndRunOfStrings(ref frame, i))
            {
                return FactorStatus.NeedChild;
            }

            if (Failed)
            {
                return FactorStatus.RoundDone;
            }

            frame.Start = i;
            frame.StrStart = strStart;
            frame.StrCount = strCount;
            frame.StrFlags = strFlags;
        }

        return FactorStatus.RoundDone;
    }

    // The run Start to i ends. Reports true when its suffixes have to be alternated: the common prefix has become a node and has
    // been cut from the alternatives of the run.
    private bool EndRunOfStrings(ref FactorFrame frame, int i)
    {
        int start = frame.Start;
        int[] work = tree.Work;
        if (i == start)
        {
            return false;
        }

        if (i == start + 1)
        {
            work[frame.Base + frame.Out++] = work[frame.Base + start];
            return false;
        }

        int prefix = tree.NewNode(NodeKind.Literal, frame.StrFlags);
        Span<int> room = tree.AppendRoom(prefix, frame.StrCount);
        tree.ArenaSlice(frame.StrStart, frame.StrCount).CopyTo(room);
        tree.Finish(prefix);
        for (int j = start; j < i; j++)
        {
            int sub = RemoveLeadingString(work[frame.Base + j], frame.StrCount);
            work[frame.Base + j] = sub;
            if (!CheckLimits(sub))
            {
                return false;
            }
        }

        frame.Prefix = prefix;
        frame.RunEnd = i;
        return true;
    }

    // Round 2: runs of alternatives that begin with the same single character or fixed repeat of one.
    private FactorStatus Round2(ref FactorFrame frame)
    {
        for (; frame.Index <= frame.Length; frame.Index++)
        {
            int i = frame.Index;
            int leading = PatternTree.Null;
            if (i < frame.Length)
            {
                leading = LeadingRegexp(tree.Work[frame.Base + i]);
                if (frame.First != PatternTree.Null && leading != PatternTree.Null && SameLeading(frame.First, leading))
                {
                    continue;
                }
            }

            if (EndRunOfLeaders(ref frame, i))
            {
                return FactorStatus.NeedChild;
            }

            if (Failed)
            {
                return FactorStatus.RoundDone;
            }

            frame.Start = i;
            frame.First = leading;
        }

        return FactorStatus.RoundDone;
    }

    private bool EndRunOfLeaders(ref FactorFrame frame, int i)
    {
        int start = frame.Start;
        int[] work = tree.Work;
        if (i == start)
        {
            return false;
        }

        if (i == start + 1)
        {
            work[frame.Base + frame.Out++] = work[frame.Base + start];
            return false;
        }

        for (int j = start; j < i; j++)
        {
            int sub = RemoveLeadingRegexp(work[frame.Base + j], j != start);
            work[frame.Base + j] = sub;
            if (!CheckLimits(sub))
            {
                return false;
            }
        }

        frame.Prefix = frame.First;
        frame.RunEnd = i;
        return true;
    }

    // Round 3: runs of single characters and classes become one class.
    private FactorStatus Round3(ref FactorFrame frame)
    {
        int[] work = tree.Work;
        for (; frame.Index <= frame.Length; frame.Index++)
        {
            int i = frame.Index;
            if (i < frame.Length && IsCharClass(work[frame.Base + i]))
            {
                continue;
            }

            int start = frame.Start;
            if (i == start + 1)
            {
                work[frame.Base + frame.Out++] = work[frame.Base + start];
            }
            else if (i > start)
            {
                work[frame.Base + frame.Out++] = MergeRun(work, frame.Base, start, i);
            }

            if (i < frame.Length)
            {
                work[frame.Base + frame.Out++] = work[frame.Base + i];
            }

            frame.Start = i + 1;
        }

        return FactorStatus.RoundDone;
    }

    // The class of the characters work[start..end): the most complex of them takes the others.
    private int MergeRun(int[] work, int listBase, int start, int end)
    {
        int most = start;
        for (int j = start + 1; j < end; j++)
        {
            if (IsMoreComplex(work[listBase + j], work[listBase + most]))
            {
                most = j;
            }
        }

        (work[listBase + start], work[listBase + most]) = (work[listBase + most], work[listBase + start]);
        int target = work[listBase + start];
        for (int j = start + 1; j < end; j++)
        {
            MergeCharClass(target, work[listBase + j]);
            tree.Free(work[listBase + j]);
        }

        CleanAlt(target);
        return target;
    }

    private bool IsMoreComplex(int candidate, int current)
    {
        NodeKind candidateKind = tree.Kind(candidate);
        NodeKind currentKind = tree.Kind(current);
        return candidateKind > currentKind || (candidateKind == currentKind && tree.DataCount(candidate) > tree.DataCount(current));
    }

    // Round 4: a run of empty matches is one.
    private FactorStatus Round4(ref FactorFrame frame)
    {
        int[] work = tree.Work;
        for (int i = 0; i < frame.Length; i++)
        {
            if (i + 1 < frame.Length && tree.Kind(work[frame.Base + i]) == NodeKind.EmptyMatch
                && tree.Kind(work[frame.Base + i + 1]) == NodeKind.EmptyMatch)
            {
                continue;
            }

            work[frame.Base + frame.Out++] = work[frame.Base + i];
        }

        frame.Index = frame.Length + 1;
        return FactorStatus.RoundDone;
    }

    // The literal that a node begins with: the node itself, or the first of a concatenation. The string is a slice of the data of
    // that literal; nothing is returned for any other node.
    private void LeadingString(int node, out int start, out int count, out int foldFlags)
    {
        if (tree.Kind(node) == NodeKind.Concat && tree.ChildCount(node) > 0)
        {
            node = tree.FirstChild(node);
        }

        if (tree.Kind(node) != NodeKind.Literal)
        {
            start = 0;
            count = 0;
            foldFlags = 0;
            return;
        }

        start = tree.DataStartOf(node);
        count = tree.DataCount(node);
        foldFlags = tree.Flags(node) & PatternFlags.FoldCase;
    }

    private int CommonPrefix(int firstStart, int firstCount, int secondStart, int secondCount)
    {
        ReadOnlySpan<int> first = tree.ArenaSlice(firstStart, firstCount);
        ReadOnlySpan<int> second = tree.ArenaSlice(secondStart, secondCount);
        int same = 0;
        while (same < first.Length && same < second.Length && first[same] == second[same])
        {
            same++;
        }

        return same;
    }

    // Cuts the first n code points from the literal that a node begins with. A literal that has none left is the empty match,
    // which a concatenation then loses. Returns the node that stands for the old one.
    private int RemoveLeadingString(int node, int n)
    {
        if (tree.Kind(node) == NodeKind.Concat && tree.ChildCount(node) > 0)
        {
            int first = tree.FirstChild(node);
            CutLiteral(first, n);
            if (tree.Kind(first) == NodeKind.EmptyMatch)
            {
                return DropEmptyFirst(node);
            }

            tree.Finish(node);
            return node;
        }

        CutLiteral(node, n);
        return node;
    }

    private void CutLiteral(int node, int n)
    {
        if (tree.Kind(node) != NodeKind.Literal)
        {
            return;
        }

        tree.DropDataFront(node, n);
        if (tree.DataCount(node) == 0)
        {
            tree.SetKind(node, NodeKind.EmptyMatch);
        }

        tree.Finish(node);
    }

    // A concatenation whose first node is the empty match: the node goes, and a concatenation of one is the one.
    private int DropEmptyFirst(int concat)
    {
        tree.Free(tree.RemoveFirstChild(concat));
        if (tree.ChildCount(concat) == 0)
        {
            tree.SetKind(concat, NodeKind.EmptyMatch);
            tree.Finish(concat);
            return concat;
        }

        if (tree.ChildCount(concat) == 1)
        {
            int only = tree.RemoveFirstChild(concat);
            tree.Free(concat);
            return only;
        }

        tree.Finish(concat);
        return concat;
    }

    // The leading node that a node begins with: the first of a concatenation, or the node itself; nothing for the empty match.
    private int LeadingRegexp(int node)
    {
        if (tree.Kind(node) == NodeKind.EmptyMatch)
        {
            return PatternTree.Null;
        }

        if (tree.Kind(node) == NodeKind.Concat && tree.ChildCount(node) > 0)
        {
            int first = tree.FirstChild(node);
            return tree.Kind(first) == NodeKind.EmptyMatch ? PatternTree.Null : first;
        }

        return node;
    }

    // Takes the leading node out of a node and returns the node that stands for the rest; the leading node is freed when reuse is set.
    private int RemoveLeadingRegexp(int node, bool reuse)
    {
        if (tree.Kind(node) == NodeKind.Concat && tree.ChildCount(node) > 0)
        {
            int first = tree.RemoveFirstChild(node);
            if (reuse)
            {
                tree.Free(first);
            }

            if (tree.ChildCount(node) == 0)
            {
                tree.SetKind(node, NodeKind.EmptyMatch);
                tree.Finish(node);
                return node;
            }

            if (tree.ChildCount(node) == 1)
            {
                int only = tree.RemoveFirstChild(node);
                tree.Free(node);
                return only;
            }

            tree.Finish(node);
            return node;
        }

        if (reuse)
        {
            tree.Free(node);
        }

        int empty = tree.NewNode(NodeKind.EmptyMatch, 0);
        tree.Finish(empty);
        return empty;
    }

    // Whether two leading nodes are equal and can be factored out: a character or class, or a fixed repeat of one.
    private bool SameLeading(int first, int other)
    {
        if (tree.Kind(first) == NodeKind.Repeat)
        {
            if (tree.Min(first) != tree.Max(first) || !IsCharClass(tree.FirstChild(first)))
            {
                return false;
            }

            return tree.Kind(other) == NodeKind.Repeat
                && (tree.Flags(first) & PatternFlags.NonGreedy) == (tree.Flags(other) & PatternFlags.NonGreedy)
                && tree.Min(first) == tree.Min(other)
                && tree.Max(first) == tree.Max(other)
                && EqualCharClass(tree.FirstChild(first), tree.FirstChild(other));
        }

        return IsCharClass(first) && EqualCharClass(first, other);
    }

    // Equality of two nodes that are characters or classes, as Equal of Go: the same kind, the same case folding, the same code
    // points or ranges.
    private bool EqualCharClass(int a, int b)
    {
        NodeKind kind = tree.Kind(a);
        if (kind != tree.Kind(b))
        {
            return false;
        }

        switch (kind)
        {
            case NodeKind.Literal:
                return (tree.Flags(a) & PatternFlags.FoldCase) == (tree.Flags(b) & PatternFlags.FoldCase)
                    && tree.LiteralRunes(a).SequenceEqual(tree.LiteralRunes(b));
            case NodeKind.Class:
                return (tree.Flags(a) & PatternFlags.FoldCase) == (tree.Flags(b) & PatternFlags.FoldCase) && EqualClassRanges(a, b);
            case NodeKind.AnyChar:
            case NodeKind.AnyCharNotNewline:
                return true;
            default:
                return false;
        }
    }

    private bool EqualClassRanges(int a, int b)
    {
        bool aStatic = tree.IsStaticClass(a);
        bool bStatic = tree.IsStaticClass(b);
        if (aStatic && bStatic)
        {
            return tree.StaticTable(a) == tree.StaticTable(b) && tree.IsNegatedStatic(a) == tree.IsNegatedStatic(b);
        }

        if (!aStatic && !bStatic)
        {
            return tree.ClassRanges(a).SequenceEqual(tree.ClassRanges(b));
        }

        // A class of a table equals a class that owns the same ranges, when it is not the complement of the table.
        int tableNode = aStatic ? a : b;
        int ownedNode = aStatic ? b : a;
        return !tree.IsNegatedStatic(tableNode)
            && StaticTables.Ranges(tree.StaticTable(tableNode)).SequenceEqual(tree.ClassRanges(ownedNode));
    }
}
