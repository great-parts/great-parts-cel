using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A read of @indexN in a block: the first read of an epoch marks the cell as in progress, evaluates the slot expression and keeps
// its value, an error value too; every later read takes the kept value. A read of a cell that is in progress, a slot that reads
// itself directly or through other slots, is Unbound at the identifier. A read costs 1 as a read of a variable does, 0 when the
// expansion of a macro added it; the first read also costs the slot expression.
internal sealed class SlotPlanNode : PlanNode
{
    private readonly BlockSlots _slots;
    private readonly int _cell;
    private readonly string _name;

    internal SlotPlanNode(int offset, ulong cost, BlockSlots slots, int number, string name)
        : base(offset, cost)
    {
        _slots = slots;
        _cell = slots.FirstCell + number;
        Number = number;
        _name = name;
    }

    // The number of the slot in its block.
    internal int Number { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        Begin(frame);
        ulong epoch = frame.BlockEpoch(_slots.Block) << 1;
        ulong state = frame.SlotState(_cell);
        if (state == (epoch | 1))
        {
            return EvaluationErrors.Placed(frame.SlotValue(_cell), frame.Lines, Offset);
        }

        if (state == epoch)
        {
            return EvaluationErrors.Unbound(_name, frame.Lines, Offset);
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
