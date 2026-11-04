using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The result rows of the type mapping (docs/design/facade.md, "Result conversion"): each row, no conversion
// between numeric types, lazy views and copies by the static type of the root, and the check before a lazy view.
public sealed class ResultRowTests
{
    private static readonly string[] AB = ["a", "b"];

    private static readonly int[] OneTwo = [1, 2];

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("li", CelType.ListOf(CelType.Int))
        .AddVariable("ld", CelType.ListOf(CelType.Dyn))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void PrimitiveRows_ConvertTheValueOfTheirType()
    {
        Assert.True(Value<bool>("true"));
        Assert.Equal(-3L, Value<long>("-3"));
        Assert.Equal(-3, Value<int>("-3"));
        Assert.Equal(3UL, Value<ulong>("3u"));
        Assert.Equal(3U, Value<uint>("3u"));
        Assert.Equal(1.5, Value<double>("1.5"));
        Assert.Equal("a", Value<string>("'a'"));
        Assert.Equal(new byte[] { 0x61 }, Value<ReadOnlyMemory<byte>>("b'a'").ToArray());
        Assert.Equal(new byte[] { 0x61 }, Value<byte[]>("b'a'"));
    }

    [Fact]
    public void RangeChecks_AreResultConversion()
    {
        Assert.Equal("result of type 'int' cannot be converted to .NET type 'System.Int32'", Error<int>("  4294967296").Message);
        Assert.Equal("result of type 'uint' cannot be converted to .NET type 'System.UInt32'", Error<uint>("4294967296u").Message);
        Assert.Equal(2, Error<int>("  4294967296").Location.Offset);
    }

    // The expected type dyn lets any value reach the row of T; a value of another type is ResultConversion.
    [Theory]
    [InlineData("1u", "uint")]
    [InlineData("1.0", "double")]
    [InlineData("'1'", "string")]
    [InlineData("null", "null_type")]
    public static void ValueOfAnotherType_IsResultConversion(string value, string celType)
    {
        CelError error = Error<long>(value, CelType.Dyn);

        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal("result of type '" + celType + "' cannot be converted to .NET type 'System.Int64'", error.Message);
    }

    [Fact]
    public void Null_IsNullForReferenceTypesNullableAndObject()
    {
        Assert.Null(Value<string>("null", CelType.Null));
        Assert.Null(Value<byte[]>("null", CelType.Null));
        Assert.Null(Value<CelList>("null", CelType.Null));
        Assert.Null(Value<IReadOnlyList<long>>("null", CelType.Null));
        Assert.Null(Value<long?>("null"));
        Assert.Null(Value<object>("null"));
        Assert.Equal(CelValueKind.Null, Value<CelValue>("null").Kind);
    }

    [Fact]
    public void Nullable_ConvertsANonNullValueByTheRowOfItsType()
    {
        Assert.Equal(7L, Value<long?>("7"));
        Assert.Equal(7, Value<int?>("7"));
        Assert.Equal(true, Value<bool?>("true"));
        Assert.Equal(1.5, Value<double?>("1.5"));
        Assert.Equal(2UL, Value<ulong?>("2u"));
        Assert.Equal(2U, Value<uint?>("2u"));
        Assert.Equal(new byte[] { 0x61 }, Value<ReadOnlyMemory<byte>?>("b'a'")?.ToArray());
        Assert.Equal(CelErrorCode.ResultConversion, Error<int?>("4294967296").Code);
    }

    [Fact]
    public void List_IsALazyViewWhenNoElementCanFail()
    {
        IReadOnlyList<long> view = Value<IReadOnlyList<long>>("[1, 2] + [3]");

        Assert.Equal(new long[] { 1, 2, 3 }, view);
        Assert.Equal(3, view.Count);
        Assert.Equal(2L, view[1]);
        Assert.DoesNotContain("ImmutableArray", view.GetType().Name, StringComparison.Ordinal);
    }

    // int to int can fail, so the list is copied, and the element outside Int32 fails the copy.
    [Fact]
    public void List_IsACopyWhenAnElementCanFail()
    {
        IReadOnlyList<int> copy = Value<IReadOnlyList<int>>("[1, 2]");

        Assert.Equal(OneTwo, copy);
        Assert.IsType<System.Collections.Immutable.ImmutableArray<int>>(copy);
        Assert.Equal(CelErrorCode.ResultConversion, Error<IReadOnlyList<int>>("[1, 4294967296]").Code);
    }

    [Fact]
    public void ListOfDyn_IsACopyThatFailsOnAnElementOfAnotherType()
    {
        var bindings = new CelBindings().Add("ld", CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a")));
        CelError error = Assert.Single(Environment.Compile<IReadOnlyList<long>>("ld").GetProgramOrThrow().Evaluate(bindings, cancellationToken: TestContext.Current.CancellationToken).Errors);

        Assert.Equal("result of type 'list' cannot be converted to .NET type '" + typeof(IReadOnlyList<long>) + "'", error.Message);
    }

    // li + d has the type list(int), but d holds strings: the check before the lazy view names the element.
    [Fact]
    public void ValueThatDoesNotFitTheStaticType_IsResultConversionBeforeTheView()
    {
        var bindings = new CelBindings()
            .Add("li", CelList.Create(CelValue.FromInt64(1)))
            .Add("d", CelList.Create(CelValue.FromString("a")));
        CelError error = Assert.Single(Environment.Compile<IReadOnlyList<long>>("li + d").GetProgramOrThrow().Evaluate(bindings, cancellationToken: TestContext.Current.CancellationToken).Errors);

        Assert.Equal("result of type 'list(string)' cannot be converted to .NET type '" + typeof(IReadOnlyList<long>) + "'", error.Message);
    }

    // Neither the check before the view nor the copy is charged: li of three elements costs its read 1 and its
    // check 3 for every T.
    [Fact]
    public void CheckAndCopy_AreNotCharged()
    {
        var bindings = new CelBindings().Add("li", CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3)));
        var options = new CelEvalOptions { CostLimit = 4 };

        Assert.True(Environment.Compile<IReadOnlyList<long>>("li").GetProgramOrThrow().Evaluate(bindings, options, TestContext.Current.CancellationToken).IsSuccess);
        Assert.True(Environment.Compile<IReadOnlyList<int>>("li").GetProgramOrThrow().Evaluate(bindings, options, TestContext.Current.CancellationToken).IsSuccess);
        Assert.True(Environment.Compile<CelValue>("li").GetProgramOrThrow().Evaluate(bindings, options, TestContext.Current.CancellationToken).IsSuccess);
    }

    [Fact]
    public void Map_IsALazyViewOrACopy()
    {
        IReadOnlyDictionary<string, long> view = Value<IReadOnlyDictionary<string, long>>("{'a': 1, 'b': 2}");
        IReadOnlyDictionary<string, int> copy = Value<IReadOnlyDictionary<string, int>>("{'a': 1, 'b': 2}");

        Assert.Equal(2L, view["b"]);
        Assert.True(view.ContainsKey("a"));
        Assert.False(view.TryGetValue("c", out _));
        Assert.Throws<KeyNotFoundException>(() => view["c"]);
        Assert.Equal(AB, view.Keys);
        Assert.Equal(new long[] { 1, 2 }, view.Values);
        Assert.Equal(new[] { new KeyValuePair<string, long>("a", 1), new KeyValuePair<string, long>("b", 2) }, view);
        Assert.Equal(2, copy["b"]);
        Assert.Equal(AB, copy.Keys);
        Assert.Equal(CelErrorCode.ResultConversion, Error<IReadOnlyDictionary<string, int>>("{'a': 4294967296}").Code);
    }

    // A key of int finds a key of uint with the same value, as the key lookup of CEL does.
    [Fact]
    public void MapView_LooksKeysUpByTheKeyRules()
    {
        IReadOnlyDictionary<object, object> map = Value<IReadOnlyDictionary<object, object>>("{1u: 'x', 'k': [true]}");

        Assert.Equal("x", map[1L]);
        Assert.Equal("x", map[1UL]);
        Assert.False(map.ContainsKey(DateTime.UnixEpoch));
        Assert.Equal(new object[] { true }, Assert.IsAssignableFrom<IReadOnlyList<object>>(map["k"]));
    }

    [Fact]
    public void CelListCelMapAndCelType_AreTheValuesAsTheyAre()
    {
        Assert.Equal(2, Value<CelList>("[1, 'a']").Count);
        Assert.Single(Value<CelMap>("{'a': 1}"));
        Assert.Equal(CelType.Int, Value<CelType>("type(1)"));
        Assert.Equal(CelType.ListOf(CelType.Dyn), Value<CelType>("list"));
    }

    [Fact]
    public void CelValue_IsTheValueAsItIs()
    {
        Assert.True(Value<CelValue>("[1, 'a']").TryGetList(out CelList? list));
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Object_IsTheNaturalMapping()
    {
        object? list = Value<object>("[1, 2u, 1.5, true, 'a', b'b', null, [1], {'k': 1}, int]");
        object?[] items = [.. Assert.IsAssignableFrom<IReadOnlyList<object?>>(list)];

        Assert.Equal(new object?[] { 1L, 2UL, 1.5, true, "a" }, items[..5]);
        Assert.Equal(new byte[] { 0x62 }, Assert.IsType<byte[]>(items[5]));
        Assert.Null(items[6]);
        Assert.Equal(new object?[] { 1L }, Assert.IsAssignableFrom<IReadOnlyList<object?>>(items[7]));
        Assert.Equal(1L, Assert.IsAssignableFrom<IReadOnlyDictionary<object, object?>>(items[8])["k"]);
        Assert.Equal(CelType.Int, items[9]);
    }

    private static T Value<T>(string expression, CelType? expectedType = null) =>
        Environment.Compile<T>(expression, new CelCompileOptions { ExpectedType = expectedType })
            .GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow();

    private static CelError Error<T>(string expression, CelType? expectedType = null) =>
        Assert.Single(Environment.Compile<T>(expression, new CelCompileOptions { ExpectedType = expectedType })
            .GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).Errors);
}
