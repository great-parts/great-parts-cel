# eng/docs

`eng/docs` checks the documentation: the C# examples of `README.md`, `docs/` and the XML documentation compile and print what they show, the error codes page matches the error codes of the library, and relative links, files and anchors exist. It is separate from `eng/test`, which stays fast. It is the service `docs` of `eng/compose.yaml` with the image stage `docs`, see [eng-test.md](eng-test.md).

## Steps

Before step 1 the command builds the checks: `dotnet build eng/checks -c Release`, with the limit of a build. Steps 2, 3 and 5 run the built checks without a build of their own, so the command needs this build when it is run alone. When it fails, the command fails and nothing else runs.

1. `dotnet build src/Great.Parts.Cel -c Release`: both targets with their XML documentation.
2. `eng/checks examples`.
3. `eng/checks error-codes --page <page>`. The path of the page is a constant of the `docs` command of `eng/run`.
4. lychee, offline, after the command has created `artifacts/docs` (the command removes the directory before it starts, so a report of an earlier run is never read):

```text
lychee --offline --include-fragments --format json --output artifacts/docs/lychee.json
       --config lychee.toml README.md docs src/**/*.cs
```

5. `eng/checks links --report artifacts/docs/lychee.json`.

- When step 1 fails, steps 2 and 3 are skipped and the command fails; steps 4 and 5 run anyway.
- The exit code of lychee is not used: the report is the result, and `links` turns it into findings. A lychee that does not start or does not end within its limit still fails the step, and a missing report fails `links`.
- Exit code: 0 when every step returned 0, otherwise 1. Output: standard output of the container; the lychee report in `artifacts/docs/`.

External links are checked only in CI: the same `links` command reads a report of lychee run without `--offline`, with retries and a request cache. The cell of CI runs lychee with the `http` and `https` schemes only, `--max-retries 3`, `--cache` and `--cache-exclude-status '429, 500..'`, keeps the cache of requests between runs, and writes the report to `artifacts/docs/lychee-external.json`; `links` reads that report.

`lychee.toml` in the repository root holds the settings and the exclusions. Every exclusion has its reason in the same line. `.lycheeignore`, which lychee reads from the working directory without being told, holds address patterns, each with its reason in the comment line directly above. `suppressions` counts both under the key `lychee` ([eng-checks-source.md](eng-checks-source.md#suppressions)).

The XML documentation links a page of `docs/` by its address on GitHub, `https://github.com/great-parts/great-parts-cel/blob/main/docs/<page>.md`, in `<see href="…">`: the documentation goes with the package, where a relative path leads nowhere. lychee 0.24.2 reads a `.cs` file as plain text and finds only addresses with a scheme, and offline it skips them. So `lychee.toml` remaps the prefix `https://github.com/great-parts/great-parts-cel/blob/main/` to `file:///repo/`, the working directory of lychee in `eng/docs` and in CI: the offline run checks the file and its anchor like a relative link (`ENG403`, `ENG404`), and the run of external links, which checks only `http` and `https`, skips it. An exclusion applies to the address after the remap, so a page that is not written yet is excluded by the pattern of its path, `/docs/<page>\.md(#.*)?$`, which matches both a remapped address and a relative link of `README.md` or `docs/`. The addresses lead to `main`, not to a tag of a version: the pages of `docs/` are living documents, so the documentation of a released version points to the current page, and a link to a tag would be rewritten at every release. The prefix of the `remap` depends on the working directory `/repo`; when it changes, the offline run stops finding the files and reports `ENG403`.

## Triggers

The command is run on the final `head` of a task, after `eng/test`, when the change touches a path of the list below; the check is `git diff --name-only main...HEAD -- <paths>`. They are the paths on which the result depends: the documents and the sources that lychee and `examples` read, the settings of lychee, the code of the checks that read the report, the steps and the container of the command, and the files that fix the build of the examples and of the error codes.

| Path | Why |
| :- | :- |
| `README.md`, `docs/` | the examples, the links and the error codes page |
| `src/` | the XML documentation with its examples and links, and `CelErrorCode`, which the error codes page must match |
| `lychee.toml`, `.lycheeignore` | the settings and the exclusions of lychee |
| `eng/checks/` | the commands `examples`, `error-codes` and `links`, with the project and its lock file |
| `eng/findings/` | the findings that these commands return |
| `eng/run/` | the steps of the command `docs` |
| `eng/compose.yaml` | the service `docs` |
| `eng/compose.worktree.yaml` | the mounts of the service in a linked worktree ([eng-test.md](eng-test.md#linked-worktrees)) |
| `eng/docker/` | the image: the stage `docs` with the version of lychee, and the SDK |
| `global.json` | the SDK of the library build and of the examples |
| `Directory.Build.props`, `Directory.Packages.props` | the settings and the package versions of the build of step 1 |
| `.github/workflows/ci.yml` | the cell of the examples and of the external links |

Not triggers: `tests/`, `eng/tests/`, `eng/difftest/`, `eng/oracle/`, `eng/analyzers/`, `eng/min-sdk/`, `eng/packages/`, `eng/ratchet.json`, `eng/layers.txt`, `eng/BannedSymbols.txt`, `eng/stryker-control.json`, `stryker-config.json`, `.editorconfig`, `NuGet.config`, `.config/dotnet-tools.json`; the files of the root that lychee does not read — `CONTRIBUTING.md`, `SECURITY.md`, `SUPPORT.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`, `AGENTS.md`. A failing test of the checks is shown by `eng/test` on the same `head`. The external links are not checked by the run on the host: the cell of CI checks them on `main`.

## examples

Every C# example is a whole program: top-level statements, followed by the type declarations it needs.

| Source | Example | Options |
| :- | :- | :- |
| `README.md`, `docs/**/*.md` | a fenced block whose information string starts with `csharp` | words after `csharp` in the same line |
| XML documentation in `src/**/*.cs` | a `<code>` element | attributes of the element |

The documentation comments are read from the [C# sources](eng-checks-source.md#c-sources) with documentation parsing on, so the examples of members under `#if NET11_0_OR_GREATER` are found; an example found in several symbol sets is one example.

| Option in Markdown | Attribute of `<code>` | Meaning |
| :- | :- | :- |
| `net11.0` | `target="net11.0"` | the example uses the `decimal` API and is built for `net11.0`; the default target is `net10.0` |
| `skip: <reason>` | `skip="<reason>"` | the example is neither built nor run; the reason is required and counted by `suppressions` under `example` |

Expected output — one of two forms, not both:

- comments `// Output: <line>` in the lines that print; the expected output is these lines in source order;
- a `text` block directly after the example, with only empty lines between them.

An example without expected output must print nothing. Output is compared exactly; only line endings are normalized and a final line break is ignored.

How it works:

1. Every example becomes a project in a temporary directory outside the repository: `Program.cs` with `#line <line> "<path>"` before the code, so that the compiler points at the line of the Markdown or `.cs` file, and a project file — a console application for the example's target with nullable reference types, implicit usings and warnings as errors, referencing both package projects and, as the package `Great.Parts.Cel` does, the generator project as an analyzer. The project has no apphost, the program runs with `dotnet`, and no NuGet audit: an advisory concerns the packages of the library, not an example, and must not fail the build of the examples. Outside the repository the strict analysis settings of the repository do not apply: an example is checked like an application of a user.
2. All projects are listed in one generated solution and built with one `dotnet build`. Compiler errors keep their codes and their places; such a line is a finding with the code of the compiler, which `Finding.Pattern` does not match ([eng-checks.md](eng-checks.md#finding-and-output)):

```text
README.md(42,17): error CS0103: The name 'env2' does not exist in the current context
```

3. Every built program runs with a time limit of 60 seconds, a constant of the command. A program that does not end within it is stopped and reported as `ENG302`.

| Result | Code | Place |
| :- | :- | :- |
| output differs | `ENG301` | the first expected line that differs; when the program prints more, the last expected line, or the first line of the example when nothing is expected |
| exception, exit code other than 0, or the time limit | `ENG302` | the first line of the example |

```text
docs/getting-started.md(57,1): error ENG301: example output 'true' differs from expected 'false'
```

## error-codes

Checks that the page of error codes matches the members of `CelErrorCode`.

- **Members.** The command reads `src/Great.Parts.Cel/bin/Release/net10.0/Great.Parts.Cel.dll`, built by step 1, with `System.Reflection.Metadata`: the name and the constant value of every field of `Great.Parts.Cel.Diagnostics.CelErrorCode`. The summaries come from `Great.Parts.Cel.xml` next to it. Nothing of the library is loaded or run.
- **Stage.** It follows from the number, as the documentation of `CelErrorCode` states: 1000–1999 `Parse`, 2000–2999 `Check`, 3000–3999 `Evaluate`, 4000–4999 `Build`, 9000–9999 `Any`.
- **Page.** The path is given by `--page`. The table of codes is the first table of the page with the columns `Code`, `Name`, `Stage` and `When it occurs`.
- **Comparison** by name: a member without a row — `ENG303` at the first line of the table, the message names the member; a row without a member, another number, another stage, another summary, a second row of the same member or a row without four cells — `ENG303` at the row. The summary of a member is compared in the form that `--write` gives it: one line, `<c>`, `<see cref>`, `<see langword>` and `<paramref>` as code spans, `|` as `\|`, white space collapsed.
- **Input.** A missing library, XML documentation or page, a page without the table, and a member whose number is in none of the ranges of the stages are inputs that cannot be used: exit code 2, the message names the file or the member.
- **Writing.** With `--write` the command rewrites the table from the members, in the order of numbers, with the summary of each member in the last column; the rest of the page stays as it is. The page is thus built from the members and their XML documentation, and the check finds a page that was not rebuilt.

```text
docs/error-codes.md(57,1): error ENG303: row 'TimeZoneNotFound' differs from CelErrorCode: number 3021, expected 3022
```

## links

Turns a lychee report into findings.

- Every failed link of the JSON report becomes a finding at its input file. The line and column come from the report; when the report has none, the first occurrence of the address in the file gives them.
- The kind of the failure gives the code:

| Failure | Code | Level |
| :- | :- | :- |
| external link: 404 or 410 | `ENG401` | error |
| external link: the domain does not exist | `ENG402` | error |
| relative link: file not found | `ENG403` | error |
| anchor not found | `ENG404` | error |
| external link: 429, 5xx, timeout, or another failed response | `ENG409` | warning |

- lychee 0.24.2 reports a domain that does not exist and any other failed connection with the same text, `Connection failed`, without a status code. For such a link, unless it timed out, the command asks the resolver of the machine for the host, once per host, with a limit of 10 seconds, a constant of the command. Only a definite answer that the host has no address — no such host, or no address records — gives `ENG402`; any other answer, a resolver that does not answer and the limit give `ENG409`, so a failure of the network is a warning, not an error. The offline report of `eng/docs` has no external links, so only the run of CI asks the resolver. The resolver is an interface of the command, so the tests give their own and do not use the network.

```text
README.md(88,3): error ENG404: fragment '#cost-limit' not found in docs/cost.md
```

The same command reads the offline report of `eng/docs` and the report of external links in CI.
