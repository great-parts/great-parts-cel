# AGENTS.md

Instructions for AI coding agents that work in this repository. For people: [CONTRIBUTING.md](CONTRIBUTING.md).

## Build and test

Everything runs in containers; a local .NET SDK is not needed. From the repository root:

- `docker compose -f eng/compose.yaml run --rm test`: the full check. Build, all tests under JIT for `net10.0` and `net11.0`, the repository checks, Native AOT on linux-x64. A change is done when it passes.
- `docker compose -f eng/compose.yaml run --rm docs`: the examples, the error codes page and the links. Needed when a path of "Triggers" in `docs/design/eng-docs.md` changes, such as `README.md`, `docs/` or `src/`.
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
