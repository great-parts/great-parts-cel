using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Fields of types described at run time (the type mapping page, "Types described at run time", and the
// rows of has() and null for them): a selection reads a field, has() tests its presence. deal is
// acme.crm.Deal over the carrier class DealCarrier, whose fields read its properties through delegates; c
// is acme.crm.Contact over a dictionary, whose field f is the entry f converted as a .NET object; x is dyn.
// acme.crm.Person is another type over a dictionary. A field without a value is FieldNotSet, a value of
// another type than declared FieldTypeMismatch, both at the "." of the selection; the value of a field is
// checked at its top level only. The place of has() is the "(" after its name. cel-go has no types
// described at run time, so these examples do not rely on it.
public sealed class ObjectFieldTests
{
    private const string UnknownName = "Great.Parts.Cel.Tests.Examples.Evaluation.ObjectFieldTests+Unknown";

    private static readonly CelObjectType Deal = new CelObjectTypeBuilder<DealCarrier>("acme.crm.Deal")
        .AddField("amount", CelType.Int, carrier => CelValue.FromInt64(carrier.Amount))
        .AddField("title", CelType.String, carrier => carrier.Title is null ? CelValue.Null : CelValue.FromString(carrier.Title))
        .AddField(
            "note",
            CelType.WrapperOf(CelType.String),
            carrier => carrier.Note is null ? CelValue.Null : CelValue.FromString(carrier.Note),
            carrier => carrier.HasNote)
        .AddField("count", CelType.Int, carrier => carrier.Count, _ => true)
        .AddField("extra", CelType.Dyn, carrier => carrier.Extra, _ => true)
        .AddField("broken", CelType.Int, _ => throw new InvalidOperationException("broken"))
        .Build();

    private static readonly CelObjectType Contact = CelObjectTypeBuilder.ForDictionary("acme.crm.Contact")
        .AddField("name", CelType.String)
        .AddField("phone", CelType.WrapperOf(CelType.String))
        .AddField("age", CelType.Int)
        .AddField("manager", CelType.ObjectNamed("acme.crm.Contact"))
        .AddField("tags", CelType.ListOf(CelType.String))
        .AddField("info", CelType.Dyn)
        .Build();

    private static readonly CelObjectType Person = CelObjectTypeBuilder.ForDictionary("acme.crm.Person")
        .AddField("name", CelType.String)
        .Build();

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CelTypeProvider.Create("crm", Deal, Contact, Person))
        .AddVariable("deal", Deal.Type)
        .AddVariable("c", Contact.Type)
        .AddVariable("x", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // A carrier bound as it is takes its description from the declared type of the variable; a pair created
    // by FromObject is the same value.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void Selection_ReadsTheFieldOfACarrier()
    {
        var carrier = new DealCarrier { Amount = 5, Title = "Big", HasNote = true, Count = CelValue.FromInt64(2) };

        foreach (CelBindings bindings in new[] { new CelBindings().Add("deal", carrier), new CelBindings().Add("deal", CelValue.FromObject(Deal, carrier)) })
        {
            Assert.Equal(5L, Evaluate<long>("deal.amount", bindings));
            Assert.Equal("Big", Evaluate<string>("deal.title", bindings));
            Assert.True(Evaluate<bool>("deal.count == 2 && has(deal.amount) && has(deal.title)", bindings));
            Assert.True(Evaluate<bool>("type(deal) == acme.crm.Deal", bindings));
        }
    }

    // wrapper(P), an object type and dyn take null as the value of a present field; has() is the presence:
    // the has delegate, or without one a read that is not null.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void PresentFieldOfAWrapperOrDynType_TakesNull()
    {
        var bindings = new CelBindings().Add("deal", new DealCarrier { HasNote = true, Extra = CelValue.Null });

        Assert.True(Evaluate<bool>("has(deal.note) && deal.note == null", bindings));
        Assert.True(Evaluate<bool>("has(deal.extra) && deal.extra == null", bindings));
    }

    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("deal.title", "title")]
    [InlineData("deal.note", "note")]
    public void FieldWithoutAValue_IsFieldNotSetAtTheSelection(string expression, string field)
    {
        var bindings = new CelBindings().Add("deal", new DealCarrier { Title = null, HasNote = false });

        AssertError(SingleError(expression, bindings), CelErrorCode.FieldNotSet, 4, "field '" + field + "' of type 'acme.crm.Deal' has no value");
        Assert.False(Evaluate<bool>("has(" + expression + ")", bindings));
    }

    // A present field of type int takes no null and no value of another kind.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void FieldValueOfAnotherKind_IsFieldTypeMismatchAtTheSelection()
    {
        foreach ((CelValue value, string actual) in new[] { (CelValue.Null, "null_type"), (CelValue.FromString("2"), "string"), (CelValue.FromUInt64(2), "uint") })
        {
            CelError error = SingleError("deal.count", new CelBindings().Add("deal", new DealCarrier { Count = value }));

            AssertError(
                error,
                CelErrorCode.FieldTypeMismatch,
                4,
                "field 'count' of type 'acme.crm.Deal' has a value of type '" + actual + "', expected 'int'");
        }
    }

    // An exception of a read delegate becomes ApplicationException with the original exception; for has()
    // without a has delegate the read is the presence test.
    [Fact]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    public void ExceptionOfARead_IsApplicationException()
    {
        var bindings = new CelBindings().Add("deal", new DealCarrier());

        CelError read = SingleError("deal.broken", bindings);
        CelError presence = SingleError("has(deal.broken)", bindings);

        AssertError(read, CelErrorCode.ApplicationException, 4, "application code threw System.InvalidOperationException in field 'broken' of type 'acme.crm.Deal'");
        AssertError(
            presence,
            CelErrorCode.ApplicationException,
            3,
            "application code threw System.InvalidOperationException in the presence test of field 'broken' of type 'acme.crm.Deal'");
        Assert.IsType<InvalidOperationException>(read.Exception);
        Assert.IsType<InvalidOperationException>(presence.Exception);
    }

    // An entry converts as a .NET object: a long or an int is an int, a string a string, null the null value.
    // The field is present when the key is.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-7")]
    public void FieldOfADictionary_IsTheEntryWithItsName()
    {
        CelBindings bindings = ContactBindings(new Dictionary<string, object?> { ["name"] = "Ann", ["age"] = 41, ["phone"] = null });

        Assert.Equal("Ann", Evaluate<string>("c.name", bindings));
        Assert.Equal(41L, Evaluate<long>("c.age", bindings));
        Assert.True(Evaluate<bool>("has(c.phone) && c.phone == null && !has(c.tags)", bindings));
        AssertError(SingleError("c.tags", bindings), CelErrorCode.FieldNotSet, 1, "field 'tags' of type 'acme.crm.Contact' has no value");
    }

    // A .NET collection does not convert in a field that is not of an object type with a dictionary
    // carrier: its type is written as its .NET type.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void EntryOfAnotherType_IsFieldTypeMismatchAtTheSelection()
    {
        var cases = new (string Field, object? Value, string Actual, string Declared)[]
        {
            ("age", null, "null_type", "int"),
            ("age", "41", "string", "int"),
            ("age", 41.0, "double", "int"),
            ("name", null, "null_type", "string"),
            ("manager", "Bob", "string", "acme.crm.Contact"),
            ("tags", new List<string> { "a" }, "System.Collections.Generic.List`1[System.String]", "list(string)"),
            ("tags", new Dictionary<string, object?>(), "System.Collections.Generic.Dictionary`2[System.String,System.Object]", "list(string)"),
            ("info", new List<long> { 1 }, "System.Collections.Generic.List`1[System.Int64]", "dyn"),
            ("manager", new Dictionary<string, string>(), "System.Collections.Generic.Dictionary`2[System.String,System.String]", "acme.crm.Contact"),
        };

        foreach ((string field, object? value, string actual, string declared) in cases)
        {
            CelError error = SingleError("c." + field, ContactBindings(new Dictionary<string, object?> { [field] = value }));

            AssertError(
                error,
                CelErrorCode.FieldTypeMismatch,
                1,
                "field '" + field + "' of type 'acme.crm.Contact' has a value of type '" + actual + "', expected '" + declared + "'");
        }
    }

    // A dictionary in a field of an object type with a dictionary carrier is a carrier of that type; null is
    // a value of an object type.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void DictionaryInAFieldOfAnObjectType_IsACarrierOfThatType()
    {
        var manager = new Dictionary<string, object?> { ["name"] = "Bob" };

        CelBindings bindings = ContactBindings(new Dictionary<string, object?> { ["manager"] = manager });
        CelBindings withNull = ContactBindings(new Dictionary<string, object?> { ["manager"] = null });

        Assert.Equal("Bob", Evaluate<string>("c.manager.name", bindings));
        Assert.True(Evaluate<bool>("type(c.manager) == acme.crm.Contact", bindings));
        Assert.True(Evaluate<bool>("has(c.manager) && c.manager == null", withNull));
    }

    // The value of a field is checked at its top level only: a list of ints in a field of type list(string)
    // is read; its elements are checked where an operation needs their kind.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-3")]
    public void FieldValue_IsCheckedAtTheTopLevelOnly()
    {
        CelBindings bindings = ContactBindings(new Dictionary<string, object?> { ["tags"] = CelList.Create(CelValue.FromInt64(1)) });

        Assert.True(Evaluate<bool>("size(c.tags) == 1 && c.tags[0] != 'a'", bindings));
    }

    // An instance that no type source describes reads and passes on without its description; only a node
    // that needs the description fails.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void ObjectWithoutADescriptionInADynField_IsTypeNotAccessibleWhereItsDescriptionIsNeeded()
    {
        CelBindings bindings = ContactBindings(new Dictionary<string, object?> { ["info"] = new Unknown() });

        Assert.True(Evaluate<bool>("has(c.info) && c.info != null", bindings));
        AssertError(
            SingleError("c.info.x", bindings),
            CelErrorCode.TypeNotAccessible,
            6,
            ".NET type '" + UnknownName + "' is not available to expressions: no type source of the environment describes it");
    }

    // In a dyn place a carrier is not described by its class — a carrier class is never in the type index —
    // so it needs FromObject there.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void CarrierInADynPlace_NeedsFromObject()
    {
        var carrier = new DealCarrier { Amount = 5 };
        const string Message = ".NET type 'Great.Parts.Cel.Tests.Examples.Evaluation.ObjectFieldTests+DealCarrier' is not available to expressions: no type source of the environment describes it";

        AssertError(SingleError("x.amount", new CelBindings().Add("x", carrier)), CelErrorCode.TypeNotAccessible, 1, Message);
        AssertError(SingleError("type(x)", new CelBindings().Add("x", carrier)), CelErrorCode.TypeNotAccessible, 4, Message);
        Assert.True(Evaluate<bool>("x.amount == 5 && type(x) == acme.crm.Deal", new CelBindings().Add("x", CelValue.FromObject(Deal, carrier))));
    }

    // A selection on a value that is neither a map nor an object — a dyn field that holds an int or a list, or
    // the element of a list of the static type list(acme.crm.Deal) that holds an int — does not support field
    // selection; has() fails the same way.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("c.info.f", 6, "int", "f")]
    [InlineData("has(c.info.f)", 3, "int", "f")]
    [InlineData("deal.extra.f", 10, "list", "f")]
    [InlineData("([deal] + dyn([1]))[1].amount", 22, "int", "amount")]
    public void SelectionOnAFieldValueThatIsNotAMapOrAnObject_IsFieldSelectionNotSupported(string expression, int offset, string type, string field)
    {
        CelBindings bindings = ContactBindings(new Dictionary<string, object?> { ["info"] = 1L })
            .Add("deal", new DealCarrier { Extra = CelValue.FromList(CelList.Create(CelValue.FromInt64(1))) });

        AssertError(
            SingleError(expression, bindings),
            CelErrorCode.FieldSelectionNotSupported,
            offset,
            "value of type '" + type + "' does not support field selection of '" + field + "'");
    }

    // Through dyn a field is found by its name at evaluation; no such field is EvalNoSuchField with the full
    // name of the type, for has() too, as cel-go v0.32.0 gives for a message ("no such field").
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("dyn(c).missing", 6)]
    [InlineData("has(dyn(c).missing)", 3)]
    public void SelectionOfAnUndescribedFieldThroughDyn_IsEvalNoSuchField(string expression, int offset)
    {
        CelBindings bindings = ContactBindings(new Dictionary<string, object?> { ["name"] = "Ann" });

        Assert.Equal("Ann", Evaluate<string>("dyn(c).name", bindings));
        AssertError(SingleError(expression, bindings), CelErrorCode.EvalNoSuchField, offset, "no such field 'missing' in acme.crm.Contact");
    }

    // A dictionary bound to a variable of a type with a dictionary carrier — by the static type
    // IReadOnlyDictionary<string, object?> or IDictionary<string, object?>, or as object — is a carrier of that
    // type: the place rule looks through the view that the input rows made to its source, an instance of the
    // carrier class. The application needs no description at hand, as for a type that it knows only by its
    // name in the JSON description of the environment.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-7")]
    public void DictionaryBoundToAVariableOfItsType_IsACarrierOfThatType()
    {
        var dictionary = new Dictionary<string, object?> { ["name"] = "Ann" };
        var bindings = new[]
        {
            new CelBindings().Add<IReadOnlyDictionary<string, object?>>("c", dictionary),
            new CelBindings().Add<IDictionary<string, object?>>("c", dictionary),
            new CelBindings().Add<object>("c", dictionary),
        };

        foreach (CelBindings binding in bindings)
        {
            Assert.Equal("Ann", Evaluate<string>("c.name", binding));
            Assert.True(Evaluate<bool>("type(c) == acme.crm.Contact && has(c.name) && !has(c.age)", binding));
        }
    }

    // A binding must belong to the declared object type: a carrier of another class, an instance without a
    // description, a dictionary of another type bound by its static type and a view that the application
    // created (both a map: the place rule looks only through a view of the input rows whose source is an
    // instance of the carrier class), a pair of another type, and a pair of another description with the same
    // name. The actual type is the name of the description of the value, or the .NET type of an instance
    // without one; never TypeNotAccessible.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void BindingThatDoesNotBelongToTheObjectType_IsBindingTypeMismatch()
    {
        CelObjectType sameName = CelObjectTypeBuilder.ForDictionary("acme.crm.Contact").AddField("name", CelType.String).Build();
        var dictionary = new Dictionary<string, object?> { ["name"] = "Ann" };
        CelMap applicationView = CelMap.CreateView<string, object?>(dictionary, value => CelValue.FromString((string)value!), keepSourceOrder: true);
        var cases = new (string Expression, CelBindings Bindings, string Name, string Actual, string Declared)[]
        {
            ("deal.amount", new CelBindings().Add("deal", new OtherCarrier()), "deal", "Great.Parts.Cel.Tests.Examples.Evaluation.ObjectFieldTests+OtherCarrier", "acme.crm.Deal"),
            ("deal.amount", new CelBindings().Add("deal", new Unknown()), "deal", UnknownName, "acme.crm.Deal"),
            ("c.name", new CelBindings().Add<IReadOnlyDictionary<string, string>>("c", new Dictionary<string, string> { ["name"] = "Ann" }), "c", "map", "acme.crm.Contact"),
            ("c.name", new CelBindings().Add("c", applicationView), "c", "map", "acme.crm.Contact"),
            ("c.name", new CelBindings().Add("c", CelValue.FromObject(Person, dictionary)), "c", "acme.crm.Person", "acme.crm.Contact"),
            ("c.name", new CelBindings().Add("c", CelValue.FromObject(sameName, dictionary)), "c", "acme.crm.Contact", "acme.crm.Contact"),
        };

        foreach ((string expression, CelBindings bindings, string name, string actual, string declared) in cases)
        {
            AssertError(
                SingleError(expression, bindings),
                CelErrorCode.BindingTypeMismatch,
                0,
                "value of variable '" + name + "' has type '" + actual + "', which does not match the declared type '" + declared + "'");
        }
    }

    private static CelBindings ContactBindings(Dictionary<string, object?> entries) =>
        new CelBindings().Add("c", CelValue.FromObject(Contact, entries));

    private T Evaluate<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private CelError SingleError(string expression, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }

    private static void AssertError(CelError error, CelErrorCode code, int offset, string message)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    private sealed class DealCarrier
    {
        public long Amount { get; init; }

        public string? Title { get; init; }

        public string? Note { get; init; }

        public bool HasNote { get; init; }

        public CelValue Count { get; init; }

        public CelValue Extra { get; init; }
    }

    private sealed class OtherCarrier
    {
    }

    private sealed class Unknown
    {
    }
}
