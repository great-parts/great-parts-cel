using System.Buffers;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library.Text;

namespace Great.Parts.Cel.Library;

// The pattern of a search for a substring that is a string literal of the expression: contains, indexOf, replace and split prepare
// it once when the expression is checked, as matches prepares the literal pattern of a regular expression, and a call searches
// by the prepared SearchValues<string> instead of the ordinal search. A pattern from the data is not prepared: building the values
// allocates and costs more than the search of a short text. The preparation is part of compilation and is not charged.
internal static class SearchPattern
{
    // What stands for the preparation of an empty literal: an empty pattern is not searched, so no values are made for it.
    private static readonly object EmptyLiteral = new();

    // The preparer of the constant argument at index 1: the values of a literal pattern. It charges nothing and cannot fail.
    internal static readonly ConstantPreparer Preparer = static (value, _) =>
        ConstantPreparation.Success(value.Length == 0 ? EmptyLiteral : OrdinalSearch.Prepare(value));

    // The constant argument of the overloads that search a substring: the pattern, at index 1, the receiver counting as 0.
    internal static ConstantArgument Constant { get; } = new(1, Preparer);

    // The values of the literal pattern of the call, or null when the pattern is not a literal or is empty.
    internal static SearchValues<string>? ValuesOf(in CallSite site) => site.Prepared as SearchValues<string>;
}
