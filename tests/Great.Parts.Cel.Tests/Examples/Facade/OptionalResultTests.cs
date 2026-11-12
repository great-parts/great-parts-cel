using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The row of the table of results for optional_type(E) (the type mapping page, table 8.1, and the table 8.2 of the expected
// types): the value of the root becomes CelOptional<X>, where E becomes X by the table; optional.none() and
// optional.of(null) stay different (an empty CelOptional<X>, which is default(CelOptional<X>), and one that has the value
// null); the content is converted as a value of its own type is, and one that the row of its type does not convert is
// ResultConversion at the root, at the "(" of a call that is the root. The expected type that T gives is optional_type(E'),
// so an expression of another type is ExpectedTypeMismatch at the root, and T that is CelValue or object takes any
// optional: CelValue as the value of the kind Optional, object as CelOptional<object>.
[Trait("Requirement", "+R-10")]
[Trait("Requirement", "F-8")]
public sealed class OptionalResultTests
{
    private static readonly long[] OneTwo = [1, 2];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void OptionalWithAValue_IsACelOptionalOfTheConvertedValue()
    {
        CelOptional<long> number = Evaluate<CelOptional<long>>("optional.of(5)").GetValueOrThrow();
        CelOptional<string> text = Evaluate<CelOptional<string>>("optional.of('a')").GetValueOrThrow();
        CelOptional<IReadOnlyList<long>> list = Evaluate<CelOptional<IReadOnlyList<long>>>("optional.of([1, 2])").GetValueOrThrow();

        Assert.Equal((true, 5L), (number.HasValue, number.Value));
        Assert.Equal((true, "a"), (text.HasValue, text.Value));
        Assert.True(list.HasValue);
        Assert.Equal(OneTwo, list.Value);
        Assert.Equal(new CelOptional<long>(5), number);
    }

    [Fact]
    public void NoneAndOfNull_AreDifferentResults()
    {
        CelOptional<string> none = Evaluate<CelOptional<string>>("optional.none()").GetValueOrThrow();
        CelOptional<object> noneObject = Evaluate<CelOptional<object>>("optional.none()").GetValueOrThrow();
        CelOptional<object> ofNull = Evaluate<CelOptional<object>>("optional.of(null)").GetValueOrThrow();

        Assert.False(none.HasValue);
        Assert.Equal(default(CelOptional<string>), none);
        Assert.False(noneObject.HasValue);
        Assert.Equal(default(CelOptional<object>), noneObject);
        Assert.True(ofNull.HasValue);
        Assert.Null(ofNull.Value);
        Assert.NotEqual(noneObject, ofNull);
    }

    // The null that an optional holds is read as an element of a copy is (the type mapping page, table 8.1): a
    // position that the view gives as string, byte[], CelList, CelMap or CelType takes it, and so does Nullable<X>, so the
    // result has a value, which is null, and differs from optional.none(), which is default. The expression is
    // optional.of(dyn(null)), of the type optional_type(dyn): optional.of(null) has optional_type(null_type), which no
    // CelOptional<X> but object and CelValue takes at the check.
    [Fact]
    public void OfNull_IsAValueThatIsNull_WhereThePositionIsAReferenceType()
    {
        CelOptional<string> text = Evaluate<CelOptional<string>>("optional.of(dyn(null))").GetValueOrThrow();
        CelOptional<byte[]> bytes = Evaluate<CelOptional<byte[]>>("optional.of(dyn(null))").GetValueOrThrow();
        CelOptional<CelList> list = Evaluate<CelOptional<CelList>>("optional.of(dyn(null))").GetValueOrThrow();
        CelOptional<CelMap> map = Evaluate<CelOptional<CelMap>>("optional.of(dyn(null))").GetValueOrThrow();
        CelOptional<CelType> type = Evaluate<CelOptional<CelType>>("optional.of(dyn(null))").GetValueOrThrow();
        CelOptional<long?> number = Evaluate<CelOptional<long?>>("optional.of(dyn(null))").GetValueOrThrow();

        Assert.True(text.HasValue);
        Assert.Null(text.Value);
        Assert.NotEqual(default, text);
        Assert.True(bytes.HasValue);
        Assert.Null(bytes.Value);
        Assert.True(list.HasValue);
        Assert.Null(list.Value);
        Assert.True(map.HasValue);
        Assert.Null(map.Value);
        Assert.True(type.HasValue);
        Assert.Null(type.Value);
        Assert.True(number.HasValue);
        Assert.Null(number.Value);
        Assert.NotEqual(default, number);
    }

    // A position of a value type takes no null: ResultConversion at the root, the "(" of the call at the offset 11.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData(typeof(long))]
    [InlineData(typeof(ReadOnlyMemory<byte>))]
    public void OfNull_WherePositionIsAValueType_IsResultConversionAtTheRoot(System.Type held)
    {
        CelError error = held == typeof(long)
            ? Assert.Single(Evaluate<CelOptional<long>>("optional.of(dyn(null))").Errors)
            : Assert.Single(Evaluate<CelOptional<ReadOnlyMemory<byte>>>("optional.of(dyn(null))").Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 11), (error.Code, error.Stage, error.Location.Offset));
        Assert.StartsWith("result of type 'optional_type(", error.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalOfAnOptional_KeepsBothLevels()
    {
        CelOptional<CelOptional<long>> both = Evaluate<CelOptional<CelOptional<long>>>("optional.of(optional.of(1))").GetValueOrThrow();
        CelOptional<CelOptional<long>> inner = Evaluate<CelOptional<CelOptional<long>>>("optional.of(optional.none())").GetValueOrThrow();
        CelOptional<CelOptional<long>> outer = Evaluate<CelOptional<CelOptional<long>>>("optional.none()").GetValueOrThrow();

        Assert.True(both.HasValue);
        Assert.Equal(new CelOptional<long>(1), both.Value);
        Assert.True(inner.HasValue);
        Assert.False(inner.Value.HasValue);
        Assert.False(outer.HasValue);
    }

    // T = object gives CelOptional<object> with the natural mapping of the value; T = CelValue gives the value of the kind
    // Optional.
    [Fact]
    public void ObjectAndCelValue_TakeAnyOptional()
    {
        object some = Evaluate<object>("optional.of(1)").GetValueOrThrow()!;
        object none = Evaluate<object>("optional.none()").GetValueOrThrow()!;
        CelValue value = Evaluate<CelValue>("optional.of('a')").GetValueOrThrow();

        Assert.Equal(new CelOptional<object?>(1L), Assert.IsType<CelOptional<object?>>(some));
        Assert.Equal(default(CelOptional<object?>), Assert.IsType<CelOptional<object?>>(none));
        Assert.Equal(CelValueKind.Optional, value.Kind);
        Assert.True(value.TryGetOptional(out CelOptional<CelValue> optional));
        Assert.True(optional.HasValue);
        Assert.True(optional.Value.TryGetString(out string? text) && text == "a");
    }

    // The type of the program is that of the expression, optional_type(int), whichever T the root is asked as. A free type
    // variable of the root, as in optional.none(), is bound by the check against the expected type, so it is the type that T
    // gives: optional_type(int) for CelOptional<long>, as [] with list(int) is list(int) (the page of checking, "Expected type").
    [Fact]
    public void ResultType_IsTheOptionalType()
    {
        Assert.Equal(CelType.OptionalOf(CelType.Int), environment.Compile<CelOptional<long>>("optional.of(1)").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.OptionalOf(CelType.Int), environment.Compile<CelValue>("optional.of(1)").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.OptionalOf(CelType.Int), environment.Compile<CelOptional<long>>("optional.none()").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.OptionalOf(CelType.Dyn), environment.Compile<CelValue>("optional.none()").GetProgramOrThrow().ResultType);
    }

    // The content that the row of its type does not convert: 2^32 does not fit System.Int32, a string is no long.
    // The root is the call, so the place is its "(" at the offset 11.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("optional.of(4294967296)")]
    [InlineData("optional.of(dyn(4294967296))")]
    public void ContentThatDoesNotConvert_IsResultConversionAtTheRoot(string expression)
    {
        CelError error = Assert.Single(Evaluate<CelOptional<int>>(expression).Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 11), (error.Code, error.Stage, error.Location.Offset));
        Assert.StartsWith("result of type 'optional_type(", error.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void ContentOfAnotherKind_IsResultConversionAtTheRoot()
    {
        CelError error = Assert.Single(Evaluate<CelOptional<long>>("optional.of(dyn('a'))").Errors);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 11), (error.Code, error.Stage, error.Location.Offset));
        Assert.StartsWith("result of type 'optional_type(string)' cannot be converted to .NET type '", error.Message, System.StringComparison.Ordinal);
    }

    // The expected type that T gives is optional_type(E'): an expression of another type is ExpectedTypeMismatch at the root, with
    // the text of the table of the codes, and a value that is not an optional is no CelOptional.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("optional.of('a')", 11, "optional_type(string)", "optional_type(int)")]
    [InlineData("1", 0, "int", "optional_type(int)")]
    [InlineData("[optional.of(1)]", 0, "list(optional_type(int))", "optional_type(int)")]
    public void ExpressionOfAnotherType_IsExpectedTypeMismatchAtTheRoot(string expression, int offset, string actual, string expected)
    {
        CelError error = Assert.Single(environment.Compile<CelOptional<long>>(expression).Errors);

        Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check, offset), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal("expression of type '" + actual + "' is not assignable to the expected type '" + expected + "'", error.Message);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void OptionalWhereAPlainTypeIsExpected_IsExpectedTypeMismatchAtTheRoot()
    {
        CelError error = Assert.Single(environment.Compile<long>("optional.of(1)").Errors);

        Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check, 11), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal("expression of type 'optional_type(int)' is not assignable to the expected type 'int'", error.Message);
    }

    // The input row: a variable of the .NET type CelOptional<X> has the CEL type optional_type(E) with E the type of X, and needs the
    // set; the empty CelOptional<X> is optional.none() and a non-empty one is optional.of of its converted value, null among them. A
    // CelOptional<X> that is bound in a place of dyn has the type optional_type(dyn).
    [Fact]
    public void VariableOfTheTypeCelOptional_IsAnOptionalVariable()
    {
        CelEnvironment declared = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Optional(2))
            .AddVariable<CelOptional<long>>("x")
            .AddVariable("d", CelType.Dyn)
            .Build()
            .GetEnvironmentOrThrow();
        var full = new CelBindings().Add("x", new CelOptional<long>(5)).Add("d", new CelOptional<long>(7));
        var empty = new CelBindings().Add("x", default(CelOptional<long>)).Add("d", default(CelOptional<long>));

        Assert.Equal(CelType.OptionalOf(CelType.Int), declared.Compile<CelValue>("x").GetProgramOrThrow().ResultType);
        Assert.True(ValueOf(declared, "x.hasValue() && x.value() + 1 == 6", full));
        Assert.True(ValueOf(declared, "!x.hasValue() && x.orValue(3) == 3", empty));
        Assert.True(ValueOf(declared, "d.hasValue() && d.value() == 7", full));
        Assert.True(ValueOf(declared, "!d.hasValue()", empty));
        Assert.True(ValueOf(declared, "x == optional.of(5) && x != optional.none()", full));
    }

    [Fact]
    public void VariableOfTheTypeCelOptional_WithoutTheSet_IsTypeNotDeclared()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<CelOptional<long>>("x").Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.TypeNotDeclared, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.StartsWith("variable 'x' has type 'optional_type", error.Message, System.StringComparison.Ordinal);
        Assert.EndsWith("; add the extension set 'optional'", error.Message, System.StringComparison.Ordinal);
    }

    private static bool ValueOf(CelEnvironment target, string expression, CelBindings values) =>
        target.Compile<bool>(expression).GetProgramOrThrow().Evaluate(values, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private CelEvalResult<T> Evaluate<T>(string expression) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);
}
