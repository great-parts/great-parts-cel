using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Policy.Yaml;

// What the reader found: the root node of the first document, or no node for a stream without a document, or the first error.
internal sealed class YamlReadResult
{
    internal YamlReadResult(YamlNode? root)
    {
        Root = root;
    }

    internal YamlReadResult(string errorMessage, int errorOffset)
    {
        ErrorMessage = errorMessage;
        ErrorOffset = errorOffset;
    }

    // The root node of the first document; null when the stream has no document or the text is not YAML.
    internal YamlNode? Root { get; }

    // The first error of the text, or null.
    internal string? ErrorMessage { get; }

    // The offset in the text where the error was found.
    internal int ErrorOffset { get; }

    internal bool IsError => ErrorMessage is not null;
}

// The reader of YAML: it reads the first document of a text into nodes, as yaml.Unmarshal of go.yaml.in/yaml/v3 does into a
// yaml.Node. It has no recursion over the depth of the document, makes no copy of an alias and takes time linear in the length of the
// text; any text gives nodes or one error, never an exception.
internal static class YamlReader
{
    // Reads the first document. What follows it is not read: as in the library of cel-go, an error after the first document that
    // the scanner has not reached is not an error of the text.
    internal static YamlReadResult Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var composer = new Composer(new YamlParser(new YamlScanner(text)));
            return new YamlReadResult(composer.ComposeFirstDocument());
        }
        catch (YamlException exception)
        {
            return new YamlReadResult(exception.Message, exception.Offset);
        }
    }

    // Builds the nodes from the events with an explicit stack of the open collections.
    private sealed class Composer(YamlParser parser)
    {
        private readonly Dictionary<string, YamlNode> _anchors = new(StringComparer.Ordinal);
        private readonly List<Frame> _open = [];

        internal YamlNode? ComposeFirstDocument()
        {
            parser.Next();
            if (parser.Next().Type == YamlEventType.StreamEnd)
            {
                return null;
            }

            YamlNode root = ComposeNode(parser.Next());

            // The event that ends the document: the scanner may have to read on to tell that the document is at its end.
            parser.Next();
            return root;
        }

        private YamlNode ComposeNode(YamlEvent first)
        {
            YamlEvent current = first;
            while (true)
            {
                YamlNode? complete = Handle(current);
                if (complete is not null)
                {
                    if (_open.Count == 0)
                    {
                        return complete;
                    }

                    _open[^1].Add(complete);
                }

                current = parser.Next();
            }
        }

        // Starts a collection, or finishes one, or makes a scalar or an alias; returns the node that is complete.
        private YamlNode? Handle(YamlEvent ev)
        {
            switch (ev.Type)
            {
                case YamlEventType.Scalar:
                    return Anchored(ev, MakeScalar(ev));
                case YamlEventType.Alias:
                    if (!_anchors.TryGetValue(ev.Anchor, out YamlNode? target))
                    {
                        throw new YamlException($"unknown anchor '{ev.Anchor}' referenced", ev.Start);
                    }

                    return target;
                case YamlEventType.SequenceStart:
                    _open.Add(new Frame(Anchored(ev, new YamlSequence(CollectionTag(ev, YamlNode.SeqTag), IsTagged(ev), ev.Start, ev.IsFlow))));
                    return null;
                case YamlEventType.MappingStart:
                    _open.Add(new Frame(Anchored(ev, new YamlMapping(CollectionTag(ev, YamlNode.MapTag), IsTagged(ev), ev.Start, ev.IsFlow))));
                    return null;
                default:
                    return Close(ev);
            }
        }

        private YamlNode Close(YamlEvent ev)
        {
            Frame frame = _open[^1];
            _open.RemoveAt(_open.Count - 1);
            frame.Node.End = ev.End;
            return frame.Node;
        }

        private T Anchored<T>(YamlEvent ev, T node)
            where T : YamlNode
        {
            if (ev.Anchor.Length > 0)
            {
                _anchors[ev.Anchor] = node;
            }

            return node;
        }

        // A tag of the document other than the non-specific tag "!".
        private static bool IsTagged(YamlEvent ev) => ev.Tag.Length > 0 && !string.Equals(ev.Tag, "!", StringComparison.Ordinal);

        private static string CollectionTag(YamlEvent ev, string defaultTag) => IsTagged(ev) ? ev.Tag : defaultTag;

        // The tag of a scalar: its own tag; a string for a scalar that is not plain; the merge tag for the plain "<<"; else what the
        // plain text resolves to.
        private static YamlScalar MakeScalar(YamlEvent ev)
        {
            string tag;
            if (IsTagged(ev))
            {
                tag = ev.Tag;
            }
            else if (ev.Style != YamlScalarStyle.Plain)
            {
                tag = YamlNode.StrTag;
            }
            else if (string.Equals(ev.Value, "<<", StringComparison.Ordinal))
            {
                tag = YamlNode.MergeTag;
            }
            else
            {
                tag = YamlResolver.ResolvePlain(ev.Value);
            }

            return new YamlScalar(tag, IsTagged(ev), ev.Start, ev.End, ev.Value, ev.Style, ev.Segments);
        }
    }

    // An open collection and, in a mapping, the key that waits for its value.
    private sealed class Frame(YamlNode node)
    {
        private YamlNode? _key;

        internal YamlNode Node { get; } = node;

        internal void Add(YamlNode child)
        {
            switch (Node)
            {
                case YamlSequence sequence:
                    sequence.Items.Add(child);
                    break;
                case YamlMapping when _key is null:
                    _key = child;
                    break;
                case YamlMapping mapping:
                    mapping.Pairs.Add(new YamlPair(_key!, child));
                    _key = null;
                    break;
                default:
                    break;
            }
        }
    }
}
