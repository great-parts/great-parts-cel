# Conformance runner

`tests/Great.Parts.Cel.Conformance` runs suites of CEL test cases against the library and judges each case by the rule of the cel-go conformance runner. It has one core and three suites: the cel-spec suite `tests/simple/testdata` of a pinned cel-spec commit, the regression corpus of the differential run ([eng-difftest.md](eng-difftest.md)), and the CEL Policy suite of a pinned commit of `cel-expr/cel-policy` ([below](#cel-policy-suite)). Every case of the suites runs; a case may fail only where an entry of the suite's skip list allows it.

## Project

| Item | Value |
| :- | :- |
| Project | `tests/Great.Parts.Cel.Conformance/Great.Parts.Cel.Conformance.csproj`, a test project, see [tests.md](tests.md) |
| Targets, modes | `net10.0`, `net11.0`; JIT and Native AOT |
| Packages | `xunit.v3.aot` 4.0.3; `Google.Protobuf` 3.36.2; `Grpc.Tools` 2.85.0 with `GrpcServices="None"` |
| References | `src/Great.Parts.Cel`, which grants it `InternalsVisibleTo`; `src/Great.Parts.Cel.Protobuf` for the test messages; `src/Great.Parts.Cel.Policy` for the CEL Policy suite, which grants it `InternalsVisibleTo` too; `eng/findings` for `EngCodes` and `Finding`, see [eng-checks.md](eng-checks.md#one-source-of-the-eng-codes) |
| Data | `tests/cel-spec/`, `tests/cel-policy/` and the files of `CelSpec/`, `CelPolicy/`, `Regression/` and `Synthetic/` |

## Data and build

`tests/cel-spec/` is an unchanged copy of the pinned cel-spec commit: `tests/simple/testdata`, the `.proto` files, `doc/langdef.md` and `LICENSE`. `tests/cel-spec/COMMIT` holds the full SHA of that commit.

- `Grpc.Tools` compiles `simple.proto` and its imports into C# classes, namespace `Cel.Expr`.
- A build target after `Protobuf_ResolvePlatform` of `Grpc.Tools` encodes every `*.textproto` of `tests/cel-spec/tests/simple/testdata/`, `Regression/` and `Synthetic/` into `obj/`: `$(Protobuf_ProtocFullPath)` with `--encode=cel.expr.conformance.test.SimpleTestFile`, the import paths `tests/cel-spec/proto` and `$(Protobuf_StandardImportsPath)`, and the test message protos as inputs, so that expanded `Any` values resolve.
- The binary files, their texts, the skip lists, the registry of known differences and `COMMIT` are embedded resources named by their paths from the repository root. The runner reads no files, also when published.
- The text serves only positions: a scan that skips comments and strings and counts braces records the line and column of each `test` token. The n-th `test` block of the m-th `section` is the n-th case of the m-th section.

## Layout

| Path | Contents |
| :- | :- |
| `Runner/` | the core: the case model, the rule, the skip list reader, the cell, the findings and the summary; `MarkdownTable.cs` and `ValueConversion.cs`, which `eng/difftest` links too; `TypeConversion.cs` |
| `CelSpec/` | `skips.md`, `CelSpecSuite.cs`, `CelSpecSuiteTests.cs`, `CelSpecParseTests.cs` |
| `Regression/` | the corpus files `*.textproto`, `skips.md`, `known-differences.md`, `RegressionSuite.cs`, `RegressionSuiteTests.cs` |
| `CelPolicy/` | `skips.md`, `PolicySuite.cs`, `PolicyCategory.cs`, `PolicyConfig.cs`, `PolicyValues.cs`, `PolicyFunctions.cs`, `PolicyJudge.cs`, `YamlAccess.cs`, `PolicySuiteTests.cs`, `PolicyRunnerTests.cs` ([CEL Policy suite](#cel-policy-suite)) |
| `Synthetic/` | `synthetic.textproto` and `SyntheticSuiteTests.cs`: the tests of the runner itself |
| `Environment/` | `CultureFitnessTests.cs`: both suites under the culture `tr-TR` ([tests.md](tests.md#culture-of-the-process)) |

## Core and suites

A suite turns its source into the common model of a case and supplies its skip list:

```text
internal sealed record ConformanceCase(
    string Name,                 // <file>/<section>/<test>
    string Path, int Line, int Column,
    string Expression,
    CaseEnvironment Environment, // container, declarations, disable_macros
    IReadOnlyDictionary<string, Cel.Expr.ExprValue> Bindings,
    CheckMode Check,             // Check, WithoutCheck, CheckOnly
    Expectation Expected)        // value, typed result, evaluation error, other matcher
```

`ICaseEvaluator` runs a case and returns a `CaseOutcome`: how far it got, the errors of the failed stage, the result type, the value or the evaluation error, or an exception. `LibraryEvaluator` calls the library; the runner's own tests use a scripted one. `Rule.Judge(case, outcome)` applies the rule below. `SuiteRun` runs all cases of a suite, matches them with the skip list for the current cell and returns findings and counts.

The suites run as two tests, `CelSpecSuiteTests.AllCases_PassOrAreSkipped` and `RegressionSuiteTests.AllCases_PassOrAreSkipped`. Each writes the summary line to its output and fails on a finding with `error`, with the finding lines as its message.

## Environment of the cases

All cases share one setup, as the cases of the cel-go runner share `stdOpts`. It is `new CelEnvironmentBuilder(CelStandardLibrary.V1)`: the latest version, because cel-go has no versions of the standard library. The builder also has the database of the test archive, `.UseTimeZones(ZoneInfoFile.Database())` ([tests](tests.md#the-suite-runner-and-time)), the extension sets of the list `ExtensionSets` — the internal `block` set joins with `bindings` version 1, as `celBlockLib` in cel-go — and the protobuf provider of the test messages ([Messages, `Any` and the provider of the cases](#messages-any-and-the-provider-of-the-cases)).

For each case the runner starts a new builder from the setup, adds what the case declares and calls `Build`; an environment is not shared between cases, since their declarations differ.

- **`container`** — `UseContainer(container)`. The name resolution of the library follows the language definition ([checking.md](checking.md#name-resolution)); `namespace.textproto` checks it with qualified variables such as `x.y` and `com.example.y`, which `AddVariable` takes as names and `CelBindings.Add` binds under the same name.
- **`type_env`, `ident`** — `AddVariable(name, type)`, the type converted by `TypeConversion` from `cel.expr.Type`: `primitive`, `list_type`, `map_type`, `dyn`, `null`, `wrapper` and `type`; `message_type` and `well_known` by the rows of the types of time ([tests](tests.md#the-suite-runner-and-time)) and of "Types" in "Messages, `Any` and the provider of the cases"; `abstract_type` as "The sets `optional` and `block` in the runner" says. A type outside the rows throws `NotImplementedException`, and the case fails with that reason.
- **`type_env`, `function`** — the declarations of a case become one internal set with stubs, as "The sets `optional` and `block` in the runner" says.
- **Reserved names.** The runner neither declares nor binds the names `true`, `false`, `null` and `in` from `type_env` and `bindings`: they are keywords, never identifiers, and the library rejects them as variable names. The cases `basic/reserved_const/*` still check their point: a literal is never shadowed. The conformance page names this difference from the cel-go runner environment, like the evaluation without type checking.
- **`disable_macros`** parses the expression with the syntax options of the environment and `MacroTable.Empty`, as the cel-go runner clears the macros. Only the internal `Parse` takes syntax options, so a case with `disable_macros` on the checked path fails with the reason `disable_macros needs an internal compile entry point`; the cases `disable_check` and `check_only` pass the options to `Parse`. No case of the pinned commit has the flag.
- **`locale`** is ignored, as by cel-go.

## The sets `optional` and `block` in the runner

This section is about the three files of [optional](extension-optional.md): `optionals` (70 cases), `block_ext` (37) and `type_deduction` (66).

**The sets.** The static list `ExtensionSets` of `LibraryEvaluator` ([tests](tests.md#conformance-runner-and-the-extension-sets)) has `CelExtensions.Optional(2)` and `ExtensionCatalog.Block`, the internal set of [extensions](extensions.md#the-sets-optional-and-block), read through `InternalsVisibleTo`, which the runner has. `Bindings(1)` is in the list already, and `block` needs it. Every case then runs in an environment with the optional syntax and the four macros of `block`, as the cases of the runner of cel-go run with `OptionalTypes` and `celBlockLib` in its common options; no other file of the suite uses `.?`, `[?` or `{?`, so the syntax changes nothing for the cases of the other files. The variables `@index0` to `@index29` are declared in every case, and a case that is not about blocks never names them.

**Functions that a case declares** (`type_env`, `function`). Nine cases of `type_deduction` declare functions, and every one is `check_only`: nothing is evaluated, so no implementation is ever called, but `Build` requires one for each overload (`MissingImplementation`). Four of the nine, `type_parameters/*`, also declare the abstract type `tuple(T, U, V)`, which no public member can declare: the application has no way to name an abstract type of its own. So the runner makes, for each case that declares a function, one internal set:

- **`CaseDeclarations`**, a class of `Runner/` that derives from `ExtensionDefinition` (its constructor is `protected`, which an heir in another assembly may call), with the name `type_env`, the version 0, and the source `type_env of the case`. Its overloads are one `OverloadBinding` for each `overload` of each `function` declaration: the function name, a `CelOverloadDecl` with the identifier, the call style (`is_instance_function`), the parameter types and the result type converted by `TypeConversion`, the fixed cost 0, no argument cost, and the implementation `Stub`. Its abstract types are one `AbstractTypeDeclaration(name, type)` for each name of an `abstract_type` that occurs in the declarations of the case — in a function or in an `ident` — except `optional_type`, which the set `optional` declares, with the type that the first occurrence converts to, so `tuple` is declared once with three parameters. It has no variables, no macros and no dependencies.
- **`Stub`** is `(arguments, in site) => throw new InvalidOperationException("function declared by a case that only checks types")`. A case that evaluates what it declared would not end in an exception: the library turns the exception of an application function into the error value `ApplicationException` (3014), `application code threw System.InvalidOperationException in overload '<id>' of function '<function>'`, and the text above is only the message of the `Exception` that `CelError.Exception` holds. The runner reports such a case as failed with that error and, when it prints one, the text of its `Exception`; none does today.
- The set is wrapped with the internal constructor of `CelExtension` and added with `AddExtension` after the shared sets, so a conflict with them — two overloads that overlap — is a build error that fails the case, as any other build error does.

The variables of a case (`ident` declarations) are added as "Environment of the cases" says, with the rows of `TypeConversion` below. The alternative to the internal set — the public `AddFunction(decl)` with `AddFunctionImplementation(id, stub)` for each overload — declares the eight cases that have no abstract type and not the four with `tuple`, so it would need this set anyway; one path serves all nine, and the checker is the only code that runs.

**`TypeConversion`** has these rows for the abstract types and the type parameters:

| `cel.expr.Type` | `CelType` | Back |
| :- | :- | :- |
| `abstract_type: optional_type` with one parameter | `CelType.OptionalOf(p)` | `abstract_type` with the name `optional_type` and the parameter, for the kind `Optional` |
| `abstract_type` of another name | `CelType.AbstractNamed(name, parameters)`, internal, with the converted parameters | `abstract_type` with the name and the parameters, for the kind `Abstract` (the row exists) |
| `type_param` | `CelType.TypeParameter(name)`, which only a declaration of an overload carries | not needed: a checked expression has no free parameter, so no result type has one |
| `type` with a parameter that is any of the above | `CelType.TypeOf(parameter)` | `type` with the converted parameter (the row exists) |

A parameter count other than one for `optional_type` is `NotImplementedException`, as the library has no such type. `ValueConversion` has no row for the kind `Optional`: `cel.expr.Value` has no optional, and no case of the three files returns one — their results are `bool`, `int`, `string`, lists and maps. A result of that kind would fail the case with `NotImplementedException`, which names the kind. The comparison of the deduced type is the one of "The rule", `diffType` by `cel.expr.Type`, so `[optional.of(1), optional.none()]` is `list_type` of the `abstract_type` `optional_type` of `INT64`, and `tuple(dyn, dyn, dyn)` the `abstract_type` `tuple` of three `dyn`.

**The synthetic suite** has cases and tests for each row above, for `CaseDeclarations` with a function that has type parameters and with an abstract type of three parameters, for the stub (a case that evaluates it fails with `ApplicationException` whose `Exception` has the text of the stub, not with a pass), for a case whose declared type has the name of a type of a set (`optional_type` is not declared twice), and for the list `ExtensionSets`, which has ten sets with `optional` and `block`.

## Calls into the library

| Case | Calls |
| :- | :- |
| checked | `env.Compile<CelValue>(expr)` — the expected type `dyn`, as the cel-go runner checks without one; the result type is `CelProgram<CelValue>.ResultType`; then `program.Evaluate(bindings)`, without a cost limit or a token |
| `disable_check` | internal `env.Parse(expr)`, then internal `env.EvaluateWithoutTypeCheck(parsed, bindings)`; the result type is `dyn`, as for an unchecked expression in cel-go |
| `check_only` | internal `env.Parse(expr)`, then internal `env.Check(parsed)`; the result type is `CheckedExpression.ResultType`; nothing is planned or evaluated |

See [facade.md](facade.md#internal-entry-points). A binding of the kind `value` is converted by `ValueConversion` and added with `CelBindings.Add(name, CelValue)`. A value converts from `cel.expr.Value` to `CelValue` and back for the kinds the library supports — `null_value`, `bool_value`, `int64_value`, `uint64_value`, `double_value`, `string_value`, `bytes_value`, `list_value`, `map_value` and `type_value`, the name of a type value as `type()` gives it (`int`, `list`, `type`), and `object_value` of `google.protobuf.Timestamp` and `google.protobuf.Duration` ([tests](tests.md#the-suite-runner-and-time)), of the other known types and of the test messages ([below](#messages-any-and-the-provider-of-the-cases)) — through the public API only, since `eng/difftest` links the file; a type converts in `TypeConversion`, a file that is not linked: from `CelType` to `cel.expr.Type` for the result type, and from `cel.expr.Type` to `CelType` for the declarations of a case. Other kinds throw `NotImplementedException`.

## The rule

Source: `conformance/conformance_test.go` of cel-go v0.33.4, https://github.com/cel-expr/cel-go/blob/v0.33.4/conformance/conformance_test.go, function `conformanceTest` and the default matcher in `TestConformance`, read on 2026-11-18; the file of v0.32.0, read on 2026-10-28, is the same.

| Step | cel-go v0.33.4 | Lines | The runner |
| :- | :- | :- | :- |
| matcher | a case without a result matcher expects the value `bool` `true` | 313–321 | the same |
| parse | a parse error fails the case | 203–207 | an error of the stage `Parse` fails |
| environment | `container` and `type_env` extend the environment; an error fails | 208–223 | a build error or a throwing builder member fails |
| check | unless `disable_check`, the expression is checked; an issue fails, even when an evaluation error is expected | 224–229 | an error of the stage `Check` fails |
| `check_only` | the matcher must be `typed_result`; only the deduced type is compared | 230–239 | the same |
| program | an error creating the program fails | 240–243 | part of `Compile<T>` |
| bindings | `value` becomes a value, `error` an error value, `unknown` an unknown set | 244–250, 149–173 | `value` only; a case with other bindings fails |
| `value` | evaluation without error and an equal value | 253–263 | the same |
| `typed_result` | as `value`, and the deduced type is equal | 264–277 | the same |
| `eval_error` | evaluation gives an error or an error value; the message is not compared | 278–284 | an error of the stage `Evaluate` |
| other matchers | `any_eval_errors`, `unknown`, `any_unknowns` fail | 285–287 | the same |

Values are compared as `cel.expr.Value` messages, as `diffValue` does with `cmp.Diff`, `protocmp.Transform()` and sorted map entries (lines 184–190): field by field, scalar fields with Go `==`, the entries of a map in any order. cel-go expands an `Any` whose type its registry knows and compares the messages, so the runner does too: it unpacks both `Any` values with the test messages compiled into C# classes like `simple.proto` and compares the messages by the `Equals` of the generated class; an `Any` of a message that the table of the runner lacks is compared by its `type_url` and the bytes of its `value`. Types are compared as `cel.expr.Type` messages (`diffType`, lines 175–182). An exception from the library fails the case, and the reason names its type; a crash of the process is a defect, never a skip entry.

## Skip lists and findings

The cell of a run is the mode (`jit` when `RuntimeFeature.IsDynamicCodeSupported`, else `native-aot`), the operating system (`linux-x64`, `macos`, `browser-wasm`) and the target, fixed at compile time. The cells of the matrix are JIT on `linux-x64`, `macos` and `browser-wasm` and Native AOT on `linux-x64`, for both targets. The runtime for WebAssembly reports dynamic code as supported, so the cell of its interpreter has the mode `jit`; a case that fails only there has the scope `* browser-wasm *`. A skip entry applies when its scope contains the cell.

| Code | When | Place |
| :- | :- | :- |
| `ENG801` | a case fails and no entry applies | the `test` token of the case |
| `ENG802` | a case passes and an entry applies | the entry line, column 1 |
| `ENG804` | an entry names a case that the suite does not have | the entry line |
| `ENG800` | an invalid entry: the columns, the form of `Case`, an unknown value of `Scope`, `Verdict` or `Severity`, a scope without cells of the matrix, an empty reason, a link of the wrong form in `Issue`, a link in `Reference` to anything but the language definition at the commit of `COMMIT`, a repeated entry or overlapping scopes of one case, a verdict `cel-go-bug` or `by-design` without a registry row of the same signature, the wrong order | the entry line, or `(1,1)` for a file without the table |

`ENG803` belongs to the release gates of `eng/checks`, not to the runner. Messages follow the form of the findings ([eng-checks.md](eng-checks.md#finding-and-output)), for example:

```text
tests/cel-spec/tests/simple/testdata/basic.textproto(12,3): error ENG801: case 'basic/self_eval_zeroish/self_eval_int_zero' failed: expected int 0, got error 'not implemented' (jit, linux-x64, net11.0)
tests/Great.Parts.Cel.Conformance/CelSpec/skips.md(9,1): error ENG802: case 'basic/functions/unbound' now passes; remove it from the skip list
```

Both skip lists share one reader, `MarkdownTable`: one table, `\|` inside a cell, the columns of the suite in their order. The corpus list has the columns `Signature`, `Verdict` and `Severity`; the registry `known-differences.md` is read only for its signatures.

## Summary line

Each suite test writes one line: the passed cases out of all cases of the suite, the source and the date of the run in UTC. A failed case on the skip list counts as not passed.

```text
cel-spec 8bef98f: 47/2592 passed, 2026-11-20 (jit, linux-x64, net11.0)
regression corpus: 0/0 passed, 2026-11-20 (jit, linux-x64, net11.0)
```

## Parse test of the cel-spec suite

`CelSpecParseTests.EveryExpression_ParsesAndPrintsBack` takes every case of the cel-spec suite from `CelSpecSuite` and parses its expression with the internal `Parse` of an environment of `CelStandardLibrary.V1`. The syntax options are those of the cel-go runner, which gives all files one setup with `OptionalTypes` and clears the macros for `disable_macros`: `SyntaxOptions.Standard` with `OptionalSyntax` on, and `MacroTable.Empty` for a case with `disable_macros`. The test never adds an extension set to the options, `block` included: the macros of the sets are not in the table, their calls stay calls, and the tree that is printed and parsed again is the tree of the text, not an expansion. The test asserts, for each case:

- the parse succeeds;
- printing the tree and parsing the text again with the same options gives a tree without a difference by `SyntaxTreeComparer` ([syntax.md](syntax.md#printing-and-tree-equality)).

The test macros of the internal set `block`, `cel.iterVar` and `cel.accuVar`, stay calls as well, and a comprehension macro whose first argument is such a call is `InvalidMacroCall` (1006), `argument must be a simple name` ([syntax.md](syntax.md#macros)). Eight cases of `block_ext.textproto` write their iteration variables so: `block_ext/basic/multiple_macros_1`, `block_ext/basic/multiple_macros_2`, `block_ext/basic/multiple_macros_3`, `block_ext/basic/nested_macros_1`, `block_ext/basic/nested_macros_2`, `block_ext/basic/adjacent_macros`, `block_ext/basic/macro_shadowed_variable_1` and `block_ext/basic/macro_shadowed_variable_2`. The test keeps their names in a list, and for a case of the list it asserts instead:

- the parse fails, with at least one error, and every error is `InvalidMacroCall` whose message is `invalid call of macro '<macro>': argument must be a simple name`, at the `(` of a macro call whose first argument is a call of `cel.iterVar` or `cel.accuVar`: the text from the offset of the error starts with `(cel.iterVar(` or `(cel.accuVar(`.

A case of the list that parses fails the test with its name and the words `parses now: remove it from the list`; a name of the list that is no case of the suite fails it too. The list stays: it does not wait for the set `block`. The test does not give the set to the parser, and it could not: the expansion of `cel.index(0)` is a slot, which `Printer` writes as `@index0`, and that of `cel.iterVar(0, 0)` is the name `@it:0:0`, texts that the parser does not read, so printing and parsing again would fail on every case of `block_ext` that has them. The macros of `block` are run by the cases themselves through `ExtensionSets` ([above](#the-sets-optional-and-block-in-the-runner)) and by the tests of the set, not by this test; `Printer` stays as it is, and a change that made it write the call that a macro replaced is not done.

A failure lists every failed case with its name, the place of its `test` token and the first error or difference. A second test, `CaseCount_IsThatOfThePinnedCommit`, checks that the suite has 2 771 cases, so a lost case shows up; it changes when the pinned commit moves. The skip list does not apply: every expression of the pinned commit parses, except the cases of the list above, which fail exactly as described. The workflow on the master branch of cel-spec does not run this test.

## CEL Policy suite

`tests/cel-policy/` is an unchanged copy of `conformance/testdata` of the pinned commit of `cel-expr/cel-policy` (Apache-2.0, see `NOTICE`); `tests/cel-policy/VERSION` has the full SHA of the commit and its date. The suite lives in `CelPolicy/` of the runner project and in the tests `PolicySuiteTests` (the suite) and `PolicyRunnerTests` (the runner on synthetic categories). The project references the policy package, which gives it `InternalsVisibleTo` for its YAML reader; `tests/cel-policy/**/policy.yaml`, `config.yaml`, `tests.yaml`, `VERSION` and `CelPolicy/skips.md` are embedded resources like the other data. The files of the copy that are no YAML of the suite (`config.textproto`, `tests.textproto`, `base_config.yaml`, `partial_config.yaml`) are not read: every category that has a textproto form has the same data in YAML.

- **Categories.** The directories of the copy that hold a `tests.yaml`, in ordinal order, 41 at the pinned commit (`PolicySuite.Categories`; the 27 directories of `_TEST_DIRS` and the 14 of `_TEST_ERROR_DIRS` of `conformance/BUILD.bazel`). The other directories are the configurations of a test of the policy of cel-go. A category that a new commit adds is run without a change of the code, and one that it removes is not; the test `Categories_AreTheDirectoriesWithTests` writes the list of the pinned commit down, so a change of the commit changes it.
- **Cases.** A case is a test of `tests.yaml` (`section` or `sections`), named `<category>/<section>/<test>`, such as `nested_rule/banned/restricted_origin` and `compile_errors/syntax/compile/syntax_error`, the name by which the skip list names it; its position in the findings is the name of the test in `tests.yaml`. The suite has 91 cases.
- **Environment.** `config.yaml` is translated to the JSON form of the environment description (`PolicyConfig`: `name`, `container`, `extensions`, `variables`, `functions`; a type record becomes the string of the JSON form, `google.protobuf.Timestamp` becomes `timestamp`) and read by `CelEnvironmentJson.Apply` into a builder that has the standard library, the time zone database and the provider of the test messages of the runner; then the sets `optional` and `bindings` are added, as the tests of cel-go do. `stdlib` is not carried (the library keeps the whole standard library). Each extension gets the version the catalog has, the only one: a version of cel-go that the catalog does not have (`strings` 2, `lists` without a version) would be `UnknownExtensionVersion`, and the catalog version is a superset. A category without `config.yaml` has the environment without declarations. The functions that a configuration declares are implemented by the runner (`PolicyFunctions`): `locationCode` of the harness of the policy package of cel-go, `hasCreditCard` and `hasEmailOrPhone` of its test of composition.
- **Policy and compilation.** `policy.yaml` is read and compiled once per category (`PreparedCategory`) with `CelPolicy.Compile<CelValue>` in that environment; a case evaluates the program.
- **Inputs.** `input.<name>.value` is a YAML literal turned into a `cel.expr.Value` by the tag of the scalar (`PolicyValues`) and into a value of the library by `ValueConversion`; `input.<name>.expr` is an expression compiled and evaluated in the environment of the category; the name may be a qualified name, bound under the same name. `context_expr` needs the fields of a message as variables, which the library has no input for: such a case fails with that reason.
- **Rule.** A case with `output.value` or `output.expr` passes when the program evaluates without error to a value equal to the expected one by the equality of CEL, or to an optional that holds such a value (the rule of the test runner of cel-go); the expected `expr` is evaluated in the environment of the category. A case with `output.error_set` passes when the parse or the compilation of the policy fails and every string of the set is a substring of the message of some error. An exception or any other output fails the case with its reason.
- **Skip list.** `CelPolicy/skips.md` has the columns of the cel-spec list; its `Reference` is the minimal case, with no link to check. The scope is `* * *` for every entry. The counter, the findings (`ENG800`, `ENG801`, `ENG802`, `ENG804`) and the summary line are those of the other suites: `cel-policy 8e4cf03: N/91 passed, <date> (<cell>)`.
- **Modes.** The suite runs as one test under JIT and under Native AOT with the others, and on `browser-wasm`: `tests/Great.Parts.Cel.Conformance.Wasm` links the files of `CelPolicy/` except the tests, embeds the same data and runs `PolicySuite.Run` after the cel-spec suite; its exit code counts the findings of both suites. The nightly run on the main branch of `cel-expr/cel-policy` is the job `conformance-master-policy` of `conformance-master.yml` ([Run on master](#run-on-master)); the content and the closure of the package are checked by `packages` and `package-closure` in the cell JIT Linux `net11.0` of `ci.yml` and in the job `build` of `release.yml`, with the list `eng/packages/Great.Parts.Cel.Policy.txt`.

## Synthetic suite

`Synthetic/synthetic.textproto` is a small suite in the cel-spec format, built like the others. `SyntheticSuiteTests` run it with a scripted evaluator, independently of the progress of the library; skip lists are raw string literals inside the tests. A wrong expectation gives `ENG801` at the file and line of the case; an entry for a passing case `ENG802`; an entry for a missing case `ENG804`; each form of an invalid entry `ENG800`; a failure outside the scope of its entry `ENG801`, a pass outside it nothing. Each test checks code, path, line and column and matches the line with `Finding.Pattern`.

## Run on master

`.github/workflows/conformance-master.yml` runs the cel-spec suite on the last commit of the master branch of `cel-expr/cel-spec`, so a change in the test data shows up before the pinned commit moves. It runs every night (02:47 UTC) and by hand, on `main` only, in one cell: JIT, `linux-x64`, `net11.0`, on `ubuntu-latest`.

1. The workflow clones the last commit of master without its history and replaces the data of `tests/cel-spec/` in its workspace, file for file as [Data and build](#data-and-build) lists it: `tests/simple/testdata`, the `.proto` files, `doc/langdef.md`, `LICENSE`, and `COMMIT`, which then holds the full SHA of the master commit.
2. It replaces `CelSpec/skips.md` with its table without entries. The skip list belongs to the pinned commit; so a case of master that fails is `ENG801`, and no entry gives `ENG802` or `ENG804`. The regression corpus and its list are not run.
3. It restores in the locked mode, builds `tests/Great.Parts.Cel.Conformance` for `net11.0` and runs only `CelSpecSuiteTests.AllCases_PassOrAreSkipped` of the built test assembly.
4. Nothing of the replaced files is committed or pushed: they exist only in the workspace of the run. `.github/scripts/conformance-report.mjs` turns the log of the suite and `tests/cel-spec/COMMIT` into the report of the run, and the job `publish`, the only one with `contents: write`, commits it to the branch `reports` ([eng-release.md](eng-release.md#reports-of-the-runs)). The job runs when the suite gave its summary line; a case that fails does not stop it.

The result is the summary line of the suite with `master` after `cel-spec`, in the step summary and the log, with the lines of the failed cases (the first 200 in the summary, all in the log):

```text
cel-spec master 1f2e3d4: 2601/2614 passed, 2026-11-05 (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/macros2.textproto(77,3): error ENG801: case 'macros2/…' failed: …
```

The share is counted from all cases of the suite, as for the pinned commit ([Summary line](#summary-line)).

A failed case does not fail the workflow: the test of the suite fails with the findings, and the step reads the summary line from the output. The workflow is red when the restore or the build fails (new test data that protoc cannot encode included), when the process of the run falls before the summary line, or when the line or the report of the run is missing. A case that crashes the process is a defect of the library: it goes to the private report of `SECURITY.md`, not to an issue.

The same workflow file runs the CEL Policy suite on the last commit of the `main` branch of `cel-expr/cel-policy` (the repository has no tags), in the jobs `conformance-master-policy` and `publish-policy`, at the same time and in the same cell. The steps are those above with the data of the suite:

1. The workflow clones the last commit of `main` without its history, empties `tests/cel-policy/` and fills it with `conformance/testdata` of that commit, its `LICENSE` and `VERSION`, whose first line is the full SHA of the commit and whose second line is its date, as the copy of the pinned commit has them.
2. It replaces `CelPolicy/skips.md` with its table without entries.
3. It restores in the locked mode, builds `tests/Great.Parts.Cel.Conformance` for `net11.0` and runs only `PolicySuiteTests.AllCases_PassOrAreSkipped`: the other tests of the policy classes depend on the pinned commit (its number of cases and its SHA).
4. `.github/scripts/conformance-report.mjs --suite cel-policy` turns the log and `tests/cel-policy/VERSION` into the report, the artifact `conformance-master-policy-report`, and `publish-policy` commits it to the directory `conformance-master-policy` of the branch `reports`.

The result is the summary line with `master` after `cel-policy`, for example `cel-policy master 8e4cf03: 85/91 passed, 2026-11-23 (jit, linux-x64, net11.0)`. Each job reads the summary line of its own suite, so the two results never mix. The categories of the suite are the directories of the copy that hold a `tests.yaml` (`PolicySuite.cs`): a category that `main` adds is run, and one that `main` removes is not, so the share counts the cases that `main` has.

## Messages, `Any` and the provider of the cases

The ten files of protobuf — `proto2`, `proto3`, `wrappers`, `dynamic`, `enums`, `proto2_ext`, `comparisons`, `parse`, `string_ext` and `sets_ext` — run on the shared environment of the cases with a protobuf provider and the set `protos`. The runner needs three things for them: the classes of the test messages, the provider, and the unpacking of `Any` where it converts values and compares results.

**The classes.** `Great.Parts.Cel.Conformance.csproj` compiles the three test messages of the pinned commit — `proto2/test_all_types.proto`, `proto3/test_all_types.proto` and `proto2/test_all_types_extensions.proto` — into C# classes, the namespaces `Cel.Expr.Conformance.Proto2` and `Cel.Expr.Conformance.Proto3`, with `Protobuf Include`, `ProtoRoot` of `tests/cel-spec/proto` and `GrpcServices="None"`, as it compiles `simple.proto` and its imports. The encoding of the text files by `protoc` is unchanged: it already takes the three messages as inputs so that an expanded `Any` and the extensions resolve. The project references `src/Great.Parts.Cel.Protobuf`.

**The provider of the cases.** The setup of the runner, once for all cases, adds one provider: a `CelProtobufTypesBuilder("conformance")` with `AddFile` of the descriptors of the three files — the `Descriptor` of their generated reflection classes — and the nine typed extensions of the extensions file, `AddProtoExtension` with the objects that `protoc` generates: the five declared at the top level of the file and the four declared inside `Proto2ExtensionScopedMessage`, a message scope with `repeated` among them. Then `AddTypes(provider)` and, in the list of sets, `CelExtensions.Protos(0)`. A case declares no provider: `container` finds `TestAllTypes` of the package that it names, and `type_env` names the messages by `message_type`. The same builder gives the table of the runner for unpacking, below, and the `ExtensionRegistry` of the nine extensions, so that the runner parses a message with the extensions the way the provider does.

**Values.** `ValueConversion` is linked by `eng/difftest` and uses the public API only; the messages of the cases are not classes of that project. So the conversion of an `object_value`, an `Any`, is in two parts:

- **The known types** are converted inside the linked file, by the classes of `Google.Protobuf.WellKnownTypes`, which both projects have: while the content is itself an `Any` it is unpacked again — the case `dynamic/any/var` binds an `Any` that holds an `Any` — and then `Timestamp` and `Duration` as before; `Int32Value` and `Int64Value` an `int`; `UInt32Value` and `UInt64Value` a `uint`; `FloatValue` and `DoubleValue` a `double`; `BoolValue`, `StringValue` and `BytesValue` their kinds; `Struct` a map, `ListValue` a list and `Value` the kind that is set, recursively. `ToExprValue` takes the same way back for the results of those kinds.
- **Any other message** is converted by a resolver that the linked file declares as a static property, `Func<Any, CelValue?>? MessageResolver`, null in the stand of `eng/difftest` and set by the runner: it finds the entry of the full name of the type URL in a table of the runner — the messages of the three descriptors and of the files they import, nested ones included, by name, each with its `MessageParser` — parses the content with the extension registry, finds the description of the name in the provider by `Name`, and returns `CelValue.FromObject(description, instance)`, a pair, so that the value carries its description wherever the case puts it. A name that the table lacks, or no resolver, is `NotImplementedException`.
- **The way back.** `ToExprValue` of an object value takes the instance with `TryGetObject`; an `IMessage` becomes `Value { ObjectValue = Any.Pack(message) }`.

**Comparison with the expected value.** The rule compares `cel.expr.Value` messages; for an `object_value` that holds a message of the table, both `Any`s are unpacked with the table and the two messages are compared with the `Equals` of the generated class, which is equal for maps in any order, covers the extensions of the registry, compares unknown fields by bytes and compares a floating-point field by its bits, so that a result with a NaN equals its expected value as the rule needs. A nested `Any` field is compared by that `Equals`, so by its type URL and its bytes; the cases of the pinned commit agree with it. An `Any` of a type that the table lacks is compared by `type_url` and bytes.

**Types.** `TypeConversion` has these rows besides those of the types of time and of the sets; a type outside the rows throws `NotImplementedException`:

| `cel.expr.Type` | `CelType` | Back |
| :- | :- | :- |
| `well_known: ANY` | `CelType.Any` | `well_known: ANY` for the kind `Any` |
| `well_known: TIMESTAMP`, `DURATION`; `message_type` of those two names | `CelType.Timestamp`, `CelType.Duration` | `well_known` of the kind |
| `message_type: "<name>"` of any other name | `CelType.ObjectNamed(name)`: the object type of a message of the provider, or for the thirteen known names the type of [types](types.md#well-known-names) — `message_type: "google.protobuf.Int32Value"` is `wrapper(int)`, `"google.protobuf.Any"` is `Any` | `message_type` with the name, for the kind `Object` |
| `wrapper: P` | `WrapperOf(P)` | `wrapper` with the converted parameter |
| `abstract_type` | the rows of "The sets `optional` and `block` in the runner" | — |

The enumeration `WellKnownType` of `cel.expr.Type` has `ANY`, `TIMESTAMP` and `DURATION`; `Struct`, `Value` and `ListValue` come as `message_type`, `map` and `dyn`.

**The skip list.** A case that passes while `CelSpec/skips.md` has an entry for it is `ENG802`, so the list keeps no entry of a case that passes. **The regression corpus** runs on the same environment of the cases, with the provider and the set `protos`, so that its protobuf file can name the messages: `Regression/protobuf-differences.textproto` has four cases, the minimal cases of the differences of [protobuf-messages](protobuf-messages.md#differences-from-cel-go), in the order of the file `any_depth/chain_of_17`, `literals/duration`, `literals/timestamp`, `unknown_fields/equality`; the expectation of each is the answer of cel-go, the library fails each by the decided difference, and `Regression/skips.md` has an entry for each, verdict `by-design`, severity S2 for the three that end in an error and S1 for the equality. **The synthetic suite** (`Synthetic/`) has cases and tests for these conversions: an `Any` that holds an `Any`, each known type in both directions, the resolver absent, an `Any` of an unknown type, the comparison of two messages with maps in different orders and with an extension, and each row of `TypeConversion`.

## Native AOT

The project is published under Native AOT and runs as an executable ([tests.md](tests.md#same-tests-under-jit-and-native-aot), [eng-test.md](eng-test.md#test)). The runner uses no reflection: generated messages, embedded resources and the API of the library.

## browser-wasm

The project `tests/Great.Parts.Cel.Conformance.Wasm` runs the cases of the cel-spec suite on `browser-wasm`. It is a console application of the runtime for WebAssembly, published in the default mode — the interpreter of the runtime, IL trimming, no AOT compilation to WebAssembly, no workload — and the assembly is named `Great.Parts.Cel.Conformance`, because the library shows its internal entry points to that name. It compiles the sources of `Runner/` (all but `SuiteAssert.cs`, which needs xUnit) and `CelSpec/CelSpecSuite.cs` as linked files, embeds the same data as the suite project (the cases of `tests/simple/testdata`, `CelSpec/skips.md`, the pinned commit and the time zone archive) and reads no file. `Program.cs` runs `CelSpecSuite.Run` in the cell `jit, browser-wasm` and prints the summary line, the skipped cases of the cell with their reasons and the findings; then it runs the check of the database of the system (`SystemZones.cs`): the application has the runtime option `System.TimeZoneInfo.Invariant`, which `InvariantTimezone` sets (the property itself needs the workload `wasm-tools`, and the job installs none; the project writes the option with a target just before `runtimeconfig.json` is generated, because the SDK adds its own item for it with the value `false` after the body of the project, and a plain item of the project would lose to it), so the runtime has no named zone, and the check requires that `CelTimeZoneDatabase.FromSystem()` behaves as a machine without zones ([time-zones-system](time-zones-system.md#tests)). The exit code is 0 without a finding with the severity error and without a problem of that check, 1 with one, and 2 when the cell is not `browser-wasm` or the suite has no cases.

The job `wasm` of `ci.yml` publishes the runner for each target, copies `main.mjs` next to the published files and runs it under Node.js in a container (`node main.mjs`): the script starts the runtime and ends the process with the exit code of `Main`. The next step requires the summary line of each suite, `cel-spec` and `cel-policy`, in the log of the run and copies them to the summary of the job. The test application `tests/Great.Parts.Cel.Wasm.App` is a Blazor WebAssembly application that builds the two expressions of the README and `docs/protobuf.md` and a small policy, and takes `Great.Parts.Cel`, `Great.Parts.Cel.Protobuf` and `Great.Parts.Cel.Policy` as roots of the trimmer, so the trimmer analyzes all their code; the job publishes it, every warning is an error, and the step counts the lines `IL2xxx` and `IL3xxx` of the log with `.github/scripts/il-gate.mjs`, which must be 0. A control example shows that this gate turns red: the next step publishes the same application with `-p:IlControl=true`, which adds `IlControl/IlWarning.cs` with one call that is not safe under trimming (`IL2070`) and makes the assembly of the application a root of the trimmer, and requires that the gate fails on that log and that the log has `IL2070`. Without the property the file is not part of the application. Neither project is in the solution.
