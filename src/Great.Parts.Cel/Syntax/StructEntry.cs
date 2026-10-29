namespace Great.Parts.Cel.Syntax;

/// <summary>
/// An entry of a map literal or a message literal, the <c>Expr.CreateStruct.Entry</c> message of <c>syntax.proto</c>.
/// It is not an expression, but it has an id and a position like a node.
/// </summary>
internal sealed class StructEntry
{
    internal StructEntry(int id, ExprNode key, ExprNode value)
    {
        Id = id;
        Key = key;
        Value = value;
    }

    /// <summary>
    /// The id of the entry, from the same sequence as the ids of the nodes of its tree.
    /// </summary>
    internal int Id { get; }

    /// <summary>
    /// The key of a map entry.
    /// </summary>
    internal ExprNode Key { get; }

    /// <summary>
    /// The value.
    /// </summary>
    internal ExprNode Value { get; }
}
