# Test projects

The tests of the library: `tests/Great.Parts.Cel.Tests` and `tests/Great.Parts.Cel.Properties`. The rules on requirement traits, on the `Examples/` directories and on the test framework hold for every test project of the repository, including the suite runner `tests/Great.Parts.Cel.Conformance` and `eng/tests` ([eng-tests.md](eng-tests.md)).

## Projects

| Project | Targets | Modes | Packages | Contents |
| :- | :- | :- | :- | :- |
| `tests/Great.Parts.Cel.Tests` | `net10.0`, `net11.0` | JIT, Native AOT | `xunit.v3.aot` 4.0.2 | unit tests, one directory per namespace of the library; `Examples/` |
| `tests/Great.Parts.Cel.Properties` | `net10.0`, `net11.0` | JIT | `xunit.v3.aot` 4.0.2, `CsCheck` 4.9.1 | properties without an oracle |
| `tests/Great.Parts.Cel.Conformance` | `net10.0`, `net11.0` | JIT, Native AOT | see the description of the suite runner | the cel-spec suite and the regression corpus |
| `eng/tests` | `net10.0` | JIT | see [eng-tests.md](eng-tests.md) | tests of the own checks |

Common settings:

- `dotnet test` runs on the test platform that `global.json` selects; every project uses the same version of `xunit.v3.aot`, under JIT as well.
- Tests run in two ways: the command `eng/test` runs the xUnit.net runner inside every test module — `dotnet <module>.dll` under JIT once, and for the two published projects also the executable under Native AOT — each with `-result-xml`, so that `test-count` compares two reports of the same kind ([eng-test.md](eng-test.md)); a narrow run in the container, `dotnet test` on Microsoft Testing Platform, is a tool of the developer and is not part of the check.
- The library grants `InternalsVisibleTo` to `Great.Parts.Cel.Tests`, `Great.Parts.Cel.Properties` and `Great.Parts.Cel.Conformance` only.
- Test projects set `IsPackable=false` and turn off, each with a reason, `CS1591` — tests have no XML documentation — and `CA1707` — test method names are `<Situation>_<Expectation>`.
- `ImplicitUsings` is off, as in the library: every `using` is written out, and `IDE0005` rejects the unused ones.
- Where the analyzers of the test framework require a token, a test passes `TestContext.Current.CancellationToken`; a test of cancellation or of its absence passes its own `CancellationTokenSource` or `CancellationToken.None`.
- Tests of the code for `net11.0` only, such as `decimal`, are compiled under `#if NET11_0_OR_GREATER`.
- A test class is named `<Subject>Tests` and lives in the directory of its subject, for example `tests/Great.Parts.Cel.Tests/Values/CelValueEqualityTests.cs`. A test method is named `<Situation>_<Expectation>`, for example `NaN_NotEqualToItself`.

## Requirement trait

A test that checks a requirement of the library names it with the xUnit trait `Requirement`:

```text
[Fact]
[Trait("Requirement", "<code>")]
public void <Situation>_<Expectation>()
```

- **Name.** The attribute is the standard `Xunit.TraitAttribute`; the trait name is always `Requirement`. The value is visible in the reports and selects tests with `--filter-trait "Requirement=<code>"`.
- **Argument.** One requirement code per attribute, a string written exactly as in the requirement catalog: `<category>-<number>`, where the category is `F`, `U`, `R`, `P`, `S` or `+R` and the number has no leading zeros. A requirement of the whole system has the prefix `system-`: `system-<category>-<number>`.
- **Several codes.** One attribute per code:

```text
[Trait("Requirement", "<code 1>")]
[Trait("Requirement", "<code 2>")]
```

- **Place.** On a test method, or on a test class when every test of the class checks the same requirement.
- **Only requirement codes, only in the trait.** The trait holds a requirement code and nothing else: no code of an architecture decision record, no other identifier. In a test, a requirement code appears only in this trait, not in names or comments. The rest of the repository — the library code and its XML documentation, documentation, messages, diagnostics, output of checks and data files — has no requirement codes at all.

## Examples/

`tests/<project>/Examples/` holds only examples of behavior: tests that were written from the specification of the library and state the behavior it promises. Developer tests are never placed there.

- An example is the specification of the behavior. When an example fails, the library is wrong; a change of the library does not change an example. An example changes only together with the behavior it states.
- Every example names what it checks: a requirement with the `Requirement` trait, or a row of a published table with the value it asserts — for an error code, the `CelErrorCode` member.
- Examples may use internal entry points of the library through `InternalsVisibleTo`. They do not use helpers of developer tests, so a change of those helpers does not change an example.
- The namespace follows the directory: `Great.Parts.Cel.Tests.Examples` and nested namespaces by subject.
- A long table of example data is one string constant rather than many `InlineData` attributes: the duplicate check counts tests too ([eng-checks-source.md](eng-checks-source.md#duplicates)).
- Examples run with all other tests of the project, under JIT and, in projects published under Native AOT, under Native AOT.

## Property tests and the seed

The verdict of a property is deterministic: the same code gives the same verdict on every machine and in every run, so the contribution of the properties to the mutation score repeats.

- A property with a small domain checks the whole domain, without a generator. `CelErrorCodeProperties` checks every member of `CelErrorCode`.
- A property with a larger domain builds case `i` from the pair of the seed and `i`, with a loop of its own over `Gen<T>.Generate` of CsCheck 4.9.1. The seed and the number of cases are set in the code; the loop reads no environment variable `CsCheck_*` and has no time limit. The loop is written together with the first property that needs it.
- `Gen<T>.Sample` does not give the verdict: a seed passed to `Sample` fixes only its first iteration, and the other iterations are generated from a seed taken from the clock and the thread. A property calls `Sample` only after its loop has found a failing case, to minimize that case. The smallest case goes into the failure message and does not change the verdict.
- A failing case is reproduced from the seed and its number and becomes a unit test with fixed input.
- Properties do no random search of their own; for the core, `eng/difftest` does it, with a seed taken from the date ([eng-difftest.md](eng-difftest.md)).
- Generated expressions of the core come from `Generators/TypedExpressions.cs` of the project: for a target CEL type it builds a well-typed expression over the standard library of this part — literals, the variables of a fixed environment (one of each type `int`, `uint`, `double`, `bool`, `string`, `bytes`, `list(int)`, `list(dyn)`, `map(string, int)`, `map(int, dyn)`, `type` and two `dyn`), calls, `&&`, `||`, `?:`, indexes, selections and `has()` on maps, the standard macros — within depth 8 and 60 nodes, and bindings of the declared types, a `dyn` variable bound to any kind, one variable in ten unbound. Map keys of a literal are pairwise unequal and of the four key types. The number of cases of each property is set so that the properties of the project together take at most 30 s per target in `eng/test`.

## Properties of the parser and the checker

`tests/Great.Parts.Cel.Properties/Syntax/` and `Checking/` hold two classes each. They call the internal `CelEnvironment.Parse` and `CelEnvironment.Check` ([facade.md](facade.md#internal-entry-points)), so the compilation boundary is part of what they check. The parse of every expression of the cel-spec suite is a test of the conformance runner ([conformance.md](conformance.md#parse-test-of-the-cel-spec-suite)).

- `ParserProperties`, each with the loop of the previous section and the number of cases set in the code:
  - `NeverThrows`: generated strings up to 64 KB — random characters, random tokens, grammatical texts, among them nesting deeper than 250 and more than 100 syntax errors. `Parse` returns; no exception escapes; no error is `InternalError`; every error lies within the text. A generator knows the limit its text goes over, and the result then has `ExpressionTooLong`, `NestingTooDeep` or `TooManyErrors` with its position.
  - `RoundTrip`: grammatical texts within the limits, from the whole grammar with the optional syntax and the standard macros, parse; printing and parsing again gives a tree without a difference by `SyntaxTreeComparer` ([syntax.md](syntax.md#printing-and-tree-equality)).
  - The failure message names the property, the seed and the number of the case, then the reason and the smallest case.
- `WorstInputTests`, one test per input, each at most 65 536 UTF-8 bytes: the deepest nesting allowed for each kind of nesting; chains of each binary level, `&&` and `||` included, filling 64 KB; a text with more than 100 syntax errors; 64 KB of garbage from a fixed seed. A test parses its input once to warm up and once under `Stopwatch`, asserts that no error is `InternalError` and writes `<input>: <seconds> s` to the test output.

- `CheckerProperties` (type checking), in an environment of `CelStandardLibrary.V1` with the variables of the generator:
  - `NeverThrows`: the grammatical texts of `ParserProperties` that parse, and texts of the generator with one node replaced by a node of another type. `Check` returns; no exception escapes; no error is `InternalError`; every error lies within the text.
  - `WellTyped`: the expressions of the generator check without errors, and their `ResultType` is assignable to the target type ([types.md](types.md)).
- `CheckerWorstInputTests` (type checking), one test per input of at most 65 536 UTF-8 bytes, timed like `WorstInputTests` for parse and check together: the inputs of `WorstInputTests` that parse; list literals filling 64 KB whose elements are `x + x` with `x` of type `dyn` (every candidate of `_+_` per call), `[]` (a fresh type variable per element, all joined), alternating `1`, `1u`, `1.0`, `'a'` (joined to `dyn`), and `1 + 'a'` (one `NoMatchingOverload` per element); nested list literals 250 deep; a chain of 250 `_+_` over `[]`; comprehensions nested as deep as the parser allows; a selection chain of 250 names `a.a.….a` in a container of 250 segments `a.a.….a`, so that every prefix of the chain meets every candidate of the container.

**Where the time limit blocks.** A time over 1 s fails a worst-input test, with the message `<input>: 1.42 s > 1 s`, only on the reference machine: a standard GitHub-hosted Linux x64 runner, recognized by the variables that GitHub Actions sets on every runner — `GITHUB_ACTIONS` is `true`, `RUNNER_OS` is `Linux`, `RUNNER_ARCH` is `X64` and `RUNNER_ENVIRONMENT` is `github-hosted`. In `ci.yml` these are the JIT cells on `ubuntu-latest`: the project runs only under JIT, and the Native AOT job runs only the published projects. Everywhere else — `eng/test` and `eng/mutate` in their containers, the cells on `macos-latest`, a machine of a developer — the time is only written, so there the verdict does not depend on time and repeats. `ci.yml` sets no variable of its own for this.

## Properties of evaluation

The failure message of each property names the property, the seed and the number of the case, then the reason and the smallest case, as for the parser.

- **`Evaluation/CheckedEqualsUncheckedTests.SameResult`**. For each case of the generator: `Compile<CelValue>` and `Evaluate`, and the internal `Parse` and `EvaluateWithoutTypeCheck` of the same text and bindings. Both give a value, or both an error; two values are equal by a structural comparison of the test — the same kind, lists element by element, maps as sets of entries, NaN equal to NaN, type values by `Name`. Error codes are written into the failure message, not compared.
- **`Facade/ConcurrencyTests.ParallelEqualsSequential`**. One environment of the generator, with two application functions — a typed delegate and a `CelFunctionImplementation` — and 64 texts with 16 bindings each. The reference results are computed first on one thread. Then 64 threads, created with `new Thread` and started together by a `Barrier`, each run every item of four scenarios in an order of their own: `program.Evaluate` of one program per text on its bindings; `env.Compile<CelValue>` of the texts and the evaluation of each result on its first bindings; `env.Evaluate<CelValue>` of text and bindings; `GetOrCompile<CelValue>` and `Evaluate<CelValue>` of one `CelCompilationCache` whose capacities hold 16 entries, so that eviction runs under contention. Half of the evaluations have a cost limit. Each result must equal its reference: values by the comparison of `CheckedEqualsUncheckedTests`, compile results by `ResultType` and by the code, stage, message and location of each error, evaluation errors by code, message and location. An exception of a thread fails the property with the thread and the item.
- **`Evaluation/CostTests.AllocationWithinCost`**. For a program and bindings, with the limit `ulong.MaxValue` the property measures the bytes that `GC.GetAllocatedBytesForCurrentThread()` counts beyond the frame and the units charged: `Plan` gives the two steps of `Plan.Evaluate` as internal members — `Frame CreateFrame(bindings, costLimit, cancellationToken)` and `CelValue Run(Frame frame)` — and `Frame.ChargedUnits` is the limit minus the remaining units. Bytes are counted from after `CreateFrame` to the end of `Run`, as the smallest of three evaluations after the first, so that one-time initialization is not counted and neither are the few KB that the JIT runtime allocates on the test thread when it moves methods between tiers (2 of 20 runs; none with `DOTNET_TieredCompilation=0` and none under Native AOT): the program allocates its bytes in every evaluation, the runtime noise only in some. Bytes ≤ units × B with B = 64, a constant of the test; where the evaluation stops at the limit, the bytes of the stop itself are added: S = 2 048, a constant of the test (`StopBytes`), above the 0.8–1.4 KB that the `CostLimitException` with its stack trace and the error value take. Inputs: the cases of the generator, and one row test per row of the cost table of this part whose memory grows with the data — list and map literals, `add_list`, `add_string`, `add_bytes`, the conversions to and from text, `map` and `filter`, `contains` with a pattern over 256 code units — each on data of sizes 0, 1, 10, 100 and 1 000, bound as variables of type `dyn` where a literal would be long, so that the check of a binding ([values](values.md), "Fits a type") charges nothing and does not lower the bytes per unit of the row. The literals have literal elements, keys and values — `[1, 1, …]` and `{0: 1, 1: 1, …}` — which cost 0, so the row charges only the literal itself, the strictest case. Two more rows measure error values: `1 / 0`, an operation of 1 unit that fails, the same on every size, and `int(x)` with `x` a text of the size that does not parse, `a` repeated, whose message holds the text. Three rows measure the copies of arguments of an application function ([evaluation](evaluation.md), "Application functions"): `f(x)` with `x` of type `dyn` and a typed delegate that returns the count or the length without reading the elements — `Func<IReadOnlyList<int>, long>` for `list(int)`, `Func<IReadOnlyDictionary<string, int>, long>` for `map(string, int)`, `Func<byte[], long>` for `bytes`. The keys of the map are the strings `"0"`, `"1"`, …, which the copy charges ⌈L/10⌉ each. Since the check of the argument charges n without memory, `Values/ConversionRulesTests` also measures the copy alone, its bytes against its own units, on 0, 1, 2, 8, 9, 100 and 1 000 elements or pairs and L of 0, 1, 10 and 1 000. A row test writes `<row>: <max bytes per unit> bytes/unit` to the test output; these numbers are the calibration of the rows of this part (stage 0, item 6), and the task that measures puts them into its report. Another row stops the loop by a limit: `map` and `filter` over a `dyn` list of 1 000 elements with the limits 2, 3, 10 and 100, bytes ≤ B per unit charged at the stop plus S. A row that does not fit B is a question to the architect: its formula or its allocation changes.
- **`Library/SubstringSearchLinearityTests.StepsBounded`** (`contains`). It calls `SubstringSearch.IndexOf(text, pattern, ref steps)` ([library.md](library.md#substring-search)) and asserts steps ≤ 4 · (n + m), n and m the lengths in UTF-16 code units, and the index equal to that of a naive search written in the test. Inputs: text `(ab)^N` with pattern `(ab)^k` and another last unit; `a^n` with `a^(m−1)b`; a pattern equal to a prefix of the text; a pattern longer than 256 code units; an empty pattern; a pattern longer than the text; surrogate pairs; random strings over alphabets of 2 and 3 code units. n reaches 1 000 000. No time is measured.

## Culture of the process

`Environment/CultureFitnessTests` in `Great.Parts.Cel.Tests` and in `Great.Parts.Cel.Conformance` run with the culture `tr-TR`: decimal comma, dotted and dotless `i`. A test sets `CultureInfo.CurrentCulture` and `CurrentUICulture` of its own thread and restores them in `finally`; the default culture of the process is not touched, since other tests run in parallel. The expectations are constants, the same as in the invariant culture.

- `Great.Parts.Cel.Tests`: a theory over the culture name with cases for the conversions between numbers and text in both directions (`string(1.5)` is `"1.5"`, `double("1.5")` is `1.5`, `string(-1)`, `string(1e21)`), the ordering of strings by code points (`'a' < 'B'` is `false`, `'I' < 'i'` is `true`), `size`, and the message of an error with a number or a type in its text: `IndexOutOfRange`, `ConversionRange`, `CostLimitExceeded`, `ExpectedTypeMismatch`.
- `Great.Parts.Cel.Conformance`: the cel-spec suite and the regression corpus run under the culture with their skip lists, as their suite tests do, and fail on the same findings. The test writes no summary line.

**Invariant globalization.** The published run sets `InvariantGlobalization=true`, where `tr-TR` cannot be created. The test then runs with the invariant culture and asserts the same constants: it neither skips nor changes its name or data, so the JIT and Native AOT runs have the same tests. The mode is read once from `AppContext.TryGetSwitch("System.Globalization.Invariant", out bool on)`; without the switch the culture `tr-TR` must exist, so a run that should use ICU and cannot fails instead of passing in the invariant culture. The time zone of the process joins these tests with time.

## Where the classes live

| Property | Project | Class |
| :- | :- | :- |
| culture of the process | `Great.Parts.Cel.Tests`, `Great.Parts.Cel.Conformance` | `Environment/CultureFitnessTests` |
| parser and type-checking properties | `Great.Parts.Cel.Properties` | `Syntax/ParserProperties`, `Checking/CheckerProperties` |
| worst input | `Great.Parts.Cel.Properties` | `Syntax/WorstInputTests`, `Checking/CheckerWorstInputTests` |
| checked equals unchecked | `Great.Parts.Cel.Properties` | `Evaluation/CheckedEqualsUncheckedTests` |
| parallel equals sequential | `Great.Parts.Cel.Properties` | `Facade/ConcurrencyTests` |
| cost and memory | `Great.Parts.Cel.Properties` | `Evaluation/CostTests` |
| substring search linearity | `Great.Parts.Cel.Properties` | `Library/SubstringSearchLinearityTests` |

The namespaces follow the directories: `Great.Parts.Cel.Properties.Checking` and so on.

## Properties of the parser

`tests/Great.Parts.Cel.Properties/Syntax/` holds two classes. Both call the internal `CelEnvironment.Parse` of an environment of `CelStandardLibrary.V1`, so the compilation boundary is part of what they check; checking joins them with complete type checking. The parse of every expression of the cel-spec suite is a test of the conformance runner ([conformance.md](conformance.md#parse-test-of-the-cel-spec-suite)).

- `ParserProperties`, each with the loop of the previous section and the number of cases set in the code:
  - `NeverThrows`: generated strings up to 64 KB — random characters, random tokens, grammatical texts, among them nesting deeper than 250 and more than 100 syntax errors. `Parse` returns; no exception escapes; no error is `InternalError`; every error lies within the text. A generator knows the limit its text goes over, and the result then has `ExpressionTooLong`, `NestingTooDeep` or `TooManyErrors` with its position.
  - `RoundTrip`: grammatical texts within the limits, from the whole grammar with the optional syntax and the standard macros, parse; printing and parsing again gives a tree without a difference by `SyntaxTreeComparer` ([syntax.md](syntax.md#printing-and-tree-equality)).
  - The failure message names the property, the seed and the number of the case, then the reason and the smallest case.
- `WorstInputTests`, one test per input, each at most 65 536 UTF-8 bytes: the deepest nesting allowed for each kind of nesting; chains of each binary level, `&&` and `||` included, filling 64 KB; a text with more than 100 syntax errors; 64 KB of garbage from a fixed seed. A test parses its input once to warm up and once under `Stopwatch`, asserts that no error is `InternalError` and writes `<input>: <seconds> s` to the test output.

**Where the time limit blocks.** A time over 1 s fails a worst-input test, with the message `<input>: 1.42 s > 1 s`, only on the reference machine: a standard GitHub-hosted Linux x64 runner, recognized by the variables that GitHub Actions sets on every runner — `GITHUB_ACTIONS` is `true`, `RUNNER_OS` is `Linux`, `RUNNER_ARCH` is `X64` and `RUNNER_ENVIRONMENT` is `github-hosted`. In `ci.yml` these are the JIT cells on `ubuntu-latest`: the project runs only under JIT, and the Native AOT job runs only the published projects. Everywhere else — `eng/test` and `eng/mutate` in their containers, the cells on `macos-latest`, a machine of a developer — the time is only written, so there the verdict does not depend on time and repeats. `ci.yml` sets no variable of its own for this.

## Same tests under JIT and Native AOT

`Great.Parts.Cel.Tests` and `Great.Parts.Cel.Conformance` are published under Native AOT and run as executables: in `eng/test` and in CI, on linux-x64, for both targets ([eng-test.md](eng-test.md)). `eng/checks test-count` compares the tests of each published run with the JIT run of the same commit, operating system and target ([eng-checks-artifacts.md](eng-checks-artifacts.md)). The other test projects run only under JIT.

Rules that keep the two sets equal:

- No generic test methods: the test framework does not run them under Native AOT.
- Test data are known when the project is built: inline data and static members, not data found by reflection at run time.
- A test does not skip itself, change its name or its data depending on the mode. `#if` around tests depends only on the target framework. The culture tests choose their culture by the globalization mode and keep their names, data and expectations ([Culture of the process](#culture-of-the-process)).
- The display name of a theory case does not depend on the culture of the process: the JIT run uses ICU, the published run the invariant culture.

Project settings:

- `PublishAot=true` and `RuntimeIdentifiers` with `linux-x64` are in the project files of the two projects. Restore is then the same for JIT and Native AOT: the publish runs with `--no-restore` after a restore without a runtime identifier and changes no lock file, neither of the project nor of the projects it references.
- `UseDefaultPublishRuntimeIdentifier=false` is in the same project files, with its reason: with `PublishAot=true` the SDK takes the runtime identifier of the host as the default publish identifier and appends it to `RuntimeIdentifiers`. On a Linux x64 host that is `linux-x64`, already in the list; on a macOS runner it is `osx-arm64`, the restore then differs from the lock file and a locked restore fails with `NU1004`. The list stays `linux-x64` on every host, and the publish names `linux-x64` with `-r`.
- `DynamicCodeSupport=true` is in the same project files for every build except the publish. With `PublishAot=true` the targets of the Native AOT compiler set `DynamicCodeSupport=false` for every build, and the SDK writes it into the runtime configuration: the JIT run would then run without dynamic code, as the published one, and the conformance runner would name its cell `native-aot` ([conformance.md](conformance.md#skip-lists-and-findings)). The publish keeps `false`.
- `PublishDocumentationFile=false` is in the same project files, with its reason: the Native AOT publish of the SDK looks for the XML documentation file in the runtime output, where the build does not put it, and fails with `MSB3030`. A test executable needs no documentation file; `GenerateDocumentationFile` stays `true`.
- `InvariantGlobalization=true` is given only on the `dotnet publish` command line. The JIT run keeps ICU, the published run uses the invariant mode, and the same expectations hold in both.

Under Native AOT the stack trace of a failed test is cut down: the console and the `<stack-trace>` element of the report show the name of the test and the message, and for a thrown exception the method and an offset, but no file and line. The file and line are in the report of the run: the attributes `source-file` and `source-line` of its `<test>` element, which the test framework records at build time — the file and the line of the `[Fact]` or `[Theory]` attribute, with the absolute path as the build saw it. If a report of a published run has no source for a test, the file and line are found by the test name in the PDB of the JIT build of the same commit, as `test-count` does.
