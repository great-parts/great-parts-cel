
namespace Great.Parts.Cel.Syntax;

/// <summary>
/// An entry <c>key: value</c> of a map literal: the <c>Expr.CreateStruct.Entry</c> message of <c>syntax.proto</c> with a
/// map key.
/// </summary>
/// <remarks>
/// <para>
/// An entry is not an expression, but it has an id from the same sequence as the nodes of its tree and a position: the
/// <c>:</c>.
/// </para>
/// <para>The entry is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelMapEntryNode
{
    internal CelMapEntryNode(int id, CelExprNode key, CelExprNode value, bool isOptional)
    {
        Id = id;
        Key = key;
        Value = value;
        IsOptional = isOptional;
    }

    /// <summary>
    /// The id of the entry, unique among the nodes and entries of its tree.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The key.
    /// </summary>
    public CelExprNode Key { get; }

    /// <summary>
    /// The value.
    /// </summary>
    public CelExprNode Value { get; }

    /// <summary>
    /// <see langword="true"/> for an optional entry, written <c>?key: value</c>: it is added only when its value is an
    /// optional with a value.
    /// </summary>
    public bool IsOptional { get; }
}
