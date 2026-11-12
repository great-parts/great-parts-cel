using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A read of @indexN in a block: the first read of an epoch marks the cell as in progress, evaluates the slot expression and keeps
// its value, an error value too; every later read takes the kept value. A read of a cell that is in progress, a slot that reads
// itself directly or through other slots, does not end there: it goes on to the cell of the same number in the next enclosing
// block that has that many slots, which is read in the same way, and is Unbound at the identifier only when no block is left. A
// read costs 1 as a read of a variable does, 0 when the expansion of a macro added it, whichever cell serves it; the first read of
// a cell also costs the slot expression.
internal sealed class SlotPlanNode : PlanNode
{
    private readonly BlockSlots _slots;
    private readonly int _cell;
    private readonly string _name;

    // The read of the same number in the next enclosing block that has it; null in the outermost one.
    private readonly SlotPlanNode? _outer;

    internal SlotPlanNode(int offset, ulong cost, BlockSlots slots, int number, string name, SlotPlanNode? outer = null)
        : base(offset, cost)
    {
        _slots = slots;
        _cell = slots.FirstCell + number;
        Number = number;
        _name = name;
        _outer = outer;
    }

    // The number of the slot in its block.
    internal int Number { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        Begin(frame);
        return Read(frame);
    }

    // The value of the cell of this block, or what the enclosing blocks give when it is in progress.
    private CelValue Read(Frame frame)
    {
        ulong epoch = frame.BlockEpoch(_slots.Block) << 1;
        ulong state = frame.SlotState(_cell);
        if (state == (epoch | 1))
        {
            return EvaluationErrors.Placed(frame.SlotValue(_cell), frame.Lines, Offset);
        }

        if (state == epoch)
        {
            return _outer is null ? EvaluationErrors.Unbound(_name, frame.Lines, Offset) : _outer.Read(frame);
        }

        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        frame.SetSlotState(_cell, epoch);
        CelValue value = _slots.Expressions[Number].Evaluate(frame);
        frame.KeepSlot(_cell, value, epoch | 1);
        return EvaluationErrors.Placed(value, frame.Lines, Offset);
    }
}
