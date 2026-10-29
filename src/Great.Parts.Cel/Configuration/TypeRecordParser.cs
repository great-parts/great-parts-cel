using System;
using System.Text;

namespace Great.Parts.Cel.Configuration;

/// <summary>
/// The outermost type of a well-formed type record: a type name, with or without type parameters in angle brackets,
/// or a type parameter.
/// </summary>
/// <param name="Name">The type name without white space, such as <c>map</c>, or the letter of a type parameter.</param>
/// <param name="IsTypeParameter">Whether the record is a type parameter, such as <c>~T</c>.</param>
/// <param name="HasParameters">Whether the name is followed by type parameters in angle brackets.</param>
internal readonly record struct TypeRecord(string Name, bool IsTypeParameter, bool HasParameters);

/// <summary>
/// The grammar of type records of the environment description, the one of cel-go:
/// <c>type = "~" letter | name [ "&lt;" type { "," type } "&gt;" ]</c>,
/// <c>name = [ "." ] ident { "." ident }</c>, <c>ident = ( letter | "_" ) { letter | digit | "_" }</c>,
/// with letters and digits of ASCII and white space — space, tab, <c>"\n"</c> and <c>"\r"</c> — between the lexemes.
/// </summary>
/// <remarks>
/// The parser keeps a count of open angle brackets instead of recursion, so the nesting of a record has no limit
/// other than the length of the text.
/// </remarks>
internal static class TypeRecordParser
{
    /// <summary>
    /// Parses a type record in full.
    /// </summary>
    /// <param name="text">The text of the record, such as <c>map&lt;string, int&gt;</c>.</param>
    /// <returns>The outermost type of the record, or <see langword="null"/> when the text breaks the grammar.</returns>
    internal static TypeRecord? Parse(string text)
    {
        ReadOnlySpan<char> span = text;
        int position = 0;
        int openLists = 0;
        TypeRecord? outermost = null;
        do
        {
            if (!TryParseType(span, ref position, out TypeRecord type))
            {
                return null;
            }

            outermost ??= type;
            if (type.HasParameters)
            {
                openLists++;
            }
            else if (!TryEndType(span, ref position, ref openLists))
            {
                return null;
            }
        }
        while (openLists > 0);

        SkipWhiteSpace(span, ref position);
        return position == span.Length ? outermost : null;
    }

    /// <summary>
    /// Tells whether a text is a type name, <c>[ "." ] ident { "." ident }</c>, without white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the whole text is a type name.</returns>
    internal static bool IsTypeName(string text)
    {
        ReadOnlySpan<char> span = text;
        int position = 0;
        return span.IndexOfAny(" \t\n\r") < 0 && TryParseName(span, ref position, out _) && position == span.Length;
    }

    // One type up to its type parameters: a type parameter, or a name and, if it has parameters, the "<" after it.
    private static bool TryParseType(ReadOnlySpan<char> text, ref int position, out TypeRecord type)
    {
        if (TryTake(text, ref position, '~'))
        {
            SkipWhiteSpace(text, ref position);
            if (position == text.Length || !char.IsAsciiLetter(text[position]))
            {
                type = default;
                return false;
            }

            type = new TypeRecord(text.Slice(position, 1).ToString(), IsTypeParameter: true, HasParameters: false);
            position++;
            return true;
        }

        if (!TryParseName(text, ref position, out string name))
        {
            type = default;
            return false;
        }

        type = new TypeRecord(name, IsTypeParameter: false, HasParameters: TryTake(text, ref position, '<'));
        return true;
    }

    // After a type without parameters: takes the ">" of each list the type ends, up to the "," before the next type
    // of a list that stays open. False when neither comes where one must.
    private static bool TryEndType(ReadOnlySpan<char> text, ref int position, ref int openLists)
    {
        while (openLists > 0 && !TryTake(text, ref position, ','))
        {
            if (!TryTake(text, ref position, '>'))
            {
                return false;
            }

            openLists--;
        }

        return true;
    }

    private static bool TryParseName(ReadOnlySpan<char> text, ref int position, out string name)
    {
        StringBuilder builder = new();
        bool dot = TryTake(text, ref position, '.');
        do
        {
            if (dot)
            {
                builder.Append('.');
            }

            SkipWhiteSpace(text, ref position);
            int start = position;
            while (position < text.Length && IsIdentifierCharacter(text[position], position == start))
            {
                position++;
            }

            if (position == start)
            {
                name = "";
                return false;
            }

            builder.Append(text[start..position]);
            dot = TryTake(text, ref position, '.');
        }
        while (dot);

        name = builder.ToString();
        return true;
    }

    private static bool IsIdentifierCharacter(char c, bool first) =>
        char.IsAsciiLetter(c) || c == '_' || (!first && char.IsAsciiDigit(c));

    // Skips white space and takes the character when it comes next.
    private static bool TryTake(ReadOnlySpan<char> text, ref int position, char c)
    {
        SkipWhiteSpace(text, ref position);
        if (position < text.Length && text[position] == c)
        {
            position++;
            return true;
        }

        return false;
    }

    private static void SkipWhiteSpace(ReadOnlySpan<char> text, ref int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t' or '\n' or '\r')
        {
            position++;
        }
    }
}
