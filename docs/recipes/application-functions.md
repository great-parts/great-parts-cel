# Application functions

An application can add its own functions to an environment: `CelEnvironmentBuilder.AddFunction` with a .NET delegate, `CelEnvironmentBuilder.AddFunctionImplementation` for a declared overload, or an extension set built with `CelExtension.Create`. This recipe shows how to give such functions the data they need.

## The rule

Evaluation is synchronous: a function implementation is called on the thread that evaluates the expression and must return its result before evaluation continues. The library itself does no I/O during evaluation.

So:

- load the data an expression needs before the evaluation, with `await` as usual, and pass it in `CelBindings`;
- do not wait for asynchronous work inside a function implementation: no `.Result`, no `.Wait()`, no `.GetAwaiter().GetResult()`.

## Why not block in a function

A function that calls `.Result` or `.Wait()` on a database query or an HTTP request holds a thread until the I/O completes. In ASP.NET Core the thread comes from the thread pool: under load, requests block all pool threads, new requests wait for threads, and the application stops responding although the processor is idle. In `browser-wasm` there is only one thread, so the call never completes and the page hangs.

```csharp skip: a fragment of the wrong way, shown only to be avoided
// Do not do this: the implementation blocks a thread until the query completes.
builder.AddFunction("isBlocked", (string inn) => registry.IsBlockedAsync(inn).Result);
```

## Load first, then evaluate

Declare the data as variables, load it asynchronously, then evaluate. The environment and the program are built once and shared: they are immutable and can be used from any number of threads at once.

```csharp skip: the library does not implement yet the parts of the API and the operators that this program uses
using Great.Parts.Cel;

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
            .AddVariable<IReadOnlyList<string>>("blockedInns")
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
        CelBindings bindings = new CelBindings()
            .Add("inn", inn)
            .Add("blockedInns", blockedInns);
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

Bindings keep application objects by reference and read them lazily: a large object graph is not copied, and only the members an expression reads are read. If the application changes the objects while an evaluation reads them, the result is undefined: do not change data that a running evaluation reads.

## What else a function implementation should know

- One program can be evaluated from many threads at once, so an implementation can be called concurrently and must be safe for that.
- An exception thrown by an implementation does not escape: the evaluation returns an error with the code `CelErrorCode.ApplicationException`, and `CelError.Exception` holds the original exception.
- The cost limit of `CelEvalOptions` does not measure the time and memory of the implementation. A function whose work grows with its arguments declares its cost with a `CelCostFunction` on its overload declaration.
- Cancellation is checked between macro steps and function calls, not inside a function.
