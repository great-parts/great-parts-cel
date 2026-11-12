# AGENTS.md

Instructions for AI coding agents that work in this repository. For people: [CONTRIBUTING.md](CONTRIBUTING.md).

## Build and test

Everything runs in containers; a local .NET SDK is not needed. From the repository root:

The full check of a change is CI (`.github/workflows/ci.yml`): on every push to `main` and every pull request it builds, runs all tests under JIT for `net10.0` and `net11.0` and under Native AOT on linux-x64, the repository checks, the documentation checks and the `min-sdk` check; the differential run against cel-go, `eng/difftest`, is the workflow `.github/workflows/difftest-main.yml` on every push to `main`. Do not run the full check, `eng/difftest` included, locally before you finish a change. The commands stay for a run by hand:

- `docker compose -f eng/compose.yaml run --rm test`: build, all tests under JIT for `net10.0` and `net11.0`, the repository checks, Native AOT on linux-x64.
- `docker compose -f eng/compose.yaml run --rm docs`: the examples, the error codes page and the links. A change of a path of "Triggers" in `docs/design/eng-docs.md`, such as `README.md`, `docs/` or `src/`, changes the result.
- `docker compose -f eng/compose.yaml run --rm min-sdk` (after `test`): the `min-sdk` check, see `docs/design/eng-test.md`. `docker compose -f eng/compose.yaml run --rm mutate eng/stryker-control.json`: the control set of mutation testing, which runs before release 1.0 together with `eng/mutate`, see `docs/design/eng-mutate.md`.
- `docker compose -f eng/compose.yaml run --rm difftest`: the differential run against cel-go, `docs/design/eng-difftest.md`. CI runs it on every push to `main`; run it locally by hand only, and reproduce a case of a run with `--seed <seed> --case <n>`.
- From a linked worktree add `-f eng/compose.worktree.yaml`, see `docs/design/eng-test.md`, "Linked worktrees".

## Where things are

- `src/Great.Parts.Cel`: the library. `src/Great.Parts.Cel.Protobuf`: protobuf types. `src/Great.Parts.Cel.Generator`: the source generator.
- `tests/`: unit, property and conformance tests. `tests/cel-spec/`: an unchanged copy of cel-spec at the commit in `tests/cel-spec/COMMIT`.
- `eng/`: the containers, the repository checks (`eng/checks`), the analyzers and their tests.
- `docs/design/`: the design of each part. Read the page of the part before you change it; the code follows it.

## Rules

- Warnings are errors, nullable reference types are on, and all analyzers run. Do not suppress a diagnostic to pass the build: a suppression needs a reason and is counted in `eng/ratchet.json`, and the count only goes down (`docs/design/eng-checks-source.md`).
- Every public type and member has XML documentation. A change of the public API updates the files in `PublicAPI/` of the project.
- Errors of compilation and evaluation are returned as `CelError` with a code of `CelErrorCode`, not thrown.
- Do not edit `tests/cel-spec/`: it changes only by moving the pinned commit. A cel-spec case that does not pass goes to `tests/Great.Parts.Cel.Conformance/CelSpec/skips.md` with its reason.
- A new package dependency is agreed in an issue first.
- Code, comments, documentation and messages are in English.
- Every commit has a `Signed-off-by` line (DCO, see CONTRIBUTING.md).
