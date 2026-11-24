namespace Great.Parts.Cel.Policy.Yaml;

// The nodes and the collections.
internal sealed partial class YamlParser
{
    // The properties of a node: where they start and end, the anchor, and the tag with its handle expanded; the token after them.
    private readonly record struct Properties(int Start, int End, string Anchor, string Tag, YamlToken Next);

    private static YamlEvent EventAt(YamlEventType type, YamlToken token) => new(type, token.Start, token.End);

    // A node: an alias, or the properties (anchor and tag, in either order) and the content, which is a scalar, a flow collection, a
    // block collection or, in a block, an indentless sequence; a node with properties and no content is an empty scalar.
    private YamlEvent ParseNode(bool block, bool indentlessSequence)
    {
        YamlToken token = Peek();
        if (token.Type == YamlTokenType.Alias)
        {
            _state = PopState();
            SkipToken();
            return new YamlEvent(YamlEventType.Alias, token.Start, token.End) { Anchor = token.Value };
        }

        Properties p = ReadProperties(token);
        token = p.Next;
        if (indentlessSequence && token.Type == YamlTokenType.BlockEntry)
        {
            _state = State.IndentlessSequenceEntry;
            return CollectionStart(YamlEventType.SequenceStart, p, token.End, false);
        }

        switch (token.Type)
        {
            case YamlTokenType.Scalar:
                _state = PopState();
                SkipToken();
                return new YamlEvent(YamlEventType.Scalar, p.Start, token.End)
                {
                    Anchor = p.Anchor,
                    Tag = p.Tag,
                    Value = token.Value,
                    Style = token.Style,
                    Segments = token.Segments,
                };
            case YamlTokenType.FlowSequenceStart:
                _state = State.FlowSequenceFirstEntry;
                return CollectionStart(YamlEventType.SequenceStart, p, token.End, true);
            case YamlTokenType.FlowMappingStart:
                _state = State.FlowMappingFirstKey;
                return CollectionStart(YamlEventType.MappingStart, p, token.End, true);
            case YamlTokenType.BlockSequenceStart when block:
                _state = State.BlockSequenceFirstEntry;
                return CollectionStart(YamlEventType.SequenceStart, p, token.End, false);
            case YamlTokenType.BlockMappingStart when block:
                _state = State.BlockMappingFirstKey;
                return CollectionStart(YamlEventType.MappingStart, p, token.End, false);
            default:
                break;
        }

        if (p.Anchor.Length > 0 || p.Tag.Length > 0)
        {
            _state = PopState();
            return new YamlEvent(YamlEventType.Scalar, p.Start, p.End)
            {
                Anchor = p.Anchor,
                Tag = p.Tag,
                Style = YamlScalarStyle.Plain,
                Segments = [new YamlSegment(0, p.End)],
            };
        }

        throw Error("did not find expected node content", token.Start);
    }

    private static YamlEvent CollectionStart(YamlEventType type, Properties p, int end, bool isFlow) =>
        new(type, p.Start, end) { Anchor = p.Anchor, Tag = p.Tag, IsFlow = isFlow };

    private Properties ReadProperties(YamlToken token)
    {
        int start = token.Start;
        int end = token.Start;
        string anchor = string.Empty;
        string handle = string.Empty;
        string suffix = string.Empty;
        bool hasTag = false;
        int tagStart = start;
        if (token.Type == YamlTokenType.Anchor)
        {
            anchor = token.Value;
            end = token.End;
            SkipToken();
            token = Peek();
            if (token.Type == YamlTokenType.Tag)
            {
                (hasTag, handle, suffix, tagStart, end) = (true, token.Value, token.Suffix, token.Start, token.End);
                SkipToken();
                token = Peek();
            }
        }
        else if (token.Type == YamlTokenType.Tag)
        {
            (hasTag, handle, suffix, tagStart, end) = (true, token.Value, token.Suffix, token.Start, token.End);
            SkipToken();
            token = Peek();
            if (token.Type == YamlTokenType.Anchor)
            {
                anchor = token.Value;
                end = token.End;
                SkipToken();
                token = Peek();
            }
        }

        string tag = hasTag ? ExpandTag(handle, suffix, tagStart) : string.Empty;
        return new Properties(start, end, anchor, tag, token);
    }

    // The tag with its handle expanded by the directive of the handle; a verbatim tag and the tag "!" have no handle.
    private string ExpandTag(string handle, string suffix, int tagStart)
    {
        if (handle.Length == 0)
        {
            return suffix;
        }

        foreach ((string known, string prefix) in _tagDirectives)
        {
            if (string.Equals(known, handle, System.StringComparison.Ordinal))
            {
                return prefix + suffix;
            }
        }

        throw Error("found undefined tag handle", tagStart);
    }

    // ---- Block sequences ----

    private YamlEvent ParseBlockSequenceEntry(bool first)
    {
        if (first)
        {
            Peek();
            SkipToken();
        }

        YamlToken token = Peek();
        if (token.Type == YamlTokenType.BlockEntry)
        {
            int mark = token.End;
            SkipToken();
            return AfterIndicator(mark, Peek().Type is YamlTokenType.BlockEntry or YamlTokenType.BlockEnd, State.BlockSequenceEntry, false);
        }

        if (token.Type == YamlTokenType.BlockEnd)
        {
            _state = PopState();
            SkipToken();
            return EventAt(YamlEventType.SequenceEnd, token);
        }

        throw Error("did not find expected '-' indicator", token.Start);
    }

    private YamlEvent ParseIndentlessSequenceEntry()
    {
        YamlToken token = Peek();
        if (token.Type == YamlTokenType.BlockEntry)
        {
            int mark = token.End;
            SkipToken();
            bool ends = Peek().Type is YamlTokenType.BlockEntry or YamlTokenType.Key or YamlTokenType.Value or YamlTokenType.BlockEnd;
            return AfterIndicator(mark, ends, State.IndentlessSequenceEntry, false);
        }

        _state = PopState();
        return new YamlEvent(YamlEventType.SequenceEnd, token.Start, token.Start);
    }

    // ---- Block mappings ----

    private YamlEvent ParseBlockMappingKey(bool first)
    {
        if (first)
        {
            Peek();
            SkipToken();
        }

        YamlToken token = Peek();
        if (token.Type == YamlTokenType.Key)
        {
            int mark = token.End;
            SkipToken();
            return AfterIndicator(mark, Peek().Type is YamlTokenType.Key or YamlTokenType.Value or YamlTokenType.BlockEnd, State.BlockMappingValue, true);
        }

        if (token.Type == YamlTokenType.BlockEnd)
        {
            _state = PopState();
            SkipToken();
            return EventAt(YamlEventType.MappingEnd, token);
        }

        throw Error("did not find expected key", token.Start);
    }

    private YamlEvent ParseBlockMappingValue()
    {
        YamlToken token = Peek();
        if (token.Type == YamlTokenType.Value)
        {
            int mark = token.End;
            SkipToken();
            return AfterIndicator(mark, Peek().Type is YamlTokenType.Key or YamlTokenType.Value or YamlTokenType.BlockEnd, State.BlockMappingKey, true);
        }

        _state = State.BlockMappingKey;
        return EmptyScalar(token.Start);
    }

    // After the indicator of an entry of a block collection: the node that follows it, which is read in the state that comes next, or an
    // empty scalar when the next token ends the entry. A node of a mapping may be an indentless sequence.
    private YamlEvent AfterIndicator(int mark, bool entryEnds, State next, bool indentlessSequence)
    {
        if (!entryEnds)
        {
            _states.Add(next);
            return ParseNode(true, indentlessSequence);
        }

        _state = next;
        return EmptyScalar(mark);
    }

    // ---- Flow sequences ----

    // An entry of a flow sequence is a node, or "?" key and value, which is a mapping of one pair.
    private YamlEvent ParseFlowSequenceEntry(bool first)
    {
        if (first)
        {
            Peek();
            SkipToken();
        }

        YamlToken token = Peek();
        if (token.Type != YamlTokenType.FlowSequenceEnd)
        {
            if (!first)
            {
                if (token.Type != YamlTokenType.FlowEntry)
                {
                    throw Error("did not find expected ',' or ']'", token.Start);
                }

                SkipToken();
                token = Peek();
            }

            if (token.Type == YamlTokenType.Key)
            {
                _state = State.FlowSequenceEntryMappingKey;
                SkipToken();
                return new YamlEvent(YamlEventType.MappingStart, token.Start, token.End) { IsFlow = true };
            }

            if (token.Type != YamlTokenType.FlowSequenceEnd)
            {
                _states.Add(State.FlowSequenceEntry);
                return ParseNode(false, false);
            }
        }

        _state = PopState();
        SkipToken();
        return EventAt(YamlEventType.SequenceEnd, token);
    }

    private YamlEvent ParseFlowSequenceEntryMappingKey()
    {
        YamlToken token = Peek();
        if (token.Type is not (YamlTokenType.Value or YamlTokenType.FlowEntry or YamlTokenType.FlowSequenceEnd))
        {
            _states.Add(State.FlowSequenceEntryMappingValue);
            return ParseNode(false, false);
        }

        int mark = token.End;
        SkipToken();
        _state = State.FlowSequenceEntryMappingValue;
        return EmptyScalar(mark);
    }

    private YamlEvent ParseFlowSequenceEntryMappingValue()
    {
        YamlToken token = Peek();
        if (token.Type == YamlTokenType.Value)
        {
            SkipToken();
            token = Peek();
            if (token.Type is not (YamlTokenType.FlowEntry or YamlTokenType.FlowSequenceEnd))
            {
                _states.Add(State.FlowSequenceEntryMappingEnd);
                return ParseNode(false, false);
            }
        }

        _state = State.FlowSequenceEntryMappingEnd;
        return EmptyScalar(token.Start);
    }

    private YamlEvent ParseFlowSequenceEntryMappingEnd()
    {
        YamlToken token = Peek();
        _state = State.FlowSequenceEntry;
        return new YamlEvent(YamlEventType.MappingEnd, token.Start, token.Start);
    }

    // ---- Flow mappings ----

    private YamlEvent ParseFlowMappingKey(bool first)
    {
        if (first)
        {
            Peek();
            SkipToken();
        }

        YamlToken token = Peek();
        if (token.Type != YamlTokenType.FlowMappingEnd)
        {
            if (!first)
            {
                if (token.Type != YamlTokenType.FlowEntry)
                {
                    throw Error("did not find expected ',' or '}'", token.Start);
                }

                SkipToken();
                token = Peek();
            }

            if (token.Type == YamlTokenType.Key)
            {
                return ParseFlowMappingExplicitKey();
            }

            if (token.Type != YamlTokenType.FlowMappingEnd)
            {
                _states.Add(State.FlowMappingEmptyValue);
                return ParseNode(false, false);
            }
        }

        _state = PopState();
        SkipToken();
        return EventAt(YamlEventType.MappingEnd, token);
    }

    private YamlEvent ParseFlowMappingExplicitKey()
    {
        SkipToken();
        YamlToken token = Peek();
        if (token.Type is not (YamlTokenType.Value or YamlTokenType.FlowEntry or YamlTokenType.FlowMappingEnd))
        {
            _states.Add(State.FlowMappingValue);
            return ParseNode(false, false);
        }

        _state = State.FlowMappingValue;
        return EmptyScalar(token.Start);
    }

    private YamlEvent ParseFlowMappingValue(bool empty)
    {
        YamlToken token = Peek();
        if (empty)
        {
            _state = State.FlowMappingKey;
            return EmptyScalar(token.Start);
        }

        if (token.Type == YamlTokenType.Value)
        {
            SkipToken();
            token = Peek();
            if (token.Type is not (YamlTokenType.FlowEntry or YamlTokenType.FlowMappingEnd))
            {
                _states.Add(State.FlowMappingKey);
                return ParseNode(false, false);
            }
        }

        _state = State.FlowMappingKey;
        return EmptyScalar(token.Start);
    }
}
