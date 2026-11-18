# Security policy

## Reporting a vulnerability

Do not report a vulnerability in a public issue, pull request or comment.

Report it privately on GitHub with the form [Report a vulnerability](https://github.com/great-parts/great-parts-cel/security/advisories/new). The same form opens from the security tab of the repository. Only you and the maintainers of the repository see the report.

Expressions often come from users of an application and cannot be trusted. If an expression or its input makes the library crash the process, report it here as a vulnerability, not in a public issue.

The library sets no cost limit by default: without a limit an evaluation is not bounded, so a long evaluation or one that uses a lot of memory is not a vulnerability by itself. An application that evaluates untrusted expressions sets a cost limit in the evaluation options. If an evaluation hangs or exhausts memory although the application set such a limit, report it here as a vulnerability. The limit is `CelEvalOptions.CostLimit`; see [Limits](docs/limits.md#type-checking-and-evaluation).

The threat model, the trust boundaries and the measures against common weaknesses, with what remains the duty of the application, are on the page [Assurance case](docs/assurance-case.md).

Please include:

- the package version, or the commit for a build from the repository;
- the target framework (`net10.0` or `net11.0`), JIT or Native AOT, and the operating system;
- the expression and the input, or the smallest code, that show the problem;
- what happens and what impact you see.

## What happens next

| Step | Time from the report |
| :- | :- |
| The report is acknowledged | up to 7 days |
| The report is assessed: whether it is a vulnerability and how severe it is | up to 14 days |
| A vulnerability of medium or high severity is fixed | up to 60 days |

The times apply to a vulnerability in a supported version ([Supported versions](#supported-versions)).

The details stay private until a fix is available. Then the vulnerability is published as a GitHub security advisory of this repository, with a CVE identifier when one is assigned. The advisory has a GHSA identifier.

## Where a fixed vulnerability is announced

The release that holds the fix is the first one that names the vulnerability. Its entry in [CHANGELOG.md](CHANGELOG.md) has a section `### Security` that names the advisory by its GHSA identifier, and by its CVE identifier when one is assigned, and says what is affected and what the fix changes. A release that fixes no vulnerability has no such section.

## Supported versions

Versions 1.x are not supported: there are no fixes for them, security fixes included, and there is no maintenance branch. When 2.0 comes out, the packages 1.x are unlisted and marked as deprecated on nuget.org.

From release 2.0 only the latest release is supported. Fixes, security fixes among them, come in a new release; earlier releases get none.

The versions follow [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html); see [Versioning and compatibility](docs/versioning.md).
