using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Configuration;

// The object greatPartsCel: objectTypes, objects of name and fields, each field with name and type. Every type is described
// over a dictionary, as CelObjectTypeBuilder.ForDictionary does, with the fields in the order of the array.
internal sealed partial class EnvironmentReader
{
    private void ReadGreatPartsCel(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "'greatPartsCel'", "an object");
            return;
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            if (key == "objectTypes")
            {
                ReadObjectTypes(ref reader);
            }
            else
            {
                KeyError(ref reader, key, keyIndex, isKnown: false);
            }
        }
    }

    private void ReadObjectTypes(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'objectTypes'", "an array");
            return;
        }

        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            ReadObjectType(ref reader);
        }
    }

    // A type with an error in itself or in a field is not built; the reading goes on with the next element.
    private void ReadObjectType(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "an element of 'objectTypes'", "an object");
            return;
        }

        int objectIndex = TokenIndex(ref reader);
        int errorsBefore = _errors.Count;
        string? name = null;
        List<PendingField> fields = [];
        HashSet<string> keys = new(StringComparer.Ordinal);
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            switch (key)
            {
                case "name":
                    name = ReadObjectTypeName(ref reader);
                    break;
                case "fields":
                    ReadFields(ref reader, fields);
                    break;
                default:
                    KeyError(ref reader, key, keyIndex, isKnown: false);
                    break;
            }
        }

        if (!keys.Contains("name"))
        {
            AddError(objectIndex, "an object type has no 'name'");
        }

        if (_errors.Count == errorsBefore && name is not null)
        {
            _objectTypes.Add(Build(name, objectIndex, fields));
        }
    }

    private string? ReadObjectTypeName(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? name = ReadString(ref reader, "'name'");
        if (name is not null && !DeclarationNames.IsQualifiedIdentifier(name))
        {
            AddError(index, $"the type name '{name}' is not a full name");
            return null;
        }

        return name;
    }

    private void ReadFields(ref Utf8JsonReader reader, List<PendingField> fields)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'fields'", "an array");
            return;
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            ReadField(ref reader, fields, names);
        }
    }

    // One field; its name counts as taken even when its type has an error, so that a repeat is reported.
    private void ReadField(ref Utf8JsonReader reader, List<PendingField> fields, HashSet<string> names)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "an element of 'fields'", "an object");
            return;
        }

        int objectIndex = TokenIndex(ref reader);
        string? name = null;
        CelType? type = null;
        HashSet<string> keys = new(StringComparer.Ordinal);
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            switch (key)
            {
                case "name":
                    name = ReadFieldName(ref reader, names);
                    break;
                case "type":
                    type = ReadFieldType(ref reader);
                    break;
                default:
                    KeyError(ref reader, key, keyIndex, isKnown: false);
                    break;
            }
        }

        if (!keys.Contains("name"))
        {
            AddError(objectIndex, "a field has no 'name'");
        }

        if (!keys.Contains("type"))
        {
            AddError(objectIndex, "a field has no 'type'");
        }

        if (name is not null && type is not null)
        {
            fields.Add(new PendingField(name, type, objectIndex));
        }
    }

    // The name of a field: an identifier that no field of the type has.
    private string? ReadFieldName(ref Utf8JsonReader reader, HashSet<string> names)
    {
        int index = TokenIndex(ref reader);
        string? name = ReadString(ref reader, "'name'");
        if (name is null)
        {
            return null;
        }

        if (!DeclarationNames.IsWordName(name))
        {
            AddError(index, $"the field name '{name}' is not an identifier");
            return null;
        }

        if (!names.Add(name))
        {
            AddError(index, $"the field '{name}' is declared twice in the type");
            return null;
        }

        return name;
    }

    private CelType? ReadFieldType(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? text = ReadString(ref reader, "'type'");
        return text is null ? null : ResolveType(text, index, allowTypeParameters: false, "a field");
    }

    // The description of the type over a dictionary, and the byte indexes of its fields in the order of the fields.
    private static PendingObjectType Build(string name, int objectIndex, List<PendingField> fields)
    {
        CelObjectTypeBuilder builder = CelObjectTypeBuilder.ForDictionary(name);
        ImmutableArray<int>.Builder indexes = ImmutableArray.CreateBuilder<int>(fields.Count);
        foreach (PendingField field in fields)
        {
            builder.AddField(field.Name, field.Type);
            indexes.Add(field.ByteIndex);
        }

        return new PendingObjectType(builder.Build(), objectIndex, indexes.MoveToImmutable());
    }

    // A field that passed its checks, and the byte index of the "{" of its element.
    private readonly record struct PendingField(string Name, CelType Type, int ByteIndex);
}
