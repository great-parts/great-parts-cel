# eng/oracle

`eng/oracle/` is the reference side of the differential run: a small Go program that answers CEL cases with cel-go. It runs in its own container from the official `golang` image; [eng-difftest.md](eng-difftest.md) connects to it from the .NET container. The oracle reports the outcome of cel-go and makes no decisions about divergences.

## Module

| Item | Value |
| :- | :- |
| Path | `eng/oracle/`, not part of the .NET solution |
| `go.mod` | `module github.com/great-parts/great-parts-cel/eng/oracle`; `go 1.27.3`, the version of Go of the oracle ([Version of Go](#version-of-go)); requires `cel.dev/cel-go` v0.32.0, `cel.dev/expr` v0.25.1 and `google.golang.org/protobuf` v1.36.10 — the last two at the versions that cel-go v0.32.0 itself requires; `go.sum` is committed |
| Direct imports | `cel.dev/cel-go/cel`, `cel.dev/cel-go/common/env`, `cel.dev/cel-go/common/types`, `cel.dev/cel-go/ext`; `cel.dev/expr` and `cel.dev/expr/conformance/...` for `Value` and `SimpleTest`; `google.golang.org/protobuf/encoding/protojson` and `prototext`; the Go standard library |
| Tests | none in Go: the oracle is checked by the steps of the launcher and by every run; the comparison is tested on the .NET side with fake sides, see [eng-difftest.md](eng-difftest.md#tests) |

| File | Contents |
| :- | :- |
| `main.go` | the commands `serve` and `conformance`, exit codes |
| `env.go` | the environment from a manifest: flags, extension factories |
| `serve.go` | the protocol on standard input and output |
| `conformance.go` | the one-time run of test files by the conformance rule |
| `launch/main.go` | the entry point of the container: the steps, the session with `eng/difftest`, the relay of lines; the standard library only |

## Environment from the manifest

`serve --env <file>` reads the run manifest, an environment description in the JSON format of the library ([configuration.md](configuration.md#run-manifest-of-the-differential-run)), which the library reads too.

1. The file is decoded as a JSON object first. A manifest with the key `greatPartsCel` or `validators` is refused with exit code 2: cel-go would ignore the first silently and read the second differently.
2. `env.ConfigFromYAML` reads the file: cel-go reads the JSON form of its configuration as YAML.
3. The environment:

```text
cel.NewCustomEnv(
    cel.FromConfig(config, ext.ExtensionOptionFactory, networkFactory),
    cel.EagerlyValidateDeclarations(true),
    cel.EnableErrorOnBadPresenceTest(true),
    cel.EnableIdentifierEscapeSyntax(),
    cel.ASTValidators(cel.ValidateRegexLiterals()))
```

`FromConfig` adds the standard library when the manifest has no `stdlib` key and knows `optional` by itself; any other extension set needs a factory, otherwise it is `unrecognized extension` (`cel/options.go` of v0.32.0). `ext.ExtensionOptionFactory` knows the sets of `ext/extension_option_factory.go`; `network` is not among them, so the oracle passes its own factory:

```text
networkFactory(element) -> (option, handled):
    element is *env.Extension with Name "network"  ->  ext.Network(ext.NetworkVersion(version)), true
    anything else                                  ->  nil, false
```

`version` is `Extension.VersionNumber()` of the manifest entry. The manifest of the walking skeleton has no extension sets.

A manifest that cel-go cannot read, or from which it builds no environment — an unknown extension set, an invalid declaration — is refused with exit code 2 as well, with the message of cel-go.

**Flags.** The three options after `FromConfig` are the environment flags of the cel-go conformance runner: `stdOpts`, lines 82–98 of https://github.com/cel-expr/cel-go/blob/v0.32.0/conformance/conformance_test.go, read on 2026-10-28. The rest of `stdOpts` is content and comes from the manifest or stays out of the run. The regex validator is not a runner flag: the library rejects a constant pattern that fails to compile at compile time, and the validator gives cel-go the same class of outcome. A change of the cel-go version compares this list with `stdOpts` of the new version.

## Time zones

The oracle takes time zones only from `zoneinfo.zip` of the pinned Go: it refuses to start, with exit code 2, unless the environment variable `ZONEINFO` names an existing file. The container hides the system zone directory under an empty mount, so every zone comes from that file. `ZONEINFO` is `/repo/tests/Great.Parts.Cel.Conformance/ZoneInfo/zoneinfo.zip`, the copy of the file of the image that the repository holds for the library tests and that `eng/difftest` gives to the library side too ([tests.md](tests.md#time-zone-data)), so a zone is the same bytes in Go and in the library.

## Protocol

After the environment is built, `serve` writes one handshake line and then answers requests: one JSON object per line on standard input, one per line on standard output, UTF-8, in order. The end of standard input ends the process with exit code 0.

```text
{"oracle":"cel.dev/cel-go v0.32.0","go":"go1.27.3"}
{"id":7,"op":"eval","expr":"x1 + 1","bindings":{"x1":{"int64Value":"41"}}}
{"id":7,"outcome":"value","value":{"int64Value":"42"}}
```

In the handshake, `go` is the Go that built the oracle, `runtime.Version()`.

| Request `op` | Fields | Answer |
| :- | :- | :- |
| `eval` | `id`, `expr`, `bindings`: name → `cel.expr.Value` | `outcome` and its fields, below |
| `case` | the fields of `eval`, `name`, `description` | `textproto`: a `SimpleTest` in text format |
| `offset` | `id`, `zone`: the name of a zone of the file, `instant`: whole seconds since 1970-01-01T00:00:00Z, no other field | `outcome` `offset` with `seconds`, or `zone-error` with `message` |

| `outcome` | When | Fields |
| :- | :- | :- |
| `compile-error` | `Parse`, `Check` or `Program` of the environment fails | `stage` — `parse`, `check` or `program`; `message` |
| `eval-error` | `Eval` returns an error or an error value | `message` |
| `value` | otherwise | `value` |
| `offset` | the answer to the request `offset` | `seconds`: the offset in seconds that `time.LoadLocation` of the zone gives at the instant |
| `zone-error` | `time.LoadLocation` does not load the zone | `message` of Go |

- Values travel as `cel.expr.Value` in the JSON mapping of protobuf. A binding becomes a value with `cel.ProtoAsValue`, a result a message with `cel.ValueAsProto`, as in the conformance runner.
- The `case` answer is a case of the regression corpus: `name`, `description`, `expr`, `type_env` with the manifest variables that the expression reads, `bindings`, and the outcome of cel-go as the expectation — `value` or `eval_error` with the message.
- The text of the `case` answer is the output of `prototext`, which is not stable on purpose: its spaces change across builds of the oracle (https://pkg.go.dev/google.golang.org/protobuf/encoding/prototext, read on 2026-10-29). `eng/difftest` copies it unchanged and never compares it byte for byte; only the empty form of `pending.textproto` is exact ([eng-difftest.md](eng-difftest.md#known-signatures-and-the-corpus)).
- A malformed request, or a result without a `cel.expr.Value` form, ends the process with exit code 1 and a message on standard error. A result without that form is a value of an abstract type (`net.IP`, `net.CIDR`) and a string that is not valid UTF-8: `%s` of `bytes` that are not valid UTF-8 gives cel-go a Go string with the raw bytes (`proto: field cel.expr.Value.string_value contains invalid UTF-8`). The profile of `eng/difftest` makes neither ([eng-difftest.md](eng-difftest.md#profile-of-the-extension-sets)); the oracle does not hide either, since a value that it cannot send is a case that cannot be compared. One exception: an optional as the whole result, which cel-go gives for an expression that its checker types as another type — a presence test `has(e.f)` over an optional operand that is a call — is the outcome `eval-error` with the message `the result is an optional and has no cel.expr.Value form: <the optional>`, so the comparison names the divergence from the library, which gives a `bool`, and the run goes on; an optional inside a list or a map still ends the process.
- The request `offset` is `ENG822` of [eng-difftest.md](eng-difftest.md#findings-and-output): the oracle loads each zone once from the archive of `ZONEINFO` and answers the offset of `time.Unix(instant, 0)` in it. A value of time is `object_value`, an `Any` of `google.protobuf.Timestamp` or `google.protobuf.Duration`, in the protojson of the answer.

## One-time run of test files

`conformance <file>...` runs cel-spec test files by the rule in [conformance.md](conformance.md#the-rule), with `stdOpts` of the cel-go runner except the test-only `block` macros and with the extension sets that the files of the run need, in one environment for all files, as the cel-go runner has, and prints `PASS` or `FAIL`, the case name and the reason per case. Exit code: 0 when every case passes, 1 when a case fails, 2 for an invalid invocation, a file that is not a `SimpleTestFile` or a missing `ZONEINFO` file. It checks the files that the cel-go runner does not run:

```text
docker compose -f eng/compose.yaml run --rm oracle conformance tests/cel-spec/tests/simple/testdata/<file>.textproto
```

## Container

The service `oracle` of `eng/compose.yaml` runs the image `golang` of Docker Hub as it is. Go exists only in this container: the service `difftest` runs the stage `sdk` ([eng-test.md](eng-test.md#image)).

| Item | Value |
| :- | :- |
| Image | `golang:1.27.3`; the tag equals the `go` line of `go.mod` |
| Entry point | `go run ./launch` in `/repo/eng/oracle`; the arguments of `docker compose run` follow it. When the launcher exits with a code other than 0, `go run` prints `exit status <code>` and exits with 1, so the exit code of the container only tells success from failure; the code of the launcher and of the oracle is in that line |
| Mounts | the working copy at `/repo`; the named volumes `gomod` at `/go/pkg/mod` and `gobuild` at `/root/.cache/go-build`; an empty `tmpfs` over `/usr/share/zoneinfo`. The git mounts of `eng/compose.worktree.yaml` are not added: the oracle does not read git |
| Variables | `GOTOOLCHAIN=local`, which the image sets too; `ZONEINFO=/repo/tests/Great.Parts.Cel.Conformance/ZoneInfo/zoneinfo.zip`; `GOFLAGS=-mod=readonly -buildvcs=false` — no version stamp, so the build does not read git |
| Modules | from the Go module proxy, checked against the checksum database |
| Network | the default network of the project, port 7070; nothing is published to the host |
| Restart policy | none |

## Launcher

`launch/main.go` is to this container what `eng/run` is to the .NET one ([eng-test.md](eng-test.md#engrun)): it runs steps as child processes and passes their lines on, with no check logic. Without arguments it serves one session:

| Phase | What happens |
| :- | :- |
| Wait | listen on port 7070; without a connection for 15 minutes exit with 1 — the run that started the container ended before it reached the oracle, for example on a failed build |
| Start | accept one connection and stop listening; the first line names the manifest by its path from the repository root: `{"env":"eng/difftest/Profile/environment.json"}` |
| Steps | delete `/repo/artifacts/oracle/`; in `eng/oracle`: `gofmt -l .` — output is a failure, then `gofmt -d .` prints the change; `go vet ./...` — no remark; `go build -o /repo/artifacts/oracle/oracle .` |
| Lines of the steps | `{"log":"== <step>"}` before each step, `{"log":"<line>"}` for each output line; the lines of `go vet` and of the compiler name `file:line:column`; a failed step sends `{"failed":"<step>"}`, closes the connection and exits with 1 |
| Session | `oracle serve --env <manifest>` in the repository root: lines from the connection go to its standard input, its standard output goes to the connection unchanged, each line of its standard error as a `log` line, and as a line `oracle: <line>` on the standard error of the launcher, so the log of the container has it too |
| End | `eng/difftest` closes the connection, standard input of the oracle ends, the oracle exits, the launcher writes `launch: the oracle ended with exit code <code>`, with the text of the error of Go after it when the oracle did not end with 0, to its standard error and as a last `log` line, then exits with the code, the container stops. When the oracle ends on a case, that line reaches `eng/difftest` before the connection closes, and the message of the closed connection has it ([eng-difftest.md](eng-difftest.md#findings-and-output)) |

With arguments it prints the step lines itself, opens no port and passes the arguments to the oracle.

**Before the session.** `go run ./launch` compiles the launcher in the oracle module. A Go older than the `go` line refuses the module under `GOTOOLCHAIN=local` (https://go.dev/doc/toolchain, read on 2026-10-28); then, as on a compile error of `launch/main.go`, the container ends before it listens. The message stays in the container log, which `docker compose run` does not show; `eng/difftest` names the command for it: `docker compose -f eng/compose.yaml logs oracle`, with the same `-p <name>` when the run had one.

**Lifetime.** `docker compose run` starts the services its service depends on and leaves them running; `--rm` removes only its own container (Docker Compose v5.6.1, `startDependencies` and `RunOneOffContainer` in https://github.com/docker/compose/blob/v5.6.1/pkg/compose/run.go, read on 2026-10-28). So the oracle ends itself, the next run starts the stopped container again, and nobody stops or removes containers. A run that ends before it connects leaves the container waiting for up to 15 minutes.

A Compose project has one container of the service `oracle`, and a run from another working copy that shares the project replaces it, together with the session of the first run. `eng/compose.yaml` names the project `great-parts-cel-eng`, which is the project of the main working copy; a run from a linked worktree adds `eng/compose.worktree.yaml`, which names the project after the working copy ([eng-test.md](eng-test.md#linked-worktrees)), so two working copies never share the oracle. Concurrent runs from one working copy take their own project names, `docker compose -p <name> -f eng/compose.yaml run --rm difftest`; otherwise the second run fails with exit code 2.

## Version of Go

The `go` line of `go.mod` is the version of Go of the oracle; there is no `toolchain` line, which `GOTOOLCHAIN=local` ignores. The image tag of `oracle` equals it, a new version changes both in one commit, and CI runs the same Compose command. A differing tag stops the run: an older Go refuses the module before the session ([Launcher](#launcher)); a newer one is named by the handshake, and `eng/difftest` reports `ENG821` at the `go` line before the first case ([eng-difftest.md](eng-difftest.md#findings-and-output)).
