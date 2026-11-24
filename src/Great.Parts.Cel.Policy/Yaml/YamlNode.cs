using System.Collections.Generic;

namespace Great.Parts.Cel.Policy.Yaml;

// The kind of a node of the YAML document.
internal enum YamlNodeKind
{
    Scalar,
    Sequence,
    Mapping,
}

// The style of a scalar as it is written in the document.
internal enum YamlScalarStyle
{
    Plain,
    SingleQuoted,
    DoubleQuoted,
    Literal,
    Folded,
}

// A place where the characters of the value of a scalar start to stand in the document one after another: from the value offset on,
// the value is the document from the document offset on, up to the next segment.
internal readonly struct YamlSegment(int valueOffset, int documentOffset)
{
    // The offset in the value of the scalar, in UTF-16 code units.
    internal int ValueOffset { get; } = valueOffset;

    // The offset in the document of the character at the value offset.
    internal int DocumentOffset { get; } = documentOffset;
}

// A node of the YAML document. The tag is the long form of the tag of the node: the explicit tag with its handle expanded, or the
// tag that the style and the value of the node imply, as the library go.yaml.in/yaml/v3 gives to a yaml.Node. An alias is not a
// node of its own: the node that has the anchor is used in its place, so the same object stands at both places.
internal abstract class YamlNode(YamlNodeKind kind, string tag, bool hasExplicitTag, int start, int end)
{
    // The tag of an untagged mapping and of an untagged sequence, and the tags that an untagged scalar resolves to.
    internal const string TagPrefix = "tag:yaml.org,2002:";
    internal const string MapTag = TagPrefix + "map";
    internal const string SeqTag = TagPrefix + "seq";
    internal const string StrTag = TagPrefix + "str";
    internal const string NullTag = TagPrefix + "null";
    internal const string BoolTag = TagPrefix + "bool";
    internal const string IntTag = TagPrefix + "int";
    internal const string FloatTag = TagPrefix + "float";
    internal const string TimestampTag = TagPrefix + "timestamp";
    internal const string MergeTag = TagPrefix + "merge";

    internal YamlNodeKind Kind { get; } = kind;

    // The long tag of the node.
    internal string Tag { get; } = tag;

    // Whether the document gives the node a tag of its own, other than the non-specific tag "!".
    internal bool HasExplicitTag { get; } = hasExplicitTag;

    // The offset in the document where the node starts: its first property (anchor or tag), else its first character.
    internal int Start { get; } = start;

    // The offset in the document where the node ends; for a collection, where it was recognized as ended.
    internal int End { get; set; } = end;
}

// A scalar: its value after the rules of its style, and the segments that map the value to the document.
internal sealed class YamlScalar : YamlNode
{
    internal YamlScalar(
        string tag,
        bool hasExplicitTag,
        int start,
        int end,
        string value,
        YamlScalarStyle style,
        YamlSegment[] segments)
        : base(YamlNodeKind.Scalar, tag, hasExplicitTag, start, end)
    {
        Value = value;
        Style = style;
        Segments = segments;
    }

    internal string Value { get; }

    internal YamlScalarStyle Style { get; }

    // At least one segment, the first at the value offset 0.
    internal YamlSegment[] Segments { get; }
}

// A sequence of nodes; a flow sequence is written in square brackets.
internal sealed class YamlSequence : YamlNode
{
    internal YamlSequence(string tag, bool hasExplicitTag, int start, bool isFlow)
        : base(YamlNodeKind.Sequence, tag, hasExplicitTag, start, start)
    {
        IsFlow = isFlow;
    }

    internal bool IsFlow { get; }

    internal List<YamlNode> Items { get; } = [];
}

// A pair of a mapping.
internal readonly struct YamlPair(YamlNode key, YamlNode value)
{
    internal YamlNode Key { get; } = key;

    internal YamlNode Value { get; } = value;
}

// A mapping: the pairs in the order of the document, with the repeated keys kept.
internal sealed class YamlMapping : YamlNode
{
    internal YamlMapping(string tag, bool hasExplicitTag, int start, bool isFlow)
        : base(YamlNodeKind.Mapping, tag, hasExplicitTag, start, start)
    {
        IsFlow = isFlow;
    }

    internal bool IsFlow { get; }

    internal List<YamlPair> Pairs { get; } = [];
}
