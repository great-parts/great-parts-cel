using System;
using System.Buffers;
using System.Diagnostics;
using Great.Parts.Cel.Library.Unicode;

namespace Great.Parts.Cel.Library.Regex;

// The state of one build of a program. The walk of the tree is a loop over a stack of tasks: a task visits a node or combines the
// fragments that the visits left on a second stack. A fragment is a run of instructions with its start and the list of its open
// ends, which the next fragment fills in; the lists are threaded through the open fields themselves, so they need no memory of
// their own. A fragment with no instruction is empty: it stands for a path that goes through. Both stacks are arrays from the
// pool, returned by Dispose; the arrays of the program are the only memory that outlives the build.
//
// The program has exactly the instructions that PatternSize counted. A counted repeat is expanded as simplify.go of Go does, and a
// repeat of a repeat that the simplification merges is one fragment, which the kinds that the tree keeps tell.
internal ref struct ProgramWalk
{
    private const int Null = -1;

    private const int VisitTask = 0;
    private const int ConcatTask = 1;
    private const int AltTask = 2;
    private const int StarTask = 3;
    private const int PlusTask = 4;
    private const int QuestTask = 5;
    private const int ConcatBelowFirstTask = 6;

    private const int InitialTasks = 128;
    private const int InitialFragments = 96;

    private readonly PatternTree tree;
    private readonly Instruction[] instructions;
    private readonly int[] ranges;
    private int instructionTop;
    private int rangeTop;
    private int[] tasks;
    private int taskTop;
    private int[] fragments;
    private int fragmentTop;

    internal ProgramWalk(PatternTree tree)
    {
        this.tree = tree;
        instructions = new Instruction[tree.Instructions(tree.Root) + 1];
        ranges = tree.OwnedRanges == 0 ? [] : new int[tree.OwnedRanges * 2];
        instructionTop = 0;
        rangeTop = 0;
        tasks = ArrayPool<int>.Shared.Rent(InitialTasks);
        taskTop = 0;
        fragments = ArrayPool<int>.Shared.Rent(InitialFragments);
        fragmentTop = 0;
    }

    internal CompiledPattern Run()
    {
        PushTask(VisitTask, tree.Root);
        while (taskTop > 0)
        {
            taskTop -= 2;
            Execute(tasks[taskTop], tasks[taskTop + 1]);
        }

        Frag root = PopFragment();
        int match = NewInstruction(InstructionKind.Match);
        int start = match;
        if (!root.IsEmpty)
        {
            Patch(root.Holes, match);
            start = root.Start;
        }

        Debug.Assert(instructionTop == instructions.Length, "The program has the number of instructions that the parse counted.");

        // The ranges of a class that is only under x{0} were counted by the parse and are not in the program.
        int[] used = rangeTop == ranges.Length ? ranges : ranges.AsSpan(0, rangeTop).ToArray();
        return new CompiledPattern(instructions, used, tree.Size, start);
    }

    internal void Dispose()
    {
        ArrayPool<int>.Shared.Return(tasks);
        ArrayPool<int>.Shared.Return(fragments);
        tasks = [];
        fragments = [];
    }

    private void Execute(int code, int argument)
    {
        switch (code)
        {
            case VisitTask:
                VisitNode(argument);
                break;
            case ConcatTask:
                PushFragment(ConcatTop(argument));
                break;
            case AltTask:
                PushFragment(AltTop(argument));
                break;
            case StarTask:
                PushFragment(Loop(PopFragment(), false));
                break;
            case PlusTask:
                PushFragment(Loop(PopFragment(), true));
                break;
            case QuestTask:
                PushFragment(Alt(PopFragment(), Frag.Empty));
                break;
            default:
                Frag right = PopFragment();
                Frag left = PopFragment();
                PushFragment(Concat(left, right));
                break;
        }
    }

    private void VisitNode(int node)
    {
        NodeKind kind = tree.Kind(node);
        switch (kind)
        {
            case NodeKind.EmptyMatch:
                PushFragment(Frag.Empty);
                break;
            case NodeKind.Literal:
                PushFragment(Literal(node));
                break;
            case NodeKind.Class:
                PushFragment(ClassOf(node));
                break;
            case NodeKind.Capture:
                PushTask(VisitTask, tree.FirstChild(node));
                break;
            case NodeKind.Star:
                VisitUnary(node, StarTask);
                break;
            case NodeKind.Plus:
                VisitUnary(node, PlusTask);
                break;
            case NodeKind.Quest:
                VisitUnary(node, QuestTask);
                break;
            case NodeKind.Repeat:
                VisitRepeat(node);
                break;
            case NodeKind.Concat:
                VisitList(node, ConcatTask);
                break;
            case NodeKind.Alternate:
                VisitList(node, AltTask);
                break;
            default:
                PushFragment(Single(kind));
                break;
        }
    }

    // The nodes that are one instruction and have no children.
    private Frag Single(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.AnyChar => Leaf(InstructionKind.Any, 0, 0),
            NodeKind.AnyCharNotNewline => Leaf(InstructionKind.AnyNotNewline, 0, 0),
            NodeKind.BeginLine => Leaf(InstructionKind.Empty, EmptyCondition.BeginLine, 0),
            NodeKind.EndLine => Leaf(InstructionKind.Empty, EmptyCondition.EndLine, 0),
            NodeKind.BeginText => Leaf(InstructionKind.Empty, EmptyCondition.BeginText, 0),
            NodeKind.EndText => Leaf(InstructionKind.Empty, EmptyCondition.EndText, 0),
            NodeKind.WordBoundary => Leaf(InstructionKind.Empty, EmptyCondition.WordBoundary, 0),
            NodeKind.NoWordBoundary => Leaf(InstructionKind.Empty, EmptyCondition.NoWordBoundary, 0),

            // A pattern that matches nothing is a class that holds nothing.
            _ => Leaf(InstructionKind.Class, 0, 0),
        };
    }

    // The children are visited last to first, so that the combination, which takes the top of the stack first, reads them in order.
    private void VisitList(int node, int combine)
    {
        PushTask(combine, tree.ChildCount(node));
        for (int child = tree.FirstChild(node); child != Null; child = tree.NextSibling(child))
        {
            PushTask(VisitTask, child);
        }
    }

    // A star, plus or quest of a node. When the simplification merges it into the child — a star of a star, or of an empty match —
    // it has the instructions of the child, and the child alone is built.
    private void VisitUnary(int node, int operation)
    {
        int child = tree.FirstChild(node);
        if (tree.Instructions(node) != tree.Instructions(child))
        {
            PushTask(operation, 0);
        }

        PushTask(VisitTask, child);
    }

    private void VisitRepeat(int node)
    {
        int child = tree.FirstChild(node);
        int min = tree.Min(node);
        int max = tree.Max(node);
        bool lazy = (tree.Flags(node) & PatternFlags.NonGreedy) != 0;
        if (max < 0)
        {
            AtLeast(node, child, min, lazy);
        }
        else if (max == 0)
        {
            PushFragment(Frag.Empty);
        }
        else if (min == 1 && max == 1)
        {
            PushTask(VisitTask, child);
        }
        else
        {
            Between(child, min, max, lazy);
        }
    }

    // x{0,} is x*, x{1,} is x+, and x{n,} is n - 1 copies of x and x+. The plus is built first, so that the combination, which takes the
    // top of the stack first, puts the copies before it.
    private void AtLeast(int node, int child, int min, bool lazy)
    {
        if (min <= 1)
        {
            VisitUnary(node, min == 0 ? StarTask : PlusTask);
            return;
        }

        int childInstructions = tree.Instructions(child);
        PatternSize.Unary(NodeKind.Plus, lazy, childInstructions, tree.SimpleKind(child), out int plusInstructions, out _);
        PushTask(ConcatTask, min);
        for (int i = 1; i < min; i++)
        {
            PushTask(VisitTask, child);
        }

        if (plusInstructions != childInstructions)
        {
            PushTask(PlusTask, 0);
        }

        PushTask(VisitTask, child);
    }

    // x{n,m}: n copies of x, and the nested x(x(x)?)? of m - n levels. The tasks are pushed last first.
    private void Between(int child, int min, int max, bool lazy)
    {
        int levels = max - min;
        int childInstructions = tree.Instructions(child);
        PatternSize.Unary(NodeKind.Quest, lazy, childInstructions, tree.SimpleKind(child), out int firstInstructions, out _);
        if (min > 0 && levels > 0)
        {
            PushTask(ConcatBelowFirstTask, 0);
        }

        for (int level = levels; level >= 2; level--)
        {
            PushTask(QuestTask, 0);
            PushTask(ConcatTask, 2);
            PushTask(VisitTask, child);
        }

        if (levels >= 1)
        {
            if (firstInstructions != childInstructions)
            {
                PushTask(QuestTask, 0);
            }

            PushTask(VisitTask, child);
        }

        if (min > 1)
        {
            PushTask(ConcatTask, min);
        }

        for (int i = 0; i < min; i++)
        {
            PushTask(VisitTask, child);
        }
    }

    private Frag Literal(int node)
    {
        bool fold = (tree.Flags(node) & PatternFlags.FoldCase) != 0;
        Frag run = Frag.Empty;
        foreach (int codePoint in tree.LiteralRunes(node))
        {
            // A code point with no other member in its orbit has nothing to fold.
            bool folds = fold && UnicodeTables.Next(codePoint) != codePoint;
            run = Concat(run, Leaf(folds ? InstructionKind.CharFold : InstructionKind.Char, codePoint, 0));
        }

        return run;
    }

    // A class that refers to a table is one instruction on the table; a class with ranges is one on its run of pairs, and the copies
    // that a counted repeat makes share the run.
    private Frag ClassOf(int node)
    {
        if (tree.IsStaticClass(node))
        {
            return Leaf(InstructionKind.StaticClass, tree.StaticTable(node), tree.IsNegatedStatic(node) ? 1 : 0);
        }

        ReadOnlySpan<int> data = tree.ClassRanges(node);
        int offset = tree.ProgramOffset(node);
        if (offset == Null)
        {
            offset = rangeTop >> 1;
            data.CopyTo(ranges.AsSpan(rangeTop));
            rangeTop += data.Length;
            tree.SetProgramOffset(node, offset);
        }

        return Leaf(InstructionKind.Class, offset, data.Length >> 1);
    }

    private Frag ConcatTop(int count)
    {
        if (count == 0)
        {
            return Frag.Empty;
        }

        Frag result = PopFragment();
        for (int i = 1; i < count; i++)
        {
            result = Concat(result, PopFragment());
        }

        return result;
    }

    private Frag AltTop(int count)
    {
        Frag result = PopFragment();
        for (int i = 1; i < count; i++)
        {
            result = Alt(result, PopFragment());
        }

        return result;
    }

    // The first fragment, then the second: the open ends of the first go to the start of the second.
    private Frag Concat(Frag first, Frag second)
    {
        if (first.IsEmpty)
        {
            return second;
        }

        if (second.IsEmpty)
        {
            return first;
        }

        Patch(first.Holes, second.Start);
        return new Frag(first.Start, second.Holes);
    }

    // A fork to either fragment. An empty fragment is a path out of the fork, so the field that would name its start is open.
    private Frag Alt(Frag first, Frag second)
    {
        int fork = NewInstruction(InstructionKind.Alt);
        Holes open = Holes.None;
        if (first.IsEmpty)
        {
            open = Join(open, Holes.Of(fork << 1));
        }
        else
        {
            instructions[fork].Next = first.Start;
            open = Join(open, first.Holes);
        }

        if (second.IsEmpty)
        {
            open = Join(open, Holes.Of((fork << 1) | 1));
        }
        else
        {
            instructions[fork].A = second.Start;
            open = Join(open, second.Holes);
        }

        return new Frag(fork, open);
    }

    // A fork whose first successor is the fragment, which returns to it; the second is the way out. A star starts at the fork, a plus
    // at the fragment. The fork of an empty fragment returns to itself.
    private Frag Loop(Frag body, bool plus)
    {
        int fork = NewInstruction(InstructionKind.Alt);
        Holes exit = Holes.Of((fork << 1) | 1);
        if (body.IsEmpty)
        {
            instructions[fork].Next = fork;
            return new Frag(fork, exit);
        }

        instructions[fork].Next = body.Start;
        Patch(body.Holes, fork);
        return new Frag(plus ? body.Start : fork, exit);
    }

    private Frag Leaf(InstructionKind kind, int a, int b)
    {
        int index = NewInstruction(kind);
        instructions[index].A = a;
        instructions[index].B = b;
        return new Frag(index, Holes.Of(index << 1));
    }

    // A new instruction whose fields that a list of open ends may use hold the end of a list.
    private int NewInstruction(InstructionKind kind)
    {
        int index = instructionTop++;
        ref Instruction created = ref instructions[index];
        created.Kind = kind;
        created.Next = Null;
        created.A = Null;
        created.B = 0;
        return index;
    }

    // The open ends are numbered 2 * instruction for Next and 2 * instruction + 1 for A; an open field holds the next end of its list.
    private Holes Join(Holes first, Holes second)
    {
        if (first.IsNone)
        {
            return second;
        }

        if (second.IsNone)
        {
            return first;
        }

        SetHole(first.Tail, second.Head);
        return new Holes(first.Head, second.Tail);
    }

    private void Patch(Holes list, int target)
    {
        int hole = list.Head;
        while (hole != Null)
        {
            int next = GetHole(hole);
            SetHole(hole, target);
            hole = next;
        }
    }

    private int GetHole(int hole)
    {
        ref Instruction owner = ref instructions[hole >> 1];
        return (hole & 1) == 0 ? owner.Next : owner.A;
    }

    private void SetHole(int hole, int value)
    {
        ref Instruction owner = ref instructions[hole >> 1];
        if ((hole & 1) == 0)
        {
            owner.Next = value;
        }
        else
        {
            owner.A = value;
        }
    }

    private void PushTask(int code, int argument)
    {
        if (taskTop + 2 > tasks.Length)
        {
            Grow(ref tasks, taskTop);
        }

        tasks[taskTop] = code;
        tasks[taskTop + 1] = argument;
        taskTop += 2;
    }

    private void PushFragment(Frag fragment)
    {
        if (fragmentTop + 3 > fragments.Length)
        {
            Grow(ref fragments, fragmentTop);
        }

        fragments[fragmentTop] = fragment.Start;
        fragments[fragmentTop + 1] = fragment.Holes.Head;
        fragments[fragmentTop + 2] = fragment.Holes.Tail;
        fragmentTop += 3;
    }

    private Frag PopFragment()
    {
        fragmentTop -= 3;
        return new Frag(fragments[fragmentTop], new Holes(fragments[fragmentTop + 1], fragments[fragmentTop + 2]));
    }

    private static void Grow(ref int[] array, int used)
    {
        int[] bigger = ArrayPool<int>.Shared.Rent(array.Length * 2);
        array.AsSpan(0, used).CopyTo(bigger);
        ArrayPool<int>.Shared.Return(array);
        array = bigger;
    }

    // The open ends of a fragment: the first and the last of a list.
    private readonly struct Holes(int head, int tail)
    {
        internal static Holes None => new(Null, Null);

        internal int Head { get; } = head;

        internal int Tail { get; } = tail;

        internal bool IsNone => Head == Null;

        internal static Holes Of(int hole) => new(hole, hole);
    }

    // A fragment of the program: the first instruction and the open ends. An empty fragment has no instruction.
    private readonly struct Frag(int start, Holes holes)
    {
        internal static Frag Empty => new(Null, Holes.None);

        internal int Start { get; } = start;

        internal Holes Holes { get; } = holes;

        internal bool IsEmpty => Start == Null;
    }
}
