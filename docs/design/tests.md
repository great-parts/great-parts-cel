# Test projects

The tests of the library: `tests/Great.Parts.Cel.Tests` and `tests/Great.Parts.Cel.Properties`. The rules on requirement traits, on the `Examples/` directories and on the test framework hold for every test project of the repository, including the suite runner `tests/Great.Parts.Cel.Conformance` and `eng/tests` ([eng-tests.md](eng-tests.md)).

## Projects

| Project | Targets | Modes | Packages | Contents |
| :- | :- | :- | :- | :- |
| `tests/Great.Parts.Cel.Tests` | `net10.0`, `net11.0` | JIT, Native AOT | `xunit.v3.aot` 4.0.2 | unit tests, one directory per namespace of the library; `Examples/` |
| `tests/Great.Parts.Cel.Properties` | `net10.0`, `net11.0` | JIT | `xunit.v3.aot` 4.0.2, `CsCheck` 4.9.1 | properties without an oracle |
| `tests/Great.Parts.Cel.Conformance` | `net10.0`, `net11.0` | JIT, Native AOT | see the description of the suite runner | the cel-spec suite and the regression corpus |
| `eng/tests` | `net10.0` | JIT | see [eng-tests.md](eng-tests.md) | tests of the own checks |

Common settings:

- `dotnet test` runs on the test platform that `global.json` selects; every project uses the same version of `xunit.v3.aot`, under JIT as well.
- Tests run in two ways: the command `eng/test` runs the xUnit.net runner inside every test module — `dotnet <module>.dll` under JIT once, and for the two published projects also the executable under Native AOT — each with `-result-xml`, so that `test-count` compares two reports of the same kind ([eng-test.md](eng-test.md)); a narrow run in the container, `dotnet test` on Microsoft Testing Platform, is a tool of the developer and is not part of the check.
- The library grants `InternalsVisibleTo` to `Great.Parts.Cel.Tests`, `Great.Parts.Cel.Properties` and `Great.Parts.Cel.Conformance` only.
- Test projects set `IsPackable=false` and turn off, each with a reason, `CS1591` — tests have no XML documentation — and `CA1707` — test method names are `<Situation>_<Expectation>`.
- `ImplicitUsings` is off, as in the library: every `using` is written out, and `IDE0005` rejects the unused ones.
- Until the library supports cancellation, tests pass `CancellationToken.None` to evaluation: a token that can be canceled throws `NotImplementedException` ([facade.md](facade.md)), and the analyzers of the test framework require passing a token.
- Tests of the code for `net11.0` only, such as `decimal`, are compiled under `#if NET11_0_OR_GREATER`.
- A test class is named `<Subject>Tests` and lives in the directory of its subject, for example `tests/Great.Parts.Cel.Tests/Values/CelValueEqualityTests.cs`. A test method is named `<Situation>_<Expectation>`, for example `NaN_NotEqualToItself`.

## Requirement trait

A test that checks a requirement of the library names it with the xUnit trait `Requirement`:

```text
[Fact]
[Trait("Requirement", "<code>")]
public void <Situation>_<Expectation>()
```

- **Name.** The attribute is the standard `Xunit.TraitAttribute`; the trait name is always `Requirement`. The value is visible in the reports and selects tests with `--filter-trait "Requirement=<code>"`.
- **Argument.** One requirement code per attribute, a string written exactly as in the requirement catalog: `<category>-<number>`, where the category is `F`, `U`, `R`, `P`, `S` or `+R` and the number has no leading zeros. A requirement of the whole system has the prefix `system-`: `system-<category>-<number>`.
- **Several codes.** One attribute per code:

```text
[Trait("Requirement", "<code 1>")]
[Trait("Requirement", "<code 2>")]
```

- **Place.** On a test method, or on a test class when every test of the class checks the same requirement.
- **Only requirement codes, only in the trait.** The trait holds a requirement code and nothing else: no code of an architecture decision record, no other identifier. In a test, a requirement code appears only in this trait, not in names or comments. The rest of the repository — the library code and its XML documentation, documentation, messages, diagnostics, output of checks and data files — has no requirement codes at all.

## Examples/

`tests/<project>/Examples/` holds only examples of behavior: tests that were written from the specification of the library and state the behavior it promises. Developer tests are never placed there.

- An example is the specification of the behavior. When an example fails, the library is wrong; a change of the library does not change an example. An example changes only together with the behavior it states.
- Every example names what it checks: a requirement with the `Requirement` trait, or a row of a published table with the value it asserts — for an error code, the `CelErrorCode` member.
- Examples may use internal entry points of the library through `InternalsVisibleTo`. They do not use helpers of developer tests, so a change of those helpers does not change an example.
- The namespace follows the directory: `Great.Parts.Cel.Tests.Examples` and nested namespaces by subject.
- A long table of example data is one string constant rather than many `InlineData` attributes: the duplicate check counts tests too ([eng-checks-source.md](eng-checks-source.md#duplicates)).
- Examples run with all other tests of the project, under JIT and, in projects published under Native AOT, under Native AOT.

## Property tests and the seed

Properties use `Gen<T>.Sample` of CsCheck 4.9.1.

- Every property passes the same seed: the constant `PropertySettings.Seed` in `tests/Great.Parts.Cel.Properties/PropertySettings.cs`, a CsCheck seed string. Every property also sets the number of iterations and sets no time limit, so a run checks the same cases on every machine and the mutation score repeats.
- A change of the seed is a change of that file.
- CsCheck also reads the environment variables `CsCheck_*`; they are default values for the parameters that a property does not pass. `CsCheck_Time` and `CsCheck_Threads` therefore apply to every property, as none passes them, while the seed and the number of iterations that every property passes explicitly win over their variables. The containers get none of these variables from the host.
- A failure message of CsCheck contains the seed and the smallest failing case. The case is reproduced with that seed and becomes a unit test with fixed input.

## Same tests under JIT and Native AOT

`Great.Parts.Cel.Tests` and `Great.Parts.Cel.Conformance` are published under Native AOT and run as executables: in `eng/test` and in CI, on linux-x64, for both targets ([eng-test.md](eng-test.md)). `eng/checks test-count` compares the tests of each published run with the JIT run of the same commit, operating system and target ([eng-checks-artifacts.md](eng-checks-artifacts.md)). The other test projects run only under JIT.

Rules that keep the two sets equal:

- No generic test methods: the test framework does not run them under Native AOT.
- Test data are known when the project is built: inline data and static members, not data found by reflection at run time.
- A test does not skip itself, change its name or its data depending on the mode. `#if` around tests depends only on the target framework.
- The display name of a theory case does not depend on the culture of the process: the JIT run uses ICU, the published run the invariant culture.

Project settings:

- `PublishAot=true` and `RuntimeIdentifiers` with `linux-x64` are in the project files of the two projects. Restore is then the same for JIT and Native AOT: the publish runs with `--no-restore` after a restore without a runtime identifier and changes no lock file, neither of the project nor of the projects it references.
- `DynamicCodeSupport=true` is in the same project files for every build except the publish. With `PublishAot=true` the targets of the Native AOT compiler set `DynamicCodeSupport=false` for every build, and the SDK writes it into the runtime configuration: the JIT run would then run without dynamic code, as the published one, and the conformance runner would name its cell `native-aot` ([conformance.md](conformance.md#skip-lists-and-findings)). The publish keeps `false`.
- `PublishDocumentationFile=false` is in the same project files, with its reason: the Native AOT publish of the SDK looks for the XML documentation file in the runtime output, where the build does not put it, and fails with `MSB3030`. A test executable needs no documentation file; `GenerateDocumentationFile` stays `true`.
- `InvariantGlobalization=true` is given only on the `dotnet publish` command line. The JIT run keeps ICU, the published run uses the invariant mode, and the same expectations hold in both.

Under Native AOT the stack trace of a failed test is cut down: the console and the `<stack-trace>` element of the report show the name of the test and the message, and for a thrown exception the method and an offset, but no file and line. The file and line are in the report of the run: the attributes `source-file` and `source-line` of its `<test>` element, which the test framework records at build time — the file and the line of the `[Fact]` or `[Theory]` attribute, with the absolute path as the build saw it. If a report of a published run has no source for a test, the file and line are found by the test name in the PDB of the JIT build of the same commit, as `test-count` does.
