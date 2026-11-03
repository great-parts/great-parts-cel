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
    /// The name of the message type as written, a leading dot included, or the empty string for a map.
    /// </summary>
    internal string MessageName { get; }

    /// <summary>
    /// The entries in the order of the text.
    /// </summary>
    internal ImmutableArray<StructEntry> Entries { get; }

    /// <summary>
    /// Tells whether an entry is optional, <c>?k: v</c> or <c>?f: v</c>.
    /// </summary>
    /// <returns><see langword="true"/> when at least one entry is optional.</returns>
    internal bool HasOptionalEntry()
    {
        foreach (StructEntry entry in Entries)
        {
            if (entry.IsOptional)
            {
                return true;
            }
        }

        return false;
    }
}
