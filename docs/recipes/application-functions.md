# Application functions

An application can add its own functions to an environment: `CelEnvironmentBuilder.AddFunction` with a .NET delegate, `CelEnvironmentBuilder.AddFunctionImplementation` for a declared overload, or an extension set built with `CelExtension.Create`. This recipe shows how to give such functions the data they need.

## The rule

Evaluation is synchronous: a function implementation is called on the thread that evaluates the expression and must return its result before evaluation continues. The library itself does no I/O during evaluation.

So:

- load the data an expression needs before the evaluation, with `await` as usual, and pass it in `CelBindings`;
- do not wait for asynchronous work inside a function implementation: no `.Result`, no `.Wait()`, no `.GetAwaiter().GetResult()`.

## Why not block in a function

A function that calls `.Result` or `.Wait()` on a database query or an HTTP request holds a thread until the I/O completes. In ASP.NET Core the thread comes from the thread pool: under load, requests block all pool threads, new requests wait for threads, and the application stops responding although the processor is idle. In `browser-wasm` there is only one thread, so the call never completes and the page hangs.

This program shows the wrong way, only to be avoided. It declares the function and never calls it, so it prints nothing:

```csharp
using Great.Parts.Cel;

var registry = new SlowRegistry();
CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);

// Do not do this: the implementation blocks a thread until the query completes.
builder.AddFunction("isBlocked", (string inn) => registry.IsBlockedAsync(inn).Result);

// A registry whose query is asynchronous, for this example.
public sealed class SlowRegistry
{
    public Task<bool> IsBlockedAsync(string inn) => Task.FromResult(inn == "7700000001");
}
```

## Load first, then evaluate

Declare the data as variables, load it asynchronously, then evaluate. The environment and the program are built once and shared: they are immutable and can be used from any number of threads at once.

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Values;

var policy = new InnPolicy(new FixedRegistry(["7700000001"]));
Console.WriteLine(await policy.IsAllowedAsync("7700000001", CancellationToken.None)); // Output: False
Console.WriteLine(await policy.IsAllowedAsync("7700000002", CancellationToken.None)); // Output: True

// The data access of the application, for example a database query.
public interface IInnRegistry
{
    Task<IReadOnlyList<string>> GetBlockedInnsAsync(CancellationToken cancellationToken);
}

public sealed class InnPolicy
{
    private readonly CelProgram<bool> _isAllowed;
    private readonly IInnRegistry _registry;

    public InnPolicy(IInnRegistry registry)
    {
        CelEnvironment env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<string>("inn")
            .AddVariable<CelList>("blockedInns")
            .Build()
            .GetEnvironmentOrThrow();

        _isAllowed = env.Compile<bool>("!(inn in blockedInns)").GetProgramOrThrow();
        _registry = registry;
    }

    public async Task<bool> IsAllowedAsync(string inn, CancellationToken cancellationToken)
    {
        // Await the I/O first...
        IReadOnlyList<string> blockedInns = await _registry.GetBlockedInnsAsync(cancellationToken);

        // ...then evaluate synchronously on the loaded data.
        CelList blocked = CelList.Create([.. blockedInns.Select(CelValue.FromString)]);
        CelBindings bindings = new CelBindings()
            .Add("inn", inn)
            .Add("blockedInns", blocked);
        CelEvalResult<bool> result = _isAllowed.Evaluate(bindings, cancellationToken: cancellationToken);

        // In this policy an evaluation error means "not allowed".
        return result.IsSuccess && result.Value;
    }
}

// A registry with a fixed list, for this example.
public sealed class FixedRegistry(IReadOnlyList<string> blockedInns) : IInnRegistry
{
    public Task<IReadOnlyList<string>> GetBlockedInnsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(blockedInns);
}
```

A `CelList` or a `CelMap` is bound as it is, without a copy, and nothing can change it after it is created. Do not change the data that a running evaluation reads: if the application changes the source of a collection while an evaluation reads it, the result is undefined.

## What else a function implementation should know

- One program can be evaluated from many threads at once, so an implementation can be called concurrently and must be safe for that.
- An exception thrown by an implementation does not escape: the evaluation returns an error with the code `CelErrorCode.ApplicationException`, and `CelError.Exception` holds the original exception.
- The cost limit of `CelEvalOptions` does not measure the time and memory of the implementation. A function whose work grows with its arguments declares its cost with a `CelCostFunction` on its overload declaration.
- Cancellation is checked between macro steps and function calls, not inside a function.
