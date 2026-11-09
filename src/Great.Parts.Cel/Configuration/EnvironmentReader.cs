using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Configuration;

/// <summary>
/// An error of an environment description: the byte index of its place in the JSON text and what is wrong.
/// </summary>
/// <param name="ByteIndex">The byte index of the place in the JSON text without the byte order mark.</param>
/// <param name="Detail">What is wrong, the end of the message of the error.</param>
internal readonly record struct JsonError(int ByteIndex, string Detail);

/// <summary>
/// A container that the key <c>container</c> declares, and the byte index of its value.
/// </summary>
internal readonly record struct PendingContainer(string Name, int ByteIndex);

/// <summary>
/// A variable of the array <c>variables</c>, and the byte index of the <c>{</c> of its element.
/// </summary>
internal readonly record struct PendingVariable(CelVariableDecl Declaration, int ByteIndex);

/// <summary>
/// A function of the array <c>functions</c>, the byte index of the <c>{</c> of its element and those of its overloads, in the
/// order of the overloads of the declaration.
/// </summary>
internal sealed record PendingFunction(CelFunctionDecl Declaration, int ByteIndex, ImmutableArray<int> OverloadIndexes);

/// <summary>
/// An object type of <c>greatPartsCel.objectTypes</c>, the byte index of the <c>{</c> of its element and those of its
/// fields, in the order of the fields of the description.
/// </summary>
internal sealed record PendingObjectType(CelObjectType Type, int ByteIndex, ImmutableArray<int> FieldIndexes);

/// <summary>
/// What the first phase of <see cref="CelEnvironmentJson.Apply"/> read from an environment description.
/// </summary>
/// <param name="Name">The value of the key <c>name</c>, or <see langword="null"/> when the description has no such string.</param>
/// <param name="Container">The pending call <c>UseContainer</c>, or <see langword="null"/> without one.</param>
/// <param name="Variables">The pending builder calls <c>AddVariable(name, type)</c>, in the order of the array <c>variables</c>.</param>
/// <param name="Functions">The pending builder calls <c>AddFunction(declaration)</c>, in the order of the array <c>functions</c>.</param>
/// <param name="ObjectTypes">The object types that form one provider, in the order of the array <c>objectTypes</c>.</param>
/// <param name="Errors">The errors, in the order of their places in the text; the calls are made only when there are none.</param>
internal sealed record EnvironmentDescription(
    string? Name,
    PendingContainer? Container,
    ImmutableArray<PendingVariable> Variables,
    ImmutableArray<PendingFunction> Functions,
    ImmutableArray<PendingObjectType> ObjectTypes,
    ImmutableArray<JsonError> Errors);

/// <summary>
/// Reads an environment description with <see cref="Utf8JsonReader"/> into pending builder calls and errors, without
/// touching a builder.
/// </summary>
/// <remarks>
/// Every schema error is reported, and reading goes on with the next key or element. Text that is not UTF-8 or not
/// well-formed JSON ends reading at the first malformed place. The reader checks every condition of the builder methods and
/// of the constructors that the second phase calls, so that phase cannot throw.
/// </remarks>
internal sealed partial class EnvironmentReader
{
    private readonly List<JsonError> _errors = [];
    private readonly ImmutableArray<PendingVariable>.Builder _variables = ImmutableArray.CreateBuilder<PendingVariable>();
    private readonly ImmutableArray<PendingFunction>.Builder _functions = ImmutableArray.CreateBuilder<PendingFunction>();
    private readonly ImmutableArray<PendingObjectType>.Builder _objectTypes = ImmutableArray.CreateBuilder<PendingObjectType>();
    private string? _name;

    // The keys of the description itself, and whether its key `name` is repeated: a repeated name names no source.
    private HashSet<string>? _descriptionKeys;
    private bool _nameIsRepeated;
    private PendingContainer? _container;

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
            reader._nameIsRepeated ? null : reader._name,
            reader._container,
            reader._variables.ToImmutable(),
            reader._functions.ToImmutable(),
            reader._objectTypes.ToImmutable(),
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
        _descriptionKeys = keys;
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
                case "container":
                    ReadContainer(ref reader);
                    break;
                case "extensions":
                    ReadExtensions(ref reader);
                    break;
                case "variables":
                    ReadVariables(ref reader);
                    break;
                case "functions":
                    ReadFunctions(ref reader);
                    break;
                case "greatPartsCel":
                    ReadGreatPartsCel(ref reader);
                    break;
                default:
                    KeyError(ref reader, key, keyIndex, IsUnsupportedTopLevelKey(key));
                    break;
            }
        }
    }

    // Keys of cel-go that the library does not support.
    private static bool IsUnsupportedTopLevelKey(string key) =>
        key is "imports" or "stdlib" or "context_variable" or "validators" or "features" or "limits";

    // The container: a qualified name, or the empty string, which declares nothing.
    private void ReadContainer(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? container = ReadString(ref reader, "'container'");
        if (string.IsNullOrEmpty(container))
        {
            return;
        }

        if (DeclarationNames.IsQualifiedIdentifier(container))
        {
            _container = new PendingContainer(container, index);
        }
        else
        {
            AddError(index, $"the container '{container}' is not a qualified name");
        }
    }

    // The type of a record, or null with an error at the value.
    private CelType? ResolveType(string text, int index, bool allowTypeParameters, string subject)
    {
        CelType? type = TypeRecordResolver.Resolve(text, allowTypeParameters, subject, out string detail);
        if (type is null)
        {
            AddError(index, detail);
        }

        return type;
    }

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

                _nameIsRepeated |= text == "name" && ReferenceEquals(keys, _descriptionKeys);
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
}
