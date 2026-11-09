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
    public void Add_OfATypeOutsideTheInputRows_IsNotImplementedAndBindsNothing()
    {
        var bindings = new CelBindings();

        Assert.Throws<NotImplementedException>(() => bindings.Add("d", 1m));
        Assert.Empty(bindings.Values);
    }
}
