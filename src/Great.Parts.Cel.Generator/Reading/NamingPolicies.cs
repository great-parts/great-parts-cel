using System.Globalization;
using System.Text;

namespace Great.Parts.Cel.Generator;

// The naming policies of System.Text.Json (JsonNamingPolicy.CamelCase and JsonNamingPolicy.SnakeCaseLower) written again: the
// generator cannot reference System.Text.Json of its own runtime, so a test compares both with the policies over a corpus of
// names.
internal static class NamingPolicies
{
    // The numeric values of CelNamingPolicy.
    private const int AsIs = 1;

    private const int SnakeCaseLower = 2;

    internal static string Apply(int policy, string name) => policy switch
    {
        AsIs => name,
        SnakeCaseLower => ToSnakeCaseLower(name),
        _ => ToCamelCase(name),
    };

    // A name whose first character is not upper case stays. Otherwise the leading run of upper case characters is lowered,
    // except that, from the second character on, the run stops before an upper case character that is followed by a lower case
    // one: URLPath is urlPath, XMLHttp is xmlHttp, ID is id, Id is id.
    internal static string ToCamelCase(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return name;
        }

        char[] chars = name.ToCharArray();
        for (int index = 0; index < chars.Length; index++)
        {
            if (index == 1 && !char.IsUpper(chars[index]))
            {
                break;
            }

            bool hasNext = index + 1 < chars.Length;
            if (index > 0 && hasNext && !char.IsUpper(chars[index + 1]))
            {
                if (chars[index + 1] == ' ')
                {
                    chars[index] = Lower(chars[index]);
                }

                break;
            }

            chars[index] = Lower(chars[index]);
        }

        return new string(chars);
    }

    // A separator _ is written before an upper case character that follows a lower case letter or a digit, and before an
    // upper case character that is followed by a lower case letter when the previous one is upper case; the letters are lowered:
    // TotalAmount is total_amount, HTTPServer is http_server, Order2Line is order2_line.
    internal static string ToSnakeCaseLower(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        State state = State.NotStarted;
        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];
            switch (char.GetUnicodeCategory(current))
            {
                case UnicodeCategory.UppercaseLetter:
                    AppendUpper(builder, name, index, state);
                    state = State.Upper;
                    break;
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.DecimalDigitNumber:
                    AppendSeparatorAfterSpace(builder, state);
                    builder.Append(current);
                    state = State.LowerOrDigit;
                    break;
                case UnicodeCategory.SpaceSeparator:
                    state = state == State.NotStarted ? state : State.Space;
                    break;
                default:
                    AppendSeparatorAfterSpace(builder, state);
                    builder.Append(current);
                    state = State.NotStarted;
                    break;
            }
        }

        return builder.ToString();
    }

    private static void AppendUpper(StringBuilder builder, string name, int index, State state)
    {
        bool separator = state switch
        {
            State.LowerOrDigit or State.Space => true,
            State.Upper => index + 1 < name.Length && char.IsLower(name[index + 1]),
            _ => false,
        };
        if (separator)
        {
            builder.Append('_');
        }

        builder.Append(Lower(name[index]));
    }

    private static void AppendSeparatorAfterSpace(StringBuilder builder, State state)
    {
        if (state == State.Space)
        {
            builder.Append('_');
        }
    }

    private static char Lower(char c) => char.ToLower(c, CultureInfo.InvariantCulture);

    private enum State
    {
        NotStarted,
        Upper,
        LowerOrDigit,
        Space,
    }
}
