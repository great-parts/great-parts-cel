# Governance

Great.Parts.Cel is a small project with a single maintainer. This document says how decisions are made, which roles exist and who holds them, and how the arrangement changes when more people join.

## Model

The project follows a benevolent-dictator model. The maintainer has the final say on every decision about the project. He explains the decision in the issue or pull request where it is made, so that contributors can see the reasons.

The project lives on GitHub in the [great-parts](https://github.com/great-parts) organization, in the repository [great-parts/great-parts-cel](https://github.com/great-parts/great-parts-cel). The packages are published on nuget.org.

## Roles

| Role | Who holds it | What the role does |
|---|---|---|
| Maintainer | Vladislav Nekrasov ([va-nekrasov](https://github.com/va-nekrasov)); the only maintainer today | Sets the requirements and the plan ([ROADMAP.md](ROADMAP.md)); reviews and merges pull requests; decides on public API changes and on the behavior that differs from cel-go; handles vulnerability reports ([SECURITY.md](SECURITY.md)); enforces the [code of conduct](CODE_OF_CONDUCT.md); cuts releases and owns the organization, the repository and the nuget.org packages. |
| Contributor | Anyone who opens an issue, a discussion or a pull request | Follows [CONTRIBUTING.md](CONTRIBUTING.md) and the code of conduct, signs off every commit ([Developer Certificate of Origin](CONTRIBUTING.md#developer-certificate-of-origin)), and answers review comments. A contributor has no access to merge. |
| User | Anyone who uses the packages | Reports bugs and differences from cel-go and asks questions, see [SUPPORT.md](SUPPORT.md). |

There are no other roles and no other people in them. If the list above changes, this file changes in the same pull request.

## How decisions are made

- **A change to the code or the documentation.** A pull request against `main` that passes CI and is accepted by the maintainer, as described in [CONTRIBUTING.md](CONTRIBUTING.md#how-a-change-is-made). A change the maintainer declines gets a reason.
- **A change to the public API.** Discussed in an issue before the pull request. The maintainer decides. A breaking change comes only in a major version, see [Versioning and compatibility](docs/versioning.md).
- **A difference from cel-go or cel-spec.** The maintainer decides whether it is a bug or a deliberate difference. A deliberate one is recorded with its reasons in the regression corpus, see [known-differences.md](tests/Great.Parts.Cel.Conformance/Regression/known-differences.md).
- **A release.** The maintainer decides when to release and what goes into it. The release workflow builds the packages, attests them and publishes them to nuget.org with Trusted Publishing; the changes are listed in [CHANGELOG.md](CHANGELOG.md).
- **A vulnerability.** Handled privately by the maintainer, as [SECURITY.md](SECURITY.md) describes.
- **Disagreement.** Discuss it in the issue or pull request. If there is no agreement, the maintainer decides.

## Changing this document

Propose a change in an issue or a pull request. The maintainer decides, like any other change.

## When more maintainers join

The maintainer may invite a person who has made sustained, good contributions to become a maintainer, and may remove the role from a person who has stopped taking part. A new maintainer is added to the table above, gets the access the role needs, and takes part in review and release. Until a second maintainer exists, the maintainer decides alone; once there are several, they decide by consensus, and when they cannot agree the founding maintainer decides. If the governance then needs more structure, it will be described here before it is used.
