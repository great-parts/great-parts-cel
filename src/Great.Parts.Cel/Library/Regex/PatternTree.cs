using System;
using System.Buffers;

namespace Great.Parts.Cel.Library.Regex;

// The tree of a parsed pattern, in arrays: a node is an index, and its fields are the ints of one rented array. The data that nodes
// own — the code points of a literal, the ranges of a class — are slices of a second rented array that only grows; a slice that has
// no room moves to the end, so appending to it is amortized constant. Children are a linked list of the nodes, so two lists join
// and the first child is dropped without copying. The parser never creates an object for a node or a range. The arrays are
// returned to the pool by Dispose.
//
// A node keeps four values and finishes them when it is built, so that no walk of the tree is ever needed: the height, the
// product of the counts of nested repeats, the number of instructions after simplification and the kind after simplification
// (PatternSize).
internal sealed class PatternTree : IDisposable
{
    internal const int Null = -1;

    // The greatest height of the tree and the greatest product of nested counts that are accepted.
    internal const int MaxHeight = 1000;
    internal const int MaxCount = 1000;

    private const int Stride = 16;
    private const int KindField = 0;
    private const int FlagsField = 1;
    private const int FirstField = 2;
    private const int LastField = 3;
    private const int NextField = 4;
    private const int CountField = 5;
    private const int AField = 6;
    private const int BField = 7;
    private const int DataStartField = 8;
    private const int DataCountField = 9;
    private const int DataCapField = 10;
    private const int HeightField = 11;
    private const int ProductField = 12;
    private const int InstructionsField = 13;
    private const int SimpleKindField = 14;
    private const int ProgramOffsetField = 15;

    private const int InitialNodes = 64;
    private const int InitialArena = 512;
    private const int InitialStack = 64;
    private const int InitialWork = 256;

    private int[] nodes;
    private int nodeCount;
    private int freeHead = Null;
    private int[] arena;
    private int arenaTop;
    private int[] stack;
    private int[] work;
    private bool disposed;

    internal PatternTree()
    {
        nodes = ArrayPool<int>.Shared.Rent(InitialNodes * Stride);
        arena = ArrayPool<int>.Shared.Rent(InitialArena);
        stack = ArrayPool<int>.Shared.Rent(InitialStack);
        work = ArrayPool<int>.Shared.Rent(InitialWork);
    }

    // The root of a parsed pattern; Null until the parse has finished.
    internal int Root { get; set; } = Null;

    // The ranges that the classes of the tree own now. A class that refers to a table owns none.
    internal int OwnedRanges { get; private set; }

    // Set when the classes own more ranges than a program may have: the parse stops storing ranges and ends with "too large".
    internal bool RangesExceeded { get; private set; }

    // Set by the build of a program: the tree notes in its nodes where the ranges went, so it is built once.
    internal bool IsBuilt { get; set; }

    // The number of nodes on the parse stack.
    internal int StackCount { get; set; }

    internal NodeKind Kind(int node) => (NodeKind)nodes[(node * Stride) + KindField];

    internal int Flags(int node) => nodes[(node * Stride) + FlagsField];

    internal int FirstChild(int node) => nodes[(node * Stride) + FirstField];

    internal int LastChild(int node) => nodes[(node * Stride) + LastField];

    internal int NextSibling(int node) => nodes[(node * Stride) + NextField];

    internal int ChildCount(int node) => nodes[(node * Stride) + CountField];

    // The counts of a repeat node; max is -1 for no upper end.
    internal int Min(int node) => nodes[(node * Stride) + AField];

    internal int Max(int node) => nodes[(node * Stride) + BField];

    // A class refers to a table of StaticTables when it is written as one escape; it owns its ranges otherwise.
    internal bool IsStaticClass(int node) => nodes[(node * Stride) + AField] >= 0;

    internal int StaticTable(int node) => nodes[(node * Stride) + AField];

    internal bool IsNegatedStatic(int node) => nodes[(node * Stride) + BField] != 0;

    internal int Height(int node) => nodes[(node * Stride) + HeightField];

    internal int Product(int node) => nodes[(node * Stride) + ProductField];

    // The instructions that the node gives after simplification, and its kind after simplification (PatternSize.LazyBit).
    internal int Instructions(int node) => nodes[(node * Stride) + InstructionsField];

    internal int SimpleKind(int node) => nodes[(node * Stride) + SimpleKindField];

    // Where the ranges of a class that owns them are in the array of ranges of the program built from the tree, as an index of a
    // pair; Null until the build has copied them. The build is the only reader and writer.
    internal int ProgramOffset(int node) => nodes[(node * Stride) + ProgramOffsetField];

    internal void SetProgramOffset(int node, int offset) => nodes[(node * Stride) + ProgramOffsetField] = offset;

    // The code points of a literal.
    internal ReadOnlySpan<int> LiteralRunes(int node) => DataOf(node);

    // The pairs lo, hi of the ranges of a class that owns them, sorted and merged once the parse is over.
    internal ReadOnlySpan<int> ClassRanges(int node) => DataOf(node);

    internal int DataCount(int node) => nodes[(node * Stride) + DataCountField];

    internal void SetKind(int node, NodeKind kind) => nodes[(node * Stride) + KindField] = (int)kind;

    internal void SetFlags(int node, int flags) => nodes[(node * Stride) + FlagsField] = flags;

    internal void SetCounts(int node, int min, int max)
    {
        nodes[(node * Stride) + AField] = min;
        nodes[(node * Stride) + BField] = max;
    }

    internal void SetStaticClass(int node, int table, bool negated)
    {
        ReleaseOwned(node);
        nodes[(node * Stride) + AField] = table;
        nodes[(node * Stride) + BField] = negated ? 1 : 0;
    }

    // The parse stack.
    internal int StackAt(int index) => stack[index];

    internal void SetStackAt(int index, int node) => stack[index] = node;

    internal void PushStack(int node)
    {
        if (StackCount == stack.Length)
        {
            Grow(ref stack, StackCount, StackCount + 1);
        }

        stack[StackCount++] = node;
    }

    // A work array of the parse, at least the given length, with the contents of the first used values kept.
    internal int[] EnsureWork(int used, int length)
    {
        if (length > work.Length)
        {
            Grow(ref work, used, length);
        }

        return work;
    }

    // A new node of a kind, with the flags; every field is set, so the old contents of a rented array never show.
    internal int NewNode(NodeKind kind, int flags)
    {
        int node;
        if (freeHead != Null)
        {
            node = freeHead;
            freeHead = NextSibling(node);
        }
        else
        {
            if ((nodeCount + 1) * Stride > nodes.Length)
            {
                Grow(ref nodes, nodeCount * Stride, (nodeCount + 1) * Stride);
            }

            node = nodeCount++;
        }

        int at = node * Stride;
        nodes[at + KindField] = (int)kind;
        nodes[at + FlagsField] = flags;
        nodes[at + FirstField] = Null;
        nodes[at + LastField] = Null;
        nodes[at + NextField] = Null;
        nodes[at + CountField] = 0;
        nodes[at + AField] = kind == NodeKind.Class ? -1 : 0;
        nodes[at + BField] = 0;
        nodes[at + DataStartField] = 0;
        nodes[at + DataCountField] = 0;
        nodes[at + DataCapField] = 0;
        nodes[at + HeightField] = 1;
        nodes[at + ProductField] = 1;
        nodes[at + InstructionsField] = 0;
        nodes[at + SimpleKindField] = (int)kind;
        nodes[at + ProgramOffsetField] = Null;
        return node;
    }

    // Makes a node free for reuse: its ranges are no longer counted, and its data are given back when they are the latest.
    internal void Free(int node)
    {
        ReleaseOwned(node);
        int at = node * Stride;
        int start = nodes[at + DataStartField];
        if (nodes[at + DataCapField] > 0 && start + nodes[at + DataCapField] == arenaTop)
        {
            arenaTop = start;
        }

        nodes[at + KindField] = (int)NodeKind.None;
        nodes[at + NextField] = freeHead;
        freeHead = node;
    }

    // Child lists. A child is in one list at most; its Next is the link.
    internal void AddChild(int parent, int child)
    {
        int at = parent * Stride;
        nodes[(child * Stride) + NextField] = Null;
        if (nodes[at + FirstField] == Null)
        {
            nodes[at + FirstField] = child;
        }
        else
        {
            nodes[(nodes[at + LastField] * Stride) + NextField] = child;
        }

        nodes[at + LastField] = child;
        nodes[at + CountField]++;
    }

    // Moves all the children of another node to the end of the list of a parent, and leaves the other with none.
    internal void MoveChildren(int parent, int other)
    {
        int otherAt = other * Stride;
        int first = nodes[otherAt + FirstField];
        if (first == Null)
        {
            return;
        }

        int at = parent * Stride;
        if (nodes[at + FirstField] == Null)
        {
            nodes[at + FirstField] = first;
        }
        else
        {
            nodes[(nodes[at + LastField] * Stride) + NextField] = first;
        }

        nodes[at + LastField] = nodes[otherAt + LastField];
        nodes[at + CountField] += nodes[otherAt + CountField];
        nodes[otherAt + FirstField] = Null;
        nodes[otherAt + LastField] = Null;
        nodes[otherAt + CountField] = 0;
    }

    // Takes the first child out of the list of a node and returns it.
    internal int RemoveFirstChild(int parent)
    {
        int at = parent * Stride;
        int first = nodes[at + FirstField];
        nodes[at + FirstField] = nodes[(first * Stride) + NextField];
        nodes[at + CountField]--;
        if (nodes[at + FirstField] == Null)
        {
            nodes[at + LastField] = Null;
        }

        nodes[(first * Stride) + NextField] = Null;
        return first;
    }

    // The work array of the parse, valid until EnsureWork grows it.
    internal int[] Work => work;

    // A slice of the data arena: the data of nodes are in it, and a slice that a node has given up still holds its values.
    internal ReadOnlySpan<int> ArenaSlice(int start, int count) => arena.AsSpan(start, count);

    // The size m of the program of the finished pattern: its instructions, the one that ends the program included, plus the ranges
    // that its classes own.
    internal int Size => PatternSize.Add(PatternSize.Add(Instructions(Root), 1), OwnedRanges);

    // Makes a node a literal of one code point.
    internal void SetLiteral(int node, int codePoint, int flags)
    {
        ReleaseOwned(node);
        SetKind(node, NodeKind.Literal);
        SetFlags(node, flags);
        nodes[(node * Stride) + DataCountField] = 0;
        AppendRoom(node, 1)[0] = codePoint;
        Finish(node);
    }

    // Makes a literal a class that owns its ranges, with none yet.
    internal void SetClass(int node)
    {
        int at = node * Stride;
        nodes[at + KindField] = (int)NodeKind.Class;
        nodes[at + AField] = -1;
        nodes[at + BField] = 0;
        nodes[at + DataCountField] = 0;
    }

    // The data of a node: code points of a literal or ranges of a class.
    internal ReadOnlySpan<int> DataOf(int node)
    {
        int at = node * Stride;
        return arena.AsSpan(nodes[at + DataStartField], nodes[at + DataCountField]);
    }

    // The data of a node, writable; valid until the next change of any data.
    internal Span<int> WritableData(int node)
    {
        int at = node * Stride;
        return arena.AsSpan(nodes[at + DataStartField], nodes[at + DataCountField]);
    }

    // Makes room for more values in the data of a node and returns the span of the room; the count grows by that many.
    internal Span<int> AppendRoom(int node, int extra)
    {
        int at = node * Stride;
        int count = nodes[at + DataCountField];
        int needed = count + extra;
        if (needed > nodes[at + DataCapField])
        {
            MoveData(node, Math.Max(8, needed * 2));
        }

        nodes[at + DataCountField] = needed;
        TrackOwned(node, count, needed);
        return arena.AsSpan(nodes[at + DataStartField] + count, extra);
    }

    // Sets the number of values of the data of a node, which is not above its capacity.
    internal void TruncateData(int node, int count)
    {
        int at = node * Stride;
        int old = nodes[at + DataCountField];
        nodes[at + DataCountField] = count;
        TrackOwned(node, old, count);
    }

    // The data of a node cut from the front: the first values are dropped, and nothing is copied.
    internal void DropDataFront(int node, int count)
    {
        int at = node * Stride;
        nodes[at + DataStartField] += count;
        nodes[at + DataCountField] -= count;
        nodes[at + DataCapField] -= count;
    }

    internal int DataStartOf(int node) => nodes[(node * Stride) + DataStartField];

    // Computes the four values of a node from its own fields and its children, which must be finished. Called when a node is built
    // and whenever its fields or its list of children change.
    internal void Finish(int node)
    {
        int at = node * Stride;
        NodeKind kind = (NodeKind)nodes[at + KindField];
        FinishBalance(at, kind);
        switch (kind)
        {
            case NodeKind.Literal:
                SetSize(at, nodes[at + DataCountField], (int)NodeKind.Literal);
                break;
            case NodeKind.EmptyMatch:
                SetSize(at, 0, (int)NodeKind.EmptyMatch);
                break;
            case NodeKind.Capture:
                SetSize(at, Instructions(nodes[at + FirstField]), (int)NodeKind.Capture);
                break;
            case NodeKind.Star:
            case NodeKind.Plus:
            case NodeKind.Quest:
            case NodeKind.Repeat:
                FinishUnary(at, kind);
                break;
            case NodeKind.Concat:
            case NodeKind.Alternate:
                FinishList(at, kind);
                break;
            default:
                SetSize(at, 1, (int)kind);
                break;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ArrayPool<int>.Shared.Return(nodes);
        ArrayPool<int>.Shared.Return(arena);
        ArrayPool<int>.Shared.Return(stack);
        ArrayPool<int>.Shared.Return(work);
        nodes = [];
        arena = [];
        stack = [];
        work = [];
    }

    private static void Grow(ref int[] array, int used, int needed)
    {
        int[] bigger = ArrayPool<int>.Shared.Rent(Math.Max(needed, array.Length * 2));
        array.AsSpan(0, used).CopyTo(bigger);
        ArrayPool<int>.Shared.Return(array);
        array = bigger;
    }

    private void SetSize(int at, int instructions, int simpleKind)
    {
        nodes[at + InstructionsField] = instructions;
        nodes[at + SimpleKindField] = simpleKind;
    }

    // The height and the product of counts of a node: from its children and, for a repeat, its counts.
    private void FinishBalance(int at, NodeKind kind)
    {
        int height = 0;
        int product = 1;
        for (int child = nodes[at + FirstField]; child != Null; child = NextSibling(child))
        {
            height = Math.Max(height, Height(child));
            product = Math.Max(product, Product(child));
        }

        nodes[at + HeightField] = height + 1;
        if (kind == NodeKind.Repeat)
        {
            // repeatIsValid of Go: a repeat with the upper count 0 is valid whatever is inside it, and the product of the others is
            // the count (the lower one when there is no upper end) times the greatest product of the children.
            int max = nodes[at + BField];
            int count = max < 0 ? nodes[at + AField] : max;
            long scaled = count == 0 ? product : (long)count * product;
            product = max == 0 ? 1 : (int)Math.Min(scaled, MaxCount + 1);
        }

        nodes[at + ProductField] = Math.Min(product, MaxCount + 1);
    }

    private void FinishUnary(int at, NodeKind kind)
    {
        int child = nodes[at + FirstField];
        bool lazy = (nodes[at + FlagsField] & PatternFlags.NonGreedy) != 0;
        int instructions;
        int simple;
        if (kind == NodeKind.Repeat)
        {
            PatternSize.Repeat(nodes[at + AField], nodes[at + BField], lazy, Instructions(child), SimpleKind(child), out instructions, out simple);
        }
        else
        {
            PatternSize.Unary(kind, lazy, Instructions(child), SimpleKind(child), out instructions, out simple);
        }

        SetSize(at, instructions, simple);
    }

    private void FinishList(int at, NodeKind kind)
    {
        int total = 0;
        for (int child = nodes[at + FirstField]; child != Null; child = NextSibling(child))
        {
            total = PatternSize.Add(total, Instructions(child));
        }

        if (kind == NodeKind.Alternate && nodes[at + CountField] > 1)
        {
            total = PatternSize.Add(total, nodes[at + CountField] - 1);
        }

        SetSize(at, total, (int)kind);
    }

    // Gives the data of a node room for the given capacity at the end of the arena, or in place when they are the latest.
    private void MoveData(int node, int capacity)
    {
        int at = node * Stride;
        int start = nodes[at + DataStartField];
        int cap = nodes[at + DataCapField];
        int count = nodes[at + DataCountField];
        if (cap > 0 && start + cap == arenaTop)
        {
            EnsureArena(start + capacity);
            arenaTop = start + capacity;
            nodes[at + DataCapField] = capacity;
            return;
        }

        EnsureArena(arenaTop + capacity);
        if (count > 0)
        {
            arena.AsSpan(start, count).CopyTo(arena.AsSpan(arenaTop, count));
        }

        nodes[at + DataStartField] = arenaTop;
        nodes[at + DataCapField] = capacity;
        arenaTop += capacity;
    }

    private void EnsureArena(int length)
    {
        if (length > arena.Length)
        {
            Grow(ref arena, arenaTop, length);
        }
    }

    // The ranges that a class owns are counted for the whole pattern; past the size of a program the parse stops storing them.
    private void TrackOwned(int node, int oldValues, int newValues)
    {
        int at = node * Stride;
        if (nodes[at + KindField] != (int)NodeKind.Class || nodes[at + AField] >= 0)
        {
            return;
        }

        OwnedRanges += (newValues - oldValues) / 2;
        if (OwnedRanges > PatternSize.MaxProgramSize)
        {
            RangesExceeded = true;
        }
    }

    private void ReleaseOwned(int node)
    {
        int at = node * Stride;
        if (nodes[at + KindField] == (int)NodeKind.Class && nodes[at + AField] < 0)
        {
            OwnedRanges -= nodes[at + DataCountField] / 2;
            nodes[at + DataCountField] = 0;
        }
    }
}
