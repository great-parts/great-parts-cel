namespace Great.Parts.Cel.Library.Regex;

// The error of a pattern: the offset, in UTF-16 code units of the pattern, of the fragment that the error is about (0 for an error
// about the whole pattern), and the reason. The reasons are the codes of the errors of regexp/syntax of Go as they are, except two:
// the Perl error carries the fragment after the colon, and a lone surrogate is "invalid UTF-16".
internal sealed class RegexError(int offset, string reason)
{
    internal const string InvalidCharRange = "invalid character class range";
    internal const string InvalidEscape = "invalid escape sequence";
    internal const string InvalidNamedCapture = "invalid named capture";
    internal const string UnsupportedPerlSyntax = "unsupported Perl syntax: ";
    internal const string InvalidRepeatOperator = "invalid nested repetition operator";
    internal const string InvalidRepeatCount = "invalid repeat count";
    internal const string InvalidUtf16 = "invalid UTF-16";
    internal const string MissingBracket = "missing closing ]";
    internal const string MissingParen = "missing closing )";
    internal const string MissingRepeatArgument = "missing argument to repetition operator";
    internal const string TrailingBackslash = "trailing backslash at end of expression";
    internal const string UnexpectedParen = "unexpected )";
    internal const string NestingDepth = "expression nests too deeply";
    internal const string TooLarge = "expression too large";

    // The offset of the fragment in the pattern, in UTF-16 code units.
    internal int Offset { get; } = offset;

    // The reason, without the offset.
    internal string Reason { get; } = reason;
}
