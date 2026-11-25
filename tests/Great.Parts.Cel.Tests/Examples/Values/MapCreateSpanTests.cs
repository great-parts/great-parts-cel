using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// CelMap.Create(params ReadOnlySpan<KeyValuePair<CelValue, CelValue>>) beside the overload with IEnumerable: a collection expression
// binds to the span, without a wrapper object and a second copy, and gives the same map with the same checks as the enumerable.
// A null enumerable still throws ArgumentNullException; a span cannot be null, so null is passed to the enumerable overload with an
// explicit type.
[Trait("Requirement", "P-4")]
public sealed class MapCreateSpanTests
{
    private static readonly CelValue A = CelValue.FromString("a");
    private static readonly CelValue B = CelValue.FromString("b");

    [Fact]
    public void CollectionExpression_KeepsTheOrderOfTheEntries()
    {
        CelMap map = CelMap.Create([KeyValuePair.Create(B, CelValue.FromInt64(2)), KeyValuePair.Create(A, CelValue.FromInt64(1))]);

        Assert.Equal([B, A], map.Select(entry => entry.Key));
        Assert.True(map.TryGetValue(A, out CelValue value));
        Assert.Equal(CelValue.FromInt64(1), value);
    }

    [Fact]
    public void CollectionExpression_GivesTheMapOfTheEnumerable()
    {
        KeyValuePair<CelValue, CelValue>[] entries = [KeyValuePair.Create(A, CelValue.FromInt64(1)), KeyValuePair.Create(CelValue.FromInt64(7), CelValue.FromBoolean(true))];

        CelMap fromSpan = CelMap.Create([.. entries]);
        CelMap fromEnumerable = CelMap.Create(entries.AsEnumerable());

        Assert.Equal(fromEnumerable.Select(entry => entry.Key), fromSpan.Select(entry => entry.Key));
        Assert.Equal(fromEnumerable.Select(entry => entry.Value), fromSpan.Select(entry => entry.Value));
    }

    [Fact]
    public void EmptyCollectionExpression_IsAnEmptyMap()
    {
        Assert.Empty(CelMap.Create([]));
    }

    // Keys equal by CEL equality (1 and 1u) and a key of a kind a map cannot have: the same ArgumentException as the enumerable
    // overload.
    [Fact]
    public void EqualKeysOrAKeyOfAnotherKind_AreArgumentException()
    {
        Assert.Equal("entries", Assert.Throws<ArgumentException>(() => CelMap.Create(
            [KeyValuePair.Create(CelValue.FromInt64(1), A), KeyValuePair.Create(CelValue.FromUInt64(1), B)])).ParamName);

        Assert.Equal("entries", Assert.Throws<ArgumentException>(() => CelMap.Create(
            [KeyValuePair.Create(CelValue.FromDouble(1.5), A)])).ParamName);
    }

    [Fact]
    public void NullEnumerable_IsArgumentNullException()
    {
        Assert.Equal("entries", Assert.Throws<ArgumentNullException>(() => CelMap.Create((IEnumerable<KeyValuePair<CelValue, CelValue>>)null!)).ParamName);
    }
}
