using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The functions and macros of the extension sets of the latest versions that no case of the conformance tests of the
// CEL specification uses (the page of the extension sets, "Sets in the test files"): math.sqrt, json.encode,
// transformMapEntry, exists_one with three arguments, isCIDR and isMask. The cases of cel-spec cover the other
// functions of the sets. Each value is that of cel-go v0.32.0, checked by a probe of the oracle with the
// flags of the conformance runner on 2026-11-09; the errors are those that the page of the sets states.
// first and last of a list belong to the optional set and have their examples with it.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-3")]
public sealed class ExtensionFunctionTests
{
    // math.sqrt takes int, uint and double and gives a double; a negative number gives NaN, not an error.
    private const string SqrtValues = """
        math.sqrt(16) => 4.0
        math.sqrt(2) => 1.4142135623730951
        math.sqrt(0) => 0.0
        math.sqrt(0.0) => 0.0
        math.sqrt(9u) => 3.0
        math.sqrt(-1) => double('NaN')
        math.sqrt(-4.0) => double('NaN')
        math.sqrt(double('inf')) => double('inf')
        math.sqrt(double('NaN')) => double('NaN')
        math.sqrt(1e300) => 1e150
        math.sqrt(dyn(25)) => 5.0
        """;

    private const string SqrtErrors = """
        Check | math.sqrt^('a') | NoMatchingOverload | found no matching overload for 'math.sqrt' applied to '(string)'
        Check | math.sqrt^(true) | NoMatchingOverload | found no matching overload for 'math.sqrt' applied to '(bool)'
        Evaluate | math.sqrt^(dyn('a')) | EvalNoMatchingOverload | no such overload: math.sqrt*
        """;

    // json.encode writes the JSON text of a value as encoding/json of Go does, with no spaces: the keys of a map in
    // ascending order, an integer beyond 2^53 - 1 as a string, a double in its shortest form, bytes in base64,
    // a time as in the JSON mapping of protobuf.
    private const string JsonValues = """
        json.encode(1) => '1'
        json.encode(-5) => '-5'
        json.encode(9007199254740991) => '9007199254740991'
        json.encode(-9007199254740991) => '-9007199254740991'
        json.encode(9007199254740992) => '"9007199254740992"'
        json.encode(-9007199254740992) => '"-9007199254740992"'
        json.encode(9223372036854775807) => '"9223372036854775807"'
        json.encode(1u) => '1'
        json.encode(9007199254740991u) => '9007199254740991'
        json.encode(9007199254740992u) => '"9007199254740992"'
        json.encode(18446744073709551615u) => '"18446744073709551615"'
        json.encode(1.5) => '1.5'
        json.encode(1.0) => '1'
        json.encode(100.0) => '100'
        json.encode(-0.0) => '-0'
        json.encode(123456789.125) => '123456789.125'
        json.encode(1e20) => '100000000000000000000'
        json.encode(1e21) => '1e+21'
        json.encode(1e100) => '1e+100'
        json.encode(0.000001) => '0.000001'
        json.encode(1e-7) => '1e-7'
        json.encode(true) => 'true'
        json.encode(false) => 'false'
        json.encode(null) => 'null'
        json.encode('abc') => '"abc"'
        json.encode('') => '""'
        json.encode('a"b\\c\n') => '"a\\"b\\\\c\\n"'
        json.encode('\u0008\u000c\r\t') => '"\\b\\f\\r\\t"'
        json.encode('\u0001') => '"\\u0001"'
        json.encode('<&>') => '"\\u003c\\u0026\\u003e"'
        json.encode('\u2028\u2029') => '"\\u2028\\u2029"'
        json.encode('é😀') => '"é😀"'
        json.encode(b'abc') => '"YWJj"'
        json.encode(b'ab') => '"YWI="'
        json.encode(b'a') => '"YQ=="'
        json.encode(b'') => '""'
        json.encode([]) => '[]'
        json.encode({}) => '{}'
        json.encode([1, 'a', null]) => '[1,"a",null]'
        json.encode(['a', ['b', {}]]) => '["a",["b",{}]]'
        json.encode({'a': null}) => '{"a":null}'
        json.encode({'b': 1, 'a': [2, {'c': false}]}) => '{"a":[2,{"c":false}],"b":1}'
        json.encode({'b': 1, 'B': 2, 'é': 3, 'z': 4, '': 5}) => '{"":5,"B":2,"b":1,"z":4,"é":3}'
        json.encode(timestamp('2009-02-13T23:31:30Z')) => '"2009-02-13T23:31:30Z"'
        json.encode(timestamp('2009-02-13T23:31:30.5Z')) => '"2009-02-13T23:31:30.5Z"'
        json.encode(duration('90m')) => '"5400s"'
        json.encode(duration('1.5s')) => '"1.5s"'
        json.encode(duration('0s')) => '"0s"'
        json.encode(dyn([1, 2])) => '[1,2]'
        """;

    // A value that has no JSON form is InvalidArgument at the call: NaN and infinity, a key of a map that is not
    // a string, a type.
    private const string JsonErrors = """
        Evaluate | json.encode^({1: 2}) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^({1: 'a', 2: 'b'}) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^({true: 1}) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^(double('NaN')) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^(double('inf')) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^(-double('inf')) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^(int) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^([1, double('NaN')]) | InvalidArgument | invalid argument to 'json.encode': *
        Evaluate | json.encode^({'a': {1: 2}}) | InvalidArgument | invalid argument to 'json.encode': *
        """;

    // transformMapEntry: the third (or fourth, after a filter) argument is a map whose pairs are added to the result;
    // a key that is already there is an error, a filter that is false adds nothing.
    private const string TransformMapEntryValues = """
        {'a': 1, 'b': 2}.transformMapEntry(k, v, {k + '!': v * 2}) => {'a!': 2, 'b!': 4}
        [10, 20].transformMapEntry(i, v, {i: v}) => {0: 10, 1: 20}
        {'a': 1, 'b': 2}.transformMapEntry(k, v, v > 1, {k: v}) => {'b': 2}
        {'a': 1, 'b': 2}.transformMapEntry(k, v, {v: k}) => {1: 'a', 2: 'b'}
        {'a': 1, 'b': 2}.transformMapEntry(k, v, v > 5, {k: v}) => {}
        [].transformMapEntry(i, v, {i: v}) => {}
        {}.transformMapEntry(k, v, {k: v}) => {}
        {'a': 1}.transformMapEntry(k, v, {}) => {}
        {'a': 1}.transformMapEntry(k, v, {k: v, k + 'x': v}) => {'a': 1, 'ax': 1}
        [1, 2, 3].transformMapEntry(i, v, v % 2 == 1, {v: i}) => {1: 0, 3: 2}
        """;

    private const string TransformMapEntryErrors = """
        Evaluate | {'a': 1, 'b': 2}.transformMapEntry^(k, v, {'z': v}) | DuplicateMapKey | duplicate map key: "z"
        Evaluate | [1, 1].transformMapEntry^(i, v, {v: i}) | DuplicateMapKey | duplicate map key: 1
        Check | [1, 2].transformMapEntry^(i, v, 5) | NoMatchingOverload | found no matching overload for 'cel.@mapInsert'*
        """;

    // exists_one and existsOne with an index or key and a value: true when the predicate is true for exactly one
    // pair; the whole range is walked, so a later error is the result even when two pairs have already matched
    // (no short circuit), and an error is not absorbed as it is by exists.
    private const string ExistsOneValues = """
        [1, 2, 3].exists_one(i, v, i == 1 && v == 2) => true
        [1, 2, 3].exists_one(i, v, v > 0) => false
        [1, 2, 3].exists_one(i, v, v > 5) => false
        [1, 2, 3].existsOne(i, v, v == 3) => true
        {'a': 1, 'b': 2}.exists_one(k, v, v > 1) => true
        {'a': 1, 'b': 2}.exists_one(k, v, v > 0) => false
        {'a': 1, 'b': 2}.exists_one(k, v, k == 'a' && v == 1) => true
        [].exists_one(i, v, true) => false
        {}.exists_one(k, v, true) => false
        [1, 2, 3].exists(i, v, v == 2 || 1 / (v - 1) > 0) => true
        """;

    private const string ExistsOneErrors = """
        Evaluate | [1, 2, 3].exists_one(i, v, 1 ^/ (v - 2) > 0) | DivisionByZero | division by zero
        Evaluate | [1, 2, 3].exists_one(i, v, v > 1 || 1 ^/ 0 > 1) | DivisionByZero | division by zero
        Evaluate | [3, 2, 1].existsOne(i, v, v == 1 || 1 ^/ (v - 3) > 0) | DivisionByZero | division by zero
        """;

    // isCIDR: true for a CIDR that parses strictly, with or without bits of the host; false for anything else,
    // never an error.
    private const string IsCidrValues = """
        isCIDR('10.0.0.0/8') => true
        isCIDR('10.0.0.1/8') => true
        isCIDR('192.168.1.5/24') => true
        isCIDR('0.0.0.0/0') => true
        isCIDR('10.1.2.3/32') => true
        isCIDR('2001:db8::/32') => true
        isCIDR('2001:DB8::1/32') => true
        isCIDR('::1/128') => true
        isCIDR('::/0') => true
        isCIDR('10.0.0.0/33') => false
        isCIDR('::1/129') => false
        isCIDR('10.0.0.0/') => false
        isCIDR('10.0.0.0') => false
        isCIDR('1.2.3.4') => false
        isCIDR('') => false
        isCIDR('/8') => false
        isCIDR('10.0.0.0/08') => false
        isCIDR('10.0.0.0/+8') => false
        isCIDR('10.0.0.0/-1') => false
        isCIDR(' 10.0.0.0/8') => false
        isCIDR('10.0.0.0/8 ') => false
        isCIDR('01.2.3.4/8') => false
        isCIDR('256.0.0.0/8') => false
        isCIDR('::ffff:192.168.0.1/96') => false
        isCIDR('fe80::1%en0/64') => false
        isCIDR('bad') => false
        isCIDR(s) => true
        """;

    // isMask: true when the address of the CIDR has no bit of the host, as the address of masked().
    private const string IsMaskValues = """
        cidr('192.168.1.0/24').isMask() => true
        cidr('192.168.1.5/24').isMask() => false
        cidr('10.0.0.0/8').isMask() => true
        cidr('10.1.2.3/8').isMask() => false
        cidr('0.0.0.0/0').isMask() => true
        cidr('255.255.255.255/32').isMask() => true
        cidr('255.255.255.254/31').isMask() => true
        cidr('255.255.0.255/16').isMask() => false
        cidr('0.0.0.255/24').isMask() => false
        cidr('::1/128').isMask() => true
        cidr('::/0').isMask() => true
        cidr('2001:db8::/32').isMask() => true
        cidr('2001:db8::1/32').isMask() => false
        cidr('10.1.2.3/8').masked().isMask() => true
        cidr('10.1.2.3/8').masked() == cidr('10.0.0.0/8') => true
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Sets(0))
        .AddExtension(CelExtensions.Encoders(1))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddExtension(CelExtensions.Network(1))
        .AddVariable<string>("s")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("s", "10.0.0.0/8");

    [Fact]
    public void MathSqrt_GivesADouble()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, SqrtValues, bindings));
        Assert.Empty(ExtensionRows.CheckErrors(environment, SqrtErrors, bindings));
    }

    [Fact]
    public void JsonEncode_WritesTheJsonOfTheValue()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, JsonValues, bindings));
    }

    [Fact]
    public void JsonEncode_RefusesAValueThatHasNoJson()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, JsonErrors, bindings));
    }

    [Fact]
    public void TransformMapEntry_AddsThePairsOfTheMapsItsStepsGive()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, TransformMapEntryValues, bindings));
        Assert.Empty(ExtensionRows.CheckErrors(environment, TransformMapEntryErrors, bindings));
    }

    [Fact]
    public void ExistsOneWithThreeArguments_WalksTheWholeRange()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, ExistsOneValues, bindings));
        Assert.Empty(ExtensionRows.CheckErrors(environment, ExistsOneErrors, bindings));
    }

    [Fact]
    public void IsCidr_IsFalseForWhatDoesNotParse()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, IsCidrValues, bindings));
    }

    [Fact]
    public void IsMask_TellsWhetherTheAddressHasNoBitsOfTheHost()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, IsMaskValues, bindings));
    }
}
