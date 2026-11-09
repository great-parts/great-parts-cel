using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Configuration;

// The key variables: objects of name, type or type_name with params, and description.
internal sealed partial class EnvironmentReader
{
    private void ReadVariables(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'variables'", "an array");
            return;
        }

        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            ReadVariable(ref reader);
        }
    }

    private void ReadVariable(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "an element of 'variables'", "an object");
            return;
        }

        int objectIndex = TokenIndex(ref reader);
        Variable variable = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            ReadVariableValue(ref reader, variable, key, keyIndex);
        }

        CompleteVariable(variable, keys, objectIndex);
    }

    private void ReadVariableValue(ref Utf8JsonReader reader, Variable variable, string key, int keyIndex)
    {
        switch (key)
        {
            case "name":
                variable.Name = ReadVariableName(ref reader);
                break;
            case "type":
                variable.Type = ReadVariableType(ref reader);
                break;
            case "type_name":
                variable.TypeNameIndex = TokenIndex(ref reader);
                variable.TypeName = ReadTypeName(ref reader);
                break;
            case "params":
                variable.Parameters = ReadParameters(ref reader);
                break;
            case "description":
                ReadString(ref reader, "'description'");
                break;
            default:
                KeyError(ref reader, key, keyIndex, key == "is_type_param");
                break;
        }
    }

    private void CompleteVariable(Variable variable, HashSet<string> keys, int objectIndex)
    {
        bool hasType = keys.Contains("type");
        bool hasTypeName = keys.Contains("type_name");
        if (!keys.Contains("name"))
        {
            AddError(objectIndex, "a variable has no 'name'");
        }

        if (hasType == hasTypeName)
        {
            AddError(objectIndex, hasType ? "a variable has both 'type' and 'type_name'" : "a variable has neither 'type' nor 'type_name'");
        }

        if (keys.Contains("params") && !hasTypeName)
        {
            AddError(objectIndex, "a variable has 'params' without 'type_name'");
        }

        if (variable.TypeName is not null && (variable.Parameters is not null || !keys.Contains("params")))
        {
            variable.Type = ResolveType(TypeNameRecord(variable.TypeName, variable.Parameters ?? []), variable.TypeNameIndex, allowTypeParameters: false, "a variable");
        }

        if (variable.Name is not null && variable.Type is not null)
        {
            _variables.Add(new PendingVariable(new CelVariableDecl(variable.Name, variable.Type), objectIndex));
        }
    }

    private string? ReadVariableName(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? name = ReadString(ref reader, "'name'");
        if (name?.Length == 0)
        {
            AddError(index, "the name of a variable is empty");
            return null;
        }

        return name;
    }

    private CelType? ReadVariableType(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? text = ReadString(ref reader, "'type'");
        return text is null ? null : ResolveType(text, index, allowTypeParameters: false, "a variable");
    }

    private string? ReadTypeName(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? text = ReadString(ref reader, "'type_name'");
        if (text is not null && !TypeRecordParser.IsTypeName(text))
        {
            AddError(index, $"'{text}' is not a type name");
            return null;
        }

        return text;
    }

    // The type records of "params", or null when the value or one of its elements has an error.
    private ImmutableArray<string>? ReadParameters(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'params'", "an array");
            return null;
        }

        ImmutableArray<string>.Builder parameters = ImmutableArray.CreateBuilder<string>();
        bool valid = true;
        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            int index = TokenIndex(ref reader);
            string? text = ReadString(ref reader, "an element of 'params'");
            if (text is not null && TypeRecordParser.Parse(text) is not null)
            {
                parameters.Add(text);
            }
            else
            {
                valid = false;
                if (text is not null)
                {
                    AddError(index, $"'{text}' is not a valid type record");
                }
            }
        }

        return valid ? parameters.ToImmutable() : null;
    }

    // The record name<params> of "type_name" with "params", or the name alone without parameters.
    private static string TypeNameRecord(string typeName, ImmutableArray<string> parameters) =>
        parameters.IsEmpty ? typeName : $"{typeName}<{string.Join(", ", parameters)}>";

    // What the keys of one element of "variables" gave.
    private sealed class Variable
    {
        internal string? Name { get; set; }

        internal CelType? Type { get; set; }

        internal string? TypeName { get; set; }

        internal int TypeNameIndex { get; set; }

        internal ImmutableArray<string>? Parameters { get; set; }
    }
}
