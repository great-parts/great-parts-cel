using System;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Policy.Yaml;

// The tokens of the scanner, as in libyaml and go.yaml.in/yaml/v3.
internal enum YamlTokenType
{
    StreamStart,
    StreamEnd,
    VersionDirective,
    TagDirective,
    DocumentStart,
    DocumentEnd,
    BlockSequenceStart,
    BlockMappingStart,
    BlockEnd,
    FlowSequenceStart,
    FlowSequenceEnd,
    FlowMappingStart,
    FlowMappingEnd,
    BlockEntry,
    FlowEntry,
    Key,
    Value,
    Alias,
    Anchor,
    Tag,
    Scalar,
}

// A token: its kind, the offsets where it starts and ends in the document, and what the kind carries.
internal sealed class YamlToken(YamlTokenType type, int start, int end)
{
    private static readonly YamlSegment[] NoSegments = [];

    internal YamlTokenType Type { get; } = type;

    internal int Start { get; } = start;

    internal int End { get; } = end;

    // The value of a scalar, the name of an anchor or an alias, the handle of a tag or of a %TAG directive.
    internal string Value { get; init; } = string.Empty;

    // The suffix of a tag, the prefix of a %TAG directive.
    internal string Suffix { get; init; } = string.Empty;

    internal YamlScalarStyle Style { get; init; }

    // The version of a %YAML directive.
    internal int Major { get; init; }

    internal int Minor { get; init; }

    // The segments of the value of a scalar.
    internal YamlSegment[] Segments { get; init; } = NoSegments;
}

// A position of the scanner: the offset in the document, the line and the column from 0, as the library counts them while it scans.
internal readonly struct YamlMark(int index, int line, int column)
{
    internal int Index { get; } = index;

    internal int Line { get; } = line;

    internal int Column { get; } = column;
}

// The first error of a document: the problem and the offset where it was found. The reader catches it and returns it as a result.
[SuppressMessage(
    "Design",
    "CA1064:Exceptions should be public",
    Justification = "The exception never leaves the reader: YamlReader.Read catches it and returns the error in its result.")]
internal sealed class YamlException : Exception
{
    public YamlException()
        : base("The text is not YAML.")
    {
    }

    public YamlException(string message)
        : base(message)
    {
    }

    public YamlException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal YamlException(string message, int offset)
        : base(message)
    {
        Offset = offset;
    }

    internal int Offset { get; }
}
