using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// has() with the set optional (the page of the extension sets, "optional"; the register of known differences with cel-go, the
// entry has(optional_type) we-value). The argument of has() is the selection e.f; the optional selection e.?f is a call of the
// operator _?._ and not a selection, so has(e.?f) is InvalidMacroCall at the "(" of has, "argument must be a field
// selection", as for any argument that is not a selection (ParseErrorCodeTests). A selection whose operand is an optional
// looks into the value: has(o.f) is true when the optional has a value and the value has the field (a key of a map, a set
// field of a message by its presence rules), and false when the optional is empty or the value has not the field; so has(e.?f.g) is the
// presence of g in the value of the optional e.?f. A selection of a field on an optional whose type has no fields is an error of type
// checking (NoSuchField), as it is for the value itself, and when only the value says so (the optional is under dyn) it is
// FieldSelectionNotSupported (3025) at the evaluation. The entry of the register is has({'foo': optional.none()}.foo.bar),
// whose operand {'foo': optional.none()}.foo is an optional with no value that the map holds: here false, as the
// case optionals/optionals/map_optional_select_has of cel-spec expects, and in cel-go the error "no such key: bar" (the
// probe of the oracle with the flags of the conformance runner and the set optional, 2026-11-11, confirms it). The signature
// of the entry is has(optional_type) we-value, the class cel-go-bug. The values of the other rows are those of cel-go
// v0.32.0 (the same probe). Rows are those of ExtensionRows. Variables: o = optional.of(5) and n = optional.none(), of the
// type optional_type(int).
[Trait("Requirement", "R-6")]
[Trait("Requirement", "F-1")]
public sealed class OptionalPresenceTests
{
    // The row of the table of value rows (ExtensionRows) that is the outcome of the entry here.
    private const string EntryRow = "has({'foo': optional.none()}.foo.bar) => false";

    private const string Presence = """
        has({'foo': optional.none()}.foo) => true
        has({?'foo': optional.none()}.foo) => false
        has({}.?x.y) => false
        has({'x': {'y': 'z'}}.?x.y) => true
        has({'x': {'y': 'z'}}.?x.w) => false
        has(optional.of({'c': {'entry': 'hello world'}}).c) => true
        has(optional.of({'c': {'entry': 'hello world'}}).c.missing) => false
        has(optional.of({'c': {'entry': 'hello world'}}).c.entry) => true
        has(optional.none().c) => false
        has(optional.of({'a': 1}).a) => true
        has(optional.of({'a': 1}).b) => false
        has(optional.of(TestAllTypes{single_int32: 3}).single_int32) => true
        has(optional.of(TestAllTypes{}).single_int32) => false
        has(TestAllTypes{}.?single_nested_message.bb) => false
        has(TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 4}}.?single_nested_message.bb) => true
        has(TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 0}}.?single_nested_message.bb) => false
        has({'x': {'y': 'z'}}[?'x'].y) => true
        has({'x': {'y': 'z'}}[?'q'].y) => false
        has(dyn({'x': {'y': 'z'}}).?x.y) => true
        has(dyn(optional.of({'c': 1})).c) => true
        has(dyn(optional.of({'c': 1})).d) => false
        """;

    // The argument of has() that is the optional selection itself, or any other call, is not a selection: InvalidMacroCall
    // at the "(" of has, with the text of the table of the codes; the macro stays an error whatever is inside.
    private const string NotASelection = """
        Parse | has^({'x': {'y': 'z'}}.?x) | InvalidMacroCall | invalid call of macro 'has': argument must be a field selection
        Parse | has^(o.value()) | InvalidMacroCall | invalid call of macro 'has': argument must be a field selection
        Parse | has^({'x': 1}[?'x']) | InvalidMacroCall | invalid call of macro 'has': argument must be a field selection
        Parse | has^(TestAllTypes{}.?single_int32) | InvalidMacroCall | invalid call of macro 'has': argument must be a field selection
        Parse | has^(optional.of({'a': 1}).?a) | InvalidMacroCall | invalid call of macro 'has': argument must be a field selection
        """;

    // The value of the optional has no fields, so the selection, and with it has(), has no meaning: NoSuchField at the "(" of
    // has(), as for the value itself (ProtobufPresenceTests).
    private const string NoFields = """
        Check | has^(o.c) | NoSuchField | -
        Check | has^(optional.of(1).c) | NoSuchField | -
        Check | has^(optional.of([1]).c) | NoSuchField | -
        """;

    // An operand that is an optional by its value and holds a value that has no fields is an error of the evaluation that no
    // check could see: FieldSelectionNotSupported at the "(" of has().
    private const string NoFieldsAtEvaluation = """
        Evaluate | has^(dyn(optional.of(1)).c) | FieldSelectionNotSupported | value of type 'int' does not support field selection of 'c'
        Evaluate | has^(dyn(optional.of('a')).c) | FieldSelectionNotSupported | value of type 'string' does not support field selection of 'c'
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

    // The entry of the register has its outcome, the entry that the differential run takes the answer of cel-go for.
    [Fact]
    public void EntryOfTheRegister_IsTheOutcomeOfTheLibrary() =>
        Assert.Empty(ExtensionRows.CheckValues(environment, EntryRow, bindings));

    // The entry is about an empty optional that a map holds; has() of a selection on an empty optional that no map holds
    // (has(optional.none().c)) is false in cel-go too, so the signature of the entry is not broader than its case.
    [Fact]
    public void Has_OnASelectionOfAnOptional_LooksIntoTheValue() => Assert.Empty(ExtensionRows.CheckValues(environment, Presence, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void Has_OfTheOptionalSelectionItself_IsInvalidMacroCall() => Assert.Empty(ExtensionRows.CheckErrors(environment, NotASelection, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void Has_OnAnOptionalWhoseValueHasNoFields_IsAnErrorOfTypeChecking() => Assert.Empty(ExtensionRows.CheckErrors(environment, NoFields, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void Has_OnADynOptionalWhoseValueHasNoFields_IsAnErrorOfTheEvaluation() =>
        Assert.Empty(ExtensionRows.CheckErrors(environment, NoFieldsAtEvaluation, bindings));
}
