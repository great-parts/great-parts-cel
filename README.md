# Great.Parts.Cel

CEL (Common Expression Language) for .NET / C#.

**Status:** pre-release. No package has been published yet, and the public API is unstable: it may change in any commit.

[CEL](https://cel.dev/) is an expression language that is not Turing complete: an expression reads only the data that the application passes to it. An application compiles an expression once and evaluates it on different data many times. Great.Parts.Cel does both in a .NET application, under JIT and under Native AOT.

## Quick start

No package is on nuget.org yet. To try the library, clone the repository and reference `src/Great.Parts.Cel/Great.Parts.Cel.csproj` from a console application. The library also targets `net11.0`, so its build needs the .NET SDK named in [global.json](global.json).

```csharp
using Great.Parts.Cel;

CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .AddVariable<long>("x")
    .Build()
    .GetEnvironmentOrThrow();

CelProgram<long> program = environment.Compile<long>("(x + 2) / 3").GetProgramOrThrow();

Console.WriteLine(program.Evaluate(new CelBindings().Add("x", 40L)).GetValueOrThrow()); // Output: 14
Console.WriteLine(program.Evaluate(new CelBindings().Add("x", -5L)).GetValueOrThrow()); // Output: -1
```

The environment declares what an expression may use: here the standard library and one variable `x` of the CEL type `int`. `Compile<long>` parses the expression, checks its types and returns a program. The program is immutable: it is evaluated any number of times, from any number of threads. Errors are returned as results, each with its place in the expression; `Get…OrThrow()` turns an error into an exception and keeps the example short.

## What works now

Every part of the library runs end to end: parsing, type checking, planning and evaluation. The language is implemented in steps, and today it covers:

- literals: `int`, `uint`, `double`, `string`, `bytes`, `bool`, `null`, lists and maps;
- `int` addition, division and negation, with an error on overflow and on division by zero;
- logical or, `||`: when one side is `true`, an error on the other side does not make the result an error;
- variables declared in code or in an environment description in JSON;
- parse and type errors: a compilation returns every error it finds, each with its offset, line and column.

Other functions and operators are not declared yet, so an expression that uses them fails type checking. A member of the public API that is not implemented yet throws `NotImplementedException`.

## How it is checked

Every pull request and every push to `main` runs [CI](.github/workflows/ci.yml):

| Target | Ubuntu, JIT | macOS, JIT | linux-x64, Native AOT |
| :- | :- | :- | :- |
| `net10.0` | yes | yes | yes |
| `net11.0` | yes | yes | yes |

- In every cell the tests include the conformance tests of [cel-spec](https://github.com/google/cel-spec) at a pinned commit ([tests/cel-spec/COMMIT](tests/cel-spec/COMMIT)). A case that does not pass yet is in the [skip list](tests/Great.Parts.Cel.Conformance/CelSpec/skips.md) with its reason.
- The Native AOT cells publish the test projects, run them and compare the test count with the JIT run.
- The examples of this README and of `docs/` are compiled and run, and their output is compared with the output they show. The links are checked.
- Differences from cel-go that have been analysed are listed in [known-differences.md](tests/Great.Parts.Cel.Conformance/Regression/known-differences.md).

## Documentation

- [API concepts](docs/api-concepts.md): every public type and the CEL concept it stands for.
- [Error codes](docs/error-codes.md): every code of `CelErrorCode`, its stage and when it occurs.
- [Application functions](docs/recipes/application-functions.md): how to give the functions of an application the data they need.
- [Design](docs/design/): how the library and its checks are built, for contributors.

## How it is built

Great.Parts.Cel is designed by its maintainer, implemented by a team of AI agents under the maintainer's direction, and verified by cel-spec and CI. Every change passes the same checks, whoever wrote it, and every commit of `main` carries the maintainer's sign-off. [CONTRIBUTING.md](CONTRIBUTING.md#how-the-library-is-developed) says more.

## Maintainers

Vladislav Nekrasov ([va-nekrasov](https://github.com/va-nekrasov)).

## Contributing and policies

- [CONTRIBUTING.md](CONTRIBUTING.md): how to build, test and propose a change.
- [SECURITY.md](SECURITY.md): how to report a vulnerability, and which versions get fixes.
- [SUPPORT.md](SUPPORT.md): where to ask questions and report bugs.
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md): the code of conduct of the project.
- [CHANGELOG.md](CHANGELOG.md): the changes of each version.

## License

[Apache License 2.0](LICENSE). See also [NOTICE](NOTICE).
