#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The value of the type decimal and its rules (the page of the values, "decimal"): the 128 bits in CelValue, .NET equality and the
// hash by value, the mark of a boxed System.Decimal, the type and the name of the kind, the top-level fit, ordering, the map keys, the
// rows of the input, the result and the parameter, a NaN and an infinity, and the members that an object type calls. The examples of
// the extension set check the same values through expressions, so nothing here needs an environment or the set.
public sealed class DecimalValueTests
{
    private static readonly CelValue OneAndHalf = CelValue.FromDecimal(Make(15, -1));

    private static readonly byte[] OneByte = [1];

    [Fact]
    public static void FromDecimal_OfANaNAndOfAnInfinity_IsArgumentExceptionWithItsText()
    {
        foreach (Decimal128 value in new[] { Decimal128.NaN, Decimal128.PositiveInfinity, Decimal128.NegativeInfinity })
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => CelValue.FromDecimal(value));

            Assert.Contains("A decimal must be finite: a NaN and an infinity are not values of CEL.", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public static void TryGetDecimal_OfEveryOtherKind_IsFalse_WithTheZeroDecimal128()
    {
        CelValue[] others =
        [
            CelValue.Null,
            CelValue.FromBoolean(true),
            CelValue.FromInt64(1),
            CelValue.FromUInt64(1),
            CelValue.FromDouble(1.5),
            CelValue.FromString("1.5"),
            CelValue.FromBytes(OneByte),
            CelValue.FromList(CelList.Create(CelValue.FromInt64(1))),
            CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromInt64(1))])),
            CelValue.FromType(CelType.Int),
            CelValue.FromTimestamp(new CelTimestamp(1, 0)),
            CelValue.FromDuration(new CelDuration(1)),
            CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1))),
        ];

        foreach (CelValue value in others)
        {
            Assert.False(value.TryGetDecimal(out Decimal128 number));
            Assert.Equal(default(Decimal128), number);
        }

        Assert.True(OneAndHalf.TryGetDecimal(out Decimal128 found));
        Assert.Equal(CelValueKind.Decimal, OneAndHalf.Kind);
        Assert.Equal(new DecimalParts(false, 15UL, -1), DecimalParts.From(found));
    }

    // The digits, the exponent and the sign of a zero stay as they are, since the value is its 128 bits.
    [Fact]
    public static void DecimalValue_KeepsTheCohortTheExponentAndTheSignOfAZero()
    {
        DecimalParts[] cases =
        [
            new(false, 150UL, -2),
            new(false, 15UL, -1),
            new(true, 0UL, 0),
            new(false, 0UL, 0),
            new(false, 0UL, -5),
            new(false, 0UL, 3),
            new(true, DecimalParts.MaxCoefficient, DecimalParts.MaxExponent),
            new(false, 1UL, DecimalParts.MinExponent),
            new(false, 100UL, 0),
            new(false, 1UL, 2),
        ];

        foreach (DecimalParts parts in cases)
        {
            Assert.Equal(parts, Parts(CelValue.FromDecimal(parts.ToDecimal128())));
        }
    }

    [Fact]
    public static void NetEquality_IsByValue_AndTheHashIsTheSameForTheCohortsOfAValueAndForBothZeros()
    {
        CelValue[][] same =
        [
            [CelValue.FromDecimal(Make(150, -2)), CelValue.FromDecimal(Make(15, -1)), CelValue.FromDecimal(Make(1500, -3))],
            [CelValue.FromDecimal(Make(1, 2)), CelValue.FromDecimal(Make(100, 0)), CelValue.FromDecimal(Make(10, 1))],
            [CelValue.FromDecimal(Make(0, 0)), CelValue.FromDecimal(Make(0, 0, negative: true)), CelValue.FromDecimal(Make(0, -4)), CelValue.FromDecimal(Make(0, 6111))],
        ];

        foreach (CelValue[] cohorts in same)
        {
            foreach (CelValue left in cohorts)
            {
                foreach (CelValue right in cohorts)
                {
                    Assert.True(left == right);
                    Assert.True(left.Equals(right));
                    Assert.True(left.Equals((object)right));
                    Assert.Equal(left.GetHashCode(), right.GetHashCode());
                }
            }
        }

        Assert.True(CelValue.FromDecimal(Make(15, -1)) != CelValue.FromDecimal(Make(25, -1)));
        Assert.False(CelValue.FromDecimal(Make(15, -1)).Equals(CelValue.FromDecimal(Make(15, -1, negative: true))));
        Assert.True(CelValue.FromDecimal(Make(1, 0)) != CelValue.FromDecimal(Make(1, 1)));
    }

    [Fact]
    public static void NetEquality_OfADecimalAndAValueOfAnotherKind_IsFalse()
    {
        CelValue one = CelValue.FromDecimal(Make(1, 0));

        Assert.False(one.Equals(CelValue.FromInt64(1)));
        Assert.False(one.Equals(CelValue.FromUInt64(1)));
        Assert.False(one.Equals(CelValue.FromDouble(1)));
        Assert.False(one.Equals(CelValue.FromString("1")));
        Assert.False(one.Equals(CelValue.Null));
        Assert.False(CelValue.FromInt64(1).Equals(one));
        Assert.False(CelValue.Null.Equals(one));
    }

    // A value made from a System.Decimal in a place of the type dyn carries a mark that only the text of an error reads: equality, the
    // hash and the reading ignore it, and the value from a Decimal128 has none.
    [Fact]
    public static void TheMarkOfSystemDecimal_IsIgnoredByEqualityTheHashAndTheReading()
    {
        CelValue marked = CelValue.FromSystemDecimalOrigin(Make(150, -2));
        CelValue plain = CelValue.FromDecimal(Make(150, -2));

        Assert.True(marked.IsFromSystemDecimal);
        Assert.False(plain.IsFromSystemDecimal);
        Assert.False(CelValue.FromInt64(1).IsFromSystemDecimal);
        Assert.False(CelValue.Null.IsFromSystemDecimal);
        Assert.True(marked == plain);
        Assert.Equal(plain.GetHashCode(), marked.GetHashCode());
        Assert.Equal(Parts(plain), Parts(marked));
        Assert.Equal(ValueEquality.Equal, EqualityRules.AreEqual(marked, plain, null, out _));
    }

    [Fact]
    public static void BoxedDecimals_AreDecimalValues_AndOnlyTheOneFromASystemDecimalIsMarked()
    {
        Assert.True(ConversionRules.TryFromClr<object>(1.50m, out CelValue fromSystem));
        Assert.True(ConversionRules.TryFromClr<object>(Make(150, -2), out CelValue fromExact));

        Assert.Equal(new DecimalParts(false, 150UL, -2), Parts(fromSystem));
        Assert.Equal(new DecimalParts(false, 150UL, -2), Parts(fromExact));
        Assert.True(fromSystem.IsFromSystemDecimal);
        Assert.False(fromExact.IsFromSystemDecimal);
        Assert.True(fromSystem == fromExact);
    }

    [Fact]
    public static void TypeAndNameOfADecimal_AreDecimal_ForEveryCohort()
    {
        foreach (CelValue value in new[] { OneAndHalf, CelValue.FromDecimal(Make(0, 0, negative: true)) })
        {
            Assert.Equal(CelType.Decimal, ValueTypeRules.TypeOf(value));
            Assert.Equal("decimal", ValueTypeRules.NameOf(value, null));
        }

        Assert.Equal("decimal", ValueTypeNames.Of(CelValueKind.Decimal));
        Assert.Equal(CelTypeKind.Decimal, CelType.Decimal.Kind);
        Assert.Equal("decimal", CelType.Decimal.Name);
        Assert.Equal("decimal", CelType.Decimal.ToString());
        Assert.True(CelType.Decimal.Parameters.IsEmpty);
        Assert.NotEqual(CelType.Double, CelType.Decimal);
    }

    // A decimal fits decimal and the types that take any value; the value null is no decimal, though the static null is assignable.
    [Fact]
    public static void FitsTopLevel_AcceptsADecimalAsDecimalAndAsAnyType_AndRefusesEveryOtherType()
    {
        CelType[] fits = [CelType.Decimal, CelType.Dyn, CelType.Any];
        CelType[] refuses =
        [
            CelType.Int, CelType.UInt, CelType.Double, CelType.String, CelType.Bool, CelType.Null, CelType.Bytes, CelType.Timestamp,
            CelType.ListOf(CelType.Dyn), CelType.MapOf(CelType.Dyn, CelType.Dyn), CelType.WrapperOf(CelType.Int), CelType.OptionalOf(CelType.Decimal),
        ];

        foreach (CelType type in fits)
        {
            Assert.True(ValueTypeRules.FitsTopLevel(OneAndHalf, type));
            Assert.True(ValueTypeRules.Fits(OneAndHalf, type, null).IsFit);
        }

        foreach (CelType type in refuses)
        {
            Assert.False(ValueTypeRules.FitsTopLevel(OneAndHalf, type));
        }

        Assert.False(ValueTypeRules.FitsTopLevel(CelValue.Null, CelType.Decimal));
        Assert.False(ValueTypeRules.FitsTopLevel(CelValue.FromInt64(1), CelType.Decimal));
        Assert.False(ValueTypeRules.FitsTopLevel(CelValue.FromDouble(1.5), CelType.Decimal));
        Assert.Equal("int", ValueTypeRules.MismatchType(CelValue.FromInt64(1), CelType.Decimal));
        Assert.Equal("decimal", ValueTypeRules.MismatchType(OneAndHalf, CelType.Int));
        Assert.Null(ValueTypeRules.MismatchType(OneAndHalf, CelType.Decimal));
    }

    [Fact]
    public static void OrderingRules_OrderDecimalsByTheNumber_AndGiveNoOrderWithAnotherKind()
    {
        (CelValue Left, CelValue Right, ValueOrdering Expected)[] cases =
        [
            (CelValue.FromDecimal(Make(150, -2)), CelValue.FromDecimal(Make(15, -1)), ValueOrdering.Equal),
            (CelValue.FromDecimal(Make(0, 0, negative: true)), CelValue.FromDecimal(Make(0, 0)), ValueOrdering.Equal),
            (CelValue.FromDecimal(Make(0, 5)), CelValue.FromDecimal(Make(0, -5, negative: true)), ValueOrdering.Equal),
            (CelValue.FromDecimal(Make(1, 0)), CelValue.FromDecimal(Make(2, 0)), ValueOrdering.Less),
            (CelValue.FromDecimal(Make(2, 0)), CelValue.FromDecimal(Make(1, 0)), ValueOrdering.Greater),
            (CelValue.FromDecimal(Make(1, 2)), CelValue.FromDecimal(Make(99, 0)), ValueOrdering.Greater),
            (CelValue.FromDecimal(Make(5, -1, negative: true)), CelValue.FromDecimal(Make(1, -6176)), ValueOrdering.Less),
            (CelValue.FromDecimal(Make(1, -6176)), CelValue.FromDecimal(Make(0, 0)), ValueOrdering.Greater),
            (CelValue.FromDecimal(Make(1, 0)), CelValue.FromInt64(1), ValueOrdering.None),
            (CelValue.FromInt64(1), CelValue.FromDecimal(Make(1, 0)), ValueOrdering.None),
            (CelValue.FromDecimal(Make(1, 0)), CelValue.FromUInt64(1), ValueOrdering.None),
            (CelValue.FromDecimal(Make(1, 0)), CelValue.FromDouble(1), ValueOrdering.None),
            (CelValue.FromDecimal(Make(1, 0)), CelValue.FromString("1"), ValueOrdering.None),
            (CelValue.FromDecimal(Make(1, 0)), CelValue.Null, ValueOrdering.None),
        ];

        foreach ((CelValue left, CelValue right, ValueOrdering expected) in cases)
        {
            Assert.Equal(expected, OrderingRules.Compare(left, right));
        }

        Assert.False(OrderingRules.IsNumber(CelValueKind.Decimal));
    }

    // A decimal is no key: a lookup finds nothing and CelMap.Create refuses it.
    [Fact]
    public static void ADecimal_IsNoMapKey_AndHasNoHashOfAKey()
    {
        Assert.False(KeyHashRules.IsKeyKind(CelValueKind.Decimal));
        Assert.False(KeyHashRules.TryGetHash(OneAndHalf, out int hash));
        Assert.Equal(0, hash);
        Assert.False(EqualityRules.KeysEqual(OneAndHalf, OneAndHalf));
        Assert.False(EqualityRules.KeysEqual(CelValue.FromInt64(1), CelValue.FromDecimal(Make(1, 0))));
        Assert.False(EqualityRules.KeysEqual(CelValue.FromDecimal(Make(1, 0)), CelValue.FromInt64(1)));
        Assert.Throws<ArgumentException>(() => CelMap.Create([new KeyValuePair<CelValue, CelValue>(OneAndHalf, CelValue.FromInt64(1))]));

        CelMap map = CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromInt64(1), CelValue.FromInt64(1))]);

        Assert.False(map.TryGetValue(CelValue.FromDecimal(Make(1, 0)), out _));
    }

    [Fact]
    public static void InputType_OfEveryRowOfDecimal_IsDecimal_AndOfATypedCollectionOfDecimalsThereIsNone()
    {
        Assert.Equal(CelType.Decimal, ConversionRules.InputType<Decimal128>());
        Assert.Equal(CelType.Decimal, ConversionRules.InputType<decimal>());
        Assert.Equal(CelType.Decimal, ConversionRules.InputType<Decimal128?>());
        Assert.Equal(CelType.Decimal, ConversionRules.InputType<decimal?>());
        Assert.Equal(CelType.Decimal, ConversionRules.ResultInputType<decimal>());

        Assert.Null(ConversionRules.InputType<IReadOnlyList<Decimal128>>());
        Assert.Null(ConversionRules.InputType<IReadOnlyList<decimal>>());
        Assert.Null(ConversionRules.InputType<Decimal128[]>());
        Assert.Null(ConversionRules.InputType<List<decimal?>>());
        Assert.Null(ConversionRules.InputType<IReadOnlyDictionary<string, Decimal128>>());
        Assert.Null(ConversionRules.InputType<ISet<decimal>>());
    }

    // The nullable form of Decimal128 and the result of a typed function give the error value of a NaN and of an infinity, and
    // CelBindings.Add refuses the nullable form, where the value would enter a variable, as it refuses the plain one; a boxed one it
    // takes, for the node that reads it places the error.
    [Fact]
    public static void ANaNAndAnInfinity_OfTheOtherRows_AreTheErrorValueConversionRange_AndAddRefusesTheNullableForm()
    {
        (Decimal128 Value, string Name)[] cases =
        [
            (Decimal128.NaN, "NaN"),
            (Decimal128.PositiveInfinity, "+Inf"),
            (Decimal128.NegativeInfinity, "-Inf"),
        ];

        foreach ((Decimal128 value, string name) in cases)
        {
            Assert.True(ConversionRules.TryFromClr<Decimal128?>(value, out CelValue nullable));
            AssertError(CelErrorCode.ConversionRange, name + " is out of the range of decimal", nullable);
            AssertError(CelErrorCode.ConversionRange, name + " is out of the range of decimal", ConversionRules.FromDecimal128(value));
            AssertError(CelErrorCode.ConversionRange, name + " is out of the range of decimal", ConversionRules.FromResult(value, "f", null));

            Assert.Throws<ArgumentException>(() => new CelBindings().Add<Decimal128?>("a", value));
            _ = new CelBindings().Add<object>("a", value);
        }
    }

    [Fact]
    public static void ExpectedType_OfEveryRowOfDecimal_IsDecimal_AndAnExplicitTypeMustHoldIt()
    {
        Assert.Equal(CelType.Decimal, ConversionRules.ExpectedType<Decimal128>(null, "type"));
        Assert.Equal(CelType.Decimal, ConversionRules.ExpectedType<decimal>(null, "type"));
        Assert.Equal(CelType.Decimal, ConversionRules.ExpectedType<Decimal128?>(null, "type"));
        Assert.Equal(CelType.Decimal, ConversionRules.ExpectedType<decimal?>(null, "type"));
        Assert.Equal(CelType.Decimal, ConversionRules.ExpectedType<Decimal128>(CelType.Decimal, "type"));
        Assert.Equal(CelType.Dyn, ConversionRules.ExpectedType<decimal>(CelType.Dyn, "type"));
        Assert.Equal(CelType.Decimal, ConversionRules.ExpectedType<object>(CelType.Decimal, "type"));
        Assert.Equal(CelType.Decimal, ConversionRules.ExpectedType<CelValue>(CelType.Decimal, "type"));
        Assert.Equal(CelType.Decimal, ConversionRules.ParameterType<Decimal128>());
        Assert.Equal(CelType.Decimal, ConversionRules.ParameterType<decimal?>());

        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<Decimal128>(CelType.Int, "type"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<long>(CelType.Decimal, "type"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<double>(CelType.Decimal, "type"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<IReadOnlyList<Decimal128>>(null, "type"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<IReadOnlyList<decimal>>(null, "type"));
    }

    // A parameter of a typed delegate goes by the result rows, and a value outside the range of System.Decimal is refused by the row:
    // the caller makes ArgumentConversion (3024) of it. Decimal128 takes any decimal as it is.
    [Fact]
    public static void ParameterRows_RefuseAValueOutsideTheRangeOfSystemDecimal_AndTakeDecimal128AsItIs()
    {
        CelValue tooLarge = CelValue.FromDecimal(Make(1, 29));

        Assert.False(ConversionRules.TryToParameter(tooLarge, null, out decimal _));
        Assert.True(ConversionRules.TryToParameter(tooLarge, null, out Decimal128 exact));
        Assert.Equal(new DecimalParts(false, 1UL, 29), DecimalParts.From(exact));
        Assert.True(ConversionRules.TryToParameter(OneAndHalf, null, out decimal fits));
        Assert.Equal(1.5m, fits);
        Assert.False(ConversionRules.TryToParameter(CelValue.FromInt64(1), null, out Decimal128 _));
        Assert.False(ConversionRules.TryToParameter(CelValue.Null, null, out Decimal128 _));
        Assert.True(ConversionRules.TryToParameter(CelValue.Null, null, out Decimal128? nothing));
        Assert.Null(nothing);
    }

    // The members that an object type calls for a field of the type decimal.
    [Fact]
    public static void ObjectTypeMembers_ConvertTheFieldsExactly_AndNameTheFieldOfAValueThatIsNotFinite()
    {
        var type = new PriceType();
        UInt128 largest = UInt128.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);

        Assert.True(type.Belongs(new Price()));
        Assert.Equal(new DecimalParts(false, 150UL, -2), Parts(type.Exact(0, Make(150, -2))));
        Assert.Equal(new DecimalParts(true, UInt128.Zero, 0), Parts(type.Exact(0, Make(0, 0, negative: true))));
        Assert.Equal(new DecimalParts(false, 150UL, -2), Parts(PriceType.Scaled(1.50m)));
        Assert.Equal(new DecimalParts(true, UInt128.Zero, -2), Parts(PriceType.Scaled(new decimal(0, 0, 0, true, 2))));
        Assert.Equal(new DecimalParts(true, largest, 0), Parts(PriceType.Scaled(decimal.MinValue)));

        AssertError(CelErrorCode.ConversionRange, "NaN is out of the range of decimal", type.Exact(0, Decimal128.NaN));
        AssertError(CelErrorCode.ConversionRange, "+Inf is out of the range of decimal", type.Exact(0, Decimal128.PositiveInfinity));
        AssertError(CelErrorCode.ConversionRange, "-Inf is out of the range of decimal", type.Exact(0, Decimal128.NegativeInfinity));
    }

    private static Decimal128 Make(ulong coefficient, int exponent, bool negative = false) =>
        new DecimalParts(negative, coefficient, exponent).ToDecimal128();

    private static DecimalParts Parts(CelValue value)
    {
        Assert.True(value.TryGetDecimal(out Decimal128 number));
        return DecimalParts.From(number);
    }

    private static void AssertError(CelErrorCode code, string message, CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error));
        Assert.Equal((code, message), (error.Code, error.Message));
    }

    // A description of a type with the field price, as the source generator would write it.
    private sealed class PriceType() : CelObjectType("acme.Price", typeof(Price), [new(0, "price", CelType.Decimal)])
    {
        internal static CelValue Scaled(decimal value) => FromDecimal(value);

        internal CelValue Exact(int number, Decimal128 value) => FromDecimal(number, value);

        protected internal override CelValue GetField(object instance, int number) => default;

        protected internal override bool HasField(object instance, int number) => false;
    }

    private sealed class Price;
}
#endif
