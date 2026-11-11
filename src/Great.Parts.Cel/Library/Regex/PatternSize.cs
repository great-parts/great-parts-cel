namespace Great.Parts.Cel.Library.Regex;

// The size of the program of a pattern, counted on the tree before any counted repeat is expanded: the number of instructions that
// Compile(Simplify(tree)) of Go gives, without the instructions of captures, no-operations and jumps, which this library's program
// does not have. A node keeps the number of instructions and the kind it has after simplification, so the repeat of a repeat that
// Simplify collapses is counted as one. The rules are those of simplify.go.
internal static class PatternSize
{
    // The greatest size m of a program that is accepted: the instructions, the one that ends the program included, plus the ranges
    // that classes own.
    internal const int MaxProgramSize = 100_000;

    // The counts saturate here, so that nested repeats of 1000 cannot overflow an int.
    internal const int Saturation = 1 << 30;

    // A kind after simplification is a NodeKind with this bit added when the node is non-greedy.
    internal const int LazyBit = 256;

    internal const int KindMask = LazyBit - 1;

    // The instructions and the kind after simplification of a star, plus or quest (op) of a node with the given values: simplify1.
    internal static void Unary(NodeKind op, bool lazy, int childInstructions, int childKind, out int instructions, out int kind)
    {
        int childBase = childKind & KindMask;
        if (childBase == (int)NodeKind.EmptyMatch)
        {
            instructions = 0;
            kind = childKind;
            return;
        }

        bool childLazy = (childKind & LazyBit) != 0;
        if (childBase == (int)op && childLazy == lazy)
        {
            instructions = childInstructions;
            kind = childKind;
            return;
        }

        instructions = Saturate(childInstructions + 1L);
        kind = (int)op | (lazy ? LazyBit : 0);
    }

    // The instructions and the kind after simplification of the repeat {min,max} (max is -1 for no upper end) of a node.
    internal static void Repeat(int min, int max, bool lazy, int childInstructions, int childKind, out int instructions, out int kind)
    {
        if (min == 0 && max == 0)
        {
            // x{0} is the empty match and does not look at x.
            instructions = 0;
            kind = (int)NodeKind.EmptyMatch;
        }
        else if (max < 0)
        {
            AtLeast(min, lazy, childInstructions, childKind, out instructions, out kind);
        }
        else if (min == 1 && max == 1)
        {
            // x{1} is x.
            instructions = childInstructions;
            kind = childKind;
        }
        else
        {
            Between(min, max, lazy, childInstructions, childKind, out instructions, out kind);
        }
    }

    // The sum of two sizes, saturated.
    internal static int Add(int left, int right) => Saturate((long)left + right);

    private static void AtLeast(int min, bool lazy, int childInstructions, int childKind, out int instructions, out int kind)
    {
        if (min == 0)
        {
            Unary(NodeKind.Star, lazy, childInstructions, childKind, out instructions, out kind);
        }
        else if (min == 1)
        {
            Unary(NodeKind.Plus, lazy, childInstructions, childKind, out instructions, out kind);
        }
        else
        {
            // x{n,} is n - 1 copies of x and x+.
            Unary(NodeKind.Plus, lazy, childInstructions, childKind, out int plus, out _);
            instructions = Saturate(((min - 1L) * childInstructions) + plus);
            kind = (int)NodeKind.Concat;
        }
    }

    // x{n,m}: n copies of x and the suffix (x(x(x)?)?)? of m - n levels. The first level is the quest of x; each of the other
    // levels is the quest of the concatenation of x and the level before, so it adds the instructions of x and one fork.
    private static void Between(int min, int max, bool lazy, int childInstructions, int childKind, out int instructions, out int kind)
    {
        long total = (long)min * childInstructions;
        kind = (int)NodeKind.Concat;
        if (max > min)
        {
            Unary(NodeKind.Quest, lazy, childInstructions, childKind, out int first, out int firstKind);
            int levels = max - min;
            total += first + ((levels - 1L) * (childInstructions + 1L));
            if (min == 0)
            {
                kind = levels == 1 ? firstKind : (int)NodeKind.Quest | (lazy ? LazyBit : 0);
            }
        }

        instructions = Saturate(total);
    }

    private static int Saturate(long value) => value > Saturation ? Saturation : (int)value;
}
