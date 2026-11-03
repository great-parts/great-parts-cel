using System;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// An entry of a map literal or a message literal, the <c>Expr.CreateStruct.Entry</c> message of <c>syntax.proto</c>:
/// a key and a value, or a field name and a value. It is not an expression, but it has an id and a position like a
/// node.
/// </summary>
internal sealed class StructEntry
{
    private readonly ExprNode? _key;

    internal StructEntry(int id, ExprNode key, ExprNode value, bool isOptional)
    {
        Id = id;
        _key = key;
        Value = value;
        IsOptional = isOptional;
    }

    internal StructEntry(int id, string fieldName, ExprNode value, bool isOptional)
    {
        Id = id;
        FieldName = fieldName;
        Value = value;
        IsOptional = isOptional;
    }

    /// <summary>
    /// The id of the entry, from the same sequence as the ids of the nodes of its tree.
    /// </summary>
    internal int Id { get; }

    /// <summary>
    /// The name of the field of a message entry, for an escaped field the text between the backticks; or
    /// <see langword="null"/> for a map entry.
    /// </summary>
    internal string? FieldName { get; }

    /// <summary>
    /// The key of a map entry.
    /// </summary>
    /// <exception cref="InvalidOperationException">The entry is a field of a message literal.</exception>
    internal ExprNode Key => _key ?? throw new InvalidOperationException("An entry of a message literal has a field name, not a key.");

    /// <summary>
    /// The value.
    /// </summary>
    internal ExprNode Value { get; }

    /// <summary>
    /// <see langword="true"/> for an optional entry, written <c>?k: v</c> or <c>?f: v</c>: it is added only when its
    /// value is an optional with a value.
    /// </summary>
    internal bool IsOptional { get; }
}
