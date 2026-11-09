using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Configuration;

// The key functions: objects of name, description and overloads, each overload with id, target, args, return and examples.
// A function is declared without implementations; the application registers them in code by the identifier of the overload.
internal sealed partial class EnvironmentReader
{
    private void ReadFunctions(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'functions'", "an array");
            return;
        }

        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            ReadFunction(ref reader);
        }
    }

    private void ReadFunction(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "an element of 'functions'", "an object");
            return;
        }

        int objectIndex = TokenIndex(ref reader);
        string? name = null;
        PendingOverloads? overloads = null;
        HashSet<string> keys = new(StringComparer.Ordinal);
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            switch (key)
            {
                case "name":
                    name = ReadFunctionName(ref reader);
                    break;
                case "description":
                    ReadString(ref reader, "'description'");
                    break;
                case "overloads":
                    overloads = ReadOverloads(ref reader);
                    break;
                default:
                    KeyError(ref reader, key, keyIndex, isKnown: false);
                    break;
            }
        }

        if (!keys.Contains("name"))
        {
            AddError(objectIndex, "a function has no 'name'");
        }

        if (!keys.Contains("overloads"))
        {
            AddError(objectIndex, "a function has no 'overloads'");
        }
        else if (overloads is { Elements: 0 })
        {
            AddError(objectIndex, "a function has no overloads");
        }

        if (name is not null && overloads is { Elements: > 0, IsValid: true })
        {
            CelFunctionDecl function = new(name, [.. overloads.Declarations]);
            _functions.Add(new PendingFunction(function, objectIndex, overloads.Indexes.ToImmutable()));
        }
    }

    private string? ReadFunctionName(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? name = ReadString(ref reader, "'name'");
        if (name?.Length == 0)
        {
            AddError(index, "the name of a function is empty");
            return null;
        }

        return name;
    }

    // The overloads of "overloads", or null when the value is not an array.
    private PendingOverloads? ReadOverloads(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'overloads'", "an array");
            return null;
        }

        PendingOverloads overloads = new();
        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            overloads.Elements++;
            int objectIndex = TokenIndex(ref reader);
            if (ReadOverload(ref reader) is { } overload)
            {
                overloads.Declarations.Add(overload);
                overloads.Indexes.Add(objectIndex);
            }
            else
            {
                overloads.IsValid = false;
            }
        }

        return overloads;
    }

    // One element of "overloads", or null when it has an error.
    private CelOverloadDecl? ReadOverload(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "an element of 'overloads'", "an object");
            return null;
        }

        int objectIndex = TokenIndex(ref reader);
        OverloadParts parts = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            ReadOverloadValue(ref reader, parts, key, keyIndex);
        }

        if (!keys.Contains("id"))
        {
            AddError(objectIndex, "an overload has no 'id'");
        }

        if (!keys.Contains("return"))
        {
            AddError(objectIndex, "an overload has no 'return'");
        }

        bool valid = parts.Id is not null
            && parts.Result is not null
            && (!keys.Contains("target") || parts.Target is not null)
            && (!keys.Contains("args") || parts.Arguments is not null);
        return valid ? parts.ToDeclaration() : null;
    }

    private void ReadOverloadValue(ref Utf8JsonReader reader, OverloadParts parts, string key, int keyIndex)
    {
        switch (key)
        {
            case "id":
                parts.Id = ReadOverloadId(ref reader);
                break;
            case "target":
                parts.Target = ReadOverloadType(ref reader, "'target'");
                break;
            case "args":
                parts.Arguments = ReadArguments(ref reader);
                break;
            case "return":
                parts.Result = ReadOverloadType(ref reader, "'return'");
                break;
            case "examples":
                ReadExamples(ref reader);
                break;
            default:
                KeyError(ref reader, key, keyIndex, isKnown: false);
                break;
        }
    }

    private string? ReadOverloadId(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? id = ReadString(ref reader, "'id'");
        if (id?.Length == 0)
        {
            AddError(index, "the id of an overload is empty");
            return null;
        }

        return id;
    }

    // A type of an overload: a record that may use type parameters.
    private CelType? ReadOverloadType(ref Utf8JsonReader reader, string subject)
    {
        int index = TokenIndex(ref reader);
        string? text = ReadString(ref reader, subject);
        return text is null ? null : ResolveType(text, index, allowTypeParameters: true, "an overload");
    }

    // The types of "args", or null when the value or one of its elements has an error.
    private ImmutableArray<CelType>? ReadArguments(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'args'", "an array");
            return null;
        }

        ImmutableArray<CelType>.Builder arguments = ImmutableArray.CreateBuilder<CelType>();
        bool valid = true;
        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            int index = TokenIndex(ref reader);
            string? text = ReadString(ref reader, "an element of 'args'");
            if (text is not null && ResolveType(text, index, allowTypeParameters: true, "an overload") is { } type)
            {
                arguments.Add(type);
            }
            else
            {
                valid = false;
            }
        }

        return valid ? arguments.ToImmutable() : null;
    }

    // The examples are metadata: the kind of the value is checked, and the strings are not read.
    private void ReadExamples(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'examples'", "an array");
            return;
        }

        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            ReadString(ref reader, "an element of 'examples'");
        }
    }

    // What the keys of one element of "overloads" gave.
    private sealed class OverloadParts
    {
        internal string? Id { get; set; }

        internal CelType? Target { get; set; }

        internal ImmutableArray<CelType>? Arguments { get; set; }

        internal CelType? Result { get; set; }

        // The overload of a description that has an id and a result; a target makes it a receiver call.
        internal CelOverloadDecl ToDeclaration()
        {
            ReadOnlySpan<CelType> arguments = Arguments is { } listed ? listed.AsSpan() : default;
            return Target is { } target
                ? CelOverloadDecl.Receiver(Id!, target, arguments, Result!)
                : CelOverloadDecl.Global(Id!, arguments, Result!);
        }
    }

    // The valid overloads of one function, the byte indexes of their elements, and the number of elements read.
    private sealed class PendingOverloads
    {
        internal List<CelOverloadDecl> Declarations { get; } = [];

        internal ImmutableArray<int>.Builder Indexes { get; } = ImmutableArray.CreateBuilder<int>();

        internal int Elements { get; set; }

        internal bool IsValid { get; set; } = true;
    }
}
