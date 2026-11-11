using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Great.Parts.Cel.Configuration;

// The key extensions: objects of name and version, both required. The name is looked up in the sets that the library
// implements, which the catalog gives (CelExtensions.Names); the version is a string of decimal digits that fits an unsigned
// 32-bit integer. An element whose name and version are both valid is a pending call of AddExtension.
internal sealed partial class EnvironmentReader
{
    private void ReadExtensions(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            WrongKind(ref reader, "'extensions'", "an array");
            return;
        }

        while (Advance(ref reader) != JsonTokenType.EndArray)
        {
            ReadExtension(ref reader);
        }
    }

    // The name and the version are checked independently, so both can be errors.
    private void ReadExtension(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WrongKind(ref reader, "an element of 'extensions'", "an object");
            return;
        }

        int objectIndex = TokenIndex(ref reader);
        HashSet<string> keys = new(StringComparer.Ordinal);
        string? name = null;
        uint? version = null;
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            switch (key)
            {
                case "name":
                    name = ReadExtensionName(ref reader);
                    break;
                case "version":
                    version = ReadExtensionVersion(ref reader);
                    break;
                default:
                    KeyError(ref reader, key, keyIndex, isKnown: false);
                    break;
            }
        }

        if (!keys.Contains("name"))
        {
            AddError(objectIndex, "an extension has no 'name'");
        }

        if (!keys.Contains("version"))
        {
            AddError(objectIndex, "an extension has no 'version'");
        }

        if (name is not null && version is { } number)
        {
            _extensions.Add(new PendingExtension(name, number, objectIndex));
        }
    }

    // The name when the catalog has it, otherwise null with an error at the value.
    private string? ReadExtensionName(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? name = ReadString(ref reader, "'name'");
        if (name is not null && !_extensionNames.Contains(name))
        {
            AddError(index, $"'{name}' is not the name of an extension set of the library");
            return null;
        }

        return name;
    }

    // The version when it is a string of decimal digits in the range of uint, otherwise null with an error at the value.
    private uint? ReadExtensionVersion(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? text = ReadString(ref reader, "'version'");
        if (text is null)
        {
            return null;
        }

        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint version))
        {
            AddError(index, $"the version '{text}' is not a string of decimal digits that fits an unsigned 32-bit integer");
            return null;
        }

        return version;
    }
}
