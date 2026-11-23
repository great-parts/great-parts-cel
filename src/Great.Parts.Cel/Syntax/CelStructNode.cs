using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A message literal <c>TypeName{field: value}</c>, which creates an object of a protobuf message or of a type of the
/// application: the <c>Expr.CreateStruct</c> message of <c>syntax.proto</c> with a message name.
/// </summary>
/// <remarks>The node is immutable and can be read from any number of threads at once.</remarks>
public sealed class CelStructNode : CelExprNode
{
    internal CelStructNode(int id, string typeName, ImmutableArray<CelStructFieldNode> fields)
        : base(id, CelExprKind.Struct)
    {
        TypeName = typeName;
        Fields = fields;
    }

    /// <summary>
    /// The name of the type as written, a leading dot included, as in <c>.acme.Order</c>; type checking resolves it in the
    /// container of the environment.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// The fields in the order of the text.
    /// </summary>
    public ImmutableArray<CelStructFieldNode> Fields { get; }

    /// <summary>
    /// The number of fields.
    /// </summary>
    internal int Count => Fields.Length;

    /// <summary>
    /// The value of a field, read without making <see cref="CelExprNode.Children"/>.
    /// </summary>
    /// <param name="index">The index of the field.</param>
    /// <returns>The value.</returns>
    internal CelExprNode ValueOf(int index) => Fields[index].Value;

    /// <summary>
    /// The optional flag of each field, in the order of the fields.
    /// </summary>
    /// <returns>The flags.</returns>
    internal ImmutableArray<bool> OptionalFlags()
    {
        bool[] flags = new bool[Fields.Length];
        for (int i = 0; i < flags.Length; i++)
        {
            flags[i] = Fields[i].IsOptional;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(flags);
    }

    /// <summary>
    /// The name of each field, in the order of the fields.
    /// </summary>
    /// <returns>The names.</returns>
    internal ImmutableArray<string> FieldNames()
    {
        string[] names = new string[Fields.Length];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = Fields[i].Name;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(names);
    }

    /// <summary>
    /// The id of a field.
    /// </summary>
    /// <param name="index">The index of the field.</param>
    /// <returns>The id.</returns>
    internal int FieldId(int index) => Fields[index].Id;

    /// <summary>
    /// Tells whether a field is optional.
    /// </summary>
    /// <returns><see langword="true"/> when at least one field is optional.</returns>
    internal bool HasOptionalEntry()
    {
        foreach (CelStructFieldNode field in Fields)
        {
            if (field.IsOptional)
            {
                return true;
            }
        }

        return false;
    }

    private protected override CelExprNode[] CreateChildren()
    {
        var children = new CelExprNode[Fields.Length];
        for (int i = 0; i < Fields.Length; i++)
        {
            children[i] = Fields[i].Value;
        }

        return children;
    }
}
