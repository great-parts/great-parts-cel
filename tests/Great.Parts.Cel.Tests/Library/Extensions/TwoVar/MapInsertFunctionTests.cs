using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.TwoVar;

// cel.@mapInsert as a function: no text can write its name, so the trees are built with NodeFactory, as a composer of expressions
// builds them. A call that is not the step of a macro goes through the two overloads of the set, which copy their first map, put the
// pair or the pairs of the second map in and charge each pair; the macros of the set do not use them. The variable d of the trees is
// of the type dyn, bound to the map {'a': 1, 'b': 2}, which must come out of every call unchanged; a variable of type dyn is read
// for 1 unit and its binding is not descended into.
public sealed class MapInsertFunctionTests
{
    private const int CallOffset = 14;

    // The text that the trees stand for: long enough for every offset the tests give, which only positions the errors.
    private static readonly string Source = new(' ', 64);

    [Fact]
    public static void KeyAndValue_GiveACopyOfTheMapWithThePairAtTheEnd()
    {
        var nodes = new NodeFactory();
        CelBindings bindings = Bindings();

        CelValue value = Run(Insert(nodes, nodes.Ident("d", 15), Str(nodes, "c"), Int(nodes, 3)), nodes, bindings);

        CelMap map = AsMap(value);
        Assert.Equal(["a", "b", "c"], map.Select(entry => Name(entry.Key)));
        Assert.Equal([1L, 2L, 3L], map.Select(entry => Integer(entry.Value)));
        Assert.Equal(2, AsMap(bindings.Values["d"]).Count);
    }

    [Fact]
    public static void TwoMaps_GiveTheFirstThenThePairsOfTheSecondInTheirOrder()
    {
        var nodes = new NodeFactory();
        CelBindings bindings = Bindings();

        CelValue value = Run(Insert(nodes, nodes.Ident("d", 15), SecondMap(nodes, "z", "y")), nodes, bindings);

        CelMap map = AsMap(value);
        Assert.Equal(["a", "b", "z", "y"], map.Select(entry => Name(entry.Key)));
        Assert.Equal([1L, 2L, 26L, 25L], map.Select(entry => Integer(entry.Value)));
        Assert.Equal(2, AsMap(bindings.Values["d"]).Count);
    }

    // A key equal to a key of the map is DuplicateMapKey at the call, with the key written as a literal; nothing is added.
    [Theory]
    [InlineData("a", "duplicate map key: \"a\"")]
    [InlineData("b", "duplicate map key: \"b\"")]
    public static void KeyThatIsInTheMap_IsDuplicateMapKeyAtTheCall(string key, string message)
    {
        ArgumentNullException.ThrowIfNull(key);
        var nodes = new NodeFactory();
        CelBindings bindings = Bindings();

        CelError error = Error(Run(Insert(nodes, nodes.Ident("d", 15), Str(nodes, key), Int(nodes, 9)), nodes, bindings));

        Assert.Equal((CelErrorCode.DuplicateMapKey, message, CallOffset), (error.Code, error.Message, error.Location.Offset));
        Assert.Equal(2, AsMap(bindings.Values["d"]).Count);
    }

    // A pair of the second map whose key is in the first is the same error, with the key of that pair; the pairs before it are in no
    // result.
    [Fact]
    public static void PairOfTheSecondMapThatIsInTheFirst_IsDuplicateMapKeyWithItsKey()
    {
        var nodes = new NodeFactory();

        CelError error = Error(Run(Insert(nodes, nodes.Ident("d", 15), SecondMap(nodes, "z", "b")), nodes, Bindings()));

        Assert.Equal((CelErrorCode.DuplicateMapKey, "duplicate map key: \"b\"", CallOffset), (error.Code, error.Message, error.Location.Offset));
    }

    // A key of a kind that cannot be a key passes the types, since K is a parameter, and is UnsupportedMapKeyType at the call.
    [Fact]
    public static void KeyOfAnotherKind_IsUnsupportedMapKeyType()
    {
        var nodes = new NodeFactory();
        ExprNode root = Insert(nodes, nodes.Struct(string.Empty, [], 15), nodes.Constant(CelValue.FromDouble(1.5), 20), Int(nodes, 1));

        CelError error = Error(Run(root, nodes, Bindings()));

        Assert.Equal(
            (CelErrorCode.UnsupportedMapKeyType, "unsupported map key type 'double'", CallOffset),
            (error.Code, error.Message, error.Location.Offset));
    }

    // Without type checking the first map is read as it is: an int key of it that equals a uint key of the pair is a repeat.
    [Fact]
    public static void KeyOfAnotherKindThatIsEqual_IsDuplicateMapKey()
    {
        var nodes = new NodeFactory();
        ExprNode first = nodes.Struct(string.Empty, [nodes.Entry(Int(nodes, 1), Int(nodes, 5), false, 18)], 15);
        ExprNode root = Insert(nodes, first, nodes.Constant(CelValue.FromUInt64(1), 22), Int(nodes, 6));

        CelError error = Error(PlanWithoutTypeCheck(root, nodes).Evaluate(Bindings().Values, null, CancellationToken.None));

        Assert.Equal((CelErrorCode.DuplicateMapKey, "duplicate map key: 1u"), (error.Code, error.Message));
    }

    // The call costs its arguments and 2 units for each pair put in, and ⌈L/10⌉ more for a string key of L units: the read of d 1, the
    // literals 0, and the pair 2 and 2 for the key of 11 units.
    [Fact]
    public static void Pair_IsChargedWithItsKey()
    {
        var nodes = new NodeFactory();
        Plan plan = PlanOf(Insert(nodes, nodes.Ident("d", 15), Str(nodes, "abcdefghijk"), Int(nodes, 3)), nodes);

        Assert.True(Evaluate(plan, 1 + 2 + 2).TryGetMap(out _));
        Assert.Equal(CelErrorCode.CostLimitExceeded, Error(Evaluate(plan, 1 + 2 + 2 - 1)).Code);
    }

    // The map literal that is the second argument costs 1 + 2n and ⌈L/10⌉ for each string key, the pairs 2 each and ⌈L/10⌉ for each
    // string key again: the read 1, the literal 1 + 4 + 2, the pairs 2 + 1 and 2 + 1.
    [Fact]
    public static void PairsOfTheSecondMap_AreChargedOneByOne()
    {
        var nodes = new NodeFactory();
        Plan plan = PlanOf(Insert(nodes, nodes.Ident("d", 15), SecondMap(nodes, "x", "y")), nodes);

        Assert.True(Evaluate(plan, 1 + 7 + 6).TryGetMap(out _));
        Assert.Equal(CelErrorCode.CostLimitExceeded, Error(Evaluate(plan, 1 + 7 + 6 - 1)).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Error(Evaluate(plan, 1 + 7 + 3)).Code);
    }

    // A JSON object as the second map is enumerated as the range of a comprehension is: its properties in their order, charged by the
    // row of the enumeration of a JSON object (1 and 1 + ⌈L/10⌉ for each property) before the first pair, and then each pair 2 and
    // ⌈L/10⌉ for its key: the read of counts 3, of d 1, the enumeration 1 + 2 + 2, the pairs 3 and 3.
    [Fact]
    public static void JsonObjectAsTheSecondMap_IsEnumeratedAndChargedBeforeItsPairs()
    {
        var nodes = new NodeFactory();
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"x": 5, "y": 7}""");
        CelBindings bindings = TwoVarEnvironment.Default().Add("d", document.RootElement.Clone());
        Plan plan = PlanOf(Insert(nodes, nodes.Ident("counts", 15), nodes.Ident("d", 25)), nodes);

        CelMap map = AsMap(plan.Evaluate(bindings.Values, 15, CancellationToken.None));

        Assert.Equal(["a", "b", "x", "y"], map.Select(entry => Name(entry.Key)));
        Assert.Equal(CelErrorCode.CostLimitExceeded, Error(plan.Evaluate(bindings.Values, 15 - 1, CancellationToken.None)).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Error(plan.Evaluate(bindings.Values, 12, CancellationToken.None)).Code);
    }

    // A name repeated in the JSON object is DuplicateMapKey at the call, found before the first pair, with the name of the repeat.
    [Fact]
    public static void JsonObjectWithARepeatedName_IsDuplicateMapKeyAtTheCall()
    {
        var nodes = new NodeFactory();
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"x": 5, "x": 7}""");
        CelBindings bindings = TwoVarEnvironment.Default().Add("d", document.RootElement.Clone());

        CelError error = Error(PlanOf(Insert(nodes, nodes.Ident("counts", 15), nodes.Ident("d", 25)), nodes).Evaluate(bindings.Values, null, CancellationToken.None));

        Assert.Equal((CelErrorCode.DuplicateMapKey, CallOffset), (error.Code, error.Location.Offset));
        Assert.Contains("\"x\"", error.Message, StringComparison.Ordinal);
    }

    // A map view that is ordered by ascending key as the second map: its pairs by ascending key whatever its own order, and the sort
    // charged before the first pair, 1 + c · n · ⌈log₂(n + 1)⌉ with c 2 for keys of 1 unit and n 2, which is 9, and then the pairs 3 and
    // 3: the read of counts 3 and of d 1.
    [Fact]
    public static void MapViewAsTheSecondMap_IsEnumeratedByAscendingKeyAndChargedBeforeItsPairs()
    {
        var nodes = new NodeFactory();
        var source = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(new Dictionary<string, long> { ["z"] = 26, ["y"] = 25 });
        CelBindings bindings = TwoVarEnvironment.Default().Add("d", CelMap.CreateView(source, CelValue.FromInt64, keepSourceOrder: false));
        Plan plan = PlanOf(Insert(nodes, nodes.Ident("counts", 15), nodes.Ident("d", 25)), nodes);

        CelMap map = AsMap(plan.Evaluate(bindings.Values, 19, CancellationToken.None));

        Assert.Equal(["a", "b", "y", "z"], map.Select(entry => Name(entry.Key)));
        Assert.Equal(CelErrorCode.CostLimitExceeded, Error(plan.Evaluate(bindings.Values, 19 - 1, CancellationToken.None)).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Error(plan.Evaluate(bindings.Values, 16, CancellationToken.None)).Code);
    }

    // Without type checking the call goes to the overloads that fit its arguments; a first argument that is no map fits none.
    [Fact]
    public static void FirstArgumentThatIsNoMap_IsEvalNoMatchingOverload()
    {
        var nodes = new NodeFactory();
        Plan plan = PlanWithoutTypeCheck(Insert(nodes, Int(nodes, 1), Int(nodes, 2), Int(nodes, 3)), nodes);

        CelError error = Error(plan.Evaluate(new Dictionary<string, CelValue>(), null, CancellationToken.None));

        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, "no such overload: cel.@mapInsert(int, int, int)"), (error.Code, error.Message));
    }

    // The checker finds the overload by the types, and the type of the call is the map of the key and the value.
    [Fact]
    public static void Call_IsTypedAsAMapOfTheArguments()
    {
        var nodes = new NodeFactory();
        ExprNode first = nodes.Struct(string.Empty, [nodes.Entry(Str(nodes, "a"), Int(nodes, 1), false, 18)], 15);

        CheckResult result = TwoVarEnvironment.Environment.Check(nodes.Build(Insert(nodes, first, Str(nodes, "c"), Int(nodes, 3)), new LineTable(Source)));

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal("map(string, int)", result.Expression!.ResultType.ToString());
    }

    // The overloads are declared with the identifiers of the set and no unit of their own.
    [Fact]
    public static void Overloads_AreDeclaredByTheSet()
    {
        CatalogExtension definition = Assert.IsType<CatalogExtension>(CelExtensions.TwoVarComprehensions(0).Definition);

        Assert.Equal(["@mapInsert_map_key_value", "@mapInsert_map_map"], definition.Overloads.Select(overload => overload.Declaration.OverloadId));
        Assert.All(definition.Overloads, overload => Assert.Equal((ExtensionMacros.MapInsertFunction, 0UL), (overload.Function, overload.FixedCost)));
        Assert.Equal("extension two-var-comprehensions v0", definition.Source);
        Assert.Equal(10, definition.Macros.Length);
    }

    // The bindings of the tests: d is the map {'a': 1, 'b': 2}.
    private static CelBindings Bindings()
    {
        CelBindings defaults = TwoVarEnvironment.Default();
        return defaults.Add("d", defaults.Values["counts"]);
    }

    private static CelMap AsMap(CelValue value)
    {
        Assert.True(value.TryGetMap(out CelMap? map), value.Kind.ToString());
        return map;
    }

    private static CelValue Run(ExprNode root, NodeFactory nodes, CelBindings bindings) =>
        PlanOf(root, nodes).Evaluate(bindings.Values, null, CancellationToken.None);

    private static Plan PlanOf(ExprNode root, NodeFactory nodes)
    {
        CheckResult result = TwoVarEnvironment.Environment.Check(nodes.Build(root, new LineTable(Source)));
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        return Planner.Plan(result.Expression!, TwoVarEnvironment.Environment.Declarations);
    }

    private static Plan PlanWithoutTypeCheck(ExprNode root, NodeFactory nodes) =>
        Planner.PlanWithoutTypeCheck(nodes.Build(root, new LineTable(Source)), TwoVarEnvironment.Environment.Declarations);

    private static CelValue Evaluate(Plan plan, ulong limit) => plan.Evaluate(Bindings().Values, limit, CancellationToken.None);

    private static CelError Error(CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error), value.Kind.ToString());
        return error;
    }

    private static CallNode Insert(NodeFactory nodes, params ExprNode[] arguments) =>
        nodes.Call(ExtensionMacros.MapInsertFunction, null, [.. arguments], CallOffset);

    // The map {first: 26, second: 25}, the second argument of the two-map form.
    private static StructNode SecondMap(NodeFactory nodes, string first, string second) =>
        nodes.Struct(
            string.Empty,
            [nodes.Entry(Str(nodes, first), Int(nodes, 26), false, 30), nodes.Entry(Str(nodes, second), Int(nodes, 25), false, 40)],
            28);

    private static ConstantNode Int(NodeFactory nodes, long value) => nodes.Constant(CelValue.FromInt64(value), 20);

    private static ConstantNode Str(NodeFactory nodes, string value) => nodes.Constant(CelValue.FromString(value), 20);

    private static string Name(CelValue value) => value.TryGetString(out string? text) ? text : throw new InvalidOperationException(value.Kind.ToString());

    private static long Integer(CelValue value) => value.TryGetInt64(out long number) ? number : throw new InvalidOperationException(value.Kind.ToString());
}
