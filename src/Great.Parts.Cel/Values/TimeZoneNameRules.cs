using System;

namespace Great.Parts.Cel.Values;

// The check of a time zone name against the grammar of IANA names, before the name reaches the platform
// (docs/design/time-zones-system.md, "Resolving a name"; docs/limits.md, "Time zones of the operating system"). A name from an
// expression is untrusted input, so only a name of this form is passed to TimeZoneInfo.
internal static class TimeZoneNameRules
{
    /// <summary>The longest name in UTF-16 code units.</summary>
    internal const int MaxLength = 64;

    /// <summary>
    /// Tells whether a name is an IANA identifier by form: 1 to 64 UTF-16 code units; parts separated by <c>/</c>, each not
    /// empty and made of Latin letters, digits, <c>_</c>, <c>-</c> and <c>+</c>; the first part is neither <c>posix</c> nor
    /// <c>right</c>, the directories of the variants of the database. A name that has the form need not be a zone.
    /// </summary>
    /// <param name="name">The name; the case counts.</param>
    /// <returns><see langword="true"/> when the name has the form of an IANA identifier.</returns>
    internal static bool IsIanaName(ReadOnlySpan<char> name)
    {
        if (name.Length is 0 or > MaxLength)
        {
            return false;
        }

        int start = 0;
        for (int i = 0; i <= name.Length; i++)
        {
            if (i < name.Length && name[i] != '/')
            {
                if (!IsNameCharacter(name[i]))
                {
                    return false;
                }

                continue;
            }

            ReadOnlySpan<char> part = name[start..i];
            if (part.IsEmpty || (start == 0 && (part.SequenceEqual("posix") || part.SequenceEqual("right"))))
            {
                return false;
            }

            start = i + 1;
        }

        return true;
    }

    private static bool IsNameCharacter(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '+';
}
