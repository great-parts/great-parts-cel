# eng/docs

`eng/docs` checks the documentation: the C# examples of `README.md`, `docs/` and the XML documentation compile and print what they show, the error codes page matches the error codes of the library, and relative links, files and anchors exist. It is separate from `eng/test`, which stays fast. It is the service `docs` of `eng/compose.yaml` with the image stage `docs`, see [eng-test.md](eng-test.md).

## Steps

1. `dotnet build src/Great.Parts.Cel -c Release`: both targets with their XML documentation.
2. `eng/checks examples`.
3. `eng/checks error-codes --page <page>`. The path of the page is a constant of the `docs` command of `eng/run`.
4. lychee, offline:

```text
lychee --offline --include-fragments --format json --output artifacts/docs/lychee.json
       --config lychee.toml README.md docs src/**/*.cs
```

5. `eng/checks links --report artifacts/docs/lychee.json`.

- When step 1 fails, steps 2 and 3 are skipped and the command fails; steps 4 and 5 run anyway.
- The exit code of lychee is not used: the report is the result, and `links` turns it into findings.
- Exit code: 0 when every step returned 0, otherwise 1. Output: standard output of the container; the lychee report in `artifacts/docs/`.

External links are checked only in CI: the same `links` command reads a report of lychee run without `--offline`, with retries and a request cache.

`lychee.toml` in the repository root holds the settings and the exclusions. Every exclusion has its reason in the same line; `suppressions` counts them under the key `lychee`.

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

1. Every example becomes a project in a temporary directory outside the repository: `Program.cs` with `#line <line> "<path>"` before the code, so that the compiler points at the line of the Markdown or `.cs` file, and a project file — a console application for the example's target with nullable reference types, implicit usings and warnings as errors, referencing both package projects. Outside the repository the strict analysis settings of the repository do not apply: an example is checked like an application of a user.
2. All projects are listed in one generated solution and built with one `dotnet build`. Compiler errors keep their codes and their places:

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
- **Comparison** by name: a member without a row — `ENG303` at the first line of the table, the message names the member; a row without a member, another number or another stage — `ENG303` at the row.
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

```text
README.md(88,3): error ENG404: fragment '#cost-limit' not found in docs/cost.md
```

The same command reads the offline report of `eng/docs` and the report of external links in CI.
