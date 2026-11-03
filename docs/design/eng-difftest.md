# eng/difftest

`eng/difftest` is the differential run against cel-go. It generates random cases, evaluates each with the library and with the oracle ([eng-oracle.md](eng-oracle.md)), shrinks every divergence to a subexpression that still diverges while its children agree, and reports divergences whose signature is not known yet. A change of the core is checked with it ([core.md](core.md)); the nightly run uses it with a longer budget.

## Project

| Item | Value |
| :- | :- |
| Project | `eng/difftest/Great.Parts.Cel.Eng.Difftest.csproj`, console application, part of the solution |
| Target, mode | `net10.0`, JIT only |
| Packages | `CsCheck` 4.9.1; `Google.Protobuf` 3.36.2; `Grpc.Tools` 2.84.0 with `GrpcServices="None"`, which compiles `cel/expr/value.proto` from `tests/cel-spec/proto` |
| References | `src/Great.Parts.Cel` through its public API only; `eng/findings` |
| Linked sources | `MarkdownTable.cs` and `ValueConversion.cs` of the conformance runner ([conformance.md](conformance.md#layout)): internal types compiled into both assemblies; for the checks of the repository a linked file is one file. `ValueConversion.cs` uses only the public API of the library; the conversion of types to `cel.expr.Type`, declared in `checked.proto`, is in `TypeConversion.cs`, which is not linked |

| Path | Contents |
| :- | :- |
| `Program.cs` | the options, the run, the findings, the journal line, the exit code |
| `BudgetText.cs` | the form of the time of `--budget`; it is also compiled into `eng/run`, which gives the run a time limit from the budget, so both read it alike |
| `Profile/` | `environment.json` — the run manifest; the profile tables; the case generator and the printer of expressions |
| `Sides/` | `ICaseSide` with `LibrarySide` and `OracleConnection`; `ICaseRecorder`, the request `case` of the oracle, which `OracleConnection` implements; the outcome classes |
| `Compare/` | kinds and severities, the equality of `cel.expr.Value`, signatures, minimization, the comparison of zone offsets, values and outcomes as text for messages |
| `Corpus/` | the known signatures, the search of new signatures in a case, the writer of `pending.textproto` |

The namespaces have no cycles (`ARCH001`): `Corpus` uses `Compare` and `Sides`, `Compare` uses `Sides`, and neither `Sides` nor `Compare` uses `Corpus`.

## Invocation

```text
docker compose -f eng/compose.yaml run --rm difftest [--seed <n>] [--budget <time>] [--case <n>] [--numbers-only]
```

| Option | Default | Meaning |
| :- | :- | :- |
| `--seed` | the date in UTC as `yyyyMMdd` | the seed of the run; case `n` depends on the seed and `n` alone |
| `--budget` | `5m` | the time of the whole run, minimization included; the long run before a release gives `2h` |
| `--case` | none | run only case `n` of the seed, to reproduce it: the same seed and `n` give the same minimized case and the same signature ([Outcomes and signatures](#outcomes-and-signatures)) |
| `--numbers-only` | off | print only the journal line and write no file; for the nightly workflow, whose log is public, so no output names a case ([Findings and output](#findings-and-output)) |

| Item | Value |
| :- | :- |
| Image stage | `sdk`, without Go ([eng-test.md](eng-test.md)) |
| Depends on | the service `oracle` from the image `golang`: Compose starts it first, and it ends itself after the session ([eng-oracle.md](eng-oracle.md#container)) |
| Command | `eng/run difftest` builds `eng/difftest`, runs it with the options and returns its exit code unchanged; the nightly workflow runs the same command |
| Time limit | `eng/run` limits the run to the budget × 1.2 + 36 s, counted from the start of the program, so the wait for the oracle (up to a minute) is inside it. A run that is not over by then is killed with its process tree, and the command ends with `difftest: failed: timeout after <N> s in step <step>` and exit code 1, not 2 ([eng-test.md](eng-test.md#time-limits)). The value of `--budget` is read by `eng/run` as by the program; an invalid value gives the limit of the default budget, and the program ends at once with exit code 2 |
| Linked worktree | the second Compose file, as for `eng/test` ([eng-test.md](eng-test.md#linked-worktrees)) |

## A run

1. Read `Profile/environment.json`; a manifest with `greatPartsCel` or `validators` is refused with exit code 2.
2. Library side: `new CelEnvironmentBuilder(CelStandardLibrary.V1)`, `CelEnvironmentJson.Apply` with the manifest, `Build` ([configuration.md](configuration.md#run-manifest-of-the-differential-run)); a build error is exit code 2.
3. Connect to `oracle:7070` within a minute and send the path of the same file; print the `log` lines unchanged. No connection, `failed` or a closed connection is exit code 2; on no connection the message names `docker compose -f eng/compose.yaml logs oracle` ([eng-oracle.md](eng-oracle.md#launcher)). After the handshake, a Go other than the `go` line of `eng/oracle/go.mod` is `ENG821`, and the run stops.
4. Run cases 0, 1, 2, … until the budget, counted from the handshake, is spent: `env.Compile<CelValue>(expr)` and `program.Evaluate(bindings)` on the library, the request `eval` on the oracle. A divergence is minimized and gets a signature. An answer to `eval` or `case` that does not come within 10 seconds is a failed oracle, exit code 2: a case of the profile takes the oracle milliseconds, and the run must not hang on a hung oracle. The steps of the oracle before the handshake have no such limit, since a cold module cache takes minutes; their `log` lines show where they are.
5. Write `pending.textproto` — the cases of new signatures, or its empty form without them — the findings and the journal line.

## Profile of the walking skeleton

The profile is what the generator produces. Variables of the manifest:

| Names | Type |
| :- | :- |
| `i1`, `i2` | `int` |
| `u1` | `uint` |
| `d1` | `double` |
| `b1`, `b2` | `bool` |
| `s1` | `string` |
| `y1` | `bytes` |
| `x1`, `x2` | `dyn` |

| Node | Forms |
| :- | :- |
| `int` literal | decimal or hexadecimal; a negative value as `-` and its magnitude, including `-9223372036854775808` |
| `uint` literal | decimal or hexadecimal with `u` or `U` |
| `double` literal | the shortest decimal that reads back to the same finite value, with or without an exponent, also with a leading `.`; a negative value as `-` and its magnitude |
| `string` literal | single, double and triple quotes; the raw prefix `r` or `R`; the escapes `` \a \b \f \n \r \t \v \\ \' \" \` \? ``, `\x`, octal, and `\u` and `\U` for Unicode scalar values; any Unicode scalar value. A carriage return (U+000D) is never printed as itself, only as an escape, so a literal that holds one is never raw: cel-go turns a carriage return, and a carriage return with a line feed, in the source of a literal into a line feed, while the library keeps the text as the language definition gives it |
| `bytes` literal | the prefix `b` or `B` on the string forms; `\x` and octal escapes give single bytes; no `\u` and `\U` |
| `bool`, `null_type` literals | `true`, `false`, `null` |
| identifier | a variable of the manifest |
| list literal | `[e, …]` with 0–3 elements, sometimes a trailing comma, also `[,]` |
| map literal | `{k: v, …}` with 0–3 entries, sometimes a trailing comma, also `{,}` |
| parentheses | where precedence needs them, and around any node at random |

| Identifier | Function | Signature |
| :- | :- | :- |
| `add_int64` | `_+_` | `(int, int) -> int` |
| `divide_int64` | `_/_` | `(int, int) -> int` |
| `negate_int64` | `-_` | `(int) -> int` |
| `logical_or` | `_\|\|_` | `(bool, bool) -> bool` |

The generator builds an expression for a target type, so every expression is well typed:

- `int`: a literal, `i1`, `i2`, or a call of `_+_`, `_/_` or `-_` on `int` operands. A `dyn` variable is an operand of `_+_` and `_/_` only next to an operand of the static type `int`, and never of `-_`.
- `bool`: a literal, `b1`, `b2`, `x1`, `x2`, or a chain of `||` with 2–4 such operands.
- other types: a literal or a variable of the type. The root, list elements and map values take any type.
- map keys: literals of `int`, `uint`, `bool` and `string`, pairwise unequal by CEL equality, so `1` and `1u` never meet.

The oracle has the whole standard library: `x1 + x2` with two strings is a concatenation there, `-x1` with a `double` a negation. Next to a static `int` its only candidate is the overload of the profile. Repeated keys and keys of other types are known differences from cel-go, covered by registry records.

Each variable is bound to a value of its type, a `dyn` variable to any scalar kind or `null`; one variable in ten stays unbound. Numbers favour boundaries: 0, ±1, the limits of `int` and `uint`, powers of two; a bound `double` is also NaN, ±infinity or -0.0. Limits: depth 6, 40 nodes, 8 code points per string, 8 bytes per `bytes` value, since the walking skeleton has no nesting limit or stack check.

**Extension.** A later part of the library that brings functions, kinds of nodes, variable types or extension sets adds their rows here and to `Profile/`, and their sets to the manifest. The restriction on `dyn` operands ends for a function once the profile holds all its overloads in cel-go. In the end the profile covers the whole standard library and the extension sets except `decimal`, which cel-go lacks, and the internal `block` set.

## Outcomes and signatures

Each side gives a compile error (parse, check; in cel-go also creating the program), an evaluation error or a value. An exception from the library counts as an error of the step it escaped from, and the message names its type; `Compile` parses and checks in one call and does not tell which of the two threw, so an exception from it is a compile error of the stage `check`. Values are equal when their `cel.expr.Value` messages have the same kind and value: lists element by element, maps as sets of entries, doubles by `==` or both NaN.

| Library | cel-go | Kind | Severity |
| :- | :- | :- | :- |
| value | another value | `value-mismatch` | S1 |
| value | error | `we-value` | S1 |
| error | value | `we-error` | S2 |
| compile error | evaluation error, or the reverse | `stage-mismatch` | S2 |

Two errors of one class, or equal values, agree; error messages are not compared. The signature is `<node>(<types>) <kind>` of the root of the minimized case:

| Root | `<node>` | `<types>` |
| :- | :- | :- |
| call | the profile overload whose parameter types fit the argument types; the function name when none fits | the types of the argument values, the receiver first |
| list literal | `list` | the types of the elements |
| map literal | `map` | key and value types, entry by entry: `map(bool, int, bool, int)` |
| other literal | `literal` | its type |
| identifier | `ident` | the type of its value |
| either side fails to parse | `parse` | none |

Types are the CEL type names that `type()` gives, without parameters: `int`, `uint`, `double`, `bool`, `string`, `bytes`, `null_type`, `list`, `map`; an argument that is an error on both sides is `error`. A node without types keeps its parentheses: `parse() we-error`, `list() value-mismatch`.

**Minimization.** A deterministic descent: while a child of the root still diverges with the same bindings, the first such child becomes the case. The children of a call are its arguments, of a list literal its elements, of a map literal the key and the value of each entry, in the order of the source. Minimization evaluates only nodes of the divergent case, with its bindings: it generates no other case and has no time limit, so the same seed and case number give the same minimized case and the same signature. Each node but the root is evaluated at most once, so a case of 40 nodes takes at most 39 evaluations on each side; a run ends the case it started within the budget together with its minimization. The arguments of the final root agree, so their types are defined. The whole tree is not reduced further.

## Known signatures and the corpus

A signature is known when the column `Signature` of the registry of known differences `tests/Great.Parts.Cel.Conformance/Regression/known-differences.md` or of the corpus skip list `Regression/skips.md` has it. The run keeps the first case of each new signature.

These cases go to `tests/Great.Parts.Cel.Conformance/Regression/pending.textproto`, which every run rewrites. The oracle writes them (request `case`) with the answer of cel-go as the expectation, named `pending/new/<seed>-<n>`. The file is part of the corpus, so `eng/test` fails with `ENG801` until each case is fixed or recorded. A compile error of cel-go goes into `eval_error`: a `SimpleTest` cannot expect a compile error.

The file is committed in its empty form, without cases, byte for byte as below, with LF line ends. A run without new signatures writes exactly this form, so the working copy stays clean; a run with new signatures leaves the file changed, and a commit with its cases fails `eng/test` with `ENG801`. A run with `--numbers-only` does not touch the file.

```text
# Written by eng/difftest: the cases of new divergence signatures of the last run. Committed without cases.
name: "pending"
description: "New divergence signatures of the last run of eng/difftest"
```

With cases, the empty form is followed by one section: the line `section {`, the line `  name: "new"`, and for each case, in the order the run found it, the line `  test {`, the text of the `case` answer as it is, and the line `  }`; the line `}` ends the section and the file. The description of a case is its signature. The first case thus starts at line 6, and the place of its `ENG820` is the `test` token, column 3.

An entry of the corpus skip list moves its case from `pending.textproto` to a corpus file, since the next run rewrites `pending.textproto` without it. A registry record and its case enter with the change that implements the behavior of the record: the skip entry carries the class of the record as the verdict and the same signature, and the oracle checks the answer of cel-go again.

## Findings and output

| Code | When | Place |
| :- | :- | :- |
| `ENG820` | a new signature | the `test` token of its case in `pending.textproto` |
| `ENG821` | the Go of the oracle, named by the handshake, differs from the `go` line of `eng/oracle/go.mod` | that line, column 1; the message names both versions |
| `ENG822` | a time zone offset differs from Go | the time zone data file of the repository, `(1,1)`; its path is a constant of `Profile/` |

`ENG822` compares the offsets of every zone of the time zone data file at generated instants: `ICaseSide.ZoneOffset(string zone, DateTimeOffset instant)` gives the offset from UTC as a `TimeSpan`. The message is `time zone offset differs from Go: zone '<zone>' at <instant>: Go <offset>, we <offset>`, with the instant in UTC in ISO 8601 and the offsets as `±hh:mm` or `±hh:mm:ss`. The comparison takes the path of the file as a parameter; the method, the comparison and its test exist from the start. The profile of the walking skeleton has no time zone data file: runs do not compare offsets, and both sides throw `NotSupportedException` from `ZoneOffset`. The part of the library that brings time zones sets the path, implements `ZoneOffset` on both sides, with the request `offset` of the oracle ([eng-oracle.md](eng-oracle.md#protocol)), and turns the comparison on.

```text
tests/Great.Parts.Cel.Conformance/Regression/pending.textproto(6,3): error ENG820: new divergence signature 'add_int64(int, int) we-value' (S1): cel-go returns error 'integer overflow', we return int -9223372036854775807; seed 20261104, case 1207
difftest seed=20261104 commit=9f8e7d6c oracle=0.32.0 cases=412345 signatures new=1 (S1=1, S2=0) known=0 new-cases=1207
```

In the message of `ENG820` an outcome is one line: a value as its type and its value, `int -9223372036854775807`; an evaluation error as `error '<message>'`; a compile error as `<stage> error '<message>'`, with the stage `parse`, `check` or `program`. A message is cut at its first line break.

The journal line comes last; `commit` is the start of `HEAD`. `new-cases` lists the numbers of the kept cases, separated by commas without spaces, and stays empty without new signatures: `new=0 (S1=0, S2=0) known=0 new-cases=`. Exit code: 0 without new signatures, 1 with a finding with `error`, 2 for an invalid invocation, a refused manifest, a failed oracle, its steps included, or a minimized case that agrees when it is run again, since a side is then not deterministic. The messages of exit code 2 go to standard error, the rest to standard output. `eng/run` passes the code on; its failed `dotnet build` of `eng/difftest` is 1 ([eng-test.md](eng-test.md#engrun)).

With `--numbers-only` standard output has the journal line alone: no `log` line and no finding, `ENG821` included. The journal line comes after `ENG821` too, with `cases=0` and exit code 1, a pair that no other finding gives. A message of exit code 2 quotes no case and no line or answer of the oracle, since those can carry expressions and values: a minimized case that agrees again is named by the seed and its number, which `--case` reproduces.

## Nightly run

`.github/workflows/difftest-nightly.yml` runs `eng/difftest` every night and, by hand, for the long run before a release. Both run the command of the container check, so the run on GitHub is the run on a developer machine; the oracle is the service `oracle` of Compose, which has Go, so the workflow has no Go setup.

| Item | Value |
| :- | :- |
| Triggers | `schedule` at 01:17 UTC every night; `workflow_dispatch` with the inputs `budget` and `seed` |
| Branch | `main` only: a manual run from another branch is skipped |
| Runner | `ubuntu-latest` |
| Command | `docker compose -f eng/compose.yaml run --rm -T difftest --numbers-only --budget <budget> [--seed <n>]` |
| Checkout | the whole history, so `eng/run` reads the commit and the state of the tree |
| Budget of the night | `20m`. With the image build and a cold module cache of Go the job takes 20 to 30 minutes; its time limit is 60 minutes, which holds the limit of `eng/run` for the run (24.6 minutes, [Invocation](#invocation)) |
| Budget of a manual run | the input `budget`, `2h` by default: the long run before a release is 2 hours or more. The job limit is 360 minutes, the longest a job may take. The input is a whole number and `s`, `m` or `h`, as `--budget`; another form fails the run with exit code 2 before it starts |
| Seed | the input `seed`, a whole number; without it the date in UTC as `yyyyMMdd`, as locally |
| Concurrency | one run at a time; a started run is not cancelled |

The log of the workflow is public, and a divergence can be an input that crashes a parser. So the run has `--numbers-only`: standard output holds the journal line alone ([Findings and output](#findings-and-output)), and the step summary of the run holds that line and the budget, nothing else. A new signature shows in the line as a count and the numbers of its cases (`new=2 (S1=1, S2=1) known=3 new-cases=1207,88391`); the case itself is reproduced on a machine of the maintainers with the same seed and `--case <n>`, and the text of a case reaches an issue only after the divergence is fixed.

The result of the workflow is the exit code of `eng/difftest`, passed through `eng/run` unchanged: 0 without new signatures is green; 1 (a new signature, a failed build or a step that ran out of time) and 2 (a failed oracle or an invalid input) are red. A run that ends with 0 and no journal line is red too. The release takes a green manual run, 2 hours or more, on the commit of the tag.

## Tests

`eng/tests/Difftest/` ([eng-tests.md](eng-tests.md)) uses fake sides with given outcomes, without Go or a working library:

- `SignatureTests`: another value from the oracle gives `ENG820` with the signature in the form above, another zone offset `ENG822` at the path the test gives; one case per kind; a known signature and agreeing outcomes give nothing; minimization takes the first divergent child, keeps a case whose children agree, evaluates only nodes of the case with its bindings, and passes on the failure of a side;
- `GeneratorTests`: for fixed seeds, no `dyn` operand of `-_`, no repeated or wrong-typed map key, the limits hold; the forms of the profile occur, among them a negative `double` literal, `[,]`, `{,}`, a map with a trailing comma and a bound `double` that is NaN, ±infinity or -0.0, while no `double` literal is other than finite;
- `ManifestTests`: a manifest with `greatPartsCel` or `validators` is refused;
- `OracleConnectionTests`, with a fake oracle on a loopback socket: `log` lines pass unchanged; `failed`, a closed connection and no listener give exit code 2, and the message on no listener names the `logs` command; another Go in the handshake gives `ENG821`; an answer that does not come within the limit, which the test sets, gives exit code 2; with `--numbers-only` standard output has the journal line alone, and a message of exit code 2 quotes no line of the oracle; two runs of the same seed and `--case` send the same requests and give the same minimized case and signature.

Each finding line matches `Finding.Pattern`.
