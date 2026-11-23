
namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A field <c>name: value</c> of a message literal: the <c>Expr.CreateStruct.Entry</c> message of <c>syntax.proto</c>
/// with a field key.
/// </summary>
/// <remarks>
/// <para>
/// A field is not an expression, but it has an id from the same sequence as the nodes of its tree and a position: the
/// <c>:</c>.
/// </para>
/// <para>The field is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelStructFieldNode
{
    internal CelStructFieldNode(int id, string name, CelExprNode value, bool isOptional)
    {
        Id = id;
        Name = name;
        Value = value;
        IsOptional = isOptional;
    }

    /// <summary>
    /// The id of the field, unique among the nodes and entries of its tree.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The name of the field; for a field in backticks, the text between them.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The value.
    /// </summary>
    public CelExprNode Value { get; }

    /// <summary>
    /// <see langword="true"/> for an optional field, written <c>?name: value</c>: it is set only when its value is an optional
    /// with a value.
    /// </summary>
    public bool IsOptional { get; }
}
