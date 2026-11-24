using System;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;
using Xunit;

namespace Great.Parts.Cel.Policy.Tests;

// What the tests of the YAML reader and of the policy share: reading a text that must be YAML, the place of an offset as
// "line:column", the place of an error, and the short name of a tag.
internal static class YamlTestHelpers
{
    // The place in the document of an error of a policy, which the error must have.
    internal static CelSourceLocation Location(CelError error) =>
        error.SourceLocation ?? throw new InvalidOperationException("The error has no place in the document.");

    // The line and the column of the place of an error.
    internal static (int Line, int Column) Where(CelError error)
    {
        CelSourceLocation location = Location(error);
        return (location.Line, location.Column);
    }

    // The root node of the first document of a text that is YAML.
    internal static T Root<T>(string text)
        where T : YamlNode
    {
        YamlReadResult result = YamlReader.Read(text);
        Assert.False(result.IsError, result.ErrorMessage);
        return Assert.IsType<T>(result.Root);
    }

    // The error of a text that is not YAML.
    internal static YamlReadResult Error(string text)
    {
        YamlReadResult result = YamlReader.Read(text);
        Assert.True(result.IsError, "The text was read as YAML.");
        return result;
    }

    // The place of an offset of a document as "line:column".
    internal static string Place(string text, int offset)
    {
        (int line, int column) = new LineTable(text).Locate(offset);
        return line.ToString(CultureInfo.InvariantCulture) + ":" + column.ToString(CultureInfo.InvariantCulture);
    }

    // The tag without the prefix of the tags of YAML, so "int" for the tag of an integer.
    internal static string Short(YamlNode node) =>
        node.Tag.StartsWith(YamlNode.TagPrefix, StringComparison.Ordinal) ? node.Tag[YamlNode.TagPrefix.Length..] : node.Tag;

    // The value of a scalar node.
    internal static string Value(YamlNode node) => Assert.IsType<YamlScalar>(node).Value;

    // The value of a plain scalar that is the only content of a document "a: <text>".
    internal static YamlScalar ValueOfKeyA(string text)
    {
        YamlMapping mapping = Root<YamlMapping>(text);
        return Assert.IsType<YamlScalar>(Assert.Single(mapping.Pairs).Value);
    }
}

// A generator of numbers for the tests that make their documents by chance: the same seed gives the same documents on every run and
// on every runtime. The numbers have no use outside the tests.
internal sealed class TestRandom(ulong seed)
{
    private ulong _state = seed;

    // A number from 0 up to the limit, which is not included.
    internal int Next(int limit) => (int)(NextValue() % (ulong)limit);

    // A number from the first up to the second, which is not included.
    internal int Next(int first, int limit) => first + Next(limit - first);

    private ulong NextValue()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
