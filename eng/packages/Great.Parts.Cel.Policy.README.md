# Great.Parts.Cel.Policy

![Great.Parts.Cel logo](https://raw.githubusercontent.com/great-parts/great-parts-cel/main/eng/packages/icon.png)

[![NuGet version](https://img.shields.io/nuget/v/Great.Parts.Cel.Policy)](https://www.nuget.org/packages/Great.Parts.Cel.Policy)
[![NuGet downloads](https://img.shields.io/nuget/dt/Great.Parts.Cel.Policy)](https://www.nuget.org/packages/Great.Parts.Cel.Policy)
[![CI status](https://img.shields.io/github/actions/workflow/status/great-parts/great-parts-cel/ci.yml?branch=main&label=ci)](https://github.com/great-parts/great-parts-cel/actions/workflows/ci.yml)
[![OpenSSF Best Practices](https://img.shields.io/cii/level/16897)](https://www.bestpractices.dev/projects/16897)
[![OpenSSF Scorecard](https://img.shields.io/ossf-scorecard/github.com/great-parts/great-parts-cel?label=openssf%20scorecard&style=flat)](https://scorecard.dev/viewer/?uri=github.com/great-parts/great-parts-cel)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue)](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/LICENSE)

CEL Policy for [Great.Parts.Cel](https://www.nuget.org/packages/Great.Parts.Cel), the CEL (Common Expression Language) library for .NET / C#.

A policy is a YAML document of rules: variables, conditions, outputs and nested rules, with first-match or aggregate semantics. The package reads the document and compiles it in a CEL environment into one program, as cel-go does; every error reports its line and column in the document, also inside indented YAML blocks. The package reads YAML with its own reader, depends only on `Great.Parts.Cel` and uses no reflection, so it works under trimming and Native AOT and in the browser.

Documentation: start with [Getting Started](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/getting-started.md), seven short scenarios from the first expression to a web service; the pages are listed in the Documentation section below.

## Install

The package targets `net10.0` and `net11.0` and depends only on `Great.Parts.Cel`:

```shell
dotnet add package Great.Parts.Cel.Policy
```

## Use

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Policy;
using Great.Parts.Cel.Values;

const string Yaml = """
    name: greeting
    rule:
      variables:
        - name: name
          expression: request.user
      match:
        - condition: variables.name.startsWith('j')
          output: "'Hi, J!'"
        - output: "'Hi, ' + variables.name + '!'"
    """;

CelEnvironment env = CelEnvironment.Create(b => b
    .AddVariable<string>("request.user")
    .AddExtension(CelExtensions.Optional(2))
    .AddExtension(CelExtensions.Bindings(1)));

CelPolicy policy = CelPolicy.Parse(Yaml, "greeting.yaml").GetPolicyOrThrow();
CelProgram<string> program = policy.Compile<string>(env).GetProgramOrThrow();
```

The conformance of the package to the CEL Policy conformance suite is published with each release.

## For AI coding assistants

The package carries a file `AGENTS.md` in its root: what the package reads, the rules for the environment and the result type, and what to avoid. The NuGet MCP server returns it with its tool `get_package_context` for a package that is restored on the machine.

## Documentation

The documentation of a release is the content of the repository at the tag of the release. The links on this page point to the tag `v2.0.0` of the repository.

- [Getting Started](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/getting-started.md): seven scenarios, from the simple entry to a web service
- [README of the repository](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/README.md)
- [Extension sets](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/extensions.md): `optional` and `bindings`, which a policy needs
- [Error codes](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/error-codes.md)
- [Versioning and compatibility](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/versioning.md)
- [Changelog](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/CHANGELOG.md)

## License

[Apache License 2.0](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/LICENSE).
