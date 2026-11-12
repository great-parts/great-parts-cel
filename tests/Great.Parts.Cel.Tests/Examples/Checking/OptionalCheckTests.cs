using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The static type of the expressions of the set optional (the page of type checking, "Optional types"; the page of the
// extension sets): optional_type(T) for the functions and the operators of the set, the type of the selection _?._ by the
// type of the field and not by the signature dyn (a map gives the type of its value, a message the type of the field,
// an optional the type of the optional selection of its value, dyn gives optional_type(dyn)), the type of the optional of
// an optional, and the type of a list, a map and a message with "?" elements, which is that of the values. A type parameter
// that is still free at the end is dyn, as everywhere (ResultTypeTests). The type value of the abstract type is
// type(optional_type(T)); its name optional_type is declared by the set, so it is an undeclared name without the set. The
// types are those that cel-go v0.32.0 gives (a probe of the oracle with the flags of the conformance runner and the set
// optional, 2026-11-11); the types of the cases of type_deduction.textproto that have optional are in the conformance run.
// The messages are the test messages of the suite (ProtobufExample), in the container of their package. Variables:
// o and n of the type optional_type(int), m of map(string, int), d of dyn, l of list(int).
[Trait("Requirement", "+R-10")]
[Trait("Requirement", "F-1")]
public sealed class OptionalCheckTests
{
    private const string Cases = """
        optional.of(1) => optional_type(int)
        optional.of('a') => optional_type(string)
        optional.of(null) => optional_type(null_type)
        optional.of([1]) => optional_type(list(int))
        optional.of(o) => optional_type(optional_type(int))
        optional.none() => optional_type(dyn)
        optional.ofNonZeroValue(0) => optional_type(int)
        optional.ofNonZeroValue('') => optional_type(string)
        optional.of(1).value() => int
        optional.of(null).value() => null_type
        optional.none().value() => dyn
        optional.of(1).hasValue() => bool
        optional.of(1).or(optional.of(2)) => optional_type(int)
        optional.none().or(optional.none()) => optional_type(dyn)
        optional.of(1).orValue(2) => int
        o.or(n) => optional_type(int)
        n.orValue(0) => int
        [1].first() => optional_type(int)
        [1].last() => optional_type(int)
        l.first() => optional_type(int)
        [].first() => optional_type(dyn)
        [[1]].first().value() => list(int)
        optional.unwrap([o]) => list(int)
        [o].unwrapOpt() => list(int)
        [optional.none()].unwrapOpt() => list(dyn)
        [].unwrapOpt() => list(dyn)
        {'a': 1}.?a => optional_type(int)
        m.?a => optional_type(int)
        d.?a => optional_type(dyn)
        {}.?a => optional_type(dyn)
        {'a': {'b': 2}}.?a.b => optional_type(int)
        {'a': {'b': 2}}.?a.?b => optional_type(int)
        optional.of({'a': 1}).?a => optional_type(int)
        optional.of({'a': 1}).a => optional_type(int)
        optional.none().?a => optional_type(dyn)
        TestAllTypes{}.?single_int32 => optional_type(int)
        TestAllTypes{}.?single_string => optional_type(string)
        TestAllTypes{}.?single_int32_wrapper => optional_type(wrapper(int))
        TestAllTypes{}.?repeated_string => optional_type(list(string))
        TestAllTypes{}.?single_nested_message => optional_type(cel.expr.conformance.proto3.TestAllTypes.NestedMessage)
        TestAllTypes{}.?single_nested_message.bb => optional_type(int)
        TestAllTypes{}.?map_string_string['a'] => optional_type(string)
        optional.of(TestAllTypes{}).?single_int32 => optional_type(int)
        [1, 2][?0] => optional_type(int)
        [][?0] => optional_type(dyn)
        l[?0] => optional_type(int)
        optional.of([1, 2])[?0] => optional_type(int)
        optional.of([1, 2])[0] => optional_type(int)
        optional.none()[0] => optional_type(dyn)
        {'a': 1}[?'a'] => optional_type(int)
        m[?'a'] => optional_type(int)
        optional.of({'a': 1})[?'a'] => optional_type(int)
        optional.of({'a': 1})['a'] => optional_type(int)
        dyn([1])[?0] => dyn
        dyn({'a': 1})[?'a'] => dyn
        [1, ?o] => list(int)
        [?n] => list(int)
        [?optional.none()] => list(dyn)
        {'a': 1, ?'b': o} => map(string, int)
        {?'a': n} => map(string, int)
        {?'a': optional.none()} => map(string, dyn)
        TestAllTypes{?single_int32: o} => cel.expr.conformance.proto3.TestAllTypes
        o.optMap(x, x + 1) => optional_type(int)
        o.optMap(x, 'a') => optional_type(string)
        o.optMap(x, optional.of(x)) => optional_type(optional_type(int))
        o.optFlatMap(x, optional.of(x)) => optional_type(int)
        o.optFlatMap(x, optional.none()) => optional_type(dyn)
        [1].first().optMap(x, [x]) => optional_type(list(int))
        optional.none().optMap(x, x + 1) => optional_type(int)
        type(o) => type(optional_type(int))
        type(optional.none()) => type(optional_type(dyn))
        optional_type => type(optional_type(dyn))
        type(o) == optional_type => bool
        type(optional.of(1)) == type(optional.of('a')) => bool
        dyn(o) => dyn
        o == n => bool
        o == dyn(n) => bool
        [o] == [n] => bool
        """;

    private readonly CelEnvironment environment = ProtobufExample.Builder3()
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("o", CelType.OptionalOf(CelType.Int))
        .AddVariable("n", CelType.OptionalOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    public static TheoryData<string, string> Types
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (string line in ExtensionRows.Lines(Cases))
            {
                string[] parts = line.Split(" => ");
                data.Add(parts[0], parts[1]);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Expression_HasItsStaticType(string expression, string type)
    {
        CelProgram<CelValue> program = environment.Compile<CelValue>(expression).GetProgramOrThrow();

        Assert.Equal(type, program.ResultType.ToString());
    }

    // The type of the result is a value of CelType too, not only its text: optional_type(T) is the abstract type with the one
    // parameter, so two types are equal when their parameters are, and the type of the type value is type(optional_type(T)).
    [Fact]
    public void ResultType_IsTheOptionalTypeOfCelType()
    {
        CelType type = environment.Compile<CelValue>("optional.of(1)").GetProgramOrThrow().ResultType;

        Assert.Equal(CelType.OptionalOf(CelType.Int), type);
        Assert.NotEqual(CelType.OptionalOf(CelType.UInt), type);
        Assert.Equal(CelType.OptionalOf(CelType.OptionalOf(CelType.Int)), environment.Compile<CelValue>("optional.of(o)").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.TypeOf(CelType.OptionalOf(CelType.Int)), environment.Compile<CelValue>("type(o)").GetProgramOrThrow().ResultType);
    }
}
