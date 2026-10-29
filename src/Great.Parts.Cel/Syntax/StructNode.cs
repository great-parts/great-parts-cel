using System.Collections.Immutable;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A map literal or a message literal, the <c>Expr.CreateStruct</c> message of <c>syntax.proto</c>.
/// </summary>
internal sealed class StructNode : ExprNode
{
    internal StructNode(int id, string messageName, ImmutableArray<StructEntry> entries)
        : base(id)
    {
        MessageName = messageName;
        Entries = entries;
    }

    /// <summary>
    /// The name of the message type, or the empty string for a map.
    /// </summary>
    internal string MessageName { get; }

    /// <summary>
    /// The entries in the order of the text.
    /// </summary>
    internal ImmutableArray<StructEntry> Entries { get; }
}
