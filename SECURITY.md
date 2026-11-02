# Security policy

## Reporting a vulnerability

Do not report a vulnerability in a public issue, pull request or comment.

Report it privately on GitHub with the form [Report a vulnerability](https://github.com/great-parts/great-parts-cel/security/advisories/new). The same form opens from the security tab of the repository. Only you and the maintainers of the repository see the report.

Expressions often come from users of an application and cannot be trusted. If an expression or its input makes the library crash the process, report it here as a vulnerability, not in a public issue.

The library sets no cost limit by default: without a limit an evaluation is not bounded, so a long evaluation or one that uses a lot of memory is not a vulnerability by itself. An application that evaluates untrusted expressions sets a cost limit in the evaluation options. If an evaluation hangs or exhausts memory although the application set such a limit, report it here as a vulnerability.

Please include:

- the commit or, after a release, the package version;
- the target framework (`net10.0` or `net11.0`), JIT or Native AOT, and the operating system;
- the expression and the input, or the smallest code, that show the problem;
- what happens and what impact you see.

## What happens next

| Step | Time from the report |
| :- | :- |
| The report is acknowledged | up to 7 days |
| The report is assessed: whether it is a vulnerability and how severe it is | up to 14 days |
| A vulnerability of medium or high severity is fixed | up to 60 days |

The details stay private until a fix is available. Then the vulnerability is published as a GitHub security advisory of this repository, with a CVE identifier when one is assigned.

## Supported versions

No version has been released yet. Until the first release, fixes go to the `main` branch only.

After releases, fixes are released for the latest minor version. Security fixes are also released for the previous major version for 6 months after the next major version comes out.
