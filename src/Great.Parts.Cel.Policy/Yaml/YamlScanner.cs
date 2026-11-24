using System;
using System.Collections.Generic;
using System.Text;

namespace Great.Parts.Cel.Policy.Yaml;

// The scanner: it turns the text of a document into the tokens of YAML. It is a port of the scanner of go.yaml.in/yaml/v3 v3.0.4
// (scannerc.go, from libyaml), so that a document is accepted, or rejected, as the library of cel-go accepts it: the same simple keys,
// the same indentation levels, the same limits. Comments are skipped, not kept. The scanner never recurses and does not copy the
// text: it moves one offset forward and builds each value once. The first error ends the scan with a YamlException.
//
// A simple key is a key without the "?" indicator: it must be on one line and at most 1024 characters long. The scanner learns that a
// token was a key only when it meets ":", so the token is kept in the queue until then and the KEY token is inserted before it.
internal sealed partial class YamlScanner
{
    // The limits of the library: the depth of the flow collections and of the indentation levels.
    private const int MaxFlowLevel = 10000;
    private const int MaxIndents = 10000;

    // The length of a simple key, and the distance in which a character that is not allowed in a document is found before it is read.
    private const int MaxSimpleKeyLength = 1024;
    private const int ReadAheadWindow = 512;

    // Characters that are written by number, so that the source text holds no invisible character.
    private const char ByteOrderMark = (char)0xFEFF;
    private const char NextLine = (char)0x85;
    private const char NoBreakSpace = (char)0xA0;
    private const char LastBeforeSurrogates = (char)0xD7FF;
    private const char FirstAfterSurrogates = (char)0xE000;
    private const char LastCharacter = (char)0xFFFD;
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    private readonly string _text;

    // The offset of the first character that is not allowed in a document, or int.MaxValue.
    private readonly int _invalidFrom;

    private readonly List<YamlToken> _tokens = [];
    private readonly List<int> _indents = [];
    private readonly List<SimpleKey> _simpleKeys = [];
    private readonly Dictionary<int, int> _simpleKeysByToken = [];
    private readonly ScalarState _scalar = new();

    private int _pos;
    private int _line;
    private int _column;
    private int _tokensHead;
    private int _tokensParsed;
    private bool _tokenAvailable;
    private bool _streamStartProduced;
    private int _indent;
    private bool _simpleKeyAllowed;
    private int _flowLevel;

    internal YamlScanner(string text)
    {
        _text = text;
        _invalidFrom = FindInvalidCharacter(text);

        // A byte order mark at the start of the document is not a character of it; it counts in the offsets, not in the column.
        if (text.Length > 0 && text[0] == ByteOrderMark)
        {
            _pos = 1;
        }
    }

    // The next token, scanned when it is not yet in the queue.
    internal YamlToken Peek()
    {
        if (!_tokenAvailable)
        {
            FetchMoreTokens();
        }

        return _tokens[_tokensHead];
    }

    // Removes the token that Peek returned.
    internal void SkipToken()
    {
        _tokenAvailable = false;
        _tokensParsed++;
        _tokensHead++;
        if (_tokensHead >= 1024 && _tokensHead * 2 >= _tokens.Count)
        {
            _tokens.RemoveRange(0, _tokensHead);
            _tokensHead = 0;
        }
    }

    // A position outside the allowed set: the characters of the document must be tab, line feed, carriage return, the printable
    // characters of the Unicode planes without U+FFFE and U+FFFF, and the next line character (U+0085); a surrogate must be paired.
    private static int FindInvalidCharacter(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '\t' or '\n' or '\r' or (>= ' ' and <= '~') or NextLine or (>= NoBreakSpace and <= LastBeforeSurrogates)
                or (>= FirstAfterSurrogates and <= LastCharacter))
            {
                continue;
            }

            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
                continue;
            }

            return i;
        }

        return int.MaxValue;
    }

    private YamlException Error(string problem) => new(problem, _pos);

    private YamlException InvalidCharacter() => new("control characters are not allowed", _invalidFrom);

    // The character at an offset; '\0' past the end, which is the end of the document: a real NUL is not allowed and is found first.
    private char At(int index) => (uint)index < (uint)_text.Length ? _text[index] : '\0';

    private char Cur => At(_pos);

    private bool IsEnd(int index) => index >= _text.Length;

    private static bool IsBlank(char c) => c is ' ' or '\t';

    private bool IsBreak(int index) => At(index) is '\r' or '\n' or NextLine or LineSeparator or ParagraphSeparator;

    private bool IsBreakOrEnd(int index) => IsEnd(index) || IsBreak(index);

    private bool IsBlankOrBreakOrEnd(int index) => IsEnd(index) || IsBlank(At(index)) || IsBreak(index);

    // The characters of anchors, aliases, tag handles and directive names.
    private static bool IsAlpha(char c) => c is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_' or '-';

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsHex(char c) => c is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f');

    private static int HexValue(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;

    // Moves over a character that is not a line break.
    private void Skip()
    {
        if (_pos >= _invalidFrom)
        {
            throw InvalidCharacter();
        }

        _pos++;
        _column++;
    }

    // Moves over a line break, "\r\n" being one.
    private void SkipLine()
    {
        if (At(_pos) == '\r' && At(_pos + 1) == '\n')
        {
            _pos += 2;
        }
        else if (IsBreak(_pos))
        {
            _pos++;
        }
        else
        {
            return;
        }

        _column = 0;
        _line++;
    }

    // Copies a character that is not a line break to a builder and moves over it.
    private void Read(StringBuilder builder)
    {
        builder.Append(Cur);
        Skip();
    }

    // Copies a line break to a builder and moves over it: "\r\n", "\r", "\n" and the next line character become "\n"; the line and
    // paragraph separators stay as they are.
    private void ReadLine(StringBuilder builder)
    {
        char c = At(_pos);
        if (c == '\r' && At(_pos + 1) == '\n')
        {
            builder.Append('\n');
            _pos += 2;
        }
        else if (c is '\r' or '\n' or NextLine)
        {
            builder.Append('\n');
            _pos++;
        }
        else if (c is LineSeparator or ParagraphSeparator)
        {
            builder.Append(c);
            _pos++;
        }
        else
        {
            return;
        }

        _column = 0;
        _line++;
    }

    private YamlMark Mark() => new(_pos, _line, _column);

    // ---- The queue of tokens ----

    private void InsertToken(int position, YamlToken token)
    {
        if (position < 0)
        {
            _tokens.Add(token);
        }
        else
        {
            _tokens.Insert(_tokensHead + position, token);
        }
    }

    private void FetchMoreTokens()
    {
        while (NeedMoreTokens())
        {
            FetchNextToken();
        }

        _tokenAvailable = true;
    }

    // More tokens are needed while the queue is empty, and while its first token may still turn out to be a simple key.
    private bool NeedMoreTokens()
    {
        if (_tokensHead == _tokens.Count)
        {
            return true;
        }

        return _simpleKeysByToken.TryGetValue(_tokensParsed, out int index) && IsSimpleKeyValid(_simpleKeys[index]);
    }

    private void FetchNextToken()
    {
        if (!_streamStartProduced)
        {
            FetchStreamStart();
            return;
        }

        if ((long)_pos + ReadAheadWindow > _invalidFrom)
        {
            throw InvalidCharacter();
        }

        int scanStart = _pos;
        ScanToNextToken();
        UnrollIndent(_column, scanStart);
        if (IsEnd(_pos))
        {
            FetchStreamEnd();
            return;
        }

        char c = Cur;
        if (!(TryFetchDocumentToken(c) || TryFetchIndicator(c) || TryFetchProperty(c) || TryFetchScalar(c)))
        {
            throw Error("found character that cannot start any token");
        }
    }

    // Skips whitespace, comments and line breaks up to the next token. A tab is not whitespace in the indentation of a block, nor
    // after "-", "?" or ":" of a block where a key may start.
    private void ScanToNextToken()
    {
        while (true)
        {
            while (Cur == ' ' || ((_flowLevel > 0 || !_simpleKeyAllowed) && Cur == '\t'))
            {
                Skip();
            }

            if (Cur == '#')
            {
                while (!IsBreakOrEnd(_pos))
                {
                    Skip();
                }
            }

            if (!IsBreak(_pos))
            {
                return;
            }

            SkipLine();
            if (_flowLevel == 0)
            {
                _simpleKeyAllowed = true;
            }
        }
    }

    private bool IsDocumentIndicator(char c) =>
        _column == 0 && Cur == c && At(_pos + 1) == c && At(_pos + 2) == c && IsBlankOrBreakOrEnd(_pos + 3);

    private bool TryFetchDocumentToken(char c)
    {
        if (_column != 0)
        {
            return false;
        }

        if (c == '%')
        {
            UnrollIndent(-1, _pos);
            RemoveSimpleKey();
            _simpleKeyAllowed = false;
            YamlToken directive = ScanDirective();
            InsertToken(-1, directive);
            return true;
        }

        if (IsDocumentIndicator('-'))
        {
            FetchDocumentIndicator(YamlTokenType.DocumentStart);
            return true;
        }

        if (IsDocumentIndicator('.'))
        {
            FetchDocumentIndicator(YamlTokenType.DocumentEnd);
            return true;
        }

        return false;
    }

    private bool TryFetchIndicator(char c)
    {
        switch (c)
        {
            case '[':
                FetchFlowCollectionStart(YamlTokenType.FlowSequenceStart);
                return true;
            case '{':
                FetchFlowCollectionStart(YamlTokenType.FlowMappingStart);
                return true;
            case ']':
                FetchFlowCollectionEnd(YamlTokenType.FlowSequenceEnd);
                return true;
            case '}':
                FetchFlowCollectionEnd(YamlTokenType.FlowMappingEnd);
                return true;
            case ',':
                FetchFlowEntry();
                return true;
            case '-' when IsBlankOrBreakOrEnd(_pos + 1):
                FetchBlockEntry();
                return true;
            case '?' when _flowLevel > 0 || IsBlankOrBreakOrEnd(_pos + 1):
                FetchKey();
                return true;
            case ':' when _flowLevel > 0 || IsBlankOrBreakOrEnd(_pos + 1):
                FetchValue();
                return true;
            default:
                return false;
        }
    }

    private bool TryFetchProperty(char c)
    {
        switch (c)
        {
            case '*':
                FetchAnchor(YamlTokenType.Alias);
                return true;
            case '&':
                FetchAnchor(YamlTokenType.Anchor);
                return true;
            case '!':
                FetchTag();
                return true;
            default:
                return false;
        }
    }

    // A plain scalar starts with any character but the indicators; "-", "?" and ":" start one when a character that is not blank
    // follows, "?" and ":" only outside flow collections.
    private bool CanStartPlainScalar(char c)
    {
        if (IsBlankOrBreakOrEnd(_pos))
        {
            return false;
        }

        switch (c)
        {
            case '-':
                return !IsBlank(At(_pos + 1));
            case '?' or ':':
                return _flowLevel == 0 && !IsBlankOrBreakOrEnd(_pos + 1);
            case ',' or '[' or ']' or '{' or '}' or '#' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`':
                return false;
            default:
                return true;
        }
    }

    private bool TryFetchScalar(char c)
    {
        if (c is '|' or '>' && _flowLevel == 0)
        {
            RemoveSimpleKey();
            _simpleKeyAllowed = true;
            InsertToken(-1, ScanBlockScalar(c == '|'));
            return true;
        }

        if (c is '\'' or '"')
        {
            SaveSimpleKey();
            _simpleKeyAllowed = false;
            InsertToken(-1, ScanFlowScalar(c == '\''));
            return true;
        }

        if (!CanStartPlainScalar(c))
        {
            return false;
        }

        SaveSimpleKey();
        _simpleKeyAllowed = false;
        InsertToken(-1, ScanPlainScalar());
        return true;
    }

    // ---- Simple keys ----

    // A possible simple key: where it starts, and the number of the token that is the first of the key.
    private sealed class SimpleKey
    {
        internal bool Possible { get; set; }

        internal bool Required { get; set; }

        internal int TokenNumber { get; set; }

        internal YamlMark Mark { get; set; }
    }

    // A possible simple key stops being one after a line break, after 1024 characters, and when it is required but not found.
    private bool IsSimpleKeyValid(SimpleKey key)
    {
        if (!key.Possible)
        {
            return false;
        }

        if (key.Mark.Line < _line || key.Mark.Index + MaxSimpleKeyLength < _pos)
        {
            if (key.Required)
            {
                throw Error("could not find expected ':'");
            }

            key.Possible = false;
            return false;
        }

        return true;
    }

    // A simple key may start here: it is required in a block when the column is the indentation.
    private void SaveSimpleKey()
    {
        if (!_simpleKeyAllowed)
        {
            return;
        }

        bool required = _flowLevel == 0 && _indent == _column;
        RemoveSimpleKey();
        SimpleKey key = _simpleKeys[^1];
        key.Possible = true;
        key.Required = required;
        key.TokenNumber = _tokensParsed + (_tokens.Count - _tokensHead);
        key.Mark = Mark();
        _simpleKeysByToken[key.TokenNumber] = _simpleKeys.Count - 1;
    }

    private void RemoveSimpleKey()
    {
        SimpleKey key = _simpleKeys[^1];
        if (!key.Possible)
        {
            return;
        }

        if (key.Required)
        {
            throw Error("could not find expected ':'");
        }

        key.Possible = false;
        _simpleKeysByToken.Remove(key.TokenNumber);
    }

    private void IncreaseFlowLevel()
    {
        _simpleKeys.Add(new SimpleKey { TokenNumber = _tokensParsed + (_tokens.Count - _tokensHead), Mark = Mark() });
        _flowLevel++;
        if (_flowLevel > MaxFlowLevel)
        {
            throw Error("exceeded max depth of 10000");
        }
    }

    private void DecreaseFlowLevel()
    {
        if (_flowLevel == 0)
        {
            return;
        }

        _flowLevel--;
        SimpleKey last = _simpleKeys[^1];
        _simpleKeysByToken.Remove(last.TokenNumber);
        _simpleKeys.RemoveAt(_simpleKeys.Count - 1);
    }

    // ---- Indentation ----

    // Starts a block collection when the column is deeper than the indentation: the token goes to the queue, at its place when the
    // number is not -1.
    private void RollIndent(int column, int number, YamlTokenType type, int index)
    {
        if (_flowLevel > 0 || _indent >= column)
        {
            return;
        }

        _indents.Add(_indent);
        _indent = column;
        if (_indents.Count > MaxIndents)
        {
            throw Error("exceeded max depth of 10000");
        }

        InsertToken(number > -1 ? number - _tokensParsed : -1, new YamlToken(type, index, index));
    }

    // Ends the block collections that are deeper than the column.
    private void UnrollIndent(int column, int scanStart)
    {
        if (_flowLevel > 0)
        {
            return;
        }

        int blockIndex = Math.Max(0, scanStart - 1);
        while (_indent > column)
        {
            InsertToken(-1, new YamlToken(YamlTokenType.BlockEnd, blockIndex, blockIndex));
            _indent = _indents[^1];
            _indents.RemoveAt(_indents.Count - 1);
        }
    }

    // ---- Fetching the tokens ----

    private void FetchStreamStart()
    {
        _indent = -1;
        _simpleKeys.Add(new SimpleKey());
        _simpleKeyAllowed = true;
        _streamStartProduced = true;
        InsertToken(-1, new YamlToken(YamlTokenType.StreamStart, _pos, _pos));
    }

    private void FetchStreamEnd()
    {
        if (_column != 0)
        {
            _column = 0;
            _line++;
        }

        UnrollIndent(-1, _pos);
        RemoveSimpleKey();
        _simpleKeyAllowed = false;
        InsertToken(-1, new YamlToken(YamlTokenType.StreamEnd, _pos, _pos));
    }

    private void FetchDocumentIndicator(YamlTokenType type)
    {
        UnrollIndent(-1, _pos);
        RemoveSimpleKey();
        _simpleKeyAllowed = false;
        int start = _pos;
        Skip();
        Skip();
        Skip();
        InsertToken(-1, new YamlToken(type, start, _pos));
    }

    private void FetchFlowCollectionStart(YamlTokenType type)
    {
        SaveSimpleKey();
        IncreaseFlowLevel();
        _simpleKeyAllowed = true;
        int start = _pos;
        Skip();
        InsertToken(-1, new YamlToken(type, start, _pos));
    }

    private void FetchFlowCollectionEnd(YamlTokenType type)
    {
        RemoveSimpleKey();
        DecreaseFlowLevel();
        _simpleKeyAllowed = false;
        int start = _pos;
        Skip();
        InsertToken(-1, new YamlToken(type, start, _pos));
    }

    private void FetchFlowEntry()
    {
        RemoveSimpleKey();
        _simpleKeyAllowed = true;
        int start = _pos;
        Skip();
        InsertToken(-1, new YamlToken(YamlTokenType.FlowEntry, start, _pos));
    }

    private void FetchBlockEntry()
    {
        if (_flowLevel == 0)
        {
            if (!_simpleKeyAllowed)
            {
                throw Error("block sequence entries are not allowed in this context");
            }

            RollIndent(_column, -1, YamlTokenType.BlockSequenceStart, _pos);
        }

        // In a flow collection the "-" is an error that the parser reports, because it can name the collection.
        RemoveSimpleKey();
        _simpleKeyAllowed = true;
        int start = _pos;
        Skip();
        InsertToken(-1, new YamlToken(YamlTokenType.BlockEntry, start, _pos));
    }

    private void FetchKey()
    {
        if (_flowLevel == 0)
        {
            if (!_simpleKeyAllowed)
            {
                throw Error("mapping keys are not allowed in this context");
            }

            RollIndent(_column, -1, YamlTokenType.BlockMappingStart, _pos);
        }

        RemoveSimpleKey();
        _simpleKeyAllowed = _flowLevel == 0;
        int start = _pos;
        Skip();
        InsertToken(-1, new YamlToken(YamlTokenType.Key, start, _pos));
    }

    private void FetchValue()
    {
        SimpleKey key = _simpleKeys[^1];
        if (IsSimpleKeyValid(key))
        {
            // The token before ":" was a simple key: its KEY token is inserted in front of it.
            InsertToken(key.TokenNumber - _tokensParsed, new YamlToken(YamlTokenType.Key, key.Mark.Index, key.Mark.Index));
            RollIndent(key.Mark.Column, key.TokenNumber, YamlTokenType.BlockMappingStart, key.Mark.Index);
            key.Possible = false;
            _simpleKeysByToken.Remove(key.TokenNumber);
            _simpleKeyAllowed = false;
        }
        else
        {
            // The ":" follows a complex key.
            if (_flowLevel == 0)
            {
                if (!_simpleKeyAllowed)
                {
                    throw Error("mapping values are not allowed in this context");
                }

                RollIndent(_column, -1, YamlTokenType.BlockMappingStart, _pos);
            }

            _simpleKeyAllowed = _flowLevel == 0;
        }

        int start = _pos;
        Skip();
        InsertToken(-1, new YamlToken(YamlTokenType.Value, start, _pos));
    }

    private void FetchAnchor(YamlTokenType type)
    {
        SaveSimpleKey();
        _simpleKeyAllowed = false;
        InsertToken(-1, ScanAnchor(type));
    }

    private void FetchTag()
    {
        SaveSimpleKey();
        _simpleKeyAllowed = false;
        InsertToken(-1, ScanTag());
    }
}
