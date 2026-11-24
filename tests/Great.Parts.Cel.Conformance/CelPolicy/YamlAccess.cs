using System;
using System.Collections.Generic;
using System.IO;
using Great.Parts.Cel.Policy.Yaml;

namespace Great.Parts.Cel.Conformance.CelPolicy;

/// <summary>Reads the nodes of the YAML reader of the policy package by the key of a mapping.</summary>
internal static class YamlAccess
{
    /// <summary>Reads the first document of a text as a mapping.</summary>
    /// <param name="path">The path of the file for the message.</param>
    /// <param name="text">The text.</param>
    /// <exception cref="InvalidDataException">The text is no YAML or its root is no mapping.</exception>
    internal static YamlMapping ReadMapping(string path, string text)
    {
        YamlReadResult result = YamlReader.Read(text);
        return result.IsError
            ? throw new InvalidDataException($"'{path}' is not YAML: {result.ErrorMessage}")
            : result.Root as YamlMapping ?? throw new InvalidDataException($"'{path}' is not a mapping.");
    }

    /// <summary>The value of the first pair of a mapping with the key, or <see langword="null"/>.</summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="key">The key, a plain or quoted scalar.</param>
    internal static YamlNode? Find(YamlMapping mapping, string key)
    {
        foreach (YamlPair pair in mapping.Pairs)
        {
            if (pair.Key is YamlScalar scalar && string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>The text of a scalar, or <see langword="null"/> when the node is none or no scalar.</summary>
    /// <param name="node">The node.</param>
    internal static string? Text(YamlNode? node) => (node as YamlScalar)?.Value;

    /// <summary>The items of a sequence, or an empty list when the node is none or no sequence.</summary>
    /// <param name="node">The node.</param>
    internal static IReadOnlyList<YamlNode> Items(YamlNode? node) => (node as YamlSequence)?.Items ?? [];
}
