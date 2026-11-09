using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// Type sources of an environment. AddTypes collects providers as a set, and Build merges their descriptions
// into the type index. Two descriptions that are not the same instance with one CEL name — in two sources or
// in one — and a name equal to a type identifier of the standard library are DuplicateType. A provider is
// the source "type source '<name>'"; of two sources the standard library comes first, otherwise the smaller
// text, ordinal, so the order of the calls changes nothing. An object type in the type of a field that no
// source describes is TypeNotDeclared for that field. Build errors are ordered by code, then by message.
public sealed class TypeSourceTests
{
    private static readonly string[] StandardTypeIdentifiers = ["int", "list", "null_type"];

    private static readonly string[] UndeclaredFieldMessages =
    [
        "field 'acme.crm.Deal.manager' has type 'acme.crm.Manager', which no type source or extension of the environment declares",
        "field 'acme.crm.Deal.owner' has type 'acme.crm.Person', which no type source or extension of the environment declares",
    ];

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-2")]
    public void TwoDescriptionsWithOneNameInTwoSources_AreDuplicateType()
    {
        CelTypeProvider crm = CelTypeProvider.Create("crm", Deal("acme.crm.Deal"));
        CelTypeProvider sales = CelTypeProvider.Create("sales", Deal("acme.crm.Deal"));
        const string Message = "type 'acme.crm.Deal' is declared by both type source 'crm' and type source 'sales'";

        AssertError(CelErrorCode.DuplicateType, Message, SingleError(Builder().AddTypes(crm).AddTypes(sales)));
        AssertError(CelErrorCode.DuplicateType, Message, SingleError(Builder().AddTypes(sales).AddTypes(crm)));
    }

    // Two Build calls of one builder give two descriptions, though their fields are the same.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-2")]
    public void TwoDescriptionsWithOneNameInOneSource_AreDuplicateType()
    {
        CelObjectTypeBuilder builder = CelObjectTypeBuilder.ForDictionary("acme.crm.Deal").AddField("amount", CelType.Int);

        CelError error = SingleError(Builder().AddTypes(CelTypeProvider.Create("crm", builder.Build(), builder.Build())));

        AssertError(CelErrorCode.DuplicateType, "type 'acme.crm.Deal' is declared by both type source 'crm' and type source 'crm'", error);
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-2")]
    public void NameOfAStandardTypeIdentifier_IsDuplicateType()
    {
        foreach (string name in StandardTypeIdentifiers)
        {
            CelError error = SingleError(Builder().AddTypes(CelTypeProvider.Create("crm", Deal(name))));

            AssertError(CelErrorCode.DuplicateType, "type '" + name + "' is declared by both standard library v1 and type source 'crm'", error);
        }
    }

    // The same description in two providers is one declaration, and the same provider added twice adds
    // nothing; a type of one source may name a type of another in a field.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-2")]
    public void SameDescriptionOrProviderTwice_IsOneDeclaration()
    {
        CelObjectType deal = Deal("acme.crm.Deal");
        CelObjectType person = CelObjectTypeBuilder.ForDictionary("acme.crm.Person")
            .AddField("deal", CelType.ObjectNamed("acme.crm.Deal"))
            .Build();
        CelTypeProvider crm = CelTypeProvider.Create("crm", deal);

        CelBuildResult result = Builder()
            .AddTypes(crm)
            .AddTypes(crm)
            .AddTypes(CelTypeProvider.Create("sales", deal, person))
            .AddVariable("deal", deal.Type)
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            CelType.TypeOf(CelType.ObjectNamed("acme.crm.Person")),
            result.GetEnvironmentOrThrow().Compile<CelValue>("acme.crm.Person").GetProgramOrThrow().ResultType);
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void ObjectTypeOfAFieldThatNoSourceDescribes_IsTypeNotDeclared()
    {
        CelObjectType deal = CelObjectTypeBuilder.ForDictionary("acme.crm.Deal")
            .AddField("owner", CelType.ObjectNamed("acme.crm.Person"))
            .AddField("manager", CelType.ObjectNamed("acme.crm.Manager"))
            .Build();

        CelBuildResult result = Builder().AddTypes(CelTypeProvider.Create("crm", deal)).Build();

        Assert.False(result.IsSuccess);
        Assert.Equal(UndeclaredFieldMessages, result.Errors.Select(error => error.Message));
        Assert.All(result.Errors, error => Assert.Equal((CelErrorCode.TypeNotDeclared, CelErrorStage.Build), (error.Code, error.Stage)));
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    public void BuildErrorsOfTypeSources_AreOrderedByCodeThenMessage()
    {
        CelObjectType withField = CelObjectTypeBuilder.ForDictionary("acme.crm.C")
            .AddField("owner", CelType.ObjectNamed("acme.crm.Person"))
            .Build();

        CelBuildResult result = Builder()
            .AddTypes(CelTypeProvider.Create("crm", withField, Deal("acme.crm.B"), Deal("acme.crm.A")))
            .AddTypes(CelTypeProvider.Create("sales", Deal("acme.crm.B"), Deal("acme.crm.A")))
            .Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.DuplicateType, "type 'acme.crm.A' is declared by both type source 'crm' and type source 'sales'"),
                (CelErrorCode.DuplicateType, "type 'acme.crm.B' is declared by both type source 'crm' and type source 'sales'"),
                (CelErrorCode.TypeNotDeclared, "field 'acme.crm.C.owner' has type 'acme.crm.Person', which no type source or extension of the environment declares"),
            },
            result.Errors.Select(error => (error.Code, error.Message)));
    }

    // A carrier class is never the exact .NET type of a description: a variable of a type described at run
    // time is declared with its CEL type.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void VariableOfACarrierClass_IsTypeNotDeclared()
    {
        CelObjectType deal = new CelObjectTypeBuilder<DealCarrier>("acme.crm.Deal")
            .AddField("amount", CelType.Int, carrier => CelValue.FromInt64(carrier.Amount))
            .Build();

        CelError error = SingleError(Builder().AddTypes(CelTypeProvider.Create("crm", deal)).AddVariable<DealCarrier>("deal"));

        AssertError(
            CelErrorCode.TypeNotDeclared,
            "variable 'deal' has type 'Great.Parts.Cel.Tests.Examples.Declarations.TypeSourceTests+DealCarrier', which no type source or extension of the environment declares",
            error);
        Assert.Equal(5L, Builder().AddTypes(CelTypeProvider.Create("crm", deal)).AddVariable("deal", deal.Type).Build().GetEnvironmentOrThrow()
            .Compile<long>("deal.amount").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("deal", new DealCarrier { Amount = 5 }), cancellationToken: CancellationToken.None)
            .GetValueOrThrow());
    }

    // ToBuilder keeps the type sources of the environment.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void ToBuilder_KeepsTheTypeSources()
    {
        CelObjectType deal = Deal("acme.crm.Deal");
        CelEnvironment environment = Builder().AddTypes(CelTypeProvider.Create("crm", deal)).Build().GetEnvironmentOrThrow();

        CelEnvironment extended = environment.ToBuilder().AddVariable("deal", deal.Type).Build().GetEnvironmentOrThrow();

        Assert.Equal(1L, extended.Compile<long>("deal.amount").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("deal", CelValue.FromObject(deal, new Dictionary<string, object?> { ["amount"] = 1L })), cancellationToken: CancellationToken.None)
            .GetValueOrThrow());
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    private static CelObjectType Deal(string name) => CelObjectTypeBuilder.ForDictionary(name).AddField("amount", CelType.Int).Build();

    private static CelError SingleError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Environment);
        return Assert.Single(result.Errors);
    }

    private static void AssertError(CelErrorCode code, string message, CelError error)
    {
        Assert.Equal((code, CelErrorStage.Build, message), (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
        Assert.Null(error.SourceLocation);
    }

    private sealed class DealCarrier
    {
        public long Amount { get; init; }
    }
}
