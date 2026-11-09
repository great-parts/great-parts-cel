using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Great.Parts.Cel.Configuration;

// The key extensions: objects of name and version, both required. The name is looked up in the catalog of the sets that the
// library implements; the version is a string of decimal digits that fits an unsigned 32-bit integer.
internal sealed partial class EnvironmentReader
{
    // The names of the sets that the library implements, as a description names them. The catalog is empty until a part brings
    // the first set; each such part adds the name of its set here and the call of AddExtension with the set of that version.
    private static readonly FrozenSet<string> ExtensionNames = FrozenSet<string>.Empty;

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
        while (NextKey(ref reader, keys, out string key, out int keyIndex))
        {
            switch (key)
            {
                case "name":
                    ReadExtensionName(ref reader);
                    break;
                case "version":
                    ReadExtensionVersion(ref reader);
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
    }

    private void ReadExtensionName(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? name = ReadString(ref reader, "'name'");
        if (name is not null && !ExtensionNames.Contains(name))
        {
            AddError(index, $"'{name}' is not the name of an extension set of the library");
        }
    }

    private void ReadExtensionVersion(ref Utf8JsonReader reader)
    {
        int index = TokenIndex(ref reader);
        string? version = ReadString(ref reader, "'version'");
        if (version is not null && !uint.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            AddError(index, $"the version '{version}' is not a string of decimal digits that fits an unsigned 32-bit integer");
        }
    }
}
