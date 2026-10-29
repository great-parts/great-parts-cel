using System;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

public sealed class CelSourceLocationTests
{
    [Fact]
    public void PublicConstructor_KeepsItsArguments()
    {
        CelSourceLocation location = new("rules/order.cel", 0, 1, 1);

        Assert.Equal(("rules/order.cel", 0, 1, 1), (location.SourceName, location.Offset, location.Line, location.Column));
    }

    [Fact]
    public void PublicConstructor_NullSourceName_Throws()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CelSourceLocation(null!, 0, 1, 1));

        Assert.Equal("sourceName", exception.ParamName);
    }

    [Fact]
    public void PublicConstructor_EmptySourceName_Throws()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CelSourceLocation(string.Empty, 0, 1, 1));

        Assert.Equal("sourceName", exception.ParamName);
    }

    [Theory]
    [InlineData(-1, 1, 1, "offset")]
    [InlineData(0, 0, 1, "line")]
    [InlineData(0, 1, 0, "column")]
    public static void PublicConstructor_NumberOutOfRange_Throws(int offset, int line, int column, string parameter)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CelSourceLocation("a.json", offset, line, column));

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void InternalConstructor_GivesAPositionWithoutASourceName()
    {
        CelSourceLocation location = new(7, 2, 3);

        Assert.Equal(((string?)null, 7, 2, 3), (location.SourceName, location.Offset, location.Line, location.Column));
    }

    [Fact]
    public void Default_MeansNoPosition()
    {
        CelSourceLocation location = default;

        Assert.Equal(((string?)null, 0, 0, 0), (location.SourceName, location.Offset, location.Line, location.Column));
    }

    [Fact]
    public void EqualPositions_AreEqualByEveryMember()
    {
        CelSourceLocation left = new("a.json", 5, 2, 3);
        CelSourceLocation right = new(string.Concat("a.", "json"), 5, 2, 3);

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void PositionsWithoutASourceName_AreEqual()
    {
        CelSourceLocation left = new(5, 2, 3);
        CelSourceLocation right = new(5, 2, 3);

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [InlineData("A.json", 5, 2, 3)]
    [InlineData("b.json", 5, 2, 3)]
    [InlineData("a.json", 6, 2, 3)]
    [InlineData("a.json", 5, 3, 3)]
    [InlineData("a.json", 5, 2, 4)]
    public static void PositionsThatDifferInOneMember_AreNotEqual(string sourceName, int offset, int line, int column)
    {
        CelSourceLocation left = new("a.json", 5, 2, 3);
        CelSourceLocation right = new(sourceName, offset, line, column);

        Assert.False(left.Equals(right));
        Assert.False(left.Equals((object)right));
        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void PositionWithASourceName_DiffersFromTheSamePositionWithout()
    {
        Assert.True(new CelSourceLocation("a.json", 5, 2, 3) != new CelSourceLocation(5, 2, 3));
    }

    [Fact]
    public void EqualsObject_OtherType_IsFalse()
    {
        CelSourceLocation location = new(0, 1, 1);

        Assert.False(location.Equals("a.json"));
        Assert.False(location.Equals(null));
    }
}
