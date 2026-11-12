using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Orders;

// The order of the allocation scenarios E1-E9 and the types that two contexts reach. The order is the one of the test that has
// no generator (`order` of the scenarios): the same fields, read here by the created code.
//
// Address and Contact are members of an order and of an invoice: ShopContext and BillingContext both list them, under one name
// and with the default options, so the forms of their two descriptions are equal and an environment takes them as one declaration
// whatever the order of the two AddTypes calls. Contact is a class that is not sealed (VipContact derives from it) and both
// contexts read a list of it, so the pair that each context makes for an element of a derived type names that context's own
// description of Contact: the one whose name is greater is dropped by the index and stays an alias of the other, and the lists of
// both contexts fit list(shop.Contact) in either order. TitleContext names Address another way, which is DuplicateType with
// ShopContext and with BillingContext in any order.

public sealed class OrderLine
{
    public string Sku { get; set; } = "";

    public long Qty { get; set; }

    public double Price { get; set; }
}

public sealed class Customer
{
    public string Email { get; set; } = "";
}

public sealed class Address
{
    public string City { get; set; } = "";

    public string Street { get; set; } = "";
}

public class Contact
{
    public string Name { get; set; } = "";
}

public sealed class VipContact : Contact
{
    public long Level { get; set; }
}

public sealed class Order
{
    public string Id { get; set; } = "";

    public string Status { get; set; } = "";

    public string Currency { get; set; } = "";

    public double Total { get; set; }

    public double? Discount { get; set; }

    public DateTimeOffset Created { get; set; }

    public OrderLine[] Lines { get; set; } = [];

    public Customer Customer { get; set; } = new();

    public string Policy { get; set; } = "";

    public Address Shipping { get; set; } = new();

    public List<Contact> Contacts { get; set; } = [];
}

public sealed class Invoice
{
    public string Number { get; set; } = "";

    public double Total { get; set; }

    public Address Billing { get; set; } = new();

    public List<Contact> Recipients { get; set; } = [];
}

[CelObject(typeof(Order), Name = "shop.Order")]
[CelObject(typeof(OrderLine), Name = "shop.OrderLine")]
[CelObject(typeof(Customer), Name = "shop.Customer")]
[CelObject(typeof(Address), Name = "shop.Address")]
[CelObject(typeof(Contact), Name = "shop.Contact")]
public sealed partial class ShopContext : CelTypeContext
{
}

[CelObject(typeof(Invoice), Name = "shop.Invoice")]
[CelObject(typeof(Address), Name = "shop.Address")]
[CelObject(typeof(Contact), Name = "shop.Contact")]
public sealed partial class BillingContext : CelTypeContext
{
}

[CelObject(typeof(Address), Name = "shop.Location")]
public sealed partial class TitleContext : CelTypeContext
{
}
