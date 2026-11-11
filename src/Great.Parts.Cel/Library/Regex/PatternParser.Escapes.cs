namespace Great.Parts.Cel.Library.Regex;

// The backslash: anchors, \Q...\E, classes written as one escape, and the escapes for one code point.
internal ref partial struct PatternParser
{
    private void ParseBackslash()
    {
        int s = pos;
        if (s + 1 < text.Length)
        {
            char next = text[s + 1];
            NodeKind anchor = next switch
            {
                'A' => NodeKind.BeginText,
                'b' => NodeKind.WordBoundary,
                'B' => NodeKind.NoWordBoundary,
                'z' => NodeKind.EndText,
                _ => NodeKind.None,
            };
            if (anchor != NodeKind.None)
            {
                Op(anchor);
                pos += 2;
                return;
            }

            if (next == 'C')
            {
                // Any byte: not supported; a code point is not a byte.
                Fail(s, RegexError.InvalidEscape);
                return;
            }

            if (next == 'Q')
            {
                ParseQuoted();
                return;
            }

            if (next == 'p' || next == 'P')
            {
                ParseStandaloneUnicodeClass();
                return;
            }

            if (next is 'd' or 'D' or 's' or 'S' or 'w' or 'W')
            {
                ParseStandalonePerlClass(next);
                return;
            }
        }

        if (ParseEscape(s, out int after, out int codePoint))
        {
            pos = after;
            Literal(codePoint);
        }
    }

    // \Q ... \E: everything up to the first \E, or to the end, is literal.
    private void ParseQuoted()
    {
        int i = pos + 2;
        int end = text.Length;
        int resume = text.Length;
        for (int k = i; k + 1 < text.Length; k++)
        {
            if (text[k] == '\\' && text[k + 1] == 'E')
            {
                end = k;
                resume = k + 2;
                break;
            }
        }

        while (i < end)
        {
            if (!ReadRune(ref i, out int codePoint))
            {
                return;
            }

            Literal(codePoint);
            if (Failed)
            {
                return;
            }
        }

        pos = resume;
    }

    // The escape at s, which is a backslash: the code point it stands for, and where the text goes on.
    private bool ParseEscape(int s, out int after, out int codePoint)
    {
        after = s;
        codePoint = 0;
        int t = s + 1;
        if (t >= text.Length)
        {
            return Fail(s, RegexError.TrailingBackslash);
        }

        if (!ReadRune(ref t, out int c))
        {
            return false;
        }

        if (c < 0x80 && !IsAsciiAlphanumeric(c))
        {
            // An escaped punctuation character is itself.
            after = t;
            codePoint = c;
            return true;
        }

        switch (c)
        {
            case >= '1' and <= '7':
                if (t >= text.Length || text[t] < '0' || text[t] > '7')
                {
                    // A single non-zero digit is a backreference: not supported.
                    break;
                }

                return ReadOctal(c, t, out after, out codePoint);
            case '0':
                return ReadOctal(c, t, out after, out codePoint);
            case 'x':
                return ReadHex(s, t, out after, out codePoint);
        }

        int control = ControlEscape(c);
        return control >= 0 ? Escaped(control, t, out after, out codePoint) : Fail(s, RegexError.InvalidEscape);
    }

    // The escapes of C: a form feed, a line feed and the like; -1 for any other letter.
    private static int ControlEscape(int c)
    {
        return c switch
        {
            'a' => 7,
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'v' => '\v',
            _ => -1,
        };
    }

    private static bool Escaped(int value, int t, out int after, out int codePoint)
    {
        after = t;
        codePoint = value;
        return true;
    }

    // Up to three octal digits, the first of which has been read.
    private bool ReadOctal(int first, int t, out int after, out int codePoint)
    {
        int value = first - '0';
        for (int i = 1; i < 3; i++)
        {
            if (t >= text.Length || text[t] < '0' || text[t] > '7')
            {
                break;
            }

            value = (value * 8) + (text[t] - '0');
            t++;
        }

        after = t;
        codePoint = value;
        return true;
    }

    // \xHH or \x{H...}, with the t after the x.
    private bool ReadHex(int s, int t, out int after, out int codePoint)
    {
        after = s;
        codePoint = 0;
        if (t >= text.Length)
        {
            return Fail(s, RegexError.InvalidEscape);
        }

        if (!ReadRune(ref t, out int c))
        {
            return false;
        }

        if (c == '{')
        {
            return ReadBracedHex(s, t, out after, out codePoint);
        }

        int x = Unhex(c);
        int y = -1;
        if (t < text.Length)
        {
            if (!ReadRune(ref t, out c))
            {
                return false;
            }

            y = Unhex(c);
        }

        if (x < 0 || y < 0)
        {
            return Fail(s, RegexError.InvalidEscape);
        }

        after = t;
        codePoint = (x * 16) + y;
        return true;
    }

    // The digits of \x{...}: at least one, and no more than U+10FFFF.
    private bool ReadBracedHex(int s, int t, out int after, out int codePoint)
    {
        after = s;
        codePoint = 0;
        int digits = 0;
        int value = 0;
        while (true)
        {
            if (t >= text.Length)
            {
                return Fail(s, RegexError.InvalidEscape);
            }

            if (!ReadRune(ref t, out int c))
            {
                return false;
            }

            if (c == '}')
            {
                break;
            }

            int digit = Unhex(c);
            if (digit < 0)
            {
                return Fail(s, RegexError.InvalidEscape);
            }

            value = (value * 16) + digit;
            if (value > StaticTables.MaxCodePoint)
            {
                return Fail(s, RegexError.InvalidEscape);
            }

            digits++;
        }

        if (digits == 0)
        {
            return Fail(s, RegexError.InvalidEscape);
        }

        after = t;
        codePoint = value;
        return true;
    }

    private static int Unhex(int c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'a' && c <= 'f')
        {
            return c - 'a' + 10;
        }

        if (c >= 'A' && c <= 'F')
        {
            return c - 'A' + 10;
        }

        return -1;
    }
}
