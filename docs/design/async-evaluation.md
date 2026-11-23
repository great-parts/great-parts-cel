# Asynchronous functions and the asynchronous driver

An application declares functions with asynchronous implementations (`AddAsyncFunction`, `AddAsyncMethod`, `AddAsyncFunctionImplementation`), and `CelProgram<T>.EvaluateAsync`, `EvaluateInputAsync`, `EvaluatePartialAsync` and the asynchronous one-step calls evaluate expressions that call them. The evaluator stays synchronous: a driver runs it in passes and awaits the calls between passes, with unknown values ([partial-evaluation](partial-evaluation.md)) standing for calls not yet made. Members and documentation are in the XML documentation; this page describes the implementation.

## Declarations

`AddAsyncFunction` and `AddAsyncMethod` take the CEL types from the delegate parameters before the `CancellationToken`, by the same code as `AddFunction` and `AddMethod` (`DelegateDeclaration`, `TypedImplementation`), and produce an `OverloadBinding` with `IsAsync` and an `AsyncImplementation` (`AsyncOverloadImplementation`, `Func<ImmutableArray<CelValue>, CallSite, CancellationToken, ValueTask<CelValue>>`) instead of the synchronous `Implementation`. The typed adapters (`TypedImplementation.OfAsync`) convert each argument by the result rows of the type mapping and the result of the task by the input rows, as the synchronous adapters do; a conversion error is the same evaluation error. `AddAsyncFunctionImplementation` binds a `CelAsyncFunctionImplementation` to a declared overload; a synchronous and an asynchronous implementation of one overload, or two of either, is `DuplicateImplementation` ([declarations](declarations.md)). Type checking does not see whether an overload is asynchronous.

## Planning

The planner plans a call with an asynchronous candidate as an `AsyncCallPlanNode`: one node that evaluates the arguments as a strict call, chooses the candidate as `DispatchPlanNode` does (a candidate of the application that does not wait is called as always), and for an asynchronous candidate does the steps below. A call without an asynchronous candidate is planned as before, so a plan without asynchronous overloads contains no such node. `Plan.HasAsyncCalls` records whether any exists, and `Plan.FirstAsyncOffset` the place of the first in text order, for `AsyncEvaluationRequired`.

`AsyncCallPlanNode.Evaluate` for an asynchronous candidate:

1. evaluate the arguments as a strict call ([partial-evaluation](partial-evaluation.md), "Unknown values in the error channel"): an error or an unknown is the result;
2. count an event and charge the call as an application function (`FixedCost` plus the cost function), by `Invocation.TryChargeApplication`, in every pass that evaluates the node;
3. look the call up in the table of the frame (`AsyncCallTable`) by node id, overload and arguments, as described under "Identity of a call" below. `char` and the integers wider than 64 bits are `string`, `int` and `uint` values in CEL and compare as those; `date` and `time_of_day` of the `calendar` set compare by kind and value;
4. a completed call gives its value: an exception is the error `ApplicationException` with the exception, and a result is checked against the declared result type each time the node reads it (`Invocation.AsyncResult`, charged as for a synchronous function, so the cost of the passes is deterministic); any other call is an unknown value that describes it (node id, function, overload id, a copy of the arguments), and when the frame collects calls (a pass of the driver) the call is registered in the order of the pass. A frame that does not collect calls (`EvaluatePartial`) only describes it.

The conversion of the arguments into the .NET parameter types and of the result back is charged by the same rows as for a synchronous function (a copy of a list of n elements 1 + n, and so on), but the typed adapter runs between two passes, outside any frame. The driver gives each call a `CallSite` whose meter counts the units of that call alone (`AsyncCall.Units`), without looking at the limit; after a wave the driver adds the units of the calls of the wave to the sum charged, in the order the calls were started, and the first call after which the sum exceeds the limit ends the evaluation with `CostLimitExceeded` at the node of that call, with the sum before its units. Neither the order of completion nor the time of a call changes the sum. The memory of a wave is bounded by the window times the size of an argument, which the construction of the argument already charged.

## Identity of a call

Every pass evaluates the whole expression again, so a call that is already registered must be recognized, or the evaluation would register it anew in every pass and never end. A call is the same call when its node, its overload and its arguments are the same; the arguments are the same when they are equal by CEL equality, with these differences:

- a `NaN` equals a `NaN` at any depth: in a list, in the value of a map, in an `optional` and in a field of an object, so `f([0.0 / 0.0])` is made once;
- `1`, `1u` and `1.0` are equal, as in the language, so `[1.0, 1].map(i, f(i))` makes one call;
- two values that are one instance of an object of the application are equal without reading their fields;
- an equality that fails, for example an application description whose equality throws, counts as equal: the arguments come from the same node and the same data, so the call must be found again whatever the description says. An object that is not equal to itself by the `CelEquals` of its description and is made anew each time it is read is therefore registered anew in every pass; that equality is the description's own.

The table looks a call up by a hash of the node, the overload and the arguments. The hash is that of the value rules (`ValueHasher`) in a mode for the table: lists by their elements in order, maps by their entries in any order, optionals, `bytes`, `timestamp` and `duration` by content, and the numbers of all kinds by the `double` they compare as, large ones and infinities included, so equal numbers hash alike and the calls of `ids.map(i, f(i))` with identifiers above 2^53 fall in different buckets, except that integers which round to one `double` share a bucket (at most 2^11 of them per `double`). A `NaN` hashes by one constant. An argument that holds an object of the application, an error or a map that gives an error has no hash of its content, because reading an object reads application data; all the calls of a node with such arguments share one bucket.

The comparisons of a lookup are charged only for a call that has such an argument: its bucket holds the calls of all the objects so far, and each comparison reads fields of the application. They are charged as the rows of equality say, by the meter of the evaluation, and the unit that exceeds the limit is `CostLimitExceeded` at the node of the call. For any other call the hash and one comparison with the call found are proportional to the construction of the arguments, which was charged before. Without a limit nothing is charged. So `orders.map(o, f(o))` over n objects costs up to n²/2 comparisons per pass against the limit; pass a key, `orders.map(o, f(o.id))`, and the cost is that of n calls.

`AppendPlanNode` and `MapInsertPlanNode` evaluate their element after an unknown accumulator and unite the unknown values, so that a `map` over a list calls every element in one wave and not one element per wave.

## The driver

`EvaluateAsync` is not an `async` method: it runs the first pass synchronously, exactly as `Evaluate` (rented frame, `Plan.Run`, release), and returns `new ValueTask<CelEvalResult<T>>(result)` when the root is a value or an error. A program with `HasAsyncCalls` runs the first pass with a frame that collects calls; the table is created on the first registration. When the root is an unknown that waits for calls, the method creates an `AsyncEvaluation<T>` (`AsyncDriver<TResult>`, in `AsyncDriver.cs` and `AsyncEvaluation.cs` of the root namespace) and returns the `ValueTask` of its `async` method `RunAsync`. `OperationCanceledException` of the first pass is returned in the `ValueTask` as the later ones are.

```text
loop
    check the token (ThrowIfCancellationRequested)
    wave = registered calls not yet started, in registration order
    for each call in wave:
        while in-flight == MaxConcurrentAsyncCalls: await the oldest in-flight call; store its outcome
        stop starting when the token is canceled; start the call (invoke the delegate with the arguments and the token)
    await every in-flight call in start order; store each outcome
    check the token
    pass: rent a frame, carry the charged units and the event counter in (Frame.Resume), Plan.Run, carry them out, release
    root is a value or an error, or an unknown with no call waiting -> return the result
```

- **No threads, tasks or timers.** The driver awaits `ValueTask<CelValue>` with `ConfigureAwait(false)`; it does not use `Task`, `Task.WhenAll`, `Task.WhenAny`, `SemaphoreSlim`, `Task.Run` or `Task.Delay`. `Task` and `Task<T>` stay in `eng/BannedSymbols.txt` with the reason "The library creates no threads or tasks; asynchronous evaluation awaits the ValueTask of the application".
- **Waves and determinism.** The pass after a wave starts only when every call of the wave has completed, so the number of passes and their content depend only on the expression and the data. A failed call does not end the wave: the exception is stored in the table, and the pass decides.
- **Frames.** A frame is rented and released inside one pass and never held across an `await` (the continuation may run on another thread, and frames belong to threads). What crosses passes lives in the driver object: the call table, the charged units, the event counter, the bindings, the input (the driver of a structure input is a generic subclass that holds the structure, not boxed) and the patterns of `EvaluatePartialAsync`.
- **Cost.** `Frame.Resume(charged, events)` starts a pass with the remaining limit and the events of the passes before it, and the driver reads `ChargedUnits` back after the pass; `CostLimitException` inside a pass ends the evaluation with `CostLimitExceeded` at the node, with the sum before the refused charge. Without a limit nothing is counted, in any pass.
- **Cancellation.** The event counter continues across passes. The token is checked before each pass and after each wave, and before the start of each call; after a signal no call starts, the calls in flight are awaited (their outcomes ignored), and `OperationCanceledException` is thrown. An `OperationCanceledException` of a call for the token of the evaluation, when it is canceled, ends the evaluation the same way; for another token it is the error `ApplicationException`, as `Plan.IsInternal` decides.
- **Synchronous members.** `Evaluate`, `EvaluateInput` and the synchronous one-step calls of a program with `HasAsyncCalls` return `AsyncEvaluationRequired` at the offset of the first asynchronous call, with cost 0, before renting a frame. `EvaluatePartial` evaluates such a program without a table: the asynchronous calls are unknown values that describe them.

## One-step calls, DI and ASP.NET Core

`CelEnvironment.EvaluateAsync<T>` and `EvaluateInputAsync<T>` (`CelEnvironment.Async.cs`) compile as `Evaluate<T>` does and call the program; a compilation error returns a completed `ValueTask`. `CelCompilationCache` does the same through `GetOrCompile<T>`. `ICelEvaluator` has both members without a default implementation, and `CacheEvaluator` delegates to the cache. In `Great.Parts.Cel.AspNetCore`, `CelAuthorizationHandler` awaits `EvaluateAsync`, `RequestRule` awaits `EvaluateInputAsync`; the endpoint filter and the MVC filter are already asynchronous. The filters pass `HttpContext.RequestAborted` to the evaluation, so after a client has gone no new asynchronous call starts and the calls in flight receive the canceled token; the evaluation ends with `OperationCanceledException`, which ASP.NET Core takes for the end of an aborted request. The authorization handler passes the token when the resource of the authorization is the `HttpContext` of the request, as it is for an endpoint, and `CancellationToken.None` for any other resource, because `IAuthorizationService.AuthorizeAsync` has no token. The options validation stays synchronous: `IValidateOptions<T>.Validate` is synchronous by contract, and an asynchronous function in its expression is `AsyncEvaluationRequired`.

## Allocations

A program that completes in the first pass allocates what `Evaluate` allocates: the result is a `readonly struct` inside a completed `ValueTask`, and no state object is created. The asynchronous path allocates the driver object, the table, an `ImmutableArray<CelValue>` of arguments per distinct call, the `async` state machine and what the application tasks allocate. The synchronous path gains no allocation and no new plan node.

## Tests

The tests of the asynchronous part are in the projects that CI publishes under Native AOT (`Great.Parts.Cel.Tests`, `Great.Parts.Cel.DependencyInjection.Tests`, `Great.Parts.Cel.AspNetCore.Tests`), without reflection and without generic test methods, so the publication shows an IL warning of the library as an error. `browser-wasm` runs the `AsyncControl` case of `Great.Parts.Cel.Conformance.Wasm` under Node.js and the samples `AsynchronousCallAsync` and `Residual` of `Great.Parts.Cel.Wasm.App`, which pass the IL gate. The properties of the equality of the asynchronous and the synchronous evaluation, of the limit by passes and of the residual are on [tests](tests.md#asynchronous-and-partial-evaluation).
