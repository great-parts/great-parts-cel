# Great.Parts.Cel.AspNetCore: instructions for AI coding agents

This file is for an agent that helps a developer use the package `Great.Parts.Cel.AspNetCore` in an ASP.NET Core application. It is not for the contributors of the library; they have `AGENTS.md` in the repository. Read the `AGENTS.md` of `Great.Parts.Cel` and of `Great.Parts.Cel.DependencyInjection` first: this package builds on both. Versions 1.x of the library are not supported; this package exists from 2.0.

## What it is

The package puts CEL expressions into two places of an ASP.NET Core application:

- **authorization policies:** `policy.RequireCel("'admin' in user.role")`, also over a resource of the authorization, `RequireCel<TResource>(…)`;
- **validation of requests** of minimal API endpoints: `app.MapPost(…).WithCelValidation<CreateOrder>("items.size() > 0", "The order has no items")`, also on a group of endpoints.

Rules live in text, so an administrator can change them in configuration or a database without a new release. The package uses no reflection, and the endpoint filter works with the request delegate generator of ASP.NET Core under Native AOT. It adds no NuGet package of its own besides `Great.Parts.Cel.DependencyInjection`: ASP.NET Core comes from the shared framework `Microsoft.AspNetCore.App`.

## When to use it, and when not

Use it when access or validation rules are short conditions over the claims of the user or the fields of the request, and they are configured or change often.

Do not use it for rules that need code, a database or an HTTP call: CEL expressions run synchronously on the data you pass in. For those, write an `IAuthorizationHandler` or a filter in C#. When expressions come from outside the application, set a cost limit on the environment (`SetDefaultCostLimit` or `CelOptions.DefaultCostLimit`).

## Install

```shell
dotnet add package Great.Parts.Cel.AspNetCore
```

The package targets `net10.0` and `net11.0` and depends on `Great.Parts.Cel.DependencyInjection` (and through it on `Great.Parts.Cel`). The environment must be registered with `AddCel` or `AddKeyedCel` of that package.

## Authorization

```csharp
using System.Security.Claims;
using Great.Parts.Cel.AspNetCore;
using Great.Parts.Cel.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

// In an application these calls are on builder.Services; here the services are made apart, to run the policy.
ServiceCollection services = new();
services.AddLogging();
services.AddCel(b => b.SetDefaultCostLimit(10_000));
services.AddCelAuthorization();

services.AddAuthorizationBuilder()
    .AddPolicy("SalesManager", policy => policy
        .RequireAuthenticatedUser()
        .RequireCel("'manager' in user.role && user.department == ['sales']"));

using ServiceProvider provider = services.BuildServiceProvider();
ClaimsPrincipal manager = new(new ClaimsIdentity([new Claim("role", "manager"), new Claim("department", "sales")], "example"));
AuthorizationResult result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(manager, "SalesManager");
Console.WriteLine(result.Succeeded); // Output: True
```

- The expression has the type `bool` and sees `user`, of the type `map(string, list(string))`: the claim types of the user and their values. `user.role` is the list of the values of the claim `role`.
- With `RequireCel<TResource>(expression)` it also sees `resource` of the type `TResource`, for `IAuthorizationService.AuthorizeAsync(user, resource, policy)`. A type source of the environment (a `CelTypeContext` with `[CelObject(typeof(TResource))]`) must describe `TResource`. A resource of another type, or no resource, denies access.
- The registered environment must not declare variables named `user` or `resource`: the handler declares them in a derived environment.
- The expression is compiled when the policy is first evaluated, not when it is added. A compilation error, an evaluation error and `false` deny access; the reason holds the code of the error; no exception is thrown and an error never allows access. If expressions come from configuration, validate them at startup with `ICelEvaluator.Validate<bool>(…)`.
- `AddCelAuthorization(environmentName)` uses the environment registered with `AddKeyedCel(name, …)` instead of the unnamed one.

## Validation of requests

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.AspNetCore;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Services.AddCel(b => b.AddTypes(OrderTypes.Default).SetDefaultCostLimit(10_000));
using WebApplication app = builder.Build();

app.MapPost("/orders", (CreateOrder order) => Results.Created($"/orders/{order.Id}", order))
   .WithCelValidation<CreateOrder>("items.size() > 0 && total >= 0", "The order is empty or the total is negative");

var api = app.MapGroup("/api/orders");
api.WithCelValidation<CreateOrder>("currency in ['EUR', 'USD']", "Unsupported currency");

[CelObject(typeof(CreateOrder))]
internal sealed partial class OrderTypes : CelTypeContext;

public sealed record CreateOrder(long Id, IReadOnlyList<string> Items, long Total, string Currency);
```

- The fields of `TRequest` are the variables of the expression (`items`, `total`), not `request.items`. The environment is derived with `UseInput<TRequest>()`, so a type source of the registered environment must describe `TRequest`; the derived environment and the compiled expression are created once.
- `true` runs the handler. `false` answers 400 with a validation problem whose `errors` map holds your message under the empty key: `errors[""]` is `["<your message>"]`. A compilation or evaluation error answers 400 with `detail` set to `The validation rule failed with the error <code>.`, where `<code>` is the `CelErrorCode`. A handler without an argument of the type `TRequest` answers 400 with `detail` set to `The request has no argument of the type <type>.` Test the status and, for a failed rule, `errors[""]`; do not parse `detail`.
- The call on a group applies the filter to every endpoint of the group.
- `WithCelValidation` takes one type argument: write `WithCelValidation<CreateOrder>(…)`, nothing else.
- MVC controllers: `[CelValidate<TRequest>("expression", "message")]` on an action or a controller (an `IAsyncActionFilter`) works the same way, with the argument of the action of the type `TRequest`. The attribute itself uses no reflection, but MVC does not support Native AOT: an application published with Native AOT uses minimal API endpoints and `WithCelValidation`.
- Settings of options of the application are checked with `ValidateCel` of the package `Great.Parts.Cel.DependencyInjection`, not here.

## Common mistakes

1. Forgetting `AddCel` before `AddCelAuthorization`: the handler has no environment to evaluate in.
2. Not describing `TRequest` or `TResource` in a `CelTypeContext`: the first evaluation fails with `TypeNotDeclared`, and the request is denied or answered with 400.
3. Writing `request.total` or `resource` as a path prefix for request fields: fields of a request are top-level names (`total`); only the resource of an authorization is `resource`.
4. Expecting an expression error to let the request through. It never does.
5. Testing `user.role == 'admin'`: `user.role` is a list; write `'admin' in user.role`.
6. Reading a claim that the user does not have: the key is missing from the map; guard with `has(user.department)` or `'department' in user`.
7. Using the rules for work that needs data from a database or a service: load the data first and pass it as claims, a resource, or a field of the request.
8. Calling `WithCelValidation<TRequest, TBuilder>`: there is no such form.

## Documentation

The documentation of a release is the content of the repository at the tag of the release; these links point to the tag `v2.0.0` of the repository.

- [Getting Started](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/getting-started.md): seven scenarios; the sixth is a web service with this package
- [FluentValidation recipe](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/recipes/fluentvalidation.md)
- [README of the repository](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/README.md)
- [API concepts](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/api-concepts.md)
- [Type mapping](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/type-mapping.md)
- [Error codes](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/error-codes.md)
- [Limits](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/limits.md) and [Cost](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/cost.md)
- [Versioning and compatibility](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/versioning.md)
- [Changelog](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/CHANGELOG.md)
