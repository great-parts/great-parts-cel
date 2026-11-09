using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The input rows of the type mapping table for collections, objects and JsonElement: the CEL type that
// AddVariable<T> declares for a .NET type and the value that CelBindings.Add<T> gives by the static type T.
// A list is a lazy view, a set a sorted copy and another collection without an index a copy made at the
// conversion; a dictionary is a lazy view. Typed nesting is one step over a closed list of element, key and
// value types; another T is TypeNotDeclared for AddVariable<T> and ArgumentException for CelBindings.Add<T>.
public sealed class InputRowTests
{
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void Lists_AreListsOfTheirElementType()
    {
        Assert.Equal(CelType.ListOf(CelType.Int), Read<long[]>([3, 1, 2], "v == [3, 1, 2]"));
        Assert.Equal(CelType.ListOf(CelType.Int), Read<IReadOnlyList<int>>([3, 1, 2], "v == [3, 1, 2]"));
        Assert.Equal(CelType.ListOf(CelType.String), Read<IList<string>>(new List<string> { "b", "a" }, "v == ['b', 'a']"));
        Assert.Equal(CelType.ListOf(CelType.WrapperOf(CelType.Int)), Read<IReadOnlyList<long?>>([1, null], "v[1] == null && v[0] == 1"));
        Assert.Equal(CelType.ListOf(CelType.String), Read<IReadOnlyList<Guid>>([Guid.Parse("0F8FAD5B-D9CB-469F-A165-70867728950E")], "v == ['0f8fad5b-d9cb-469f-a165-70867728950e']"));
        Assert.Equal(CelType.ListOf(CelType.Dyn), Read<IReadOnlyList<object>>([1L, "a"], "v == [1, 'a']"));
        Assert.Equal(CelType.ListOf(CelType.ListOf(CelType.Dyn)), Read<IReadOnlyList<CelList>>([CelList.Create(CelValue.FromInt64(1))], "v == [[1]]"));
    }

    // A view reads its source when the expression reads it; a copy is made by CelBindings.Add.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void ListIsAView_SetAndCollectionWithoutAnIndexAreCopies()
    {
        long[] array = [1, 2];
        var set = new HashSet<long> { 3, 1, 2 };
        var queue = new Queue<long>([3, 1, 2]);
        CelBindings bindings = new CelBindings()
            .Add("l", array)
            .Add<IReadOnlySet<long>>("s", set)
            .Add<IReadOnlyCollection<long>>("q", queue);
        array[0] = 9;
        set.Add(0);
        queue.Enqueue(0);

        CelProgram<bool> program = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long[]>("l")
            .AddVariable<IReadOnlySet<long>>("s")
            .AddVariable<IReadOnlyCollection<long>>("q")
            .Build()
            .GetEnvironmentOrThrow()
            .Compile<bool>("l == [9, 2] && s == [1, 2, 3] && q == [3, 1, 2]")
            .GetProgramOrThrow();

        Assert.True(program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // A set is sorted in the order of CEL, strings by code points.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void Sets_AreSortedLists()
    {
        Assert.Equal(CelType.ListOf(CelType.String), Read<ISet<string>>(new HashSet<string> { "\U0001F600", "b", "", "a" }, "v == ['a', 'b', '', '\U0001F600']"));
        Assert.Equal(CelType.ListOf(CelType.Double), Read<IReadOnlySet<double>>(new HashSet<double> { 2.5, -1.0 }, "v == [-1.0, 2.5]"));
    }

    // Elements that CEL cannot sort are refused by CelBindings.Add.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void SetThatCelCannotSort_IsArgumentExceptionOfAdd()
    {
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<ISet<object>>("v", new HashSet<object> { 1L, "a" }));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<IReadOnlySet<double>>("v", new HashSet<double> { double.NaN }));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<IReadOnlySet<string?>>("v", new HashSet<string?> { "a", null }));
    }

    // Type checking takes only the key type of the map; a key of another numeric type is found at run time,
    // on one number line, when it comes through dyn().
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void Dictionaries_AreMapsOfTheirKeyAndValueTypes()
    {
        Assert.Equal(
            CelType.MapOf(CelType.String, CelType.Int),
            Read<IReadOnlyDictionary<string, long>>(new Dictionary<string, long> { ["b"] = 2, ["a"] = 1 }, "v == {'a': 1, 'b': 2} && v.map(k, k) == ['b', 'a']"));
        Assert.Equal(
            CelType.MapOf(CelType.String, CelType.String),
            Read<IDictionary<Guid, string>>(new Dictionary<Guid, string> { [Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")] = "x" }, "v['0f8fad5b-d9cb-469f-a165-70867728950e'] == 'x'"));
        Assert.Equal(
            CelType.MapOf(CelType.Int, CelType.Bool),
            Read<IReadOnlyDictionary<int, bool>>(new Dictionary<int, bool> { [1] = true }, "v[dyn(1u)] && v[dyn(1.0)]"));
        Assert.Equal(
            CelType.MapOf(CelType.UInt, CelType.Dyn),
            Read<IReadOnlyDictionary<uint, object>>(new Dictionary<uint, object> { [7] = "a" }, "v[7u] == 'a'"));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void JsonElement_IsDyn()
    {
        using JsonDocument document = JsonDocument.Parse("""{"a": [1, "x"]}""");

        Assert.Equal(CelType.Dyn, Read(document.RootElement, "v.a == [1, 'x']"));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", default(JsonElement)));
    }

    // A list of an application class is read through covariance as a list of bare instances, each described
    // by the type index: an instance that no type source describes fails where its description is needed.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void ListOfAnApplicationClass_IsAListOfItsInstances()
    {
        IReadOnlyList<Order> orders = [new Order(), new Order()];
        CelProgram<CelValue> program = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("v", CelType.ListOf(CelType.Dyn))
            .Build()
            .GetEnvironmentOrThrow()
            .Compile<CelValue>("size(v) == 2 ? v[0].id : 0")
            .GetProgramOrThrow();

        CelError error = Assert.Single(program.Evaluate(new CelBindings().Add("v", orders), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.TypeNotAccessible, 19), (error.Code, error.Location.Offset));
        Assert.Equal(
            ".NET type 'Great.Parts.Cel.Tests.Examples.Values.InputRowTests+Order' is not available to expressions: no type source of the environment describes it",
            error.Message);
    }

    // The object row rejects no instance; the node that reads the value from the application decides with the
    // type index. A .NET collection that no type source describes is TypeNotAccessible at the identifier that
    // reads the variable. An element of a collection of the application is not decided there: a .NET
    // collection as an element is a bare object without a description, which fails where its description is
    // needed and is not equal to a list. cel-go has no .NET collections, so this does not rely on it.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void NetCollectionAsObject_IsTypeNotAccessibleAtTheReadAndAnElementIsDescribedLazily()
    {
        const string Message = ".NET type 'System.Collections.Generic.List`1[System.Int64]' is not available to expressions: no type source of the environment describes it";
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("v", CelType.Dyn).Build().GetEnvironmentOrThrow();
        var collection = new CelBindings().Add<object>("v", new List<long> { 1 });
        var element = new CelBindings().Add<IReadOnlyList<object>>("v", [new List<long> { 1 }]);

        foreach ((string expression, CelBindings bindings, int offset) in new[] { ("v", collection, 0), ("size(v)", collection, 5), ("v[0].f", element, 4) })
        {
            CelError error = Assert.Single(environment.Compile<CelValue>(expression).GetProgramOrThrow()
                .Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

            Assert.Equal((CelErrorCode.TypeNotAccessible, CelErrorStage.Evaluate, offset, Message), (error.Code, error.Stage, error.Location.Offset, error.Message));
        }

        Assert.False(environment.Compile<bool>("v[0] == [1]").GetProgramOrThrow()
            .Evaluate(element, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // Outside the closed list: an element of another type (short, an enumeration), a map value of an
    // application class, a key type that is not a map key, a typed nesting deeper than one step, and an
    // application class that no type source describes.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-3")]
    public void TypeOutsideTheRows_IsTypeNotDeclaredAndArgumentException()
    {
        AssertOutsideTheRows<IReadOnlyList<short>>([1], "System.Collections.Generic.IReadOnlyList`1[System.Int16]");
        AssertOutsideTheRows<IReadOnlyList<DayOfWeek>>([DayOfWeek.Friday], "System.Collections.Generic.IReadOnlyList`1[System.DayOfWeek]");
        AssertOutsideTheRows<IReadOnlyDictionary<string, Order>>(
            new Dictionary<string, Order> { ["a"] = new() },
            "System.Collections.Generic.IReadOnlyDictionary`2[System.String,Great.Parts.Cel.Tests.Examples.Values.InputRowTests+Order]");
        AssertOutsideTheRows<IReadOnlyDictionary<double, long>>(
            new Dictionary<double, long> { [1.5] = 1 },
            "System.Collections.Generic.IReadOnlyDictionary`2[System.Double,System.Int64]");
        AssertOutsideTheRows<IReadOnlyList<IReadOnlyList<long>>>(
            [new long[] { 1 }],
            "System.Collections.Generic.IReadOnlyList`1[System.Collections.Generic.IReadOnlyList`1[System.Int64]]");

        Assert.Equal(
            "variable 'v' has type 'Great.Parts.Cel.Tests.Examples.Values.InputRowTests+Order', which no type source or extension of the environment declares",
            SingleBuildError<Order>().Message);
    }

    private static void AssertOutsideTheRows<T>(T value, string typeName)
    {
        CelError error = SingleBuildError<T>();

        Assert.Equal(
            (CelErrorCode.TypeNotDeclared, CelErrorStage.Build, "variable 'v' has type '" + typeName + "', which no type source or extension of the environment declares"),
            (error.Code, error.Stage, error.Message));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", value));
    }

    private static CelError SingleBuildError<T>()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<T>("v").Build();

        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    // Declares v by AddVariable<T>, binds the value by CelBindings.Add<T>, checks that the expression is true
    // and returns the declared CEL type.
    private static CelType Read<T>(T value, string expression)
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<T>("v").Build().GetEnvironmentOrThrow();

        Assert.True(environment.Compile<bool>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("v", value), cancellationToken: CancellationToken.None).GetValueOrThrow());
        return environment.Compile<CelValue>("v").GetProgramOrThrow().ResultType;
    }

    private sealed class Order
    {
    }
}
