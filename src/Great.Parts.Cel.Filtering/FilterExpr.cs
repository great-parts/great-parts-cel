using System.Collections.Generic;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// A node of the tree of a filter, with its range in UTF-16 code units of the text of the filter.
/// </summary>
internal abstract class FilterExpr(int start, int end)
{
    /// <summary>The offset of the first code unit of the node.</summary>
    internal int Start { get; } = start;

    /// <summary>The offset after the last code unit of the node.</summary>
    internal int End { get; } = end;
}

/// <summary>
/// An empty filter: no text, or white space only.
/// </summary>
internal sealed class FilterEmpty() : FilterExpr(0, 0);

/// <summary>
/// How the operands of a <see cref="FilterJoin"/> are joined.
/// </summary>
internal enum FilterJoinKind
{
    /// <summary>Operands joined by the keyword <c>AND</c>.</summary>
    And,

    /// <summary>Operands separated by white space only.</summary>
    Sequence,

    /// <summary>Operands joined by the keyword <c>OR</c>.</summary>
    Or,
}

/// <summary>
/// Two or more operands of one kind of join, in the order of the text.
/// </summary>
/// <param name="kind">The kind of the join.</param>
/// <param name="operands">The operands, at least two.</param>
/// <param name="keywords">The ranges of the keywords between the operands: one fewer than the operands; empty for a sequence.</param>
internal sealed class FilterJoin(FilterJoinKind kind, IReadOnlyList<FilterExpr> operands, IReadOnlyList<(int Start, int End)> keywords)
    : FilterExpr(operands[0].Start, operands[^1].End)
{
    /// <summary>The kind of the join.</summary>
    internal FilterJoinKind Kind { get; } = kind;

    /// <summary>The operands.</summary>
    internal IReadOnlyList<FilterExpr> Operands { get; } = operands;

    /// <summary>The ranges of the keywords between the operands.</summary>
    internal IReadOnlyList<(int Start, int End)> Keywords { get; } = keywords;
}

/// <summary>
/// The negation of a term: <c>NOT</c> or <c>-</c> and the term.
/// </summary>
/// <param name="operand">The term.</param>
/// <param name="markStart">The offset of the mark, <c>NOT</c> or <c>-</c>.</param>
/// <param name="markEnd">The offset after the mark.</param>
internal sealed class FilterNot(FilterExpr operand, int markStart, int markEnd) : FilterExpr(markStart, operand.End)
{
    /// <summary>The negated term.</summary>
    internal FilterExpr Operand { get; } = operand;

    /// <summary>The offset of the mark.</summary>
    internal int MarkStart { get; } = markStart;

    /// <summary>The offset after the mark.</summary>
    internal int MarkEnd { get; } = markEnd;
}

/// <summary>
/// An expression in parentheses.
/// </summary>
/// <param name="inner">The expression.</param>
/// <param name="start">The offset of the opening parenthesis.</param>
/// <param name="end">The offset after the closing parenthesis.</param>
internal sealed class FilterGroup(FilterExpr inner, int start, int end) : FilterExpr(start, end)
{
    /// <summary>The expression in the parentheses.</summary>
    internal FilterExpr Inner { get; } = inner;
}

/// <summary>
/// One part of a dotted name or of a number.
/// </summary>
/// <param name="text">The text of the part, as written; the value of a string, unescaped.</param>
/// <param name="start">The offset of the first code unit.</param>
/// <param name="end">The offset after the last code unit.</param>
/// <param name="quoted">The string, for a part in quotes; <see langword="null"/> otherwise.</param>
internal readonly struct FilterSegment(string text, int start, int end, FilterString? quoted)
{
    /// <summary>The text of the part, as written.</summary>
    internal string Text { get; } = text;

    /// <summary>The offset of the first code unit.</summary>
    internal int Start { get; } = start;

    /// <summary>The offset after the last code unit.</summary>
    internal int End { get; } = end;

    /// <summary>The string, for a part in quotes; <see langword="null"/> otherwise.</summary>
    internal FilterString? Quoted { get; } = quoted;
}

/// <summary>
/// A value and the fields that follow it: <c>author.name</c>, a number such as <c>1.5</c>, a text or a string.
/// </summary>
internal sealed class FilterMember(IReadOnlyList<FilterSegment> segments, string source)
    : FilterExpr(segments[0].Start, segments[^1].End)
{
    /// <summary>The parts between the dots.</summary>
    internal IReadOnlyList<FilterSegment> Segments { get; } = segments;

    /// <summary>The text of the member as written, from the start of the first part to the end of the last, with the dots.</summary>
    internal string Source { get; } = source;
}

/// <summary>
/// A call of a function: a dotted name and the arguments.
/// </summary>
/// <param name="name">The parts of the name.</param>
/// <param name="arguments">The arguments.</param>
/// <param name="end">The offset after the closing parenthesis.</param>
internal sealed class FilterCall(IReadOnlyList<FilterSegment> name, IReadOnlyList<FilterExpr> arguments, int end)
    : FilterExpr(name[0].Start, end)
{
    /// <summary>The parts of the name.</summary>
    internal IReadOnlyList<FilterSegment> Name { get; } = name;

    /// <summary>The arguments.</summary>
    internal IReadOnlyList<FilterExpr> Arguments { get; } = arguments;
}

/// <summary>
/// A comparison: a field or a function, a comparator and a value.
/// </summary>
/// <param name="left">The field or the function call.</param>
/// <param name="comparator">The comparator.</param>
/// <param name="comparatorStart">The offset of the comparator.</param>
/// <param name="comparatorEnd">The offset after the comparator.</param>
/// <param name="right">The value: a member, a function call or an expression in parentheses.</param>
internal sealed class FilterRestriction(FilterExpr left, FilterComparator comparator, int comparatorStart, int comparatorEnd, FilterExpr right)
    : FilterExpr(left.Start, right.End)
{
    /// <summary>The field or the function call.</summary>
    internal FilterExpr Left { get; } = left;

    /// <summary>The comparator.</summary>
    internal FilterComparator Comparator { get; } = comparator;

    /// <summary>The offset of the comparator.</summary>
    internal int ComparatorStart { get; } = comparatorStart;

    /// <summary>The offset after the comparator.</summary>
    internal int ComparatorEnd { get; } = comparatorEnd;

    /// <summary>The value.</summary>
    internal FilterExpr Right { get; } = right;
}
