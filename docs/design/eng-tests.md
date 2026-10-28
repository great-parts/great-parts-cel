# eng/tests

`eng/tests/` holds the tests of the repository's own checks: every rule of the architecture analyzer and every command of `eng/checks` has a test on a violating example, and every finding the tests produce has the common form. The project also holds the code registry test, see [eng-checks.md](eng-checks.md).

## Project

| Item | Value |
| :- | :- |
| Project | `eng/tests/Great.Parts.Cel.Eng.Tests.csproj`, part of the solution |
| Target, mode | `net10.0`, JIT only |
| Packages | `xunit.v3.aot` 4.0.2 like every test project, `Microsoft.CodeAnalysis.CSharp` 5.0.0 |
| References | `eng/checks` — the commands run in the test process; `eng/analyzers` as a library — the rules run in memory; `eng/findings` comes through `eng/checks` and through `eng/difftest`, one assembly for both |

It runs in `eng/test` and in the JIT cells of CI, never under Native AOT.

## Layout

| Path | Contents |
| :- | :- |
| `Analyzers/` | one class per rule or group of rules: `Arch001Tests`, `Arch002To004Tests`, `Arch005Tests`, `Arch006Tests`, `Arch007Tests`, `Arch008Tests` |
| `Checks/` | one class per command: `SuppressionsTests`, `DuplicatesTests`, `RatchetTests`, `PackagesTests`, `SignoffTests`, `TestCountTests`, `MutationTests`, `ExamplesTests`, `LinksTests`, `ErrorCodesPageTests` |
| `Difftest/` | tests of `eng/difftest`, described with it |
| `CodeRegistryTests.cs` | the code registry test |
| `Support/` | the temporary repository, the in-memory compilation and the finding assertions |

A file with tests is named `<Subject>Tests.cs`; the code registry test searches these files for codes.

## Violating examples are data

A violating example is a raw string literal inside the test, never a file of the repository: as a file, it would be reported by the checks of the repository itself and would change the ratchet. The test writes its files into a new temporary directory and gives that directory to the command as the repository root (`--root` or the root of `CheckContext`). The directory is deleted after the test.

Every test:

- names the violation in its name, for example `PragmaWithoutReason_IsReported`;
- checks code, level, path, line and column of each expected finding, and that nothing else is reported;
- checks that each formatted line matches `Finding.Pattern`;
- has a case that is not a violation next to the violating ones, where the rule defines one: a generated file, an internal type with a reserved name, `global::Cel.Expr.Value`.

The cases come from the rows of the checks: for example, `SuppressionsTests` has one case per form of the suppressions table without a reason, a reason that only repeats the rule ID, a generated file in an allowed directory that is not counted, and each sign of generated code outside them without a reason.

Reading C# sources has its own cases in `SuppressionsTests` and `DuplicatesTests`: a form and a duplicate under `#if NET11_0_OR_GREATER` are found, and counted once although several symbol sets see them, also when a branch near the start of a repeat moves it; a branch that no set activates is not read; an attribute and a directive next to syntax the parser does not know are found.

## Analyzer tests

- A test builds a `CSharpCompilation` in memory with Roslyn 5.0.0, the version the analyzer is built for. Sources are strings with repository paths, for example `src/Great.Parts.Cel/Values/CelList.cs`; references are the assemblies of the running .NET runtime.
- The layer table and the API concept list are in-memory additional files with their repository paths, so a finding in them has the right path.
- The test runs the analyzer with `CompilationWithAnalyzers` and turns each diagnostic into a `Finding`: the path of the location, the line and column of its start plus one. The finding is then checked like a finding of `eng/checks`.
- Analyzers of the SDK and of packages, such as the list of banned APIs, are not tested here: they are built for a newer compiler than Roslyn 5.0.0 and do not load into this compilation. A one-time sample checks them.

## Tests of commands

- Commands that read files get files in the temporary root: `.editorconfig`, project files, `lychee.toml`, `eng/ratchet.json`, package lists, Markdown pages.
- Commands that read reports get reports written by the test: a lychee report, two xUnit XML reports, a mutation report.
- `PackagesTests` build `.nupkg` files with `System.IO.Compression`: one package per case, with an entry, a group or a license expression that breaks the list.
- `SignoffTests` create a git repository in the temporary root with `git init` and make commits with and without the trailer: a plain commit, a merge and a commit of a bot.
- `ExamplesTests` build and run generated example programs, so they take longer than the others.
- `ErrorCodesPageTests` compare pages with member lists given by the test; one test reads the members from the built library.

`SignoffTests` and `ExamplesTests` need `git` and the .NET SDK; both are in the container image and on the CI runners.

## Rules shared with all tests

Test projects follow [tests.md](tests.md): the requirement trait, the test framework, the seed of property tests. The tests here carry a requirement trait when they check a requirement of the library directly, for example the composition of the packages.

## Control set of mutation testing

The code of `eng/checks` with these tests is the known set on which the mutation tool and its test runner are checked, see [eng-mutate.md](eng-mutate.md).
