# Conformance runner

`tests/Great.Parts.Cel.Conformance` runs suites of CEL test cases against the library and judges each case by the rule of the cel-go conformance runner. It has one core and two suites: the cel-spec suite `tests/simple/testdata` of a pinned cel-spec commit, and the regression corpus of the differential run ([eng-difftest.md](eng-difftest.md)). Every case of both suites runs; a case may fail only where an entry of the suite's skip list allows it.

## Project

| Item | Value |
| :- | :- |
| Project | `tests/Great.Parts.Cel.Conformance/Great.Parts.Cel.Conformance.csproj`, a test project, see [tests.md](tests.md) |
| Targets, modes | `net10.0`, `net11.0`; JIT and Native AOT |
| Packages | `xunit.v3.aot` 4.0.2; `Google.Protobuf` 3.36.2; `Grpc.Tools` 2.84.0 with `GrpcServices="None"` |
| References | `src/Great.Parts.Cel`, which grants it `InternalsVisibleTo`; `eng/findings` for `EngCodes` and `Finding`, see [eng-checks.md](eng-checks.md#one-source-of-the-eng-codes) |
| Data | `tests/cel-spec/` and the files of `CelSpec/`, `Regression/` and `Synthetic/` |

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

All cases share one setup, as the cases of the cel-go runner share `stdOpts`. In the walking skeleton it is `new CelEnvironmentBuilder(CelStandardLibrary.V1)`: the latest version, because cel-go has no versions of the standard library. From the part of time on, the builder also has the database of the test archive, `.UseTimeZones(ZoneInfoFile.Database())` ([tests](tests.md#the-suite-runner-and-time)). Later parts add their extension sets; the internal `block` set joins with `bindings` version 1, as `celBlockLib` in cel-go.

For each case the runner starts a new builder from the setup, adds what the case declares and calls `Build`; an environment is not shared between cases, since their declarations differ. A member that is not implemented yet throws, and the case fails until it arrives.

- **`container`** — `UseContainer(container)`. The name resolution of the library follows the language definition ([checking.md](checking.md#name-resolution)); `namespace.textproto` checks it with qualified variables such as `x.y` and `com.example.y`, which `AddVariable` takes as names and `CelBindings.Add` binds under the same name.
- **`type_env`, `ident`** — `AddVariable(name, type)`, the type converted by `TypeConversion` from `cel.expr.Type`: `primitive`, `list_type`, `map_type`, `dyn`, `null`, `wrapper` and `type` in this part; `message_type` and `well_known` for the two types of time, with the time part ([tests](tests.md#the-suite-runner-and-time)); the other `message_type`, `well_known` and `abstract_type` throw `NotImplementedException` until their parts, and the case fails with that reason. The seven files of this part declare only `primitive`, `list_type` and `map_type`.
- **`type_env`, `function`** — no file of this part declares functions. The case fails with the reason `function declarations need a later part`, and the part that brings the first such file decides how the runner supplies their implementations, which `Build` requires (`MissingImplementation`).
- **Reserved names.** The runner neither declares nor binds the names `true`, `false`, `null` and `in` from `type_env` and `bindings`: they are keywords, never identifiers, and the library rejects them as variable names. The cases `basic/reserved_const/*` still check their point: a literal is never shadowed. The conformance page names this difference from the cel-go runner environment, like the evaluation without type checking.
- **`disable_macros`** parses the expression with the syntax options of the environment and `MacroTable.Empty`, as the cel-go runner clears the macros. Only the internal `Parse` takes syntax options, so a case with `disable_macros` on the checked path fails with the reason `disable_macros needs an internal compile entry point`; the cases `disable_check` and `check_only` pass the options to `Parse`. No case of the pinned commit has the flag.
- **`locale`** is ignored, as by cel-go.

## Calls into the library

| Case | Calls |
| :- | :- |
| checked | `env.Compile<CelValue>(expr)` — the expected type `dyn`, as the cel-go runner checks without one; the result type is `CelProgram<CelValue>.ResultType`; then `program.Evaluate(bindings)`, without a cost limit or a token |
| `disable_check` | internal `env.Parse(expr)`, then internal `env.EvaluateWithoutTypeCheck(parsed, bindings)`; the result type is `dyn`, as for an unchecked expression in cel-go |
| `check_only` | internal `env.Parse(expr)`, then internal `env.Check(parsed)`; the result type is `CheckedExpression.ResultType`; nothing is planned or evaluated |

See [facade.md](facade.md#internal-entry-points). A binding of the kind `value` is converted by `ValueConversion` and added with `CelBindings.Add(name, CelValue)`. A value converts from `cel.expr.Value` to `CelValue` and back for the kinds the library supports — in this part `null_value`, `bool_value`, `int64_value`, `uint64_value`, `double_value`, `string_value`, `bytes_value`, `list_value`, `map_value` and `type_value`, the name of a type value as `type()` gives it (`int`, `list`, `type`), and, with the time part, `object_value` of `google.protobuf.Timestamp` and `google.protobuf.Duration` ([tests](tests.md#the-suite-runner-and-time)) — through the public API only, since `eng/difftest` links the file; a type converts in `TypeConversion`, a file that is not linked: from `CelType` to `cel.expr.Type` for the result type, and from `cel.expr.Type` to `CelType` for the declarations of a case. Other kinds throw `NotImplementedException`.

## The rule

Source: `conformance/conformance_test.go` of cel-go v0.32.0, https://github.com/cel-expr/cel-go/blob/v0.32.0/conformance/conformance_test.go, function `conformanceTest` and the default matcher in `TestConformance`, read on 2026-10-28.

| Step | cel-go v0.32.0 | Lines | The runner |
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

Values are compared as `cel.expr.Value` messages, as `diffValue` does with `cmp.Diff`, `protocmp.Transform()` and sorted map entries (lines 184–190): field by field, scalar fields with Go `==`, the entries of a map in any order. Until the library has values of messages, an `Any` is compared by its `type_url` and the bytes of its `value`: the runner has no classes of the test messages. cel-go expands an `Any` whose type its registry knows and compares the messages, so the runner will expand it too, with the test messages compiled into C# classes like `simple.proto`. Types are compared as `cel.expr.Type` messages (`diffType`, lines 175–182). An exception from the library fails the case, and the reason names its type; a crash of the process is a defect, never a skip entry.

## Skip lists and findings

The cell of a run is the mode (`jit` when `RuntimeFeature.IsDynamicCodeSupported`, else `native-aot`), the operating system (`linux-x64`, `macos`) and the target, fixed at compile time. The cells of the matrix are JIT on `linux-x64` and `macos` and Native AOT on `linux-x64`, for both targets. A skip entry applies when its scope contains the cell.

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

`CelSpecParseTests.EveryExpression_ParsesAndPrintsBack` takes every case of the cel-spec suite from `CelSpecSuite` and parses its expression with the internal `Parse` of an environment of `CelStandardLibrary.V1`. The syntax options are those of the cel-go runner, which gives all files one setup with `OptionalTypes` and clears the macros for `disable_macros`: `SyntaxOptions.Standard` with `OptionalSyntax` on, and `MacroTable.Empty` for a case with `disable_macros`. The macros of extension sets join the options with their sets; until then their calls stay calls. The test asserts, for each case:

- the parse succeeds;
- printing the tree and parsing the text again with the same options gives a tree without a difference by `SyntaxTreeComparer` ([syntax.md](syntax.md#printing-and-tree-equality)).

Until the internal set `block` joins the options, its test macros `cel.iterVar` and `cel.accuVar` stay calls as well, and a comprehension macro whose first argument is such a call is `InvalidMacroCall` (1006), `argument must be a simple name` ([syntax.md](syntax.md#macros)). Eight cases of `block_ext.textproto` write their iteration variables so: `block_ext/basic/multiple_macros_1`, `block_ext/basic/multiple_macros_2`, `block_ext/basic/multiple_macros_3`, `block_ext/basic/nested_macros_1`, `block_ext/basic/nested_macros_2`, `block_ext/basic/adjacent_macros`, `block_ext/basic/macro_shadowed_variable_1` and `block_ext/basic/macro_shadowed_variable_2`. The test keeps their names in a list, and for a case of the list it asserts instead:

- the parse fails, with at least one error, and every error is `InvalidMacroCall` whose message is `invalid call of macro '<macro>': argument must be a simple name`, at the `(` of a macro call whose first argument is a call of `cel.iterVar` or `cel.accuVar`: the text from the offset of the error starts with `(cel.iterVar(` or `(cel.accuVar(`.

A case of the list that parses fails the test with its name and the words `parses now: remove it from the list`; a name of the list that is no case of the suite fails it too. When the set `block` joins the options, its macros expand, these cases parse, and the list is removed.

A failure lists every failed case with its name, the place of its `test` token and the first error or difference. A second test, `CaseCount_IsThatOfThePinnedCommit`, checks that the suite has 2 592 cases, so a lost case shows up; it changes when the pinned commit moves. The skip list does not apply: every expression of the pinned commit parses, except the cases of the list above, which fail exactly as described. The workflow on the master branch of cel-spec does not run this test.

## Synthetic suite

`Synthetic/synthetic.textproto` is a small suite in the cel-spec format, built like the others. `SyntheticSuiteTests` run it with a scripted evaluator, independently of the progress of the library; skip lists are raw string literals inside the tests. A wrong expectation gives `ENG801` at the file and line of the case; an entry for a passing case `ENG802`; an entry for a missing case `ENG804`; each form of an invalid entry `ENG800`; a failure outside the scope of its entry `ENG801`, a pass outside it nothing. Each test checks code, path, line and column and matches the line with `Finding.Pattern`.

## Run on master

`.github/workflows/conformance-master.yml` runs the cel-spec suite on the last commit of the master branch of `google/cel-spec`, so a change in the test data shows up before the pinned commit moves. It runs every night (02:47 UTC) and by hand, on `main` only, in one cell: JIT, `linux-x64`, `net11.0`, on `ubuntu-latest`.

1. The workflow clones the last commit of master without its history and replaces the data of `tests/cel-spec/` in its workspace, file for file as [Data and build](#data-and-build) lists it: `tests/simple/testdata`, the `.proto` files, `doc/langdef.md`, `LICENSE`, and `COMMIT`, which then holds the full SHA of the master commit.
2. It replaces `CelSpec/skips.md` with its table without entries. The skip list belongs to the pinned commit; so a case of master that fails is `ENG801`, and no entry gives `ENG802` or `ENG804`. The regression corpus and its list are not run.
3. It restores in the locked mode, builds `tests/Great.Parts.Cel.Conformance` for `net11.0` and runs only `CelSpecSuiteTests.AllCases_PassOrAreSkipped` of the built test assembly.
4. Nothing is committed or pushed: the replaced files exist only in the workspace of the run.

The result is the summary line of the suite with `master` after `cel-spec`, in the step summary and the log, with the lines of the failed cases (the first 200 in the summary, all in the log):

```text
cel-spec master 1f2e3d4: 2601/2614 passed, 2026-11-05 (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/macros2.textproto(77,3): error ENG801: case 'macros2/…' failed: …
```

The share is counted from all cases of the suite, as for the pinned commit ([Summary line](#summary-line)).

A failed case does not fail the workflow: the test of the suite fails with the findings, and the step reads the summary line from the output. The workflow is red when the restore or the build fails (new test data that protoc cannot encode included), when the process of the run falls before the summary line, or when the line or the report of the run is missing. A case that crashes the process is a defect of the library: it goes to the private report of `SECURITY.md`, not to an issue.

## Native AOT

The project is published under Native AOT and runs as an executable ([tests.md](tests.md#same-tests-under-jit-and-native-aot), [eng-test.md](eng-test.md#test)). The runner uses no reflection: generated messages, embedded resources and the API of the library.
