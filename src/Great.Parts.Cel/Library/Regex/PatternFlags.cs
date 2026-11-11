namespace Great.Parts.Cel.Library.Regex;

// The flags of the parse, which are also kept in the nodes. The flags of syntax.Perl that never change — a negated class matches a
// line feed, the Perl extensions, the Unicode groups — are not flags here: they are always on.
internal static class PatternFlags
{
    // (?i): case-insensitive.
    internal const int FoldCase = 1;

    // (?s): "." matches a line feed.
    internal const int DotNewline = 2;

    // Not (?m): ^ and $ are the beginning and the end of the text only.
    internal const int OneLine = 4;

    // (?U) and the lazy forms: kept for the simplification and nothing else.
    internal const int NonGreedy = 8;

    // The flags at the start of a parse: syntax.Perl.
    internal const int Initial = OneLine;
}
