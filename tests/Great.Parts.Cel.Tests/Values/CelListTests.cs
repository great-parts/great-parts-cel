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
        CelList.Enumerator first = list.GetEnumerator();
        CelList.Enumerator second = list.GetEnumerator();

        Assert.True(first.MoveNext());
        Assert.True(second.MoveNext());
        Assert.Equal(first.Current, second.Current);
        Assert.False(first.MoveNext());
        Assert.Equal(CelValue.FromBoolean(true), second.Current);
        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void GetEnumerator_IsAStructure_ThatForeachStepsThroughInOrder()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1), CelValue.FromString("two"), CelValue.Null);

        CelList.Enumerator enumerator = list.GetEnumerator();
        var seen = new List<CelValue>();
        foreach (CelValue element in list)
        {
            seen.Add(element);
        }

        Assert.True(typeof(CelList.Enumerator).IsValueType);
        Assert.Equal([CelValue.FromInt64(1), CelValue.FromString("two"), CelValue.Null], seen);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(CelValue.FromInt64(1), enumerator.Current);
    }

    [Fact]
    public void Enumerator_ReadsOneElementPerStep_AndHasNoCurrentOutsideTheList()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2));
        CelList.Enumerator enumerator = list.GetEnumerator();

        Assert.Equal(default(CelValue), enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(CelValue.FromInt64(1), enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(CelValue.FromInt64(2), enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default(CelValue), enumerator.Current);
        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
    }

    [Fact]
    public void Enumerator_OfAnEmptyList_HasNoElements()
    {
        CelList.Enumerator enumerator = CelList.Create().GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ByDefault_HasNoElements()
    {
        var created = new CelList.Enumerator();
        CelList.Enumerator byDefault = default;

        Assert.False(created.MoveNext());
        Assert.False(byDefault.MoveNext());
        Assert.Equal(default(CelValue), byDefault.Current);
        byDefault.Dispose();
    }

    [Fact]
    public void Enumerator_ThroughTheInterfaces_StepsInOrderAndCanStartAgain()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2));

        using IEnumerator<CelValue> generic = ((IEnumerable<CelValue>)list).GetEnumerator();
        IEnumerator plain = ((IEnumerable)list).GetEnumerator();

        Assert.True(generic.MoveNext());
        Assert.True(generic.MoveNext());
        Assert.False(generic.MoveNext());
        generic.Reset();
        Assert.True(generic.MoveNext());
        Assert.Equal(CelValue.FromInt64(1), generic.Current);
        Assert.True(plain.MoveNext());
        Assert.Equal(CelValue.FromInt64(1), plain.Current);
    }

    [Fact]
    public void Enumerator_OverAViewWithAFailingElement_GivesTheErrorValueAndReadsEachElementOnce()
    {
        int reads = 0;
        CelList view = CelList.CreateView(
            new List<long> { 1, 2, 3 },
            value =>
            {
                reads++;
                return value == 2 ? throw new InvalidOperationException() : CelValue.FromInt64(value);
            });
        var kinds = new List<CelValueKind>();

        CelList.Enumerator enumerator = view.GetEnumerator();
        while (enumerator.MoveNext())
        {
            kinds.Add(enumerator.Current.Kind);
        }

        Assert.Equal([CelValueKind.Int, CelValueKind.Error, CelValueKind.Int], kinds);
        Assert.Equal(3, reads);
    }
}
