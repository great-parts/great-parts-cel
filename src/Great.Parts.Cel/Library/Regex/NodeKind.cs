namespace Great.Parts.Cel.Library.Regex;

// The kinds of the nodes of a parsed pattern, as the operators of regexp/syntax of Go, in the same order: the order decides
// which of two nodes of an alternation is merged into the other. LeftParen and VerticalBar are marks of the parse stack and are
// not nodes of the finished tree.
internal enum NodeKind
{
    None = 0,
    NoMatch = 1,
    EmptyMatch = 2,
    Literal = 3,
    Class = 4,
    AnyCharNotNewline = 5,
    AnyChar = 6,
    BeginLine = 7,
    EndLine = 8,
    BeginText = 9,
    EndText = 10,
    WordBoundary = 11,
    NoWordBoundary = 12,
    Capture = 13,
    Star = 14,
    Plus = 15,
    Quest = 16,
    Repeat = 17,
    Concat = 18,
    Alternate = 19,
    LeftParen = 128,
    VerticalBar = 129,
}
