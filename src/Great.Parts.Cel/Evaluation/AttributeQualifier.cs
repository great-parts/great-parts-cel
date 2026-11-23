using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A qualifier of a chain of attributes in the tree: a constant — a field name or a key written as a literal — or the node of a key
// that is an expression.
internal sealed class AttributeQualifier(CelValue constant, CelExprNode? key)
{
    internal CelValue Constant { get; } = constant;

    internal CelExprNode? Key { get; } = key;
}
