# Release: `release.yml`, release gates and the quick-start check

A release of the packages is made by `.github/workflows/release.yml` when the maintainer pushes a tag `v*`. Two commands of [eng/checks](eng-checks.md) belong to it: `release-gates`, the checks that stand before the build and after the publication, and `quick-start`, the check that the steps of the README work on a clean machine in the time the README promises; the second one also runs in every `ci.yml` run. This page describes the workflow, the order in which a release is made, what is done with the packages 1.x after 2.0, the reports of the runs that the figures of the repository link, and both commands.

## Order of a release

The long run against cel-go runs on `main` ([Nightly run](eng-difftest.md#nightly-run)), and the gates take a run on the commit of the tag. The order below keeps the three — the run, the dry run and the tag — on one commit, X:

1. X is the last commit of the content of the release, on `main`. The `CHANGELOG.md` entry of the version is in it. The pinned commit of cel-spec is moved last, shortly before X: a commit of cel-spec master that becomes older than 6 weeks between the move and the day of the tag fails the gate `ENG905`, and moving the pin again makes a new X.
2. The run `ci.yml` and the run `difftest-main.yml` on X are green; the gate `ENG908` asks for both on the commit of the tag.
3. The long run: `difftest-nightly.yml` is started by hand (`workflow_dispatch`) on `main` while the head of `main` is X, with the budget `2h`. The workflow runs only on `main`, so the run belongs to the head of `main` at the time of the start.
4. The dry run of `release.yml` on X ([Dry run](#dry-run)), at the same time as the long run or after it.
5. The tag `v2.0.0` is set on X and pushed by the maintainer. The tag stands on X, not on the head of `main`: the gate `ENG902` asks that the commit of the tag is reachable from `main`, and the gate `ENG904` asks for a long run on the commit of the tag or on its ancestor, when no path that the differential run depends on changed between them. A commit that reaches `main` after the long run does not cancel the run while the tag stays on X; a tag on a commit that differs from the run in such a path needs another long run. A change that only sets the metadata of the packages (the README of a package and the properties of [Package metadata](#package-metadata) that do not reach the assembly) is not a change in such a path ([the paths of `ENG904`](#engchecks-release-gates)): a patch release that adds only that stands on the long run of the earlier version.
6. The run of `release.yml` on the tag, described below. It stops at the environment `release` until the maintainer approves it.

A failure before step 6 of the workflow (publication) leaves nothing public. If the fix is a commit, X changes: the long run, the dry run and the tag start again, and a tag that nothing was published for is removed and set on the new commit by the maintainer. A failed job that has a transient cause, such as an unreachable API, is run again with no commit.

## `release.yml`

```text
on:
  push:
    tags: ["v*"]
  workflow_dispatch:
    inputs:
      tag: the name of the tag the release will have, for example v2.0.0; the tag does not have to exist
```

The event has three uses: a push of a tag is the release; a `workflow_dispatch` on `main` with the input `tag` is the [dry run](#dry-run); a `workflow_dispatch` whose ref is a tag `v*` is the repeat of steps 7 and 8 ([When step 7 fails after the publication](#when-step-7-fails-after-the-publication)), and it takes no input. A dispatch on any other ref does nothing.

The tag is `v` and a semantic version. The six packages have one version, the version of the tag without `v`; it reaches the build as `-p:Version=<version>`. Every value that comes from the event or from an input reaches a script only through the environment, never through the text of the script.

| Job | Runs when | Permissions | Steps of the release | What it does |
| :- | :- | :- | :- | :- |
| `gates` | tag push; dispatch on `main` | `contents: read`, `actions: read` | 1 | checks out with the history and `origin/main`, builds `eng/checks`, runs `release-gates <tag>`; in a dry run with `--commit` the commit of the run, and findings go to the step summary and do not fail the job ([Dry run](#dry-run)) |
| `build` | after `gates` | `contents: read` | 2 | restore in locked mode, build with `ContinuousIntegrationBuild`, the tests of both targets under JIT (a project without `xunit.v3`, `Generator.Samples`, and a project with no build for the target, `Generator.Tests` on `net11.0`, are skipped, as in `ci.yml`), `eng/checks packages package-closure` (the content of the packages, and what the restore of each adds to an application; the second one restores from the packages that the first one packed and from nuget.org), `dotnet pack`, the quick-start check (the last section of this page) when the version has no pre-release suffix, the packages as an artifact |
| `attest` | tag push, after `build` | `contents: read`, `id-token: write`, `attestations: write`, `artifact-metadata: write` | 3 | the SBOM, the attestation of origin and the attestation of the SBOM for each `.nupkg` |
| `draft` | after `attest` | `contents: write`, `attestations: read` | 4 | the check of origin of the built packages, then the draft Release with the `.nupkg`, `.snupkg` and the bundles `.intoto.jsonl` |
| `publish` | after `draft` | `contents: read`, `id-token: write` | 5 and 6 | environment `release`; publishes the packages on nuget.org |
| `verify` | after `publish`; in the repeat run, alone | `contents: read`, `actions: read`, `id-token: write`, `attestations: write`, `artifact-metadata: write` | 7 | downloads the packages from nuget.org, issues the second attestation of origin for them and checks the command of the README on them |
| `release` | after `verify` | `contents: write` | 8 | adds the bundles of the second attestation to the draft Release and publishes it |

- The default permission of the workflow is `contents: read`; a job takes more only as the table says. `id-token: write` is only in `attest`, `publish` and `verify`. `pull_request_target` is not used. No job sets `timeout-minutes`: a job that hangs is stopped by the limit of GitHub Actions, 6 hours, as in the other workflows ([eng-test.md](eng-test.md#time-limits)). In the repeat run only `verify` and `release` run; the others are skipped by the condition on the event and the ref.
- Every action is pinned by the SHA of a commit with the version in a comment, as in `ci.yml`; Dependabot proposes the updates. The actions are `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`, `actions/download-artifact`, `actions/attest` and `NuGet/login`.
- **Step 3.** The SBOM is the export of the dependency graph of the repository at the commit, SPDX JSON, taken with `gh api repos/<repository>/dependency-graph/sbom`; no tool is added to make it. `actions/attest` runs twice: for the `.nupkg` files with no other input (the attestation of origin), and for the same files with `sbom-path` (the attestation of the SBOM): the action does not combine them. The bundles that the action writes (its output `bundle-path`) are copied to `<name>.intoto.jsonl` files for the Release.
- **Step 4.** The check of origin runs on the packages as they were built, before anything is public: `release-gates <tag> --provenance <directory>` ([Provenance](#provenance)). A wrong flag of the command, or an attestation that does not match the package, stops the release here, not after the publication. The Release is created as a draft with `gh release create --draft --verify-tag`.
- **Step 5.** The job `publish` has `environment: release`, which waits for the approval of the maintainer. No speed measurement stands before the approval: the result of the speed comparison is the input of the work on speed, not a condition of a release.
- **Step 6.** `NuGet/login` exchanges the OIDC token of the job for an API key that lives one hour; its input `user` is the nuget.org profile that holds the policy of Trusted Publishing (owner `great-parts`, repository `great-parts-cel`, file `release.yml`, environment `release`), kept as the variable `NUGET_USER` of the environment, not as a secret. `dotnet nuget push "artifacts/release/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key <the key of the login> --skip-duplicate`; the `.snupkg` next to each `.nupkg` is pushed by the same command. `--skip-duplicate` makes a run again safe when some of the packages were pushed and the others were not. No long-lived key exists anywhere.
- **Step 7.** nuget.org adds the signature of the repository (`.signature.p7s`) to a package, so a downloaded package is not the file that step 3 attested and `gh attestation verify` does not pass on it. The job `verify` therefore attests the downloaded file, in three sub-steps, all in the directory `nuget` of the job, the built packages being downloaded from the artifact of the build into the directory `built`:
  0. The artifact `packages` belongs to the run of the push of the tag, so `verify` first takes the id of that run: in the run of the push it is the run itself; in the repeat run it is the newest run of `release.yml` with the event `push` on the branch `<tag>` whose `head_sha` is the commit of the repeat run (`gh run list`), and the step fails if there is none. `actions/download-artifact` then takes `run-id` and `github-token`; this is why `verify` has `actions: read`. The artifact lives as long as the retention of the repository (90 days by default); after that the repeat run is not possible and the maintainer decides as for a wrong package.
  1. `release-gates <tag> --nuget <directory> --same-content-as <built directory>` downloads the six packages of the version from nuget.org, waits until it gives them, and compares the content of each with the package of the same name that the build made and step 4 checked ([Provenance](#provenance)). This is the guard of the second attestation: it is issued only for a file whose content is the content the build attested, never for whatever nuget.org serves. The comparison is by content, not by bytes of the file: a zip written again has other bytes, and the attestation cannot be asked about a copy;
  2. `actions/attest` with `subject-path: nuget/*.nupkg` issues the second attestation for the downloaded files as they are; its bundles are copied to `<name>.nuget.intoto.jsonl` and uploaded as an artifact of the run;
  3. `release-gates <tag> --provenance <directory>` runs the command of the README on the downloaded files as they are, and finds the new attestation (it is visible after some seconds, [Provenance](#provenance)).
- **Step 8.** The job `release` downloads the artifact with the bundles of the second attestation, adds them to the draft Release (`gh release upload <tag> <bundle>...`) and runs `gh release edit <tag> --draft=false`. After it the assets and the tag of the Release do not change.

### Dry run

`workflow_dispatch` on `main` with the input `tag` runs `gates` and `build` only, on the head of `main` with the version of the planned tag. The gates are the real gates: `ENG901` for the entry of that version, `ENG903` and `ENG906` for the lists, `ENG905` for today's date, `ENG904` for the long run on the head of `main`, and `ENG908` for `ci.yml` and `difftest-main.yml` on it; `ENG902` is true of the head of `main`. The build is the real build with the real version, the tests and the quick-start check included.

In a dry run the findings of the gates do not stop the build: the job `gates` writes them to the step summary of the run (the section "Gates of the dry run"), ends with a warning and succeeds when `release-gates` exits with 1, so one run shows the faults of the gates and of the build together. An input that cannot be used (exit code 2: the API does not answer, a file is missing) still fails the job. On a tag nothing changes: any finding fails `gates` and the release stops before the build.

Steps 3 to 8 do not run in a dry run: the attestations go to a public log that is never edited, the publication on nuget.org cannot be taken back, and a published Release cannot be changed. So a dry run finds the faults of the gates and of the build and creates nothing public. What a dry run does not exercise are the actions of steps 3 to 8 and the check of origin of the packages: the second is covered by the tests of `release-gates` with a replaced `gh` and, on the real tag, by step 4 before the publication.

### When step 7 fails after the publication

At that point the packages are on nuget.org and the Release is still a draft. A version cannot be pushed to nuget.org again; the workflow stops, so step 8 does not run and nothing in the Release is final. What to do depends on the sub-step of step 7 that failed:

- **Sub-step 1, 3 or 4 failed, or sub-step 2 failed because the file could not be read** (nuget.org does not give the package within 15 minutes, the service of attestations or `gh` does not answer, the new attestation is not visible yet): the cause is transient. The maintainer starts the repeat run: `workflow_dispatch` of `release.yml` with the ref set to the tag `v*` (`gh workflow run release.yml --ref <tag>`). It runs `verify` and `release` alone: it takes the packages of the build from the artifact of the run of the push of this tag, downloads the packages from nuget.org again, issues the second attestation (a second one for a file that already has it is harmless: the log keeps both and `gh` accepts either) and publishes the Release. It does not publish anything to nuget.org again and takes the same `--source-ref` as the first run, because its ref is the tag. No decision is needed.
- **Sub-step 2 failed because a file differs** (the content on nuget.org without `.signature.p7s` is not the content that step 3 attested; no second attestation was issued): the package is wrong. The maintainer decides, as the step is public and not reversible: unlist the version on nuget.org, publish the fixed content as the next version (for 2.0.0, 2.0.1), delete the draft Release.
- **Sub-step 4 failed with the second attestation issued** (the command found no attestation for the file for a reason other than time, or one of its flags does not match): the package is right and the command is wrong. The same command ran on the built packages in step 4, so a wrong flag of the command is found there, before the publication, and what is left is a change on the side of nuget.org or of GitHub. The maintainer decides: fix the command in `release-gates` and in the README, check the published packages by hand with the fixed command, publish the Release by hand with `gh release edit <tag> --draft=false`, and note the fix in the `CHANGELOG.md` of the next version.

A tag is never moved back to make the first run happen again (it is already used by the packages).

## After the release of 2.0: the packages 1.x

Versions 1.x are not supported, there is no maintenance branch for them, and 2.0 is the only release that is planned, so no release of 1.x is made ([SECURITY.md](../../SECURITY.md#supported-versions)). When 2.0 is published, the maintainer takes the packages 1.x out of use on nuget.org, by hand, on the pages of the packages after signing in, for every 1.x version of both packages:

1. **Unlist** the version, so that search and a `dotnet add package` without a version no longer find it. A version that was unlisted can still be restored by a reference to its exact number.
2. **Deprecate** the version with the reason `Legacy` and the package `Great.Parts.Cel` or `Great.Parts.Cel.Protobuf` of version 2.0 as the alternative, so that a project that still refers to it gets a warning that names the replacement.

It is done by hand because no key that can change a package exists outside `release.yml`: the publication uses Trusted Publishing (step 6 of `release.yml`), and its token lives one hour inside the job. The GitHub Releases and the tags of 1.x stay as they are; a tag is never removed or moved.

## Reports of the runs

A figure that the repository publishes — the share of the cel-spec suite, the result on the master branch of cel-spec, the result of the speed comparison — has the date of its run and a link to the report of the run, and the link works as long as the figure is published ([Conformance](../conformance.md#reports-of-the-runs)). A link to a run of GitHub Actions does not: GitHub keeps the logs and the artifacts of the runs for 90 days. So the link points to a commit of the branch `reports`:

- A workflow with the permission `contents: write` writes a report to the branch `reports` with `GITHUB_TOKEN`, after a scheduled or a manual run: a JSON file and a Markdown file with the date and the time of the run in UTC, the commit of cel-spec and the commit of this repository. The branch holds only reports, and the rules of `main` do not apply to it. Nothing is written to `main`.
- The writer is the reusable workflow `.github/workflows/publish-report.yml`, the only place of the repository that writes the branch and the only job with `contents: write` in the callers: `conformance-master.yml` (the job `publish` after the nightly run of cel-spec, with the artifact `conformance-master-report`, and the job `publish-policy` after that of CEL Policy, with the artifact `conformance-master-policy-report`) and `bench.yml` (the job `publish` after a measurement, with the artifact `bench-report`). The job that makes the report has `contents: read`; the actions of the workflows are pinned by SHA.
- The layout of the branch: `conformance-master/<yyyy-MM-ddTHHmmssZ>/report.json` and `report.md`, and the same files of the CEL Policy suite in `conformance-master-policy/`; `bench/<yyyy-MM-ddTHHmmssZ>/` with `report.json`, `report.md`, `verify.json` and `runs/<k>/`; `<directory>/latest.json`, a copy of the `report.json` of the last report, for the README and the site; `README.md` of the branch. `report.json` has `schema`, `kind`, `generatedAtUtc`, `greatPartsCelCommit`, `run` and, for the cel-spec report, `celSpecCommit`, `passed`, `total` and the lines of the cases that fail. A report is never changed after it is written; the next run adds a directory.
- The first report creates the branch as an orphan branch with its `README.md`; the push is never forced. A run that finds the branch moved starts again from its head (five attempts).
- The link of a figure is the permanent link of the file at the commit that wrote it, `https://github.com/great-parts/great-parts-cel/blob/<commit>/<file>`, never `blob/reports/…`, which moves with the branch. The figures on the pages of the documentation and in the README are taken from the reports; a report is not committed to `main`.
- A Release is not used: a published Release cannot be changed, so it cannot carry a figure that is published again every night.
- `lychee.toml` excludes the addresses of `…/actions/runs/` with the reason that the run is gone after 90 days. The exclusion stays for the links to runs that the documentation still has, for example the CI run of a figure that was made before its report; the links to the reports are checked like any other external link.

The branch exists after the first scheduled run of `conformance-master.yml` or after the first manual start of it. The figures that were made before their report link the runs until the report is there; then the link is replaced by the link of the report ([Conformance](../conformance.md#reports-of-the-runs)).

## Package metadata

The metadata of the packages comes from MSBuild properties; the values shared by the packages are in `Directory.Build.props`, the description of each package in its project under `src/`. `eng/checks packages` checks the result ([eng-checks-artifacts.md](eng-checks-artifacts.md#packages), `ENG208`).

| Property | Value | Where |
| :- | :- | :- |
| `Authors` | `The Great.Parts Authors` | `Directory.Build.props` |
| `Title` | `Great.Parts.Cel`: "Great.Parts.Cel: Common Expression Language for .NET". `Great.Parts.Cel.Protobuf`: "Great.Parts.Cel.Protobuf: Protocol Buffers support for Great.Parts.Cel". `Great.Parts.Cel.DependencyInjection`: "Great.Parts.Cel.DependencyInjection: dependency injection for Great.Parts.Cel". `Great.Parts.Cel.AspNetCore`: "Great.Parts.Cel.AspNetCore: ASP.NET Core support for Great.Parts.Cel". nuget.org does not show the title; it is in the `.nuspec` for the clients that do | the project of the package |
| `Description` | `Great.Parts.Cel`: "Common Expression Language (CEL) for .NET 10 and .NET 11: compile and evaluate CEL expressions, with a source generator for typed objects and Native AOT support." `Great.Parts.Cel.Protobuf`: "Protocol Buffers support for Great.Parts.Cel: use protobuf messages and well-known types as values in CEL expressions." `Great.Parts.Cel.DependencyInjection`: "Dependency injection for Great.Parts.Cel: register CEL environments, a compilation cache and a replaceable evaluator in Microsoft.Extensions.DependencyInjection, and validate options with CEL expressions." `Great.Parts.Cel.AspNetCore`: "ASP.NET Core support for Great.Parts.Cel: CEL expressions in authorization policies and in the validation of requests of minimal API endpoints." | the project of the package |
| `PackageReleaseNotes` | "The changes of this release are listed in `<RepositoryUrl>/blob/v<Version>/CHANGELOG.md`": a link to the file at the tag of the release, which holds the entry of the version (`ENG901`); nuget.org takes up to 35,000 characters | `Directory.Build.props` |
| `PackageIcon` | `icon.png`, the file `eng/packages/icon.png` in the root of the package; set only when the file exists ([Package icon](#package-icon)) | `Directory.Build.props` |
| `PackageProjectUrl`, `RepositoryUrl` | `https://github.com/great-parts/great-parts-cel` | `Directory.Build.props` |
| `RepositoryType` | `git` | `Directory.Build.props` |
| `PublishRepositoryUrl` | `true`: the URL is written to the `.nuspec` and to the source link of the symbols | `Directory.Build.props` |
| `PackageTags` | `cel;common-expression-language;expression;expressions;rules;validation;dotnet;nativeaot;source-generator;expression-evaluator;rules-engine;cel-spec;csharp` (the list of `Directory.Build.props`; no check compares it); `Great.Parts.Cel.Protobuf` adds `protobuf`, `Great.Parts.Cel.DependencyInjection` adds `dependency-injection`, `Great.Parts.Cel.AspNetCore` adds `aspnetcore` and `authorization` | `Directory.Build.props`, the project of the package |
| `IncludeSymbols`, `SymbolPackageFormat` | `true`, `snupkg` | `Directory.Build.props` |
| `PackageLicenseExpression`, `Copyright`, `Version` | see `Directory.Build.props` | `Directory.Build.props` |
| `PackageRequireLicenseAcceptance` | `false`: the client shows no license prompt (the default, written down on purpose) | `Directory.Build.props` |
| `EmbedUntrackedSources` | `true`: see "Source link and reproducible builds" below | `Directory.Build.props` |
| `Company`, `Product`, `NeutralLanguage` | `The Great.Parts Authors`, `Great.Parts.Cel`, `en`: attributes of the assemblies, not shown by nuget.org and not written to the `.nuspec` | `Directory.Build.props` |
| `RepositoryBranch`, `RepositoryCommit` | the SDK sets them from the checkout (see below) | not set by hand |
| `PackageReadmeFile` | `README.md`: the name of the file in the root of the package; nuget.org shows it on the page of the package | `Directory.Build.props`; the file is an item `None` of the project, below |

- `dotnet pack` writes a `.snupkg` next to every `.nupkg`: the portable PDBs of both target frameworks, with the source link to the repository. The `.nupkg` has no PDB. `release.yml` publishes the `.snupkg` with the same `dotnet nuget push`.
- **The README of a package.** The file of each package is `eng/packages/<package id>.README.md`, next to the list of the files of the package, and the project puts it into the root of the package under the name `README.md`: `<None Include="../../eng/packages/<package id>.README.md" Pack="true" PackagePath="README.md" Visible="false" />`. It is not the README of the repository: nuget.org shows no relative links and no images from domains outside its [list of trusted domains](https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images-and-badges), so the text is short — the icon, the badges, what the package is, the installation, the example of the quick start (for `Great.Parts.Cel.Protobuf`, the example of [Protobuf messages](../protobuf.md)) and absolute links to the documentation at the tag of the release, `https://github.com/great-parts/great-parts-cel/blob/v<version>/…`. The version in the links is the version of the release and changes with it. `eng/checks packages` checks that the `.nuspec` names a README and that the package has the file ([eng-checks-artifacts.md](eng-checks-artifacts.md#packages), `ENG208`), and the list of the package names it, `file README.md`.
- **Badges and pictures of the README.** Badges are images of `img.shields.io` (version and downloads of nuget.org, the status of `ci.yml`, OpenSSF Best Practices 16897, OpenSSF Scorecard, the license), with absolute addresses and a link each. The icon `eng/packages/icon.png` and one diagram per package, `eng/packages/images/great-parts-cel*-pipeline.png` (the `.svg` file beside it is its source), are linked by `https://raw.githubusercontent.com/great-parts/great-parts-cel/main/eng/packages/…`: both domains are on the list of nuget.org, the files are in the repository, and an address of `main` works before the tag of the release exists. Every image has an `alt` text. The README of a package is checked by hand for the domains; no check reads it.
- **`AGENTS.md` of a package.** The file `eng/packages/<package id>.AGENTS.md` is instructions for the coding agent of the consumer of the package, not for a contributor (those are in `AGENTS.md` of the repository): what the package is for and when not to use it, the installation, short examples, the common mistakes and the links to the documentation at the tag of the release. The project puts it into the root of the package under the name `AGENTS.md`: `<None Include="../../eng/packages/<package id>.AGENTS.md" Pack="true" PackagePath="AGENTS.md" Visible="false" />`, and the list of the package names it, `file AGENTS.md`. The NuGet MCP server (`NuGet.Mcp.Server`) returns the file with its tool `get_package_context` for a package that is restored on the machine, and the README otherwise; the server reads `AGENTS.md` from the folder of the package in the global packages folder, and the only name in its code is the file name, so the file belongs in the root of the package (no documentation of NuGet names the path; this is read from version 1.4.23 of the server). The examples of the file are compiled and run by `eng/docs` like those of `README.md` and `docs/` ([eng-docs.md](eng-docs.md#examples)), for a package that has a project in `src/`: each is a whole program, with the type declarations after the statements, and shows its output with `// Output:`. The file of a package that is not written yet is not read; its examples start to be checked when the project appears.
- **Source link and reproducible builds.** The .NET SDK reads the repository (Source Link for GitHub is part of the SDK), so with `PublishRepositoryUrl` the `.nuspec` names the repository URL and the commit, and the portable PDBs of the `.snupkg` map the code to the files of that commit. `EmbedUntrackedSources` is `true` in `Directory.Build.props`: the sources that git does not track, such as the files of the source generator, go into the portable PDBs. The SDK also writes `branch` and `commit` of the repository to the `.nuspec` by itself (`RepositoryBranch` and `RepositoryCommit` are not set by hand: the values come from the checkout that built the package, and a fixed branch would be wrong for a build from a tag). The build is deterministic (`Deterministic`) by default; `ContinuousIntegrationBuild` is set in the workflows (`GITHUB_ACTIONS`), so the paths of the machine do not reach the PDBs.
- **Trimming and Native AOT.** `IsAotCompatible` in the project of each package turns on the trimming and AOT analyzers, makes the assembly trimmable and writes the metadata of the assembly that the tools of trimming read; nothing else is set.

### Package icon

The icon is one file for all packages, `eng/packages/icon.png`, drawn and approved apart from the build. `Directory.Build.props` sets `PackageIcon` to `icon.png` and packs the file into the root of the package when the file exists; a build without the file has no icon and no warning. The file comes with the line `file icon.png` in every list of `eng/packages`, so `ENG203` and `ENG204` hold on both sides.

| Requirement | Value | Source |
| :- | :- | :- |
| format | PNG (JPEG is accepted, PNG has the transparency) | [nuspec reference, icon](https://learn.microsoft.com/en-us/nuget/reference/nuspec#icon) |
| size of the file | at most 1 MB | the same |
| image | square, 128 × 128 pixels, a transparent background; nuget.org scales it for every client | [package authoring best practices](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices#icon) |
| property | `PackageIcon`, a path relative to the root of the package; the file is an item `None` with `Pack="true"` | [pack target, icon](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#packing-an-icon-image-file) |
| not used | `PackageIconUrl` is deprecated; the SDK warns when it is the only icon property (NU5048) | the same |

## `eng/checks release-gates`

```text
eng/checks release-gates <tag> [--commit <sha>] [--date <yyyy-MM-dd>] [--cel-spec <repository>] [--repository <owner/name>] [--api <url>]
eng/checks release-gates <tag> --provenance <directory> [--repository <owner/name>]
eng/checks release-gates <tag> --nuget <directory> [--same-content-as <directory>] [--repository <owner/name>]
```

`<tag>` is the name with `v`; a name that is not `v` and a semantic version is exit code 2, as it is for any input that cannot be used ([eng-checks.md](eng-checks.md#finding-and-output)). Findings have the form of the other commands; a finding without a file names the tag, `tag/<name>(1,1)`.

| Option | Meaning |
| :- | :- |
| `--commit <sha>` | the commit to check instead of the commit of the tag; the dry run gives the commit of the run, because the tag does not exist |
| `--date <yyyy-MM-dd>` | the date of the release for `ENG905`; without it today, UTC |
| `--cel-spec <repository>` | the address or the path of the repository of cel-spec for `ENG905`; without it `https://github.com/cel-expr/cel-spec.git`; the tests give a temporary repository |
| `--repository <owner/name>` | the repository of the workflow runs and of the origin; without it the environment variable `GITHUB_REPOSITORY`, and without that `great-parts/great-parts-cel` |
| `--api <url>` | the address of the API of GitHub; without it `https://api.github.com`; the tests give a stub |
| `--provenance <directory>` | the mode of the check of origin of every `.nupkg` of the directory ([Provenance](#provenance)); the gates below do not run in it |
| `--same-content-as <directory>` | with `--nuget`: after the download compare each package with the package of the same file name in the directory (the packages of the build); without `--nuget` it is exit code 2 |
| `--nuget <directory>` | the mode of the download: the six packages of the version of the tag from nuget.org into the directory ([Provenance](#provenance)); the gates below do not run in it |

All gates run in one invocation and every finding is printed; the exit code is 1 when any of them is an error. A gate whose input is missing or cannot be read — a file, the repository, the API — is exit code 2 with the message on standard error.

| Code | What it checks | Where the data comes from | Place |
| :- | :- | :- | :- |
| `ENG901` | `CHANGELOG.md` has the entry of the version: the heading `## [<version>] - <yyyy-MM-dd>`, where `<version>` is the tag without `v`, and a non-empty text up to the next `## ` heading | `CHANGELOG.md` of the checked commit | `CHANGELOG.md(1,1)` |
| `ENG902` | the commit of the tag is reachable from `main` | `git merge-base --is-ancestor <commit> origin/main` in the checked-out repository; the workflow fetched `origin/main` (a missing `origin/main` is exit code 2) | `tag/<name>(1,1)` |
| `ENG903` | no record of the regression list is an unresolved divergence signature (`Verdict` is `pending`) or an open one of the highest severity (`Verdict` is `our-bug` and `Severity` is `S1`) | `tests/Great.Parts.Cel.Conformance/Regression/skips.md`, its columns read by header name | `tag/<name>(1,1)`, one finding per record, the message names the signature and the severity |
| `ENG904` | a successful long run on the commit of the tag, or on its ancestor with no change of the code since, exists | the API of GitHub Actions: the runs of the workflow `difftest-nightly.yml` with the event `workflow_dispatch` and the status `success`, without a filter by commit; the runs whose `head_sha` is the commit of the tag or its ancestor (`git merge-base --is-ancestor`; a commit that is not in the checkout, such as that of a branch that was not fetched, does not count), the closest to the tag first (`git rev-list --count <run>..<commit>`); for each run the jobs, and the job `Differential run` with the conclusion `success` and the time from `started_at` to `completed_at` of at least 7 200 s; a run on the ancestor counts when `git diff --name-only <run> <commit>` names no path of the list below, where a change of `Directory.Build.props` or of a project `src/<name>/<name>.csproj` that only touches the metadata of the packages is not a path of the list (the second bullet below). The first run that counts passes the gate. The history is whole in the workflow `gates` (`fetch-depth: 0`) | `tag/<name>(1,1)`; without a run the message names the commit of the tag; when a long run on an ancestor exists but the code changed since, the message names the closest such run and the changed paths of the list |
| `ENG905` | the pinned commit of cel-spec includes every commit of master older than 6 weeks | `tests/cel-spec/COMMIT`, a clone of the repository of cel-spec (`git clone --bare --filter=blob:none`, the branch `master`) in a temporary directory; the gate fails when `git rev-list <pinned>..master --before=<date − 42 days, 00:00 UTC>` is not empty, and the message names the newest such commit with its date; a pinned commit that is not in the history of `master` fails it too | `tests/cel-spec/COMMIT(1,1)` |
| `ENG803` | every record of the lists of the cel-spec suite and of the CEL Policy suite has a reason and a link `https://github.com/great-parts/great-parts-cel/issues/<N>` | `tests/Great.Parts.Cel.Conformance/CelSpec/skips.md` and `tests/Great.Parts.Cel.Conformance/CelPolicy/skips.md`, the columns `Reason` and `Issue` | the line of the record in its own list |
| `ENG906` | in the version `1.0.0` every record of the list of the cel-spec suite has a `Reference`; the gate does not apply to another version, and an empty list passes | the same file, the column `Reference` | the line of the record |
| `ENG907` | the packages of the release have the attestation of origin: see [Provenance](#provenance) | the packages, `gh` | `tag/<name>(1,1)` |
| `ENG908` | `ci.yml` and `difftest-main.yml` are green on the commit of the tag: for each, a run of the event `push` with the `head_sha` of the commit and the status `success` exists | the API of GitHub Actions: the runs of the workflow with `event=push&head_sha=<commit>` | `tag/<name>(1,1)`, one finding per workflow |

- **`ENG904`.** The input `budget` of a manual run is not in the answer of the API, so the length of the job is the only evidence that the run was long. The job is longer than the budget by the time of the build of the image. A run of the event `schedule`, the nightly one of 20 minutes, never counts. The token is `GITHUB_TOKEN` of the job (permission `actions: read`); without a token the requests are anonymous. An API that does not answer is exit code 2: a transient failure is a run again, not a missing long run.
- **The paths of `ENG904`.** The files that the differential run depends on, a prefix of the path of a changed file (the list is `CodePaths` of `LongRunGate`): `src/`, `eng/difftest/`, `eng/findings/` (referenced by `eng/difftest`), `eng/run/` (the command of the run), `eng/oracle/` (cel-go and its launcher), `eng/docker/` and `eng/compose.yaml` (the images and the services of the run), `tests/cel-spec/` (the proto files that `eng/difftest` compiles), `tests/Great.Parts.Cel.Conformance/Runner/` (the sources that `eng/difftest` takes), `tests/Great.Parts.Cel.Conformance/Regression/` (the registry of known signatures that decides what is new), `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `NuGet.config` (the packages and the SDK of the build) and `.github/workflows/difftest-*.yml`. The gates themselves (`eng/checks`, `eng/tests`), `release.yml`, `ci.yml`, the documents and the other files are not in the list: they do not change what the run does, and the commit of the tag is checked by `ci.yml` and `difftest-main.yml` in `ENG908`.
- **The metadata of the packages in the paths of `ENG904`.** `Directory.Build.props` and every project `src/<directory>/<name>.csproj` are in the list as a whole, and they also hold the metadata of the packages, which the run does not depend on. A changed file of these two kinds does not count as a changed path when, in the XML of the commit of the run and of the checked commit, nothing but the metadata differs. Both versions are read with `git show <commit>:<path>` and normalized: the comments are removed; in a `PropertyGroup` directly under `Project`, the elements `PackageReadmeFile`, `PackageIcon`, `PackageTags`, `PackageProjectUrl`, `PackageReleaseNotes`, `PackageLicenseExpression`, `PackageRequireLicenseAcceptance` and `EmbedUntrackedSources` (it changes the portable PDB only) are removed; in an `ItemGroup` directly under `Project`, an element `None` with `Pack="true"` is removed (the file that the package carries, the README of the package); the groups without elements are removed. The files count as one change in the code when the rest of the two versions is not equal, when a version does not exist (a new or a deleted file) or is not XML. The list of the properties is closed on purpose: `Description`, `Authors`, `Copyright`, `Version`, `Company`, `Product` and `NeutralLanguage` are not in it because they reach the attributes of the assembly, and a property of the compiler, of the build or of the packages that the build restores changes the code of the run; a target, a `Compile` item, a reference or a condition on a group are in the rest and are compared. The paths `src/**.cs` and the other paths of the list are not touched by this rule.
- **`ENG905`.** The clone and the API-free comparison need no token and no limit of requests. The date of the release is the date of the run of the gate, so the pin has to hold on the day of the tag ([Order of a release](#order-of-a-release)).
- **Lists.** The reader of the two lists is part of the command: a Markdown table whose columns are found by the names of the header row, as the format of the lists defines them ([conformance.md](conformance.md)). It does not share code with the runner of the suites, which is a different assembly; a test reads both lists of the repository with it.
- **Code and tests.** The code of the gates is in the directory `ReleaseGates/` of `eng/checks`, one class per gate; the codes `ENG803` and `ENG901`–`ENG908` are constants of `EngCodes` ([eng-checks.md](eng-checks.md#one-source-of-the-eng-codes)). The API of GitHub and `gh` are behind interfaces so that the tests give their own answers.

### Provenance

`release-gates <tag> --provenance <directory>` takes every `.nupkg` of the directory (not subdirectories), and for each one:

1. runs `gh attestation verify <file> --repo <repository>` with the flags of `ProvenanceFlags` on the file as it is;
2. a non-zero exit code of `gh` is repeated 5 times, 15 seconds apart (6 calls in all), because a new attestation is not visible at once. The output of `gh` does not tell "not found" from another cause, so every non-zero code is repeated; a wrong flag costs 75 seconds of waiting and then fails, and a wrong flag is caught earlier by step 4, before the publication;
3. after the last attempt a non-zero exit code is `ENG907` with the name of the package and the first line of the output of the last attempt.

`release-gates <tag> --nuget <directory>` downloads the six packages of the version of the tag (the ids `Great.Parts.Cel`, `Great.Parts.Cel.Protobuf`, `Great.Parts.Cel.DependencyInjection`, `Great.Parts.Cel.AspNetCore`, `Great.Parts.Cel.Policy` and `Great.Parts.Cel.Filtering`, one constant list, `NuGetDownload.PackageIds`) from nuget.org (`https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.<version>.nupkg`, id and version in lower case) into the directory, as nuget.org gives them, with the signature of the repository inside. It asks again every 30 seconds for up to 15 minutes while nuget.org does not give a package yet; a package that does not appear is `ENG907`. With `--same-content-as <directory>` it then compares each downloaded package with the package of the same file name (case ignored) in that directory: the sets of entries without `.signature.p7s` are equal, and each entry has the same decompressed bytes. A package with no counterpart, an entry that differs, a missing or an extra entry (other than `.signature.p7s`) is `ENG907` with the name of the package and of the first such entry.

`gh` that cannot be started is exit code 2. `ProvenanceFlags` is the constant of the flags that make the check exact, and it is one for the workflow and for the README:

```text
--repo <repository> --signer-workflow <repository>/.github/workflows/release.yml --source-ref refs/tags/<tag> --deny-self-hosted-runners
```

`--signer-workflow` names the workflow that issued the attestation, `--source-ref` the tag the release was built from, `--deny-self-hosted-runners` refuses a build on a runner that is not standard. The code builds the flags from `<repository>` and `<tag>`; the README shows the same line with the values of the release.

The command of the README is `gh attestation verify <package> <ProvenanceFlags>` on the downloaded file as it is, one command with no preparation. Step 7 of the release runs the same command on the same kind of file (sub-step 3), so a change of the flags is a change of the README and of the constant at once.

Two attestations of the origin exist for a published package, both issued by `release.yml` for the same tag: the one of step 3 for the file that the build made, and the one of step 7 for the file nuget.org gives. The user's command finds the second. The first stays for the check of step 4 and for the Release, whose assets are the files of the build.

### Tests

`eng/tests/Checks/ReleaseGatesTests.cs` runs the command on temporary repositories and with replaced API and `gh`. Every gate has a red and a green case:

| Gate | Red | Green |
| :- | :- | :- |
| `ENG901` | the file without the entry of the version; the entry with an empty text | the entry with a text |
| `ENG902` | a tag on a commit of a branch that is not `main` | a tag on an older commit of `main` |
| `ENG903` | a record with `pending`; a record with `our-bug` and `S1` | a record with `our-bug` and `S2`; a record with `by-design` and `S1` |
| `ENG904` | a run of 1 h 50 min; a run that did not succeed; a run of the event `schedule` of 2 h 10 min; a run on a commit that is not the tag or its ancestor; a run of 2 h 5 min on an ancestor when `src/`, `eng/difftest/`, `eng/oracle/` or another path of the list changed since; a run of 1 h 50 min on an ancestor with no change of the code; a run of 2 h 5 min on an ancestor when `Directory.Build.props` or a project under `src/` changed beyond the metadata of the packages (the value of `Nullable`, `Description` or `TargetFrameworks`, a new `Version`, a new item other than `None` with `Pack="true"`, a new target, a file that is not XML, a new file, a project of the depth other than `src/<directory>/<name>.csproj`) | a successful `workflow_dispatch` run of 2 h 5 min on the commit; the same run on an ancestor when only documents, `eng/checks`, `eng/tests` or `release.yml` changed since; the same run when only the metadata of the packages changed in `Directory.Build.props` or in a project under `src/` (`PackageReadmeFile`, `PackageTags`, a `None` item with `Pack="true"`, a comment); of two runs on ancestors, the closest one without a change of the code is taken |
| `ENG908` | the run of `ci.yml` or of `difftest-main.yml` that did not succeed; both runs on another commit | successful `push` runs of both workflows on the commit |
| `ENG905` | a temporary cel-spec repository with a commit older than 6 weeks after the pinned one | the pinned commit is the newest commit older than 6 weeks; commits newer than that are not required |
| `ENG803` | a record without a reason; a link to another repository; an empty link | a record with a reason and a link to the repository |
| `ENG906` | the tag `v1.0.0` and a record with an empty `Reference` | the tag `v1.0.0` and an empty list; the tag `v1.0.1` and a record with an empty `Reference` |
| `ENG907` | a replaced `gh` that exits with an error on every call (6 calls, 5 waits of 15 s); a package that nuget.org does not have (an answer 404 for 15 minutes, with a replaced clock); with `--same-content-as`, a downloaded package with a changed entry, with a missing entry, with an extra entry, or with no counterpart | a replaced `gh` that exits with an error 2 times and then with 0; a replaced `gh` that exits with an error 5 times and passes on the sixth call; a replaced `gh` that exits with 0, and the file it received is byte for byte the downloaded file with its `.signature.p7s`; with `--same-content-as`, a downloaded package that differs from the built one only by `.signature.p7s` and by the bytes of the zip; the flags it received are the line of `ProvenanceFlags` for the tag |

The tests of invocation: a tag that is not a version, a missing `origin/main`, a missing list and an API that does not answer are exit code 2.

## `eng/checks quick-start`

```text
eng/checks quick-start --packages <directory> --image <image>
```

The command runs the steps of the quick start of `README.md` in a clean container with the .NET SDK 10 and reports whether they work and how long they take.

- **The steps.** They are the block of the README between the lines `<!-- quick-start:begin -->` and `<!-- quick-start:end -->`: fenced blocks of shell commands, one block with the C# program, and the command that runs it. The command runs the steps as they are written; it substitutes nothing in the text. The format of the block is below. A README without the block, or a block that cannot be read, is exit code 2.
- **The package.** `<directory>` has one stable `Great.Parts.Cel` package built from the checked commit (a version with no suffix, so that `dotnet add package Great.Parts.Cel` without a version takes it); a directory without it is exit code 2. `ci.yml` builds it with `dotnet pack src/Great.Parts.Cel -c Release -p:Version=1.0.0`; `release.yml` passes the packages of the release, for a version that has no pre-release suffix.
- **The container.** `<image>` is `mcr.microsoft.com/dotnet/sdk:10.0.404`, the tag of the stage `sdk-min` of the container file, the oldest SDK 10 on which the generator of the package loads ([min-sdk](eng-test.md#min-sdk)); the two tags change together. The container is new for every run, so it has no package cache and no build server of an earlier run. It starts with `--network none`: the steps need nothing from the network, and a step that does reaches no source other than the local one. The only difference from the machine of a reader is the source of the package: `/work/NuGet.config` with `<clear />` and one source, the mounted directory of packages. The working directory of the session is `/work`.
- **The session.** One `bash` session in the container runs the commands of the block in order, with a common current directory. The `csharp` block is written to `Program.cs` of the current directory where it stands in the README.
- **The time.** One stopwatch from the start of the first command to the end of the last one. It does not count the pull of the image, the start of the container and the build of the package. The limit is 300 seconds. A session that does not end within 10 minutes is killed.
- **The result.**

| Situation | Code | Place |
| :- | :- | :- |
| a command exits with a non-zero code; the session is killed at 10 minutes; the end of the output of the last command is not the expected output of the example | `ENG304` | the line of the command in `README.md`; for the output, the first line of the expected output |
| the time of a run in which every step passed is more than 300 s | `ENG305` | the line of the begin marker |

A failed step is only `ENG304`: the time of a run that did not finish is not judged. The message of `ENG304` has one line of the output of the failed command: the first line that contains `error` (any case), looked for in the error output and then in the standard output (`dotnet` prints some errors to the standard output); without such a line, the last non-empty line of the error output, or of the standard output when the error output is empty. After the run the container is removed, whatever the result.

```text
README.md(14,1): error ENG304: step 'dotnet add package Great.Parts.Cel' exited with code 1: error: Unable to find package 'Great.Parts.Cel'
README.md(10,1): error ENG305: the quick start took 412 s, the limit is 300 s
```

### Format of the block

- A block is one pair of the marker lines; a README has one block. A missing or unpaired marker, a second block or a block of code of the kinds below that cannot be read is exit code 2.
- Inside, the command reads only fenced blocks of code, with the rules of fences of the check of the examples (three or more backticks or tildes); the text around them is for the reader. The kind of a block is the first word of its information string:
  - `shell`: every non-empty line that does not start with `#` is one command. The commands of all blocks `shell` run in order in one session.
  - `csharp`: exactly one block, with no word after `csharp`. Its text goes to `Program.cs`. The expected output is read as in [eng-docs.md](eng-docs.md#examples): the comments `// Output: <line>` in the lines that print, or a `text` block directly after the example. A block with no expected output is a block that cannot be read (exit code 2): the quick start has to show what the reader will see.
  - other kinds, such as `xml`, are shown to the reader and not run.
- The last command of the last `shell` block runs the example (`dotnet run`). The expected output is the final lines of its standard output: lines are compared one by one, line endings are normalized, the final line break is ignored, and the lines before them, the messages of the build, are not compared. The exact comparison of the whole output of the program is the check of the examples ([eng-docs.md](eng-docs.md#examples)).

An example of the block:

````text
<!-- quick-start:begin -->
```shell
dotnet new console -o QuickStart
cd QuickStart
dotnet add package Great.Parts.Cel
```
```csharp
// the example, with its `// Output:` comments
```
```shell
dotnet run
```
<!-- quick-start:end -->
````

### Where it runs

- `ci.yml`, the job `Quick start`, on every push to `main` and every pull request: checks out, sets up .NET as the other jobs do, builds `eng/checks`, packs the library into `artifacts/quick-start/packages`, runs `quick-start` with the image from the variable `QUICK_START_IMAGE` of the job. The permission is `contents: read`, the actions are pinned by SHA, the job sets no time limit of its own (the check itself ends a session at 10 minutes).
- `release.yml`, the job `build`: the same command on the packages of the release, before they are attested.

### Code and tests

The code is the directory `QuickStart/` of `eng/checks`: the reader of the block, the runner of the session and the command; the codes `ENG304` and `ENG305` are constants of `EngCodes`. The container and the clock are behind interfaces. `eng/tests/Checks/QuickStartTests.cs` runs the command with a replaced container and a replaced clock, so it needs no Docker:

| Case | Result |
| :- | :- |
| a command exits with 1 | `ENG304` at the line of the command |
| the end of the output is not the expected output | `ENG304` at the first expected line |
| the session is killed at 10 minutes | `ENG304` |
| the time is 301 s | `ENG305` |
| the time is 300 s | no finding |
| the output has build messages before the expected lines | no finding |
| a README without the block, with an unpaired marker or with two blocks | exit code 2 |
| a directory without a stable package | exit code 2 |
