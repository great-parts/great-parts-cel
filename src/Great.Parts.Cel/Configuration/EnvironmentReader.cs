using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Configuration;

/// <summary>
/// An error of an environment description: the byte index of its place in the JSON text and what is wrong.
/// </summary>
/// <param name="ByteIndex">The byte index of the place in the JSON text without the byte order mark.</param>
/// <param name="Detail">What is wrong, the end of the message of the error.</param>
internal readonly record struct JsonError(int ByteIndex, string Detail);

/// <summary>
/// What the first phase of <see cref="CelEnvironmentJson.Apply"/> read from an environment description.
/// </summary>
/// <param name="Name">The value of the key <c>name</c>, or <see langword="null"/> when the description has no such string.</param>
/// <param name="Variables">The pending builder calls <c>AddVariable(name, type)</c>, in the order of the array <c>variables</c>.</param>
/// <param name="Errors">The errors, in the order of their places in the text; the calls are made only when there are none.</param>
internal sealed record EnvironmentDescription(
    string? Name,
    ImmutableArray<CelVariableDecl> Variables,
    ImmutableArray<JsonError> Errors);

/// <summary>
/// Reads an environment description with <see cref="Utf8JsonReader"/> into pending builder calls and errors, without
/// touching a builder.
/// </summary>
/// <remarks>
/// Every schema error is reported, and reading goes on with the next key or element. Text that is not UTF-8 or not
/// well-formed JSON ends reading at the first malformed place.
/// </remarks>
internal sealed class EnvironmentReader
{
    private readonly List<JsonError> _errors = [];
    private readonly ImmutableArray<CelVariableDecl>.Builder _variables = ImmutableArray.CreateBuilder<CelVariableDecl>();
    private string? _name;

    private EnvironmentReader()
    {
    }

    /// <summary>
    /// Reads an environment description.
    /// </summary>
    /// <param name="json">The JSON text without the byte order mark.</param>
    /// <returns>What the description holds and its errors.</returns>
    internal static EnvironmentDescription Read(ReadOnlySpan<byte> json)
    {
        EnvironmentReader reader = new();
        reader.ReadText(json);
        return new EnvironmentDescription(
            reader._name,
            reader._variables.ToImmutable(),
            [.. reader._errors.OrderBy(error => error.ByteIndex)]);
    }

    private void ReadText(ReadOnlySpan<byte> json)
    {
        int invalidUtf8 = InvalidUtf8Index(json);
        if (invalidUtf8 < json.Length)
        {
            AddError(invalidUtf8, "the text is not valid UTF-8");
            return;
        }

        Utf8JsonReader reader = new(json);
        try
        {
            reader.Read();
            ReadDescription(ref reader);

            // Anything but white space after the description throws.
            reader.Read();
        }
        catch (JsonException exception)
        {
            int index = JsonPositions.ByteIndex(
                json,
                exception.LineNumber.GetValueOrDefault(),
                exception.BytePositionInLine.GetValueOrDefault());
            AddError(index, "the text is not well-formed JSON");
        }
    }

    // The index of the first byte that does not start a valid UTF-8 sequence, or the length of the text.
    private static int InvalidUtf8Index(ReadOnlySpan<byte> json)
    {
        int index = 0;
        while (Rune.DecodeFromUtf8(json[index..], out _, out int length) == OperationStatus.Done)
        {
            index += length;
        }

        return index;
    }

    private void ReadDescription(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "the description", "an object");
            return;
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            switch (key)
            {
                case "name":
                    _name = ReadString(ref reader, "'name'");
                    break;
                case "description":
                    ReadString(ref reader, "'description'");
                    break;
                case "variables":
                    ReadVariables(ref reader);
                    break;
                default:
                    KeyError(ref reader, key, keyIndex, IsSupportedLater(key) || IsUnsupportedTopLevelKey(key));
                    break;
            }
        }
    }

    // Keys of the format that the reader does not read yet.
    private static bool IsSupportedLater(string key) =>
        key is "container" or "extensions" or "functions" or "greatPartsCel";

    // Keys of cel-go that the library does not support.
    private static bool IsUnsupportedTopLevelKey(string key) =>
        key is "imports" or "stdlib" or "context_variable" or "validators" or "features" or "limits";

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
                variable.Type = ReadType(ref reader);
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
            variable.Type = TypeNameType(variable.TypeName, variable.Parameters ?? [], variable.TypeNameIndex);
        }

        if (variable.Name is not null && variable.Type is not null)
        {
            _variables.Add(new CelVariableDecl(variable.Name, variable.Type));
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

    private CelType? ReadType(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? text = ReadString(ref reader, "'type'");
        if (text is null)
        {
            return null;
        }

        TypeRecord? record = ParseRecord(text, index);
        return record is null ? null : VariableType(record.Value, text, index);
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
            if (text is not null && ParseRecord(text, index) is not null)
            {
                parameters.Add(text);
            }
            else
            {
                valid = false;
            }
        }

        return valid ? parameters.ToImmutable() : null;
    }

    // The type record of a text, or null with an error at the value when the text breaks the grammar.
    private TypeRecord? ParseRecord(string text, int index)
    {
        TypeRecord? record = TypeRecordParser.Parse(text);
        if (record is null)
        {
            AddError(index, $"'{text}' is not a valid type record");
        }

        return record;
    }

    // The CEL type of "type_name" with "params", the record name<params>, or null with an error at "type_name".
    private CelType? TypeNameType(string typeName, ImmutableArray<string> parameters, int index)
    {
        string text = parameters.IsEmpty ? typeName : $"{typeName}<{string.Join(", ", parameters)}>";
        return VariableType(new TypeRecord(typeName, IsTypeParameter: false, HasParameters: !parameters.IsEmpty), text, index);
    }

    // The CEL type of a variable of a well-formed type record, or null with an error at the value.
    private CelType? VariableType(TypeRecord record, string text, int index)
    {
        if (record.IsTypeParameter)
        {
            AddError(index, $"the type parameter '{text}' cannot be the type of a variable");
            return null;
        }

        CelType? type = SimpleType(record.Name);
        if (type is null)
        {
            AddError(index, $"the type '{text}' is not supported");
            return null;
        }

        if (record.HasParameters)
        {
            AddError(index, $"the type '{record.Name}' has no type parameters: '{text}'");
            return null;
        }

        return type;
    }

    private static CelType? SimpleType(string name) => name switch
    {
        "int" => CelType.Int,
        "uint" => CelType.UInt,
        "double" => CelType.Double,
        "bool" => CelType.Bool,
        "string" => CelType.String,
        "bytes" => CelType.Bytes,
        "dyn" => CelType.Dyn,
        _ => null,
    };

    // Moves to the value of the next key of the object that can be decoded and is not a repeat, and reports the other
    // keys on the way and skips their values.
    private bool NextKey(ref Utf8JsonReader reader, HashSet<string> keys, out string key, out int keyIndex)
    {
        while (Advance(ref reader) == JsonTokenType.PropertyName)
        {
            keyIndex = TokenIndex(ref reader);
            string? text = DecodeString(ref reader, "a key");
            reader.Read();
            if (text is not null)
            {
                if (keys.Add(text))
                {
                    key = text;
                    return true;
                }

                AddError(keyIndex, $"the key '{text}' is repeated");
            }

            reader.Skip();
        }

        key = "";
        keyIndex = 0;
        return false;
    }

    private void KeyError(ref Utf8JsonReader reader, string key, int keyIndex, bool isKnown)
    {
        AddError(keyIndex, isKnown ? $"the key '{key}' is not supported" : $"unknown key '{key}'");
        reader.Skip();
    }

    // The string at the reader, or null with an error when the value is not a string or cannot be decoded.
    private string? ReadString(ref Utf8JsonReader reader, string subject)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return DecodeString(ref reader, subject);
        }

        WrongKind(ref reader, subject, "a string");
        return null;
    }

    // The text of the key or the string at the reader, or null with an error at it when the text has an escaped lone
    // surrogate: Utf8JsonReader reads past such a string but cannot decode it.
    private string? DecodeString(ref Utf8JsonReader reader, string subject)
    {
        try
        {
            return reader.GetString();
        }
        catch (InvalidOperationException) when (reader.ValueIsEscaped)
        {
            AddError(TokenIndex(ref reader), $"{subject} has an escaped lone surrogate");
            return null;
        }
    }

    private void WrongKind(ref Utf8JsonReader reader, string subject, string kind)
    {
        AddError(TokenIndex(ref reader), $"{subject} must be {kind}");
        reader.Skip();
    }

    private void AddError(int byteIndex, string detail) => _errors.Add(new JsonError(byteIndex, detail));

    private static JsonTokenType Advance(ref Utf8JsonReader reader)
    {
        reader.Read();
        return reader.TokenType;
    }

    private static int TokenIndex(ref Utf8JsonReader reader) => (int)reader.TokenStartIndex;

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
