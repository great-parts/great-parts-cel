using System;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The value of the kind Optional: its factory and reader, .NET equality and CEL equality, the check against a declared type at depth
// with its charges, and the rules that refuse an optional as a map key.
public sealed class OptionalValueTests
{
    private static readonly CelType OptionalOfListOfInt = CelType.OptionalOf(CelType.ListOf(CelType.Int));

    [Fact]
    public void FromOptional_OfAnEmptyOptional_IsTheOptionalKindWithoutAHeldValue()
    {
        CelValue first = CelValue.FromOptional(default);
        CelValue second = CelValue.FromOptional(default);

        Assert.Equal(CelValueKind.Optional, first.Kind);
        Assert.True(first.TryGetOptional(out CelOptional<CelValue> optional));
        Assert.False(optional.HasValue);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void FromOptional_OfAHeldValue_GivesItBackAndKeepsNullAsAValue()
    {
        CelValue ofNull = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.Null));
        CelValue ofOne = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1)));

        Assert.True(ofNull.TryGetOptional(out CelOptional<CelValue> nullHeld));
        Assert.True(nullHeld.HasValue);
        Assert.Equal(CelValueKind.Null, nullHeld.Value.Kind);
        Assert.True(ofOne.TryGetOptional(out CelOptional<CelValue> oneHeld));
        Assert.True(oneHeld.Value.TryGetInt64(out long number) && number == 1);
        Assert.False(ofNull == CelValue.FromOptional(default));
        Assert.False(ofNull == ofOne);
    }

    [Fact]
    public void FromOptional_WithAnErrorValueInside_IsAnArgumentException()
    {
        CelValue error = ConversionRules.ApplicationError(new InvalidOperationException(), "a list of the application");

        Assert.Equal(CelValueKind.Error, error.Kind);
        Assert.Throws<ArgumentException>(() => CelValue.FromOptional(new CelOptional<CelValue>(error)));
    }

    [Fact]
    public void TryGetOptional_OfEveryOtherKind_IsFalseAndGivesAnEmptyOptional()
    {
        CelValue[] values =
        [
            CelValue.Null,
            CelValue.FromBoolean(true),
            CelValue.FromInt64(1),
            CelValue.FromUInt64(1),
            CelValue.FromDouble(1.5),
            CelValue.FromString("a"),
            CelValue.FromBytes(new byte[] { 1 }),
            CelValue.FromList(CelList.Create([CelValue.FromInt64(1)])),
            CelValue.FromType(CelType.Int),
            CelValue.FromTimestamp(default),
            CelValue.FromDuration(default),
        ];

        Assert.All(values, value =>
        {
            Assert.False(value.TryGetOptional(out CelOptional<CelValue> optional));
            Assert.False(optional.HasValue);
        });
    }

    [Fact]
    public void NetEquality_OfOptionalsWithNaNInside_IsReflexive_WhereCelEqualityIsNot()
    {
        CelValue first = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromDouble(double.NaN)));
        CelValue second = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromDouble(double.NaN)));

        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(CelValue.CelEquals(first, second).TryGetBoolean(out bool equal));
        Assert.False(equal);
    }

    [Fact]
    public void NetEquality_ComparesTheKindOfTheHeldValue()
    {
        CelValue ofInt = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1)));
        CelValue ofDouble = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromDouble(1.0)));

        Assert.False(ofInt.Equals(ofDouble));
        Assert.True(CelValue.CelEquals(ofInt, ofDouble).TryGetBoolean(out bool equal));
        Assert.True(equal);
    }

    [Fact]
    public void CelEquality_OfAnOptionalAndAnotherKind_IsFalseWithoutAnError()
    {
        CelValue none = CelValue.FromOptional(default);
        CelValue ofNull = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.Null));

        Assert.True(CelValue.CelEquals(none, CelValue.Null).TryGetBoolean(out bool noneAndNull));
        Assert.False(noneAndNull);
        Assert.True(CelValue.CelEquals(ofNull, CelValue.Null).TryGetBoolean(out bool ofNullAndNull));
        Assert.False(ofNullAndNull);
        Assert.True(CelValue.CelEquals(none, ofNull).TryGetBoolean(out bool noneAndOfNull));
        Assert.False(noneAndOfNull);
    }

    [Fact]
    public void CelEquality_ChargesOneUnitWhenAnOptionalIsEmpty_AndOnePlusTheValuesWhenBothHoldOne()
    {
        CelValue none = CelValue.FromOptional(default);
        CelValue ofOne = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1)));
        CelValue ofTwo = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(2)));
        var emptyAgainstFull = new RecordingMeter();
        var fullAgainstFull = new RecordingMeter();

        _ = EqualityRules.AreEqual(none, ofOne, emptyAgainstFull, out _);
        _ = EqualityRules.AreEqual(ofOne, ofTwo, fullAgainstFull, out _);

        Assert.Equal(1UL, emptyAgainstFull.Total);
        Assert.Equal(2UL, fullAgainstFull.Total);
    }

    [Fact]
    public void Fits_DescendsIntoTheHeldValue_AndChargesWhatThatDescentCharges()
    {
        CelValue numbers = Held(CelValue.FromList(CelList.Create([CelValue.FromInt64(1), CelValue.FromInt64(2)])));
        CelValue text = Held(CelValue.FromList(CelList.Create([CelValue.FromString("a")])));
        var meter = new RecordingMeter();

        Assert.True(ValueTypeRules.Fits(numbers, OptionalOfListOfInt, meter).IsFit);
        Assert.Equal(2UL, meter.Total);
        Assert.False(ValueTypeRules.Fits(text, OptionalOfListOfInt, null).IsFit);
        Assert.Equal("optional_type(list(string))", ValueTypeRules.MismatchType(text, OptionalOfListOfInt));
    }

    [Fact]
    public void Fits_TakesTheEmptyOptional_AndDoesNotLookIntoDynOrATypeParameter()
    {
        CelValue none = CelValue.FromOptional(default);
        CelValue text = Held(CelValue.FromString("a"));
        var meter = new RecordingMeter();

        Assert.True(ValueTypeRules.Fits(none, OptionalOfListOfInt, meter).IsFit);
        Assert.True(ValueTypeRules.Fits(text, CelType.OptionalOf(CelType.Dyn), meter).IsFit);
        Assert.True(ValueTypeRules.Fits(text, CelType.OptionalOf(CelType.TypeParameter("A")), meter).IsFit);
        Assert.True(ValueTypeRules.Fits(text, CelType.OptionalOf(CelType.String), meter).IsFit);
        Assert.False(ValueTypeRules.Fits(text, CelType.OptionalOf(CelType.Int), meter).IsFit);
        Assert.Empty(meter.Charges);
    }

    [Fact]
    public void Fits_RefusesNullAndEveryOtherKind_ForAnOptionalType()
    {
        CelType optionalOfInt = CelType.OptionalOf(CelType.Int);

        Assert.False(ValueTypeRules.Fits(CelValue.Null, optionalOfInt, null).IsFit);
        Assert.False(ValueTypeRules.Fits(CelValue.FromInt64(1), optionalOfInt, null).IsFit);
        Assert.False(ValueTypeRules.Fits(CelValue.FromOptional(default), CelType.Int, null).IsFit);
        Assert.Equal("null_type", ValueTypeRules.MismatchType(CelValue.Null, optionalOfInt));
    }

    [Fact]
    public void Fits_WithNullElements_LetsANullHeldValueThrough()
    {
        CelValue ofNull = Held(CelValue.Null);
        CelType optionalOfString = CelType.OptionalOf(CelType.String);

        Assert.False(ValueTypeRules.Fits(ofNull, optionalOfString, null, nullElements: false).IsFit);
        Assert.True(ValueTypeRules.Fits(ofNull, optionalOfString, null, nullElements: true).IsFit);
        Assert.Null(ValueTypeRules.MismatchType(ofNull, optionalOfString, nullElements: true));
        Assert.Equal("optional_type(null_type)", ValueTypeRules.MismatchType(ofNull, optionalOfString, nullElements: false));
    }

    [Fact]
    public void TypeOf_IsOptionalOfDyn_WhateverTheHeldValueIs()
    {
        CelType optionalOfDyn = CelType.OptionalOf(CelType.Dyn);

        Assert.Equal(optionalOfDyn, ValueTypeRules.TypeOf(CelValue.FromOptional(default)));
        Assert.Equal(optionalOfDyn, ValueTypeRules.TypeOf(Held(CelValue.FromInt64(1))));
        Assert.Equal("optional_type", ValueTypeRules.NameOf(Held(CelValue.FromInt64(1)), null));
    }

    [Fact]
    public void Optional_IsNoMapKey()
    {
        Assert.False(KeyHashRules.IsKeyKind(CelValueKind.Optional));
        Assert.False(KeyHashRules.TryGetHash(CelValue.FromOptional(default), out _));
    }

    // The empty optional is one value without a reference, so making it allocates nothing; an optional that holds a value is one box
    // of a header and one CelValue, within the 64 bytes that the unit of optional.of pays for. The smallest of three runs is counted, so
    // the few bytes that the runtime allocates on the test thread while it moves methods between tiers do not count.
    [Fact]
    [Trait("Requirement", "P-2")]
    public void EmptyOptional_AllocatesNothing_AndAnOptionalThatHoldsAValueOneBox()
    {
        CelValue held = CelValue.FromInt64(1);
        _ = CelValue.FromOptional(default);
        _ = CelValue.FromOptional(new CelOptional<CelValue>(held));

        long empty = Smallest(static () => CelValue.FromOptional(default));
        long one = Smallest(() => CelValue.FromOptional(new CelOptional<CelValue>(held)));

        Assert.Equal(0, empty);
        Assert.InRange(one, 1, 64);
    }

    private static long Smallest(Func<CelValue> make)
    {
        long smallest = long.MaxValue;
        for (int run = 0; run < 3; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            _ = make();
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }

    private static CelValue Held(CelValue value) => CelValue.FromOptional(new CelOptional<CelValue>(value));
}
