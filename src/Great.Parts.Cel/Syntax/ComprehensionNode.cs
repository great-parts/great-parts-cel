namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A comprehension, the <c>Expr.Comprehension</c> message of <c>syntax.proto</c>: a loop over the elements of a list
/// or the keys of a map with an accumulator, or over the indexes and elements of a list or the keys and values of a map
/// when it has two iteration variables. Macros such as <c>all</c> and <c>map</c> expand into it; the text has no syntax of
/// its own for it.
/// </summary>
internal sealed class ComprehensionNode : ExprNode
{
    internal ComprehensionNode(
        int id,
        string iterVar,
        string? iterVar2,
        ExprNode iterRange,
        string accuVar,
        ExprNode accuInit,
        ExprNode loopCondition,
        ExprNode loopStep,
        ExprNode result)
        : base(id)
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
    internal string IterVar { get; }

    /// <summary>
    /// The name of the second iteration variable of a comprehension of two variables: the element of a list after its index, the
    /// value of a map after its key; <see langword="null"/> for a comprehension of one variable.
    /// </summary>
    internal string? IterVar2 { get; }

    /// <summary>
    /// The range: a list, whose elements are iterated, or a map, whose keys are.
    /// </summary>
    internal ExprNode IterRange { get; }

    /// <summary>
    /// The name of the accumulator variable.
    /// </summary>
    internal string AccuVar { get; }

    /// <summary>
    /// The initial value of the accumulator.
    /// </summary>
    internal ExprNode AccuInit { get; }

    /// <summary>
    /// The condition checked before each step; the loop ends early when it is <see langword="false"/>.
    /// </summary>
    internal ExprNode LoopCondition { get; }

    /// <summary>
    /// The step: the next value of the accumulator.
    /// </summary>
    internal ExprNode LoopStep { get; }

    /// <summary>
    /// The result, computed from the accumulator after the loop.
    /// </summary>
    internal ExprNode Result { get; }
}
