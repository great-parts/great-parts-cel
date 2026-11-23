using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The node of a key that is an expression, in a chain of attributes of a partial plan: l[i + 1], m[k]. The chain node evaluates its
// keys before it looks at the chain, to match the patterns of unknown data against the whole access, and stores the values in the
// partial state; this node then gives the stored value to the node of the chain that uses the key, so that the key is evaluated, and
// charged, once. Where nothing is stored — in any evaluation without patterns — it evaluates the key node as it is. It costs nothing.
internal sealed class KeyPlanNode : PlanNode
{
    private readonly PlanNode _key;

    internal KeyPlanNode(int offset, int slot, PlanNode key)
        : base(offset, 0)
    {
        Slot = slot;
        _key = key;
    }

    // The slot of the stored value in the partial state.
    internal int Slot { get; }

    // The value of the key node itself, which the chain node needs before it matches.
    internal CelValue EvaluateKey(Frame frame) => _key.Evaluate(frame);

    internal override CelValue Evaluate(Frame frame) =>
        frame.Partial is { } state && state.TryTakeKey(Slot, out CelValue stored) ? stored : _key.Evaluate(frame);
}
