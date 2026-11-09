using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The fields a builder of a type described at run time collects: the next number, distinct identifiers, no type
// parameter, each checked at once.
internal sealed class FieldList
{
    private readonly List<CelObjectField> _fields = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    // The full CEL name of a type, checked.
    internal static string CheckTypeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return DeclarationNames.IsQualifiedIdentifier(name)
            ? name
            : throw new ArgumentException("The type name '" + name + "' is not a full CEL name.", nameof(name));
    }

    internal void Add(string name, CelType type)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(type);
        if (!DeclarationNames.IsWordName(name))
        {
            throw new ArgumentException("The field name '" + name + "' is not an identifier.", nameof(name));
        }

        if (type.Kind == CelTypeKind.TypeParameter)
        {
            throw new ArgumentException("The field '" + name + "' has the type parameter " + type + ", which no value has.", nameof(type));
        }

        if (!_names.Add(name))
        {
            throw new ArgumentException("The field '" + name + "' is added twice.", nameof(name));
        }

        _fields.Add(new CelObjectField(_fields.Count, name, type));
    }

    internal CelObjectField[] ToArray() => [.. _fields];
}
