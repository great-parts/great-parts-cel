using System;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// Types that two generated contexts share ("Types that two generated contexts share" of the page of the objects). ShopContext and
// BillingContext both describe Address and Contact, under one name and with the default options, so the forms of their descriptions are
// equal and the environment takes each pair as one declaration, not DuplicateType, in either order of the two AddTypes calls. Contact is a
// class that is not sealed and both contexts read a list of it, so the pair that a context makes for an element of the derived class
// VipContact names that context's own description of Contact: the description of the source whose name is smaller stays in the index
// (BillingContext), the other (ShopContext) is dropped and stays an alias of it, and the lists of both fit list(shop.Contact) and read
// the same, whichever call comes first. TitleContext describes Address under another name, which is DuplicateType in every order.
public sealed class GeneratedSharedTypeTests
{
    public static TheoryData<bool> ShopFirst => new() { true, false };

    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-8")]
    [MemberData(nameof(ShopFirst))]
    public void TypeThatTwoContextsShare_IsOneDeclarationInEveryOrder(bool shopFirst)
    {
        CelEnvironment environment = Build(shopFirst);
        CelBindings bindings = Bindings();

        Assert.True(Evaluate<bool>(environment, "order.shipping.city == 'Kazan' && invoice.billing.street == 'Lenina'", bindings));
        Assert.True(Evaluate<bool>(environment, "type(order.shipping) == shop.Address && type(invoice.billing) == shop.Address", bindings));
        Assert.True(Evaluate<bool>(environment, "order.shipping == invoice.billing && !(order.shipping != invoice.billing)", bindings));
    }

    // The list of a class that is not sealed: the element of the derived class is paired with the description of its own context, and
    // that description is the one that stays in the index for one context and an alias of it for the other, so the elements are
    // shop.Contact in both lists, read the same, and equal across the contexts by their fields.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-8")]
    [MemberData(nameof(ShopFirst))]
    public void ListOfAnUnsealedClass_FitsListOfTheTypeWhicheverContextLoses(bool shopFirst)
    {
        CelEnvironment environment = Build(shopFirst);
        CelBindings bindings = Bindings();

        Assert.True(Evaluate<bool>(environment, "order.contacts.all(c, type(c) == shop.Contact) && invoice.recipients.all(c, type(c) == shop.Contact)", bindings));
        Assert.True(Evaluate<bool>(environment, "order.contacts[1].name == 'Ann' && invoice.recipients[1].name == 'Ann'", bindings));
        Assert.True(Evaluate<bool>(environment, "order.contacts[1] == invoice.recipients[1] && order.contacts[0] == invoice.recipients[0]", bindings));
        Assert.True(Evaluate<bool>(environment, "order.contacts[0] != invoice.recipients[1] && order.contacts[1] != invoice.recipients[0]", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "order.contacts[1].level").Code);
        Assert.Equal("list(shop.Contact)", environment.Compile<CelValue>("invoice.recipients").GetProgramOrThrow().ResultType.ToString());
    }

    // A list that the application builds itself and declares list(shop.Contact) may hold a pair made with the description of either
    // context: the one that was dropped is an alias of the one that stays, and fits.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-8")]
    [MemberData(nameof(ShopFirst))]
    public void PairOfEitherContextsDescription_FitsTheDeclaredList(bool shopFirst)
    {
        CelEnvironment environment = Build(shopFirst);
        var vip = new VipContact { Name = "Ann", Level = 1 };
        CelValue ofShop = CelValue.FromObject(Description(ShopContext.Default), vip);
        CelValue ofBilling = CelValue.FromObject(Description(BillingContext.Default), vip);
        CelBindings bindings = new CelBindings().Add("contacts", CelList.Create(ofShop, ofBilling));

        Assert.True(Evaluate<bool>(environment, "contacts.size() == 2 && contacts[0].name == 'Ann' && contacts[1].name == 'Ann'", bindings));
        Assert.True(Evaluate<bool>(environment, "contacts[0] == contacts[1] && type(contacts[0]) == shop.Contact && type(contacts[1]) == shop.Contact", bindings));
    }

    // Each context keeps its own description: the two are two instances of equal form, and the index decides which one answers.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void EachContext_KeepsItsOwnDescriptionOfTheSharedType()
    {
        CelObjectType shop = Description(ShopContext.Default);
        CelObjectType billing = Description(BillingContext.Default);

        Assert.NotSame(shop, billing);
        Assert.Equal(shop.Name, billing.Name);
        Assert.Equal(shop.ClrType, billing.ClrType);
        Assert.Equal(shop.Fields.Select(field => field.Name + ":" + field.Type), billing.Fields.Select(field => field.Name + ":" + field.Type));
    }

    // A context that describes the same .NET type under another name (shop.Location) is a description that differs: DuplicateType with
    // the two sources named, and the same errors in the same order whichever call comes first, as for any two descriptions.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-8")]
    public void ContextThatNamesTheTypeAnotherWay_IsDuplicateTypeInEveryOrder()
    {
        CelBuildResult[] results =
        [
            Builder(ShopContext.Default, TitleContext.Default).Build(),
            Builder(TitleContext.Default, ShopContext.Default).Build(),
            Builder(BillingContext.Default, TitleContext.Default).Build(),
            Builder(TitleContext.Default, BillingContext.Default).Build(),
        ];

        Assert.All(results, result => Assert.False(result.IsSuccess));
        Assert.All(results.SelectMany(result => result.Errors), error => Assert.Equal(CelErrorCode.DuplicateType, error.Code));
        Assert.Equal(Codes(results[0]), Codes(results[1]));
        Assert.Equal(Codes(results[2]), Codes(results[3]));
        Assert.Contains(results[0].Errors, error => error.Message.Contains("ShopContext", StringComparison.Ordinal) && error.Message.Contains("TitleContext", StringComparison.Ordinal));
        Assert.Contains(results[2].Errors, error => error.Message.Contains("BillingContext", StringComparison.Ordinal) && error.Message.Contains("TitleContext", StringComparison.Ordinal));
    }

    private static CelObjectType Description(CelTypeProvider provider) => provider.ObjectTypes.Single(type => type.Name == "shop.Contact");

    private static CelEnvironment Build(bool shopFirst) =>
        (shopFirst ? Builder(ShopContext.Default, BillingContext.Default) : Builder(BillingContext.Default, ShopContext.Default))
            .AddVariable<Order>("order")
            .AddVariable<Invoice>("invoice")
            .AddVariable("contacts", CelType.ListOf(CelType.ObjectNamed("shop.Contact")))
            .Build()
            .GetEnvironmentOrThrow();

    private static CelBindings Bindings() => new CelBindings()
        .Add(
            "order",
            new Order
            {
                Shipping = new Address { City = "Kazan", Street = "Lenina" },
                Contacts = [new Contact { Name = "Bob" }, new VipContact { Name = "Ann", Level = 2 }],
            })
        .Add(
            "invoice",
            new Invoice
            {
                Billing = new Address { City = "Kazan", Street = "Lenina" },
                Recipients = [new Contact { Name = "Bob" }, new VipContact { Name = "Ann", Level = 5 }],
            });
}
