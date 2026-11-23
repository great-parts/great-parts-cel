using System;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The format of isHostname, and the labels that the domain of an e-mail address shares with it. A host name is labels of 1 to 63
// ASCII letters, digits and hyphens, not starting or ending with a hyphen, separated by dots; the last label is not all digits; the
// name has at most 253 characters, not counting one trailing dot, which is allowed. One pass from left to right, no recursion.
internal static class HostnameFormat
{
    private const int MaxLabelLength = 63;
    private const int MaxNameLength = 253;

    internal static bool IsHostname<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        ReadOnlySpan<char> name = text.Length > 0 && text[^1] == '.' ? text[..^1] : text;
        return name.Length <= MaxNameLength && ScanLabels(name, false, ref counter);
    }

    // Labels separated by dots that fill the whole text; the text has at least one label, and every label is non-empty. The last label
    // may be all digits only when the flag says so, as in the domain of an e-mail address.
    internal static bool ScanLabels<TCounter>(ReadOnlySpan<char> text, bool lastMayBeNumeric, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int position = 0;
        while (true)
        {
            if (!ScanLabel(text, ref position, out bool numeric, ref counter))
            {
                return false;
            }

            if (position == text.Length)
            {
                return lastMayBeNumeric || !numeric;
            }

            // The label ended at a dot; the next one starts after it and is empty, hence refused, when the text ends.
            position++;
        }
    }

    // One label from the position, up to a dot or the end. False when it is empty, too long, starts or ends with a hyphen, or ends at
    // a unit that is not a dot.
    private static bool ScanLabel<TCounter>(ReadOnlySpan<char> text, ref int position, out bool numeric, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int start = position;
        numeric = true;
        while (position < text.Length)
        {
            counter.Step();
            char c = text[position];
            if (c == '-' || FormatChars.IsLetter(c))
            {
                numeric = false;
            }
            else if (!FormatChars.IsDigit(c))
            {
                break;
            }

            position++;
        }

        int length = position - start;
        if (length == 0 || length > MaxLabelLength || text[start] == '-' || text[position - 1] == '-')
        {
            return false;
        }

        return position == text.Length || text[position] == '.';
    }
}
