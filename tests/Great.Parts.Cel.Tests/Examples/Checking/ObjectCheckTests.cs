using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// Type checking with object types: a selection on an object type has the declared type of the field, and
// an unknown field is NoSuchField at the selection — the "." — or, for has(), at the "(" after its name.
// Object type names are identifiers of the type type(T); a name is tried as a variable first, then as an
// object type. An object literal names a type: a name that is not an object type is NotAType, and an object
// type of this part cannot be created by an expression (TypeNotConstructible); both at the "{" of the
// literal. deal is acme.crm.Deal with the fields amount (int), owner (acme.crm.Person) and extra (dyn);
// acme.crm.Person has the field name (string); x is int.
public sealed class ObjectCheckTests
{
    private const string NotConstructible = "type 'acme.crm.Deal' cannot be created by an expression: object literals are supported only for protobuf messages";

    private static readonly CelObjectType Deal = CelObjectTypeBuilder.ForDictionary("acme.crm.Deal")
        .AddField("amount", CelType.Int)
        .AddField("owner", CelType.ObjectNamed("acme.crm.Person"))
        .AddField("extra", CelType.Dyn)
        .Build();

    private static readonly CelObjectType Person = CelObjectTypeBuilder.ForDictionary("acme.crm.Person")
        .AddField("name", CelType.String)
        .Build();

    private readonly CelEnvironment environment = Builder().Build().GetEnvironmentOrThrow();

    private readonly CelEnvironment inContainer = Builder()
        .UseContainer("acme.crm")
        .AddVariable<string>("acme.crm.y")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-10")]
    public void SelectionOnAnObjectType_HasTheTypeOfTheField()
    {
        var cases = new (string Expression, CelType Type)[]
        {
            ("deal.amount", CelType.Int),
            ("deal.owner", CelType.ObjectNamed("acme.crm.Person")),
            ("deal.owner.name", CelType.String),
            ("deal.extra.f", CelType.Dyn),
            ("[deal][0].amount", CelType.Int),
            ("has(deal.owner)", CelType.Bool),
            ("acme.crm.Deal", CelType.TypeOf(CelType.ObjectNamed("acme.crm.Deal"))),
        };

        Assert.All(cases, item => Assert.Equal(item.Type, environment.Compile<CelValue>(item.Expression).GetProgramOrThrow().ResultType));
    }

    // type() of an object value is its object type; the type identifier resolves in the container. cel-go
    // v0.32.0 gives true for the same comparison of a protobuf message with its type name.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void TypeOfAnObject_IsItsObjectType()
    {
        var bindings = new CelBindings().Add("deal", CelValue.FromObject(Deal, new Dictionary<string, object?> { ["amount"] = 1L }));

        Assert.True(Evaluate(environment, "type(deal) == acme.crm.Deal && type(deal) != acme.crm.Person", bindings));
        Assert.True(Evaluate(inContainer, "type(deal) == Deal && Person != Deal", bindings));
    }

    // cel-go v0.32.0 reports "undefined field" at the same places.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("deal.missing", 4, "missing", "acme.crm.Deal")]
    [InlineData("has(deal.missing)", 3, "missing", "acme.crm.Deal")]
    [InlineData("deal.Amount", 4, "Amount", "acme.crm.Deal")]
    [InlineData("deal.owner.age", 10, "age", "acme.crm.Person")]
    [InlineData("[deal][0].nope", 9, "nope", "acme.crm.Deal")]
    public void UnknownFieldOfAnObjectType_IsNoSuchFieldAtTheSelection(string expression, int offset, string field, string type)
    {
        AssertSingleError(environment, expression, CelErrorCode.NoSuchField, offset, "undefined field '" + field + "' in type '" + type + "'");
    }

    // The name is written in full after it resolves in the container acme.crm. cel-go v0.32.0 refuses each
    // of these literals too, with other texts: an undeclared reference for a variable, "not a message type"
    // for int, and an unknown type for list when it creates the program.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("x{}", 1, "x")]
    [InlineData("y{}", 1, "acme.crm.y")]
    [InlineData("int{}", 3, "int")]
    [InlineData("list{}", 4, "list")]
    public void LiteralOfANameThatIsNotAnObjectType_IsNotATypeAtTheLiteral(string expression, int offset, string name)
    {
        AssertSingleError(inContainer, expression, CelErrorCode.NotAType, offset, "'" + name + "' is not a type");
    }

    // The field names of the literal are not looked up; its field values are checked as expressions.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("Deal{}", 4)]
    [InlineData("acme.crm.Deal{}", 13)]
    [InlineData("Deal{nope: 1}", 4)]
    [InlineData("Deal{amount: 'a'}", 4)]
    public void LiteralOfATypeDescribedAtRunTime_IsTypeNotConstructibleAtTheLiteral(string expression, int offset)
    {
        AssertSingleError(inContainer, expression, CelErrorCode.TypeNotConstructible, offset, NotConstructible);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void LiteralWithAFieldValueThatFails_GivesBothErrorsByOffset()
    {
        CelCompileResult<CelValue> result = inContainer.Compile<CelValue>("Deal{amount: 1 + 'a'}");

        Assert.Equal(
            new[]
            {
                (CelErrorCode.TypeNotConstructible, 4, NotConstructible),
                (CelErrorCode.NoMatchingOverload, 15, "found no matching overload for '_+_' applied to '(int, string)'"),
            },
            result.Errors.Select(error => (error.Code, error.Location.Offset, error.Message)));
    }

    // The same text as cel-go v0.32.0 gives.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void LiteralOfAnUndeclaredName_IsUndeclaredReferenceAtTheLiteral()
    {
        AssertSingleError(inContainer, "Nope{}", CelErrorCode.UndeclaredReference, 4, "undeclared reference to 'Nope' (in container 'acme.crm')");
    }

    // A variable may have the name of a type, and the variable wins, as in cel-go v0.32.0 (Env.LookupIdent).
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-8")]
    public void VariableWithTheNameOfAType_WinsOverTheType()
    {
        CelEnvironment shadowed = Builder().AddVariable<string>("acme.crm.Deal").Build().GetEnvironmentOrThrow();

        Assert.True(Evaluate(shadowed, "acme.crm.Deal == 'v' && type(acme.crm.Deal) == string", new CelBindings().Add("acme.crm.Deal", "v")));
    }

    [Fact]
    [Trait("Requirement", "+R-10")]
    [Trait("Requirement", "F-4")]
    public void ObjectTypeThatIsNotTheExpectedType_IsExpectedTypeMismatch()
    {
        CelError error = Assert.Single(environment.Compile<long>("deal.owner").Errors);

        Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check, 4), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal("expression of type 'acme.crm.Person' is not assignable to the expected type 'int'", error.Message);
    }

    private static CelEnvironmentBuilder Builder() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CelTypeProvider.Create("crm", Deal, Person))
        .AddVariable("deal", Deal.Type)
        .AddVariable<long>("x");

    private static bool Evaluate(CelEnvironment environment, string expression, CelBindings bindings) =>
        environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static void AssertSingleError(CelEnvironment environment, string expression, CelErrorCode code, int offset, string message)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal((code, CelErrorStage.Check, message), (error.Code, error.Stage, error.Message));
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }
}
