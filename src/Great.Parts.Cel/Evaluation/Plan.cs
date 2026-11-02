using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A planned expression: the root node, the number of variable slots, the size of the argument stack
// and the line table of the text. It never changes, so it can be evaluated from any number of threads
// at once.
internal sealed class Plan
{
    internal Plan(PlanNode root, int slotCount, int stackSize, LineTable lines)
    {
        Root = root;
        SlotCount = slotCount;
        StackSize = stackSize;
        Lines = lines;
    }

    internal PlanNode Root { get; }

    // One slot per distinct variable of the expression.
    internal int SlotCount { get; }

    // The most argument values on the stack at one time.
    internal int StackSize { get; }

    internal LineTable Lines { get; }

    // Evaluates the plan on the values of the bindings by name: the value of the root, or its error value.
    internal CelValue Evaluate(IReadOnlyDictionary<string, CelValue> bindings) => Root.Evaluate(new Frame(this, bindings));
}
