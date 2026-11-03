using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

/// <summary>
/// Options of an evaluation, such as the cost limit.
/// </summary>
/// <remarks>
/// The options are immutable once created and can be shared between threads and evaluations. The
/// cancellation token is a separate argument of the evaluation methods.
/// </remarks>
public sealed class CelEvalOptions
{
    /// <summary>
    /// Creates options without a cost limit.
    /// </summary>
    public CelEvalOptions()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The cost limit of one evaluation in the cost units of the
    /// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>, or
    /// <see langword="null"/> for no limit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The limit bounds both the time and the memory of an evaluation: one unit stands for a fixed amount of work and a
    /// bounded number of allocated bytes. The cost of each operation is known before it runs and depends only on the
    /// expression and the data, not on caches or on earlier calls. When charging the next operation would make the
    /// total greater than the limit, the operation does not run and the evaluation ends with the error
    /// <see cref="CelErrorCode.CostLimitExceeded"/>; a total equal to the limit is allowed. Costs of operations are on the page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>;
    /// an application function costs 1 unit plus what its <see cref="CelCostFunction"/> returns.
    /// </para>
    /// <para>
    /// There is no limit by default, and without a limit no cost is counted at all. Set a limit for
    /// expressions that come from untrusted sources. Compilation is not charged: its work is bounded by the
    /// limits on the length and nesting of the expression.
    /// </para>
    /// </remarks>
    public ulong? CostLimit
    {
        get => throw new NotImplementedException();
        init => throw new NotImplementedException();
    }
}
