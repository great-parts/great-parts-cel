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

## Profile

The profile is what the generator produces: the standard library version 1 without time and `matches`, over the variables of the manifest. The reader of the manifest takes simple types and `dyn` only ([configuration.md](configuration.md)), so lists, maps and type values come from literals, calls and `dyn` variables; typed collections and the container join when it takes them. The variables: `i1`, `i2` of type `int`, `u1` `uint`, `d1` `double`, `b1`, `b2` `bool`, `s1` `string`, `y1` `bytes`, `x1`, `x2` `dyn`.

| Node | Forms |
| :- | :- |
| `int` literal | decimal or hexadecimal; a negative value as `-` and its magnitude, including `-9223372036854775808` |
| `uint` literal | decimal or hexadecimal with `u` or `U` |
| `double` literal | the shortest decimal that reads back to the same finite value, with or without an exponent, also with a leading `.`; a negative value as `-` and its magnitude |
| `string` literal | single, double and triple quotes; the raw prefix `r` or `R`; the escapes `` \a \b \f \n \r \t \v \\ \' \" \` \? ``, `\x`, octal, and `\u` and `\U` for Unicode scalar values; any Unicode scalar value. A carriage return (U+000D) is printed only as an escape, so a literal that holds one is never raw: cel-go turns CR and CRLF in the source of a literal into LF, the library keeps the text |
| `bytes` literal | the prefix `b` or `B` on the string forms; `\x` and octal escapes give single bytes; no `\u` and `\U` |
| `bool`, `null_type` literals | `true`, `false`, `null` |
| identifier | a variable of the manifest; a type identifier `bool`, `bytes`, `double`, `int`, `uint`, `string`, `null_type`, `type`, `list`, `map`; the iteration variable `v` or `w` inside its macro |
| list literal | `[e, …]` with 0–3 elements, sometimes a trailing comma, also `[,]` |
| map literal | `{k: v, …}` with 0–3 entries, sometimes a trailing comma, also `{,}` |
| selection, presence | `e.f` and `has(e.f)` on an operand of type `map` or `dyn`; `f` is `a`, `b` or `f`, which map literals also take as string keys |
| call | `!_`, `-_`, `_&&_`, `_\|\|_` (chains of 2–4 operands), `_?_:_`, `_==_`, `_!=_`, `_<_`, `_<=_`, `_>_`, `_>=_`, `_+_`, `_-_`, `_*_`, `_/_`, `_%_`, `_[_]`, `@in`, `size` global and on a receiver, `contains`, `startsWith`, `endsWith`, `type`, `dyn`, `int`, `uint`, `double`, `string`, `bytes`, `bool` |
| macro | `all`, `exists`, `exists_one` on an operand of type `list`, `map` or `dyn`; `map` (2 and 3 arguments) and `filter` on a range of fixed order: a list literal, also in `dyn()`, or a map literal of 0–1 entries, which is also in `dyn()` when empty (cel-go iterates a map in no fixed order); the iteration variable has a static type that the generator knows and that is not `type`, else the range is in `dyn()`, and a macro whose range has no room for `dyn()` is not built: cel-go plans a variable of the static type `type` as a type name (`[int].map(v, v)`: `reference to undefined type: v`), and the variable over an empty literal or another range of unknown element type has a free type variable that the body binds to `type` (`{}.filter(v, v == int)`: the same error); at most two macros nested |
| parentheses | where precedence needs them, and around any node at random |

The generator builds an expression for a target type — any type of the profile, `dyn` included — so every expression type checks; a `dyn` operand may stand wherever a function takes one. The overloads of the oracle that the profile lacks take time values, which no case makes, and `matches` is never called, so both sides choose among the same overloads. A string argument of `int`, `uint`, `double` or `bool` is, half the time, a number or a `bool` in the text forms of the standard library page, with signs, `_`, hexadecimal, `inf`, `nan` and spaces.

Map keys are literals of `int`, `uint`, `bool` and `string`, pairwise unequal by CEL equality: repeated keys and keys of other types are known differences, covered by registry records. A presence test or a selection on a value that is not a map is an error on both sides, since the oracle has `EnableErrorOnBadPresenceTest` as the cel-go conformance runner does.

A variable is bound to a value of its type, a `dyn` one to a scalar, `null`, a list of 0–3 scalars or a map of 0–3 entries with keys as in literals; one in ten stays unbound. Numbers favour boundaries: 0, ±1, the limits of `int` and `uint`, powers of two; a bound `double` is also NaN, ±infinity or -0.0, and travels to the oracle, also inside a list or a map, as its shortest text, read back bit for bit. Limits: depth 6, 40 nodes, 8 code points per string, 8 bytes per `bytes` value; no cost limit and no token.

**Extension.** A later part of the library that brings functions, kinds of nodes, variable types or extension sets adds their rows here and to `Profile/`, and their sets to the manifest. A `dyn` operand is restricted for a function while the oracle has an overload of it that the profile lacks and a value of the profile reaches. In the end the profile covers the whole standard library and the extension sets except `decimal`, which cel-go lacks, and the internal `block` set.

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
| call, `_[_]`, `_?_:_` included | the profile overload whose parameter types fit the argument types, a type parameter fitting any type but `error` at each place alone (`equals(int, string)`); the function name when none fits | the types of the argument values, the receiver first |
| selection; presence test | `select`; `has` | the type of the operand |
| macro | `comprehension` | the type of the range |
| list literal | `list` | the types of the elements |
| map literal | `map` | key and value types, entry by entry: `map(bool, int, bool, int)` |
| other literal | `literal` | its type |
| identifier | `ident` | the type of its value |
| either side fails to parse | `parse` | none |

Types are the CEL type names that `type()` gives, without parameters: `int`, `uint`, `double`, `bool`, `string`, `bytes`, `null_type`, `list`, `map`, `type`; an argument that is an error on both sides is `error`. A node without types keeps its parentheses: `parse() we-error`, `list() value-mismatch`.

**Minimization.** A deterministic descent: while a child of the root still diverges with the same bindings, the first such child becomes the case. The children of a call are its arguments, of a list literal its elements, of a map literal the key and the value of each entry, in the order of the source; of a selection and a presence test the operand; of a macro the range only, since its other arguments read the iteration variable. Minimization evaluates only nodes of the divergent case, with its bindings: it generates no other case and has no time limit, so the same seed and case number give the same minimized case and the same signature. Each node but the root is evaluated at most once, so a case of 40 nodes takes at most 39 evaluations on each side; a run ends the case it started within the budget together with its minimization. The arguments of the final root agree, so their types are defined. The whole tree is not reduced further.

## Known signatures and the corpus

A signature is known when the column `Signature` of the registry of known differences `tests/Great.Parts.Cel.Conformance/Regression/known-differences.md` or of the corpus skip list `Regression/skips.md` has it. The run keeps the first case of each new signature.

These cases go to `tests/Great.Parts.Cel.Conformance/Regression/pending.textproto`, which every run rewrites. The oracle writes them (request `case`) with the answer of cel-go as the expectation, named `pending/new/<seed>-<n>`. The file is part of the corpus, so `eng/test` fails with `ENG801` until each case is fixed or recorded. A compile error of cel-go goes into `eval_error`: a `SimpleTest` cannot expect a compile error.

The file is committed in its empty form, without cases, byte for byte as below, with LF line ends. A run without new signatures writes exactly this form, so the working copy stays clean. A run with `--numbers-only` does not touch the file.

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

`ENG822` compares the offsets of every zone of the time zone data file at generated instants: `ICaseSide.ZoneOffset(string zone, DateTimeOffset instant)` gives the offset from UTC as a `TimeSpan`. The message is `time zone offset differs from Go: zone '<zone>' at <instant>: Go <offset>, we <offset>`, with the instant in UTC in ISO 8601 and the offsets as `±hh:mm` or `±hh:mm:ss`. The comparison takes the path of the file as a parameter; the method, the comparison and its test exist from the start. The profile has no time zone data file until time: runs do not compare offsets, and both sides throw `NotSupportedException` from `ZoneOffset`. The part of the library that brings time zones sets the path, implements `ZoneOffset` on both sides, with the request `offset` of the oracle ([eng-oracle.md](eng-oracle.md#protocol)), and turns the comparison on.

```text
tests/Great.Parts.Cel.Conformance/Regression/pending.textproto(6,3): error ENG820: new divergence signature 'add_int64(int, int) we-value' (S1): cel-go returns error 'integer overflow', we return int -9223372036854775807; seed 20261104, case 1207
difftest seed=20261104 commit=9f8e7d6c oracle=0.32.0 cases=412345 signatures new=1 (S1=1, S2=0) known=0 new-cases=1207
```

In the message of `ENG820` an outcome is one line: a value as its type and its value, `int -9223372036854775807`; an evaluation error as `error '<message>'`; a compile error as `<stage> error '<message>'`, with the stage `parse`, `check` or `program`. A message is cut at its first line break.

The journal line comes last; `commit` is the start of `HEAD`. `new-cases` lists the numbers of the kept cases, separated by commas without spaces, and stays empty without new signatures: `new=0 (S1=0, S2=0) known=0 new-cases=`. Exit code: 0 without new signatures, 1 with a finding with `error`, 2 for an invalid invocation, a refused manifest, a failed oracle, its steps included, or a minimized case that agrees when it is run again, since a side is then not deterministic. The messages of exit code 2 go to standard error, the rest to standard output. `eng/run` passes the code on; its failed `dotnet build` of `eng/difftest` is 1 ([eng-test.md](eng-test.md#engrun)).

With `--numbers-only` standard output has the journal line alone: no `log` line and no finding, `ENG821` included. The journal line comes after `ENG821` too, with `cases=0` and exit code 1, a pair that no other finding gives. A message of exit code 2 quotes no case and no line or answer of the oracle, since those can carry expressions and values: a minimized case that agrees again is named by the seed and its number, which `--case` reproduces.

## Nightly run

`.github/workflows/difftest-nightly.yml` runs `eng/difftest` every night and, by hand, for the long run before a release, with the command of the container check, so the run on GitHub is the run on a developer machine; the oracle is the service `oracle` of Compose, so the workflow has no Go setup.

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

`eng/tests/Difftest/` ([eng-tests.md](eng-tests.md)) uses fake sides with given outcomes, without Go; only `GeneratorTests` uses the library, whose `Compile` is the type check:

- `SignatureTests`: another value from the oracle gives `ENG820` with the signature in the form above, another zone offset `ENG822` at the path the test gives; one case per kind; a known signature and agreeing outcomes give nothing; minimization takes the first divergent child, keeps a case whose children agree, evaluates only nodes of the case with its bindings, and passes on the failure of a side;
- `GeneratorTests`: for fixed seeds, every expression type checks, no repeated or wrong-typed map key, no `matches`, the limits hold; every node, function and target type of the profile occurs, among them a negative `double` literal, `[,]`, `{,}`, a map with a trailing comma, `has()` and a macro on `dyn`, a `dyn` variable bound to a list and to a map, a bound `double` that is NaN, ±infinity or -0.0, while no `double` literal is other than finite; the only child of a macro is its range; `map` and `filter` take only ranges of fixed order, and no iteration variable has the static type `type` or one the generator does not know, so a range of unknown element type stands in `dyn()`;
- `ManifestTests`: a manifest with `greatPartsCel` or `validators` is refused;
- `OracleConnectionTests`, with a fake oracle on a loopback socket: `log` lines pass unchanged; `failed`, a closed connection and no listener give exit code 2, and the message on no listener names the `logs` command; another Go in the handshake gives `ENG821`; an answer that does not come within the limit, which the test sets, gives exit code 2; with `--numbers-only` standard output has the journal line alone, and a message of exit code 2 quotes no line of the oracle; two runs of the same seed and `--case` send the same requests and give the same minimized case and signature.

Each finding line matches `Finding.Pattern`.
