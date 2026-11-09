using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The reading of a value of the application by the node that has the type index: the place rule looks through a view of
// the input rows, and a .NET collection is an object when a type source describes it and TypeNotAccessible, or in a field
// of a dictionary carrier FieldTypeMismatch, when nothing does.
public sealed class ReadingApplicationValuesTests
{
    private const string ListName = "System.Collections.Generic.List`1[System.Int64]";

    private static readonly CelObjectType BasketDescription = new BasketType();

    private static readonly CelObjectType Contact = CelObjectTypeBuilder.ForDictionary("acme.Contact")
        .AddField("name", CelType.String)
        .AddField("info", CelType.Dyn)
        .Build();

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CelTypeProvider.Create("source", BasketDescription, Contact))
        .AddVariable("v", CelType.Dyn)
        .AddVariable("c", Contact.Type)
        .AddFunction<object>("make", () => new List<long> { 1 })
        .AddFunction<object>("basket", () => new Basket { 1, 2 })
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void CollectionThatNoSourceDescribes_IsTypeNotAccessibleAtTheIdentifierAndAtTheCall()
    {
        CelBindings bindings = new CelBindings().Add<object>("v", new List<long> { 1 });

        AssertNotAccessible(SingleError("v", bindings), 0);
        AssertNotAccessible(SingleError("size(v)", bindings), 5);
        AssertNotAccessible(SingleError("make()", bindings), 4);
    }

    [Fact]
    public void CollectionThatASourceDescribes_IsAnObjectEvenIfItIsEnumerable()
    {
        CelBindings bindings = new CelBindings().Add<object>("v", new Basket { 4, 5, 6 });

        Assert.Equal(3L, Evaluate<long>("v.count", bindings));
        Assert.True(Evaluate<bool>("type(v) == acme.Basket && basket().count == 2", bindings));
    }

    [Fact]
    public void CollectionAsAnElementOfACollectionOfTheApplication_IsDescribedWhereItsDescriptionIsNeeded()
    {
        CelBindings bindings = new CelBindings().Add<IReadOnlyList<object>>("v", [new List<long> { 1 }, new Basket { 7 }]);

        CelError error = SingleError("v[0].f", bindings);

        Assert.Equal((CelErrorCode.TypeNotAccessible, 4), (error.Code, error.Location.Offset));
        Assert.True(Evaluate<bool>("v[1].count == 1 && v[0] != [1]", bindings));
    }

    [Fact]
    public void CollectionInAFieldOfADictionaryCarrier_IsFieldTypeMismatchUnlessASourceDescribesIt()
    {
        var list = new Dictionary<string, object?> { ["info"] = new List<long> { 1 } };
        var basket = new Dictionary<string, object?> { ["info"] = new Basket { 8, 9 } };

        CelError error = SingleError("c.info", new CelBindings().Add("c", CelValue.FromObject(Contact, list)));

        Assert.Equal((CelErrorCode.FieldTypeMismatch, 1), (error.Code, error.Location.Offset));
        Assert.Equal("field 'info' of type 'acme.Contact' has a value of type '" + ListName + "', expected 'dyn'", error.Message);
        Assert.Equal(2L, Evaluate<long>("c.info.count", new CelBindings().Add("c", CelValue.FromObject(Contact, basket))));
    }

    // A dictionary bound to a variable of a type with a dictionary carrier is a carrier of that type, by the static type of
    // the binding or as object; a dictionary of another type and a view that the application created stay a map.
    [Fact]
    public void DictionaryBoundToAVariableOfItsType_IsACarrierOfThatType()
    {
        var dictionary = new Dictionary<string, object?> { ["name"] = "Ann" };
        CelBindings[] bindings =
        [
            new CelBindings().Add<IReadOnlyDictionary<string, object?>>("c", dictionary),
            new CelBindings().Add<IDictionary<string, object?>>("c", dictionary),
            new CelBindings().Add<object>("c", dictionary),
        ];

        foreach (CelBindings binding in bindings)
        {
            Assert.True(Evaluate<bool>("c.name == 'Ann' && type(c) == acme.Contact && has(c.name) && !has(c.info)", binding));
        }
    }

    [Fact]
    public void DictionaryThatIsNotACarrierOfTheType_IsBindingTypeMismatchNamingAMap()
    {
        CelMap applicationView = CelMap.CreateView(new Dictionary<string, object?> { ["name"] = "Ann" }, value => CelValue.FromString((string)value!), keepSourceOrder: true);
        CelBindings[] bindings =
        [
            new CelBindings().Add<IReadOnlyDictionary<string, string>>("c", new Dictionary<string, string> { ["name"] = "Ann" }),
            new CelBindings().Add("c", applicationView),
        ];

        foreach (CelBindings binding in bindings)
        {
            CelError error = SingleError("c.name", binding);

            Assert.Equal((CelErrorCode.BindingTypeMismatch, 0), (error.Code, error.Location.Offset));
            Assert.Equal("value of variable 'c' has type 'map', which does not match the declared type 'acme.Contact'", error.Message);
        }
    }

    private static void AssertNotAccessible(CelError error, int offset)
    {
        Assert.Equal((CelErrorCode.TypeNotAccessible, offset), (error.Code, error.Location.Offset));
        Assert.Equal(".NET type '" + ListName + "' is not available to expressions: no type source of the environment describes it", error.Message);
    }

    private T Evaluate<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private CelError SingleError(string expression, CelBindings bindings) =>
        Assert.Single(environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

    // A list class of the application that a source describes: its description has the field count.
    private sealed class Basket : List<long>;

    private sealed class BasketType() : CelObjectType("acme.Basket", typeof(Basket), [new(0, "count", CelType.Int)])
    {
        protected internal override CelValue GetField(object instance, int number) => CelValue.FromInt64(((Basket)instance).Count);

        protected internal override bool HasField(object instance, int number) => true;
    }
}
