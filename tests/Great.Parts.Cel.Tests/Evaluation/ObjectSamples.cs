using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using static Great.Parts.Cel.Tests.Values.ObjectTypeTests;

namespace Great.Parts.Cel.Tests.Evaluation;

// An environment with object types of the application for the tests of field selection, has(), the place rule and the
// equality of objects: acme.crm.Contact, a type described at run time over a carrier class; acme.Order, a description
// of a .NET type; acme.crm.Broken, whose field returns a value of the wrong type; and the enumeration acme.crm.Status.
internal static class ObjectSamples
{
    internal static readonly CelObjectType ContactType = new CelObjectTypeBuilder<Contact>("acme.crm.Contact")
        .AddField("name", CelType.String, static contact => contact.Name is null ? CelValue.Null : CelValue.FromString(contact.Name))
        .AddField(
            "age",
            CelType.Int,
            static contact => contact.Throws ? throw new InvalidOperationException("age") : CelValue.FromInt64(contact.Age),
            static contact => contact.Age >= 0)
        .AddField(
            "manager",
            CelType.ObjectNamed("acme.crm.Contact"),
            static contact => contact.Manager is null ? CelValue.Null : CelValue.FromBareObject(contact.Manager),
            static _ => true)
        .AddField("score", CelType.Double, static contact => CelValue.FromDouble(contact.Score), static _ => true)
        .Build();

    // The same carrier class, another description: its values are never equal to contacts.
    internal static readonly CelObjectType LeadType = new CelObjectTypeBuilder<Contact>("acme.crm.Lead")
        .AddField("name", CelType.String, static contact => contact.Name is null ? CelValue.Null : CelValue.FromString(contact.Name))
        .Build();

    internal static readonly CelObjectType BrokenType = new CelObjectTypeBuilder<Contact>("acme.crm.Broken")
        .AddField("count", CelType.Int, static _ => CelValue.FromString("x"), static _ => true)
        .Build();

    internal static readonly OrderType OrderDescription = new(
        "acme.Order",
        [new(0, "total", CelType.Int), new(1, "rate", CelType.Double), new(2, "next", CelType.ObjectNamed("acme.Order"))]);

    internal static readonly CelEnumType Status = new("acme.crm.Status", [new("Active", 1), new("Closed", 2)]);

    internal static readonly CelEnvironment ObjectEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(new SampleProvider("crm", [ContactType, LeadType, BrokenType, OrderDescription], [Status]))
        .AddVariable("c", ContactType.Type)
        .AddVariable("other", ContactType.Type)
        .AddVariable("lead", LeadType.Type)
        .AddVariable("broken", BrokenType.Type)
        .AddVariable("o", OrderDescription.Type)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("e", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // Compiles and evaluates the expression with T = object on the bindings.
    internal static CelEvalResult<object> Evaluate(string expression, CelBindings bindings) =>
        ObjectEnvironment.Compile<object>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

    // The same with a canceled token.
    internal static CelEvalResult<object> EvaluateCanceled(string expression, CelBindings bindings) =>
        ObjectEnvironment.Compile<object>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: new CancellationToken(true));

    // The units that one evaluation charges, with a limit that is never reached.
    internal static ulong Cost(string expression, CelBindings bindings)
    {
        Plan plan = ObjectEnvironment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
        _ = plan.Run(frame);
        return frame.ChargedUnits;
    }

    // A contact bound as a bare carrier: the place rule pairs it at the first read of a variable of its type.
    internal static CelBindings Bind(params (string Name, object Value)[] values)
    {
        var bindings = new CelBindings();
        foreach ((string name, object value) in values)
        {
            _ = bindings.Add(name, value);
        }

        return bindings;
    }

    internal static IReadOnlyList<Contact> Two(string name) => [new Contact { Name = name, Age = 30 }, new Contact { Name = name, Age = 30 }];

    // The carrier class of the types described at run time.
    internal sealed class Contact
    {
        internal string? Name { get; init; }

        internal long Age { get; init; }

        internal Contact? Manager { get; init; }

        internal double Score { get; init; }

        internal bool Throws { get; init; }
    }

    // A type source with object and enumeration types.
    internal sealed class SampleProvider(string sourceName, CelObjectType[] objectTypes, CelEnumType[] enumTypes)
        : CelTypeProvider(sourceName, objectTypes, enumTypes);

    // A class that no type source describes.
    internal sealed class Unknown;
}
