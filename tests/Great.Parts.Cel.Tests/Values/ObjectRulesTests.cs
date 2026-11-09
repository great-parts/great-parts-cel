using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Values.ObjectTypeTests;

namespace Great.Parts.Cel.Tests.Values;

// The rules of object values: the description of a value, the place rule, whether a value fits an object type, the
// names of mismatches, TypeNotAccessible and the default equality of objects with its cost.
public sealed class ObjectRulesTests
{
    private static readonly OrderType Orders = new(
        "acme.Order",
        [new(0, "total", CelType.Int), new(1, "rate", CelType.Double), new(2, "next", CelType.ObjectNamed("acme.Order"))]);

    private static readonly CelObjectType Deals = DealType();

    private static readonly TypeIndex Types = Index(Orders, Deals);

    [Fact]
    public void BareInstance_IsDescribedByItsExactTypeThenItsBaseClass()
    {
        Assert.Same(Orders, ObjectRules.Describe(OrderType.Bare(new Order()), Types));
        Assert.Same(Orders, ObjectRules.Describe(OrderType.Bare(new SpecialOrder()), Types));
        Assert.Null(ObjectRules.Describe(OrderType.Bare(new Deal()), Types));
        Assert.Null(ObjectRules.Describe(OrderType.Bare(new Order()), null));
        Assert.Same(Deals, ObjectRules.Describe(CelValue.FromObject(Deals, new Deal()), null));
    }

    [Fact]
    public void NodeThatNeedsTheDescriptionOfAnInstanceWithoutOne_GetsTypeNotAccessible()
    {
        Assert.False(ObjectRules.TryDescribe(OrderType.Bare(new Deal()), Types, out CelObjectType? description, out CelValue error));

        Assert.Null(description);
        AssertError(
            CelErrorCode.TypeNotAccessible,
            ".NET type 'Great.Parts.Cel.Tests.Values.ObjectTypeTests+Deal' is not available to expressions: no type source of the environment describes it",
            error);
    }

    [Fact]
    public void PlaceRule_TakesABareInstanceThatBelongsAndPairsItWhenTheIndexDescribesItOtherwise()
    {
        var order = new Order();
        CelValue bareOrder = OrderType.Bare(order);
        CelValue bareDeal = OrderType.Bare(new Deal());

        Assert.True(ObjectRules.TryEnterPlace(bareOrder, Orders.Type, Types, out CelValue sameOrder, out _));
        Assert.True(ObjectRules.TryEnterPlace(bareDeal, Deals.Type, Types, out CelValue pairedDeal, out _));
        Assert.True(ObjectRules.TryEnterPlace(CelValue.Null, Deals.Type, Types, out _, out _));

        Assert.Equal(bareOrder, sameOrder);
        Assert.Same(Deals, pairedDeal.PairedType);
        Assert.True(ObjectRules.Fits(sameOrder, Orders.Type, Types));
        Assert.True(ObjectRules.Fits(pairedDeal, Deals.Type, Types));
    }

    [Fact]
    public void PlaceRule_RefusesWhatDoesNotBelongAndNamesItsType()
    {
        Assert.False(ObjectRules.TryEnterPlace(OrderType.Bare(new Deal()), Orders.Type, Types, out _, out string? bare));
        Assert.False(ObjectRules.TryEnterPlace(CelValue.FromObject(DealType(), new Deal()), Deals.Type, Types, out _, out string? otherPair));
        Assert.False(ObjectRules.TryEnterPlace(CelValue.FromInt64(1), Deals.Type, Types, out _, out string? number));

        Assert.Equal("Great.Parts.Cel.Tests.Values.ObjectTypeTests+Deal", bare);
        Assert.Equal("acme.Deal", otherPair);
        Assert.Equal("int", number);
    }

    [Fact]
    public void ObjectType_TakesNullAndTheDescriptionOfTheIndexOnly()
    {
        CelObjectType otherDeals = DealType();

        Assert.True(ObjectRules.Fits(CelValue.Null, Deals.Type, Types));
        Assert.False(ObjectRules.Fits(CelValue.FromObject(otherDeals, new Deal()), Deals.Type, Types));
        Assert.False(ObjectRules.Fits(OrderType.Bare(new Deal()), Deals.Type, Types));
        Assert.False(ObjectRules.Fits(CelValue.FromObject(Deals, new Deal()), Deals.Type, null));
        Assert.False(ObjectRules.Fits(CelValue.FromInt64(1), Deals.Type, Types));
    }

    [Fact]
    public void ElementOfACollection_IsDescribedByTheIndexSoABareCarrierDoesNotFitAndThePairDoes()
    {
        CelType listOfDeals = CelType.ListOf(Deals.Type);
        var bare = CelValue.FromList(CelList.Create(OrderType.Bare(new Deal())));
        var paired = CelValue.FromList(CelList.Create(CelValue.FromObject(Deals, new Deal())));

        Assert.False(ValueTypeRules.Fits(bare, listOfDeals, null, false, Types).IsFit);
        Assert.Equal(
            "list(Great.Parts.Cel.Tests.Values.ObjectTypeTests+Deal)",
            ValueTypeRules.MismatchType(bare, listOfDeals, false, Types));
        Assert.True(ValueTypeRules.Fits(paired, listOfDeals, null, false, Types).IsFit);
    }

    [Fact]
    public void MismatchOfAnObjectValue_NamesItsDescriptionOrItsDotNetType()
    {
        Assert.Equal("acme.Order", ValueTypeRules.MismatchType(OrderType.Bare(new Order()), CelType.Int, false, Types));
        Assert.Equal("acme.Deal", ValueTypeRules.MismatchType(CelValue.FromObject(Deals, new Deal()), Orders.Type, false, Types));
        Assert.Equal(
            "Great.Parts.Cel.Tests.Values.ObjectTypeTests+Deal",
            ValueTypeRules.MismatchType(OrderType.Bare(new Deal()), Orders.Type, false, Types));
    }

    [Fact]
    public void ObjectTypedFieldWithAnInstanceThatDoesNotBelong_IsFieldTypeMismatch()
    {
        CelObjectType holder = new CelObjectTypeBuilder<Deal>("acme.Holder")
            .AddField("order", Orders.Type, static deal => deal.Parent is null ? CelValue.Null : OrderType.Bare(deal.Parent))
            .AddField("name", CelType.String, static deal => OrderType.Bare(deal))
            .Build();
        TypeIndex types = Index(Orders, Deals, holder);
        var instance = new Deal { Parent = new Deal() };

        AssertError(
            CelErrorCode.FieldTypeMismatch,
            "field 'order' of type 'acme.Holder' has a value of type 'Great.Parts.Cel.Tests.Values.ObjectTypeTests+Deal', expected 'acme.Order'",
            ObjectRules.ReadField(holder, instance, 0, types));
        AssertError(
            CelErrorCode.FieldTypeMismatch,
            "field 'name' of type 'acme.Holder' has a value of type 'Great.Parts.Cel.Tests.Values.ObjectTypeTests+Deal', expected 'string'",
            ObjectRules.ReadField(holder, instance, 1, types));
        AssertError(CelErrorCode.FieldNotSet, "field 'order' of type 'acme.Holder' has no value", ObjectRules.ReadField(holder, new Deal(), 0, types));
    }

    [Fact]
    public void ObjectRow_TurnsAnyInstanceOutsideTheRowsIntoABareObject_ACollectionIncluded()
    {
        var order = new Order();

        Assert.True(ConversionRules.TryFromClr<object>(new System.Collections.Generic.Dictionary<string, int>(), out CelValue collection));
        Assert.True(ConversionRules.TryFromClr<object>(order, out CelValue instance));

        Assert.Equal(CelValueKind.Object, collection.Kind);
        Assert.Null(collection.PairedType);
        Assert.True(instance.TryGetObject(out object? read));
        Assert.Same(order, read);
        Assert.Null(instance.PairedType);
    }

    [Fact]
    public void EqualityOfBareInstancesOutsideAnEvaluation_IsTypeNotAccessible()
    {
        CelValue result = CelValue.CelEquals(OrderType.Bare(new Order()), OrderType.Bare(new Order()));

        AssertError(
            CelErrorCode.TypeNotAccessible,
            ".NET type 'Great.Parts.Cel.Tests.Values.ObjectTypeTests+Order' is not available to expressions: no type source of the environment describes it",
            result);
    }

    [Fact]
    public void ObjectsOfOneDescription_CompareFieldByFieldWithoutAShortcutForTheSameReference()
    {
        var order = new Order { Total = 3, Rate = 0.5 };
        var nan = new Order { Total = 3, Rate = double.NaN };

        Assert.Equal(CelValue.FromBoolean(true), CelValue.CelEquals(CelValue.FromObject(Orders, order), CelValue.FromObject(Orders, new Order { Total = 3, Rate = 0.5 })));
        Assert.Equal(CelValue.FromBoolean(false), CelValue.CelEquals(CelValue.FromObject(Orders, order), CelValue.FromObject(Orders, new Order { Total = 4, Rate = 0.5 })));
        Assert.Equal(CelValue.FromBoolean(false), CelValue.CelEquals(CelValue.FromObject(Orders, nan), CelValue.FromObject(Orders, nan)));
        Assert.Equal(CelValue.FromBoolean(true), Orders.CelEquals(order, new Order { Total = 3, Rate = 0.5 }));
        Assert.Equal(CelValue.FromBoolean(false), Orders.CelEquals(nan, nan));
    }

    [Fact]
    public void FieldPresentInOneInstanceOnly_MakesThemUnequalAndAFieldAbsentFromBothIsSkipped()
    {
        var next = new Order { Total = 1 };

        Assert.Equal(CelValue.FromBoolean(false), Orders.CelEquals(new Order { Next = next }, new Order()));
        Assert.Equal(CelValue.FromBoolean(true), Orders.CelEquals(new Order(), new Order()));
    }

    [Fact]
    public void ObjectsOfDifferentDescriptions_AreUnequalWithoutReadingFields()
    {
        var meter = new RecordingMeter();
        var instance = new Deal { Title = "a", Amount = 1 };

        ValueEquality result = ObjectRules.AreEqual(
            CelValue.FromObject(Deals, instance), CelValue.FromObject(DealType(), instance), Types, meter, out _);

        Assert.Equal(ValueEquality.NotEqual, result);
        Assert.Equal([1UL], meter.Charges);
    }

    [Fact]
    public void EqualityOfObjects_ChargesOneThenTheReadsOfEachFieldBeforeComparingItsValues()
    {
        var meter = new RecordingMeter();

        ValueEquality result = ObjectRules.AreEqual(
            OrderType.Bare(new Order { Total = 3 }), OrderType.Bare(new Order { Total = 3 }), Types, meter, out _);

        // 1; total: 2 reads, 1 comparison; rate: 2 reads, 1 comparison; next: 2 reads of an absent field.
        Assert.Equal(ValueEquality.Equal, result);
        Assert.Equal([1UL, 2, 1, 2, 1, 2], meter.Charges);
    }

    [Fact]
    public void ErrorReadFromAField_IsTheResultOfTheEquality()
    {
        CelObjectType broken = new CelObjectTypeBuilder<Deal>("acme.Broken").AddField("amount", CelType.Int, static _ => CelValue.FromString("x")).Build();

        Assert.Equal(
            ValueEquality.Error,
            ObjectRules.AreEqual(CelValue.FromObject(broken, new Deal()), CelValue.FromObject(broken, new Deal()), null, null, out CelValue error));
        AssertError(CelErrorCode.FieldTypeMismatch, "field 'amount' of type 'acme.Broken' has a value of type 'string', expected 'int'", error);
    }
}
