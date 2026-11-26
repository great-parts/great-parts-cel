using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Tests.Examples.Generated;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Generated;

// The elements of a list and the values of a dictionary of classes in a property of an anonymous object. The first read of the
// variable checks the collection at the top level only: a null element where the annotation does not allow one is UnexpectedNull
// when the expression reads that element, at the node that reads it, and the read of the variable charges nothing for the elements.
// An element is converted by the place rule of a member of a context: bare for a sealed class, a structure and an instance of exactly
// the declared class, paired with the description of the declared type for an instance of a derived class.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "S-1")]
public sealed class GeneratedClassElementTests
{
    [Fact]
    public void NullElementInAList_IsUnexpectedNullWhereTheElementIsRead()
    {
        const string Text = "strict[1].email";
        var data = new { strict = new List<Customer> { new() { Email = "a" }, null!, new() { Email = "c" } } };

        Assert.True(CelExpression.Evaluate<bool>("size(strict) == 3", data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("strict.exists(c, c.email == 'a')", data, cancellationToken: TestContext.Current.CancellationToken));
        AssertUnexpectedNullAtTheIndex(Text, data);
    }

    [Fact]
    public void NullValueInADictionary_IsUnexpectedNullWhereTheValueIsRead()
    {
        const string Text = "byName['b'].email";
        var data = new { byName = new Dictionary<string, Customer> { ["a"] = new() { Email = "a" }, ["b"] = null! } };

        Assert.True(CelExpression.Evaluate<bool>("size(byName) == 2 && byName['a'].email == 'a'", data, cancellationToken: TestContext.Current.CancellationToken));
        AssertUnexpectedNullAtTheIndex(Text, data);
    }

    // The check of the binding does not descend into the list, so it costs 0 and not 100: a limit below the number of elements is
    // enough for size().
    [Fact]
    public void FirstReadOfAListOfClasses_DoesNotChargeItsElements()
    {
        var lines = new List<OrderLine>();
        for (int index = 0; index < 100; index++)
        {
            lines.Add(new OrderLine { Sku = "a", Qty = 1, Price = 1.0 });
        }

        Assert.True(CelExpression.Evaluate<bool>("size(lines) == 100", new { lines }, costLimit: 10, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ElementOfASealedClassOrOfExactlyTheDeclaredClass_IsBare_AndOfADerivedClassIsPaired()
    {
        var customers = new { customers = new List<Customer> { new() { Email = "a" } } };
        var contacts = new { contacts = new List<Contact> { new VipContact { Name = "Vip", Level = 3 }, new() { Name = "Plain" } } };
        var points = new { points = new List<CallPoint> { new() { X = 1, Y = 2 } } };

        // The bare element and the paired one have the type and the equality that the pair had.
        Assert.True(CelExpression.Evaluate<bool>(
            "customers[0] == customers[0] && type(customers[0]) == " + typeof(Customer).FullName,
            customers,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>(
            "contacts[0] == contacts[0] && contacts[1] == contacts[1]"
            + " && type(contacts[0]) == " + typeof(Contact).FullName + " && type(contacts[1]) == " + typeof(Contact).FullName,
            contacts,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("type(points[0]) == " + typeof(CallPoint).FullName, points, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(Element(customers, "customers", 0).PairedType);
        Assert.Equal(typeof(Contact).FullName, Element(contacts, "contacts", 0).PairedType?.Name);
        Assert.Null(Element(contacts, "contacts", 1).PairedType);
        Assert.Null(Element(points, "points", 0).PairedType);
    }

    // The text reads a null element through an index: the evaluation fails with UnexpectedNull at that index.
    private static void AssertUnexpectedNullAtTheIndex(string text, object data)
    {
        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<string>(text, data, cancellationToken: TestContext.Current.CancellationToken));
        CelError error = Assert.Single(exception.Errors);
        Assert.Equal((CelErrorCode.UnexpectedNull, text.IndexOf('[', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
    }

    // An element of the list that the registration of the anonymous type binds to a variable, as the expression reads it.
    private static CelValue Element(object data, string name, int index)
    {
        Assert.True(AnonymousRegistry.TryGet(data.GetType(), out AnonymousRegistry.Entry? entry));
        var bindings = new CelBindings();
        entry.Bind(data, bindings);
        Assert.True(bindings.Values[name].TryGetList(out CelList? list));
        return list[index];
    }
}
