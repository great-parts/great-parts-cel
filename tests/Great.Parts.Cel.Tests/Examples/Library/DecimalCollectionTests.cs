using System;
using System.Collections.Generic;
using System.Linq;
#if NET11_0_OR_GREATER
using System.Numerics;
#endif
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// A set of decimals that the application gives as a list (the page of the values, "decimal"; the type mapping, the row of
// IReadOnlySet<T>): CelList.CreateSorted orders the elements of the kind decimal in the order of CEL, by the number, so a set of
// System.Decimal or, on net11.0, of Decimal128 is read as a sorted list; two elements that are equal in CEL, such as 1.5 and 1.50, or
// 0 and -0, have no order and are ArgumentException, as 0.0 and -0.0 of a double are, and so are elements of two kinds and a mixture
// of a decimal and an int. The order is for this one use: the sort of a list in an expression is the sort of the standard sets, which
// a list of decimals does not have (DecimalEqualityOrderingTests).
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-5")]
public sealed class DecimalCollectionTests
{
    private static readonly decimal[] SortedNumbers = [-1.5m, 0.5m, 2.5m, 10.00m, 100m];

    private static readonly int[] SortedScales = [1, 1, 1, 2, 0];

    [Fact]
    public void SetOfSystemDecimal_IsSortedByTheNumber_WithTheScaleOfEachElementKept()
    {
        CelList sorted = CelList.CreateSorted(new HashSet<decimal> { 2.5m, -1.5m, 0.5m, 100m, 10.00m }, value => CelValue.FromDecimal(value));

        decimal[] numbers = [.. sorted.Select(value => Number(value))];

        Assert.Equal(SortedNumbers, numbers);
        Assert.Equal(SortedScales, numbers.Select(number => (int)number.Scale));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void ElementsThatAreEqualInCel_AreArgumentException()
    {
        decimal[][] refused =
        [
            [1.5m, 1.50m],
            [0m, decimal.Negate(0m)],
            [100m, 100.00m],
        ];

        Assert.All(refused, values => Assert.Throws<ArgumentException>(() => CelList.CreateSorted(values, value => CelValue.FromDecimal(value))));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DecimalWithAnotherKind_IsArgumentException()
    {
        var mixed = new[] { CelValue.FromDecimal(1.5m), CelValue.FromInt64(1) };
        var withNull = new[] { CelValue.FromDecimal(1.5m), CelValue.Null };
        var withDouble = new[] { CelValue.FromDecimal(1.5m), CelValue.FromDouble(2.5) };

        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(mixed, value => value));
        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(withNull, value => value));
        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(withDouble, value => value));
    }

    private static decimal Number(CelValue value)
    {
        Assert.True(value.TryGetDecimal(out decimal number));
        return number;
    }

#if NET11_0_OR_GREATER
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

        CelList sorted = CelList.CreateSorted(set, CelValue.FromDecimal128);

        Assert.Equal(
            new (bool, int, int)[] { (true, 5, -1), (false, 15, -1), (false, 25, -1), (false, 1000, -2), (false, 1, 3) },
            sorted.Select(Parts));
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void ElementsOfDecimal128ThatAreEqualInCel_AreArgumentException()
    {
        Decimal128[][] refused =
        [
            [DecimalExample.Make(15, -1), DecimalExample.Make(150, -2)],
            [DecimalExample.Make(0, 0), DecimalExample.Make(0, 0, negative: true)],
            [DecimalExample.Make(1, 2), DecimalExample.Make(100, 0)],
        ];

        Assert.All(refused, values => Assert.Throws<ArgumentException>(() => CelList.CreateSorted(values, CelValue.FromDecimal128)));
    }

    private static (bool Negative, int Coefficient, int Exponent) Parts(CelValue value)
    {
        Assert.True(value.TryGetDecimal128(out Decimal128 number));
        (bool negative, UInt128 coefficient, int exponent) = DecimalExample.Parts(number);
        return (negative, (int)coefficient, exponent);
    }
#endif
}
