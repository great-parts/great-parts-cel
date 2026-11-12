using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Generator.Samples.Protos;
using Great.Parts.Cel.Generator.Samples.Special;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Examples.Declarations;

// The order of the calls of the builder when the sources of types are contexts of the generator (the property of the order of the
// calls, written for them): the calls AddTypes with ShopContext, BillingContext, MessageContext and the provider of protobuf for the
// message of the samples, and the declaration of the variables, give the same environment in every order, with or without repeated calls —
// the same results of a fixed set of texts: compile results, values and errors. ShopContext and BillingContext both describe Address and
// Contact, a class that is not sealed, and each reads a list of Contact whose element of the derived class is paired with that context's
// own description, the one of the two that the index drops being an alias of the other: so the lists of both fit list(shop.Contact) and read
// the same in every order, and the shared types are one declaration in every order. TitleContext describes Address under another name, and
// the environment is DuplicateType, with the same errors in the same order, whichever call comes first; a message without its provider is
// TypeNotDeclared the same way. The three AddVariable calls are one step of the model, "the variables": their order among themselves
// changes no declaration, since their names differ, and the five steps make 120 orders that the test takes whole, which is a case of the
// property that the generator of calls of the property test draws at random.
[Trait("Requirement", "+R-8")]
[Trait("Requirement", "F-2")]
public sealed class GeneratedContextBuilderOrderTests
{
    private static readonly string[] Texts =
    [
        "order.shipping.city == 'Kazan' && invoice.billing.street == 'Lenina'",
        "type(order.shipping) == shop.Address && order.shipping == invoice.billing",
        "order.contacts.all(c, type(c) == shop.Contact) && invoice.recipients.all(c, type(c) == shop.Contact)",
        "order.contacts[1] == invoice.recipients[1] && order.contacts[0] != invoice.recipients[1]",
        "contacts[0] == contacts[1] && contacts[0].name == 'Ann'",
        "order.contacts[1].level == 2",
        "shipment.parcel.weight == 3 && shipment.parcels[0].label == 'x'",
        "type(shipment.parcel) == great_parts.samples.Parcel",
        "shipment.byLabel.map(k, k) == ['y', 'x'] && shipment.counts.map(k, k) == ['a', 'z']",
        "shipment.missing == 1",
    ];

    private static readonly CelProtobufTypes Provider = new CelProtobufTypesBuilder("messages")
        .AddFile(SamplesReflection.Descriptor)
        .Build();

    private static readonly Action<CelEnvironmentBuilder>[] Calls =
    [
        builder => builder.AddTypes(ShopContext.Default),
        builder => builder.AddTypes(BillingContext.Default),
        builder => builder.AddTypes(MessageContext.Default),
        builder => builder.AddTypes(Provider),
        builder => builder
            .AddVariable<Order>("order")
            .AddVariable<Invoice>("invoice")
            .AddVariable<Shipment>("shipment")
            .AddVariable("contacts", CelType.ListOf(CelType.ObjectNamed("shop.Contact"))),
        builder => builder.AddTypes(TitleContext.Default),
    ];

    private static readonly CelBindings Bindings = new CelBindings()
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
            })
        .Add("shipment", SampleShipment())
        .Add(
            "contacts",
            CelList.Create(
                CelValue.FromObject(ContactOf(ShopContext.Default), new VipContact { Name = "Ann" }),
                CelValue.FromObject(ContactOf(BillingContext.Default), new VipContact { Name = "Ann" })));

    // Every order of Shop, Billing, Message, the provider and the variables: the environment is built, and each text gives the same
    // result as in the first order.
    [Fact]
    public void EveryOrderOfTheCalls_GivesTheSameEnvironment()
    {
        int[] model = [0, 1, 2, 3, 4];
        string[] expected = Snapshot(model);
        Assert.Equal("built", expected[0]);
        Assert.Equal(Texts[0] + " => True", expected[1]);
        Assert.Equal(Texts[2] + " => True", expected[3]);
        Assert.Equal(Texts[4] + " => True", expected[5]);
        Assert.StartsWith(Texts[5] + " => compile 2004", expected[6], StringComparison.Ordinal);

        var different = new List<string>();
        foreach (int[] order in Permutations(model))
        {
            string[] actual = Snapshot(order);
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                different.Add(string.Join(' ', order) + ": " + string.Join(" | ", actual.Except(expected)));
            }
        }

        Assert.Empty(different);
    }

    // A call made twice, and a context or a provider that is added again, change nothing.
    [Fact]
    public void RepeatedCalls_GiveTheSameEnvironment()
    {
        string[] expected = Snapshot([0, 1, 2, 3, 4]);

        Assert.Equal(expected, Snapshot([0, 0, 1, 2, 3, 4]));
        Assert.Equal(expected, Snapshot([4, 3, 2, 1, 0, 0, 4, 1, 3]));
        Assert.Equal(expected, Snapshot([1, 0, 2, 4, 3, 0, 0, 2]));
    }

    // Two contexts that share a type (one source of it, BillingContext: the one with the smaller name) and a third that names it another
    // way: DuplicateType in every order of the three calls, naming BillingContext and TitleContext, with the same texts in the same order. The call of the variables is not among them: Shipment would be a message that nobody describes, and
    // Build would report 4008 as well.
    [Fact]
    public void ContextThatNamesASharedTypeAnotherWay_GivesTheSameBuildErrorsInEveryOrder()
    {
        string[] expected = Snapshot([0, 1, 5]);

        Assert.True(expected.Length > 0 && expected.All(line => line.StartsWith("4001 ", StringComparison.Ordinal)));
        Assert.Contains(expected, line => line.Contains("TitleContext", StringComparison.Ordinal) && line.Contains("BillingContext", StringComparison.Ordinal));
        foreach (int[] order in Permutations([0, 1, 5]))
        {
            Assert.Equal(expected, Snapshot(order));
        }
    }

    // The context of a message without the provider that describes it: TypeNotDeclared (4008) in every order, with the same texts in the
    // same order.
    [Fact]
    public void MessageContextWithoutItsProvider_GivesTheSameBuildErrorsInEveryOrder()
    {
        string[] expected = Snapshot([0, 1, 2, 4]);

        Assert.True(expected.Length > 0 && expected.All(line => line.StartsWith("4008 ", StringComparison.Ordinal)));
        foreach (int[] order in Permutations([0, 1, 2, 4]))
        {
            Assert.Equal(expected, Snapshot(order));
        }
    }

    // What the environment of the calls gives: the result of Build, then each text with its compile errors or its value or error.
    private static string[] Snapshot(int[] order)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        foreach (int index in order)
        {
            Calls[index](builder);
        }

        CelBuildResult result = builder.Build();
        if (!result.IsSuccess)
        {
            return [.. result.Errors.Select(error => (int)error.Code + " " + error.Message)];
        }

        CelEnvironment environment = result.GetEnvironmentOrThrow();
        var lines = new List<string> { "built" };
        foreach (string text in Texts)
        {
            CelCompileResult<bool> compiled = environment.Compile<bool>(text);
            if (!compiled.IsSuccess)
            {
                lines.Add(text + " => compile " + string.Join(", ", compiled.Errors.Select(Shown)));
                continue;
            }

            CelEvalResult<bool> evaluated = compiled.GetProgramOrThrow().Evaluate(Bindings, cancellationToken: CancellationToken.None);
            lines.Add(text + " => " + (evaluated.IsSuccess ? evaluated.GetValueOrThrow().ToString() : "eval " + string.Join(", ", evaluated.Errors.Select(Shown))));
        }

        return [.. lines];
    }

    private static string Shown(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)error.Code}@{error.Location.Offset} {error.Message}");

    private static CelObjectType ContactOf(CelTypeProvider provider) => provider.ObjectTypes.Single(type => type.Name == "shop.Contact");

    private static Shipment SampleShipment()
    {
        var shipment = new Shipment
        {
            Parcel = new Parcel { Weight = 3, Label = "a" },
            Parcels = [new Parcel { Weight = 5, Label = "x" }],
            ByLabel = new Dictionary<string, Parcel> { ["y"] = new Parcel { Weight = 1, Label = "p" }, ["x"] = new Parcel { Weight = 2, Label = "q" } },
        };
        shipment.Counts["z"] = 1;
        shipment.Counts["a"] = 2;
        return shipment;
    }

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (int index = 0; index < items.Length; index++)
        {
            int[] rest = [.. items.Where((_, position) => position != index)];
            foreach (int[] tail in Permutations(rest))
            {
                yield return [items[index], .. tail];
            }
        }
    }
}
