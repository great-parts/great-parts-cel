# eng/checks

`eng/checks/` is the console application of the repository's own checks. It reads files of the repository, build results, test and tool reports and the git history, and writes every finding as one line of the common form. Rules that work inside the compiler are in the architecture analyzer, see [eng-analyzers.md](eng-analyzers.md).

## Project

| Item | Value |
| :- | :- |
| Project | `eng/checks/Great.Parts.Cel.Eng.Checks.csproj`, console application, part of the solution |
| Target | `net10.0` |
| Packages | `Microsoft.CodeAnalysis.CSharp` 5.0.0: C# syntax for suppressions, duplicates and examples |
| References | `eng/findings`: the codes and the finding, see [One source of the ENG codes](#one-source-of-the-eng-codes) |
| Other libraries | only the .NET libraries: `System.Text.Json`, `System.Xml.Linq`, `System.IO.Compression`, `System.Reflection.Metadata` |
| Packed | no |

## Invocation

```text
dotnet run --project eng/checks -- <command> [<argument>] [<command> [<argument>] ...] [<option> ...]
```

A command that takes an argument is followed by it, for example `signoff <range>`. The token after such a command is its argument even when it looks like a command name; a missing argument is exit code 2. An option stands before a command or after the argument of a command, not between a command and its argument. An unknown command or option is exit code 2 too.

| Option | Meaning |
| :- | :- |
| `--root <directory>` | the repository root, instead of the search from the working directory, see [Repository helpers](#repository-helpers) |
| `--no-build` | `packages` packs the Release build that already exists instead of building it, see [eng-checks-artifacts.md](eng-checks-artifacts.md#packages); other commands ignore it |
| `--jit <report>` | `test-count`: the xUnit XML report of the JIT run; other commands ignore it |
| `--aot <report>` | `test-count`: the report of the Native AOT run of the same commit, operating system and target; other commands ignore it |
| `--where <text>` | `test-count`: the text for messages, used as it is and not parsed, for example `linux-x64, net11.0`; other commands ignore it |
| `--report <file>` | `mutation`: the JSON report of Stryker.NET; other commands ignore it |
| `--config <file>` | `mutation`: the Stryker.NET configuration file of the run; other commands ignore it |
| `--since <commit>` | `mutation`: the commit against which the changed files are listed; without it `main`. It enables `since` as it does in Stryker.NET, also when the configuration disables it ([eng-mutate.md](eng-mutate.md#mutation)). `eng/mutate` gives the full SHA of its own `--since`; other commands ignore it |

A relative path of a report or a configuration file is taken from the working directory. `test-count` without any of its three options, or `mutation` without `--report` or without `--config`, is exit code 2.

Commands run in the given order. Several commands in one run share their scans: `ratchet` uses the suppressions and the duplicate pairs that `suppressions` and `duplicates` found in the same run instead of scanning again. The container commands call it this way, see [eng-test.md](eng-test.md).

| Command | Reads | Codes | Runs in | Described in |
| :- | :- | :- | :- | :- |
| `suppressions` | repository files | `ENG101`, `ENG102` | `eng/test`; CI, cell JIT Linux `net11.0` | [eng-checks-source.md](eng-checks-source.md) |
| `duplicates` | C# files, `eng/ratchet.json` | `ENG601` | the same | [eng-checks-source.md](eng-checks-source.md) |
| `ratchet` | `eng/ratchet.json`, `CodeMetricsConfig.txt`, results of the two commands above | `ENG600`, `ENG602`–`ENG605` | the same | [eng-checks-source.md](eng-checks-source.md) |
| `packages` | result of `dotnet pack`, `eng/packages/*.txt` | `ENG200`–`ENG207` | the same | [eng-checks-artifacts.md](eng-checks-artifacts.md) |
| `signoff` | commits of a range | `ENG701` | CI: push to `main`, pull request of an outside contributor | [eng-checks-artifacts.md](eng-checks-artifacts.md) |
| `test-count` | two test reports | `ENG810`, `ENG811` | `eng/test` for Native AOT; CI, Native AOT cells | [eng-checks-artifacts.md](eng-checks-artifacts.md) |
| `mutation` | mutation report, its configuration file, changed files from git when `since` is enabled | `ENG501`–`ENG504` | `eng/mutate` | [eng-mutate.md](eng-mutate.md) |
| `examples` | `README.md`, `docs/**/*.md`, `<code>` of XML documentation | `ENG301`, `ENG302`; compiler errors | `eng/docs`; CI, one Linux cell | [eng-docs.md](eng-docs.md) |
| `links` | lychee report | `ENG401`–`ENG404`, `ENG409` | the same; external links only in CI | [eng-docs.md](eng-docs.md) |
| `error-codes` | built library and its XML documentation, error codes page | `ENG303` | the same | [eng-docs.md](eng-docs.md) |

## Layout

| Path | Contents |
| :- | :- |
| `Program.cs` | parses the arguments, runs the commands, writes the findings, sets the exit code |
| `CheckContext.cs`, `CheckInputException.cs` | the context of a run and the exception for an input that does not exist or cannot be read |
| `Commands/` | the command interface, the registry of the commands and one class per command |
| `Repository/` | repository root, file set, C# sources, symbol sets, positions, calls of `git` |
| `Suppressions/` | the scanners of the forms of `suppressions`, the reason, the reader of `lychee.toml` |
| `Duplicates/` | tokens, the search and the joining of pairs of `duplicates` |
| `Ratchet/` | the reader of `eng/ratchet.json`, the comparison, the lines added against `main` |

A command added later puts the types that only it uses, the readers of its input formats included, into a directory of its own named after it.

The directories are not namespaces: every type of `eng/checks` is in the one namespace `Great.Parts.Cel.Eng.Checks`. The cycle rule `ARCH001` of the architecture analyzer applies to every C# project, `eng/checks` included ([eng-analyzers.md](eng-analyzers.md)), and namespaces per directory would make cycles: `Program` uses the registry of the commands, and the commands use `CheckContext` next to `Program`.

Every command implements one interface:

```text
internal interface ICheckCommand
{
    string Name { get; }
    string? ArgumentName => null;
    IEnumerable<Finding> Run(CheckContext context);
}
```

`ArgumentName` is the name of the one argument that follows the command, such as `<range>`, or `null` when the command takes none; the messages of a missing argument use it. `CheckContext` holds the repository root, the options of the run, the arguments of the commands by command name and the scans that several commands share; a scan is computed on first use. A command reports an input that does not exist or cannot be read — a file, a revision range — by throwing `CheckInputException`: `Program` writes its message to standard error and returns exit code 2.

## Finding and output

```text
public enum FindingSeverity { Error, Warning }

public readonly record struct Finding(
    string Path, int Line, int Column, FindingSeverity Severity, string Code, string Message)
```

`Finding.ToString()` is the only place that formats a line:

```text
<path>(<line>,<column>): <error|warning> <code>: <message>
```

- The path is relative to the repository root and uses `/`. A finding without a file of the repository names a git object instead: `commit/<sha>` or `tag/<name>`, place `(1,1)`; `Finding.ForCommit` and `Finding.ForTag` build such findings.
- Line and column start at 1; the constructor rejects smaller values. A column counts UTF-16 code units of the line.
- The message is English, one line. It names what is wrong and, where it helps, the second place or the value to change. It has no codes of requirements or decision records.
- `Finding.Pattern` is the regular expression of a line, `Finding.TryParse` parses a line. Tests check every line of every own check against it:

```text
^(?<path>[^(]+)\((?<line>\d+),(?<col>\d+)\): (error|warning) (ARCH|ENG)\d{3}: .+$
```

Findings go to standard output. Within a command they are sorted by path (ordinal), line and column, so two runs on the same input print the same lines. Messages of the application itself — an unknown command, a missing input file — go to standard error.

| Exit code | Meaning |
| :-: | :- |
| 0 | no finding with `error` |
| 1 | at least one finding with `error` |
| 2 | invalid invocation or an input that does not exist or cannot be read |

Warnings do not change the exit code.

## One source of the ENG codes

`EngCodes`, `Finding` and `FindingSeverity` are public types of their own library project:

| Item | Value |
| :- | :- |
| Project | `eng/findings/Great.Parts.Cel.Eng.Findings.csproj`, part of the solution |
| Target | `net10.0`; projects for `net11.0` reference it too |
| Packages | none: only the base library, no reflection |
| Settings | `IsAotCompatible=true`, so the trimming and Native AOT analyzers check it in every build; `IsPackable=false` |

`eng/checks`, the suite runner `tests/Great.Parts.Cel.Conformance` and `eng/difftest` reference this project, and every component that writes `ENG` findings does the same; a file-based app references it with `#:project`. No component formats a finding line or declares a code itself.

Why a project of its own:

- Each type exists in one assembly. `eng/tests` references `eng/checks` and `eng/difftest` and uses `EngCodes` and `Finding` through both. Source files compiled into each of them would give two types with one full name, and every use in `eng/tests` would be ambiguous.
- The runner is published under Native AOT. A reference to `eng/checks` would bring Roslyn into it; `eng/findings` brings nothing.
- These public types are not API of the packages: the API rules of the analyzer do not apply to `eng/` ([eng-analyzers.md](eng-analyzers.md)).

`EngCodes` is a `public static class` with one `public const string` field per code. The field name is the code in Pascal case, the value is the code, for example `Eng601 = "ENG601"`. The XML summary of the field states the meaning in one sentence. Fields are grouped by hundreds, in the order of numbers.

- A code is added together with the check that writes it and takes the next free number of its group. Numbers are not reused, and the meaning of a code does not change.
- No other file of the repository declares an `ENG` code. Checks pass `EngCodes` fields to `Finding`; tests refer to the same fields.

## Repository helpers

- **Root** — the nearest parent directory that contains `Great.Parts.Cel.slnx`, or the working directory given by `--root`.
- **File set** — all files under the root except `.git/`, `bin/`, `obj/`, `artifacts/` and `tests/cel-spec/`. A C# file is generated by Roslyn's signs in the file itself, the marker comment or the name, see [Generated code](eng-checks-source.md#generated-code); checks of code skip it, and outside the allowed directories `suppressions` counts it as a suppression.
- **C# sources** — the C# files of the file set, generated files excluded, parsed once per symbol set of the targets, see [C# sources](eng-checks-source.md#c-sources).
- **Positions** — line and column from an offset in a file read as UTF-8.
- **git** — runs the `git` program in the root and reads its output; no git library is used.

## Code registry test

`eng/tests/CodeRegistryTests.cs` checks that every code of the repository has a test on a violating example.

- `ENG` codes — the constant fields of `EngCodes`. The test also checks that every value is `ENG` and three digits, equals its field name in upper case and occurs once.
- `ARCH` codes — the `Rule ID` column of `eng/analyzers/AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`.
- A code has a test when a file named `*Tests.cs` under `eng/tests/` or `tests/` contains it as a token of code: the identifier of the `EngCodes` field or a string literal equal to the code. The files are read as C# sources, so a code in a comment does not count and a code under `#if` does. The registry test file itself is not searched.
- On failure the test `EveryCodeHasViolationTest` names every code without a test, for example `ENG604 has no test`.

The test reads only the repository: the codes in `EngCodes` and in the release files are the complete list.
