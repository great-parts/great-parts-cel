using System;
using System.Buffers;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The format of isEmail: the "valid e-mail address" of the HTML standard, not RFC 5322. A local part of one or more ASCII letters,
// digits and the characters of atext and the dot, '@', then labels of 1 to 63 letters, digits and hyphens, not starting or ending with
// a hyphen, where a label may be all digits. No quoted strings, comments or IP literals; a unit above U+007F is refused.
internal static class EmailFormat
{
    // The characters of the local part besides letters and digits: those that the HTML standard lists and the dot. The set is made once.
    private static readonly SearchValues<char> LocalPartSymbols = SearchValues.Create(".!#$%&'*+/=?^_`{|}~-");

    internal static bool IsEmail<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int at = 0;
        while (at < text.Length && IsLocalPartChar(text[at]))
        {
            counter.Step();
            at++;
        }

        if (at == 0 || at == text.Length || text[at] != '@')
        {
            return false;
        }

        return HostnameFormat.ScanLabels(text[(at + 1)..], true, ref counter);
    }

    private static bool IsLocalPartChar(char c) => FormatChars.IsAlphanumeric(c) || LocalPartSymbols.Contains(c);
}
