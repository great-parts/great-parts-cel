using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A literal, the <c>Constant</c> message of <c>syntax.proto</c>.
/// </summary>
internal sealed class ConstantNode : ExprNode
{
    internal ConstantNode(int id, CelValue value)
        : base(id)
    {
        Value = value;
    }

    /// <summary>
    /// The value of the literal, of the kind <see cref="CelValueKind.Null"/>, <see cref="CelValueKind.Bool"/>,
    /// <see cref="CelValueKind.Int"/>, <see cref="CelValueKind.UInt"/>, <see cref="CelValueKind.Double"/>,
    /// <see cref="CelValueKind.String"/> or <see cref="CelValueKind.Bytes"/>.
    /// </summary>
    internal CelValue Value { get; }
}
