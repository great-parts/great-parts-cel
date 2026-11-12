using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Protos;
using Great.Parts.Cel.Generator.Samples.Special;
using Great.Parts.Cel.Protobuf;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// A message of protobuf as a member, as an element of a list and as a value of a dictionary (the sample class Shipment and the message
// Parcel of the proto file of the samples). The context names the message by the full name of its descriptor, great_parts.samples.Parcel,
// and reads the instance as it is; the description of the message comes from a provider of Great.Parts.Cel.Protobuf that the application
// adds with AddTypes, so the environment is the same in either order of the two calls. Without the provider Build is
// TypeNotDeclared (4008) for each field that names the message. The collections of protobuf are collections by the rows, and the
// order of a MapField is the ascending key.
public sealed class GeneratedMessageTests
{
    private const string ParcelName = "great_parts.samples.Parcel";

    private static readonly CelProtobufTypes Provider = new CelProtobufTypesBuilder("messages")
        .AddFile(SamplesReflection.Descriptor)
        .Build();

    public static TheoryData<bool> Orders => new() { true, false };

    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    [MemberData(nameof(Orders))]
    public void MessageMembers_AreReadByTheProviderInEitherOrderOfTheCalls(bool contextFirst)
    {
        CelEnvironment environment = Build(contextFirst);
        CelBindings bindings = new CelBindings().Add("s", Sample());

        Assert.True(Evaluate<bool>(environment, "s.parcel.weight == 3 && s.parcel.label == 'a'", bindings));
        Assert.True(Evaluate<bool>(environment, "type(s.parcel) == " + ParcelName, bindings));
        Assert.True(Evaluate<bool>(environment, "!has(s.noParcel) && s.noParcel == null", bindings));
        Assert.True(Evaluate<bool>(environment, "s.parcels.size() == 2 && s.parcels[1].label == 'y' && type(s.parcels[0]) == " + ParcelName, bindings));
        Assert.True(Evaluate<bool>(environment, "s.byLabel.x.weight == 5 && s.byLabel.map(k, k) == ['y', 'x'] && type(s.byLabel.y) == " + ParcelName, bindings));
    }

    // Two instances of the message with equal fields are equal, an element of a list and a member alike: the equality of messages is
    // the provider's, and a message is not equal to a value of another kind, which the check allows only through dyn.
    [Theory]
    [Trait("Requirement", "F-2")]
    [MemberData(nameof(Orders))]
    public void Messages_AreEqualByTheirFields(bool contextFirst)
    {
        CelEnvironment environment = Build(contextFirst);
        CelBindings bindings = new CelBindings().Add("s", Sample());

        Assert.True(Evaluate<bool>(environment, "s.parcels[0] == s.byLabel.y && s.parcels[0] != s.parcel && dyn(s.parcel) != 3", bindings));
    }

    // The collections of protobuf are collections by the rows: RepeatedField<T> is a list in the order of its elements, MapField<K,V> a
    // map whose order is the ascending key, where a Dictionary keeps the order of insertion.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    [MemberData(nameof(Orders))]
    public void CollectionsOfProtobuf_AreListsAndMapsWithTheKeysAscending(bool contextFirst)
    {
        CelEnvironment environment = Build(contextFirst);
        CelBindings bindings = new CelBindings().Add("s", Sample());

        Assert.True(Evaluate<bool>(environment, "s.tags == ['t', 'a'] && s.counts.map(k, k) == ['a', 'm', 'z'] && s.counts.z == 1", bindings));
    }

    // Without a provider that describes the message the context alone is TypeNotDeclared at Build, once for each field that names the
    // message, as it is for a type that no source describes; the context is not a description of the message and does not make one.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void WithoutTheProvider_BuildIsTypeNotDeclaredForEachMessageField()
    {
        CelBuildResult result = Builder(MessageContext.Default).Build();

        Assert.False(result.IsSuccess);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code));
        foreach (string field in new[] { "parcel", "noParcel", "parcels", "byLabel" })
        {
            Assert.Contains(result.Errors, error => error.Message.Contains("samples.Shipment." + field, StringComparison.Ordinal));
        }

        Assert.Equal(4, result.Errors.Count());
    }

    // The provider adds the description of the message once, whichever call comes first and however often it is made.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void ProviderAddedTwice_ChangesNothing()
    {
        CelEnvironment once = Build(true);
        CelEnvironment twice = Builder(MessageContext.Default, Provider, Provider).AddVariable<Shipment>("s").Build().GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("s", Sample());

        Assert.True(Evaluate<bool>(once, "s.parcel.weight == 3", bindings));
        Assert.True(Evaluate<bool>(twice, "s.parcel.weight == 3", bindings));
    }

    private static CelEnvironment Build(bool contextFirst) =>
        (contextFirst ? Builder(MessageContext.Default, Provider) : Builder(Provider, MessageContext.Default))
            .AddVariable<Shipment>("s")
            .Build()
            .GetEnvironmentOrThrow();

    private static Shipment Sample()
    {
        var shipment = new Shipment
        {
            Parcel = new Parcel { Weight = 3, Label = "a" },
            Parcels = [new Parcel { Weight = 5, Label = "x" }, new Parcel { Weight = 6, Label = "y" }],
            ByLabel = new Dictionary<string, Parcel>
            {
                ["y"] = new Parcel { Weight = 5, Label = "x" },
                ["x"] = new Parcel { Weight = 5, Label = "y" },
            },
        };
        shipment.Tags.Add("t");
        shipment.Tags.Add("a");
        shipment.Counts["z"] = 1;
        shipment.Counts["a"] = 2;
        shipment.Counts["m"] = 3;
        return shipment;
    }
}
