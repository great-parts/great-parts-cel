using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// Classes, structures and interfaces of the application in the properties of an anonymous object of the simple entry. The
// generator of call sites writes their descriptions
// with the rules of a type context and the default options (CamelCase names, public instance members), so a member is read by its
// camel-case name, lazily, without a copy. A list or a dictionary with string keys of such types is a list(C) or a map(string, C):
// each element is read by the members of the declared type C. The types of Great.Parts.Cel.Generator.Samples come from another
// assembly; the types at the end of this file are of this one. The same rows run under the JIT and under Native AOT.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "S-1")]
public sealed class ClassPropertyTests
{
    [Fact]
    public void ClassInAProperty_IsReadByTheCamelCaseNamesOfItsMembers()
    {
        var customer = new Customer { Email = "ann@example.com" };

        Assert.True(CelExpression.Evaluate<bool>(
            "customer.email.endsWith('@example.com')",
            new { customer },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // Order is a type of another assembly; Customer and OrderLine are reached through its members.
    [Fact]
    public void TypeOfAnotherAssembly_AndTypesReachedThroughItsMembers_AreRead()
    {
        var order = new Order
        {
            Id = "A-1",
            Total = 30.0,
            Customer = new Customer { Email = "bob@example.com" },
            Lines = [Line("x", 2, 5.0), Line("y", 1, 20.0)],
        };

        Assert.True(CelExpression.Evaluate<bool>(
            "order.id == 'A-1' && order.total == 30.0 && order.customer.email == 'bob@example.com'"
            + " && order.lines.size() == 2 && order.lines.all(l, l.qty > 0) && order.lines[1].price == 20.0",
            new { order },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StructureAndInterface_AreReadByTheirMembers()
    {
        var data = new { point = new CallPoint { X = 1, Y = 2 }, item = (IPriced)new PricedItem { Price = 2.5 } };

        Assert.True(CelExpression.Evaluate<bool>(
            "point.x + point.y == 3 && item.price == 2.5",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // An array, List<C>, IReadOnlyList<C> and ImmutableArray<C> are each a list(C) over the source.
    [Fact]
    public void ListsOfAClass_OfEveryKind_AreListsOfTheClass()
    {
        OrderLine[] array = [Line("a", 1, 1.0), Line("b", 2, 2.0)];
        var data = new
        {
            array,
            list = new List<OrderLine>(array),
            readOnly = (IReadOnlyList<OrderLine>)array,
            immutable = ImmutableArray.Create(array),
        };

        Assert.True(CelExpression.Evaluate<bool>(
            "array.size() == 2 && list[1].sku == 'b' && readOnly.exists(l, l.qty == 2)"
            + " && immutable.map(l, l.price) == [1.0, 2.0]",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DictionariesOfAClass_AreMapsOfTheClass()
    {
        var byName = new Dictionary<string, Customer>
        {
            ["ann"] = new() { Email = "a@example.com" },
            ["bob"] = new() { Email = "b@example.com" },
        };
        var data = new { byName, readOnly = (IReadOnlyDictionary<string, Customer>)byName };

        Assert.True(CelExpression.Evaluate<bool>(
            "byName['ann'].email == 'a@example.com' && 'bob' in readOnly && readOnly.size() == 2",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ListOfStructures_IsAListOfTheStructure()
    {
        var data = new { points = new List<CallPoint> { new() { X = 1, Y = 2 }, new() { X = 3, Y = 4 } } };

        Assert.True(CelExpression.Evaluate<bool>(
            "points.exists(p, p.x == 3 && p.y == 4) && points[0].x + points[0].y == 3",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // VipContact derives from Contact: in a list(Contact) it is read by the members of Contact, and a member of VipContact alone is
    // not a field of the declared type, an error of checking.
    [Fact]
    public void DerivedClassInAList_IsReadByTheMembersOfTheDeclaredType()
    {
        var data = new { contacts = new List<Contact> { new VipContact { Name = "Vip", Level = 3 }, new() { Name = "Plain" } } };

        Assert.True(CelExpression.Evaluate<bool>(
            "contacts.map(c, c.name) == ['Vip', 'Plain']",
            data,
            cancellationToken: TestContext.Current.CancellationToken));

        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>(
            "contacts[0].level == 3",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CelErrorCode.NoSuchField, Assert.Single(exception.Errors).Code);
    }

    // The annotation of the element decides, as for a member of a context: C? gives null, C gives UnexpectedNull when the element
    // is read.
    [Fact]
    public void NullElement_IsNullOrUnexpectedNullByTheAnnotation()
    {
        var nullable = new { maybe = new List<Customer?> { null, new() { Email = "c@example.com" } } };
        Assert.True(CelExpression.Evaluate<bool>(
            "maybe[0] == null && maybe[1].email == 'c@example.com'",
            nullable,
            cancellationToken: TestContext.Current.CancellationToken));

        var strict = new { strict = new List<Customer> { null! } };
        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>(
            "strict[0].email == ''",
            strict,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CelErrorCode.UnexpectedNull, Assert.Single(exception.Errors).Code);
    }

    // ImmutableArray<C> in the state default has no value of its own: it is read as a null collection, UnexpectedNull.
    [Fact]
    public void DefaultImmutableArray_IsUnexpectedNull()
    {
        var data = new { lines = default(ImmutableArray<OrderLine>) };

        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>(
            "lines.size() == 0",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CelErrorCode.UnexpectedNull, Assert.Single(exception.Errors).Code);
    }

    // A named class as the data itself is still not read (GPCEL010 at build time): its advice is to pass it as a property.
#pragma warning disable GPCEL010 // the named class as the data is the subject of this row
    [Fact]
    public void NamedClassAsTheData_IsTypeNotDeclared()
    {
        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>(
            "total > 1.0",
            new Order(),
            cancellationToken: TestContext.Current.CancellationToken));

        CelError error = Assert.Single(exception.Errors);
        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Equal(typeof(Order).FullName, error.Subject);
    }
#pragma warning restore GPCEL010

    private static OrderLine Line(string sku, long qty, double price) => new() { Sku = sku, Qty = qty, Price = price };
}

// A structure of the application, read by its members.
internal struct CallPoint
{
    public long X { get; set; }

    public long Y { get; set; }
}

// An interface of the application and a class that implements it: the property of the interface type is read by the members of
// the interface.
internal interface IPriced
{
    double Price { get; }
}

internal sealed class PricedItem : IPriced
{
    public double Price { get; init; }
}
