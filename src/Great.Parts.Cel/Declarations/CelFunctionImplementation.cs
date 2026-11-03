using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

/// <summary>
/// The .NET code that computes one overload of a function from CEL values: the general form of an
/// implementation, for overloads with parameters of type <c>dyn</c>, generic overloads and overloads
/// called on a receiver.
/// </summary>
/// <remarks>
/// <para>
/// The implementation is bound to its overload by the overload identifier when the environment is built. The
/// environment builder also takes typed .NET delegates, such as <c>Func&lt;string, bool&gt;</c>, whose parameters it
/// converts by
/// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
/// this delegate receives the CEL values as they are. As the CEL language definition requires, an argument that
/// evaluates to an error ends the call before the implementation runs, so no argument has the kind
/// <see cref="CelValueKind.Error"/>; an element of a list or map over application data can still be such a value. The
/// result must have the result type of the overload: a result whose kind does not fit it is the evaluation error
/// <see cref="CelErrorCode.ImplementationResultMismatch"/>, which names the function and the overload identifier. A
/// result type <c>dyn</c> accepts any value, and a returned value of the kind <see cref="CelValueKind.Error"/> is
/// propagated as an error value, as an error of any other call.
/// </para>
/// <para>
/// An exception thrown by the implementation does not escape the evaluation: it becomes the evaluation
/// error with the code <see cref="CelErrorCode.ApplicationException"/>, which keeps the original exception
/// in <see cref="CelError.Exception"/>. Evaluation does not limit the time and memory of the
/// implementation itself: the cost limit counts only the cost the overload declares with
/// <see cref="CelCostFunction"/>, and cancellation is checked between calls, not inside them.
/// </para>
/// <para>
/// One program can be evaluated from any number of threads at once, so the implementation can be called
/// concurrently and must be safe for that.
/// </para>
/// <para>
/// Evaluation is synchronous, and so is the implementation. Do not block in it on asynchronous work —
/// <c>Task.Result</c>, <c>Task.Wait()</c>, <c>GetAwaiter().GetResult()</c> — for example to query a database or a
/// service: a thread blocked on I/O is taken from the thread pool, which starves ASP.NET Core under load, and in
/// <c>browser-wasm</c>, which has one thread, the call hangs. Load the data the expression needs before the evaluation
/// and pass it in the bindings, as the
/// recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
/// shows.
/// </para>
/// </remarks>
/// <param name="arguments">
/// The arguments in the order of the parameter types of the overload; for an overload called on a
/// receiver, the receiver comes first. The span is valid only during the call.
/// </param>
/// <returns>The result of the call, a value of the result type of the overload.</returns>
public delegate CelValue CelFunctionImplementation(ReadOnlySpan<CelValue> arguments);
