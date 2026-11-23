using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A map literal <c>{k: v, ?k2: v2}</c>: the <c>Expr.CreateStruct</c> message of <c>syntax.proto</c> without a message
/// name.
/// </summary>
/// <remarks>The node is immutable and can be read from any number of threads at once.</remarks>
public sealed class CelMapNode : CelExprNode
{
    internal CelMapNode(int id, ImmutableArray<CelMapEntryNode> entries)
        : base(id, CelExprKind.Map)
    {
        Entries = entries;
    }

    /// <summary>
    /// The entries in the order of the text.
    /// </summary>
    public ImmutableArray<CelMapEntryNode> Entries { get; }

    /// <summary>
    /// The number of entries.
    /// </summary>
    internal int Count => Entries.Length;

    /// <summary>
    /// The key of an entry, read without making <see cref="CelExprNode.Children"/>.
    /// </summary>
    /// <param name="index">The index of the entry.</param>
    /// <returns>The key.</returns>
    internal CelExprNode KeyOf(int index) => Entries[index].Key;

    /// <summary>
    /// The value of an entry, read without making <see cref="CelExprNode.Children"/>.
    /// </summary>
    /// <param name="index">The index of the entry.</param>
    /// <returns>The value.</returns>
    internal CelExprNode ValueOf(int index) => Entries[index].Value;

    /// <summary>
    /// The optional flag of each entry, in the order of the entries.
    /// </summary>
    /// <returns>The flags.</returns>
    internal ImmutableArray<bool> OptionalFlags()
    {
        bool[] flags = new bool[Entries.Length];
        for (int i = 0; i < flags.Length; i++)
        {
            flags[i] = Entries[i].IsOptional;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(flags);
    }

    /// <summary>
    /// Tells whether an entry is optional.
    /// </summary>
    /// <returns><see langword="true"/> when at least one entry is optional.</returns>
    internal bool HasOptionalEntry()
    {
        foreach (CelMapEntryNode entry in Entries)
        {
            if (entry.IsOptional)
            {
                return true;
            }
        }

        return false;
    }

    private protected override CelExprNode[] CreateChildren()
    {
        var children = new CelExprNode[Entries.Length * 2];
        for (int i = 0; i < Entries.Length; i++)
        {
            children[2 * i] = Entries[i].Key;
            children[(2 * i) + 1] = Entries[i].Value;
        }

        return children;
    }
}
