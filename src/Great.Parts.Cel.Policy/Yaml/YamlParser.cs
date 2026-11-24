using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Policy.Yaml;

// The kinds of events of the parser.
internal enum YamlEventType
{
    StreamStart,
    StreamEnd,
    DocumentStart,
    DocumentEnd,
    Alias,
    Scalar,
    SequenceStart,
    SequenceEnd,
    MappingStart,
    MappingEnd,
}

// An event: the start and the end of the part of the document it stands for, and what its kind carries. The tag is the full tag
// with the handle expanded, empty for none.
internal sealed class YamlEvent(YamlEventType type, int start, int end)
{
    internal YamlEventType Type { get; } = type;

    internal int Start { get; } = start;

    internal int End { get; } = end;

    // The anchor of a node, or the name of an alias; empty for none.
    internal string Anchor { get; init; } = string.Empty;

    internal string Tag { get; init; } = string.Empty;

    internal string Value { get; init; } = string.Empty;

    internal YamlScalarStyle Style { get; init; }

    internal bool IsFlow { get; init; }

    internal YamlSegment[] Segments { get; init; } = [];
}

// The parser: the tokens of the scanner become the events of YAML, as the parser of go.yaml.in/yaml/v3 v3.0.4 (parserc.go, from
// libyaml) does it, by a state machine with a stack of states and no recursion. The first error ends the reading with a YamlException.
internal sealed partial class YamlParser
{
    private enum State
    {
        StreamStart,
        ImplicitDocumentStart,
        DocumentStart,
        DocumentContent,
        DocumentEnd,
        BlockNode,
        BlockNodeOrIndentlessSequence,
        FlowNode,
        BlockSequenceFirstEntry,
        BlockSequenceEntry,
        IndentlessSequenceEntry,
        BlockMappingFirstKey,
        BlockMappingKey,
        BlockMappingValue,
        FlowSequenceFirstEntry,
        FlowSequenceEntry,
        FlowSequenceEntryMappingKey,
        FlowSequenceEntryMappingValue,
        FlowSequenceEntryMappingEnd,
        FlowMappingFirstKey,
        FlowMappingKey,
        FlowMappingValue,
        FlowMappingEmptyValue,
        End,
    }

    // The tag directives that every document has: "!" stands for "!", "!!" for the prefix of the tags of YAML.
    private static readonly (string Handle, string Prefix)[] DefaultTagDirectives = [("!", "!"), ("!!", YamlNode.TagPrefix)];

    private readonly YamlScanner _scanner;
    private readonly List<State> _states = [];
    private readonly List<(string Handle, string Prefix)> _tagDirectives = [];
    private State _state = State.StreamStart;

    internal YamlParser(YamlScanner scanner)
    {
        _scanner = scanner;
    }

    // The next event.
    internal YamlEvent Next() => _state switch
    {
        State.StreamStart => ParseStreamStart(),
        State.ImplicitDocumentStart => ParseDocumentStart(true),
        State.DocumentStart => ParseDocumentStart(false),
        State.DocumentContent => ParseDocumentContent(),
        State.DocumentEnd => ParseDocumentEnd(),
        State.BlockNode => ParseNode(true, false),
        State.BlockNodeOrIndentlessSequence => ParseNode(true, true),
        State.FlowNode => ParseNode(false, false),
        State.BlockSequenceFirstEntry => ParseBlockSequenceEntry(true),
        State.BlockSequenceEntry => ParseBlockSequenceEntry(false),
        State.IndentlessSequenceEntry => ParseIndentlessSequenceEntry(),
        State.BlockMappingFirstKey => ParseBlockMappingKey(true),
        State.BlockMappingKey => ParseBlockMappingKey(false),
        State.BlockMappingValue => ParseBlockMappingValue(),
        State.FlowSequenceFirstEntry => ParseFlowSequenceEntry(true),
        State.FlowSequenceEntry => ParseFlowSequenceEntry(false),
        State.FlowSequenceEntryMappingKey => ParseFlowSequenceEntryMappingKey(),
        State.FlowSequenceEntryMappingValue => ParseFlowSequenceEntryMappingValue(),
        State.FlowSequenceEntryMappingEnd => ParseFlowSequenceEntryMappingEnd(),
        State.FlowMappingFirstKey => ParseFlowMappingKey(true),
        State.FlowMappingKey => ParseFlowMappingKey(false),
        State.FlowMappingValue => ParseFlowMappingValue(false),
        State.FlowMappingEmptyValue => ParseFlowMappingValue(true),
        _ => throw new InvalidOperationException("The parser has no event after the end of the stream."),
    };

    private YamlToken Peek() => _scanner.Peek();

    private void SkipToken() => _scanner.SkipToken();

    private static YamlException Error(string problem, int offset) => new(problem, offset);

    private State PopState()
    {
        State state = _states[^1];
        _states.RemoveAt(_states.Count - 1);
        return state;
    }

    private YamlEvent ParseStreamStart()
    {
        YamlToken token = Peek();
        if (token.Type != YamlTokenType.StreamStart)
        {
            throw Error("did not find expected <stream-start>", token.Start);
        }

        _state = State.ImplicitDocumentStart;
        SkipToken();
        return new YamlEvent(YamlEventType.StreamStart, token.Start, token.End);
    }

    // An implicit document has no "---"; an explicit one has directives and "---". "..." ends a document and may repeat.
    private YamlEvent ParseDocumentStart(bool implicitDocument)
    {
        YamlToken token = Peek();
        if (!implicitDocument)
        {
            while (token.Type == YamlTokenType.DocumentEnd)
            {
                SkipToken();
                token = Peek();
            }
        }

        if (implicitDocument && token.Type is not (YamlTokenType.VersionDirective or YamlTokenType.TagDirective
            or YamlTokenType.DocumentStart or YamlTokenType.StreamEnd))
        {
            ProcessDirectives();
            _states.Add(State.DocumentEnd);
            _state = State.BlockNode;
            return new YamlEvent(YamlEventType.DocumentStart, token.Start, token.End);
        }

        if (token.Type != YamlTokenType.StreamEnd)
        {
            return ParseExplicitDocumentStart(token);
        }

        _state = State.End;
        SkipToken();
        return new YamlEvent(YamlEventType.StreamEnd, token.Start, token.End);
    }

    private YamlEvent ParseExplicitDocumentStart(YamlToken first)
    {
        ProcessDirectives();
        YamlToken token = Peek();
        if (token.Type != YamlTokenType.DocumentStart)
        {
            throw Error("did not find expected <document start>", token.Start);
        }

        _states.Add(State.DocumentEnd);
        _state = State.DocumentContent;
        SkipToken();
        return new YamlEvent(YamlEventType.DocumentStart, first.Start, token.End);
    }

    private YamlEvent ParseDocumentContent()
    {
        YamlToken token = Peek();
        if (token.Type is YamlTokenType.VersionDirective or YamlTokenType.TagDirective or YamlTokenType.DocumentStart
            or YamlTokenType.DocumentEnd or YamlTokenType.StreamEnd)
        {
            _state = PopState();
            return EmptyScalar(token.Start);
        }

        return ParseNode(true, false);
    }

    private YamlEvent ParseDocumentEnd()
    {
        YamlToken token = Peek();
        int end = token.Start;
        if (token.Type == YamlTokenType.DocumentEnd)
        {
            end = token.End;
            SkipToken();
        }

        _tagDirectives.Clear();
        _state = State.DocumentStart;
        return new YamlEvent(YamlEventType.DocumentEnd, token.Start, end);
    }

    // Reads the directives before a document: one %YAML directive, which must be of version 1.1, and %TAG directives, each handle
    // once. The default handles are added after them.
    private void ProcessDirectives()
    {
        YamlToken token = Peek();
        bool hasVersion = false;
        while (token.Type is YamlTokenType.VersionDirective or YamlTokenType.TagDirective)
        {
            if (token.Type == YamlTokenType.VersionDirective)
            {
                if (hasVersion)
                {
                    throw Error("found duplicate %YAML directive", token.Start);
                }

                if (token.Major != 1 || token.Minor != 1)
                {
                    throw Error("found incompatible YAML document", token.Start);
                }

                hasVersion = true;
            }
            else
            {
                AppendTagDirective(token.Value, token.Suffix, false, token.Start);
            }

            SkipToken();
            token = Peek();
        }

        foreach ((string handle, string prefix) in DefaultTagDirectives)
        {
            AppendTagDirective(handle, prefix, true, token.Start);
        }
    }

    private void AppendTagDirective(string handle, string prefix, bool allowDuplicates, int offset)
    {
        foreach ((string known, string _) in _tagDirectives)
        {
            if (string.Equals(known, handle, StringComparison.Ordinal))
            {
                if (allowDuplicates)
                {
                    return;
                }

                throw Error("found duplicate %TAG directive", offset);
            }
        }

        _tagDirectives.Add((handle, prefix));
    }

    // An empty scalar stands for a value that the document leaves out.
    private static YamlEvent EmptyScalar(int offset) =>
        new(YamlEventType.Scalar, offset, offset) { Style = YamlScalarStyle.Plain, Segments = [new YamlSegment(0, offset)] };
}
