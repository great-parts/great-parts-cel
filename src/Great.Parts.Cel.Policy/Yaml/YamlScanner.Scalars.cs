using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Great.Parts.Cel.Policy.Yaml;

// The scalars: plain, quoted and block. Each scalar is built once into a value, together with the segments that tell where the
// characters of the value stand in the document: a new segment starts after every place where YAML changed the text on its way to
// the value, that is after an escape sequence, after a folded line break with the indentation after it, and at every line of a block.
internal sealed partial class YamlScanner
{
    // The escape sequences of one character after the backslash in a double-quoted scalar, and what each one stands for.
    private const string EscapeLetters = "0abt\tnvfre \"'\\N_LP";
    private static readonly string EscapeValues = "\0\a\b\t\t\n\v\f\r\u001b \"'\\" + NextLine + NoBreakSpace + LineSeparator + ParagraphSeparator;

    // The builders of the scalar that is being scanned. They are reused, because only one scalar is scanned at a time.
    private sealed class ScalarState
    {
        private readonly List<YamlSegment> _segments = [];

        internal StringBuilder Text { get; } = new();

        internal StringBuilder LeadingBreak { get; } = new();

        internal StringBuilder TrailingBreaks { get; } = new();

        internal StringBuilder Whitespaces { get; } = new();

        internal bool LeadingBlanks { get; set; }

        internal void Reset()
        {
            Text.Clear();
            LeadingBreak.Clear();
            TrailingBreaks.Clear();
            Whitespaces.Clear();
            LeadingBlanks = false;
            _segments.Clear();
        }

        // A segment at a value offset that already has one replaces it: the later place is where the character stands.
        internal void AddSegment(int valueOffset, int documentOffset)
        {
            if (_segments.Count > 0 && _segments[^1].ValueOffset == valueOffset)
            {
                _segments[^1] = new YamlSegment(valueOffset, documentOffset);
            }
            else
            {
                _segments.Add(new YamlSegment(valueOffset, documentOffset));
            }
        }

        // A segment at the end of the value maps no character; the first segment stays for an empty value.
        internal YamlSegment[] BuildSegments()
        {
            int count = _segments.Count;
            if (count > 1 && _segments[count - 1].ValueOffset >= Text.Length)
            {
                count--;
            }

            return CollectionsMarshal.AsSpan(_segments)[..count].ToArray();
        }

        internal bool LeadingBreakIsLineFeed => LeadingBreak.Length > 0 && LeadingBreak[0] == '\n';
    }

    private ScalarState ResetScalar()
    {
        _scalar.Reset();
        return _scalar;
    }

    private static YamlToken ScalarToken(ScalarState state, int start, int end, YamlScalarStyle style) =>
        new(YamlTokenType.Scalar, start, end)
        {
            Value = state.Text.ToString(),
            Style = style,
            Segments = state.BuildSegments(),
        };

    // Appends the folded text of the line breaks of a flow scalar: a single line break is a space, and the line breaks that follow
    // an empty line stay. The characters after the fold start a new segment.
    private static void FoldBreaks(ScalarState state)
    {
        if (state.LeadingBreakIsLineFeed)
        {
            if (state.TrailingBreaks.Length == 0)
            {
                state.Text.Append(' ');
            }
            else
            {
                state.Text.Append(state.TrailingBreaks);
            }
        }
        else
        {
            state.Text.Append(state.LeadingBreak);
            state.Text.Append(state.TrailingBreaks);
        }

        state.TrailingBreaks.Clear();
        state.LeadingBreak.Clear();
    }

    // ---- Plain scalars ----

    private YamlToken ScanPlainScalar()
    {
        ScalarState state = ResetScalar();
        int indent = _indent + 1;
        int start = _pos;
        int end = _pos;
        state.AddSegment(0, _pos);
        while (!IsDocumentIndicator('-') && !IsDocumentIndicator('.') && Cur != '#')
        {
            end = ScanPlainRun(state, end);
            if (!(IsBlank(Cur) || IsBreak(_pos)))
            {
                break;
            }

            ScanPlainBlanks(state, indent);
            if (_flowLevel == 0 && _column < indent)
            {
                break;
            }
        }

        if (state.LeadingBlanks)
        {
            _simpleKeyAllowed = true;
        }

        return ScalarToken(state, start, end, YamlScalarStyle.Plain);
    }

    // The characters of a plain scalar that are not blank, up to a character that ends it. The offset where the scalar ended so far
    // is returned.
    private int ScanPlainRun(ScalarState state, int end)
    {
        while (!IsBlankOrBreakOrEnd(_pos) && !EndsPlainScalar())
        {
            if (state.LeadingBlanks)
            {
                FoldBreaks(state);
                state.LeadingBlanks = false;
                state.AddSegment(state.Text.Length, _pos);
            }
            else if (state.Whitespaces.Length > 0)
            {
                state.Text.Append(state.Whitespaces);
                state.Whitespaces.Clear();
            }

            Read(state.Text);
            end = _pos;
        }

        return end;
    }

    private bool EndsPlainScalar()
    {
        char c = Cur;
        return (c == ':' && IsBlankOrBreakOrEnd(_pos + 1)) || (_flowLevel > 0 && c is ',' or '?' or '[' or ']' or '{' or '}');
    }

    private void ScanPlainBlanks(ScalarState state, int indent)
    {
        while (IsBlank(Cur) || IsBreak(_pos))
        {
            if (IsBlank(Cur))
            {
                if (state.LeadingBlanks && _column < indent && Cur == '\t')
                {
                    throw Error("found a tab character that violates indentation");
                }

                if (state.LeadingBlanks)
                {
                    Skip();
                }
                else
                {
                    Read(state.Whitespaces);
                }
            }
            else if (state.LeadingBlanks)
            {
                ReadLine(state.TrailingBreaks);
            }
            else
            {
                state.Whitespaces.Clear();
                ReadLine(state.LeadingBreak);
                state.LeadingBlanks = true;
            }
        }
    }

    // ---- Quoted scalars ----

    private YamlToken ScanFlowScalar(bool single)
    {
        int start = _pos;
        Skip();
        ScalarState state = ResetScalar();
        state.AddSegment(0, _pos);
        char quote = single ? '\'' : '"';
        while (true)
        {
            CheckInsideQuotes();
            state.LeadingBlanks = false;
            ScanQuotedRun(state, single);
            if (Cur == quote)
            {
                break;
            }

            ScanQuotedBlanks(state);
            JoinQuotedBlanks(state);
        }

        Skip();
        return ScalarToken(state, start, _pos, single ? YamlScalarStyle.SingleQuoted : YamlScalarStyle.DoubleQuoted);
    }

    private void CheckInsideQuotes()
    {
        if (IsDocumentIndicator('-') || IsDocumentIndicator('.'))
        {
            throw Error("found unexpected document indicator");
        }

        if (IsEnd(_pos))
        {
            throw Error("found unexpected end of stream");
        }
    }

    // The characters of a quoted scalar that are not blank; an escape sequence or a doubled quote starts a new segment.
    private void ScanQuotedRun(ScalarState state, bool single)
    {
        while (!IsBlankOrBreakOrEnd(_pos))
        {
            char c = Cur;
            if (single && c == '\'' && At(_pos + 1) == '\'')
            {
                state.Text.Append('\'');
                Skip();
                Skip();
                state.AddSegment(state.Text.Length, _pos);
            }
            else if (c == (single ? '\'' : '"'))
            {
                return;
            }
            else if (!single && c == '\\' && IsBreak(_pos + 1))
            {
                // An escaped line break joins the lines without a space.
                Skip();
                SkipLine();
                state.LeadingBlanks = true;
                return;
            }
            else if (!single && c == '\\')
            {
                ScanEscape(state);
            }
            else
            {
                Read(state.Text);
            }
        }
    }

    private void ScanEscape(ScalarState state)
    {
        char letter = At(_pos + 1);
        int index = EscapeLetters.AsSpan().IndexOf(letter);
        int codeLength = letter switch
        {
            'x' => 2,
            'u' => 4,
            'U' => 8,
            _ => 0,
        };
        if (index < 0 && codeLength == 0)
        {
            throw Error("found unknown escape character");
        }

        if (codeLength == 0)
        {
            state.Text.Append(EscapeValues[index]);
        }

        Skip();
        Skip();
        if (codeLength > 0)
        {
            ScanHexEscape(state.Text, codeLength);
        }

        state.AddSegment(state.Text.Length, _pos);
    }

    private void ScanHexEscape(StringBuilder text, int length)
    {
        int value = 0;
        for (int k = 0; k < length; k++)
        {
            if (!IsHex(At(_pos + k)))
            {
                throw Error("did not find expected hexdecimal number");
            }

            value = (value << 4) + HexValue(At(_pos + k));
        }

        if (value is >= 0xD800 and <= 0xDFFF or > 0x10FFFF)
        {
            throw Error("found invalid Unicode character escape code");
        }

        if (value <= 0xFFFF)
        {
            text.Append((char)value);
        }
        else
        {
            text.Append((char)(0xD800 + ((value - 0x10000) >> 10)));
            text.Append((char)(0xDC00 + ((value - 0x10000) & 0x3FF)));
        }

        for (int k = 0; k < length; k++)
        {
            Skip();
        }
    }

    // The blanks and line breaks after a run: the blanks are kept, unless a line break follows them.
    private void ScanQuotedBlanks(ScalarState state)
    {
        while (IsBlank(Cur) || IsBreak(_pos))
        {
            if (IsBlank(Cur))
            {
                if (state.LeadingBlanks)
                {
                    Skip();
                }
                else
                {
                    Read(state.Whitespaces);
                }
            }
            else if (state.LeadingBlanks)
            {
                ReadLine(state.TrailingBreaks);
            }
            else
            {
                state.Whitespaces.Clear();
                ReadLine(state.LeadingBreak);
                state.LeadingBlanks = true;
            }
        }
    }

    private void JoinQuotedBlanks(ScalarState state)
    {
        if (state.LeadingBlanks)
        {
            FoldBreaks(state);
            state.AddSegment(state.Text.Length, _pos);
        }
        else
        {
            state.Text.Append(state.Whitespaces);
            state.Whitespaces.Clear();
        }
    }

    // ---- Block scalars ----

    private YamlToken ScanBlockScalar(bool literal)
    {
        int start = _pos;
        Skip();
        (int chomping, int increment) = ScanBlockHeader();
        SkipBlockHeaderEnd();
        int end = _pos;
        int indent = increment > 0 ? (_indent >= 0 ? _indent + increment : increment) : 0;
        ScalarState state = ResetScalar();
        state.AddSegment(0, _pos);
        ScanBlockBreaks(ref indent, state.TrailingBreaks, ref end);
        bool leadingBlank = false;
        while (_column == indent && !IsEnd(_pos))
        {
            AppendBlockBreaks(state, literal, leadingBlank, IsBlank(Cur));
            leadingBlank = IsBlank(Cur);
            state.AddSegment(state.Text.Length, _pos);
            while (!IsBreakOrEnd(_pos))
            {
                Read(state.Text);
            }

            ReadLine(state.LeadingBreak);
            ScanBlockBreaks(ref indent, state.TrailingBreaks, ref end);
        }

        if (chomping != -1)
        {
            state.Text.Append(state.LeadingBreak);
        }

        if (chomping == 1)
        {
            state.Text.Append(state.TrailingBreaks);
        }

        return ScalarToken(state, start, end, literal ? YamlScalarStyle.Literal : YamlScalarStyle.Folded);
    }

    // The chomping indicator ("+" keep, "-" strip) and the indentation indicator (1 to 9), in either order.
    private (int Chomping, int Increment) ScanBlockHeader()
    {
        int chomping = 0;
        int increment = 0;
        for (int i = 0; i < 2; i++)
        {
            char c = Cur;
            if (c is '+' or '-' && chomping == 0)
            {
                chomping = c == '+' ? 1 : -1;
                Skip();
            }
            else if (IsDigit(c) && increment == 0)
            {
                if (c == '0')
                {
                    throw Error("found an indentation indicator equal to 0");
                }

                increment = c - '0';
                Skip();
            }
        }

        return (chomping, increment);
    }

    // The blanks and the comment after the indicators, and the line break.
    private void SkipBlockHeaderEnd()
    {
        while (IsBlank(Cur))
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

        if (!IsBreakOrEnd(_pos))
        {
            throw Error("did not find expected comment or line break");
        }

        SkipLine();
    }

    // Folds the line break before a line of a folded scalar: the break of a line between two lines that do not start with a blank is
    // a space; the other breaks stay.
    private static void AppendBlockBreaks(ScalarState state, bool literal, bool leadingBlank, bool trailingBlank)
    {
        if (!literal && !leadingBlank && !trailingBlank && state.LeadingBreakIsLineFeed)
        {
            if (state.TrailingBreaks.Length == 0)
            {
                state.Text.Append(' ');
            }
        }
        else
        {
            state.Text.Append(state.LeadingBreak);
        }

        state.LeadingBreak.Clear();
        state.Text.Append(state.TrailingBreaks);
        state.TrailingBreaks.Clear();
    }

    // The indentation and the empty lines before a line of a block scalar; with no indentation indicator the indentation is that of
    // the first line that is not empty.
    private void ScanBlockBreaks(ref int indent, StringBuilder breaks, ref int end)
    {
        end = _pos;
        int maxIndent = 0;
        while (true)
        {
            while ((indent == 0 || _column < indent) && Cur == ' ')
            {
                Skip();
            }

            maxIndent = Math.Max(maxIndent, _column);
            if ((indent == 0 || _column < indent) && Cur == '\t')
            {
                throw Error("found a tab character where an indentation space is expected");
            }

            if (!IsBreak(_pos))
            {
                break;
            }

            ReadLine(breaks);
            end = _pos;
        }

        if (indent == 0)
        {
            indent = Math.Max(Math.Max(maxIndent, _indent + 1), 1);
        }
    }
}
