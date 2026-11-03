using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

/// <summary>
/// Tells the evaluation cost of a call of an application function from its arguments, in the cost units of the
/// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>, before the
/// function is called.
/// </summary>
/// <remarks>
/// <para>
/// A call of an application function costs 1 unit plus the value of this function; an overload without a
/// cost function costs 1. When an evaluation has a cost limit, the function is called before the
/// implementation, and the cost is charged first: if the total then exceeds the limit, the
/// implementation is not called and the evaluation ends with the error
/// <see cref="CelErrorCode.CostLimitExceeded"/>. Without a cost limit the function is not called at all.
/// </para>
/// <para>
/// The cost limit bounds both the time and the memory of an evaluation, so a function whose work grows
/// with its arguments — the length of a string, the number of elements of a list — should return a cost
/// that grows the same way. The library does not limit the time and memory of the implementation
/// itself. An exception thrown by the cost function becomes the evaluation error
/// <see cref="CelErrorCode.ApplicationException"/>, like an exception of the implementation.
/// </para>
/// <para>The function can be called from any number of threads at once.</para>
/// </remarks>
/// <param name="arguments">
/// The arguments of the call, in the same order as the implementation receives them. The span is valid
/// only during the call.
/// </param>
/// <returns>The cost of the call in units, in addition to the fixed unit of the call.</returns>
public delegate ulong CelCostFunction(ReadOnlySpan<CelValue> arguments);
