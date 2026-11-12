using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The macros of the set optional, optMap and optFlatMap (the page of the extension sets, "optional", the table of the
// macros): a call on a receiver with two arguments, a name and an expression. The call is x.hasValue() ? <body with the
// name bound to x.value()> : optional.none(), where optMap wraps the body in optional.of and optFlatMap takes the body as
// the optional that it is; a receiver that is not an identifier is evaluated once. The name hides a variable of the same
// name in the body only, and the body is not evaluated when the receiver is empty. optFlatMap flattens one level. An error of
// expansion is InvalidMacroCall (1006) at the argument that is not a simple identifier, with the text "invalid call of macro
// '<macro>': <macro>() variable name must be a simple identifier" (the text of cel-go, as for the other macros of the
// sets); a call with another number of arguments is not a macro, as for the macros of the other sets. Without the set the
// call stays a call: the names that the macro would have bound are free names, and the function itself is not named (the
// rule of ExtensionMacroTests). The set has one version in this release, 2, so optFlatMap, which cel-go has from version 1,
// is always there; the other versions are UnknownExtensionVersion (OptionalExtensionBuildTests). The values are those of
// cel-go v0.32.0 (a probe of the oracle with the flags of the conformance runner and the set optional, 2026-11-11); the
// cost of the macros is in OptionalCostTableTests. Rows are those of ExtensionRows. Variables: o = optional.of(5) and
// n = optional.none() of the type optional_type(int), d of the type dyn.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-3")]
[Trait("Requirement", "F-4")]
public sealed class OptionalMacroTests
{
    private const string Prefix = "invalid call of macro ";

    private const string OptMapValues = """
        optional.of(1).optMap(x, x + 1) => optional.of(2)
        optional.none().optMap(x, x + 1) => optional.none()
        optional.of(1).optMap(x, optional.of(x)).value().value() => 1
        optional.of(1).optMap(x, optional.none()).value().hasValue() => false
        optional.of(2).optMap(x, x * x).value() => 4
        o.optMap(x, x + 1) => optional.of(6)
        n.optMap(x, x + 1) => optional.none()
        [1, 2].first().optMap(v, v * 10) => optional.of(10)
        [].first().optMap(v, v * 10) => optional.none()
        {'a': 1}.?a.optMap(v, v + 1) => optional.of(2)
        {'a': 1}.?b.optMap(v, v + 1) => optional.none()
        optional.of(1).optMap(x, x).optMap(x, x + 1) => optional.of(2)
        optional.of(optional.of(1)).optMap(x, x) => optional.of(optional.of(1))
        optional.of(1).optMap(x, [x, x]) => optional.of([1, 1])
        optional.of('abc').optMap(s, s.size()) => optional.of(3)
        ['a'].first().optMap(s, s + 'b') => optional.of('ab')
        (true ? optional.of(1) : optional.none()).optMap(x, x + 1) => optional.of(2)
        (false ? optional.of(1) : optional.none()).optMap(x, x + 1) => optional.none()
        optional.none().optMap(x, 1 / 0) => optional.none()
        optional.none().optMap(x, x + 1).orValue(7) => 7
        optional.of(1).optMap(x, x).orValue(7) => 1
        """;

    // The name of the macro hides a variable of the same name inside the body only; an inner macro hides the name of the outer one;
    // the body sees the other names of the outer macro.
    private const string Scope = """
        optional.of(1).optMap(o, o + 1) => optional.of(2)
        optional.of(1).optMap(d, d) => optional.of(1)
        optional.of(1).optMap(x, optional.of(2).optMap(x, x)).value().value() => 2
        optional.of(1).optMap(x, optional.of(2).optMap(y, y + x)).value().value() => 3
        optional.of(1).optFlatMap(x, optional.of(x).optMap(y, y + x)) => optional.of(2)
        [1, 2, 3].filter(x, optional.of(x).optMap(y, y > 1).value()) => [2, 3]
        [1, 2].map(x, optional.of(x).optMap(y, y * 3).value()) => [3, 6]
        [o, n].map(x, x.optMap(y, y + 1).orValue(0)) => [6, 0]
        """;

    private const string OptFlatMapValues = """
        {'a': {'b': 1}}.?a.optFlatMap(m, m.?b) => optional.of(1)
        {}.?a.optFlatMap(m, m.?b) => optional.none()
        {'a': {}}.?a.optFlatMap(m, m.?b) => optional.none()
        optional.of(1).optFlatMap(x, optional.of(x + 1)) => optional.of(2)
        optional.none().optFlatMap(x, optional.of(x + 1)) => optional.none()
        optional.of(1).optFlatMap(x, optional.none()).hasValue() => false
        optional.none().optFlatMap(x, optional.of(x / 0)) => optional.none()
        optional.of(1).optFlatMap(x, optional.of(optional.of(x))).value().hasValue() => true
        optional.of(2).optFlatMap(x, x > 1 ? optional.of(x) : optional.none()) => optional.of(2)
        optional.of(1).optFlatMap(x, x > 1 ? optional.of(x) : optional.none()) => optional.none()
        [1].first().optFlatMap(v, optional.ofNonZeroValue(v - 1)) => optional.none()
        [1, 2].last().optFlatMap(v, [v][?0]) => optional.of(2)
        optional.of([1]).optFlatMap(l, l.first()) => optional.of(1)
        o.optFlatMap(x, optional.of(x * 2)) => optional.of(10)
        n.optFlatMap(x, optional.of(x * 2)) => optional.none()
        """;

    // The error of the body is the error of the call: the place is the node that has it.
    private const string EvaluationErrors = """
        Evaluate | optional.of(1).optMap(x, x ^/ 0) | DivisionByZero | division by zero
        Evaluate | optional.of(1 ^/ 0).optMap(x, 2) | DivisionByZero | division by zero
        Evaluate | optional.of(1).optFlatMap(x, optional.of(x ^/ 0)) | DivisionByZero | division by zero
        Evaluate | optional.of(1 ^/ 0).optFlatMap(x, optional.of(x)) | DivisionByZero | division by zero
        Evaluate | optional.of(1).optMap(x, optional.none().value^()) | OptionalHasNoValue | optional value is empty
        """;

    // The first argument must be a simple identifier: a literal, a selection or a call is the error, at that argument,
    // the "." for a selection; the second argument is any expression, and the text of the other errors is the usual.
    private const string ExpansionErrors = $$"""
        Parse | optional.of(1).optMap(^1, 2) | InvalidMacroCall | {{Prefix}}'optMap': optMap() variable name must be a simple identifier
        Parse | optional.of(1).optMap(a^.b, 2) | InvalidMacroCall | {{Prefix}}'optMap': optMap() variable name must be a simple identifier
        Parse | optional.of(1).optMap(^'x', 2) | InvalidMacroCall | {{Prefix}}'optMap': optMap() variable name must be a simple identifier
        Parse | optional.of(1).optMap(x^.y, 2) | InvalidMacroCall | {{Prefix}}'optMap': optMap() variable name must be a simple identifier
        Parse | o.optFlatMap(^1, 2) | InvalidMacroCall | {{Prefix}}'optFlatMap': optFlatMap() variable name must be a simple identifier
        Parse | o.optFlatMap(a^.b, 2) | InvalidMacroCall | {{Prefix}}'optFlatMap': optFlatMap() variable name must be a simple identifier
        Parse | o.optFlatMap(^'x', 2) | InvalidMacroCall | {{Prefix}}'optFlatMap': optFlatMap() variable name must be a simple identifier
        Parse | o.optMap(a^.b, 2) + n.optFlatMap(^1, 2) | InvalidMacroCall InvalidMacroCall | {{Prefix}}'optMap': optMap() variable name must be a simple identifier ;; {{Prefix}}'optFlatMap': optFlatMap() variable name must be a simple identifier
        """;

    // A call with another number of arguments is no macro: the receiver is looked up as a call of a function that the
    // set does not declare, and the names that the call mentions are free names.
    private const string OtherArity = """
        Check | o.optMap(^x) | UndeclaredReference | undeclared reference to 'x' (in container '')
        Check | o.optMap(^x, ^x, ^x) | UndeclaredReference UndeclaredReference UndeclaredReference | undeclared reference to 'x' (in container '') ;; undeclared reference to 'x' (in container '') ;; undeclared reference to 'x' (in container '')
        Check | o.optFlatMap^(1) | UndeclaredReference | undeclared reference to 'optFlatMap' (in container '')
        Check | o.optMap^() | UndeclaredReference | undeclared reference to 'optMap' (in container '')
        """;

    // Without the set, or with other sets, the call is an undeclared function and the free names are errors, in the order
    // of the text, at the name; for a call whose arguments have no free name, the function is the error, at the "(".
    private const string WithoutTheSet = """
        Check | d.optMap(^x, ^x) | UndeclaredReference UndeclaredReference | undeclared reference to 'x' (in container '') ;; undeclared reference to 'x' (in container '')
        Check | d.optFlatMap(^x, ^x) | UndeclaredReference UndeclaredReference | undeclared reference to 'x' (in container '') ;; undeclared reference to 'x' (in container '')
        Check | d.optMap^(1, 2) | UndeclaredReference | undeclared reference to 'optMap' (in container '')
        Check | d.optFlatMap^(1, 2) | UndeclaredReference | undeclared reference to 'optFlatMap' (in container '')
        Check | d.optMap(^a.b, 2) | UndeclaredReference | undeclared reference to 'a' (in container '')
        """;

    private readonly CelEnvironment enabled = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("o", CelType.OptionalOf(CelType.Int))
        .AddVariable("n", CelType.OptionalOf(CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment none = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment otherSets = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("o", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(5))))
        .Add("n", CelValue.FromOptional(default))
        .Add("d", CelValue.FromInt64(7));

    [Fact]
    public void OptMap_WrapsTheBodyInAnOptional() => Assert.Empty(ExtensionRows.CheckValues(enabled, OptMapValues, bindings));

    [Fact]
    public void NameOfTheMacro_IsBoundInTheBodyOnly() => Assert.Empty(ExtensionRows.CheckValues(enabled, Scope, bindings));

    [Fact]
    public void OptFlatMap_TakesTheBodyAsTheOptional() => Assert.Empty(ExtensionRows.CheckValues(enabled, OptFlatMapValues, bindings));

    [Fact]
    public void ErrorOfTheBodyOrTheReceiver_IsTheErrorOfTheCall() => Assert.Empty(ExtensionRows.CheckErrors(enabled, EvaluationErrors, bindings));

    [Fact]
    public void NameThatIsNotASimpleIdentifier_IsInvalidMacroCallAtTheArgument() => Assert.Empty(ExtensionRows.CheckErrors(enabled, ExpansionErrors, bindings));

    [Fact]
    public void CallWithAnotherNumberOfArguments_IsNoMacro() => Assert.Empty(ExtensionRows.CheckErrors(enabled, OtherArity, bindings));

    [Fact]
    public void WithoutTheSet_TheCallIsNotExpanded() => Assert.Empty(ExtensionRows.CheckErrors(none, WithoutTheSet, bindings));

    [Fact]
    public void WithOtherSets_TheCallIsNotExpanded() => Assert.Empty(ExtensionRows.CheckErrors(otherSets, WithoutTheSet, bindings));
}
