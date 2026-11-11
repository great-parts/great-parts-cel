using Great.Parts.Cel.Tests.Examples.Library;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The macros of the extension sets: math.greatest and math.least (math), sortBy (lists), cel.bind (bindings) and the
// macros of two variables (two-var-comprehensions). The parser expands a macro only when the set that has it is
// in the environment (the page of the extension sets, "Macros"); without it the call stays a call and type checking
// refuses it as an undeclared function. A macro is found by its name, its number of arguments and the call on a
// receiver, so all with three arguments is a macro of the set while all with two arguments is the standard one.
//
// Error rows are those of ExtensionRows: "stage | expression | codes | messages", with the mark "^" at the place of
// each error. The text is "invalid call of macro '<name of the function>': <the text of cel-go>"; the place is the node
// that cel-go names, an argument that is wrong or, for math.greatest() with no argument and for the receiver of
// sortBy, the receiver. The first fault found is the only error of a macro call. Every text and place is that of
// cel-go v0.32.0 (a probe of the oracle with the flags of the conformance runner, 2026-11-09).
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-4")]
[Trait("Requirement", "F-3")]
public sealed class ExtensionMacroTests
{
    private const string Prefix = "invalid call of macro ";

    // Table 3.2 of the page: no argument; a single argument that is not a number or a list literal of numbers; an
    // argument that is a constant of another type or a literal of a list, a map or a message among several, the first one
    // that is wrong.
    private const string MathErrors = $$"""
        Parse | ^math.greatest() | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() requires at least one argument
        Parse | ^math.least() | InvalidMacroCall | {{Prefix}}'least': math.least() requires at least one argument
        Parse | math.greatest(^[]) | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() invalid single argument value
        Parse | math.greatest(^'a') | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() invalid single argument value
        Parse | math.greatest(^[1, 'a']) | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() invalid single argument value
        Parse | math.greatest(^{1: 2}) | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() invalid single argument value
        Parse | math.least(^true) | InvalidMacroCall | {{Prefix}}'least': math.least() invalid single argument value
        Parse | math.greatest(1, ^[2]) | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() simple literal arguments must be numeric
        Parse | math.greatest(1, ^'a') | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() simple literal arguments must be numeric
        Parse | math.greatest(^'a', 1) | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() simple literal arguments must be numeric
        Parse | math.greatest(^null, 1) | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() simple literal arguments must be numeric
        Parse | math.greatest(1, 2, ^'a') | InvalidMacroCall | {{Prefix}}'greatest': math.greatest() simple literal arguments must be numeric
        Parse | math.least(1, ^[2], 3) | InvalidMacroCall | {{Prefix}}'least': math.least() simple literal arguments must be numeric
        Parse | math.least(^'a', 'b') | InvalidMacroCall | {{Prefix}}'least': math.least() simple literal arguments must be numeric
        Parse | math.least(^'a', 1) + math.greatest(^[], 2) | InvalidMacroCall InvalidMacroCall | {{Prefix}}'least': math.least() simple literal arguments must be numeric ;; {{Prefix}}'greatest': math.greatest() simple literal arguments must be numeric
        """;

    // Table 3.3: the receiver of sortBy, then the name, then the name that clashes with the accumulator (the hidden
    // name @result has no text of the expression).
    private const string SortByErrors = $$"""
        Parse | ^1.sortBy(x, x) | InvalidMacroCall | {{Prefix}}'sortBy': sortBy can only be applied to a list, identifier, comprehension, call or select expression
        Parse | ^'a'.sortBy(x, x) | InvalidMacroCall | {{Prefix}}'sortBy': sortBy can only be applied to a list, identifier, comprehension, call or select expression
        Parse | ^{1: 2}.sortBy(x, x) | InvalidMacroCall | {{Prefix}}'sortBy': sortBy can only be applied to a list, identifier, comprehension, call or select expression
        Parse | [3, 1, 2].sortBy(^1, 2) | InvalidMacroCall | {{Prefix}}'sortBy': argument is not an identifier
        Parse | [3, 1, 2].sortBy(a^.b, a) | InvalidMacroCall | {{Prefix}}'sortBy': argument is not an identifier
        Parse | [3, 1, 2].sortBy(^__result__, __result__) | InvalidMacroCall | {{Prefix}}'sortBy': iteration variable overwrites accumulator variable
        """;

    // Table 3.6: the first argument of cel.bind is a name.
    private const string BindErrors = $$"""
        Parse | cel.bind(^1, 2, 3) | InvalidMacroCall | {{Prefix}}'bind': cel.bind() variable names must be simple identifiers
        Parse | cel.bind(a^.b, 2, 3) | InvalidMacroCall | {{Prefix}}'bind': cel.bind() variable names must be simple identifiers
        Parse | cel.bind(^'x', 2, 3) | InvalidMacroCall | {{Prefix}}'bind': cel.bind() variable names must be simple identifiers
        """;

    // Table 3.8, checked in order: the first name, the second name, the two the same, the first against the accumulator,
    // the second against it. The text of a repeated name has the name.
    private const string TwoVariableErrors = $$"""
        Parse | [1].all(^1, v, true) | InvalidMacroCall | {{Prefix}}'all': argument must be a simple name
        Parse | [1].all(i, ^2, true) | InvalidMacroCall | {{Prefix}}'all': argument must be a simple name
        Parse | [1].all(^1, 1, true) | InvalidMacroCall | {{Prefix}}'all': argument must be a simple name
        Parse | [1].all(i, ^i, true) | InvalidMacroCall | {{Prefix}}'all': duplicate variable name: i
        Parse | [1].exists(i, ^i, true) | InvalidMacroCall | {{Prefix}}'exists': duplicate variable name: i
        Parse | [1].exists_one(i, ^i, true) | InvalidMacroCall | {{Prefix}}'exists_one': duplicate variable name: i
        Parse | [1].existsOne(k, ^k, true) | InvalidMacroCall | {{Prefix}}'existsOne': duplicate variable name: k
        Parse | [1].transformMap(i, ^i, v) | InvalidMacroCall | {{Prefix}}'transformMap': duplicate variable name: i
        Parse | [1].all(^__result__, v, true) | InvalidMacroCall | {{Prefix}}'all': iteration variable overwrites accumulator variable
        Parse | [1].all(i, ^__result__, true) | InvalidMacroCall | {{Prefix}}'all': iteration variable overwrites accumulator variable
        Parse | [1].transformList(^1, v, v) | InvalidMacroCall | {{Prefix}}'transformList': argument must be a simple name
        Parse | [1].transformList(^__result__, v, v) | InvalidMacroCall | {{Prefix}}'transformList': iteration variable overwrites accumulator variable
        Parse | [1].transformMap(i, ^__result__, v) | InvalidMacroCall | {{Prefix}}'transformMap': iteration variable overwrites accumulator variable
        Parse | [1].transformMapEntry(a^.b, v, {v: v}) | InvalidMacroCall | {{Prefix}}'transformMapEntry': argument must be a simple name
        Parse | [1].transformMapEntry(i, ^i, {i: i}, true) | InvalidMacroCall | {{Prefix}}'transformMapEntry': duplicate variable name: i
        Parse | [1].transformList(i, ^i, true, v) | InvalidMacroCall | {{Prefix}}'transformList': duplicate variable name: i
        Parse | [1].all(^1, 2, true) || [1].all(i, ^i, true) | InvalidMacroCall InvalidMacroCall | {{Prefix}}'all': argument must be a simple name ;; {{Prefix}}'all': duplicate variable name: i
        """;

    // The macros that are right give their value; the range is a list or a map, and a macro of three arguments with a
    // map has its key and its value.
    private const string Values = """
        math.greatest(dyn(5.4), 2) => 5.4
        math.greatest([dyn(5.4), dyn(10), dyn(3u), dyn(-5.0), dyn(3.5)]) => 10
        math.greatest(5.4, 10, 3u, -5.0, 3.5) => 10
        math.greatest(5) => 5
        math.greatest(5u) => 5u
        math.greatest(1, 2.5) => 2.5
        math.least(5.5) => 5.5
        math.least([1, 2, 3]) => 1
        math.least(3, 2, 1) => 1
        cel.bind(x, 1, x + 1) => 2
        cel.bind(a, 'hello', cel.bind(b, 'world', a + b + b + a)) => 'helloworldworldhello'
        cel.bind(x, [1, 2], x.exists(e, e in x)) => true
        [3, 1, 2].sortBy(x, x) => [1, 2, 3]
        [3, 1, 2].sortBy(x, -x) => [3, 2, 1]
        ['bb', 'a', 'ccc'].sortBy(e, size(e)) => ['a', 'bb', 'ccc']
        [].sortBy(e, e) => []
        [1, 2, 3].all(i, v, i < v) => true
        [1, 2, 3].exists(i, v, i == v) => false
        {'a': 1, 'b': 2}.all(k, v, v > 0) => true
        [1, 2, 3].transformList(i, v, i + v) => [1, 3, 5]
        [1, 2, 3].transformList(i, v, v > 1, v * 10) => [20, 30]
        {'a': 1, 'b': 2}.transformList(k, v, k + string(v)) => ['a1', 'b2']
        {'a': 1, 'b': 2}.transformMap(k, v, v * 2) => {'a': 2, 'b': 4}
        [10, 20].transformMap(i, v, v + 1) => {0: 11, 1: 21}
        [10, 20].transformMap(i, v, v > 10, v + 1) => {1: 21}
        [1, 2].all(x, x > 0) => true
        [1, 2, 3].exists_one(x, x > 2) => true
        [1, 2].map(x, x * 2) => [2, 4]
        """;

    // Without the set the call is not expanded: it is a call of a function that is not declared, and the names that the
    // macro would have bound are free names. Every undeclared name is an error, in the order of the text, at the name.
    // The function itself is not named: a node with an error gives no further error and a call with such a part is not
    // looked up (the page of type checking, "Overload candidates" and "The type error and the list of errors"), so a
    // receiver that is a name, such as `math` or `cel`, or an argument that is a free name hides the function; the
    // count of errors is therefore not that of cel-go, which names the function too, and the checked part is the stage,
    // the code and the place of each error.
    private const string WithoutTheSet = """
        Check | ^math.greatest(1, 2) | UndeclaredReference | undeclared reference to 'math' (in container '')
        Check | ^cel.bind(^x, 1, ^x) | UndeclaredReference UndeclaredReference UndeclaredReference | undeclared reference to 'cel' (in container '') ;; undeclared reference to 'x' (in container '') ;; undeclared reference to 'x' (in container '')
        Check | [3, 1].sortBy(^x, ^x) | UndeclaredReference UndeclaredReference | undeclared reference to 'x' (in container '') ;; undeclared reference to 'x' (in container '')
        Check | [1].all(^i, ^v, true) | UndeclaredReference UndeclaredReference | undeclared reference to 'i' (in container '') ;; undeclared reference to 'v' (in container '')
        Check | [1].transformList(^i, ^v, ^v) | UndeclaredReference UndeclaredReference UndeclaredReference | undeclared reference to 'i' (in container '') ;; undeclared reference to 'v' (in container '') ;; undeclared reference to 'v' (in container '')
        """;

    // A macro of a set needs its own set: strings brings no macro, so math.greatest stays a call there.
    private const string OtherSets = """
        Check | ^math.greatest(1, 2) | UndeclaredReference | -
        Check | [1].all(^i, ^v, true) | UndeclaredReference UndeclaredReference | -
        Check | ^cel.bind(^x, 1, ^x) | UndeclaredReference UndeclaredReference UndeclaredReference | -
        """;

    private readonly CelEnvironment all = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Sets(0))
        .AddExtension(CelExtensions.Encoders(1))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddExtension(CelExtensions.Network(1))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment none = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment withoutMacros = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Sets(0))
        .AddExtension(CelExtensions.Encoders(1))
        .AddExtension(CelExtensions.Network(1))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void MathGreatestAndLeast_RefuseArgumentsThatAreNotNumbers()
    {
        Assert.Empty(ExtensionRows.CheckErrors(all, MathErrors));
    }

    [Fact]
    public void SortBy_RefusesAReceiverOfAnotherKindAndABadName()
    {
        Assert.Empty(ExtensionRows.CheckErrors(all, SortByErrors));
    }

    [Fact]
    public void CelBind_RefusesANameThatIsNotAnIdentifier()
    {
        Assert.Empty(ExtensionRows.CheckErrors(all, BindErrors));
    }

    [Fact]
    public void MacrosOfTwoVariables_RefuseBadNames()
    {
        Assert.Empty(ExtensionRows.CheckErrors(all, TwoVariableErrors));
    }

    [Fact]
    public void MacroThatIsRight_GivesItsValue()
    {
        Assert.Empty(ExtensionRows.CheckValues(all, Values));
    }

    [Fact]
    public void WithoutTheSet_TheMacroIsNotExpanded()
    {
        Assert.Empty(ExtensionRows.CheckErrors(none, WithoutTheSet));
    }

    [Fact]
    public void WithOtherSets_TheMacroIsNotExpanded()
    {
        Assert.Empty(ExtensionRows.CheckErrors(withoutMacros, OtherSets));
    }
}
