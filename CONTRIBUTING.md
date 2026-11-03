# Contributing

Great.Parts.Cel is in pre-release, and the public API is unstable. Contributions are welcome.

## Before you start

- A vulnerability goes through [SECURITY.md](SECURITY.md), not through an issue or a pull request.
- Questions and bug reports: see [SUPPORT.md](SUPPORT.md).
- Everyone who takes part follows the [code of conduct](CODE_OF_CONDUCT.md).

## Build and test

Everything runs in containers, so you need Docker with Docker Compose, not a .NET SDK. From the repository root, one command:

```text
docker compose -f eng/compose.yaml run --rm test
```

This is `eng/test`, the full check of a change. It builds the solution, runs every test project under JIT for each of its targets (`net10.0`, `net11.0`), including the cel-spec conformance suite, runs the repository checks, publishes the test projects that run under Native AOT for linux-x64, runs them and compares their test count with the JIT run. The last line is the summary, for example `test: passed`; the exit code is 0 when everything passed. The first run downloads the container images and the NuGet packages.

A change of `README.md`, `docs/` or the XML documentation of `src/` also needs the check of the documentation:

```text
docker compose -f eng/compose.yaml run --rm docs
```

This is `eng/docs`. It compiles and runs every C# example of `README.md`, `docs/` and the XML documentation and compares its output with the output the example shows, checks that the [error codes page](docs/error-codes.md) matches `CelErrorCode`, and checks the relative links, files and anchors. External links are checked only in CI. The list of paths that need `eng/docs` is in [eng-docs.md](docs/design/eng-docs.md#triggers).

From a linked worktree (`git worktree add`), both commands need a second Compose file; see [Linked worktrees](docs/design/eng-test.md#linked-worktrees).

The design of the library and of the checks is in [docs/design](docs/design/); the container commands are described in [eng-test.md](docs/design/eng-test.md).

## How a change is made

1. For a bug, open an issue first, unless the fix is small and obvious. For a new feature, open an issue and describe the use case.
2. A change of the public API is discussed in an issue before the pull request. The public API is recorded in the `PublicAPI` directory of each package project, `src/Great.Parts.Cel` and `src/Great.Parts.Cel.Protobuf`, and the build fails when the code and these files differ.
3. Every change of behavior comes with a test that fails without it. A difference from cel-spec or cel-go is shown by the expression, its variables and the expected result.
4. A change that users of the package can see gets a line under `Unreleased` in [CHANGELOG.md](CHANGELOG.md). A change of the result or of the error of an expression that was valid before goes under `Behavior changes`.
5. Run `eng/test` (and `eng/docs`, if the change needs it), sign off every commit (below), and open a pull request against `main`.
6. CI runs on the pull request. The maintainer reviews it and merges it as described below.

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every pull request and on every push to `main`:

- build and tests under JIT on Ubuntu and macOS, for `net10.0` and `net11.0`;
- Native AOT on linux-x64, for both targets: publish, run, and the test count compared with JIT;
- the repository checks and the tests of `eng/`, once, in the cell for Ubuntu and `net11.0`;
- the documentation: the examples, the error codes page, and the links, both relative and external;
- `Signed-off-by` in every commit of the pull request or of the push to `main`.

## Developer Certificate of Origin

Every commit needs a `Signed-off-by` line with your name and email. With it you certify the [Developer Certificate of Origin 1.1](https://developercertificate.org/): among other things, that you have the right to submit the change under the license of the project. `git commit -s` adds the line:

```text
Signed-off-by: Your Name <you@example.com>
```

A commit without it fails CI. To sign off the commits of your branch, run `git rebase --signoff main` and push the branch again with `--force-with-lease`.

A contribution is licensed under the Apache License 2.0, as section 5 of [LICENSE](LICENSE) says.

## How a pull request reaches `main`

Pull requests are not merged with the button on GitHub. The maintainer merges the branch locally without rebasing it, so your commits keep their author and sign-off, signs off the merge commit (`git merge --no-ff --signoff`) and pushes `main`. GitHub then shows the pull request as merged.

## How the library is developed

Great.Parts.Cel is designed by its maintainer, implemented by a team of AI agents under the maintainer's direction, and verified by cel-spec and CI.

A change made by the agents is checked by `eng/test`, `eng/docs` and CI, the same checks that apply to a change of any contributor. It reaches `main` as one commit under the name of the maintainer, signed off by the maintainer.
