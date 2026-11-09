using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Declarations.ApplicationTypeDeclarationTests;
using static Great.Parts.Cel.Tests.Values.ObjectTypeTests;

namespace Great.Parts.Cel.Tests.Declarations;

// A class, record, struct or interface of the application in AddFunctionImplementation<T1, ..., TResult>: Build resolves
// it by the type index to the object type that a type source describes for exactly that .NET type and compares that type
// with the declared overload; without a description it is TypeNotDeclared, never ImplementationSignatureMismatch.
public sealed class ApplicationTypeImplementationTests
{
    private static readonly CelTypeProvider Source = new ObjectSamples.SampleProvider("crm", [ObjectSamples.OrderDescription], []);

    private static readonly CelType OrderType = ObjectSamples.OrderDescription.Type;

    private static readonly CelObjectType InvoiceDescription = new SimpleType("acme.Invoice", typeof(Invoice));

    private static readonly CelObjectType PointDescription = new SimpleType("acme.Point", typeof(Point));

    private static readonly CelObjectType ShapeDescription = new SimpleType("acme.Shape", typeof(IShape));

    private static readonly CelTypeProvider KindsSource =
        new ObjectSamples.SampleProvider("kinds", [InvoiceDescription, PointDescription, ShapeDescription], []);

    private static CelType InvoiceType => InvoiceDescription.Type;

    private static CelType PointType => PointDescription.Type;

    private static CelType ShapeType => ShapeDescription.Type;

    [Fact]
    public void ParameterOfAClassWithADescription_IsComparedWithTheDeclaredObjectType()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Source)
            .AddVariable<Order>("o")
            .AddFunction(new CelFunctionDecl("big", CelOverloadDecl.Global("big_order", [OrderType], CelType.Bool)))
            .AddFunctionImplementation<Order, bool>("big_order", order => order.Total > 100)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(Evaluate<bool>(environment, "big(o)", new CelBindings().Add("o", new Order { Total = 500 })));
        Assert.False(Evaluate<bool>(environment, "big(o)", new CelBindings().Add("o", new Order { Total = 5 })));
    }

    [Fact]
    public void ResultOfAClassWithADescription_IsComparedWithTheDeclaredObjectType()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Source)
            .AddFunction(new CelFunctionDecl("order", CelOverloadDecl.Global("order_int", [CelType.Int], OrderType)))
            .AddFunctionImplementation<long, Order>("order_int", total => new Order { Total = total })
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(7L, Evaluate<long>(environment, "order(7).total", new CelBindings()));
    }

    [Fact]
    public void ParameterAndResultOfClasses_ArePassedThroughTheCall()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Source)
            .AddVariable<Order>("o")
            .AddFunction(new CelFunctionDecl("doubled", CelOverloadDecl.Global("doubled_order", [OrderType], OrderType)))
            .AddFunctionImplementation<Order, Order>("doubled_order", order => new Order { Total = order.Total * 2 })
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(14L, Evaluate<long>(environment, "doubled(o).total", new CelBindings().Add("o", new Order { Total = 7 })));
    }

    // A list of classes of the application is list(Class) as a result by the input rows, and fits a declared list(dyn).
    [Fact]
    public void ResultOfAListOfClasses_FitsTheDeclaredListOfDyn()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Source)
            .AddFunction(new CelFunctionDecl("orders", CelOverloadDecl.Global("orders_int", [CelType.Int], CelType.ListOf(CelType.Dyn))))
            .AddFunctionImplementation<long, IReadOnlyList<Order>>("orders_int", count => [new Order { Total = 1 }, new Order { Total = count }])
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(2L, Evaluate<long>(environment, "orders(9).size()", new CelBindings()));
        Assert.Equal(9L, Evaluate<long>(environment, "orders(9)[1].total", new CelBindings()));
    }

    // IReadOnlyList<Order>, Order[] and IList<Order> are list(Order) by the input rows: they build for list(Order) and for list(dyn).
    [Theory]
    [InlineData("list", "IReadOnlyList")]
    [InlineData("list", "array")]
    [InlineData("list", "IList")]
    [InlineData("dyn", "IReadOnlyList")]
    [InlineData("dyn", "array")]
    [InlineData("dyn", "IList")]
    public static void ResultOfAListOfClasses_BuildsForTheDeclaredListOfClassAndOfDyn(string declared, string shape)
    {
        CelType resultType = CelType.ListOf(declared == "list" ? OrderType : CelType.Dyn);
        CelEnvironmentBuilder builder = Builder()
            .AddTypes(Source)
            .AddFunction(new CelFunctionDecl("orders", CelOverloadDecl.Global("orders_int", [CelType.Int], resultType)));
        builder = shape switch
        {
            "IReadOnlyList" => builder.AddFunctionImplementation<long, IReadOnlyList<Order>>("orders_int", count => [new Order { Total = 1 }, new Order { Total = count }]),
            "array" => builder.AddFunctionImplementation<long, Order[]>("orders_int", count => [new Order { Total = 1 }, new Order { Total = count }]),
            _ => builder.AddFunctionImplementation<long, IList<Order>>("orders_int", count => [new Order { Total = 1 }, new Order { Total = count }]),
        };

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        Assert.Equal(2L, Evaluate<long>(environment, "orders(9).size()", new CelBindings()));
        Assert.Equal(9L, Evaluate<long>(environment, "orders(9)[1].total", new CelBindings()));
    }

    // The delegate forms infer list(Order) from the .NET result type.
    [Fact]
    public static void DelegateFormWithAListOfClasses_InfersTheListOfTheObjectType()
    {
        CelEnvironment environment = Builder()
            .AddTypes(Source)
            .AddFunction<long, IReadOnlyList<Order>>("orders", count => [new Order { Total = 1 }, new Order { Total = count }])
            .AddFunction<Order[]>("pair", () => [new Order { Total = 5 }])
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(CelType.ListOf(OrderType), Assert.Single(environment.Declarations.GetOverloads("orders", 1)).Declaration.ResultType);
        Assert.Equal(9L, Evaluate<long>(environment, "orders(9)[1].total", new CelBindings()));
        Assert.Equal(5L, Evaluate<long>(environment, "pair()[0].total", new CelBindings()));
    }

    // No description of the class: TypeNotDeclared, whatever the declared result type, and in the delegate forms too.
    [Fact]
    public static void ResultOfAListOfAClassThatNoSourceDescribes_IsTypeNotDeclared()
    {
        string name = typeof(ObjectSamples.Unknown).ToString();
        string message = "overload '{0}' has type '" + name + "', which no type source or extension of the environment declares";

        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, message, "make_int"),
            Builder()
                .AddTypes(Source)
                .AddFunction(new CelFunctionDecl("make", CelOverloadDecl.Global("make_int", [CelType.Int], CelType.ListOf(CelType.Dyn))))
                .AddFunctionImplementation<long, IReadOnlyList<ObjectSamples.Unknown>>("make_int", _ => []));
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, message, "make_int"),
            Builder()
                .AddFunction(new CelFunctionDecl("make", CelOverloadDecl.Global("make_int", [CelType.Int], CelType.ListOf(CelType.Dyn))))
                .AddFunctionImplementation<long, ObjectSamples.Unknown[]>("make_int", _ => []));
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, message, "make_int"),
            Builder()
                .AddTypes(Source)
                .AddFunction<long, IList<ObjectSamples.Unknown>>("make", _ => []));
    }

    // A list of classes in the result is list(Class) and fits list(dyn), no other list: list(int) and a list of another class
    // are mismatches.
    [Fact]
    public static void ResultOfAListOfClasses_FitsNoOtherDeclaredList()
    {
        foreach (CelType declared in new[] { CelType.ListOf(CelType.Int), CelType.ListOf(InvoiceType), CelType.Dyn })
        {
            CelBuildResult result = Builder()
                .AddTypes(Source)
                .AddTypes(KindsSource)
                .AddFunction(new CelFunctionDecl("orders", CelOverloadDecl.Global("orders_int", [CelType.Int], declared)))
                .AddFunctionImplementation<long, IReadOnlyList<Order>>("orders_int", _ => [])
                .Build();

            Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, Assert.Single(result.Errors).Code);
        }
    }

    // As a parameter a list of a class has no expected type: 4011, whatever the declared parameter type.
    [Theory]
    [InlineData("IReadOnlyList")]
    [InlineData("array")]
    [InlineData("IList")]
    public static void ParameterOfAListOfClasses_IsImplementationSignatureMismatch(string shape)
    {
        foreach (CelType declared in new[] { CelType.ListOf(CelType.Dyn), CelType.ListOf(OrderType) })
        {
            CelEnvironmentBuilder builder = Builder()
                .AddTypes(Source)
                .AddFunction(new CelFunctionDecl("count", CelOverloadDecl.Global("count_list", [declared], CelType.Int)));
            builder = shape switch
            {
                "IReadOnlyList" => builder.AddFunctionImplementation<IReadOnlyList<Order>, long>("count_list", list => list.Count),
                "array" => builder.AddFunctionImplementation<Order[], long>("count_list", list => list.Length),
                _ => builder.AddFunctionImplementation<IList<Order>, long>("count_list", list => list.Count),
            };

            Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, Assert.Single(builder.Build().Errors).Code);
        }
    }

    [Fact]
    public void RecordStructAndInterfaceWithDescriptions_AreComparedWithTheirObjectTypes()
    {
        CelEnvironment environment = Builder()
            .AddTypes(KindsSource)
            .AddVariable<Invoice>("i")
            .AddVariable<Point>("p")
            .AddFunction(new CelFunctionDecl("idOf", CelOverloadDecl.Global("idOf_invoice", [InvoiceType], CelType.Int)))
            .AddFunction(new CelFunctionDecl("idOf", CelOverloadDecl.Global("idOf_point", [PointType], CelType.Int)))
            .AddFunction(new CelFunctionDecl("idOf", CelOverloadDecl.Global("idOf_shape", [ShapeType], CelType.Int)))
            .AddFunctionImplementation<Invoice, long>("idOf_invoice", invoice => invoice.Id)
            .AddFunctionImplementation<Point, long>("idOf_point", _ => 2)
            .AddFunctionImplementation<IShape, long>("idOf_shape", _ => 3)
            .Build()
            .GetEnvironmentOrThrow();
        var bindings = new CelBindings().Add("i", new Invoice(5)).Add("p", default(Point));

        Assert.Equal(5L, Evaluate<long>(environment, "idOf(i)", bindings));
        Assert.Equal(2L, Evaluate<long>(environment, "idOf(p)", bindings));
        Assert.Equal(3, environment.Declarations.GetOverloads("idOf", 1).Length);
    }

    [Fact]
    public void ImplementationWithAClassThatNoSourceDescribes_IsTypeNotDeclaredNotAMismatch()
    {
        string name = typeof(ObjectSamples.Unknown).ToString();

        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "overload 'check_dyn' has type '" + name + "', which no type source or extension of the environment declares",
            Builder()
                .AddTypes(Source)
                .AddFunction(new CelFunctionDecl("check", CelOverloadDecl.Global("check_dyn", [CelType.Dyn], CelType.Bool)))
                .AddFunctionImplementation<ObjectSamples.Unknown, bool>("check_dyn", _ => true));
        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "overload 'make_int' has type '" + name + "', which no type source or extension of the environment declares",
            Builder()
                .AddFunction(new CelFunctionDecl("make", CelOverloadDecl.Global("make_int", [CelType.Int], CelType.Dyn)))
                .AddFunctionImplementation<long, ObjectSamples.Unknown>("make_int", _ => new ObjectSamples.Unknown()));
    }

    // One type is described and another is not: the error names the one that is not.
    [Fact]
    public void ImplementationWithADescribedAndAnUndescribedClass_NamesTheUndescribedOne()
    {
        string name = typeof(ObjectSamples.Unknown).ToString();

        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "overload 'pair_acme.Order' has type '" + name + "', which no type source or extension of the environment declares",
            Builder()
                .AddTypes(Source)
                .AddFunction(new CelFunctionDecl("pair", CelOverloadDecl.Global("pair_acme.Order", [OrderType], CelType.Dyn)))
                .AddFunctionImplementation<Order, ObjectSamples.Unknown>("pair_acme.Order", _ => new ObjectSamples.Unknown()));
    }

    // The carrier of a type described at run time is no exact .NET type of a description.
    [Fact]
    public void ImplementationWithTheCarrierOfATypeDescribedAtRunTime_IsTypeNotDeclared()
    {
        CelObjectType contactType = ObjectSamples.ContactType;

        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "overload 'name_crm.Contact' has type '" + typeof(ObjectSamples.Contact) + "', which no type source or extension of the environment declares",
            Builder()
                .AddTypes(new ObjectSamples.SampleProvider("crm", [contactType], []))
                .AddFunction(new CelFunctionDecl("name", CelOverloadDecl.Global("name_crm.Contact", [contactType.Type], CelType.String)))
                .AddFunctionImplementation<ObjectSamples.Contact, string>("name_crm.Contact", _ => string.Empty));
    }

    [Fact]
    public void ImplementationOfAnotherObjectTypeThanDeclared_IsImplementationSignatureMismatch()
    {
        CelBuildResult result = Builder()
            .AddTypes(KindsSource)
            .AddTypes(Source)
            .AddFunction(new CelFunctionDecl("big", CelOverloadDecl.Global("big_order", [OrderType], CelType.Bool)))
            .AddFunctionImplementation<Invoice, bool>("big_order", _ => true)
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, error.Code);
        Assert.StartsWith("implementation 'big_order' does not match the signature big(", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("dyn")]
    [InlineData("result")]
    public static void ImplementationOfAClassForAnotherDeclaredType_IsImplementationSignatureMismatch(string declared)
    {
        CelOverloadDecl overload = declared switch
        {
            "int" => CelOverloadDecl.Global("f_x", [CelType.Int], CelType.Bool),
            "dyn" => CelOverloadDecl.Global("f_x", [CelType.Dyn], CelType.Bool),
            _ => CelOverloadDecl.Global("f_x", [OrderType], CelType.Int),
        };

        CelBuildResult result = Builder()
            .AddTypes(Source)
            .AddFunction(new CelFunctionDecl("f", overload))
            .AddFunctionImplementation<Order, bool>("f_x", _ => true)
            .Build();

        Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, Assert.Single(result.Errors).Code);
    }

    // A list of classes is outside the expected-type table as a parameter: the other causes of the mismatch are as before.
    [Fact]
    public void ParameterOfAListOfClasses_IsStillImplementationSignatureMismatch()
    {
        CelBuildResult result = Builder()
            .AddTypes(Source)
            .AddFunction(new CelFunctionDecl("count", CelOverloadDecl.Global("count_list", [CelType.ListOf(CelType.Dyn)], CelType.Int)))
            .AddFunctionImplementation<IReadOnlyList<Order>, long>("count_list", list => list.Count)
            .Build();

        Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void ImplementationOfAClassWithAnotherParameterCount_IsImplementationSignatureMismatch()
    {
        CelBuildResult result = Builder()
            .AddTypes(Source)
            .AddFunction(new CelFunctionDecl("big", CelOverloadDecl.Global("big_order", [OrderType, CelType.Int], CelType.Bool)))
            .AddFunctionImplementation<Order, bool>("big_order", _ => true)
            .Build();

        Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void ImplementationOfAClassForNoDeclaredOverload_IsUnusedImplementationOnly()
    {
        CelBuildResult result = Builder()
            .AddTypes(Source)
            .AddFunctionImplementation<ObjectSamples.Unknown, bool>("nothing", _ => true)
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UnusedImplementation, error.Code);
    }

    [Fact]
    public void ResolutionDoesNotDependOnTheOrderOfTheCalls()
    {
        CelEnvironment environment = Builder()
            .AddFunctionImplementation<Order, bool>("big_order", order => order.Total > 100)
            .AddFunction(new CelFunctionDecl("big", CelOverloadDecl.Global("big_order", [OrderType], CelType.Bool)))
            .AddTypes(Source)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Single(environment.Declarations.GetOverloads("big", 1));
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    private static T Evaluate<T>(CelEnvironment environment, string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static void AssertSingle(CelErrorCode code, string message, CelEnvironmentBuilder builder)
    {
        CelError error = Assert.Single(builder.Build().Errors);

        Assert.Equal((code, message), (error.Code, error.Message));
    }

    // A description of a .NET type with one field, id.
    private sealed class SimpleType(string name, Type clrType) : CelObjectType(name, clrType, [new(0, "id", CelType.Int)])
    {
        protected internal override CelValue GetField(object instance, int number) => CelValue.FromInt64(1);

        protected internal override bool HasField(object instance, int number) => true;
    }
}
