using System.Collections.Immutable;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.ObjectSamples;

namespace Great.Parts.Cel.Tests.Checking;

// Type checking of the fields of object types, of the identifiers of object types and enumeration constants, and of
// message literals (docs/design/checking.md, "Fields of objects", "Message literals").
public sealed class ObjectCheckingTests
{
    private static readonly CelEnvironment InContainer = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(new SampleProvider("crm", [ContactType], [Status]))
        .AddVariable("acme.crm.v", CelType.Int)
        .UseContainer("acme.crm")
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("c.name", "string")]
    [InlineData("c.manager", "acme.crm.Contact")]
    [InlineData("c.manager.age", "int")]
    [InlineData("has(c.manager.age)", "bool")]
    [InlineData("o.next.rate", "double")]
    [InlineData("acme.crm.Contact", "type(acme.crm.Contact)")]
    [InlineData("acme.crm.Status.Closed", "int")]
    [InlineData("d.anything", "dyn")]
    public static void FieldsOfObjectTypes_TypeIdentifiersAndConstants_HaveTheirTypes(string expression, string type)
    {
        CheckResult result = Check(ObjectEnvironment, expression);

        Assert.True(result.IsSuccess);
        Assert.Equal(type, result.Expression!.ResultType.ToString());
    }

    [Fact]
    public static void FieldSelection_RecordsTheDescriptionAndTheFieldNumber()
    {
        CheckedExpression expression = Check(ObjectEnvironment, "c.manager.age").Expression!;

        Reference age = expression.References[expression.Parsed.Root.Id];
        Assert.Same(ContactType, age.ObjectType);
        Assert.Equal(1, age.FieldNumber);
    }

    [Theory]
    [InlineData("c.missing", 1, "undefined field 'missing' in type 'acme.crm.Contact'")]
    [InlineData("has(c.manager.missing)", 3, "undefined field 'missing' in type 'acme.crm.Contact'")]
    [InlineData("o.next.missing", 6, "undefined field 'missing' in type 'acme.Order'")]
    public static void FieldThatTheObjectTypeDoesNotHave_IsNoSuchFieldAtTheSelection(string expression, int offset, string message)
    {
        CelError error = Assert.Single(Check(ObjectEnvironment, expression).Errors);

        Assert.Equal((CelErrorCode.NoSuchField, offset, message), (error.Code, error.Location.Offset, error.Message));
    }

    [Theory]
    [InlineData("Missing{}", 7, CelErrorCode.UndeclaredReference, "undeclared reference to 'Missing' (in container 'acme.crm')")]
    [InlineData("v{}", 1, CelErrorCode.NotAType, "'acme.crm.v' is not a type")]
    [InlineData("int{}", 3, CelErrorCode.NotAType, "'int' is not a type")]
    [InlineData("Status.Active{}", 13, CelErrorCode.NotAType, "'acme.crm.Status.Active' is not a type")]
    [InlineData(
        "Contact{}",
        7,
        CelErrorCode.TypeNotConstructible,
        "type 'acme.crm.Contact' cannot be created by an expression: object literals are supported only for protobuf messages")]
    public static void MessageLiteral_ResolvesItsNameByTheContainer_AndIsAnErrorAtTheBrace(string expression, int brace, CelErrorCode code, string message)
    {
        CelError error = Assert.Single(Check(InContainer, expression).Errors);

        Assert.Equal((code, brace, message), (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public static void MessageLiteral_ChecksItsFieldValues_ButNotItsFieldNames()
    {
        ImmutableArray<CelError> errors = Check(InContainer, "Contact{nothing: 1 + 'a'}").Errors;

        Assert.Equal(
            [(CelErrorCode.TypeNotConstructible, 7), (CelErrorCode.NoMatchingOverload, 19)],
            errors.Select(error => (error.Code, error.Location.Offset)).ToArray());
    }

    [Fact]
    public static void VariableWithTheNameOfAType_ComesBeforeTheType()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(new SampleProvider("crm", [ContactType], [Status]))
            .AddVariable("acme.crm.Status.Active", CelType.String)
            .AddVariable("acme.crm.Contact", CelType.Bool)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal("string", Check(environment, "acme.crm.Status.Active").Expression!.ResultType.ToString());
        Assert.Equal("bool", Check(environment, "acme.crm.Contact").Expression!.ResultType.ToString());
    }

    private static CheckResult Check(CelEnvironment environment, string expression)
    {
        ParseResult parsed = environment.Parse(expression);
        Assert.True(parsed.IsSuccess);
        return environment.Check(parsed.Expression);
    }
}
