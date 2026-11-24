using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;

namespace Great.Parts.Cel.Policy;

// The strings of a policy, the types of the nodes and the errors.
internal sealed partial class PolicyReader
{
    // What the reader puts in place of a string whose node is not a string; the policy has an error then and is not used.
    private const string ErrorText = "*error*";

    // ---- Node types ----

    private static NodeType? TypeOf(string tag) => tag switch
    {
        "!txt" => NodeType.Text,
        YamlNode.StrTag => NodeType.Str,
        YamlNode.NullTag => NodeType.Null,
        YamlNode.BoolTag => NodeType.Bool,
        YamlNode.IntTag => NodeType.Int,
        YamlNode.FloatTag => NodeType.Float,
        YamlNode.SeqTag => NodeType.Seq,
        YamlNode.MapTag => NodeType.Map,
        YamlNode.TimestampTag => NodeType.Timestamp,
        _ => null,
    };

    private static string TagOf(NodeType type) => type switch
    {
        NodeType.Text => "!txt",
        NodeType.Str => YamlNode.StrTag,
        NodeType.Null => YamlNode.NullTag,
        NodeType.Bool => YamlNode.BoolTag,
        NodeType.Int => YamlNode.IntTag,
        NodeType.Float => YamlNode.FloatTag,
        NodeType.Seq => YamlNode.SeqTag,
        NodeType.Map => YamlNode.MapTag,
        _ => YamlNode.TimestampTag,
    };

    // Checks the type of a node by its tag; the type, or null after an error: a tag that the format does not know, or a type other
    // than the wanted ones. The texts are those of cel-go.
    private NodeType? AssertType(YamlNode node, NodeType wanted, NodeType? alternative = null)
    {
        NodeType? type = TypeOf(node.Tag);
        if (type is null)
        {
            ReportNamed(node, "unsupported yaml tag type: ", node.Tag);
            return null;
        }

        if (type == wanted || type == alternative)
        {
            return type;
        }

        if (!_stopped)
        {
            string list = alternative is { } other ? TagOf(wanted) + " " + TagOf(other) : TagOf(wanted);
            Report(node, "got yaml node type " + node.Tag + ", wanted type(s) [" + list + "]", node.Tag);
        }

        return null;
    }

    // The value of a key of a mapping as a name; a key that is not a scalar has no name.
    private static string KeyText(YamlNode key) => key is YamlScalar scalar ? scalar.Value : string.Empty;

    // ---- Strings ----

    // The string of an expression, or of another field that the format reads as an expression, by the style of the node. A string that
    // is a literal or folded block, without a tag of its own, is not the value that YAML gives: it is the lines of the document from
    // the line after the key, as long as they are indented deeper than the key, with their indentation and line breaks. A string with
    // the tag "!txt" and any other string is the value that YAML gives. The characters and the places of the string are charged to
    // the budget of the reader for text, and the reader gives up at the node where the budget is passed.
    private PolicyString NewString(YamlNode key, YamlNode value)
    {
        if (_stopped)
        {
            // The reader gave up and the policy is not used: nothing is built for the string.
            return Whole(string.Empty, value, value.Start);
        }

        PolicyString result = BuildString(key, value);
        if (!_stopped && !Spend((long)result.Text.Length + result.Segments.Length))
        {
            (int offset, int length) = Place(value);
            GiveUp(offset, length, TextLimitMessage);
        }

        return result;
    }

    private PolicyString BuildString(YamlNode key, YamlNode value)
    {
        NodeType? type = AssertType(value, NodeType.Str, NodeType.Text);
        if (type is null)
        {
            return Whole(ErrorText, value, value.Start);
        }

        if (type != NodeType.Text && value is YamlScalar { Style: YamlScalarStyle.Literal or YamlScalarStyle.Folded, HasExplicitTag: false } block)
        {
            return RawBlock(key, block);
        }

        return FromValue(value);
    }

    // The string of the description of the policy: the value of the node, which is not an expression.
    private string StrictString(YamlNode value)
    {
        NodeType? type = AssertType(value, NodeType.Str, NodeType.Text);
        if (type is null)
        {
            return ErrorText;
        }

        return value is YamlScalar scalar ? scalar.Value : string.Empty;
    }

    private PolicyString FromValue(YamlNode value)
    {
        if (value is not YamlScalar scalar)
        {
            return Whole(string.Empty, value, value.Start);
        }

        ImmutableArray<CelSourceSegment>.Builder segments = ImmutableArray.CreateBuilder<CelSourceSegment>(scalar.Segments.Length);
        foreach (YamlSegment segment in scalar.Segments)
        {
            segments.Add(new CelSourceSegment(segment.ValueOffset, Locate(segment.DocumentOffset)));
        }

        return new PolicyString(scalar.Value, segments.MoveToImmutable(), value);
    }

    // A text that stands in the document from one place on.
    private PolicyString Whole(string text, YamlNode node, int offset) =>
        new(text, [new CelSourceSegment(0, Locate(offset))], node);

    // The lines of a block: from the line after the one that holds the indicator, while a line starts with as many spaces as the
    // key has columns, so deeper than the key. Lines are cut by the line feed only, so a carriage return stays in the text; the
    // text is the part of the document from the start of the first line to the end of the last one, and it is mapped to the document
    // by one segment from the start of the first line, in the column 1.
    private PolicyString RawBlock(YamlNode key, YamlScalar block)
    {
        int indent = _lines.Locate(key.Start).Column;
        int next = NextLineFeed(block.Start);
        int lineStart = next < 0 ? _text.Length : next + 1;
        int first = -1;
        int last = lineStart;
        int position = lineStart;
        while (position <= _text.Length && next >= 0)
        {
            int lineBreak = NextLineFeed(position);
            int lineEnd = lineBreak < 0 ? _text.Length : lineBreak;
            if (!StartsWithSpaces(position, lineEnd, indent))
            {
                break;
            }

            first = first < 0 ? position : first;
            last = lineEnd;
            if (lineBreak < 0)
            {
                break;
            }

            position = lineBreak + 1;
        }

        string text = first < 0 ? string.Empty : _text.Substring(first, last - first);
        return Whole(text, block, first < 0 ? lineStart : first);
    }

    // The offset of the first line feed at or after an offset, or -1.
    private int NextLineFeed(int from)
    {
        int relative = _text.AsSpan(from).IndexOf('\n');
        return relative < 0 ? -1 : from + relative;
    }

    private bool StartsWithSpaces(int start, int end, int count)
    {
        if (end - start < count)
        {
            return false;
        }

        for (int i = start; i < start + count; i++)
        {
            if (_text[i] != ' ')
            {
                return false;
            }
        }

        return true;
    }

    // ---- Errors ----

    private CelSourceLocation Locate(int offset)
    {
        (int line, int column) = _lines.Locate(offset);
        return new CelSourceLocation(_sourceName, offset, line, column);
    }

    // The place of a node in the document: the start of the node, and for a quoted scalar without a tag the character after the quote,
    // which is where the text starts. The length is that of a scalar without its quotes; a collection has no length.
    internal static (int Offset, int Length) Place(YamlNode node)
    {
        if (node is not YamlScalar scalar)
        {
            return (node.Start, 0);
        }

        bool quoted = scalar.Style is YamlScalarStyle.SingleQuoted or YamlScalarStyle.DoubleQuoted && !scalar.HasExplicitTag;
        int length = scalar.End - scalar.Start;
        return quoted ? (scalar.Start + 1, Math.Max(0, length - 2)) : (scalar.Start, length);
    }

    // An error of the structure of the policy at a node.
    private void Report(YamlNode node, string message, string? subject)
    {
        (int offset, int length) = Place(node);
        AddError(CelErrorCode.InvalidPolicy, offset, length, message, subject);
    }

    // An error whose message repeats the name of a key or a tag of the document: the message is built only when it is reported.
    private void ReportNamed(YamlNode node, string messagePrefix, string name)
    {
        if (!_stopped)
        {
            Report(node, messagePrefix + name, name);
        }
    }

    // Adds an error unless the reader gave up. The subject, the key or the tag that the message repeats, is charged to the budget for
    // text; the error that passes the budget is not added, the error of the limit is, in its place.
    private void AddError(CelErrorCode code, int offset, int length, string message, string? subject)
    {
        if (_stopped)
        {
            return;
        }

        if (!Spend(subject?.Length ?? 0))
        {
            GiveUp(offset, length, TextLimitMessage);
            return;
        }

        Append(code, offset, length, message, subject);
    }

    private void Append(CelErrorCode code, int offset, int length, string message, string? subject) =>
        _errors.Add((offset, _errors.Count, CelError.Create(code, CelErrorStage.Parse, message, Locate(offset), length, subject)));
}
