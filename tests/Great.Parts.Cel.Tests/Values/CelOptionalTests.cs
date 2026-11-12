using System;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// CelOptional<T> by itself: the empty structure is default, a structure over null has a value, and .NET equality compares the
// state and the values, so a NaN equals itself here, though not in CEL.
public sealed class CelOptionalTests
{
    [Fact]
    public void Default_IsEmpty_AndItsValueIsAnInvalidOperation()
    {
        CelOptional<string> empty = default;

        Assert.False(empty.HasValue);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => empty.Value);
        Assert.Equal("The optional is empty.", error.Message);
    }

    [Fact]
    public void OverNull_HasAValue_AndDiffersFromDefault()
    {
        var overNull = new CelOptional<string?>(null);
        CelOptional<string?> empty = default;

        Assert.True(overNull.HasValue);
        Assert.Null(overNull.Value);
        Assert.False(overNull.Equals(empty));
        Assert.True(overNull != empty);
        Assert.False(overNull == empty);
        Assert.False(overNull.Equals((object)empty));
        Assert.Equal(new CelOptional<string?>(null), overNull);
    }

    [Fact]
    public void Equality_ComparesTheStateAndTheValues()
    {
        var one = new CelOptional<long>(1);
        var anotherOne = new CelOptional<long>(1);

        Assert.True(one.Equals(anotherOne));
        Assert.True(one == anotherOne);
        Assert.False(one != anotherOne);
        Assert.Equal(one.GetHashCode(), anotherOne.GetHashCode());
        Assert.False(one.Equals(new CelOptional<long>(2)));
        Assert.False(one.Equals(default(CelOptional<long>)));
        Assert.True(default(CelOptional<long>).Equals(default(CelOptional<long>)));
        Assert.Equal(0, default(CelOptional<long>).GetHashCode());
    }

    [Fact]
    public void ObjectEquality_IsTrueOnlyForAnEqualOptionalOfTheSameType()
    {
        var one = new CelOptional<long>(1);

        Assert.True(one.Equals((object)new CelOptional<long>(1)));
        Assert.False(one.Equals((object)1L));
        Assert.False(one.Equals((object)new CelOptional<int>(1)));
        Assert.False(one.Equals(null));
    }

    [Fact]
    public void NaN_EqualsItself_ByNetEquality()
    {
        var first = new CelOptional<double>(double.NaN);
        var second = new CelOptional<double>(double.NaN);

        Assert.True(first.Equals(second));
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
