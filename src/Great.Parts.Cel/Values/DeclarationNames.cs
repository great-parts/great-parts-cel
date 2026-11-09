using System;
using System.Collections.Frozen;

namespace Great.Parts.Cel.Values;

// The names that declarations may have: an identifier or a qualified name by the grammar of the language
// definition — segments separated by ".", the first an IDENT, the others SELECTORs. A SELECTOR is a word
// that is not a keyword; an IDENT is a SELECTOR that is not a reserved word either.
internal static class DeclarationNames
{
    private static readonly FrozenSet<string> Keywords = FrozenSet.Create(StringComparer.Ordinal, "true", "false", "null", "in");

    private static readonly FrozenSet<string> ReservedWords = FrozenSet.Create(
        StringComparer.Ordinal,
        "as", "break", "const", "continue", "else", "for", "function", "if", "import", "let", "loop", "package",
        "namespace", "return", "var", "void", "while");

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> KeywordLookup =
        Keywords.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ReservedLookup =
        ReservedWords.GetAlternateLookup<ReadOnlySpan<char>>();

    internal static bool IsQualifiedIdentifier(string name)
    {
        ReadOnlySpan<char> rest = name;
        bool first = true;
        while (true)
        {
            int dot = rest.IndexOf('.');
            ReadOnlySpan<char> segment = dot < 0 ? rest : rest[..dot];
            if (!IsWord(segment) || KeywordLookup.Contains(segment) || (first && ReservedLookup.Contains(segment)))
            {
                return false;
            }

            if (dot < 0)
            {
                return true;
            }

            rest = rest[(dot + 1)..];
            first = false;
        }
    }

    // The name of a field of an object type: a word, a keyword and a reserved word included, which an expression
    // writes in backquotes; with dots, the full name of a protobuf extension field.
    internal static bool IsFieldName(string name)
    {
        ReadOnlySpan<char> rest = name;
        while (true)
        {
            int dot = rest.IndexOf('.');
            if (!IsWord(dot < 0 ? rest : rest[..dot]))
            {
                return false;
            }

            if (dot < 0)
            {
                return true;
            }

            rest = rest[(dot + 1)..];
        }
    }

    // The name of a field that a builder adds: one word, a keyword and a reserved word included.
    internal static bool IsWordName(string name) => IsWord(name);

    // [_a-zA-Z][_a-zA-Z0-9]*
    private static bool IsWord(ReadOnlySpan<char> segment)
    {
        if (segment.IsEmpty || char.IsAsciiDigit(segment[0]))
        {
            return false;
        }

        foreach (char c in segment)
        {
            if (c != '_' && !char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
