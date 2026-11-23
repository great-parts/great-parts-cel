using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

/// <summary>
/// The asynchronous implementation of an overload in .NET: receives the CEL values of the arguments, receiver first, and the
/// cancellation token of the evaluation, and returns a task of the CEL value of the result.
/// </summary>
/// <remarks>
/// <para>
/// Given to an overload by <see cref="CelEnvironmentBuilder.AddAsyncFunctionImplementation"/>. The evaluation calls it once per
/// distinct call — the same arguments at the same place of the expression — and keeps the arguments until the next pass, so
/// they come as an immutable array. The result must fit the result type of the overload, otherwise the call is the evaluation
/// error <c>ImplementationResultMismatch</c>; an exception, thrown or in the task, is the evaluation error
/// <c>ApplicationException</c>.
/// </para>
/// <para>
/// Honor <paramref name="cancellationToken"/>: after the evaluation is canceled it awaits the calls it started. The
/// implementation can be called from several evaluations at once.
/// </para>
/// </remarks>
/// <param name="arguments">The arguments, receiver first.</param>
/// <param name="cancellationToken">The cancellation token of the evaluation.</param>
/// <returns>A task of the result.</returns>
public delegate ValueTask<CelValue> CelAsyncFunctionImplementation(ImmutableArray<CelValue> arguments, CancellationToken cancellationToken);
