using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The descriptions of object and enumeration types, the providers and the builders of types described at run time:
// their checks, field numbers, presence and the values of fields by the rules of the object values.
public sealed class ObjectTypeTests
{
    private static readonly CelValue FieldErrorValue =
        new OrderType("acme.Order", [new(0, "total", CelType.Int)]).Error(0, CelErrorCode.UnexpectedNull, null);

    [Theory]
    [InlineData("1acme.Deal")]
    [InlineData("acme..Deal")]
    [InlineData("in.Deal")]
    [InlineData("")]
    public static void NameThatIsNotAFullCelName_IsRejectedByEveryDescription(string name)
    {
        Assert.Throws<ArgumentException>(() => new CelObjectTypeBuilder<Deal>(name));
        Assert.Throws<ArgumentException>(() => CelObjectTypeBuilder.ForDictionary(name));
        Assert.Throws<ArgumentException>(() => new CelEnumType(name, []));
        Assert.Throws<ArgumentException>(() => new OrderType(name, []));
    }

    [Fact]
    public void DescriptionOfADotNetType_KeepsNameTypeFieldsAndLooksUpFieldsByOrdinalName()
    {
        var type = new OrderType("acme.sales.Order", [new(0, "total", CelType.Int), new(1, "note", CelType.WrapperOf(CelType.String))]);

        Assert.Equal("acme.sales.Order", type.Name);
        Assert.Equal(typeof(Order), type.ClrType);
        Assert.Equal(CelType.ObjectNamed("acme.sales.Order"), type.Type);
        Assert.Equal(["total", "note"], new[] { type.Fields[0].Name, type.Fields[1].Name });
        Assert.True(type.TryGetField("note", out CelObjectField? note));
        Assert.Equal(1, note.Number);
        Assert.False(type.TryGetField("Note", out CelObjectField? missing));
        Assert.Null(missing);
        Assert.Throws<ArgumentNullException>(() => type.TryGetField(null!, out _));
    }

    [Fact]
    public void DescriptionWithAFieldOutOfPlaceOrTwoFieldsOfOneName_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new OrderType("acme.Order", [new(1, "total", CelType.Int)]));
        Assert.Throws<ArgumentException>(() => new OrderType("acme.Order", [new(0, "total", CelType.Int), new(1, "total", CelType.Int)]));
        Assert.Throws<ArgumentNullException>(() => new OrderType("acme.Order", [null!]));
    }

    [Fact]
    public void DefaultsOfADescription_AreNotConstructibleAndExcludeNothing()
    {
        var type = new OrderType("acme.Order", []);

        Assert.False(type.IsConstructible);
        Assert.Null(type.GetExcludedFieldReason("total"));
    }

    [Theory]
    [InlineData("in")]
    [InlineData("var")]
    [InlineData("acme.ext.priority")]
    public static void FieldName_TakesReservedWordsAndDottedExtensionNames(string name)
    {
        Assert.Equal(name, new CelObjectField(0, name, CelType.Int).Name);
    }

    [Fact]
    public void Field_RejectsANegativeNumberAWrongNameAndATypeParameter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelObjectField(-1, "total", CelType.Int));
        Assert.Throws<ArgumentException>(() => new CelObjectField(0, "1total", CelType.Int));
        Assert.Throws<ArgumentException>(() => new CelObjectField(0, "a..b", CelType.Int));
        Assert.Throws<ArgumentException>(() => new CelObjectField(0, "total", CelType.TypeParameter("A")));
        Assert.Throws<ArgumentNullException>(() => new CelObjectField(0, "total", null!));
    }

    [Fact]
    public void EnumType_KeepsConstantsInOrderWithAliasesAndRejectsARepeatedOrWrongName()
    {
        var status = new CelEnumType("acme.Status", [new("Draft", 0), new("Open", 1), new("Active", 1)]);

        Assert.Equal("acme.Status", status.Name);
        KeyValuePair<string, long>[] expected = [new("Draft", 0), new("Open", 1), new("Active", 1)];
        Assert.Equal(expected, status.Constants);
        Assert.Throws<ArgumentException>(() => new CelEnumType("acme.Status", [new("Open", 0), new("Open", 1)]));
        Assert.Throws<ArgumentException>(() => new CelEnumType("acme.Status", [new("in", 0)]));
        Assert.Throws<ArgumentException>(() => new CelEnumType("acme.Status", [new("a.b", 0)]));
        Assert.Throws<ArgumentNullException>(() => new CelEnumType("acme.Status", [new(null!, 0)]));
    }

    [Fact]
    public void ProviderOfTypesDescribedAtRunTime_HasTheNameTheTypesAndNoEnumerations()
    {
        CelObjectType deal = new CelObjectTypeBuilder<Deal>("acme.Deal").Build();

        CelTypeProvider provider = CelTypeProvider.Create("crm", deal);

        Assert.Equal("crm", provider.SourceName);
        Assert.Same(deal, Assert.Single(provider.ObjectTypes));
        Assert.Empty(provider.EnumTypes);
        Assert.Throws<ArgumentException>(() => CelTypeProvider.Create(string.Empty, deal));
        Assert.Throws<ArgumentNullException>(() => CelTypeProvider.Create(null!, deal));
        Assert.Throws<ArgumentNullException>(() => CelTypeProvider.Create("crm", deal, null!));
    }

    [Fact]
    public void Builder_NumbersFieldsInOrderAndALaterFieldDoesNotChangeABuiltDescription()
    {
        var builder = new CelObjectTypeBuilder<Deal>("acme.Deal")
            .AddField("title", CelType.String, static deal => CelValue.FromString(deal.Title!))
            .AddField("amount", CelType.Int, static deal => CelValue.FromInt64(deal.Amount));

        CelObjectType first = builder.Build();
        builder.AddField("owner", CelType.Dyn, static _ => CelValue.Null);
        CelObjectType second = builder.Build();

        Assert.Null(first.ClrType);
        Assert.Equal(CelType.ObjectNamed("acme.Deal"), first.Type);
        Assert.Equal([0, 1], new[] { first.Fields[0].Number, first.Fields[1].Number });
        Assert.Equal(2, first.Fields.Length);
        Assert.Equal(3, second.Fields.Length);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Builder_RejectsARepeatedNameAWrongNameATypeParameterAndAMissingDelegate()
    {
        var builder = new CelObjectTypeBuilder<Deal>("acme.Deal").AddField("title", CelType.String, static _ => CelValue.Null);

        Assert.Throws<ArgumentException>(() => builder.AddField("title", CelType.Int, static _ => CelValue.Null));
        Assert.Throws<ArgumentException>(() => builder.AddField("a.b", CelType.Int, static _ => CelValue.Null));
        Assert.Throws<ArgumentException>(() => builder.AddField("t", CelType.TypeParameter("A"), static _ => CelValue.Null));
        Assert.Throws<ArgumentNullException>(() => builder.AddField("t", CelType.Int, null!));
        Assert.Throws<ArgumentException>(() => CelObjectTypeBuilder.ForDictionary("acme.Contact").AddField("x", CelType.Int).AddField("x", CelType.Int));
        Assert.Throws<ArgumentNullException>(() => CelObjectTypeBuilder.ForDictionary("acme.Contact").AddField("x", null!));
    }

    [Fact]
    public void CarrierField_IsReadByItsDelegateAndCheckedAtTheTopLevel()
    {
        CelObjectType deal = DealType();
        var instance = new Deal { Title = "Supply", Amount = 12 };

        Assert.Equal(CelValue.FromString("Supply"), deal.GetField(instance, 0));
        Assert.Equal(CelValue.FromInt64(12), deal.GetField(instance, 1));
        Assert.Equal(CelValue.Null, deal.GetField(instance, 2));
        Assert.True(deal.HasField(instance, 2));
    }

    [Fact]
    public void CarrierFieldThatIsNotPresent_IsFieldNotSet()
    {
        CelObjectType deal = DealType();
        var instance = new Deal { Title = null, Amount = -1 };

        AssertError(CelErrorCode.FieldNotSet, "field 'title' of type 'acme.Deal' has no value", deal.GetField(instance, 0));
        AssertError(CelErrorCode.FieldNotSet, "field 'amount' of type 'acme.Deal' has no value", deal.GetField(instance, 1));
        Assert.False(deal.HasField(instance, 0));
        Assert.False(deal.HasField(instance, 1));
    }

    [Fact]
    public void CarrierFieldOfAnotherKind_IsFieldTypeMismatchWithoutDescendingIntoAList()
    {
        CelObjectType type = new CelObjectTypeBuilder<Deal>("acme.Deal")
            .AddField("amount", CelType.Int, static _ => CelValue.FromString("12"))
            .AddField("tags", CelType.ListOf(CelType.String), static _ => CelValue.FromList(CelList.Create([CelValue.FromInt64(1)])))
            .AddField("broken", CelType.Int, static _ => FieldErrorValue)
            .Build();

        AssertError(
            CelErrorCode.FieldTypeMismatch,
            "field 'amount' of type 'acme.Deal' has a value of type 'string', expected 'int'",
            type.GetField(new Deal(), 0));
        Assert.Equal(CelValueKind.List, type.GetField(new Deal(), 1).Kind);
        Assert.Equal(FieldErrorValue, type.GetField(new Deal(), 2));
    }

    [Fact]
    public void DictionaryField_IsTheEntryConvertedAsAnObjectAndAMissingKeyIsNotSet()
    {
        CelObjectType contact = CelObjectTypeBuilder.ForDictionary("acme.Contact")
            .AddField("age", CelType.Int)
            .AddField("tags", CelType.ListOf(CelType.String))
            .AddField("note", CelType.WrapperOf(CelType.String))
            .AddField("extra", CelType.Dyn)
            .Build();
        var carrier = new Dictionary<string, object?>
        {
            ["age"] = 42,
            ["tags"] = CelValue.FromList(CelList.Create([CelValue.FromString("vip")])),
            ["note"] = null,
            ["extra"] = new List<int> { 1 },
        };

        Assert.Equal(CelValue.FromInt64(42), contact.GetField(carrier, 0));
        Assert.Equal(CelValueKind.List, contact.GetField(carrier, 1).Kind);
        Assert.Equal(CelValue.Null, contact.GetField(carrier, 2));

        // A .NET collection is a bare object here; the node that reads the field decides it with the type index.
        Assert.Equal(CelValueKind.Object, contact.GetField(carrier, 3).Kind);
        carrier.Remove("age");
        AssertError(CelErrorCode.FieldNotSet, "field 'age' of type 'acme.Contact' has no value", contact.GetField(carrier, 0));
        Assert.False(contact.HasField(carrier, 0));
        Assert.True(contact.HasField(carrier, 2));
    }

    [Fact]
    public void DictionaryInAnObjectTypedField_IsACarrierOfThatTypeWhichTheReadPairs()
    {
        CelObjectType address = CelObjectTypeBuilder.ForDictionary("acme.Address").AddField("city", CelType.String).Build();
        CelObjectType contact = CelObjectTypeBuilder.ForDictionary("acme.Contact").AddField("home", address.Type).Build();
        TypeIndex types = Index(address, contact);
        var home = new Dictionary<string, object?> { ["city"] = "Riga" };
        var carrier = new Dictionary<string, object?> { ["home"] = home };

        CelValue bare = contact.GetField(carrier, 0);
        CelValue read = ObjectRules.ReadField(contact, carrier, 0, types);

        Assert.Null(bare.PairedType);
        Assert.Same(address, read.PairedType);
        Assert.True(read.TryGetObject(out object? instance));
        Assert.Same(home, instance);
    }

    [Fact]
    public void FromObject_PairsAnInstanceThatBelongsAndRejectsAnotherCarrier()
    {
        CelObjectType deal = DealType();
        var instance = new Deal();

        CelValue value = CelValue.FromObject(deal, instance);

        Assert.Equal(CelValueKind.Object, value.Kind);
        Assert.True(value.TryGetObject(out object? read));
        Assert.Same(instance, read);
        Assert.Same(deal, value.PairedType);
        Assert.Throws<ArgumentException>(() => CelValue.FromObject(deal, new Order()));
        Assert.Throws<ArgumentException>(() => CelValue.FromObject(new OrderType("acme.Order", []), new Deal()));
        Assert.Throws<ArgumentNullException>(() => CelValue.FromObject(deal, null!));
        Assert.False(CelValue.FromString("x").TryGetObject(out object? none));
        Assert.Null(none);
    }

    [Fact]
    public void DotNetEqualityOfObjectValues_ComparesTheInstanceAndTheDescriptionByReference()
    {
        CelObjectType deal = DealType();
        CelObjectType other = DealType();
        var instance = new Deal();

        Assert.Equal(CelValue.FromObject(deal, instance), CelValue.FromObject(deal, instance));
        Assert.Equal(CelValue.FromObject(deal, instance).GetHashCode(), CelValue.FromObject(deal, instance).GetHashCode());
        Assert.NotEqual(CelValue.FromObject(deal, instance), CelValue.FromObject(other, instance));
        Assert.NotEqual(CelValue.FromObject(deal, instance), CelValue.FromObject(deal, new Deal()));
        Assert.NotEqual(CelValue.FromObject(deal, instance), OrderType.Bare(instance));
        Assert.Equal(OrderType.Bare(instance), OrderType.Bare(instance));
        Assert.Equal(OrderType.Bare(instance).GetHashCode(), OrderType.Bare(instance).GetHashCode());
    }

    [Fact]
    public void FromInstance_IsABareValueThatTakesNoNull()
    {
        var order = new Order();

        CelValue value = OrderType.Bare(order);

        Assert.True(value.TryGetObject(out object? instance));
        Assert.Same(order, instance);
        Assert.Null(value.PairedType);
        Assert.Throws<ArgumentNullException>(() => OrderType.Bare(null!));
    }

    [Fact]
    public void FieldError_IsTheTemplateOfItsCodeAndChecksItsArguments()
    {
        var type = new OrderType("acme.Order", [new(0, "note", CelType.String)]);

        AssertError(
            CelErrorCode.UnexpectedNull,
            "member 'note' of type 'acme.Order' is null, but its CEL type does not allow null",
            type.Error(0, CelErrorCode.UnexpectedNull, null));
        AssertError(CelErrorCode.ConversionRange, "300 is out of the range of int", type.Error(0, CelErrorCode.ConversionRange, "300 is out of the range of int"));
        AssertError(CelErrorCode.InvalidArgument, "invalid argument to 'acme.Order.note': too long", type.Error(0, CelErrorCode.InvalidArgument, "too long"));
        AssertError(
            CelErrorCode.TypeNotAccessible,
            ".NET type 'Some.Type' is not available to expressions: no type source of the environment describes it",
            type.Error(0, CelErrorCode.TypeNotAccessible, "Some.Type"));
        Assert.Throws<ArgumentOutOfRangeException>(() => type.Error(1, CelErrorCode.UnexpectedNull, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => type.Error(-1, CelErrorCode.UnexpectedNull, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => type.Error(0, CelErrorCode.DuplicateType, null));
    }

    [Fact]
    public void ConversionHelpers_ConvertEnumerationsAndDynamicValuesAndNameTheFieldOfARefusal()
    {
        var type = new OrderType("acme.Order", [new(0, "flags", CelType.Int), new(1, "extra", CelType.Dyn)]);
        var order = new Order();

        Assert.Equal(CelValue.FromInt64(2), type.Enum(0, DayOfWeek.Tuesday));
        AssertError(
            CelErrorCode.ConversionRange,
            "18446744073709551615 is out of the range of int",
            type.Enum(0, Wide.Top));
        Assert.Equal(CelValue.FromString("x"), type.Dynamic(1, "x"));
        Assert.Equal(CelValue.Null, type.Dynamic(1, null));
        Assert.True(type.Dynamic(1, order).TryGetObject(out object? instance));
        Assert.Same(order, instance);
        Assert.Equal(CelValueKind.Object, type.Dynamic(1, new Queue<int>()).Kind);
    }

    internal static CelObjectType DealType() => new CelObjectTypeBuilder<Deal>("acme.Deal")
        .AddField("title", CelType.String, static deal => deal.Title is null ? CelValue.Null : CelValue.FromString(deal.Title))
        .AddField("amount", CelType.Int, static deal => CelValue.FromInt64(deal.Amount), static deal => deal.Amount >= 0)
        .AddField("note", CelType.WrapperOf(CelType.String), static _ => CelValue.Null, static _ => true)
        .Build();

    internal static TypeIndex Index(params CelObjectType[] objectTypes)
    {
        var byName = new Dictionary<string, CelObjectType>(StringComparer.Ordinal);
        var byClrType = new Dictionary<Type, CelObjectType>();
        foreach (CelObjectType objectType in objectTypes)
        {
            byName.Add(objectType.Name, objectType);
            if (objectType.ClrType is Type clrType)
            {
                byClrType.Add(clrType, objectType);
            }
        }

        return new TypeIndex(byName, new Dictionary<string, long>(StringComparer.Ordinal), byClrType);
    }

    internal static void AssertError(CelErrorCode code, string message, CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error));
        Assert.Equal((code, message), (error.Code, error.Message));
    }

    private enum Wide : ulong
    {
        Top = ulong.MaxValue,
    }

    // A carrier class of types described at run time.
    internal sealed class Deal
    {
        internal string? Title { get; init; }

        internal long Amount { get; init; }

        internal Deal? Parent { get; init; }
    }

    // A .NET type with a description of its own.
    internal class Order
    {
        internal long Total { get; init; }

        internal double Rate { get; init; }

        internal Order? Next { get; init; }
    }

    internal sealed class SpecialOrder : Order;

    // A description of a .NET type as the source generator would write it, with the fields total, rate and next.
    internal sealed class OrderType(string name, ReadOnlySpan<CelObjectField> fields) : CelObjectType(name, typeof(Order), fields)
    {
        internal static CelValue Bare(object instance) => FromInstance(instance);

        internal CelValue Error(int number, CelErrorCode code, string? detail) => FieldError(number, code, detail);

        internal CelValue Enum<TEnum>(int number, TEnum value)
            where TEnum : struct, System.Enum => FromEnum(number, value);

        internal CelValue Dynamic(int number, object? value) => FromDynamic(number, value);

        internal CelValue DateTimeField(int number, DateTime value) => FromDateTime(number, value);

        internal CelValue TimeSpanField(int number, TimeSpan value) => FromTimeSpan(number, value);

        protected internal override CelValue GetField(object instance, int number)
        {
            var order = (Order)instance;
            return number switch
            {
                0 => CelValue.FromInt64(order.Total),
                1 => CelValue.FromDouble(order.Rate),
                _ => order.Next is null ? CelValue.Null : FromInstance(order.Next),
            };
        }

        protected internal override bool HasField(object instance, int number) => number != 2 || ((Order)instance).Next is not null;
    }
}
