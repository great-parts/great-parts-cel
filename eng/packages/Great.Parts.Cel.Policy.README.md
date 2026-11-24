# Great.Parts.Cel.Policy

![Great.Parts.Cel logo](https://raw.githubusercontent.com/great-parts/great-parts-cel/main/eng/packages/icon.png)

[![NuGet version](https://img.shields.io/nuget/v/Great.Parts.Cel.Policy)](https://www.nuget.org/packages/Great.Parts.Cel.Policy)
[![CI status](https://img.shields.io/github/actions/workflow/status/great-parts/great-parts-cel/ci.yml?branch=main&label=ci)](https://github.com/great-parts/great-parts-cel/actions/workflows/ci.yml)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue)](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/LICENSE)

CEL Policy for [Great.Parts.Cel](https://www.nuget.org/packages/Great.Parts.Cel), the CEL (Common Expression Language) library for .NET / C#.

A policy is a YAML document of rules: variables, conditions, outputs and nested rules, with first-match or aggregate semantics. The package reads the document and compiles it in a CEL environment into one program, as cel-go does; every error reports its line and column in the document, also inside indented YAML blocks. The package reads YAML with its own reader, depends only on `Great.Parts.Cel` and uses no reflection, so it works under trimming and Native AOT and in the browser.

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
