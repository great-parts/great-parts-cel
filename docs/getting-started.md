# Getting started

Great.Parts.Cel evaluates expressions of CEL, the Common Expression Language, in a .NET application. An expression is a small text such as `total > limit && country == 'DE'`. It reads only the data that the application passes to it, so a rule can live in a database or a configuration file and change without a new release.

This page goes from the shortest call to the more complete ways of using the library. Each section is one scenario with a project that you can run, in [samples](../samples/README.md); the code of a section is the `Program.cs` of its project. CI builds every sample, and it compiles and runs the code on this page and compares the output with the output shown.

To try a section in a project of your own, add the package and paste the code:

```shell
dotnet add package Great.Parts.Cel
```

The library targets `net10.0` and `net11.0`. The samples use .NET 10 with nullable reference types and implicit usings, so `Console` and `Dictionary` need no `using`.

## 1. Evaluate one expression

`CelExpression.Evaluate<T>` takes the text of an expression and the data, and returns the value in one call. The properties of an anonymous object are the variables of the expression, and the type argument is the type of the result.

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Diagnostics;

// The variables are the properties of an anonymous object; the type argument is the type of the result.
long total = CelExpression.Evaluate<long>("price * quantity", new { price = 12L, quantity = 3L });
Console.WriteLine(total); // Output: 36

bool allowed = CelExpression.Evaluate<bool>(
    "age >= 18 && country in ['DE', 'FR']",
    new { age = 30L, country = "FR" });
Console.WriteLine(allowed); // Output: True

// A dictionary does the same when the names are known only at run time.
var variables = new Dictionary<string, object?> { ["name"] = "Ada", ["roles"] = new[] { "admin", "dev" } };
Console.WriteLine(CelExpression.Evaluate<bool>("name == 'Ada' && 'admin' in roles", variables)); // Output: True

// A wrong expression is a CelException that carries every error.
try
{
    _ = CelExpression.Evaluate<long>("price * quantity", new { price = 12L });
}
catch (CelException exception)
{
    Console.WriteLine(exception.Errors[0].Code); // Output: UndeclaredReference
}
```

What to know:

- The first call with a text compiles it. The compiled program is kept in a bounded cache of the process, so a later call with the same text and the same variables does not compile again.
- A dictionary does the same as an anonymous object. The type of each variable comes from its value, so use it when the names are known only at run time.
- Every error of the expression, whether in the text, in the types or in the evaluation, is thrown as `CelException`. Its `Errors` list all of them, and the `Code` of an error is a member of `CelErrorCode`; see [Error codes](error-codes.md).
- Every call has a cost limit, `CelExpression.DefaultCostLimit`, 1,000,000 cost units, which stops an expression that is too expensive. The argument `costLimit` replaces it; see [Cost](cost.md).
- The source generator of the package reads each call at build time and registers the type of the anonymous object. No reflection runs, so the call works under trimming and Native AOT. The generator sees the static type of the argument: pass the anonymous object directly to `Evaluate`.
- The entry reads a closed list of types: numbers, `bool`, strings, bytes, time, `JsonElement`, and lists and dictionaries of them. An object of your application is not on the list; section 3 shows how to pass one. The list is in "Data of the simple entry" of [Type mapping](type-mapping.md).

## 2. Describe an environment and pass JSON

When a rule runs many times, or reads more than the simple entry reads, describe the environment yourself. It says which variables an expression may use and of which types, and which functions and extension sets it may call. An environment is built once and is immutable, so any number of threads can share it. Compiling an expression in it gives a program, which is immutable too, and a program evaluates on any number of sets of data.

```csharp
using System.Text;
using System.Text.Json;
using Great.Parts.Cel;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;

// The environment says which names an expression may use. Here a part of it comes from JSON text, as it could from a file.
const string description = """
    {
      "extensions": [ { "name": "strings", "version": "5" } ],
      "variables": [ { "name": "limit", "type": "double" } ]
    }
    """;

CelEnvironment environment = CelEnvironment.Create(builder =>
{
    var errors = CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(description));
    if (errors.Length > 0)
    {
        throw new CelException(errors);
    }

    // The rest is declared in code: a JSON document is one variable of any shape.
    builder.AddVariable<JsonElement>("payload");
});

// Compile once; the program is immutable and evaluates any number of times.
CelProgram<bool> program = environment.Compile<bool>(
    "payload.total > limit && payload.items.exists(i, i.sku == 'A-1') && payload.customer.upperAscii() == 'ACME'")
    .GetProgramOrThrow();

using JsonDocument document = JsonDocument.Parse("""
    { "customer": "acme", "total": 250.5, "items": [ { "sku": "A-1" }, { "sku": "B-2" } ] }
    """);
CelBindings bindings = new CelBindings().Add("limit", 100.0).Add("payload", document.RootElement);
Console.WriteLine(program.Evaluate(bindings).GetValueOrThrow()); // Output: True
```

What to know:

- A part of the environment comes from JSON text: here the extension set `strings` with its version, which gives `upperAscii`, and the variable `limit` of the CEL type `double`. The format is a subset of the environment configuration of cel-go; see [Environment description in JSON](environment-json.md) for its keys. What JSON cannot carry, such as the code of a function, is added in code.
- `payload` is declared with `AddVariable<JsonElement>`. Its CEL type is `dyn`, and the expression reads the fields of the document when it selects them. A number of JSON is a `double` in CEL, an integer too; `int(payload.qty)` converts it. That is why `limit` is a `double`.
- Keep the `JsonDocument` alive until the evaluation ends.
- `Compile<T>` and `Evaluate` return results that hold the errors, never an exception for a wrong expression. `GetProgramOrThrow()` and `GetValueOrThrow()` turn an error into `CelException` and keep the example short. In an application, test `IsSuccess` and show the errors; the last section shows how.
- `CelBindings.Add<T>` converts a value by its static type. The page [Standard library](standard-library.md) lists what the expressions can call, and [Extension sets](extensions.md) the extension sets.

## 3. Give an expression an object of your application

The simple entry does not read the classes of your application. Describe them in a type context: a `partial` class derived from `CelTypeContext`, with a `[CelObject]` attribute for each type that an expression may read. The source generator completes the class at build time. It reads the members by direct access, with no reflection, so the description works under trimming and Native AOT. It describes the types that the members reach as well, here `Customer` and `OrderLine`.

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Values;

// The type context lists the .NET types that an expression may read; the source generator of the package completes it at build time.
// UseInput makes the properties of Order variables of the environment: `total`, not `order.total`.
CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddTypes(ShopTypes.Default)
    .UseInput<Order>()
    .AddVariable<double>("limit"));

CelProgram<bool> needsReview = environment
    .Compile<bool>("total > limit && lines.exists(l, l.quantity > 10) && customer.country != 'DE'")
    .GetProgramOrThrow();

CelBindings bindings = new CelBindings().Add("limit", 1000.0);

Order small = new("A-1", 120.0, new Customer("FR"), [new OrderLine("pen", 3)]);
Order large = new("A-2", 4500.0, new Customer("FR"), [new OrderLine("pen", 40), new OrderLine("ink", 2)]);
Console.WriteLine(needsReview.EvaluateInput(small, bindings).GetValueOrThrow()); // Output: False
Console.WriteLine(needsReview.EvaluateInput(large, bindings).GetValueOrThrow()); // Output: True

[CelObject(typeof(Order))]
internal sealed partial class ShopTypes : CelTypeContext;

public sealed record Customer(string Country);

public sealed record OrderLine(string Sku, long Quantity);

public sealed record Order(string Id, double Total, Customer Customer, IReadOnlyList<OrderLine> Lines);
```

What to know:

- `UseInput<Order>()` makes the properties of `Order` variables of the top level: `total`, not `order.total`. `AddVariable` declares other variables, which the call binds by name in `CelBindings`.
- `EvaluateInput` is the call with an input; `Evaluate` is the call with bindings only.
- The names of the fields are camel case by default, so `Quantity` is `quantity`. `[CelName]` gives another name and `[CelIgnore]` hides a member; see [Type mapping](type-mapping.md).
- The generator reports what it cannot read at build time, with the codes `GPCEL001` to `GPCEL012`; see [Source generator diagnostics](generator-diagnostics.md).
- In a program with top-level statements the declarations of the types go after the statements.

## 4. Check a rule when a user saves it

A rule that a user writes should be checked when the user saves it, not when it first runs. `Validate<T>` compiles the text by the rules of `Compile<T>` and returns no program. The result tells whether the rule is valid, lists every error with its place, gives the type of the result and the names of the variables and functions the rule reads.

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Diagnostics;

// The environment of the rules that users write: two variables and the standard library.
CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddVariable<double>("total")
    .AddVariable<string>("country"));

string[] rules =
[
    "total > 100.0 && country == 'DE'",
    "total > 100.0 && contry == 'DE'",
    "country in ['DE', 'FR']",
];

// What the application does when a user saves a rule: it validates the text and does not run it.
foreach (string rule in rules)
{
    CelValidationResult check = environment.Validate<bool>(rule);
    if (check.IsValid)
    {
        Console.WriteLine($"saved, reads: {string.Join(", ", check.ReferencedVariables)}");
        continue;
    }

    foreach (CelError error in check.Errors)
    {
        Console.WriteLine(error.ToDisplayString(rule));
    }
}
```

```text
saved, reads: country, total
undeclared reference to 'contry' (in container '')
total > 100.0 && contry == 'DE'
                 ^~~~~~
saved, reads: country
```

What to know:

- The type argument is the expected type of the result. `Validate<bool>` rejects a rule that gives a number, with the code `ExpectedTypeMismatch`.
- `ToDisplayString` gives the message, the line of the text where the fragment starts, and a mark under the fragment. The text of a message is for people and is not part of the compatibility contract; a program tests `CelError.Code`.
- `ReferencedVariables` and `ReferencedFunctions` are sorted lists of names. Use them to tell the user what a rule reads, or to refuse a rule that reads a name it should not.
- `Validate<T>` gives the errors that `Compile<T>` gives for the same text, and keeps no program. A rule that is valid compiles.

## 5. Add a function of your application

The standard library knows nothing about your business. `AddFunction` adds a function that an expression calls as `discount(total, years)`; `AddMethod` adds one that it calls on a value, as `email.domain()`. Both take a .NET delegate, so there is no declaration to write.

```csharp
using Great.Parts.Cel;

// A function of the application is a .NET delegate. The CEL types of its parameters and of its result come from the .NET types:
// double is double, long is int, string is string and IReadOnlyList<string> is list(string).
CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddVariable<string>("email")
    .AddVariable<double>("total")
    .AddVariable<long>("years")
    // A function is called as discount(total, years).
    .AddFunction("discount", (double price, long loyalty) => price * Math.Min(loyalty, 5L) / 100.0)
    // A method takes the value before the dot as its first parameter: email.domain() and email.endsWithAny(['.corp', '.gov']).
    .AddMethod("domain", (string address) => address[(address.IndexOf('@') + 1)..])
    .AddMethod("endsWithAny", (string text, IReadOnlyList<string> suffixes) =>
        suffixes.Any(suffix => text.EndsWith(suffix, StringComparison.Ordinal))));

CelProgram<double> payable = environment
    .Compile<double>("total - discount(total, years)")
    .GetProgramOrThrow();
CelProgram<bool> business = environment
    .Compile<bool>("email.domain() == 'example.com' || email.endsWithAny(['.corp', '.gov'])")
    .GetProgramOrThrow();

CelBindings first = new CelBindings().Add("email", "ada@example.com").Add("total", 200.0).Add("years", 3L);
CelBindings second = new CelBindings().Add("email", "eve@mail.test").Add("total", 80.0).Add("years", 9L);

Console.WriteLine(payable.Evaluate(first).GetValueOrThrow()); // Output: 194
Console.WriteLine(business.Evaluate(first).GetValueOrThrow()); // Output: True
Console.WriteLine(payable.Evaluate(second).GetValueOrThrow()); // Output: 76
Console.WriteLine(business.Evaluate(second).GetValueOrThrow()); // Output: False
```

What to know:

- The CEL types of the parameters and of the result come from the .NET types by the rows of [Type mapping](type-mapping.md): here `double` is `double`, `long` is `int`, `string` is `string` and `IReadOnlyList<string>` is `list(string)`. An expression that calls a function with other types does not compile.
- For `AddMethod` the first parameter of the delegate is the receiver, the value before the dot. `AddFunction` has no receiver.
- The implementation runs on the thread that evaluates the expression, so it does not wait for I/O, and it can be called from many threads at once. An exception that it throws does not escape: the evaluation returns the error `ApplicationException`, and `CelError.Exception` holds the original exception. Load the data before the evaluation, or declare an asynchronous function; the recipe [Application functions](recipes/application-functions.md) shows both.
- A call costs one unit. A function whose work grows with its arguments declares its cost with the `CelFunctionOptions` argument of `AddFunction` and `AddMethod`; see [Cost](cost.md).
- The functions belong to the environment. Declare them where the environment is built, once, and compile expressions in it as before.

## 6. A web service

In a web service the environment is built once for the application, and an endpoint checks its request with a rule. The package `Great.Parts.Cel.DependencyInjection` registers the environment and a compilation cache in the container (`AddCel`); the package `Great.Parts.Cel.AspNetCore` adds `WithCelValidation<TRequest>` to the endpoints of a minimal API. Install the second, which brings the first and the core:

```shell
dotnet add package Great.Parts.Cel.AspNetCore
```

The type of the request is described by a type context, as in section 3, so the fields of the request are the variables of the rule. The sample is its own client, so that it ends by itself: it listens on a free port of the loopback, sends two requests and stops. A service calls `app.Run()` instead.

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Great.Parts.Cel.AspNetCore;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders(); // The sample prints only its own lines.

// One CEL environment for the application. Its type context describes CreateOrder, so the fields of the request are variables.
builder.Services.AddCel(cel => cel.AddTypes(OrderTypes.Default));

await using WebApplication app = builder.Build();

// The rule is a text: it could come from configuration or from a database.
app.MapPost("/orders", (CreateOrder order) => Results.Created($"/orders/{order.Id}", order))
    .WithCelValidation<CreateOrder>("items.size() > 0 && total >= 0.0", "The order is empty or the total is negative");

// The sample is its own client, so that it ends by itself: it listens on a free port of the loopback, sends two requests and stops.
// A service calls app.Run() instead.
app.Urls.Clear();
app.Urls.Add("http://127.0.0.1:0");
await app.StartAsync();
using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

using HttpResponseMessage accepted = await client.PostAsJsonAsync("/orders", new { id = "A-1", total = 99.0, items = new[] { "pen" } });
Console.WriteLine((int)accepted.StatusCode); // Output: 201

using HttpResponseMessage rejected = await client.PostAsJsonAsync("/orders", new { id = "A-2", total = 99.0, items = Array.Empty<string>() });
Console.WriteLine((int)rejected.StatusCode); // Output: 400
using JsonDocument problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
Console.WriteLine(problem.RootElement.GetProperty("title").GetString()); // Output: One or more validation errors occurred.
Console.WriteLine(problem.RootElement.GetProperty("errors").GetProperty("")[0].GetString()); // Output: The order is empty or the total is negative

await app.StopAsync();

[CelObject(typeof(CreateOrder))]
internal sealed partial class OrderTypes : CelTypeContext;

public sealed record CreateOrder(string Id, double Total, IReadOnlyList<string> Items);
```

What to know:

- `WithCelValidation<TRequest>(expression, message)` adds a filter to the endpoint, and the same method exists for a group of endpoints. Before the handler runs, the filter takes the argument of the type `TRequest` and evaluates the expression with it as the input: `items` and `total` are the fields of `CreateOrder`, named as in section 3. `true` runs the handler.
- The answer 400 is a validation problem of ASP.NET Core, `application/problem+json`. Its `title` is "One or more validation errors occurred." and its `errors` has one key, the empty string, which stands for the whole request; the list under it holds the message that you gave. The three cases differ in `detail`:
  - the rule gives `false`: no `detail`;
  - the rule fails with an error, because it does not compile or its evaluation fails: `detail` is "The validation rule failed with the error `<code>`.", where `<code>` is the name of the `CelErrorCode`, for example `UndeclaredReference`;
  - the handler has no argument of the type `TRequest`: `detail` is "The request has no argument of the type `<type>`.", with the name of the type.
- The rule is built at the first request of the endpoint, not when the application starts, so a rule that does not compile gives 400 to every request. Check the rules with `Validate<bool>` (section 4) in a test, or when the rule is saved.
- The evaluation is asynchronous and gets the abort token of the request, so a rule may call an asynchronous function of the application (section 5).
- The same package puts CEL into authorization: `AddCelAuthorization` and `RequireCel` in a policy. A service that evaluates texts itself takes `ICelEvaluator` from the container; it works through the compilation cache of the next section.
- To check an object with FluentValidation instead of an endpoint filter, see the recipe [CEL rules in FluentValidation](recipes/fluentvalidation.md).

## 7. Compile once, evaluate many times

A rule that is kept as text is compiled when it is used. `CelCompilationCache` keeps the program of each text of one environment, so a text is compiled once however many rows or requests use it. The sample evaluates two rules over four rows with one cache and one `CelBindings`.

```csharp
using Great.Parts.Cel;

CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddVariable<string>("country")
    .AddVariable<double>("total"));

// The cache compiles a text once and keeps the program, so a rule that comes from a database costs one compilation, not one per row.
CelCompilationCache cache = new(environment);

string[] rules = ["total > 100.0", "country == 'DE' && total > 100.0"];
(string Country, double Total)[] rows = [("DE", 250.0), ("FR", 80.0), ("DE", 40.0), ("FR", 900.0)];

// One CelBindings serves the whole loop: Clear removes the values of the previous row.
CelBindings bindings = new();
foreach (string rule in rules)
{
    int matches = 0;
    foreach ((string country, double total) in rows)
    {
        bindings.Clear();
        bindings.Add("country", country).Add("total", total);
        if (cache.Evaluate<bool>(rule, bindings).GetValueOrThrow())
        {
            matches++;
        }
    }

    Console.WriteLine($"{rule}: {matches} of {rows.Length}");
}

// Each text is one entry. When a rule changes in the database, remove its text; the next call compiles the new one.
Console.WriteLine($"entries: {cache.Count}");
Console.WriteLine($"removed: {cache.TryRemove(rules[0])}");
Console.WriteLine($"entries: {cache.Count}");
```

```text
total > 100.0: 2 of 4
country == 'DE' && total > 100.0: 1 of 4
entries: 2
removed: True
entries: 1
```

What to know:

- An entry of the cache is a text with the type of the result and the options of the compilation, and `Count` counts such entries. The first `Evaluate` of a text compiles it and the later ones find the program. An error of a compilation is kept as well, so a wrong text is not compiled again and again.
- The cache is bounded: by default 80,000 entries and a total of 900,000 UTF-16 units of text, about 64 MiB when it is full (`CelCompilationCache.DefaultMaxEntryCount` and `DefaultMaxTotalTextLength`). The constructor with two numbers sets other capacities. A full cache evicts entries, and a program that the cache returned stays valid; see [Limits](limits.md#compilation-cache).
- Every member of the cache can be called from many threads at once. Bindings cannot be shared like that: `Clear` lets one set of bindings serve the next row of a loop, so the set belongs to one loop. Do not clear or change bindings while an evaluation reads them; give each thread its own.
- `TryRemove(text)` removes the entries of a text, whatever the type of the result and the options, and tells whether it removed any. `Clear()` removes all entries. Call them when a rule changes in the database.
- To keep the program, `cache.GetOrCompile<T>(text)` gives the result of the compilation, and the program is used as in section 2. For rows that are objects of the application, `cache.EvaluateInput<T>(text, input)` is the cache form of the call of section 3.

Next: [Type mapping](type-mapping.md) says which .NET types an expression reads and returns, and [Error codes](error-codes.md) lists every error.
