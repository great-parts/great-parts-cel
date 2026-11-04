using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// !, equality and ordering over the value rules. Equality takes values of any kinds and charges each pair
// before comparing it through the meter of the call site; ordering takes the kinds of its overload.
internal static class Comparisons
{
    // The relations of ordering.
    internal enum Relation
    {
        Less,
        LessEquals,
        Greater,
        GreaterEquals,
    }

    // logical_not, (bool) -> bool.
    internal static CelValue LogicalNot(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetBoolean(out bool operand) ? CelValue.FromBoolean(!operand) : site.NoMatchingOverload(arguments);

    // equals, (A, A) -> bool.
    internal static CelValue AreEqual(ReadOnlySpan<CelValue> arguments, in CallSite site) => Equality(arguments, in site, true);

    // not_equals, (A, A) -> bool.
    internal static CelValue AreNotEqual(ReadOnlySpan<CelValue> arguments, in CallSite site) => Equality(arguments, in site, false);

    // The implementation of one overload of ordering, for the kinds of its two parameters.
    internal static OverloadImplementation Ordering(Relation relation, CelValueKind left, CelValueKind right) =>
        new OrderingOverload(relation, left, right).Compare;

    private static CelValue Equality(ReadOnlySpan<CelValue> arguments, in CallSite site, bool equal) =>
        EqualityRules.AreEqual(arguments[0], arguments[1], site.Meter, out CelValue error) switch
        {
            ValueEquality.Equal => CelValue.FromBoolean(equal),
            ValueEquality.NotEqual => CelValue.FromBoolean(!equal),
            ValueEquality.Error => error,
            _ => site.Error(CelErrorCode.InsufficientStack, EqualityRules.InsufficientStackMessage),
        };

    private sealed class OrderingOverload(Relation relation, CelValueKind left, CelValueKind right)
    {
        internal CelValue Compare(ReadOnlySpan<CelValue> arguments, in CallSite site)
        {
            if (arguments[0].Kind != left || arguments[1].Kind != right)
            {
                return site.NoMatchingOverload(arguments);
            }

            // An unordered pair, with a NaN, makes every relation false.
            ValueOrdering ordering = OrderingRules.Compare(arguments[0], arguments[1]);
            return CelValue.FromBoolean(relation switch
            {
                Relation.Less => ordering == ValueOrdering.Less,
                Relation.LessEquals => ordering is ValueOrdering.Less or ValueOrdering.Equal,
                Relation.Greater => ordering == ValueOrdering.Greater,
                _ => ordering is ValueOrdering.Greater or ValueOrdering.Equal,
            });
        }
    }
}
