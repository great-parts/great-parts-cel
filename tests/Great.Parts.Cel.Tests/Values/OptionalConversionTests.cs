using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The rows of optional_type(E) and CelOptional<X> of the type mapping: the CEL type of a .NET type, the input conversion, the result
// conversion with its refusals, and the expected type. The checked results are in the examples of the facade.
public sealed class OptionalConversionTests
{
    private static readonly CelType OptionalOfInt = CelType.OptionalOf(CelType.Int);
    private static readonly CelType OptionalOfDyn = CelType.OptionalOf(CelType.Dyn);
    private static readonly long[] OneTwoOfLong = [1, 2];
    private static readonly int[] OneTwoOfInt = [1, 2];

    private enum BigEnum : ulong
    {
        Big = ulong.MaxValue,
    }

    [Fact]
    public void InputType_OfCelOptional_IsTheOptionalTypeOfTheTypeOfItsArgument()
    {
        Assert.Equal(OptionalOfInt, ConversionRules.InputType<CelOptional<long>>());
        Assert.Equal(OptionalOfInt, ConversionRules.InputType<CelOptional<short>>());
        Assert.Equal(CelType.OptionalOf(CelType.String), ConversionRules.InputType<CelOptional<string>>());
        Assert.Equal(CelType.OptionalOf(CelType.String), ConversionRules.InputType<CelOptional<Guid>>());
        Assert.Equal(OptionalOfDyn, ConversionRules.InputType<CelOptional<CelValue>>());
        Assert.Equal(OptionalOfDyn, ConversionRules.InputType<CelOptional<object>>());
        Assert.Equal(CelType.OptionalOf(CelType.ListOf(CelType.String)), ConversionRules.InputType<CelOptional<IReadOnlyList<string>>>());
        Assert.Equal(CelType.OptionalOf(OptionalOfInt), ConversionRules.InputType<CelOptional<CelOptional<long>>>());
        Assert.Null(ConversionRules.InputType<CelOptional<Uri>>());
    }

    [Fact]
    public void FromClr_OfAnEmptyCelOptional_IsTheEmptyOptional()
    {
        Assert.True(ConversionRules.TryFromClr(default(CelOptional<long>), out CelValue value));

        Assert.True(value.TryGetOptional(out CelOptional<CelValue> optional));
        Assert.False(optional.HasValue);
    }

    [Fact]
    public void FromClr_OfAHeldValue_IsTheOptionalOfTheConvertedValue()
    {
        Assert.True(ConversionRules.TryFromClr(new CelOptional<long>(5), out CelValue number));
        Assert.True(ConversionRules.TryFromClr(new CelOptional<string>("a"), out CelValue text));
        Assert.True(ConversionRules.TryFromClr(new CelOptional<CelOptional<long>>(default), out CelValue nested));

        Assert.True(HeldOf(number).TryGetInt64(out long five) && five == 5);
        Assert.True(HeldOf(text).TryGetString(out string? a) && a == "a");
        Assert.Equal(CelValueKind.Optional, HeldOf(nested).Kind);
    }

    [Fact]
    public void FromClr_OfAReferenceThatIsNull_IsAnOptionalThatHoldsNull()
    {
        Assert.True(ConversionRules.TryFromClr(new CelOptional<string?>(null), out CelValue value));

        Assert.True(value.TryGetOptional(out CelOptional<CelValue> optional));
        Assert.True(optional.HasValue);
        Assert.Equal(CelValueKind.Null, optional.Value.Kind);
    }

    [Fact]
    public void FromClr_OfABoxedCelOptional_ByObject_ConvertsItsHeldValue()
    {
        object boxed = new CelOptional<object?>(7L);
        object boxedCelValue = new CelOptional<CelValue>(CelValue.FromString("b"));

        Assert.True(ConversionRules.TryFromClr(boxed, out CelValue number));
        Assert.True(ConversionRules.TryFromClr(boxedCelValue, out CelValue text));

        Assert.True(HeldOf(number).TryGetInt64(out long seven) && seven == 7);
        Assert.True(HeldOf(text).TryGetString(out string? b) && b == "b");
    }

    [Fact]
    public void FromClr_OfACelOptionalWhoseArgumentHasNoRow_IsRefused()
    {
        Assert.False(ConversionRules.TryFromClr(new CelOptional<Uri>(new Uri("https://example.org")), out _));
    }

    [Fact]
    public void FromClr_WhenTheHeldValueConvertsToAnErrorValue_IsThatErrorValue()
    {
        Assert.True(ConversionRules.TryFromClr(new CelOptional<BigEnum>(BigEnum.Big), out CelValue value));

        Assert.True(value.TryGetError(out CelError? error));
        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
    }

    [Fact]
    public void ToResult_OfAnEmptyOptional_IsDefault_AndOfAValue_IsTheConvertedValue()
    {
        CelValue none = CelValue.FromOptional(default);
        CelValue five = Held(CelValue.FromInt64(5));

        Assert.True(ConversionRules.TryToResult(none, OptionalOfInt, out CelOptional<long> empty, out _, out _));
        Assert.True(ConversionRules.TryToResult(five, OptionalOfInt, out CelOptional<long> full, out _, out _));

        Assert.Equal(default, empty);
        Assert.Equal(new CelOptional<long>(5), full);
    }

    [Fact]
    public void ToResult_OfNull_IsAValueThatIsNull_WherePositionTakesNull_AndARefusalWhereItDoesNot()
    {
        CelValue ofNull = Held(CelValue.Null);

        Assert.True(ConversionRules.TryToResult(ofNull, OptionalOfDyn, out CelOptional<string> text, out _, out _));
        Assert.True(ConversionRules.TryToResult(ofNull, OptionalOfDyn, out CelOptional<byte[]> bytes, out _, out _));
        Assert.True(ConversionRules.TryToResult(ofNull, OptionalOfDyn, out CelOptional<long?> number, out _, out _));
        Assert.True(text.HasValue);
        Assert.Null(text.Value);
        Assert.True(bytes.HasValue);
        Assert.Null(bytes.Value);
        Assert.True(number.HasValue);
        Assert.Null(number.Value);
        Assert.False(ConversionRules.TryToResult(ofNull, OptionalOfDyn, out CelOptional<long> _, out string? actualType, out _));
        Assert.Equal("optional_type(null_type)", actualType);
        Assert.False(ConversionRules.TryToResult(ofNull, OptionalOfDyn, out CelOptional<ReadOnlyMemory<byte>> _, out _, out _));
    }

    [Fact]
    public void ToResult_OfAValueThatIsNoOptional_IsARefusal()
    {
        Assert.False(ConversionRules.TryToResult(CelValue.Null, OptionalOfDyn, out CelOptional<string> _, out string? nullType, out _));
        Assert.False(ConversionRules.TryToResult(CelValue.FromInt64(1), OptionalOfDyn, out CelOptional<long> _, out string? intType, out _));

        Assert.Equal("null_type", nullType);
        Assert.Equal("int", intType);
    }

    [Fact]
    public void ToResult_WhenTheHeldValueDoesNotConvert_IsARefusalThatNamesTheValue()
    {
        CelValue large = Held(CelValue.FromInt64(4294967296));
        CelValue text = Held(CelValue.FromString("a"));

        Assert.False(ConversionRules.TryToResult(large, OptionalOfInt, out CelOptional<int> _, out string? largeType, out _));
        Assert.False(ConversionRules.TryToResult(text, OptionalOfDyn, out CelOptional<long> _, out string? textType, out _));

        Assert.Equal("optional_type(int)", largeType);
        Assert.Equal("optional_type(string)", textType);
    }

    [Fact]
    public void ToResult_OfAnOptionalOfAnOptional_KeepsBothLevels()
    {
        CelType nested = CelType.OptionalOf(OptionalOfInt);
        CelValue both = Held(Held(CelValue.FromInt64(1)));
        CelValue inner = Held(CelValue.FromOptional(default));

        Assert.True(ConversionRules.TryToResult(both, nested, out CelOptional<CelOptional<long>> full, out _, out _));
        Assert.True(ConversionRules.TryToResult(inner, nested, out CelOptional<CelOptional<long>> emptyInside, out _, out _));

        Assert.Equal(new CelOptional<CelOptional<long>>(new CelOptional<long>(1)), full);
        Assert.True(emptyInside.HasValue);
        Assert.False(emptyInside.Value.HasValue);
    }

    [Fact]
    public void ToResult_ByObject_IsACelOptionalOfObject_WithTheNaturalMappingOfTheValue()
    {
        Assert.True(ConversionRules.TryToResult(Held(CelValue.FromInt64(1)), OptionalOfInt, out object? some, out _, out _));
        Assert.True(ConversionRules.TryToResult(CelValue.FromOptional(default), OptionalOfDyn, out object? none, out _, out _));
        Assert.True(ConversionRules.TryToResult(Held(CelValue.Null), OptionalOfDyn, out object? ofNull, out _, out _));

        Assert.Equal(new CelOptional<object?>(1L), Assert.IsType<CelOptional<object?>>(some));
        Assert.Equal(default, Assert.IsType<CelOptional<object?>>(none));
        Assert.True(Assert.IsType<CelOptional<object?>>(ofNull).HasValue);
    }

    [Fact]
    public void ToResult_OfAListThatCannotFail_IsALazyView_AndOtherwiseACopy()
    {
        CelValue numbers = Held(CelValue.FromList(CelList.Create([CelValue.FromInt64(1), CelValue.FromInt64(2)])));
        CelType optionalOfList = CelType.OptionalOf(CelType.ListOf(CelType.Int));

        Assert.True(ConversionRules.TryToResult(numbers, optionalOfList, out CelOptional<IReadOnlyList<long>> view, out _, out _));
        Assert.True(ConversionRules.TryToResult(numbers, optionalOfList, out CelOptional<IReadOnlyList<int>> copy, out _, out _));

        Assert.Equal(OneTwoOfLong, view.Value);
        Assert.Contains("ListView", view.Value.GetType().Name, StringComparison.Ordinal);
        Assert.Equal(OneTwoOfInt, copy.Value);
        Assert.DoesNotContain("ListView", copy.Value.GetType().Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ToResult_OfAListWithAnElementThatDoesNotConvert_IsARefusal()
    {
        CelValue large = Held(CelValue.FromList(CelList.Create([CelValue.FromInt64(4294967296)])));

        Assert.False(ConversionRules.TryToResult(large, CelType.OptionalOf(CelType.ListOf(CelType.Int)), out CelOptional<IReadOnlyList<int>> _, out _, out _));
    }

    [Fact]
    public void ExpectedType_OfCelOptional_IsTheOptionalTypeOfTheExpectedTypeOfItsArgument()
    {
        Assert.Equal(OptionalOfInt, ConversionRules.ExpectedType<CelOptional<long>>(null, "p"));
        Assert.Equal(OptionalOfInt, ConversionRules.ExpectedType<CelOptional<int>>(null, "p"));
        Assert.Equal(OptionalOfDyn, ConversionRules.ExpectedType<CelOptional<object>>(null, "p"));
        Assert.Equal(OptionalOfDyn, ConversionRules.ExpectedType<CelOptional<CelValue>>(null, "p"));
        Assert.Equal(CelType.OptionalOf(CelType.WrapperOf(CelType.Int)), ConversionRules.ExpectedType<CelOptional<long?>>(null, "p"));
        Assert.Equal(
            CelType.OptionalOf(CelType.MapOf(CelType.String, CelType.Int)),
            ConversionRules.ExpectedType<CelOptional<IReadOnlyDictionary<string, long>>>(null, "p"));
        Assert.Equal(CelType.OptionalOf(OptionalOfInt), ConversionRules.ExpectedType<CelOptional<CelOptional<long>>>(null, "p"));
    }

    [Fact]
    public void ExpectedType_WithAnExplicitType_IsKeptWhenTheRowHoldsItAndRefusedOtherwise()
    {
        Assert.Equal(OptionalOfInt, ConversionRules.ExpectedType<CelOptional<long>>(OptionalOfInt, "p"));
        Assert.Equal(OptionalOfInt, ConversionRules.ExpectedType<CelValue>(OptionalOfInt, "p"));
        Assert.Equal(CelType.Dyn, ConversionRules.ExpectedType<CelOptional<long>>(CelType.Dyn, "p"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<CelOptional<long>>(CelType.OptionalOf(CelType.String), "p"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<CelOptional<long>>(CelType.Int, "p"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<long>(OptionalOfInt, "p"));
    }

    private static CelValue Held(CelValue value) => CelValue.FromOptional(new CelOptional<CelValue>(value));

    private static CelValue HeldOf(CelValue optional)
    {
        Assert.True(optional.TryGetOptional(out CelOptional<CelValue> held));
        return held.Value;
    }
}
