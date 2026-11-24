using System;
using System.Text;

namespace Great.Parts.Cel.Policy.Yaml;

// Anchors, aliases, tags and directives.
internal sealed partial class YamlScanner
{
    // The characters of a tag URI besides the letters, the digits, "_" and "-".
    private const string UriPunctuation = ";/?:@&=+$,.!~*'()[]%";

    // The characters that may follow an anchor or an alias besides a blank: the indicators that cannot be part of its name.
    private const string AfterAnchor = "?:,]}%@`";

    // ---- Anchors and aliases ----

    private YamlToken ScanAnchor(YamlTokenType type)
    {
        int start = _pos;
        Skip();
        var name = new StringBuilder();
        while (IsAlpha(Cur))
        {
            Read(name);
        }

        if (name.Length == 0 || !(IsBlankOrBreakOrEnd(_pos) || AfterAnchor.AsSpan().Contains(Cur)))
        {
            throw Error("did not find expected alphabetic or numeric character");
        }

        return new YamlToken(type, start, _pos) { Value = name.ToString() };
    }

    // ---- Tags ----

    // The forms of a tag are "!<uri>" (a verbatim tag), "!handle!suffix", "!suffix" and "!". The token has the handle and the suffix;
    // the handle is empty for a verbatim tag and for "!", whose suffix is "!".
    private YamlToken ScanTag()
    {
        int start = _pos;
        string handle;
        string suffix;
        if (At(_pos + 1) == '<')
        {
            Skip();
            Skip();
            handle = string.Empty;
            suffix = ScanTagUri(string.Empty);
            if (Cur != '>')
            {
                throw Error("did not find the expected '>'");
            }

            Skip();
        }
        else
        {
            handle = ScanTagHandle(false);
            if (handle.Length > 1 && handle[0] == '!' && handle[^1] == '!')
            {
                suffix = ScanTagUri(string.Empty);
            }
            else
            {
                // It was not a handle after all: the rest of the tag follows the characters read.
                suffix = ScanTagUri(handle);
                handle = "!";
                if (suffix.Length == 0)
                {
                    handle = string.Empty;
                    suffix = "!";
                }
            }
        }

        if (!IsBlankOrBreakOrEnd(_pos))
        {
            throw Error("did not find expected whitespace or line break");
        }

        return new YamlToken(YamlTokenType.Tag, start, _pos) { Value = handle, Suffix = suffix };
    }

    private string ScanTagHandle(bool directive)
    {
        if (Cur != '!')
        {
            throw Error("did not find expected '!'");
        }

        var handle = new StringBuilder();
        Read(handle);
        while (IsAlpha(Cur))
        {
            Read(handle);
        }

        if (Cur == '!')
        {
            Read(handle);
        }
        else if (directive && handle.Length != 1)
        {
            // In a %TAG directive the handle is "!" or ends with "!".
            throw Error("did not find expected '!'");
        }

        return handle.ToString();
    }

    // The URI of a tag, after the head that was read already: the head without its first character is part of the URI.
    private string ScanTagUri(string head)
    {
        var uri = new StringBuilder();
        bool hasTag = head.Length > 0;
        if (head.Length > 1)
        {
            uri.Append(head, 1, head.Length - 1);
        }

        while (IsAlpha(Cur) || UriPunctuation.AsSpan().Contains(Cur))
        {
            if (Cur == '%')
            {
                ScanUriEscapes(uri);
            }
            else
            {
                Read(uri);
            }

            hasTag = true;
        }

        if (!hasTag)
        {
            throw Error("did not find expected tag URI");
        }

        return uri.ToString();
    }

    // The escapes %XX of one character: the first octet tells how many octets the UTF-8 sequence has.
    private void ScanUriEscapes(StringBuilder uri)
    {
        Span<byte> octets = stackalloc byte[4];
        int count = 0;
        int width = 1024;
        while (width > 0)
        {
            if (!(Cur == '%' && IsHex(At(_pos + 1)) && IsHex(At(_pos + 2))))
            {
                throw Error("did not find URI escaped octet");
            }

            byte octet = (byte)((HexValue(At(_pos + 1)) << 4) + HexValue(At(_pos + 2)));
            if (width == 1024)
            {
                width = OctetWidth(octet);
                if (width == 0)
                {
                    throw Error("found an incorrect leading UTF-8 octet");
                }
            }
            else if ((octet & 0xC0) != 0x80)
            {
                throw Error("found an incorrect trailing UTF-8 octet");
            }

            octets[count++] = octet;
            Skip();
            Skip();
            Skip();
            width--;
        }

        uri.Append(Encoding.UTF8.GetString(octets[..count]));
    }

    private static int OctetWidth(byte octet) =>
        (octet & 0x80) == 0 ? 1 : (octet & 0xE0) == 0xC0 ? 2 : (octet & 0xF0) == 0xE0 ? 3 : (octet & 0xF8) == 0xF0 ? 4 : 0;

    // ---- Directives ----

    private YamlToken ScanDirective()
    {
        int start = _pos;
        Skip();
        string name = ScanDirectiveName();
        YamlToken token = name switch
        {
            "YAML" => ScanVersionDirective(start),
            "TAG" => ScanTagDirective(start),
            _ => throw Error("found unknown directive name"),
        };

        SkipBlanks();
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
        return token;
    }

    private void SkipBlanks()
    {
        while (IsBlank(Cur))
        {
            Skip();
        }
    }

    private string ScanDirectiveName()
    {
        var name = new StringBuilder();
        while (IsAlpha(Cur))
        {
            Read(name);
        }

        if (name.Length == 0)
        {
            throw Error("could not find expected directive name");
        }

        if (!IsBlankOrBreakOrEnd(_pos))
        {
            throw Error("found unexpected non-alphabetical character");
        }

        return name.ToString();
    }

    private YamlToken ScanVersionDirective(int start)
    {
        SkipBlanks();
        int major = ScanVersionNumber();
        if (Cur != '.')
        {
            throw Error("did not find expected digit or '.' character");
        }

        Skip();
        int minor = ScanVersionNumber();
        return new YamlToken(YamlTokenType.VersionDirective, start, _pos) { Major = major, Minor = minor };
    }

    // A version number has one or two digits.
    private int ScanVersionNumber()
    {
        int value = 0;
        int length = 0;
        while (IsDigit(Cur))
        {
            length++;
            if (length > 2)
            {
                throw Error("found extremely long version number");
            }

            value = (value * 10) + (Cur - '0');
            Skip();
        }

        if (length == 0)
        {
            throw Error("did not find expected version number");
        }

        return value;
    }

    private YamlToken ScanTagDirective(int start)
    {
        SkipBlanks();
        string handle = ScanTagHandle(true);
        if (!IsBlank(Cur))
        {
            throw Error("did not find expected whitespace");
        }

        SkipBlanks();
        string prefix = ScanTagUri(string.Empty);
        if (!IsBlankOrBreakOrEnd(_pos))
        {
            throw Error("did not find expected whitespace or line break");
        }

        return new YamlToken(YamlTokenType.TagDirective, start, _pos) { Value = handle, Suffix = prefix };
    }
}
