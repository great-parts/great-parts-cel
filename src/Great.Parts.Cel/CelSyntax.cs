using System;
using System.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// Helpers for an application that builds the text of an expression from values: write a string as a CEL string literal,
/// and test whether a string is a valid identifier.
/// </summary>
/// <remarks>
/// <para>
/// Inserting a value into an expression by concatenation is how expression injection happens. Prefer passing values as
/// variables in the bindings; when the text must contain the value, write it with <see cref="QuoteString(string)"/>, and
/// check a name with <see cref="IsIdentifier(string)"/> before writing it.
/// </para>
/// <para>The methods are pure and can be called from any number of threads at once.</para>
/// </remarks>
public static class CelSyntax
{
    /// <summary>
    /// Writes a string as a CEL string literal in double quotes, which parses back into the same string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>QuoteString("say \"hi\"\n")</c> is <c>"say \"hi\"\n"</c> with the escapes of the CEL language definition. The
    /// backslash and the double quote are escaped, control characters are written as <c>\n</c>, <c>\r</c>, <c>\t</c> and
    /// the other escapes of the language definition or as <c>\x</c> and two hexadecimal digits, and other characters are
    /// written as they are; a surrogate pair is one character.
    /// </para>
    /// <para>
    /// The result is a literal in any position of an expression: parsing it gives a string equal to
    /// <paramref name="value"/> by ordinal comparison. A CEL string is a sequence of Unicode code points, so a string with a
    /// lone surrogate has no literal.
    /// </para>
    /// </remarks>
    /// <param name="value">The string.</param>
    /// <returns>The CEL string literal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> has a lone surrogate, which is not a code point.</exception>
    public static string QuoteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(c))
            {
                throw new ArgumentException("The string has a lone surrogate, which is not a code point and has no literal.", nameof(value));
            }
        }

        var text = new StringBuilder(value.Length + 2);
        ValueText.AppendString(text, value);
        return text.ToString();
    }

    /// <summary>
    /// Tells whether a string is an identifier of the CEL language definition: the grammar rule <c>IDENT</c>, a Latin letter
    /// or <c>_</c> followed by Latin letters, digits and <c>_</c>, and not a reserved word such as <c>in</c>, <c>true</c>,
    /// <c>null</c> or <c>package</c>.
    /// </summary>
    /// <remarks>
    /// A qualified name such as <c>acme.sales</c> is not an identifier: test each part. An identifier is a valid name of a
    /// variable, a field in a field selection and a function, but whether it is declared is decided by the environment.
    /// </remarks>
    /// <param name="value">The string.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is an identifier and not a reserved word.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsIdentifier(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return DeclarationNames.IsIdentifier(value);
    }
}
