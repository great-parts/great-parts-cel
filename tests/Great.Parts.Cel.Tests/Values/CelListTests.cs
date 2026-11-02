using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

public sealed class CelListTests
{
    [Fact]
    public void Create_KeepsTheElementsInOrder()
    {
        CelList list = CelList.Create(CelValue.FromInt64(3), CelValue.FromString("two"), CelValue.Null, CelValue.FromUInt64(1));

        CelValue[] expected = [CelValue.FromInt64(3), CelValue.FromString("two"), CelValue.Null, CelValue.FromUInt64(1)];
        Assert.Equal(4, list.Count);
        Assert.Equal(expected, Enumerable.Range(0, list.Count).Select(index => list[index]));
        Assert.Equal(expected, list);
        Assert.Equal(expected, ((IEnumerable)list).Cast<CelValue>());
    }

    [Fact]
    public void Create_WithoutElements_IsEmpty()
    {
        CelList list = CelList.Create();

        Assert.Empty(list);
    }

    [Fact]
    public void Create_CopiesTheElements()
    {
        CelValue[] source = [CelValue.FromInt64(1), CelValue.FromInt64(2)];

        CelList list = CelList.Create(source);
        source[0] = CelValue.FromInt64(9);

        Assert.Equal([CelValue.FromInt64(1), CelValue.FromInt64(2)], list);
    }

    [Fact]
    public void Create_WithAnErrorValue_Throws()
    {
        CelValue error = CelValue.FromError(new CelError(CelErrorCode.InternalError, CelErrorStage.Evaluate, "failed", default, null, null));

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CelList.Create(CelValue.FromInt64(1), error));

        Assert.Equal("items", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public static void Indexer_OutsideTheList_Throws(int index)
    {
        CelList list = CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2));

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => list[index]);

        Assert.Equal("index", exception.ParamName);
    }

    [Fact]
    public void Enumeration_CanRepeat()
    {
        CelList list = CelList.Create(CelValue.FromBoolean(true));
        using IEnumerator<CelValue> first = list.GetEnumerator();
        using IEnumerator<CelValue> second = list.GetEnumerator();

        Assert.True(first.MoveNext());
        Assert.True(second.MoveNext());
        Assert.Equal(first.Current, second.Current);
        Assert.False(first.MoveNext());
    }
}
