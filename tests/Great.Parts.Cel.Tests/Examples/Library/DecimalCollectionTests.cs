#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// A set of decimals that the application gives as a list (the page of the values, "decimal"; the type mapping, the row of
// IReadOnlySet<T>): CelList.CreateSorted orders the elements of the kind decimal in the order of CEL, by the number, so a set of
// Decimal128 or of System.Decimal is read as a sorted list; two elements that are equal in CEL, such as 1.5 and 1.50, or 0 and -0, have no
// order and are ArgumentException, as 0.0 and -0.0 of a double are, and so are elements of two kinds and a mixture of a decimal and
// an int. The order is for this one use: the sort of a list in an expression is the sort of the standard sets, which a list of
// decimals does not have (DecimalEqualityOrderingTests).
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-5")]
public sealed class DecimalCollectionTests
{
    [Fact]
    public void SetOfDecimal128_IsSortedByTheNumber_WithTheExponentOfEachElementKept()
    {
        var set = new HashSet<Decimal128>
        {
            DecimalExample.Make(25, -1),
            DecimalExample.Make(15, -1),
            DecimalExample.Make(5, -1, negative: true),
            DecimalExample.Make(1000, -2),
            DecimalExample.Make(1, 3),
        };

        CelList sorted = CelList.CreateSorted(set, CelValue.FromDecimal);

        Assert.Equal(
            new (bool, int, int)[] { (true, 5, -1), (false, 15, -1), (false, 25, -1), (false, 1000, -2), (false, 1, 3) },
            sorted.Select(Parts));
    }

    [Fact]
    public void SetOfSystemDecimal_IsSortedByTheNumber()
    {
        CelList sorted = CelList.CreateSorted(new HashSet<decimal> { 2.5m, -1.5m, 0.5m, 100m }, value => CelValue.FromDecimal(value));

        Decimal128[] numbers = [.. sorted.Select(value => Number(value))];

        Assert.Equal(new Decimal128[] { -1.5m, 0.5m, 2.5m, 100m }, numbers);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void ElementsThatAreEqualInCel_AreArgumentException()
    {
        Decimal128[][] refused =
        [
            [DecimalExample.Make(15, -1), DecimalExample.Make(150, -2)],
            [DecimalExample.Make(0, 0), DecimalExample.Make(0, 0, negative: true)],
            [DecimalExample.Make(1, 2), DecimalExample.Make(100, 0)],
        ];

        Assert.All(refused, values => Assert.Throws<ArgumentException>(() => CelList.CreateSorted(values, CelValue.FromDecimal)));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DecimalWithAnotherKind_IsArgumentException()
    {
        var mixed = new[] { CelValue.FromDecimal(DecimalExample.Make(15, -1)), CelValue.FromInt64(1) };
        var withNull = new[] { CelValue.FromDecimal(DecimalExample.Make(15, -1)), CelValue.Null };
        var withDouble = new[] { CelValue.FromDecimal(DecimalExample.Make(15, -1)), CelValue.FromDouble(2.5) };

        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(mixed, value => value));
        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(withNull, value => value));
        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(withDouble, value => value));
    }

    private static Decimal128 Number(CelValue value)
    {
        Assert.True(value.TryGetDecimal(out Decimal128 number));
        return number;
    }

    private static (bool Negative, int Coefficient, int Exponent) Parts(CelValue value)
    {
        (bool negative, UInt128 coefficient, int exponent) = DecimalExample.Parts(Number(value));
        return (negative, (int)coefficient, exponent);
    }
}
#endif
