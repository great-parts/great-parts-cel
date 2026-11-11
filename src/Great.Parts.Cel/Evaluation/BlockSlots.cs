namespace Great.Parts.Cel.Evaluation;

// The slots of one block as the planner made them: the number of the block, the first of its cells in the frame, and the plan
// node of each slot expression. The nodes are set while the block is planned, before any evaluation, because a slot may read a
// later one; after planning the object never changes, so any number of threads share it.
internal sealed class BlockSlots
{
    internal BlockSlots(int block, int firstCell, int count)
    {
        Block = block;
        FirstCell = firstCell;
        Expressions = new PlanNode[count];
    }

    // The index of the block among the blocks of the plan; the frame keeps the epoch of the block at this index.
    internal int Block { get; }

    // The index in the frame of the cell of the first slot; the cell of slot N is FirstCell + N.
    internal int FirstCell { get; }

    // The plan node of each slot expression, by slot number.
    internal PlanNode[] Expressions { get; }

    internal int Count => Expressions.Length;
}
