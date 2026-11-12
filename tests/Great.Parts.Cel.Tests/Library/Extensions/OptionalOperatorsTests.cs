using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions;

// The seven operators of the set optional (OptionalOperators): the table of their declarations in the order of the set, the argument
// costs, the six index overloads on keys of every numeric kind, on an operand of the type dyn, on a held value that is no list and
// no map, and on lists and maps of the application with an element that is an error value, and the units of an index of an empty
// optional and of a JSON array that an optional holds. The values that the examples fix for lists and maps are not repeated.
public sealed class OptionalOperatorsTests : IDisposable
{
    private static readonly CelError Failure = new(CelErrorCode.InvalidArgument, CelErrorStage.Evaluate, "failed", default, null, null);

    private const string Keys = """
        dyn({1: 'a'})[?1u] => optional.of('a')
        dyn({1u: 'a'})[?1] => optional.of('a')
        dyn({1: 'a'})[?1.0] => optional.of('a')
        dyn({1: 'a'})[?1.5] => optional.none()
        dyn({1: 'a'})[?'1'] => optional.none()
        dyn({1: 'a'})[?true] => optional.none()
        dyn({'a': 1})[?1] => optional.none()
        optional.of(dyn({1: 'a'}))[?1u] => optional.of('a')
        optional.of(dyn({1: 'a'}))[?2u] => optional.none()
        optional.of(dyn({1: 'a'}))[1u] => optional.of('a')
        """;

    // An index of the type dyn takes the two overloads of an optional as it takes those of a list and a map, by the kind of the value.
    private const string DynOperand = """
        dyn(optional.of([1]))[0] => optional.of(1)
        dyn(optional.of([1]))[5] => optional.none()
        dyn(optional.of({'a': 1}))['a'] => optional.of(1)
        dyn(optional.of({'a': 1}))['b'] => optional.none()
        dyn(optional.none())[0] => optional.none()
        dyn(optional.none())['a'] => optional.none()
        dyn([1, 2])[1] => 2
        dyn({'a': 1})['a'] => 1
        """;

    // The index of an optional takes an int only, and the held value has to be a list or a map: the kinds are the guard of the
    // dispatch, so the call is EvalNoMatchingOverload where the index of the standard library takes a uint or a double.
    private const string Mismatches = """
        Evaluate | dyn([1, 2])^[?1u] | EvalNoMatchingOverload | no such overload: _[?_](list, uint)
        Evaluate | dyn([1, 2])^[?1.0] | EvalNoMatchingOverload | no such overload: _[?_](list, double)
        Evaluate | dyn(optional.of(1))^[0] | EvalNoMatchingOverload | no such overload: _[_](optional_type, int)
        Evaluate | optional.of(dyn(1))^[?0] | EvalNoMatchingOverload | no such overload: _[?_](optional_type, int)
        """;

    // An element that is an error value is the result of the index, with the place of the call; an element that nothing reads is no error.
    private const string ErrorElements = """
        ys[?0] => optional.of(1)
        ys[?5] => optional.none()
        ms[?'a'] => optional.of(1)
        ms[?'z'] => optional.none()
        optional.of(ys)[0] => optional.of(1)
        optional.of(ms)['a'] => optional.of(1)
        """;

    private const string ErrorElementErrors = """
        Evaluate | ys^[?1] | InvalidArgument | failed
        Evaluate | optional.of(ys)^[1] | InvalidArgument | failed
        Evaluate | ms^[?'b'] | InvalidArgument | failed
        Evaluate | optional.of(ms)^['b'] | InvalidArgument | failed
        """;

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("ys", CelType.ListOf(CelType.Dyn))
        .AddVariable("ms", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddVariable("j", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly JsonDocument document = JsonDocument.Parse("""{"a": 1, "l": [10, 20, 30]}""");

    public void Dispose() => document.Dispose();

    [Fact]
    public static void Overloads_AreTheSevenOfTheTableInItsOrder()
    {
        ImmutableArray<OverloadBinding> table = OptionalOperators.Overloads("test");

        Assert.Equal(
            [
                "select_optional_field",
                "list_optindex_optional_int",
                "optional_list_optindex_optional_int",
                "map_optindex_optional_value",
                "optional_map_optindex_optional_value",
                "optional_list_index_int",
                "optional_map_index_value",
            ],
            table.Select(binding => binding.Declaration.OverloadId));
        Assert.Equal(["_?._", "_[?_]", "_[?_]", "_[?_]", "_[?_]", "_[_]", "_[_]"], table.Select(binding => binding.Function));
        Assert.Equal(
            [
                "(dyn, string) -> optional_type(V)",
                "(list(V), int) -> optional_type(V)",
                "(optional_type(list(V)), int) -> optional_type(V)",
                "(map(K, V), K) -> optional_type(V)",
                "(optional_type(map(K, V)), K) -> optional_type(V)",
                "(optional_type(list(V)), int) -> optional_type(V)",
                "(optional_type(map(K, V)), K) -> optional_type(V)",
            ],
            table.Select(binding => "(" + string.Join(", ", binding.Declaration.ParameterTypes) + ") -> " + binding.Declaration.ResultType));
        Assert.All(table, binding => Assert.False(binding.Declaration.IsReceiverStyle));
        Assert.All(table, binding => Assert.Equal("test", binding.Source));
    }

    // The selection is a special form: no implementation, no cost of its own, and the validator that the checker runs; the six
    // indexes have an implementation, the unit of the overload and no validator.
    [Fact]
    public static void SelectionIsASpecialForm_AndTheIndexesAreOrdinaryOverloads()
    {
        ImmutableArray<OverloadBinding> table = OptionalOperators.Overloads("test");

        Assert.Null(table[0].Implementation);
        Assert.NotNull(table[0].Validator);
        Assert.Equal(0UL, table[0].FixedCost);
        Assert.Null(table[0].ArgumentCost);
        foreach (OverloadBinding index in table.Skip(1))
        {
            Assert.NotNull(index.Implementation);
            Assert.Null(index.Validator);
            Assert.Equal(1UL, index.FixedCost);
            Assert.NotNull(index.ArgumentCost);
        }
    }

    // The argument cost of an overload over an optional is that of the held list or map, and nothing for an empty optional or for a
    // value that is no optional.
    [Fact]
    public static void ArgumentCostOfAnIndexOverAnOptional_IsThatOfTheHeldValue()
    {
        ImmutableArray<OverloadBinding> table = OptionalOperators.Overloads("test");
        CelValue list = CelValue.FromList(CelList.Create(CelValue.FromInt64(1)));
        CelValue map = CelValue.FromMap(CelMap.Create([new(CelValue.FromString("key"), CelValue.FromInt64(1))]));
        CelValue key = CelValue.FromString("abcdefghijk");
        CelValue none = CelValue.FromOptional(default);

        Assert.Equal(0UL, table[1].ArgumentCost!([list, CelValue.FromInt64(0)]));
        Assert.Equal(2UL, table[3].ArgumentCost!([map, key]));
        Assert.Equal(0UL, table[2].ArgumentCost!([Held(list), CelValue.FromInt64(0)]));
        Assert.Equal(0UL, table[2].ArgumentCost!([none, CelValue.FromInt64(0)]));
        Assert.Equal(2UL, table[4].ArgumentCost!([Held(map), key]));
        Assert.Equal(2UL, table[6].ArgumentCost!([Held(map), key]));
        Assert.Equal(0UL, table[4].ArgumentCost!([none, key]));
        Assert.Equal(0UL, table[6].ArgumentCost!([map, key]));
    }

    [Fact]
    public void KeysOfEveryKind_AreLookedUpByCelEqualityAcrossTheNumericKinds() =>
        Assert.Empty(ExtensionRows.CheckValues(Environment, Keys, Bindings()));

    [Fact]
    public void OperandOfTheTypeDyn_TakesTheOverloadsOfAnOptionalByTheKindOfTheValue() =>
        Assert.Empty(ExtensionRows.CheckValues(Environment, DynOperand, Bindings()));

    [Fact]
    public void IndexOfAnOptional_TakesAnIntAndAHeldListOrMap() =>
        Assert.Empty(ExtensionRows.CheckErrors(Environment, Mismatches, Bindings()));

    [Fact]
    public void ElementThatNothingReads_IsNoError() =>
        Assert.Empty(ExtensionRows.CheckValues(Environment, ErrorElements, Bindings()));

    [Fact]
    public void ElementThatIsAnErrorValue_IsTheResultAtTheCall() =>
        Assert.Empty(ExtensionRows.CheckErrors(Environment, ErrorElementErrors, Bindings()));

    // An empty optional costs the unit of the overload and nothing for an argument: the operand is the 1 of optional.none().
    [Theory]
    [InlineData("optional.none()[0]")]
    [InlineData("optional.none()['a']")]
    [InlineData("optional.none()[?0]")]
    [InlineData("optional.none()[?'a']")]
    public static void IndexOfAnEmptyOptional_CostsTwoUnits(string expression) =>
        Assert.Equal(2UL, Units(expression, new CelBindings()));

    // A JSON array that an optional holds charges its length at the index, as the array itself does.
    [Theory]
    [InlineData("optional.of(j.l)[?1]")]
    [InlineData("optional.of(j.l)[1]")]
    public void IndexOfAnOptionalThatHoldsAJsonArray_ChargesTheLengthOfTheArray(string expression)
    {
        CelBindings bindings = Bindings();

        ulong index = Units(expression, bindings) - Units("optional.of(j.l)", bindings);

        Assert.True(index >= 4, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private CelBindings Bindings() => new CelBindings()
        .Add<CelValue>("ys", CelValue.FromList(Elements(1L, Failure)))
        .Add<CelValue>("ms", CelValue.FromMap(Entries(("a", 1L), ("b", Failure))))
        .Add("j", document.RootElement);

    private static CelValue Held(CelValue value) => CelValue.FromOptional(new CelOptional<CelValue>(value));

    private static ulong Units(string expression, CelBindings bindings)
    {
        Plan plan = Environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
        CelValue value = plan.Run(frame);
        Assert.NotEqual(CelValueKind.Error, value.Kind);
        return frame.ChargedUnits;
    }

    // A list of the application whose elements are longs and error values.
    private static CelList Elements(params object[] items) => CelList.CreateView<object>(items, Convert);

    // A map of the application, in the order of the entries.
    private static CelMap Entries(params (string Key, object Value)[] entries)
    {
        var source = new Dictionary<string, object>();
        foreach ((string key, object value) in entries)
        {
            source.Add(key, value);
        }

        return CelMap.CreateView(source, Convert, keepSourceOrder: true);
    }

    private static CelValue Convert(object item) => item switch
    {
        CelError error => CelValue.FromError(error),
        _ => CelValue.FromInt64((long)item),
    };
}
