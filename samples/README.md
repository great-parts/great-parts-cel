# Samples

Each directory is a small console application that shows one way to use `Great.Parts.Cel`, and prints its result. The page [Getting started](../docs/getting-started.md) explains them one by one; the code in a section of the page is the code of the `Program.cs` of its sample.

| Sample | What it shows |
| :- | :- |
| `SimpleEntry` | one call, `CelExpression.Evaluate`, on an anonymous object or a dictionary |
| `EnvironmentAndJson` | an environment described in JSON and in code, a `JsonElement` as data, a program compiled once |
| `AppObject` | objects of the application: a type context, `UseInput<T>` and `EvaluateInput` |
| `ValidateOnSave` | `Validate` and `ToDisplayString`: check a rule when a user saves it |
| `AppFunctions` | functions of the application: `AddFunction` and `AddMethod` with a .NET delegate |
| `WebService` | a minimal API of ASP.NET Core: `AddCel` and `WithCelValidation<TRequest>`; the sample makes a request to itself, prints the answer 400 and ends |
| `CacheAndBulk` | `CelCompilationCache` and one `CelBindings` for a loop over many rows |
| `FluentValidation` | the method `Cel(...)` for the rules of FluentValidation; the recipe [CEL rules in FluentValidation](../docs/recipes/fluentvalidation.md) explains it |

## Run a sample

The samples build the packages from the sources of this repository, so the build needs the .NET SDK that [global.json](../global.json) names. From the root of the repository:

```shell
dotnet run --project samples/SimpleEntry
```

`dotnet build samples/Great.Parts.Cel.Samples.slnx` builds all samples. The samples do not take the settings of the repository: `Directory.Build.props`, `Directory.Packages.props` and `.editorconfig` in this directory stop the files of the root, which are written for the library. A warning of the compiler is a build error in the samples.

## Copy a sample into your project

The samples are written to be copied. The .NET SDK 10 is enough in your own project.

1. Create a console application, or take a project you have: `dotnet new console -o MyApp`.
2. Add the package: `dotnet add package Great.Parts.Cel`. The package carries the source generator, so nothing else is needed.
3. Replace the content of `Program.cs` with the `Program.cs` of the sample.
4. Check the settings the sample uses in its project file: the target framework `net10.0` or `net11.0`, `Nullable` and `ImplicitUsings` set to `enable`. Without implicit usings add `using System;` and the other namespaces the compiler asks for.

A project file of a sample has two `ProjectReference` items, one for `Great.Parts.Cel` and one for the source generator `Great.Parts.Cel.Generator` as an analyzer. They exist only to build the current sources of this repository. Do not copy them: step 2 replaces both.

Two samples need more:

- `WebService` is a web application: its project uses the SDK `Microsoft.NET.Sdk.Web`, and step 2 is `dotnet add package Great.Parts.Cel.AspNetCore`, which brings `Great.Parts.Cel.DependencyInjection` and `Great.Parts.Cel`. Its project file has four `ProjectReference` items for the same reason as above.
- `FluentValidation` needs the package `FluentValidation` as well: `dotnet add package FluentValidation`. The sample names the version 12.2.0 in its project file, and the project is called `FluentValidationSample` so that its assembly does not take the name of the package. Copy `CelRuleBuilderExtensions.cs` together with `Program.cs`.
