using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Generator;

// The literals and the identifiers of the created code.
internal static class Literals
{
    // A string literal: the quote, the backslash and every character that is not printable ASCII are escaped, so the text does
    // not depend on the encoding of the file.
    internal static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c is < ' ' or > '~')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    // A value of the type long: 12L, -3L.
    internal static string Long(long value) => value.ToString(CultureInfo.InvariantCulture) + "L";

    internal static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    // The name of a declaration that C# may need to escape: a keyword gets @.
    internal static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    // A dotted name, each segment as an identifier.
    internal static string QualifiedName(string name)
    {
        string[] segments = name.Split('.');
        for (int index = 0; index < segments.Length; index++)
        {
            segments[index] = Identifier(segments[index]);
        }

        return string.Join(".", segments);
    }
}
