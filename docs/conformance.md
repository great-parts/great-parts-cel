# Conformance

This page says how much of the CEL specification the library passes. The measure is the conformance suite of [cel-spec](https://github.com/cel-expr/cel-spec): the cases of `tests/simple/testdata` at a pinned commit. Each case is an expression with its variables and the result or the error it must give. The library runs every case on every platform it supports, and a case counts as passed by the rule of the conformance runner of cel-go.

## Result

| Item | Value |
| :- | :- |
| Pinned commit of cel-spec | `8bef98f1caadc2036fe63d5516cf1df65dc4b741` |
| Cases in the suite | 2,592 |
| Cases that pass | 2,591 of 2,592 |
| Date of the run | 2026-11-16 |
| Run | [CI run 43959043833](https://github.com/great-parts/great-parts-cel/actions/runs/43959043833), commit `8fca352` |

The run is the workflow `ci.yml` on the `main` branch. It runs the suite in every cell of its matrix: `net10.0` and `net11.0`, under JIT on Ubuntu and on macOS and under Native AOT on linux-x64. The share is the same in every cell. The runner prints it as one line, for example `cel-spec 8bef98f: 2591/2592 passed, 2026-11-16 (jit, linux-x64, net11.0)`.

The share is counted from all cases of the suite: a case on the skip list that does not pass is a case that did not pass, not a case left out of the count. The pinned commit includes every commit of the master branch of cel-spec that is older than 6 weeks on the day of a release.

## Skip list

A case that fails is allowed only if it is on the [skip list](../tests/Great.Parts.Cel.Conformance/CelSpec/skips.md), with its reason. In release 1.0.0 the list may hold only cases with a defect in the cel-spec test data that no implementation passes under the rule of the cel-go conformance runner. The list has one case:

| Case | Reason | Issue |
| :- | :- | :- |
| `network_ext/ip_type/is_ip_cidr_compile_error` | Defect in the cel-spec test data. The case expects an evaluation error for `isIP(cidr('192.168.0.0/24'))`, but the expression is ill-typed: `isIP` takes a string, so type checking rejects it under any implementation, and the conformance rule counts a type-check error as a failure. | [#5](https://github.com/great-parts/great-parts-cel/issues/5); the defect is also reported to cel-spec as [cel-expr/cel-spec#539](https://github.com/cel-expr/cel-spec/issues/539) |

Type checking reports `found no matching overload for 'isIP' applied to '(net.CIDR)'`, because `isIP` is declared only for `string`. The case sets `disable_check: false`, so the check runs and the expected evaluation error is never reached.

## Cases that are not skips

Two groups of cases pass, but not through the public API alone. They are part of the 2,591.

- **`disable_check`.** 141 of the 2,592 cases set `disable_check`. They are evaluated by an internal entry point without type checking, as the cel-go conformance runner does. The public API always type-checks expressions.
- **`block_ext`.** 37 of the 2,592 cases use the test-only macros `cel.block`, `cel.index`, `cel.iterVar` and `cel.accuVar` of the cel-spec data. They are run with an internal extension set of the runner, as the cel-go conformance runner does. The public API has no such set.

The runner differs from the cel-go runner in one more place: it neither declares nor binds the names `true`, `false`, `null` and `in` from the declarations and bindings of a case. They are keywords, never identifiers, and the library rejects them as variable names. The cases `basic/reserved_const/*` still check what they are for: a literal is never shadowed.

## The master branch of cel-spec

The workflow [`conformance-master.yml`](https://github.com/great-parts/great-parts-cel/actions/workflows/conformance-master.yml) runs the same suite every night on the last commit of the master branch of cel-spec, with an empty skip list, so a change in the test data shows up before the pinned commit moves. The result of each run is in the summary of the run.

| Item | Value |
| :- | :- |
| Commit of cel-spec master | `3dd8162` |
| Cases in the suite | 2,771 |
| Cases that pass | 2,745 of 2,771 |
| Date of the run | 2026-11-16 |
| Run | [run 43869365994](https://github.com/great-parts/great-parts-cel/actions/runs/43869365994) |

Four commits of master are newer than the pinned commit; three are dated 2026-10-27 and one 2026-11-13, all less than 6 weeks before the date of the run, so the pinned commit does not include them. The commit of 2026-11-13 adds a generator and a report of the coverage of the suite, not cases. 26 cases of master do not pass: the case of the skip list above and 25 more. The 25 come from one of the commits of 2026-10-27, which changes the limits of durations and adds cases for literals of `google.protobuf.Duration` and `google.protobuf.Timestamp`:

- 10 cases of `timestamps.textproto`: `toString_duration_max`, `toString_duration_min`, `from_string_max`, `from_string_min`, `add_max_duration_to_min_time`, `subtract_max_duration_from_max_time`, `add_duration_to_duration_max`, `subtract_duration_from_duration_min`, `sub_time_duration_over` and `sub_time_duration_under`. They use durations in the range of `google.protobuf.Duration`, about ±10,000 years. A `duration` of this library is a 64-bit count of nanoseconds, about ±292 years, see [Limits](limits.md#values).
- 15 cases of `wrappers.textproto`: `wrappers/duration/*` (8) and `wrappers/timestamp/*` (7). They write the literals `google.protobuf.Duration{…}` and `google.protobuf.Timestamp{…}`, which this library does not support: they are `NotAType`, and `timestamp(…)` and `duration(…)` stand for them, see [Protobuf messages](protobuf.md#differences-from-cel-go).

## Differences from cel-go

The suite checks the library against the specification. Differences from cel-go that have been analysed with a differential run and are not counted as bugs of this library are listed in [known-differences.md](../tests/Great.Parts.Cel.Conformance/Regression/known-differences.md). Each has a class, `cel-go-bug` or `by-design`, and a reference: a link to the language definition or a minimal case. An entry of the class `cel-go-bug` that has been reported to cel-go also links its upstream issue.
