# Conformance

This page says how much of the CEL specification the library passes. The measure is the conformance suite of [cel-spec](https://github.com/cel-expr/cel-spec): the cases of `tests/simple/testdata` at a pinned commit. Each case is an expression with its variables and the result or the error it must give. The library runs every case on every platform it supports, and a case counts as passed by the rule of the conformance runner of cel-go.

## Result

| Item | Value |
| :- | :- |
| Pinned commit of cel-spec | `3dd8162270505540492e4198a35717c173e1506a` |
| Cases in the suite | 2,771 |
| Cases that pass | 2,755 of 2,771 |
| Date of the run | 2026-11-27 |
| Run | [report of the run of 2026-11-27](https://github.com/great-parts/great-parts-cel/commit/c0123d1a1b0e304561c77618c2671f3e340916ea), directory `conformance-master/` of the branch `reports` |

The pinned commit is also the last commit of the master branch of cel-spec, so the date and the link are those of the report of the nightly run on that branch (see [The master branch of cel-spec](#the-master-branch-of-cel-spec)): the same suite, with the skip list not applied, which gives the same share. The workflow `ci.yml` on the `main` branch runs the suite in every cell of its matrix: `net10.0` and `net11.0`, under JIT on Ubuntu and on macOS and under Native AOT on linux-x64. The share is the same in every cell. The runner prints it as one line, for example `cel-spec 3dd8162: 2755/2771 passed, 2026-11-18 (jit, linux-x64, net11.0)`.

The share is counted from all cases of the suite: a case on the skip list that does not pass is a case that did not pass, not a case left out of the count. The pinned commit includes every commit of the master branch of cel-spec that is older than 6 weeks on the day of a release.

## Skip list

A case that fails is allowed only if it is on the [skip list](../tests/Great.Parts.Cel.Conformance/CelSpec/skips.md), with its reason and, before a release, an issue. An entry is a defect in the cel-spec test data that no implementation passes under the rule of the cel-go conformance runner, or a difference by design of this library. In release 1.0.0 the list could hold only defects in the test data. The list has 16 cases:

| Case | Reason | Issue |
| :- | :- | :- |
| `network_ext/ip_type/is_ip_cidr_compile_error` | Defect in the cel-spec test data. The case expects an evaluation error for `isIP(cidr('192.168.0.0/24'))`, but the expression is ill-typed: `isIP` takes a string, so type checking rejects it under any implementation, and the conformance rule counts a type-check error as a failure. | [#5](https://github.com/great-parts/great-parts-cel/issues/5); the defect is also reported to cel-spec as [cel-expr/cel-spec#539](https://github.com/cel-expr/cel-spec/issues/539) |
| `wrappers/duration/*` (8 cases) and `wrappers/timestamp/*` (7 cases) | By design. The cases write the literals `google.protobuf.Duration{…}` and `google.protobuf.Timestamp{…}`. This library does not create these types as messages: the names are the types of a duration and a timestamp in the standard library, so the literals fail type checking with `NotAType`, and `duration(…)` and `timestamp(…)` stand for them, see [Protobuf messages](protobuf.md#differences-from-cel-go). | [#7](https://github.com/great-parts/great-parts-cel/issues/7) |

Type checking of `isIP(cidr('192.168.0.0/24'))` reports `found no matching overload for 'isIP' applied to '(net.CIDR)'`, because `isIP` is declared only for `string`. The case sets `disable_check: false`, so the check runs and the expected evaluation error is never reached.

## Cases that are not skips

Two groups of cases pass, but not through the public API alone. They are part of the 2,755.

- **`disable_check`.** 141 of the 2,771 cases set `disable_check`. They are evaluated by an internal entry point without type checking, as the cel-go conformance runner does. The public API always type-checks expressions.
- **`block_ext`.** 37 of the 2,771 cases use the test-only macros `cel.block`, `cel.index`, `cel.iterVar` and `cel.accuVar` of the cel-spec data. They are run with an internal extension set of the runner, as the cel-go conformance runner does. The public API has no such set.

The runner differs from the cel-go runner in one more place: it neither declares nor binds the names `true`, `false`, `null` and `in` from the declarations and bindings of a case. They are keywords, never identifiers, and the library rejects them as variable names. The cases `basic/reserved_const/*` still check what they are for: a literal is never shadowed.

## CEL Policy

The package `Great.Parts.Cel.Policy` is measured by the conformance suite of [cel-policy](https://github.com/cel-expr/cel-policy): the 91 tests of the 41 categories of `conformance/testdata` at a pinned commit. A test gives a policy document, the declarations of its environment and either the value the policy must evaluate to or the strings that the errors of its compilation must contain. The runner is the one of the cel-spec suite; it runs on the same platforms and in the same modes, and `browser-wasm` too. A test counts as passed when the policy evaluates to a value equal to the expected one, or, for a test of errors, when the compilation fails and every expected string is a substring of the message of an error.

| Item | Value |
| :- | :- |
| Pinned commit of cel-policy | `8e4cf0378e3f0d3406b537afbcc6f59562652ad6` (2026-08-18) |
| Tests in the suite | 91 |
| Tests that pass | 85 of 91 |
| Date of the run | 2026-11-27 |
| Run | [report of the run of 2026-11-27](https://github.com/great-parts/great-parts-cel/commit/23f20c50f52e56c9d5c7a7cc9f9592531cbb9451), directory `conformance-master-policy/` of the branch `reports` |

The pinned commit is also the last commit of the master branch of cel-policy, so the date and the link are those of the report of the nightly run of the workflow `conformance-master.yml` on that branch: the same suite, with the skip list not applied, which gives the same share.

The runner translates the environment description of cel-go (`config.yaml`) into the JSON form of the library and gives every set of extensions the one version the library has: a version that the suite names, such as `strings` 2, or leaves out is read as that one, because the catalog of the library holds the functions of every version of a set and the result of an evaluation does not depend on the version. The functions of the declarations are written by the runner, as the harness of cel-go writes them. The [skip list](../tests/Great.Parts.Cel.Conformance/CelPolicy/skips.md) has 6 tests, each with its reason:

| Test | Reason |
| :- | :- |
| `compile_errors/aggregate_false_condition/compile/false_condition`, `compile_errors/syntax/compile/syntax_error`, `compile_errors/aggregate_heterogeneous_outputs/compile/incompatible_types` | The suite was written for a newer implementation than the pinned cel-go v0.33.4. The policy is rejected as the test expects, but the text of the error differs from the expected one: the capital letter of `Condition is always false`, the message of the parser of this library instead of that of ANTLR, one error for the outputs that disagree instead of one for each. The oracle v0.33.4 gives the text of the library. |
| `context_pb/valid/good spec`, `context_pb/invalid/bad spec` | The category declares the fields of a message as variables (`context_variable`), which the environment description of the library has no key for. |
| `k8s/invalid/restricted_container` | The policy is a Kubernetes ValidatingAdmissionPolicy, which cel-go reads with a tag handler of its own; the library has none. |

## AIP-160 filters

The package `Great.Parts.Cel.Filtering` translates the filters of [AIP-160](https://google.aip.dev/160) into CEL expressions of an environment. AIP-160 has no official test suite, so the package has no share of conformance to report. It is checked by a corpus of its own, [`tests/filtering/testdata`](https://github.com/great-parts/great-parts-cel/tree/main/tests/filtering/testdata): each case gives a filter, the options of the compiler, and the CEL expression the filter must become, the result on some items, or the error with its code and its place in the filter. The corpus covers every example of the text of AIP-160, each row of the translation, every code that a filter reports, the lexical rules, the limits and each place where the package reads AIP-160 differently. The runner of the cel-spec suite runs it, in the JIT and Native AOT cells of `ci.yml`, and a case that fails is a defect that is fixed, never a case on a list.

AIP-160: own test corpus, 369 cases, run on 2026-11-24, [tests/filtering/testdata](https://github.com/great-parts/great-parts-cel/tree/main/tests/filtering/testdata).

The line is the whole result: a count of cases and the date of the run, with no percentage and no badge. The runner prints it too, for example `aip-160 corpus: 369/369 passed, 2026-11-24 (jit, linux-x64, net11.0)`, and `eng/checks` compares the count of the line with the count of the cases of the corpus.

## The master branch of cel-spec

The workflow [`conformance-master.yml`](https://github.com/great-parts/great-parts-cel/actions/workflows/conformance-master.yml) runs the same suite every night on the last commit of the master branch of cel-spec, with an empty skip list, so a change in the test data shows up before the pinned commit moves. The report of each run is committed to the branch `reports`, in the directory `conformance-master/`: the date and the time of the run in UTC, the commit of cel-spec master, the commit of this repository, the result and the cases that fail, as `report.json` and `report.md`; the last report is also `conformance-master/latest.json`. The summary of the run has the same line.

| Item | Value |
| :- | :- |
| Commit of cel-spec master | `3dd8162` |
| Cases in the suite | 2,771 |
| Cases that pass | 2,755 of 2,771 |
| Date of the run | 2026-11-27 |
| Run | [report of the run of 2026-11-27](https://github.com/great-parts/great-parts-cel/commit/c0123d1a1b0e304561c77618c2671f3e340916ea), directory `conformance-master/` of the branch `reports` |

The run of master does not apply the skip list of the pinned commit. The pinned commit is the last commit of master, so the 16 cases that do not pass are the 16 cases of the skip list above: the case `network_ext/ip_type/is_ip_cidr_compile_error` and the 15 cases of `wrappers.textproto`. While the pinned commit is the last commit of master, the run of master gives the share of the pinned commit without the skip list. The workflow runs the suite of cel-policy the same way, and its reports are in the directory `conformance-master-policy/` of the branch `reports`.

## Differential run against cel-go

The suite checks the library against the specification. A second check compares it with cel-go, the reference implementation of CEL: the program `eng/difftest` generates expressions, evaluates each one with the library and with cel-go, and compares the results and the errors. The run happens on every push to `main` and every night, and a run of at least two hours must pass on the commit of a release before the release is published. A release is also not published while a difference is not analysed yet, or while a difference that is the fault of this library has the highest severity and is open. How the run works is described in [eng-difftest.md](design/eng-difftest.md).

Differences from cel-go that have been analysed and are not counted as bugs of this library are listed in [known-differences.md](../tests/Great.Parts.Cel.Conformance/Regression/known-differences.md). Each has a class, `cel-go-bug` or `by-design`, and a reference: a link to the language definition or a minimal case. An entry of the class `cel-go-bug` that has been reported to cel-go also links its upstream issue.

## Reports of the runs

Every figure on this page has the date of its run, the commit of cel-spec and a link to the report of the run. A report is a file in the branch `reports` of this repository, with the date and the time of the run (UTC), the commit of cel-spec and the commit of this repository. The link of a figure points to a commit of that branch, so it works as long as the figure is published; a link to a run of GitHub Actions would not, because GitHub keeps the logs of the runs for 90 days only. Workflows write the branch with the token of the run, and nothing of it is written to `main`. The result on the master branch of cel-spec is published to the branch automatically after each nightly run, and the result of the speed comparison after each measurement (directory `bench/`). The figures of the three tables above link the commits of the branch `reports` that hold the reports of their runs.
