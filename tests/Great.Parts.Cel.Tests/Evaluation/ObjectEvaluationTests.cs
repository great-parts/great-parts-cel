using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.ObjectSamples;
using static Great.Parts.Cel.Tests.Values.ObjectTypeTests;

namespace Great.Parts.Cel.Tests.Evaluation;

// Evaluation of field selection and has() on objects of the application, the place rule of variables, the errors of
// reading fields with their texts and positions, the equality of objects, type() and enumeration constants, and the
// cost of reading application data (docs/design/evaluation.md, "Fields of objects", "Cost"; objects.md).
public sealed class ObjectEvaluationTests
{
    // A type whose field name throws when it is read: the field has no presence delegate, so the read is its presence
    // test.
    private static readonly CelObjectType ProbeType = new CelObjectTypeBuilder<Contact>("acme.crm.Probe")
        .AddField("name", CelType.String, static _ => throw new InvalidOperationException("name"))
        .Build();

    [Fact]
    public static void FieldOfABareCarrier_IsReadByTheBoundNumberAfterThePlaceRulePairsIt()
    {
        var contact = new Contact { Name = "Ann", Age = 41, Manager = new Contact { Name = "Bob" } };

        Assert.Equal("Ann", Evaluate("c.name", Bind(("c", contact))).GetValueOrThrow());
        Assert.Equal(41L, Evaluate("c.age", Bind(("c", contact))).GetValueOrThrow());
        Assert.Equal("Bob", Evaluate("c.manager.name", Bind(("c", contact))).GetValueOrThrow());
        Assert.Equal(true, Evaluate("has(c.name) && c.manager.manager == null", Bind(("c", contact))).GetValueOrThrow());
    }

    [Fact]
    public static void FieldOfADescribedDotNetType_IsReadFromABareInstanceOfItsClassOrASubclass()
    {
        var order = new SpecialOrder { Total = 7, Next = new Order { Total = 3 } };

        Assert.Equal(10L, Evaluate("o.total + o.next.total", Bind(("o", order))).GetValueOrThrow());
        Assert.Equal(false, Evaluate("has(o.next.next)", Bind(("o", order))).GetValueOrThrow());
    }

    [Fact]
    public static void FieldWithoutAValue_IsFieldNotSetAtTheSelection()
    {
        CelError error = SingleError(Evaluate("1 + c.name.size()", Bind(("c", new Contact()))));

        Assert.Equal((CelErrorCode.FieldNotSet, 5), (error.Code, error.Location.Offset));
        Assert.Equal("field 'name' of type 'acme.crm.Contact' has no value", error.Message);
    }

    [Fact]
    public static void FieldValueOfTheWrongType_IsFieldTypeMismatchAtTheSelection()
    {
        CelError error = SingleError(Evaluate("broken.count", Bind(("broken", new Contact()))));

        Assert.Equal((CelErrorCode.FieldTypeMismatch, 6), (error.Code, error.Location.Offset));
        Assert.Equal("field 'count' of type 'acme.crm.Broken' has a value of type 'string', expected 'int'", error.Message);
    }

    [Fact]
    public static void ExceptionOfAReadOrOfAPresenceTest_IsApplicationExceptionWithTheField()
    {
        CelError read = SingleError(Evaluate("c.age", Bind(("c", new Contact { Throws = true }))));
        CelError presence = SingleError(Evaluate("has(d.name)", Bind(("d", CelValue.FromObject(ProbeType, new Contact())))));

        Assert.Equal((CelErrorCode.ApplicationException, 1), (read.Code, read.Location.Offset));
        Assert.Equal("application code threw System.InvalidOperationException in field 'age' of type 'acme.crm.Contact'", read.Message);
        Assert.IsType<InvalidOperationException>(read.Exception);
        Assert.Equal(CelErrorCode.ApplicationException, presence.Code);
        Assert.Equal("application code threw System.InvalidOperationException in the presence test of field 'name' of type 'acme.crm.Probe'", presence.Message);
    }

    [Fact]
    public static void SelectionByNameOnADynValue_FindsTheFieldInTheDescriptionOfTheValue()
    {
        CelBindings bindings = Bind(("d", CelValue.FromObject(ContactType, new Contact { Name = "Ann" })));

        Assert.Equal("Ann", Evaluate("d.name", bindings).GetValueOrThrow());
        CelError missing = SingleError(Evaluate("d.missing", bindings));
        CelError presence = SingleError(Evaluate("has(d.missing)", bindings));

        Assert.Equal((CelErrorCode.EvalNoSuchField, 1), (missing.Code, missing.Location.Offset));
        Assert.Equal("no such field 'missing' in acme.crm.Contact", missing.Message);
        Assert.Equal("no such field 'missing' in acme.crm.Contact", presence.Message);
    }

    [Fact]
    public static void BoundNumberOfAnotherDescription_IsNotUsed_TheFieldIsFoundByName()
    {
        // The list joins a contact and a lead, so the static type is acme.crm.Contact while the value is a lead.
        CelBindings bindings = Bind(("c", new Contact()), ("e", CelValue.FromList(CelList.Create(CelValue.FromObject(LeadType, new Contact { Name = "Lee" })))));

        Assert.Equal("Lee", Evaluate("([c] + e)[1].name", bindings).GetValueOrThrow());
    }

    [Fact]
    public static void SelectionOnAValueThatIsNoMapAndNoObject_IsFieldSelectionNotSupported()
    {
        CelError error = SingleError(Evaluate("([c] + dyn([1]))[1].name", Bind(("c", new Contact()))));

        Assert.Equal((CelErrorCode.FieldSelectionNotSupported, 19), (error.Code, error.Location.Offset));
        Assert.Equal("value of type 'int' does not support field selection of 'name'", error.Message);
    }

    [Theory]
    [InlineData("d.name", 1)]
    [InlineData("has(d.name)", 3)]
    [InlineData("type(d)", 4)]
    [InlineData("d == e", 2)]
    [InlineData("d != e", 2)]
    [InlineData("d in [e]", 2)]
    public static void NodeThatNeedsTheDescriptionOfAnInstanceWithoutOne_IsTypeNotAccessibleThere(string expression, int offset)
    {
        CelError error = SingleError(Evaluate(expression, Bind(("d", new Unknown()), ("e", new Unknown()))));

        Assert.Equal((CelErrorCode.TypeNotAccessible, offset), (error.Code, error.Location.Offset));
        Assert.Equal(
            ".NET type 'Great.Parts.Cel.Tests.Evaluation.ObjectSamples+Unknown' is not available to expressions: no type source of the environment describes it",
            error.Message);
    }

    [Fact]
    public static void InstanceWithoutADescription_PassedOnOrComparedWithAnotherKind_IsNoError()
    {
        CelBindings bindings = Bind(("d", new Unknown()));

        Assert.Equal(false, Evaluate("d == 1", bindings).GetValueOrThrow());
        Assert.Equal(1L, Evaluate("size([d])", bindings).GetValueOrThrow());
    }

    [Fact]
    public static void VariableBoundToAValueThatDoesNotBelongToItsType_IsBindingTypeMismatch()
    {
        CelError text = SingleError(Evaluate("c", Bind(("c", "x"))));
        CelError unknown = SingleError(Evaluate("c", Bind(("c", new Unknown()))));
        CelError lead = SingleError(Evaluate("c", Bind(("c", CelValue.FromObject(LeadType, new Contact())))));

        Assert.Equal(CelErrorCode.BindingTypeMismatch, text.Code);
        Assert.Equal("value of variable 'c' has type 'string', which does not match the declared type 'acme.crm.Contact'", text.Message);
        Assert.Equal(
            "value of variable 'c' has type 'Great.Parts.Cel.Tests.Evaluation.ObjectSamples+Unknown', which does not match the declared type 'acme.crm.Contact'",
            unknown.Message);
        Assert.Equal("value of variable 'c' has type 'acme.crm.Lead', which does not match the declared type 'acme.crm.Contact'", lead.Message);
    }

    [Fact]
    public static void NullIsAValueOfAnObjectType_AndOfAnObjectTypedField()
    {
        Assert.Null(Evaluate("c", Bind(("c", CelValue.Null))).GetValueOrThrow());
        Assert.Equal(true, Evaluate("c.manager == null", Bind(("c", new Contact()))).GetValueOrThrow());
    }

    [Theory]
    [InlineData("c == other", true)]
    [InlineData("c != other", false)]
    [InlineData("[c] == [other]", true)]
    [InlineData("{'k': c} == {'k': other}", true)]
    [InlineData("c in [other]", true)]
    [InlineData("d == lead", false)]
    [InlineData("d == 1", false)]
    [InlineData("c == null", false)]
    public static void EqualityOfObjects_ComparesTheFieldsOfOneDescription(string expression, bool expected)
    {
        CelBindings bindings = Bind(
            ("c", new Contact { Name = "a", Age = 30 }),
            ("other", new Contact { Name = "a", Age = 30 }),
            ("lead", new Contact { Name = "a", Age = 30 }),
            ("d", CelValue.FromObject(ContactType, new Contact { Name = "a", Age = 30 })));

        Assert.Equal(expected, Evaluate(expression, bindings).GetValueOrThrow());
    }

    [Fact]
    public static void FieldPresentInOneInstanceOnly_OrDifferent_MakesThemUnequal_AndANaNFieldMakesAnInstanceUnequalToItself()
    {
        Assert.Equal(false, Evaluate("c == other", Bind(("c", new Contact { Name = "a" }), ("other", new Contact()))).GetValueOrThrow());
        Assert.Equal(false, Evaluate("c == other", Bind(("c", new Contact { Age = 1 }), ("other", new Contact { Age = 2 }))).GetValueOrThrow());
        Assert.Equal(false, Evaluate("c == c", Bind(("c", new Contact { Score = double.NaN }))).GetValueOrThrow());
        Assert.Equal(true, Evaluate("c == other", Bind(("c", new Contact { Age = -1 }), ("other", new Contact { Age = -2 }))).GetValueOrThrow());
    }

    [Fact]
    public static void ErrorAndExceptionOfAFieldReadInTheEquality_AreTheResultAtTheCall()
    {
        CelError mismatch = SingleError(Evaluate("broken == broken", Bind(("broken", new Contact()))));
        CelError thrown = SingleError(Evaluate("c == other", Bind(("c", new Contact { Throws = true }), ("other", new Contact()))));

        Assert.Equal((CelErrorCode.FieldTypeMismatch, 7), (mismatch.Code, mismatch.Location.Offset));
        Assert.Equal((CelErrorCode.ApplicationException, 2), (thrown.Code, thrown.Location.Offset));
        Assert.Equal("application code threw System.InvalidOperationException in field 'age' of type 'acme.crm.Contact'", thrown.Message);
        Assert.IsType<InvalidOperationException>(thrown.Exception);
    }

    [Fact]
    public static void TypeOfAnObject_IsItsObjectType_AndAnEnumerationConstantIsItsInt()
    {
        CelBindings bindings = Bind(("c", new Contact()), ("d", CelValue.FromObject(LeadType, new Contact())));

        Assert.Equal(true, Evaluate("type(c) == acme.crm.Contact && type(d) == acme.crm.Lead && type(d) != acme.crm.Contact", bindings).GetValueOrThrow());
        Assert.Equal(2L, Evaluate("acme.crm.Status.Closed", bindings).GetValueOrThrow());
    }

    [Fact]
    public static void ObjectInAMessageThatNamesTypes_IsNamedByItsDescription()
    {
        CelError overload = SingleError(Evaluate("d + 1", Bind(("d", CelValue.FromObject(LeadType, new Contact())))));
        CelError range = SingleError(Evaluate("d.all(x, true)", Bind(("d", new Unknown()))));

        Assert.Equal("no such overload: _+_(acme.crm.Lead, int)", overload.Message);
        Assert.Equal(
            "value of type 'Great.Parts.Cel.Tests.Evaluation.ObjectSamples+Unknown' cannot be the range of a comprehension (must be list or map)",
            range.Message);
    }

    [Theory]
    [InlineData("c.name", 2UL)]
    [InlineData("has(c.name)", 2UL)]
    [InlineData("d.name", 2UL)]
    [InlineData("acme.crm.Status.Active", 1UL)]
    [InlineData("acme.crm.Contact", 1UL)]
    [InlineData("d == lead", 3UL)]
    [InlineData("d == 1", 2UL)]

    // 2 reads; 1; name: 2 reads + 2 for two strings of 1; age, manager, score: 2 reads + 1 each.
    [InlineData("c == other", 16UL)]
    public static void CostOfReadingApplicationData_IsTheRowOfTheCostTable(string expression, ulong expected)
    {
        CelBindings bindings = Bind(
            ("c", new Contact { Name = "a", Age = 30 }),
            ("other", new Contact { Name = "a", Age = 30 }),
            ("lead", new Contact { Name = "a" }),
            ("d", CelValue.FromObject(ContactType, new Contact { Name = "a" })));

        Assert.Equal(expected, Cost(expression, bindings));
    }

    [Theory]
    [InlineData("c.name")]
    [InlineData("has(c.name)")]
    [InlineData("c == other")]
    public static void FieldRead_IsAnEventOfCancellation(string expression)
    {
        CelBindings bindings = Bind(("c", new Contact { Name = "a" }), ("other", new Contact { Name = "a" }));

        _ = Assert.ThrowsAny<OperationCanceledException>(() => EvaluateCanceled(expression, bindings));
        Assert.True(EvaluateCanceled("c == null", bindings).IsSuccess);
    }

    [Fact]
    public static void WithoutTypeChecking_ADeclaredObjectVariableAppliesThePlaceRule_AndFieldsAreFoundByName()
    {
        ParseResult parsed = ObjectEnvironment.Parse("c.name + '/' + d.manager.name");
        var contact = new Contact { Name = "Ann", Manager = new Contact { Name = "Bob" } };

        CelEvalResult<CelValue> result = ObjectEnvironment.EvaluateWithoutTypeCheck(parsed.Expression!, Bind(("c", contact), ("d", CelValue.FromObject(ContactType, contact))));

        Assert.Equal(CelValue.FromString("Ann/Bob"), result.GetValueOrThrow());
    }

    // A dictionary that carries a type described at run time is the value of the type, with or without type checking: the
    // variable of an object type applies the place rule to a value of the kind map too.
    [Theory]
    [InlineData("card.name")]
    [InlineData("has(card.name)")]
    [InlineData("card.name + card.other.name")]
    public static void WithoutTypeChecking_ADictionaryThatCarriesTheType_BehavesAsWithTypeChecking(string expression)
    {
        CelObjectType card = CelObjectTypeBuilder.ForDictionary("acme.crm.Card")
            .AddField("name", CelType.String)
            .AddField("other", CelType.ObjectNamed("acme.crm.Card"))
            .Build();
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(CelTypeProvider.Create("cards", card))
            .AddVariable("card", card.Type)
            .Build()
            .GetEnvironmentOrThrow();
        IReadOnlyDictionary<string, object?> value = new Dictionary<string, object?>
        {
            ["name"] = "Ann",
            ["other"] = new Dictionary<string, object?> { ["name"] = "Bob" },
        };
        var bindings = new CelBindings().Add<IReadOnlyDictionary<string, object?>>("card", value);

        CelEvalResult<CelValue> loose = environment.EvaluateWithoutTypeCheck(environment.Parse(expression).Expression!, bindings);
        CelEvalResult<CelValue> typed = environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(typed.IsSuccess);
        Assert.Equal(typed.GetValueOrThrow(), loose.GetValueOrThrow());
    }

    private static CelError SingleError<T>(CelEvalResult<T> result) => Assert.Single(result.Errors);
}
