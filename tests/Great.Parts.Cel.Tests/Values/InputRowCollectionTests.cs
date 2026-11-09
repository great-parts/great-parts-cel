using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The input rows of collections, application classes and JsonElement: the CEL type that AddVariable<T> declares for a
// .NET type and the value that CelBindings.Add<T> gives by the static type.
public sealed class InputRowCollectionTests
{
    [Fact]
    public void ListRows_GiveTheListOfTheElementType()
    {
        Assert.Equal(CelType.ListOf(CelType.Double), Declared<List<double>>());
        Assert.Equal(CelType.ListOf(CelType.String), Declared<ImmutableArray<string>>());
        Assert.Equal(CelType.ListOf(CelType.Bool), Declared<ImmutableList<bool>>());
        Assert.Equal(CelType.ListOf(CelType.Int), Declared<ReadOnlyCollection<int>>());
        Assert.Equal(CelType.ListOf(CelType.UInt), Declared<IReadOnlyCollection<ulong>>());
        Assert.Equal(CelType.ListOf(CelType.UInt), Declared<ISet<uint>>());
        Assert.Equal(CelType.ListOf(CelType.Bytes), Declared<IReadOnlyList<byte[]>>());
        Assert.Equal(CelType.ListOf(CelType.WrapperOf(CelType.Double)), Declared<double?[]>());
        Assert.Equal(CelType.ListOf(CelType.Dyn), Declared<IReadOnlyList<JsonElement>>());
    }

    [Fact]
    public void DictionaryRows_GiveTheMapOfTheKeyAndValueTypes()
    {
        Assert.Equal(CelType.MapOf(CelType.Bool, CelType.String), Declared<IReadOnlyDictionary<bool, string>>());
        Assert.Equal(CelType.MapOf(CelType.String, CelType.WrapperOf(CelType.Int)), Declared<IDictionary<string, long?>>());
        Assert.Equal(CelType.MapOf(CelType.UInt, CelType.ListOf(CelType.Dyn)), Declared<IReadOnlyDictionary<ulong, CelList>>());
        Assert.Equal(CelType.MapOf(CelType.String, CelType.Dyn), Declared<IReadOnlyDictionary<Guid, JsonElement>>());
    }

    // Dictionary<K, V> is a row of its own, as List<T> is for the lists: the type and the value are those of the interface.
    [Fact]
    public void ConcreteDictionaryRow_GivesTheTypeOfTheInterface()
    {
        Assert.Equal(Declared<IReadOnlyDictionary<string, long>>(), Declared<Dictionary<string, long>>());
        Assert.Equal(Declared<IReadOnlyDictionary<Guid, JsonElement>>(), Declared<Dictionary<Guid, JsonElement>>());
        Assert.Equal(Declared<IReadOnlyDictionary<int, double?>>(), Declared<Dictionary<int, double?>>());
        Assert.Equal(Declared<IReadOnlyDictionary<ulong, CelList>>(), Declared<Dictionary<ulong, CelList>>());
        Assert.Equal(Declared<IReadOnlyDictionary<bool, object>>(), Declared<Dictionary<bool, object>>());
        Assert.Equal(CelType.MapOf(CelType.UInt, CelType.Bytes), Declared<Dictionary<uint, byte[]>>());
    }

    [Fact]
    public void ConcreteDictionaryWithInferredT_IsAMapInBindingsAndVariables()
    {
        var dictionary = new Dictionary<string, long> { ["a"] = 1, ["b"] = 2 };
        CelBindings bindings = new CelBindings().Add("d", dictionary);
        dictionary["c"] = 3;

        Assert.Equal(CelValueKind.Map, bindings.Values["d"].Kind);
        Assert.True(bindings.Values["d"].TryGetMap(out CelMap? map));
        Assert.Equal(3, map.Count);
        Assert.True(Holds(
            "d == {'a': 1, 'b': 2, 'c': 3} && d['b'] == 2 && size(d) == 3 && !('z' in d)",
            bindings,
            builder => builder.AddVariable<Dictionary<string, long>>("d")));
    }

    [Fact]
    public void ConcreteDictionary_KeepsItsSourceForThePlaceRuleOfAnObjectType()
    {
        CelObjectType card = CelObjectTypeBuilder.ForDictionary("acme.crm.Card").AddField("name", CelType.String).Build();
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(CelTypeProvider.Create("cards", card))
            .AddVariable("card", card.Type)
            .Build()
            .GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("card", new Dictionary<string, object?> { ["name"] = "Ann" });

        Assert.Equal("Ann", environment.Compile<string>("card.name").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.True(environment.Compile<bool>("has(card.name) && type(card) == acme.crm.Card").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void ConcreteDictionaryInTheResultOfAFunction_IsAMap()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<Dictionary<string, long>>("scores", () => new Dictionary<string, long> { ["x"] = 7 })
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(CelType.MapOf(CelType.String, CelType.Int), environment.Compile<CelValue>("scores()").GetProgramOrThrow().ResultType);
        Assert.True(environment.Compile<bool>("scores() == {'x': 7} && scores()['x'] == 7").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void ApplicationClassInAList_IsAListOfDyn()
    {
        Assert.Equal(CelType.ListOf(CelType.Dyn), Declared<IReadOnlyList<Plain>>());
        Assert.Equal(CelType.ListOf(CelType.Dyn), Declared<Plain[]>());
        Assert.Equal(CelType.ListOf(CelType.Dyn), Declared<IReadOnlyCollection<IPlain>>());
    }

    [Fact]
    public void ListViews_ReadTheirSourceWhenTheExpressionReadsIt()
    {
        long[] array = [4, 5];
        var list = new List<string> { "x" };
        CelBindings bindings = new CelBindings()
            .Add("a", array)
            .Add("l", list)
            .Add("i", ImmutableArray.Create(1L, 2L))
            .Add("e", default(ImmutableArray<long>))
            .Add<IList<long>>("il", new ReadOnlyCollection<long>([8, 9]));
        array[0] = 40;
        list.Add("y");

        Assert.True(Holds(
            "a == [40, 5] && l == ['x', 'y'] && i == [1, 2] && size(e) == 0 && il == [8, 9]",
            bindings,
            builder => builder.AddVariable<long[]>("a").AddVariable<List<string>>("l").AddVariable<ImmutableArray<long>>("i")
                .AddVariable<ImmutableArray<long>>("e").AddVariable<IList<long>>("il")));
    }

    [Fact]
    public void SetAndCollectionWithoutAnIndex_AreCopiesMadeByAdd()
    {
        var set = new HashSet<long> { 30, 10, 20 };
        var queue = new Queue<string>(["b", "a"]);
        CelBindings bindings = new CelBindings().Add<IReadOnlySet<long>>("s", set).Add<IReadOnlyCollection<string>>("q", queue);
        set.Add(0);
        queue.Enqueue("c");

        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<IReadOnlySet<long>>("s")
            .AddVariable<IReadOnlyCollection<string>>("q")
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(environment.Compile<bool>("s == [10, 20, 30] && q == ['b', 'a']").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void SetThatCelCannotSort_IsArgumentExceptionOfAdd()
    {
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<ISet<object>>("v", new HashSet<object> { 1L, "a" }));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<IReadOnlySet<double>>("v", new HashSet<double> { 1.5, double.NaN }));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<IReadOnlySet<string?>>("v", new HashSet<string?> { "a", null }));
    }

    [Fact]
    public void ApplicationClassInAList_IsAListOfBareInstancesThroughCovariance()
    {
        var plain = new Plain();
        CelBindings bindings = new CelBindings().Add<IReadOnlyList<Plain>>("v", [plain]).Add<IReadOnlyCollection<IPlain>>("c", [plain]);

        Assert.True(bindings.Values["v"].TryGetList(out CelList? list));
        Assert.True(list[0].TryGetObject(out object? instance));
        Assert.Same(plain, instance);
        Assert.True(bindings.Values["c"].TryGetList(out CelList? copy));
        Assert.Equal(CelValueKind.Object, copy[0].Kind);
    }

    [Fact]
    public void ApplicationClass_IsABareInstanceAndNullIsNull()
    {
        var plain = new Plain();
        CelBindings bindings = new CelBindings().Add("o", plain).Add<Plain?>("n", null).Add<IPlain>("i", plain);

        Assert.True(bindings.Values["o"].TryGetObject(out object? instance));
        Assert.Same(plain, instance);
        Assert.Null(bindings.Values["o"].PairedType);
        Assert.Equal(CelValue.Null, bindings.Values["n"]);
        Assert.Equal(CelValueKind.Object, bindings.Values["i"].Kind);
    }

    [Fact]
    public void ObjectRow_KeepsACollectionAsABareInstance()
    {
        var list = new List<long> { 1 };
        CelBindings bindings = new CelBindings().Add<object>("v", list).Add<object>("d", new Dictionary<string, object?>());

        Assert.True(bindings.Values["v"].TryGetObject(out object? instance));
        Assert.Same(list, instance);
        Assert.Equal(CelValueKind.Object, bindings.Values["d"].Kind);
    }

    [Fact]
    public void TypeOutsideTheRows_IsTypeNotDeclaredAndArgumentException()
    {
        AssertOutsideTheRows<IReadOnlyList<short>>([1]);
        AssertOutsideTheRows<List<IReadOnlyList<long>>>([[1]]);
        AssertOutsideTheRows<SortedDictionary<string, long>>(new SortedDictionary<string, long>());
        AssertOutsideTheRows<Dictionary<double, long>>(new Dictionary<double, long>());
        AssertOutsideTheRows<IReadOnlyDictionary<double, long>>(new Dictionary<double, long>());
        AssertOutsideTheRows<IReadOnlyDictionary<string, Plain>>(new Dictionary<string, Plain>());
        AssertOutsideTheRows<IReadOnlyList<DayOfWeek>>([DayOfWeek.Friday]);
        Assert.Equal(CelErrorCode.TypeNotDeclared, Assert.Single(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<Plain>("v").Build().Errors).Code);
    }

    [Fact]
    public void JsonElement_IsDynAndUndefinedIsRefused()
    {
        using JsonDocument document = JsonDocument.Parse("""{"k": [1, 2]}""");

        Assert.Equal(CelType.Dyn, Declared<JsonElement>());
        Assert.True(Holds("v.k == [1, 2]", new CelBindings().Add("v", document.RootElement), builder => builder.AddVariable<JsonElement>("v")));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", default(JsonElement)));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<object>("v", default(JsonElement)));
    }

    // A set in the result of an application function is sorted at the call, charged before it is, and refused with
    // InvalidArgument at the call when its elements have no order.
    [Fact]
    public void SetInTheResultOfAFunction_IsSortedAndChargedAndRefusedWithAnError()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<IReadOnlySet<string>>("tags", () => new HashSet<string> { "pq", "r", "stu" })
            .AddFunction<IReadOnlySet<double>>("bad", () => new HashSet<double> { double.NaN })
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(environment.Compile<bool>("tags() == ['pq', 'r', 'stu']").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
        CelError error = Assert.Single(environment.Compile<CelValue>("bad()").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors);
        Assert.Equal((CelErrorCode.InvalidArgument, 3), (error.Code, error.Location.Offset));
        Assert.Equal("invalid argument to 'bad': a result set whose elements CEL cannot sort", error.Message);
    }

    [Fact]
    public void SetOfTheResult_ChargesOneUnitPlusTheFormulaOfItsSort()
    {
        var meter = new RecordingMeter();

        CelValue converted = ConversionRules.FromResult<IReadOnlySet<string>>(new HashSet<string> { "pq", "r", "stu" }, "tags", meter);

        // n = 3, the longest element is 3 units long: c = 1 + ceil(2 * 3 / 10) = 2, and ceil(log2(4)) = 2: 1 + 2 * 3 * 2.
        Assert.Equal(CelValueKind.List, converted.Kind);
        Assert.Equal(13UL, meter.Total);
    }

    [Fact]
    public void CollectionWithoutAnIndexInTheResult_ChargesOneUnitPlusItsCount()
    {
        var meter = new RecordingMeter();

        CelValue converted = ConversionRules.FromResult<IReadOnlyCollection<long>>(new Queue<long>([1, 2, 3, 4]), "ids", meter);

        Assert.Equal(CelValueKind.List, converted.Kind);
        Assert.Equal(5UL, meter.Total);
    }

    private static CelType Declared<T>() =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<T>("v").Build().GetEnvironmentOrThrow()
            .Compile<CelValue>("v").GetProgramOrThrow().ResultType;

    private static bool Holds(string expression, CelBindings bindings, Func<CelEnvironmentBuilder, CelEnvironmentBuilder> declare) =>
        declare(new CelEnvironmentBuilder(CelStandardLibrary.V1)).Build().GetEnvironmentOrThrow().Compile<bool>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static void AssertOutsideTheRows<T>(T value)
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<T>("v").Build();

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.TypeNotDeclared, Assert.Single(result.Errors).Code);
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", value));
    }

    private interface IPlain;

    private sealed class Plain : IPlain;
}
