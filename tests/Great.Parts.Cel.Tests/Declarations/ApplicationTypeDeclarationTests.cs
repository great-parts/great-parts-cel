using System;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Values.ObjectTypeTests;

namespace Great.Parts.Cel.Tests.Declarations;

// A class, record, struct or interface of the application in AddVariable<T> and in the delegate forms of AddFunction: Build
// resolves it by the type index to the object type that a type source describes for exactly that .NET type; without one it
// is TypeNotDeclared with the .NET type, as before.
public sealed class ApplicationTypeDeclarationTests
{
    private static readonly CelTypeProvider Source = new ObjectSamples.SampleProvider("crm", [ObjectSamples.OrderDescription], []);

    private static readonly CelObjectType InvoiceDescription = new SimpleType("acme.Invoice", typeof(Invoice));

    private static readonly CelObjectType PointDescription = new SimpleType("acme.Point", typeof(Point));

    private static readonly CelObjectType ShapeDescription = new SimpleType("acme.Shape", typeof(IShape));

    private static readonly CelTypeProvider Kinds =
        new ObjectSamples.SampleProvider("kinds", [InvoiceDescription, PointDescription, ShapeDescription], []);

    [Fact]
    public void VariableOfAClassWithADescription_IsDeclaredWithItsObjectType()
    {
        CelEnvironment environment = Builder().AddTypes(Source).AddVariable<Order>("o").Build().GetEnvironmentOrThrow();

        Assert.True(environment.Declarations.TryGetVariable("o", out CelVariableDecl? variable));
        Assert.Equal(ObjectSamples.OrderDescription.Type, variable.Type);
        Assert.Equal(7L, Evaluate<long>(environment, "o.total", new CelBindings().Add("o", new Order { Total = 7 })));
        Assert.True(Evaluate<bool>(environment, "type(o) == acme.Order", new CelBindings().Add("o", new Order())));
    }

    [Fact]
    public void ResolutionDoesNotDependOnTheOrderOfTheCalls()
    {
        CelEnvironment before = Builder().AddVariable<Order>("o").AddTypes(Source).Build().GetEnvironmentOrThrow();
        CelEnvironment extended = Builder().AddTypes(Source).Build().GetEnvironmentOrThrow().ToBuilder().AddVariable<Order>("o").Build().GetEnvironmentOrThrow();

        Assert.True(before.Declarations.TryGetVariable("o", out CelVariableDecl? first));
        Assert.True(extended.Declarations.TryGetVariable("o", out CelVariableDecl? second));
        Assert.Equal(first.Type, second.Type);
    }

    [Fact]
    public void RecordStructAndInterfaceWithDescriptions_AreDeclaredWithTheirObjectTypes()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Kinds)
            .AddVariable<Invoice>("i")
            .AddVariable<Point>("p")
            .AddVariable<IShape>("s")
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(environment.Declarations.TryGetVariable("i", out CelVariableDecl? invoice));
        Assert.True(environment.Declarations.TryGetVariable("p", out CelVariableDecl? point));
        Assert.True(environment.Declarations.TryGetVariable("s", out CelVariableDecl? shape));
        Assert.Equal(InvoiceDescription.Type, invoice.Type);
        Assert.Equal(PointDescription.Type, point.Type);
        Assert.Equal(ShapeDescription.Type, shape.Type);
        Assert.Equal(1L, Evaluate<long>(environment, "i.id", new CelBindings().Add("i", new Invoice(5))));
        Assert.Equal(1L, Evaluate<long>(environment, "p.id", new CelBindings().Add("p", default(Point))));
    }

    [Fact]
    public void ClassWithoutADescription_IsTypeNotDeclaredNamingTheDotNetType()
    {
        string name = typeof(ObjectSamples.Unknown).ToString();

        AssertSingle(
            "variable 'u' has type '" + name + "', which no type source or extension of the environment declares",
            Builder().AddVariable<ObjectSamples.Unknown>("u"));
        AssertSingle(
            "variable 'u' has type '" + name + "', which no type source or extension of the environment declares",
            Builder().AddTypes(Source).AddVariable<ObjectSamples.Unknown>("u"));
    }

    // A type described at run time has no .NET type of its own in the index: its carrier class does not say which type it is.
    [Fact]
    public void CarrierOfATypeDescribedAtRunTime_IsNotDescribedByItsClass()
    {
        AssertSingle(
            "variable 'c' has type '" + typeof(ObjectSamples.Contact) + "', which no type source or extension of the environment declares",
            Builder().AddTypes(new ObjectSamples.SampleProvider("crm", [ObjectSamples.ContactType], [])).AddVariable<ObjectSamples.Contact>("c"));
    }

    [Fact]
    public void DelegateWithClassesOfTheApplication_IsAnOverloadOfTheirObjectTypes()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Source)
            .AddVariable<Order>("o")
            .AddFunction<Order, bool>("big", order => order.Total > 100)
            .AddFunction<long, Order>("order", total => new Order { Total = total })
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(Evaluate<bool>(environment, "big(o)", new CelBindings().Add("o", new Order { Total = 500 })));
        Assert.False(Evaluate<bool>(environment, "big(o)", new CelBindings().Add("o", new Order { Total = 5 })));
        Assert.Equal(7L, Evaluate<long>(environment, "order(7).total", new CelBindings()));
        Assert.True(Evaluate<bool>(environment, "big(order(900))", new CelBindings()));
    }

    [Fact]
    public void DelegateOverloadIdentifier_NamesTheObjectTypeLikeAnEnvironmentDescription()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Source)
            .AddFunction<Order, bool>("big", order => order.Total > 100)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Single(environment.Declarations.GetOverloads("big", 1), overload => overload.Declaration.OverloadId == "big_acme.Order");
    }

    [Fact]
    public void SameDelegateRegisteredTwice_IsOneImplementation()
    {
        Func<Order, bool> big = order => order.Total > 100;

        CelBuildResult result = Builder().AddTypes(Source).AddFunction("big", big).AddFunction("big", big).Build();

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void DelegateWithAClassThatNoSourceDescribes_IsTypeNotDeclaredAsBefore()
    {
        string name = typeof(ObjectSamples.Unknown).ToString();

        AssertSingle(
            "overload 'check_" + name + "' has type '" + name + "', which no type source or extension of the environment declares",
            Builder().AddFunction<ObjectSamples.Unknown, bool>("check", _ => true));
        AssertSingle(
            "overload 'make_int' has type '" + name + "', which no type source or extension of the environment declares",
            Builder().AddTypes(Source).AddFunction<long, ObjectSamples.Unknown>("make", _ => new ObjectSamples.Unknown()));
    }

    // One parameter is described and another is not: the error names the one that is not, and the identifier shows the
    // object type that the index gave to the first.
    [Fact]
    public void DelegateWithADescribedAndAnUndescribedClass_NamesTheUndescribedOne()
    {
        string name = typeof(ObjectSamples.Unknown).ToString();

        AssertSingle(
            "overload 'pair_acme.Order_" + name + "' has type '" + name + "', which no type source or extension of the environment declares",
            Builder().AddTypes(Source).AddFunction<Order, ObjectSamples.Unknown, bool>("pair", (_, _) => true));
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    private static T Evaluate<T>(CelEnvironment environment, string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static void AssertSingle(string message, CelEnvironmentBuilder builder)
    {
        CelError error = Assert.Single(builder.Build().Errors);

        Assert.Equal((CelErrorCode.TypeNotDeclared, message), (error.Code, error.Message));
    }

    // .NET types of the application of each kind that a type source can describe.
    internal sealed record Invoice(long Id);

    internal readonly struct Point;

    internal interface IShape;

    // A description of a .NET type with one field, id, which reads as 1.
    private sealed class SimpleType(string name, Type clrType) : CelObjectType(name, clrType, [new(0, "id", CelType.Int)])
    {
        protected internal override CelValue GetField(object instance, int number) => CelValue.FromInt64(1);

        protected internal override bool HasField(object instance, int number) => true;
    }
}
