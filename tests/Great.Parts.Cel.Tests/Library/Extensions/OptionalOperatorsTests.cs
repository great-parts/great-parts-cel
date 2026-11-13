using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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
// optional and of a JSON array that an optional holds. The values that the examples fix for lists and maps are not repeated. The
// keys of the type dyn of every kind are tried on each operand a list, a map and an empty optional can be written as, in both
// syntaxes, with the outcome of the table "Indexes" of the page of the set.
public sealed class OptionalOperatorsTests : IDisposable
{
    private const string None = "optional.none()";

    private static readonly CelError Failure = new(CelErrorCode.InvalidArgument, CelErrorStage.Evaluate, "failed", default, null, null);

    // The key of a list of two elements, 10 and 20, and what the index gives: a value as an expression, or "!" and the error, with
    // the kind of the key for EvalNoMatchingOverload. An index outside the list is none, but a key that is no index at all is an error.
    private static readonly (string Key, string Outcome)[] ListKeys =
    [
        ("dyn(0u)", "optional.of(10)"),
        ("dyn(1u)", "optional.of(20)"),
        ("dyn(2u)", None),
        ("dyn(9223372036854775807u)", None),
        ("dyn(9223372036854775808u)", "!IndexOutOfRange"),
        ("dyn(18446744073709551615u)", "!IndexOutOfRange"),
        ("dyn(-0.0)", "optional.of(10)"),
        ("dyn(1.0)", "optional.of(20)"),
        ("dyn(-1.0)", None),
        ("dyn(1.5)", "!InvalidArgument"),
        ("dyn(0.0 / 0.0)", "!InvalidArgument"),
        ("dyn(1.0 / 0.0)", "!InvalidArgument"),
        ("dyn(-1.0 / 0.0)", "!InvalidArgument"),
        ("dyn(1e19)", "!IndexOutOfRange"),
        ("dyn(-1e19)", "!IndexOutOfRange"),
        ("dyn(-9223372036854775808.0)", "!IndexOutOfRange"),
        ("dyn(-9223372036854774784.0)", None),
        ("dyn(9223372036854775808.0)", "!IndexOutOfRange"),
        ("dyn(true)", NoOverload("bool")),
        ("dyn('a')", NoOverload("string")),
        ("dyn(b'a')", NoOverload("bytes")),
        ("dyn(null)", NoOverload("null_type")),
        ("dyn([1])", NoOverload("list")),
        ("dyn({1: 2})", NoOverload("map")),
        ("dyn(int)", NoOverload("type")),
        ("dyn(duration('1s'))", NoOverload("duration")),
        ("dyn(timestamp(0))", NoOverload("timestamp")),
    ];

    // The key of {1: 'x'}: the key lookup across the numeric kinds, and nothing for a kind that is no key.
    private static readonly (string Key, string Outcome)[] MapKeys =
    [
        ("dyn(1)", "optional.of('x')"),
        ("dyn(1u)", "optional.of('x')"),
        ("dyn(1.0)", "optional.of('x')"),
        ("dyn(2)", None),
        ("dyn(2u)", None),
        ("dyn(1.5)", None),
        ("dyn(true)", None),
        ("dyn('a')", None),
        ("dyn(b'a')", None),
        ("dyn([1])", None),
        ("dyn({1: 2})", None),
        ("dyn(null)", None),
        ("dyn(int)", None),
        ("dyn(duration('1s'))", None),
        ("dyn(timestamp(0))", None),
    ];

    private static readonly string[] EmptyOperands = [None, "dyn(optional.none())", "optional.ofNonZeroValue([])"];

    private static readonly string[] CostOperands = ["optional.of([10, 20])", "optional.of(jl)", "jl"];

    // The syntaxes of an index that gives an optional: with the "?", and, over an optional operand, without it.
    private static readonly string[] Syntaxes = ["?", string.Empty];

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
        dyn([1, 2])[?1u] => optional.of(2)
        dyn([1, 2])[?1.0] => optional.of(2)
        dyn([1, 2])[?2u] => optional.none()
        optional.of(dyn([1, 2]))[?1u] => optional.of(2)
        optional.of(dyn([1, 2]))[1.0] => optional.of(2)
        optional.of(dyn({1: 'a'}))[1.0] => optional.of('a')
        """;

    // A held value that is no list and no map, and an operand that is no optional, list or map at all, are EvalNoMatchingOverload
    // whatever the key is: the third difference from cel-go ("Differences from cel-go" of the page of the set).
    private const string Mismatches = """
        Evaluate | dyn(optional.of(1))^[0] | EvalNoMatchingOverload | no such overload: _[_](optional_type, int)
        Evaluate | optional.of(dyn(1))^[?0] | EvalNoMatchingOverload | no such overload: _[?_](optional_type, int)
        Evaluate | dyn(1)^[?0] | EvalNoMatchingOverload | no such overload: _[?_](int, int)
        Evaluate | dyn(1)^[?dyn('a')] | EvalNoMatchingOverload | no such overload: _[?_](int, string)
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
        .AddVariable("jl", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly JsonDocument document = JsonDocument.Parse("""{"a": 1, "l": [10, 20, 30]}""");

    private readonly JsonDocument jsonList = JsonDocument.Parse("[10, 20]");

    public void Dispose()
    {
        document.Dispose();
        jsonList.Dispose();
    }

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
    public void IndexOfAValueThatIsNoListOrMap_IsEvalNoMatchingOverload() =>
        Assert.Empty(ExtensionRows.CheckErrors(Environment, Mismatches, Bindings()));

    [Fact]
    public void ElementThatNothingReads_IsNoError() =>
        Assert.Empty(ExtensionRows.CheckValues(Environment, ErrorElements, Bindings()));

    [Fact]
    public void ElementThatIsAnErrorValue_IsTheResultAtTheCall() =>
        Assert.Empty(ExtensionRows.CheckErrors(Environment, ErrorElementErrors, Bindings()));

    // The six overloads fit a key of any kind: what the call does is decided by the kinds of its values, so Matches reads the kind of
    // the operand and not that of the key, with or without the marks of the planner. An operand that is no list, map or optional
    // fits none of them.
    [Fact]
    public static void IndexOverloads_FitAKeyOfAnyKind_ButNotAnOperandThatIsNoListMapOrOptional()
    {
        ImmutableArray<OverloadBinding> table = OptionalOperators.Overloads("test");
        CelValue list = CelValue.FromList(CelList.Create(CelValue.FromInt64(1)));
        CelValue map = CelValue.FromMap(CelMap.Create([new(CelValue.FromString("key"), CelValue.FromInt64(1))]));
        CelValue[] keys =
        [
            CelValue.Null,
            CelValue.FromBoolean(true),
            CelValue.FromInt64(1),
            CelValue.FromUInt64(1),
            CelValue.FromDouble(1.5),
            CelValue.FromString("a"),
            CelValue.FromBytes("a"u8.ToArray()),
            list,
            map,
            CelValue.FromType(CelType.Int),
        ];
        (int Overload, CelValue Operand)[] operands = [(1, list), (2, Held(list)), (3, map), (4, Held(map)), (5, Held(list)), (6, Held(map))];

        foreach ((int overload, CelValue operand) in operands)
        {
            foreach (CelValue key in keys)
            {
                Assert.True(table[overload].Matches([operand, key], [], null).IsFit);
                Assert.True(table[overload].Matches([operand, key], [true, true], null).IsFit);
                Assert.False(table[overload].Matches([CelValue.FromInt64(1), key], [], null).IsFit);
            }
        }
    }

    // The units do not depend on the key: the argument cost reads the held list or map, and an index is the unit of the overload
    // whether the key is a uint, a double, an int or no index at all; a JSON array charges its length in each case.
    [Fact]
    public void IndexOfAListOrAnOptionalList_ChargesTheSameForAKeyOfAnyKind()
    {
        CelBindings bindings = Bindings();

        foreach (string operand in CostOperands)
        {
            ulong expected = Units(operand + "[?dyn(1)]", bindings);

            Assert.Equal(expected, Units(operand + "[?dyn(1u)]", bindings));
            Assert.Equal(expected, Units(operand + "[?dyn(1.0)]", bindings));
            Assert.Equal(expected, Units(operand + "[?dyn(9)]", bindings));
            Assert.Equal(expected, Units(operand + "[?dyn(true)]", bindings, allowError: true));
            Assert.Equal(expected, Units(operand + "[?dyn(null)]", bindings, allowError: true));
        }
    }

    [Fact]
    public void IndexOfAListByAKeyOfTheTypeDyn_GivesAnElementOrNone()
    {
        (string values, _) = IndexRows(["[10, 20]", "dyn([10, 20])", "jl"], ["optional.of([10, 20])", "optional.of(dyn([10, 20]))", "optional.of(jl)"], ListKeys);

        Assert.Empty(ExtensionRows.CheckValues(Environment, values, Bindings()));
    }

    [Fact]
    public void IndexOfAListByAKeyThatIsNoIndex_IsTheErrorOfTheKey()
    {
        (_, string errors) = IndexRows(["[10, 20]", "dyn([10, 20])", "jl"], ["optional.of([10, 20])", "optional.of(dyn([10, 20]))", "optional.of(jl)"], ListKeys);

        Assert.Empty(ExtensionRows.CheckErrors(Environment, errors, Bindings()));
    }

    [Fact]
    public void IndexOfAMapByAKeyOfTheTypeDyn_FindsTheKeyOrGivesNone()
    {
        (string values, _) = IndexRows(["{1: 'x'}", "dyn({1: 'x'})"], ["optional.of({1: 'x'})", "optional.of(dyn({1: 'x'}))"], MapKeys);

        Assert.Empty(ExtensionRows.CheckValues(Environment, values, Bindings()));
    }

    [Fact]
    public void IndexOfAnEmptyOptionalByAKeyOfAnyKind_IsTheEmptyOptional()
    {
        var rows = new StringBuilder();
        foreach (string operand in EmptyOperands)
        {
            foreach (string syntax in Syntaxes)
            {
                foreach (string key in ListKeys.Concat(MapKeys).Select(entry => entry.Key).Distinct())
                {
                    rows.Append(operand).Append('[').Append(syntax).Append(key).Append("] == ").Append(None).Append(" => true\n");
                }
            }
        }

        Assert.Empty(ExtensionRows.CheckValues(Environment, rows.ToString(), Bindings()));
    }

    // The key of an index of an empty optional is evaluated like every argument, so an error in it is the result, where cel-go
    // returns the empty optional (the registry of known differences has the signature).
    [Fact]
    public void KeyThatIsAnErrorOfAnIndexOfAnEmptyOptional_IsTheResult()
    {
        var rows = new StringBuilder();
        foreach (string operand in EmptyOperands)
        {
            foreach (string syntax in Syntaxes)
            {
                rows.Append("Evaluate | ").Append(operand).Append('[').Append(syntax).Append("dyn(1) ^/ 0] | DivisionByZero | division by zero\n");
            }
        }

        Assert.Empty(ExtensionRows.CheckErrors(Environment, rows.ToString(), Bindings()));
    }

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
        .Add("j", document.RootElement)
        .Add("jl", jsonList.RootElement);

    private static CelValue Held(CelValue value) => CelValue.FromOptional(new CelOptional<CelValue>(value));

    // The outcome of a key of a kind that no overload takes, in the form that IndexRows reads: "!" and the code with the kind.
    private static string NoOverload(string kind) => "!EvalNoMatchingOverload:" + kind;

    private static ulong Units(string expression, CelBindings bindings, bool allowError = false)
    {
        Plan plan = Environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
        CelValue value = plan.Run(frame);
        Assert.Equal(allowError, value.Kind == CelValueKind.Error);
        return frame.ChargedUnits;
    }

    // The rows of an index of each operand by each key: a value row for an outcome that is a value, and an error row, at the "[" of
    // the index, for an outcome that starts with "!". An operand that is an optional is indexed in both syntaxes.
    private static (string Values, string Errors) IndexRows(string[] operands, string[] optionalOperands, (string Key, string Outcome)[] keys)
    {
        var values = new StringBuilder();
        var errors = new StringBuilder();
        foreach (string operand in operands)
        {
            AddRows(values, errors, operand, "?", "list", keys);
        }

        foreach (string operand in optionalOperands)
        {
            AddRows(values, errors, operand, "?", "optional_type", keys);
            AddRows(values, errors, operand, string.Empty, "optional_type", keys);
        }

        return (values.ToString(), errors.ToString());
    }

    private static void AddRows(StringBuilder values, StringBuilder errors, string operand, string syntax, string kind, (string Key, string Outcome)[] keys)
    {
        string function = syntax.Length > 0 ? "_[?_]" : "_[_]";
        foreach ((string key, string outcome) in keys)
        {
            if (outcome[0] != '!')
            {
                values.Append(operand).Append('[').Append(syntax).Append(key).Append("] == ").Append(outcome).Append(" => true\n");
                continue;
            }

            string[] error = outcome[1..].Split(':');
            string text = error.Length > 1 ? "no such overload: " + function + "(" + kind + ", " + error[1] + ")" : "-";
            errors.Append("Evaluate | ").Append(operand).Append("^[").Append(syntax).Append(key).Append("] | ").Append(error[0]).Append(" | ").Append(text).Append('\n');
        }
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
