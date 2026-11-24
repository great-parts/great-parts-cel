using System.Text;

namespace Great.Parts.Cel.Filtering.Tests;

// Random texts and filters of the grammar for the tests of untrusted filters, from a repeatable source of numbers, so that a failing
// filter is the same on every run.
internal static class FilterGenerator
{
    // The characters that the lexer treats specially, and a few that it does not, so that random texts reach every rule.
    private const string Alphabet = "abAND ORNOT0123456789.,()-:=<>!*\"'\\\t\r\n+_";

    // A text of random characters of the alphabet.
    internal static string RandomText(Source random, int length)
    {
        StringBuilder text = new(length);
        for (int i = 0; i < length; i++)
        {
            text.Append(Alphabet[random.Next(0, Alphabet.Length)]);
        }

        return text.ToString();
    }

    // expression = sequence { " AND " sequence }, and below it the rules of the grammar, each with the white space that it needs.
    internal static void Expression(Source random, StringBuilder text, int depth)
    {
        Sequence(random, text, depth);
        while (random.Next(0, 4) == 0)
        {
            text.Append(" AND ");
            Sequence(random, text, depth);
        }
    }

    // One restriction: a comparable, and most often a comparator and an argument.
    internal static void Restriction(Source random, StringBuilder text)
    {
        Comparable(random, text);
        if (random.Next(0, 3) != 0)
        {
            string[] comparators = ["=", "!=", "<", "<=", ">", ">=", ":"];
            text.Append(random.Next(0, 2) == 0 ? " " : string.Empty).Append(comparators[random.Next(0, comparators.Length)]).Append(random.Next(0, 2) == 0 ? " " : string.Empty);
            Argument(random, text);
        }
    }

    private static void Sequence(Source random, StringBuilder text, int depth)
    {
        Factor(random, text, depth);
        while (random.Next(0, 4) == 0)
        {
            text.Append(random.Next(0, 3) == 0 ? "\t" : " ");
            Factor(random, text, depth);
        }
    }

    private static void Factor(Source random, StringBuilder text, int depth)
    {
        Term(random, text, depth);
        while (random.Next(0, 4) == 0)
        {
            text.Append(" OR ");
            Term(random, text, depth);
        }
    }

    private static void Term(Source random, StringBuilder text, int depth)
    {
        switch (random.Next(0, 4))
        {
            case 0:
                text.Append("NOT ");
                break;
            case 1:
                text.Append('-');
                break;
            default:
                break;
        }

        if (depth > 0 && random.Next(0, 5) == 0)
        {
            text.Append('(');
            Expression(random, text, depth - 1);
            text.Append(')');
        }
        else
        {
            Restriction(random, text);
        }
    }

    private static void Argument(Source random, StringBuilder text)
    {
        switch (random.Next(0, 6))
        {
            case 0:
                text.Append('(');
                Comparable(random, text);
                text.Append(" OR ");
                Comparable(random, text);
                text.Append(')');
                break;
            case 1:
                text.Append("-5.5e3");
                break;
            default:
                Comparable(random, text);
                break;
        }
    }

    private static void Comparable(Source random, StringBuilder text)
    {
        string[] names =
        [
            "title", "pages", "author", "x1", "a-b", "ANDx", "Dune", "5", "*", "NOTa", "\"q u\"", "'z'", "\"\\*s\"",
            "tags", "editions", "labels", "state", "published", "read_time", "subtitle", "meta", "copies", "rating", "available",
            "\"*x\"", "\"x*\"", "\"*\"", "20s", "true", "null", "\"2026-01-01T00:00:00Z\"", "Published", "\"\"",
        ];
        if (random.Next(0, 4) == 0)
        {
            text.Append(random.Next(0, 2) == 0 ? "lower" : "f.g").Append('(');
            if (random.Next(0, 2) == 0)
            {
                text.Append(names[random.Next(0, names.Length)]).Append(", ").Append("x.AND");
            }

            text.Append(')');
            return;
        }

        text.Append(names[random.Next(0, names.Length)]);
        while (random.Next(0, 3) == 0)
        {
            string[] parts = ["NOT", "y", "year", "name", "genre", "formats", "city", "address", "\"\""];
            text.Append('.').Append(parts[random.Next(0, parts.Length)]);
        }
    }

    // A repeatable source of numbers (SplitMix64).
    internal sealed class Source(ulong seed)
    {
        private ulong _state = seed;

        // A number from the first limit, included, to the second, excluded.
        internal int Next(int from, int limit)
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return from + (int)(z % (ulong)(limit - from));
        }
    }
}
