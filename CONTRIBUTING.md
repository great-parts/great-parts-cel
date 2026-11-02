# Contributing

Great.Parts.Cel is in pre-release, and the public API is unstable. Contributions are welcome.

## Before you start

- A vulnerability goes through [SECURITY.md](SECURITY.md), not through an issue or a pull request.
- Questions and bug reports: see [SUPPORT.md](SUPPORT.md).

## Build and test

Everything runs in containers, so you need Docker with Docker Compose, not a .NET SDK. From the repository root, one command:

```text
docker compose -f eng/compose.yaml run --rm test
```

This is `eng/test`, the full check of a change. It builds the solution, runs every test project under JIT for each of its targets (`net10.0`, `net11.0`), including the cel-spec conformance suite, runs the repository checks, publishes the test projects that run under Native AOT for linux-x64, runs them and compares their test count with the JIT run. The last line is the summary, for example `test: passed`; the exit code is 0 when everything passed. The first run downloads the container images and the NuGet packages.

From a linked worktree (`git worktree add`), the command needs a second Compose file; see [Linked worktrees](docs/design/eng-test.md#linked-worktrees).

The design of the library and of the checks is in [docs/design](docs/design/); the container commands are described in [eng-test.md](docs/design/eng-test.md).

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every pull request and on every push to `main`:

- build and tests under JIT on Ubuntu and macOS, for `net10.0` and `net11.0`;
- Native AOT on linux-x64, for both targets: publish, run, and the test count compared with JIT;
- the repository checks and the tests of `eng/`, once, in the cell for Ubuntu and `net11.0`;
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

The library is designed by the maintainer and implemented by AI agents under the maintainer's direction. The commits of the maintainer in `main` carry `Co-Authored-By` trailers for the AI models that took part. The same checks, `eng/test` and CI, apply to every change, whoever wrote it.
