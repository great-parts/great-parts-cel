# Great.Parts.Cel.DependencyInjection: instructions for AI coding agents

This file is for an agent that helps a developer use the package `Great.Parts.Cel.DependencyInjection` in an application. It is not for the contributors of the library; they have `AGENTS.md` in the repository. Read the `AGENTS.md` of `Great.Parts.Cel` first: this package registers its objects in a dependency injection container and adds the validation of options. Versions 1.x of the library are not supported; this package exists from 2.0.

## What it is

The package connects `Great.Parts.Cel` to `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Options`:

- `AddCel` registers three singletons: a `CelEnvironment`, a `CelCompilationCache` over it, and an `ICelEvaluator` that evaluates through the cache;
- `AddKeyedCel` does the same under a name, for an application with several environments (keyed services of .NET);
- `ICelEvaluator` is the interface to inject and to replace in tests;
- `ValidateCel` validates the options of the application with a CEL expression.

The package reads no configuration and uses no reflection, so it works under trimming and Native AOT, in services without a web host, and in the browser.

## When to use it, and when not

Use it when the application already uses dependency injection, rules are evaluated in several places, and one environment and one compilation cache should be shared.

Do not use it for a console tool or a single call: `CelExpression.Evaluate` or a `CelEnvironment` built by hand is simpler. For ASP.NET Core policies and request validation take `Great.Parts.Cel.AspNetCore`, which depends on this package.

## Install

```shell
dotnet add package Great.Parts.Cel.DependencyInjection
```

The package targets `net10.0` and `net11.0`. It depends on `Great.Parts.Cel`, `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options`, version 10.0.0 or higher; an application on .NET 11 uses its own newer versions. It does not depend on `Microsoft.Extensions.Configuration`: binding configuration stays in the application.

## Register and use

Declare the types the rules read in a `CelTypeContext` (see the `AGENTS.md` of `Great.Parts.Cel`), register the environment, and inject the evaluator:

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Values;
using Microsoft.Extensions.DependencyInjection;

// In an application these calls are on builder.Services; here the services are made apart, to run the rules.
ServiceCollection services = new();
services.AddCel(b => b
    .AddTypes(AppTypes.Default)
    .UseInput<Order>()
    .SetDefaultCostLimit(100_000));
services.AddSingleton<PricingRules>();

using ServiceProvider provider = services.BuildServiceProvider();
Console.WriteLine(provider.GetRequiredService<PricingRules>().IsLarge(new Order(150, "EUR"))); // Output: True

[CelObject(typeof(Order))]
internal sealed partial class AppTypes : CelTypeContext;

public sealed record Order(long Total, string Currency);

public sealed class PricingRules(ICelEvaluator evaluator)
{
    public bool IsLarge(Order order) =>
        evaluator.EvaluateInput<bool>("total > 100", order).GetValueOrThrow();
}
```

- The environment is built when it is first resolved, with `CelEnvironment.Create`: a build error is a `CelException` at that moment, and an exception thrown by your `configure` delegate reaches the caller unchanged. Resolve it at startup if you want to fail early.
- `ICelEvaluator.Evaluate<T>(text, bindings)` takes bindings; `EvaluateInput<T>(text, input, bindings)` takes an input object (its type is the input of the environment); `Validate<T>(text)` checks a text. Each takes the optional `CelCompileOptions`, `CelEvalOptions` and a `CancellationToken`. Evaluation errors come back in the result: check `IsSuccess` before `Value`.
- The call calls the cache: a text is compiled once while it stays in the cache. Do not cache programs yourself.
- Several environments: `services.AddKeyedCel("pricing", b => …)`, and take them with `[FromKeyedServices("pricing")] ICelEvaluator evaluator` (or `CelEnvironment`, or `CelCompilationCache`).
- Calling `AddCel` or `AddKeyedCel` twice with the same name adds nothing the second time; call them while the container is configured, from one thread.

## Settings

`CelOptions` has `DefaultCostLimit` (`ulong?`, replaces a limit that `configure` set), `CacheMaxEntryCount` and `CacheMaxTotalTextLength`. The package does not bind them; the application does, with its own binding, for example:

```csharp
using Great.Parts.Cel.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Cel:DefaultCostLimit"] = "50000",
    ["Cel:Pricing:CacheMaxEntryCount"] = "64",
});

builder.Services.AddCel(b => b.SetDefaultCostLimit(100_000));
builder.Services.AddKeyedCel("pricing", b => b.SetDefaultCostLimit(100_000));
builder.Services.Configure<CelOptions>(builder.Configuration.GetSection("Cel"));
builder.Services.Configure<CelOptions>("pricing", builder.Configuration.GetSection("Cel:Pricing")); // the named environment

using IHost host = builder.Build();
IOptionsMonitor<CelOptions> options = host.Services.GetRequiredService<IOptionsMonitor<CelOptions>>();
Console.WriteLine(options.CurrentValue.DefaultCostLimit); // Output: 50000
Console.WriteLine(options.Get("pricing").CacheMaxEntryCount); // Output: 64
```

The values are read once, when the environment and the cache are created. Wrong values are an `OptionsValidationException` when `IOptions<CelOptions>.Value` is read.

## Validate options with CEL

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Values;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Shop:MaxDiscount"] = "80", ["Shop:MinOrder"] = "0" });

builder.Services.AddCel(b => b.AddTypes(ShopTypes.Default));
builder.Services.AddOptions<ShopOptions>()
    .Bind(builder.Configuration.GetSection("Shop"))
    .ValidateCel("maxDiscount <= 50 && minOrder >= 0", "The discount is above 50% or the minimum order is negative")
    .ValidateOnStart();

using IHost host = builder.Build();
try
{
    _ = host.Services.GetRequiredService<IOptions<ShopOptions>>().Value;
}
catch (OptionsValidationException exception)
{
    Console.WriteLine(exception.Failures.Single()); // Output: The discount is above 50% or the minimum order is negative
}

[CelObject(typeof(ShopOptions))]
internal sealed partial class ShopTypes : CelTypeContext;

public sealed class ShopOptions
{
    public long MaxDiscount { get; set; }

    public long MinOrder { get; set; }
}
```

The fields of the options type are the variables of the expression, so the context of the environment has to describe the type (`[CelObject(typeof(ShopOptions))]`). `false`, an error of compilation and an error of evaluation all fail the validation with the message; the last two add the code of the error. Pass `environmentName` for an environment registered by name. The check runs when the options are read, or at startup with `ValidateOnStart()`.

## Tests

Replace `ICelEvaluator` with a fake in tests of the services that use it, or build the real environment with the same `configure` delegate and evaluate for real: the environment is cheap to create once per test class.

## Common mistakes

1. Injecting `CelEnvironment` and compiling every text by hand on each request. Inject `ICelEvaluator`, or use `CelCompilationCache`.
2. Expecting the package to bind `CelOptions` from configuration. It does not; call `Configure<CelOptions>` yourself.
3. Using `ValidateCel` with an options type that no `CelTypeContext` describes: the environment fails with `TypeNotDeclared`.
4. Declaring a variable with the name of a field of the input or of the options type: `DuplicateVariable`.
5. Reading `Value` of a result without checking `IsSuccess`: from 2.0 it throws `CelException` for a failed result.
6. Registering one environment and resolving it by key, or the other way round. A keyed registration is a separate set of services.
7. Passing to `EvaluateInput` an object of another .NET type than the one declared with `UseInput<T>`: that is an `ArgumentException`, a fault of the call, not an evaluation error.

## Documentation

The documentation of a release is the content of the repository at the tag of the release; these links point to the tag `v2.0.0`.

- [README of the repository](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/README.md)
- [API concepts](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/api-concepts.md)
- [Type mapping](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/type-mapping.md)
- [Error codes](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/error-codes.md)
- [Limits](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/limits.md) and [Cost](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/cost.md)
- [Versioning and compatibility](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/versioning.md)
- [Changelog](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/CHANGELOG.md)
