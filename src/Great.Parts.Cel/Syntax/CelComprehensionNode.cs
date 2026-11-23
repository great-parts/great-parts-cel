
namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A comprehension: the <c>Expr.Comprehension</c> message of <c>syntax.proto</c>, a loop over the elements of a list or
/// the keys of a map with an accumulator, or, with a second iteration variable, over the indexes and elements of a list or
/// the keys and values of a map.
/// </summary>
/// <remarks>
/// <para>
/// The text has no syntax of its own for a comprehension: macros such as <c>all</c>, <c>exists</c>, <c>map</c>,
/// <c>filter</c> and <c>cel.bind</c> expand into it, and <c>CelParsedExpr.GetMacroCall</c> gives the call as written. The
/// evaluation sets the accumulator to <see cref="AccuInit"/>, then for each element, while <see cref="LoopCondition"/> is
/// <see langword="true"/>, sets it to <see cref="LoopStep"/>, and finally gives <see cref="Result"/>.
/// </para>
/// <para>The node is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelComprehensionNode : CelExprNode
{
    internal CelComprehensionNode(
        int id,
        string iterVar,
        string? iterVar2,
        CelExprNode iterRange,
        string accuVar,
        CelExprNode accuInit,
        CelExprNode loopCondition,
        CelExprNode loopStep,
        CelExprNode result)
        : base(id, CelExprKind.Comprehension)
    {
        IterVar = iterVar;
        IterVar2 = iterVar2;
        IterRange = iterRange;
        AccuVar = accuVar;
        AccuInit = accuInit;
        LoopCondition = loopCondition;
        LoopStep = loopStep;
        Result = result;
    }

    /// <summary>
    /// The name of the iteration variable.
    /// </summary>
    public string IterVar { get; }

    /// <summary>
    /// The name of the second iteration variable — the element of a list after its index, the value of a map after its key —
    /// or <see langword="null"/> for a comprehension of one variable.
    /// </summary>
    public string? IterVar2 { get; }

    /// <summary>
    /// The range: a list, whose elements are iterated, or a map, whose keys are.
    /// </summary>
    public CelExprNode IterRange { get; }

    /// <summary>
    /// The name of the accumulator variable; the macros of the library use <c>@result</c>, which no text can write.
    /// </summary>
    public string AccuVar { get; }

    /// <summary>
    /// The initial value of the accumulator.
    /// </summary>
    public CelExprNode AccuInit { get; }

    /// <summary>
    /// The condition evaluated before each step; the loop ends early when it is <see langword="false"/>.
    /// </summary>
    public CelExprNode LoopCondition { get; }

    /// <summary>
    /// The step: the next value of the accumulator.
    /// </summary>
    public CelExprNode LoopStep { get; }

    /// <summary>
    /// The result, computed from the accumulator after the loop.
    /// </summary>
    public CelExprNode Result { get; }

    private protected override CelExprNode[] CreateChildren() => [IterRange, AccuInit, LoopCondition, LoopStep, Result];
}
