using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

public sealed class CelBindingsTests
{
    [Fact]
    public void NewBindings_AreEmpty()
    {
        Assert.Empty(new CelBindings().Values);
    }

    [Fact]
    public void Add_ConvertsByTheStaticTypeAndReturnsTheSameBindings()
    {
        var bindings = new CelBindings();

        CelBindings returned = bindings.Add("i", 41).Add("u", 7u).Add("s", "text").Add("g", Guid.Empty);

        Assert.Same(bindings, returned);
        Assert.Equal(
            new Dictionary<string, CelValue>
            {
                ["i"] = CelValue.FromInt64(41),
                ["u"] = CelValue.FromUInt64(7),
                ["s"] = CelValue.FromString("text"),
                ["g"] = CelValue.FromString("00000000-0000-0000-0000-000000000000"),
            },
            bindings.Values);
    }

    [Fact]
    public void Add_TakesACelValueAsItIs()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1));

        CelBindings bindings = new CelBindings().Add("l", CelValue.FromList(list));

        Assert.True(bindings.Values["l"].TryGetList(out CelList? read));
        Assert.Same(list, read);
    }

    [Fact]
    public void Names_AreCaseSensitive()
    {
        CelBindings bindings = new CelBindings().Add("x", 1L).Add("X", 2L);

        Assert.Equal(CelValue.FromInt64(1), bindings.Values["x"]);
        Assert.Equal(CelValue.FromInt64(2), bindings.Values["X"]);
    }

    [Fact]
    public void Add_OfABoundName_ThrowsAndKeepsTheFirstValue()
    {
        CelBindings bindings = new CelBindings().Add("x", 1L);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => bindings.Add("x", 2L));

        Assert.Equal("name", exception.ParamName);
        Assert.Equal(CelValue.FromInt64(1), Assert.Single(bindings.Values).Value);
    }

    [Fact]
    public void Add_WithANullOrEmptyName_Throws()
    {
        var bindings = new CelBindings();

        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => bindings.Add(null!, 1L)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => bindings.Add("", 1L)).ParamName);
        Assert.Empty(bindings.Values);
    }

    [Fact]
    public void Add_OfATypeOutsideTheInputRows_IsArgumentExceptionAndBindsNothing()
    {
        var bindings = new CelBindings();

        // A System.Decimal and an Int128 are rows on both targets, so the type here is one that is never read.
        Assert.Throws<ArgumentException>(() => bindings.Add("d", new Version(1, 0)));
        Assert.Empty(bindings.Values);
    }

    // The filter of the names is internal: the simple entry sets it for the binding of an anonymous object. The names outside it are not
    // inserted; without it every name is.
    [Fact]
    public void Add_WithAFilter_InsertsOnlyTheNamesOfTheFilter()
    {
        var bindings = new CelBindings();
        bindings.SetNameFilter(["kept", "alsoKept"]);

        CelBindings returned = bindings.Add("kept", 1L).Add("dropped", 2L).Add("alsoKept", 3L).Add("Kept", 4L);

        Assert.Same(bindings, returned);
        Assert.Equal(
            new Dictionary<string, CelValue>
            {
                ["kept"] = CelValue.FromInt64(1),
                ["alsoKept"] = CelValue.FromInt64(3),
            },
            bindings.Values);
    }

    // A name that is not inserted is not a change: an enumerator that runs meanwhile goes on.
    [Fact]
    public void Add_OfANameOutsideTheFilter_IsNotAChange()
    {
        var bindings = new CelBindings().Add("first", 1L);
        bindings.SetNameFilter(["first"]);
        int count = 0;

        foreach (KeyValuePair<string, CelValue> pair in bindings)
        {
            bindings.Add("outside", 2L);
            count++;
            Assert.Equal("first", pair.Key);
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public void Add_WithAnEmptyFilter_InsertsNothing()
    {
        var bindings = new CelBindings();
        bindings.SetNameFilter([]);

        bindings.Add("x", 1L).Add("y", "text");

        Assert.Empty(bindings.Values);
    }

    // The filter limits only the insertion: the name, the conversion and the refusals of the value are checked as without it, for a name
    // that the filter drops as well.
    [Fact]
    public void Add_WithAFilter_StillChecksTheNameAndTheValueOfADroppedName()
    {
        var bindings = new CelBindings();
        bindings.SetNameFilter(["kept"]);

        Assert.Equal("name", Assert.Throws<ArgumentException>(() => bindings.Add("", 1L)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => bindings.Add(null!, 1L)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => bindings.Add("dropped", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local))).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => bindings.Add("dropped", new Version(1, 0))).ParamName);
        Assert.Empty(bindings.Values);
    }

    [Fact]
    public void Add_AfterTheFilterIsLifted_InsertsEveryNameAgain()
    {
        var bindings = new CelBindings();
        bindings.SetNameFilter(["kept"]);
        bindings.Add("dropped", 1L);
        bindings.SetNameFilter(null);

        bindings.Add("dropped", 2L);

        Assert.Equal(CelValue.FromInt64(2), Assert.Single(bindings.Values).Value);
    }
}
