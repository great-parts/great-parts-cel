using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The functions of the set optional, version 2 (the page of the extension sets, "optional"): the eleven overloads of
// the table that are functions, optional.of, optional.ofNonZeroValue, optional.none, value, hasValue, or, orValue,
// first, last, optional.unwrap and unwrapOpt. The seven overloads of the operators _?._, _[?_] and _[_] have their
// examples with the syntax of the set (OptionalAccessTests). The semantics is that the page states: or and orValue
// evaluate their right part only when the left part is optional.none(); optional.ofNonZeroValue gives optional.none()
// for the zero values; value() of optional.none() is OptionalHasNoValue (3016) at the call; optional.none() and
// optional.of(null) are different values. The cases of the conformance tests of the specification (optionals.textproto)
// cover most of these functions; the examples add first, last, optional.unwrap and unwrapOpt, which no case uses, and the
// errors. The values are those of cel-go v0.32.0 (a probe of the oracle with the flags of the conformance runner and the
// set optional, 2026-11-11); a row that the probe did not run follows the rule of the page by its neighbors that it
// did. Rows are those of ExtensionRows: "expression => expected" and "stage | expression | codes | messages", "^" marks
// the place of an error. Variables: o = optional.of(5) and n = optional.none(), both of the type optional_type(int).
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-3")]
public sealed class OptionalFunctionTests
{
    // optional.of keeps its argument, null and optional.none() among them: the result has a value in every case.
    private const string Of = """
        optional.of(1) => optional.of(1)
        optional.of('a').value() => 'a'
        optional.of(null).hasValue() => true
        optional.of(null).value() => null
        optional.of(optional.none()).hasValue() => true
        optional.of(optional.none()).value().hasValue() => false
        optional.of(optional.of(1)).value().value() => 1
        optional.of([1, 2]).value() => [1, 2]
        optional.of({'a': 1}).value() => {'a': 1}
        optional.of(o).value().value() => 5
        """;

    // optional.none() is not optional.of(null): the first has no value, the second has the value null. Two optionals are
    // equal when both are empty or both have equal values; the values are compared as CEL compares them, under dyn too.
    private const string Equality = """
        optional.none() == optional.none() => true
        optional.none() == optional.of(null) => false
        optional.of(null) == optional.of(null) => true
        optional.of(null) != optional.none() => true
        optional.none() == optional.of(1) => false
        optional.of(1) == optional.none() => false
        optional.of(1) == optional.of(1) => true
        optional.of(1) != optional.of(1) => false
        optional.of([1]) == optional.of([1]) => true
        optional.of({'a': 1}) == optional.of({'a': 1}) => true
        optional.of(optional.of(1)) == optional.of(optional.of(1)) => true
        optional.of(optional.none()) == optional.of(optional.none()) => true
        optional.of(optional.none()) == optional.none() => false
        o == n => false
        o == optional.of(5) => true
        dyn(optional.of(1)) == dyn(optional.of(1)) => true
        dyn(optional.of(1)) == dyn(optional.of(1u)) => true
        dyn(optional.of(1)) == dyn(optional.none()) => false
        dyn(optional.of(1)) == 1 => false
        dyn(optional.of(1)) != dyn(1) => true
        optional.of(1) == dyn(optional.of(1)) => true
        [optional.of(1), optional.none()] == [optional.of(1), optional.none()] => true
        {'a': optional.of(1)} == {'a': optional.of(1)} => true
        optional.of(1) in [optional.of(1)] => true
        optional.of(null) == null => false
        """;

    // The zero values are null, 0, 0u, 0.0 and -0.0, false, the empty string, the empty bytes, the empty list, the empty map,
    // the duration of 0 and the timestamp of 0001-01-01T00:00:00Z (the page of the set names null, 0, the empty string, the
    // empty list and the empty map; the other zeros are the same kind of value, the zero of each kind that cel-go calls
    // zero). The epoch is no zero timestamp, a NaN is no zero double, and an optional or a type value is no zero. An object with
    // no field set is a zero value, with one set it is not (OptionalAccessTests, which has the messages).
    private const string NonZero = """
        optional.ofNonZeroValue(null).hasValue() => false
        optional.ofNonZeroValue(0).hasValue() => false
        optional.ofNonZeroValue(0u).hasValue() => false
        optional.ofNonZeroValue(0.0).hasValue() => false
        optional.ofNonZeroValue(false).hasValue() => false
        optional.ofNonZeroValue('').hasValue() => false
        optional.ofNonZeroValue(b'').hasValue() => false
        optional.ofNonZeroValue([]).hasValue() => false
        optional.ofNonZeroValue({}).hasValue() => false
        optional.ofNonZeroValue(-0.0).hasValue() => false
        optional.ofNonZeroValue(duration('0s')).hasValue() => false
        optional.ofNonZeroValue(timestamp('0001-01-01T00:00:00Z')).hasValue() => false
        optional.ofNonZeroValue(0.0 / 0.0).hasValue() => true
        optional.ofNonZeroValue(duration('1s')).hasValue() => true
        optional.ofNonZeroValue(timestamp(0)).hasValue() => true
        optional.ofNonZeroValue(int).hasValue() => true
        optional.ofNonZeroValue(optional.none()).hasValue() => true
        optional.ofNonZeroValue(optional.of(0)).hasValue() => true
        optional.ofNonZeroValue(1).hasValue() => true
        optional.ofNonZeroValue(-1).hasValue() => true
        optional.ofNonZeroValue(1u).hasValue() => true
        optional.ofNonZeroValue(0.5).hasValue() => true
        optional.ofNonZeroValue(true).hasValue() => true
        optional.ofNonZeroValue('a').hasValue() => true
        optional.ofNonZeroValue(b'a').hasValue() => true
        optional.ofNonZeroValue([0]).hasValue() => true
        optional.ofNonZeroValue([[]]).hasValue() => true
        optional.ofNonZeroValue({'a': 0}).hasValue() => true
        optional.ofNonZeroValue(1) => optional.of(1)
        optional.ofNonZeroValue('a') => optional.of('a')
        optional.ofNonZeroValue([0]) => optional.of([0])
        optional.ofNonZeroValue({'a': 0}) => optional.of({'a': 0})
        optional.ofNonZeroValue(0) => optional.none()
        optional.ofNonZeroValue('') => optional.none()
        optional.ofNonZeroValue(dyn(0)).hasValue() => false
        optional.ofNonZeroValue(dyn('x')).hasValue() => true
        """;

    private const string ValueAndHasValue = """
        o.value() => 5
        optional.of(5).value() => 5
        optional.of('a').value() + 'b' => 'ab'
        optional.of(null).value() => null
        optional.of(1).hasValue() => true
        optional.none().hasValue() => false
        o.hasValue() => true
        n.hasValue() => false
        optional.of(1).hasValue() && !optional.none().hasValue() => true
        """;

    // or gives the left optional when it has a value and the right one otherwise; the right one is not evaluated when
    // the left one has a value, so its error does not appear (the rows of ShortCircuitErrors).
    private const string Or = """
        optional.none().or(optional.of(2)) => optional.of(2)
        optional.of(1).or(optional.of(2)) => optional.of(1)
        optional.none().or(optional.none()) => optional.none()
        n.or(o) => optional.of(5)
        o.or(n) => optional.of(5)
        n.or(n) => optional.none()
        optional.of(1).or(optional.of(2)).value() => 1
        optional.none().or(optional.of(2)).value() => 2
        optional.none().or(optional.none()).orValue(42) => 42
        optional.of(1).or(optional.of(1 / 0)) => optional.of(1)
        optional.of(1).or(dyn(1)) => optional.of(1)
        optional.of(dyn(null)).or(optional.of(1)).value() => null
        optional.none().or(optional.of(dyn(null))).hasValue() => true
        n.or(n).or(o) => optional.of(5)
        """;

    private const string OrValue = """
        optional.none().orValue(2) => 2
        optional.of(1).orValue(2) => 1
        optional.of(1).orValue(1 / 0) => 1
        o.orValue(0) => 5
        n.orValue(0) => 0
        optional.none().orValue('x') => 'x'
        optional.of(optional.none()).value().orValue(3) => 3
        optional.of(dyn(null)).orValue(1) => null
        """;

    // The part that is evaluated gives its error as the value of the call: the left part always, the right part when
    // the left one is empty. The place is the node that has the error. An empty left part and a right part that is no
    // optional, which only dyn or null can be, is EvalNoMatchingOverload with the types of both at the call (cel-go gives
    // the right part; the registry has the signatures, class cel-go-bug).
    private const string ShortCircuitErrors = """
        Evaluate | optional.none().or(optional.of(1 ^/ 0)) | DivisionByZero | division by zero
        Evaluate | optional.none().orValue(1 ^/ 0) | DivisionByZero | division by zero
        Evaluate | optional.of(1 ^/ 0).or(optional.of(2)) | DivisionByZero | division by zero
        Evaluate | optional.of(1 ^/ 0).orValue(2) | DivisionByZero | division by zero
        Evaluate | n.or(optional.of(1 ^/ 0)) | DivisionByZero | division by zero
        Evaluate | dyn(1).or^(optional.of(2)) | EvalNoMatchingOverload | no such overload: or(int)
        Evaluate | dyn(1).orValue^(2) | EvalNoMatchingOverload | no such overload: orValue(int)
        Evaluate | optional.none().or^(dyn(1)) | EvalNoMatchingOverload | no such overload: or(optional_type, int)
        Evaluate | optional.none().or^(null) | EvalNoMatchingOverload | no such overload: or(optional_type, null_type)
        """;

    // value() of an empty optional: OptionalHasNoValue at the call, that is at the "(" after value, with the text of the
    // table of the codes; whatever made the optional empty.
    private const string HasNoValue = """
        Evaluate | optional.none().value^() | OptionalHasNoValue | optional value is empty
        Evaluate | n.value^() | OptionalHasNoValue | optional value is empty
        Evaluate | [].first().value^() | OptionalHasNoValue | optional value is empty
        Evaluate | [].last().value^() | OptionalHasNoValue | optional value is empty
        Evaluate | {}.?a.value^() | OptionalHasNoValue | optional value is empty
        Evaluate | optional.ofNonZeroValue(0).value^() | OptionalHasNoValue | optional value is empty
        Evaluate | optional.none().or(optional.none()).value^() | OptionalHasNoValue | optional value is empty
        Evaluate | optional.none().optMap(x, x).value^() | OptionalHasNoValue | optional value is empty
        Evaluate | optional.none().value^() == 1 | OptionalHasNoValue | optional value is empty
        Evaluate | [optional.none()].unwrapOpt().first().value^() | OptionalHasNoValue | optional value is empty
        Evaluate | optional.of(optional.none()).value().value^() | OptionalHasNoValue | optional value is empty
        """;

    private const string FirstAndLast = """
        [1, 2, 3].first() => optional.of(1)
        [1, 2, 3].last() => optional.of(3)
        [7].first() => optional.of(7)
        [7].last() => optional.of(7)
        [].first() => optional.none()
        [].last() => optional.none()
        [null].first().hasValue() => true
        [[]].last().value() => []
        ['a'].first().orValue('b') => 'a'
        [].first().orValue('x') => 'x'
        [[1], [2]].first().value()[0] => 1
        [3, 1, 2].first().value() => 3
        [1, 2].last().optMap(v, v * 10) => optional.of(20)
        [optional.none()].first().hasValue() => true
        [optional.none()].first().value().hasValue() => false
        """;

    // unwrapOpt and optional.unwrap drop the empty optionals and give the values in their order; a value null stays;
    // the result is a list of the values, never of optionals.
    private const string Unwrap = """
        optional.unwrap([optional.of(1), optional.none(), optional.of(3)]) => [1, 3]
        [optional.of(1), optional.none(), optional.of(3)].unwrapOpt() => [1, 3]
        [optional.none(), optional.none()].unwrapOpt() => []
        [].unwrapOpt() => []
        optional.unwrap([]) => []
        [optional.of(null)].unwrapOpt() => [null]
        [optional.of([])].unwrapOpt() => [[]]
        optional.unwrap([o, n, o]) => [5, 5]
        [o, n].unwrapOpt().size() => 1
        optional.unwrap([optional.of('a')]) => ['a']
        [optional.of(1), optional.of(2)].unwrapOpt() == [1, 2] => true
        [1, 2, 3].map(x, optional.ofNonZeroValue(x - 1)).unwrapOpt() => [1, 2]
        [[1, 2], []].map(l, l.first()).unwrapOpt() => [1]
        [optional.of(1)].unwrapOpt().first().value() => 1
        """;

    // No overload takes the types: NoMatchingOverload at the "(" of the call (at the operator for an operator). The text is
    // that of the table of the codes for a global call; the text of a call on a receiver is not fixed by the examples.
    private const string Mistakes = """
        Check | optional.of(1).or^(2) | NoMatchingOverload | -
        Check | optional.of(1).orValue^('a') | NoMatchingOverload | -
        Check | optional.of(1).orValue^() | NoMatchingOverload | -
        Check | optional.of(1).or^() | NoMatchingOverload | -
        Check | optional.of(1).value^(2) | NoMatchingOverload | -
        Check | optional.of(1).hasValue^(1) | NoMatchingOverload | -
        Check | [1].unwrapOpt^() | NoMatchingOverload | -
        Check | [1].first().first^() | NoMatchingOverload | -
        Check | [].first^(1) | NoMatchingOverload | -
        Check | optional.unwrap^([1]) | NoMatchingOverload | found no matching overload for 'optional.unwrap' applied to '(list(int))'
        Check | optional.unwrap^(1) | NoMatchingOverload | found no matching overload for 'optional.unwrap' applied to '(int)'
        Check | optional.unwrap^() | NoMatchingOverload | found no matching overload for 'optional.unwrap' applied to '()'
        Check | optional.of^() | NoMatchingOverload | found no matching overload for 'optional.of' applied to '()'
        Check | optional.of^(1, 2) | NoMatchingOverload | found no matching overload for 'optional.of' applied to '(int, int)'
        Check | optional.none^(1) | NoMatchingOverload | found no matching overload for 'optional.none' applied to '(int)'
        Check | optional.ofNonZeroValue^() | NoMatchingOverload | found no matching overload for 'optional.ofNonZeroValue' applied to '()'
        Check | optional.of(1) ^== optional.of(1u) | NoMatchingOverload | found no matching overload for '_==_' applied to '(optional_type(int), optional_type(uint))'
        Check | optional.of(1) ^== optional.of(1.0) | NoMatchingOverload | found no matching overload for '_==_' applied to '(optional_type(int), optional_type(double))'
        Check | optional.of(1) ^== 1 | NoMatchingOverload | found no matching overload for '_==_' applied to '(optional_type(int), int)'
        Check | optional.of(1) ^+ optional.of(2) | NoMatchingOverload | found no matching overload for '_+_' applied to '(optional_type(int), optional_type(int))'
        Check | optional.of(1) ^< optional.of(2) | NoMatchingOverload | found no matching overload for '_<_' applied to '(optional_type(int), optional_type(int))'
        Check | ^!optional.of(true) | NoMatchingOverload | found no matching overload for '!_' applied to '(optional_type(bool))'
        Check | ^-optional.of(1) | NoMatchingOverload | found no matching overload for '-_' applied to '(optional_type(int))'
        Check | size^(optional.of([1])) | NoMatchingOverload | found no matching overload for 'size' applied to '(optional_type(list(int)))'
        Check | string^(optional.of(1)) | NoMatchingOverload | found no matching overload for 'string' applied to '(optional_type(int))'
        Check | int^(optional.of(1)) | NoMatchingOverload | found no matching overload for 'int' applied to '(optional_type(int))'
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("o", CelType.OptionalOf(CelType.Int))
        .AddVariable("n", CelType.OptionalOf(CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("o", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(5))))
        .Add("n", CelValue.FromOptional(default));

    [Fact]
    public void OptionalOf_KeepsItsArgument() => Assert.Empty(ExtensionRows.CheckValues(environment, Of, bindings));

    [Fact]
    [Trait("Requirement", "F-8")]
    public void NoneAndOfNull_AreDifferentValues_AndEqualityIsThatOfCel() =>
        Assert.Empty(ExtensionRows.CheckValues(environment, Equality, bindings));

    [Fact]
    public void OptionalOfNonZeroValue_IsEmptyForTheZeroValues() =>
        Assert.Empty(ExtensionRows.CheckValues(environment, NonZero, bindings));

    [Fact]
    public void ValueAndHasValue_ReadTheOptional() =>
        Assert.Empty(ExtensionRows.CheckValues(environment, ValueAndHasValue, bindings));

    [Fact]
    public void Or_GivesTheFirstOptionalThatHasAValue() => Assert.Empty(ExtensionRows.CheckValues(environment, Or, bindings));

    [Fact]
    public void OrValue_GivesTheValueOrItsArgument() => Assert.Empty(ExtensionRows.CheckValues(environment, OrValue, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void RightPartOfOrAndOrValue_IsEvaluatedOnlyWhenTheLeftPartIsEmpty() =>
        Assert.Empty(ExtensionRows.CheckErrors(environment, ShortCircuitErrors, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void ValueOfAnEmptyOptional_IsOptionalHasNoValueAtTheCall() =>
        Assert.Empty(ExtensionRows.CheckErrors(environment, HasNoValue, bindings));

    [Fact]
    public void FirstAndLast_GiveAnOptionalOfTheElement() => Assert.Empty(ExtensionRows.CheckValues(environment, FirstAndLast, bindings));

    [Fact]
    public void UnwrapOpt_AndOptionalUnwrap_DropTheEmptyOptionals() => Assert.Empty(ExtensionRows.CheckValues(environment, Unwrap, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-6")]
    public void ArgumentsThatNoOverloadTakes_AreNoMatchingOverloadAtTheCall() =>
        Assert.Empty(ExtensionRows.CheckErrors(environment, Mistakes, bindings));
}
