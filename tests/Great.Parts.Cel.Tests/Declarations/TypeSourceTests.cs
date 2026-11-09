using System;
using System.Collections.Generic;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// The type sources of an environment: AddTypes as a set, DuplicateType with its two sources in a fixed order,
// TypeNotDeclared for the type of a field, the type index that Build creates, and the internal state of the builder
// that an environment description in JSON is compared by.
public sealed class TypeSourceTests
{
    [Fact]
    public void VariableOfAnObjectTypeOfASource_Builds()
    {
        CelObjectType deal = Dictionary("acme.Deal");

        CelBuildResult result = Builder().AddTypes(CelTypeProvider.Create("crm", deal)).AddVariable("deal", deal.Type).Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TwoDescriptionsOfOneNameInTwoSources_AreDuplicateTypeInTheOrderOfTheSourceTexts()
    {
        CelTypeProvider crm = CelTypeProvider.Create("crm", Dictionary("acme.Deal"));
        CelTypeProvider billing = CelTypeProvider.Create("billing", Dictionary("acme.Deal"));
        const string Message = "type 'acme.Deal' is declared by both type source 'billing' and type source 'crm'";

        AssertSingle(CelErrorCode.DuplicateType, Message, Builder().AddTypes(crm).AddTypes(billing));
        AssertSingle(CelErrorCode.DuplicateType, Message, Builder().AddTypes(billing).AddTypes(crm));
    }

    [Fact]
    public void TwoDescriptionsOfOneNameInOneSource_NameTheSourceTwice()
    {
        CelTypeProvider crm = CelTypeProvider.Create("crm", Dictionary("acme.Deal"), Dictionary("acme.Deal"));

        AssertSingle(CelErrorCode.DuplicateType, "type 'acme.Deal' is declared by both type source 'crm' and type source 'crm'", Builder().AddTypes(crm));
    }

    [Fact]
    public void EnumerationWithTheNameOfAnObjectType_IsDuplicateType()
    {
        CelTypeProvider crm = CelTypeProvider.Create("crm", Dictionary("acme.Status"));
        var enums = new EnumProvider("enums", new CelEnumType("acme.Status", [new("Open", 1)]));

        AssertSingle(
            CelErrorCode.DuplicateType,
            "type 'acme.Status' is declared by both type source 'crm' and type source 'enums'",
            Builder().AddTypes(enums).AddTypes(crm));
    }

    [Fact]
    public void SameDescriptionInTwoSources_IsOneDeclaration()
    {
        CelObjectType deal = Dictionary("acme.Deal");

        CelBuildResult result = Builder()
            .AddTypes(CelTypeProvider.Create("crm", deal))
            .AddTypes(CelTypeProvider.Create("billing", deal))
            .AddVariable("deal", deal.Type)
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("list")]
    [InlineData("google.protobuf.Timestamp")]
    public static void TypeNamedAsATypeIdentifierOfTheVersion_IsDuplicateTypeWithTheStandardLibraryFirst(string name)
    {
        AssertSingle(
            CelErrorCode.DuplicateType,
            "type '" + name + "' is declared by both standard library v1 and type source 'crm'",
            Builder().AddTypes(CelTypeProvider.Create("crm", Dictionary(name))));
    }

    [Fact]
    public void TwoDescriptionsOfOneDotNetType_AreDuplicateTypeNamedByTheDotNetType()
    {
        CelTypeProvider sales = CelTypeProvider.Create("sales", new OrderType("acme.Order"));
        CelTypeProvider archive = CelTypeProvider.Create("archive", new OrderType("archive.Order"));

        AssertSingle(
            CelErrorCode.DuplicateType,
            "type 'Great.Parts.Cel.Tests.Declarations.TypeSourceTests+Order' is declared by both type source 'archive' and type source 'sales'",
            Builder().AddTypes(sales).AddTypes(archive));
        AssertSingle(
            CelErrorCode.DuplicateType,
            "type 'Great.Parts.Cel.Tests.Declarations.TypeSourceTests+Order' is declared by both type source 'sales' and type source 'sales'",
            Builder().AddTypes(CelTypeProvider.Create("sales", new OrderType("acme.Order"), new OrderType("acme.OldOrder"))));
    }

    [Fact]
    public void FieldOfAnObjectTypeThatNoSourceDescribes_IsTypeNotDeclared()
    {
        CelObjectType deal = CelObjectTypeBuilder.ForDictionary("acme.Deal").AddField("owners", CelType.ListOf(CelType.ObjectNamed("acme.User"))).Build();

        AssertSingle(
            CelErrorCode.TypeNotDeclared,
            "field 'acme.Deal.owners' has type 'acme.User', which no type source or extension of the environment declares",
            Builder().AddTypes(CelTypeProvider.Create("crm", deal)));
    }

    [Fact]
    public void ObjectTypeInTheTypeOfAFieldOrAVariable_IsDeclaredByAnySource()
    {
        CelObjectType user = Dictionary("acme.User");
        CelObjectType deal = CelObjectTypeBuilder.ForDictionary("acme.Deal").AddField("owner", user.Type).Build();

        CelBuildResult result = Builder()
            .AddTypes(CelTypeProvider.Create("crm", deal))
            .AddTypes(CelTypeProvider.Create("users", user))
            .AddVariable("owners", CelType.ListOf(user.Type))
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Build_IndexesObjectTypesEnumerationConstantsAndDotNetTypes()
    {
        CelObjectType deal = Dictionary("acme.Deal");
        var order = new OrderType("acme.Order");
        var enums = new EnumProvider("enums", new CelEnumType("acme.Status", [new("Open", 1), new("Closed", 2)]));

        CelEnvironment environment = Builder()
            .AddTypes(CelTypeProvider.Create("crm", deal, order))
            .AddTypes(enums)
            .Build()
            .GetEnvironmentOrThrow();
        TypeIndex types = environment.Declarations.Types;

        Assert.True(types.TryGetObjectType("acme.Deal", out CelObjectType? found));
        Assert.Same(deal, found);
        Assert.True(types.TryGetEnumConstant("acme.Status.Closed", out long closed));
        Assert.Equal(2, closed);
        Assert.Same(order, types.Describe(new Order()));
        Assert.Null(types.Describe(new Dictionary<string, object?>()));
        Assert.True(environment.Declarations.TryGetTypeIdentifier("acme.Deal", out CelType? identifier));
        Assert.Equal(deal.Type, identifier);
        Assert.True(environment.Declarations.TryGetTypeIdentifier("int", out _));
    }

    [Fact]
    public void ProviderAddedTwice_IsOneSourceAndToBuilderKeepsTheSources()
    {
        CelObjectType deal = Dictionary("acme.Deal");
        CelTypeProvider crm = CelTypeProvider.Create("crm", deal);

        CelEnvironmentBuilder builder = Builder().AddTypes(crm).AddTypes(crm);
        CelEnvironmentBuilder rebuilt = builder.Build().GetEnvironmentOrThrow().ToBuilder();

        Assert.Same(crm, Assert.Single(builder.TypeProviders));
        Assert.Same(crm, Assert.Single(rebuilt.TypeProviders));
        Assert.True(rebuilt.AddVariable("deal", deal.Type).Build().IsSuccess);
        Assert.Throws<ArgumentNullException>(() => Builder().AddTypes(null!));
    }

    [Fact]
    public void SourceOfAnEnvironmentDescription_IsNamedByItsLabelAndGivesThePositionOfItsDeclaration()
    {
        CelObjectType fromCode = Dictionary("acme.Deal");
        CelObjectType fromJson = Dictionary("acme.Deal");
        var position = new CelSourceLocation("environment JSON 'm'", 40, 3, 7);
        var positions = new Dictionary<object, CelSourceLocation> { [fromJson] = position };

        CelBuildResult result = Builder()
            .AddTypes(CelTypeProvider.Create("crm", fromCode))
            .AddTypeSource(CelTypeProvider.Create("environment JSON 'm'", fromJson), "environment JSON 'm'", positions)
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal("type 'acme.Deal' is declared by both environment JSON 'm' and type source 'crm'", error.Message);
        Assert.Equal(position, error.SourceLocation);
    }

    [Fact]
    public void FieldOfAnEnvironmentDescription_GivesThePositionOfTypeNotDeclared()
    {
        CelObjectType deal = CelObjectTypeBuilder.ForDictionary("acme.Deal").AddField("owner", CelType.ObjectNamed("acme.User")).Build();
        var position = new CelSourceLocation("environment JSON", 12, 2, 5);

        CelBuildResult result = Builder()
            .AddTypeSource(
                CelTypeProvider.Create("environment JSON", deal),
                "environment JSON",
                new Dictionary<object, CelSourceLocation> { [deal.Fields[0]] = position })
            .Build();

        Assert.Equal(position, Assert.Single(result.Errors).SourceLocation);
    }

    [Fact]
    public void InternalState_HoldsFunctionsTypeSourcesAndTheContainer()
    {
        var function = new CelFunctionDecl("score", CelOverloadDecl.Global("score_string", [CelType.String], CelType.Int));

        CelEnvironmentBuilder builder = Builder().AddFunction(function).UseContainer("acme.sales");

        Assert.Same(function, Assert.Single(builder.FunctionDeclarations));
        Assert.Equal("acme.sales", builder.Container);
        Assert.Empty(builder.TypeProviders);
        Assert.Equal(string.Empty, Builder().Container);
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    private static CelObjectType Dictionary(string name) => CelObjectTypeBuilder.ForDictionary(name).AddField("title", CelType.String).Build();

    private static void AssertSingle(CelErrorCode code, string message, CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((code, message), (error.Code, error.Message));
        Assert.Equal(CelErrorStage.Build, error.Stage);
        Assert.Null(error.SourceLocation);
    }

    internal sealed class Order;

    // A description of a .NET type without fields.
    private sealed class OrderType(string name) : CelObjectType(name, typeof(Order), [])
    {
        protected internal override CelValue GetField(object instance, int number) => throw new InvalidOperationException();

        protected internal override bool HasField(object instance, int number) => throw new InvalidOperationException();
    }

    // A source of enumeration types.
    private sealed class EnumProvider(string sourceName, params CelEnumType[] enumTypes) : CelTypeProvider(sourceName, [], enumTypes);
}
