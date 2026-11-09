using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The equality of objects: two object values of one description are equal when each field is present in
// both or in neither and the present values are equal in CEL; values of two descriptions, and an object and
// a value of another kind, are not equal. A NaN field makes an object unequal to itself: there is no
// shortcut for the same reference. cel-go v0.32.0 gives the same for protobuf messages (a probe with
// cel.expr.conformance.proto3.TestAllTypes): a message with a NaN field is unequal to itself, a field set in
// one message only makes them unequal, and a message is unequal to a map and to an int.
// a and b are acme.crm.Deal over dictionaries; x and y are dyn.
public sealed class ObjectEqualityTests
{
    private const string UnknownMessage =
        ".NET type 'Great.Parts.Cel.Tests.Examples.Values.ObjectEqualityTests+Unknown' is not available to expressions: no type source of the environment describes it";

    private static readonly CelObjectType Deal = CelObjectTypeBuilder.ForDictionary("acme.crm.Deal")
        .AddField("amount", CelType.Int)
        .AddField("rate", CelType.Double)
        .AddField("note", CelType.WrapperOf(CelType.String))
        .Build();

    private static readonly CelObjectType Person = CelObjectTypeBuilder.ForDictionary("acme.crm.Person")
        .AddField("amount", CelType.Int)
        .Build();

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CelTypeProvider.Create("crm", Deal, Person))
        .AddVariable("a", Deal.Type)
        .AddVariable("b", Deal.Type)
        .AddVariable("x", CelType.Dyn)
        .AddVariable("y", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-2")]
    [InlineData("a == b", true)]
    [InlineData("a != b", false)]
    [InlineData("a in [b]", true)]
    [InlineData("[a] == [b]", true)]
    [InlineData("{'k': a} == {'k': b}", true)]
    public void ObjectsWithEqualFields_AreEqual(string expression, bool expected)
    {
        CelBindings bindings = Bind(Entries(1, 0.5, "n"), Entries(1, 0.5, "n"));

        Assert.Equal(expected, Evaluate(expression, bindings).GetValueOrThrow());
    }

    // A field absent from both is skipped; a field present in one only, or a different value, makes the
    // objects unequal.
    [Fact]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-2")]
    public void ObjectsWithAFieldThatDiffers_AreNotEqual()
    {
        Assert.False(Evaluate("a == b", Bind(Entries(1, 0.5, "n"), Entries(2, 0.5, "n"))).GetValueOrThrow());
        Assert.False(Evaluate("a == b", Bind(Entries(1, 0.5, "n"), Entries(1, 0.5, null))).GetValueOrThrow());
        Assert.True(Evaluate("a == b", Bind(Entries(1, 0.5, null), Entries(1, 0.5, null))).GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-2")]
    public void ObjectWithANaNField_IsNotEqualToItself()
    {
        CelBindings bindings = Bind(Entries(1, double.NaN, null), Entries(1, double.NaN, null));

        Assert.False(Evaluate("a == a", bindings).GetValueOrThrow());
        Assert.False(Evaluate("a == b", bindings).GetValueOrThrow());
    }

    // Two descriptions are not equal without reading fields, even with equal entries; an object and a value
    // of another kind are not equal.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-2")]
    [InlineData("x == y")]
    [InlineData("x == 1")]
    [InlineData("x == {'amount': 1}")]
    [InlineData("x == null")]
    public void ObjectAndAValueOfAnotherTypeOrKind_AreNotEqual(string expression)
    {
        var entries = new Dictionary<string, object?> { ["amount"] = 1L };
        CelBindings bindings = new CelBindings()
            .Add("x", CelValue.FromObject(Deal, entries))
            .Add("y", CelValue.FromObject(Person, entries));

        Assert.False(Evaluate(expression, bindings).GetValueOrThrow());
    }

    // CelValue.CelEquals is the same equality for the application.
    [Fact]
    [Trait("Requirement", "F-1")]
    public void CelEquals_ComparesObjectsByTheirFields()
    {
        CelValue left = CelValue.FromObject(Deal, Entries(1, 0.5, "n"));

        Assert.True(CelValue.CelEquals(left, CelValue.FromObject(Deal, Entries(1, 0.5, "n"))).TryGetBoolean(out bool equal) && equal);
        Assert.True(CelValue.CelEquals(left, CelValue.FromObject(Deal, Entries(2, 0.5, "n"))).TryGetBoolean(out bool unequal) && !unequal);
    }

    // An instance that no type source describes has no description. Reading it, passing it on and comparing
    // it with a value of another kind need none; selecting a field, has(), type() and comparing it with an
    // object do, and give TypeNotAccessible at that node.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("x == x", 2)]
    [InlineData("x != x", 2)]
    [InlineData("x == y", 2)]
    [InlineData("x in [x]", 2)]
    [InlineData("[x] == [x]", 4)]
    [InlineData("type(x)", 4)]
    [InlineData("x.f", 1)]
    [InlineData("has(x.f)", 3)]
    public void ObjectWithoutADescription_IsTypeNotAccessibleWhereItsDescriptionIsNeeded(string expression, int offset)
    {
        CelBindings bindings = new CelBindings()
            .Add("x", new Unknown())
            .Add("y", CelValue.FromObject(Deal, Entries(1, 0.5, null)));

        CelError error = Assert.Single(environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.TypeNotAccessible, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal(UnknownMessage, error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("x == 1", false)]
    [InlineData("x != null", true)]
    [InlineData("size([x, x]) == 2", true)]
    [InlineData("dyn([x])[0] == 'a'", false)]
    public void ObjectWithoutADescription_ReadsAndPassesOn(string expression, bool expected)
    {
        Assert.Equal(expected, Evaluate(expression, new CelBindings().Add("x", new Unknown())).GetValueOrThrow());
    }

    private static Dictionary<string, object?> Entries(long amount, double rate, string? note)
    {
        var entries = new Dictionary<string, object?> { ["amount"] = amount, ["rate"] = rate };
        if (note is not null)
        {
            entries["note"] = note;
        }

        return entries;
    }

    private static CelBindings Bind(Dictionary<string, object?> a, Dictionary<string, object?> b) =>
        new CelBindings().Add("a", CelValue.FromObject(Deal, a)).Add("b", CelValue.FromObject(Deal, b));

    private CelEvalResult<bool> Evaluate(string expression, CelBindings bindings) =>
        environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

    private sealed class Unknown
    {
    }
}
