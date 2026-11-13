using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The operators of the set optional, version 2 (the page of the extension sets, "optional"): the optional selection
// _?._ (select_optional_field), the optional index _[?_] (list_optindex_optional_int, optional_list_optindex_optional_int,
// map_optindex_optional_value, optional_map_optindex_optional_value) and the index of an optional _[_]
// (optional_list_index_int, optional_map_index_value), which are seven of the eighteen overloads. The selection and the index
// of an optional value are optional: the result is optional.none() when the key, the index or the field is absent, the
// operand is optional.none() or the field of a message is not set, and the optional of the element otherwise. A field
// of a message is set by the rules of its presence: a proto3 scalar with implicit presence is not set at its zero, a
// proto3 optional field, a wrapper and a message field are set when they were set. A selection of a field on an optional
// message or map, without the "?", is the same optional selection (the cases optional_chaining_4 to _9 of the conformance
// tests). The values are those of cel-go v0.32.0 (a probe of the oracle with the flags of the conformance runner and the
// set optional, 2026-11-11). The messages are the test messages of the suite (ProtobufExample), in the container of
// their package. Rows are those of ExtensionRows. Variables: o = optional.of(5) and n = optional.none(), both of the type
// optional_type(int).
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-3")]
[Trait("Requirement", "F-2")]
public sealed class OptionalAccessTests
{
    private const string MapSelection = """
        {'a': 1}.?a => optional.of(1)
        {'a': 1}.?b => optional.none()
        {'a': {'b': 2}}.?a.b => optional.of(2)
        {'a': {'b': 2}}.?a.c => optional.none()
        {'a': {'b': 2}}.?a.?b => optional.of(2)
        {'a': {'b': 2}}.?a.?c => optional.none()
        {}.?a => optional.none()
        {'a': dyn(null)}.?a.hasValue() => true
        {'a': dyn(null)}.?a.value() => null
        dyn({'a': 1}).?a => optional.of(1)
        dyn({'a': 1}).?b => optional.none()
        {'a': 1}.?a.orValue(0) => 1
        {'a': 1}.?b.orValue(0) => 0
        """;

    // On an optional the selection looks into the value: optional.none() stays empty, and a field that the value has not
    // gives optional.none().
    private const string OptionalOperand = """
        optional.of({'a': 1}).?a => optional.of(1)
        optional.of({'a': 1}).?b => optional.none()
        optional.none().?a => optional.none()
        optional.of({'a': 1}).a => optional.of(1)
        optional.of({'a': 1}).b => optional.none()
        optional.none().a => optional.none()
        optional.of({'c': {'e': 'x'}}).c.e.orValue('d') => 'x'
        optional.of({'c': {}}).c.missing.orValue('d') => 'd'
        optional.of({'c': {}}).c.missing.or(optional.of(['v'])[0]).orValue('d') => 'v'
        """;

    private const string MessageSelection = """
        TestAllTypes{single_int32: 5}.?single_int32 => optional.of(5)
        TestAllTypes{}.?single_int32 => optional.none()
        TestAllTypes{single_int32: 0}.?single_int32 => optional.none()
        TestAllTypes{single_string: 'a'}.?single_string => optional.of('a')
        TestAllTypes{}.?single_string => optional.none()
        TestAllTypes{optional_bool: false}.?optional_bool => optional.of(false)
        TestAllTypes{}.?optional_bool => optional.none()
        TestAllTypes{single_bool: false}.?single_bool => optional.none()
        TestAllTypes{single_int32_wrapper: 0}.?single_int32_wrapper => optional.of(0)
        TestAllTypes{}.?single_int32_wrapper => optional.none()
        TestAllTypes{repeated_string: ['foo']}.?repeated_string.value() => ['foo']
        TestAllTypes{}.?repeated_string.hasValue() => false
        TestAllTypes{}.?single_nested_message.hasValue() => false
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 4}}.?single_nested_message.hasValue() => true
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 4}}.?single_nested_message.bb => optional.of(4)
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 4}}.?single_nested_message.?bb => optional.of(4)
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 0}}.?single_nested_message.?bb => optional.none()
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 0}}.?single_nested_message.bb => optional.none()
        TestAllTypes{}.?single_nested_message.bb => optional.none()
        TestAllTypes{map_string_string: {'a': 'b'}}.?map_string_string['a'] => optional.of('b')
        TestAllTypes{map_string_string: {'a': 'b'}}.?map_string_string[?'a'] => optional.of('b')
        TestAllTypes{map_string_string: {'a': 'b'}}.?map_string_string[?'z'] => optional.none()
        TestAllTypes{repeated_int32: [7, 8]}.?repeated_int32[1] => optional.of(8)
        TestAllTypes{repeated_int32: [7, 8]}.?repeated_int32[5] => optional.none()
        TestAllTypes{single_int32: 5}.?single_int32.orValue(0) => 5
        TestAllTypes{}.?single_int32.orValue(9) => 9
        TestAllTypes{single_int32: 5}.?single_int32.optMap(v, v * 2) => optional.of(10)
        optional.of(TestAllTypes{single_int32: 5}).?single_int32 => optional.of(5)
        optional.of(TestAllTypes{}).?single_int32 => optional.none()
        optional.of(TestAllTypes{single_int32: 5}).single_int32 => optional.of(5)
        optional.of(TestAllTypes{}).single_int32 => optional.none()
        optional.none().?single_int32 => optional.none()
        optional.ofNonZeroValue(TestAllTypes{}).hasValue() => false
        optional.ofNonZeroValue(TestAllTypes{single_int32: 1}).hasValue() => true
        optional.ofNonZeroValue(TestAllTypes{single_int32: 0}).hasValue() => false
        """;

    // _[?_] on a list: the index in range gives the element, any other index, negative ones too, gives optional.none();
    // on a map the key is looked up by CEL equality of the keys of its kind.
    private const string OptionalIndex = """
        [1, 2][?0] => optional.of(1)
        [1, 2][?1] => optional.of(2)
        [1, 2][?2] => optional.none()
        [1, 2][?5] => optional.none()
        [1, 2][?-1] => optional.none()
        [][?0] => optional.none()
        optional.of([1, 2])[?1] => optional.of(2)
        optional.of([1, 2])[?2] => optional.none()
        optional.of([1, 2])[?-1] => optional.none()
        optional.none()[?0] => optional.none()
        {'a': 1}[?'a'] => optional.of(1)
        {'a': 1}[?'b'] => optional.none()
        {1: 'x'}[?1] => optional.of('x')
        {1: 'x'}[?2] => optional.none()
        {1u: 1.0, 2: 2.0, 3u: 3.0}[?3.0] => optional.of(3.0)
        {}[?'a'] => optional.none()
        optional.of({'a': 1})[?'a'] => optional.of(1)
        optional.of({'a': 1})[?'b'] => optional.none()
        optional.none()[?'a'] => optional.none()
        dyn([1, 2])[?1] => optional.of(2)
        dyn([1, 2])[?5] => optional.none()
        dyn({'a': 1})[?'a'] => optional.of(1)
        [1, 2, 3][?1].orValue(0) => 2
        [1, 2, 3][?7].orValue(0) => 0
        {'a': [1, 2]}[?'a'][0] => optional.of(1)
        {'a': [1, 2]}[?'b'][0] => optional.none()
        """;

    // _[_] with an optional on the left is optional too: it never fails for a missing key or an index out of range, it
    // gives optional.none(), where the same index of the value itself is NoSuchKey or IndexOutOfRange.
    private const string IndexOfAnOptional = """
        optional.of([1, 2])[0] => optional.of(1)
        optional.of([1, 2])[1] => optional.of(2)
        optional.of([1, 2])[2] => optional.none()
        optional.of([1, 2])[-1] => optional.none()
        optional.none()[0] => optional.none()
        optional.of({'a': 1})['a'] => optional.of(1)
        optional.of({'a': 1})['b'] => optional.none()
        optional.none()['a'] => optional.none()
        optional.of({1: 'x'})[1] => optional.of('x')
        optional.of({1: 'x'})[2] => optional.none()
        optional.of([[1], [2]])[1][0] => optional.of(2)
        optional.of([[1], [2]])[3][0] => optional.none()
        """;

    // A key of the type dyn is read by the kind of its value, as the optional qualifiers of cel-go read it ("Indexes" of the
    // page of the set): a list takes an int, a uint and a double with an integral value, a map looks the key up across the
    // numeric kinds and finds nothing for a kind that is no key, and an empty optional is empty for any key, whatever the
    // static type of the operand is, optional.none() and an optional of dyn included.
    private const string DynKeys = """
        [1, 2][?dyn(1u)] => optional.of(2)
        [1, 2][?dyn(2u)] => optional.none()
        [1, 2][?dyn(9223372036854775807u)] => optional.none()
        [1, 2][?dyn(1.0)] => optional.of(2)
        [1, 2][?dyn(-0.0)] => optional.of(1)
        [1, 2][?dyn(5.0)] => optional.none()
        [1, 2][?dyn(-1.0)] => optional.none()
        optional.of([1, 2])[?dyn(1u)] => optional.of(2)
        optional.of([1, 2])[dyn(1.0)] => optional.of(2)
        optional.of([1, 2])[dyn(2u)] => optional.none()
        optional.of(dyn([1, 2]))[dyn(1u)] => optional.of(2)
        optional.of(dyn([1, 2]))[?dyn(1.0)] => optional.of(2)
        optional.none()[dyn(1u)] => optional.none()
        optional.none()[dyn('a')] => optional.none()
        optional.none()[dyn(b'a')] => optional.none()
        optional.none()[?dyn(1.0)] => optional.none()
        optional.none()[?dyn(null)] => optional.none()
        optional.none()[?dyn([1])] => optional.none()
        optional.none()[?dyn({'a': 1})] => optional.none()
        optional.none()[?dyn(int)] => optional.none()
        dyn(optional.none())[dyn(b'a')] => optional.none()
        optional.of(dyn({'a': 1}))[dyn('a')] => optional.of(1)
        optional.of(dyn({'a': 1}))[dyn(1)] => optional.none()
        optional.of(dyn({'a': 1}))[?dyn('b')] => optional.none()
        optional.of({1: 'x'})[dyn(1u)] => optional.of('x')
        optional.of({1: 'x'})[dyn(1.0)] => optional.of('x')
        optional.of({1: 'x'})[?dyn(2u)] => optional.none()
        optional.of({1: 'x'})[dyn('a')] => optional.none()
        optional.of({1: 'x'})[?dyn(true)] => optional.none()
        {'a': 1}[?dyn('a')] => optional.of(1)
        {'a': 1}[?dyn(1)] => optional.none()
        {'a': 1}[?dyn(1.5)] => optional.none()
        {1: 'x'}[?dyn(1u)] => optional.of('x')
        {1: 'x'}[?dyn(1.0)] => optional.of('x')
        {1: 'x'}[?dyn(1.5)] => optional.none()
        {1: 'x'}[?dyn(true)] => optional.none()
        {1: 'x'}[?dyn('a')] => optional.none()
        """;

    // A key that is no index of a list stays an error where cel-go fails too: a kind that a list is not indexed by, a double that
    // is not integral, and a uint or a double beyond the range of int64, which is no empty optional either. The place is the "[".
    private const string DynKeyErrors = """
        Evaluate | [1, 2]^[?dyn('a')] | EvalNoMatchingOverload | no such overload: _[?_](list, string)
        Evaluate | [1, 2]^[?dyn(true)] | EvalNoMatchingOverload | no such overload: _[?_](list, bool)
        Evaluate | [1, 2]^[?dyn(b'a')] | EvalNoMatchingOverload | no such overload: _[?_](list, bytes)
        Evaluate | [1, 2]^[?dyn(null)] | EvalNoMatchingOverload | no such overload: _[?_](list, null_type)
        Evaluate | [1, 2]^[?dyn([1])] | EvalNoMatchingOverload | no such overload: _[?_](list, list)
        Evaluate | optional.of([1, 2])^[dyn('a')] | EvalNoMatchingOverload | no such overload: _[_](optional_type, string)
        Evaluate | optional.of([1, 2])^[?dyn(b'a')] | EvalNoMatchingOverload | no such overload: _[?_](optional_type, bytes)
        Evaluate | [1, 2]^[?dyn(1.5)] | InvalidArgument | -
        Evaluate | [1, 2]^[?dyn(9223372036854775808u)] | IndexOutOfRange | -
        Evaluate | [1, 2]^[?dyn(1e19)] | IndexOutOfRange | -
        Evaluate | optional.none()[dyn(1) ^/ 0] | DivisionByZero | division by zero
        """;

    // The selection of a value that is not a map, a message or an optional of one is an error of the evaluation when the type
    // was dyn: FieldSelectionNotSupported at the ".", with the text of the table of the codes.
    private const string EvaluationErrors = """
        Evaluate | dyn(1)^.?a | FieldSelectionNotSupported | value of type 'int' does not support field selection of 'a'
        Evaluate | dyn('abc')^.?size | FieldSelectionNotSupported | value of type 'string' does not support field selection of 'size'
        Evaluate | dyn([1])^.?a | FieldSelectionNotSupported | -
        """;

    // The selection and the index of the set apply to the kinds that have them: not to a scalar, not to a list by a string,
    // not to a map by a key of another kind, and the index of a list by an int only when the type of the key is known (a key of
    // the type dyn is read by its kind: DynKeys). The place is the "." of a selection and the "[" of an index; the text of the selection
    // that a message does not have is that of the table of the codes.
    private const string Mistakes = """
        Check | 'abc'^.?size | NoSuchField | -
        Check | TestAllTypes{}^.?nosuchfield | NoSuchField | undefined field 'nosuchfield' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | optional.of(TestAllTypes{})^.nosuchfield | NoSuchField | undefined field 'nosuchfield' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | optional.of(TestAllTypes{})^.?nosuchfield | NoSuchField | undefined field 'nosuchfield' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | optional.of(1)^.foo | NoSuchField | -
        Check | optional.of(1)^.?foo | NoSuchField | -
        Check | [1, 2]^[?'a'] | NoMatchingOverload | found no matching overload for '_[?_]' applied to '(list(int), string)'
        Check | {'a': 1}^[?1] | NoMatchingOverload | found no matching overload for '_[?_]' applied to '(map(string, int), int)'
        Check | {1: 'x'}^[?1u] | NoMatchingOverload | found no matching overload for '_[?_]' applied to '(map(int, string), uint)'
        Check | {1: 'x'}^[?1.0] | NoMatchingOverload | found no matching overload for '_[?_]' applied to '(map(int, string), double)'
        Check | [1, 2]^[?1u] | NoMatchingOverload | found no matching overload for '_[?_]' applied to '(list(int), uint)'
        Check | optional.of({1: 2})^[?1u] | NoMatchingOverload | found no matching overload for '_[?_]' applied to '(optional_type(map(int, int)), uint)'
        Check | optional.of({1: 2})^[1u] | NoMatchingOverload | found no matching overload for '_[_]' applied to '(optional_type(map(int, int)), uint)'
        Check | optional.of(1)^[?0] | NoMatchingOverload | found no matching overload for '_[?_]' applied to '(optional_type(int), int)'
        Check | optional.of(1)^[0] | NoMatchingOverload | found no matching overload for '_[_]' applied to '(optional_type(int), int)'
        Check | optional.of([1, 2])^['a'] | NoMatchingOverload | found no matching overload for '_[_]' applied to '(optional_type(list(int)), string)'
        Check | [1, 2]^['a'] | NoMatchingOverload | found no matching overload for '_[_]' applied to '(list(int), string)'
        Check | {'a': 1}^[1] | NoMatchingOverload | found no matching overload for '_[_]' applied to '(map(string, int), int)'
        """;

    private readonly CelEnvironment environment = ProtobufExample.Builder3()
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("o", CelType.OptionalOf(CelType.Int))
        .AddVariable("n", CelType.OptionalOf(CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("o", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(5))))
        .Add("n", CelValue.FromOptional(default));

    [Fact]
    public void OptionalSelectionOfAMap_IsTheOptionalOfTheValue() => Assert.Empty(ExtensionRows.CheckValues(environment, MapSelection, bindings));

    [Fact]
    public void SelectionOnAnOptional_LooksIntoTheValue() => Assert.Empty(ExtensionRows.CheckValues(environment, OptionalOperand, bindings));

    [Fact]
    [Trait("Requirement", "F-2")]
    public void OptionalSelectionOfAMessage_IsEmptyForAFieldThatIsNotSet() =>
        Assert.Empty(ExtensionRows.CheckValues(environment, MessageSelection, bindings));

    [Fact]
    public void OptionalIndex_IsEmptyForAnAbsentKeyOrAnIndexOutOfRange() => Assert.Empty(ExtensionRows.CheckValues(environment, OptionalIndex, bindings));

    [Fact]
    public void IndexOfAnOptional_IsOptional() => Assert.Empty(ExtensionRows.CheckValues(environment, IndexOfAnOptional, bindings));

    [Fact]
    public void IndexByADynKey_IsReadByTheKindOfTheKey() => Assert.Empty(ExtensionRows.CheckValues(environment, DynKeys, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void IndexByADynKeyThatIsNoIndexOfAList_IsAnError() => Assert.Empty(ExtensionRows.CheckErrors(environment, DynKeyErrors, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void OperatorsOnKindsThatDoNotHaveThem_AreErrorsOfTypeChecking() => Assert.Empty(ExtensionRows.CheckErrors(environment, Mistakes, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void SelectionOfADynThatHasNoFields_IsFieldSelectionNotSupportedAtTheEvaluation() =>
        Assert.Empty(ExtensionRows.CheckErrors(environment, EvaluationErrors, bindings));
}
