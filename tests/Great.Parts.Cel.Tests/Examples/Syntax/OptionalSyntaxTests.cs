using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The syntax of the set optional (the page of the extension sets, "optional", the table of the flags of the syntax): the
// five forms x.?f, x[?k], [?e], {?k: v} and T{?f: v}, which the set turns on and which are an error of parsing without it.
// With the set the forms have their meaning: an optional element, key or field is added to the list, the map or the message
// only when its optional has a value, and the selection and the index give an optional. Without the set every form is
// SyntaxNotEnabled (1007) at its lexeme, the "?" itself (not the "." of ".?" and not the "[" of "[?"), with the text
// "'<lexeme>' is not enabled: add the extension set optional", where the lexeme is '.?', '[?' or '?'; the parser goes on, so
// each form of a text is an error, in the order of the text (the syntax tree of the forms is in SyntaxTreeTests,
// the codes in ParseErrorCodeTests). The examples of the forms with the set are values of cel-go v0.32.0 (a probe of the
// oracle with the flags of the conformance runner and the set optional, 2026-11-11). A "?" element must be an optional,
// or dyn: a value of another type is TypeMismatch, "expected type 'optional_type(<what is expected>)' but found '<its
// type>'", at the element of a list and at the value of a map entry ("[?1]" gives "expected type 'optional_type(int)' but
// found 'int'", as cel-go does), and at the ":" of the field in a message literal, where the place of an entry is that of
// the entries without "?" (ProtobufLiteralCheckTests); the expected type there is optional_type(<field type>) and the found
// type is the type of the value, also when that is an optional of another kind. cel-go words the second error as
// "expected type of field 'f' is 'int' but provided type is 'string'" and puts the first one at the value, so the places
// and the text of the message-literal rows are those of the page of type checking and not of cel-go; the stage and the code
// are the same. A field that the message has not is NoSuchField at the ":". A "?" position whose value is a dyn that is no
// optional is the error of the evaluation, EvalNoMatchingOverload with the function "?" and the type of the value, at the
// node of the value. The key of "{?k: v}" is a key as in any map; a key that appears twice is the error of any map
// (DuplicateMapKey, which the examples of this part do not repeat). The parts of a literal are evaluated in the order of
// the text, and an element whose optional is empty adds nothing, but its optional has been evaluated. Rows are those of
// ExtensionRows. Variables: o = optional.of(5) and n = optional.none() of the type optional_type(int); d of the type dyn.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-3")]
[Trait("Requirement", "F-5")]
public sealed class OptionalSyntaxTests
{
    private const string Hint = " is not enabled: add the extension set optional";

    // The lists: an element with "?" is added when its optional has a value; the type of the list is that of the values.
    private const string Lists = """
        [1, ?optional.of(2), ?optional.none(), 3] => [1, 2, 3]
        [?optional.none()] => []
        [?optional.of(1)] => [1]
        [?optional.of(1), ?optional.of(2)] => [1, 2]
        [?o, ?n] => [5]
        [?n, ?n] => []
        [1, ?{}.?a] => [1]
        [1, ?{'a': 2}.?a] => [1, 2]
        [?optional.of(null)] => [null]
        [?optional.of([])] => [[]]
        [?optional.of(optional.none())].size() => 1
        [?o, ?n].size() => 1
        [1, ?o, ?optional.none()].size() == 2 => true
        [?[1, 2][?5], ?[1, 2][?1]] => [2]
        """;

    private const string Maps = """
        {'a': 1, ?'b': optional.of(2), ?'c': optional.none()} => {'a': 1, 'b': 2}
        {?'a': optional.none()} => {}
        {?'a': optional.of(1)} => {'a': 1}
        {?1: optional.of(1)} => {1: 1}
        {?'a': optional.none(), 'a': 2} => {'a': 2}
        {?'k': o}.size() => 1
        {?'k': n}.size() => 0
        {?'k': o}.k => 5
        {?'a': {'x': 1}.?x, ?'b': {'x': 1}.?y} => {'a': 1}
        {?true: optional.of('t')} => {true: 't'}
        {?'a': optional.of(null)} => {'a': null}
        """;

    private const string Messages = """
        TestAllTypes{?single_int32: optional.of(5), ?single_string: optional.none()}.single_int32 => 5
        TestAllTypes{?single_int32: optional.none()}.single_int32 => 0
        has(TestAllTypes{?single_int32: optional.none()}.single_int32) => false
        has(TestAllTypes{?single_int32: optional.of(2)}.single_int32) => true
        has(TestAllTypes{?single_int32: optional.of(0)}.single_int32) => false
        TestAllTypes{?single_int32: optional.of(dyn(5))}.single_int32 => 5
        TestAllTypes{?single_int32: optional.of(5), single_int32: 6}.single_int32 => 6
        TestAllTypes{?single_nested_message: optional.of(TestAllTypes.NestedMessage{bb: 3})}.single_nested_message.bb => 3
        TestAllTypes{?single_nested_message: optional.none()}.single_nested_message.bb => 0
        has(TestAllTypes{?single_nested_message: optional.none()}.single_nested_message) => false
        TestAllTypes{repeated_string: ['x', ?optional.of('y'), ?optional.none()]}.repeated_string => ['x', 'y']
        TestAllTypes{?repeated_string: optional.of(['z'])}.repeated_string => ['z']
        TestAllTypes{?map_string_string: {'nested': {}}[?'nested']}.map_string_string => {}
        TestAllTypes{?map_string_string: {'nested': {'hello': 'world'}}[?'nested']}.map_string_string => {'hello': 'world'}
        TestAllTypes{?single_double_wrapper: optional.ofNonZeroValue(0.0)}.single_double_wrapper => null
        has(TestAllTypes{?single_double_wrapper: optional.ofNonZeroValue(0.0)}.single_double_wrapper) => false
        has(TestAllTypes{?single_double_wrapper: optional.of(0.0)}.single_double_wrapper) => true
        TestAllTypes{?single_string: {'s': 'v'}.?s}.single_string => 'v'
        TestAllTypes{?single_string: {'s': 'v'}.?t}.single_string => ''
        """;

    // The forms keep their meaning with the set: a "?" is a part of the optional forms only where the form has one, and the
    // ternary operator is not one.
    private const string PlainFormsWithTheSet = """
        true ? 1 : 2 => 1
        [true ? 1 : 2] => [1]
        {'a': true ? 1 : 2} => {'a': 1}
        [1, 2][1] => 2
        {'a': 1}['a'] => 1
        {'a': 1}.a => 1
        [o.value()] => [5]
        o.hasValue() ? 'y' : 'n' => 'y'
        n.hasValue() ? 'y' : 'n' => 'n'
        """;

    // A "?" element that is not an optional, a key or a value of the wrong kind, and a field that the message has not.
    private const string TypeErrors = """
        Check | [?^1] | TypeMismatch | expected type 'optional_type(int)' but found 'int'
        Check | {?'a': ^1} | TypeMismatch | expected type 'optional_type(int)' but found 'int'
        Check | TestAllTypes{?single_int32^: 5} | TypeMismatch | expected type 'optional_type(int)' but found 'int'
        Check | TestAllTypes{?single_int32^: optional.of('a')} | TypeMismatch | expected type 'optional_type(int)' but found 'optional_type(string)'
        Check | TestAllTypes{?single_string^: optional.of(1)} | TypeMismatch | expected type 'optional_type(string)' but found 'optional_type(int)'
        Check | TestAllTypes{?nosuchfield^: optional.of(1)} | NoSuchField | undefined field 'nosuchfield' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | TestAllTypes{single_int32: 1, ?nosuchfield^: optional.of(1), ?single_string^: optional.of(2)} | NoSuchField TypeMismatch | undefined field 'nosuchfield' in type 'cel.expr.conformance.proto3.TestAllTypes' ;; expected type 'optional_type(string)' but found 'optional_type(int)'
        """;

    // The elements are evaluated in the order of the text and the first error ends the evaluation; the element that is not
    // added still has its optional evaluated, so its error is the error of the literal.
    private const string EvaluationErrors = """
        Evaluate | [?optional.of(1 ^/ 0)] | DivisionByZero | division by zero
        Evaluate | [1, ?optional.of(1 ^/ 0), 3] | DivisionByZero | division by zero
        Evaluate | {?'a': optional.of(1 ^/ 0)} | DivisionByZero | division by zero
        Evaluate | {?'a': optional.none(), 'b': 1 ^/ 0} | DivisionByZero | division by zero
        Evaluate | TestAllTypes{?single_int64: optional.of(1 ^/ 0)} | DivisionByZero | division by zero
        Evaluate | TestAllTypes{?single_int64: optional.none(), single_int32: 1 ^/ 0} | DivisionByZero | division by zero
        Evaluate | [?optional.of(optional.none().value^())] | OptionalHasNoValue | optional value is empty
        Evaluate | [?dyn^(1)] | EvalNoMatchingOverload | no such overload: ?(int)
        Evaluate | [1, ?dyn^('a')] | EvalNoMatchingOverload | no such overload: ?(string)
        Evaluate | {?'a': dyn^(1)} | EvalNoMatchingOverload | no such overload: ?(int)
        Evaluate | TestAllTypes{?single_int32: dyn^(1)} | EvalNoMatchingOverload | no such overload: ?(int)
        """;

    // Without the set, one error for each form; "d" is a variable of the type dyn, so its selection is the one that the
    // parser refuses and not an unknown name.
    private const string WithoutTheSet = $$"""
        Parse | {'a': 1}.^?a | SyntaxNotEnabled | '.?'{{Hint}}
        Parse | d.^?a | SyntaxNotEnabled | '.?'{{Hint}}
        Parse | d.a.^?b | SyntaxNotEnabled | '.?'{{Hint}}
        Parse | {'a': 1}[^?'a'] | SyntaxNotEnabled | '[?'{{Hint}}
        Parse | [1, 2][^?0] | SyntaxNotEnabled | '[?'{{Hint}}
        Parse | d[^?0] | SyntaxNotEnabled | '[?'{{Hint}}
        Parse | [^?1] | SyntaxNotEnabled | '?'{{Hint}}
        Parse | [1, ^?2] | SyntaxNotEnabled | '?'{{Hint}}
        Parse | [1, 2, ^?3, 4] | SyntaxNotEnabled | '?'{{Hint}}
        Parse | {^?'a': 1} | SyntaxNotEnabled | '?'{{Hint}}
        Parse | {'a': 1, ^?'b': 2} | SyntaxNotEnabled | '?'{{Hint}}
        Parse | TestAllTypes{^?single_int32: 1} | SyntaxNotEnabled | '?'{{Hint}}
        Parse | TestAllTypes{single_int32: 1, ^?single_string: 'a'} | SyntaxNotEnabled | '?'{{Hint}}
        Parse | {'a': 1}.^?a + [^?1].size() + {^?'a': 1}.size() | SyntaxNotEnabled SyntaxNotEnabled SyntaxNotEnabled | '.?'{{Hint}} ;; '?'{{Hint}} ;; '?'{{Hint}}
        """;

    // The same forms in the text with the set are not errors of parsing: the check and the evaluation are what answer.
    private const string FormsThatParse = """
        {'a': 1}.?a
        d.?a
        {'a': 1}[?'a']
        [1, 2][?0]
        [?o]
        [1, ?n]
        {?'a': o}
        {'a': 1, ?'b': n}
        TestAllTypes{?single_int32: o}
        TestAllTypes{single_int32: 1, ?single_string: optional.of('a')}
        """;

    private readonly CelEnvironment enabled = ProtobufExample.Builder3()
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("o", CelType.OptionalOf(CelType.Int))
        .AddVariable("n", CelType.OptionalOf(CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment disabled = ProtobufExample.Builder3()
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("o", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(5))))
        .Add("n", CelValue.FromOptional(default))
        .Add("d", CelValue.FromInt64(7));

    [Fact]
    public void OptionalElementOfAList_IsAddedWhenItsOptionalHasAValue() => Assert.Empty(ExtensionRows.CheckValues(enabled, Lists, bindings));

    [Fact]
    public void OptionalEntryOfAMap_IsAddedWhenItsOptionalHasAValue() => Assert.Empty(ExtensionRows.CheckValues(enabled, Maps, bindings));

    [Fact]
    [Trait("Requirement", "F-2")]
    public void OptionalFieldOfAMessage_IsSetWhenItsOptionalHasAValue() => Assert.Empty(ExtensionRows.CheckValues(enabled, Messages, bindings));

    [Fact]
    public void PlainForms_KeepTheirMeaningWithTheSet() => Assert.Empty(ExtensionRows.CheckValues(enabled, PlainFormsWithTheSet, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void OptionalElementThatIsNotAnOptional_IsTypeMismatchAtTheValue() => Assert.Empty(ExtensionRows.CheckErrors(enabled, TypeErrors, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void EvaluationOfTheElements_GivesTheFirstError() => Assert.Empty(ExtensionRows.CheckErrors(enabled, EvaluationErrors, bindings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void EveryFormWithoutTheSet_IsSyntaxNotEnabledAtItsLexeme() => Assert.Empty(ExtensionRows.CheckErrors(disabled, WithoutTheSet, bindings));

    // The text of every form with the set has no error of parsing: the forms of the previous test are no errors here.
    [Fact]
    public void EveryFormWithTheSet_Compiles()
    {
        var failures = new System.Collections.Generic.List<string>();
        foreach (string expression in ExtensionRows.Lines(FormsThatParse))
        {
            CelCompileResult<CelValue> compiled = enabled.Compile<CelValue>(expression);
            if (!compiled.IsSuccess)
            {
                failures.Add(expression + ": " + compiled.Errors[0].Code + " " + compiled.Errors[0].Message);
            }
        }

        Assert.Empty(failures);
    }
}
