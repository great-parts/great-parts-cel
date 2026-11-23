using System;
using System.Collections.Generic;
using Cel.Expr;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// A side that evaluates partially: the library through <c>EvaluatePartial</c>, the oracle through the request <c>partial</c>
/// (docs/design/eng-difftest.md, "Partial evaluation"). A side that does not implement it takes no part in the partial comparison.
/// </summary>
internal interface IPartialSide
{
    /// <summary>Compiles an expression for partial evaluation and evaluates it with patterns of unknown data.</summary>
    /// <param name="expression">The expression as CEL source.</param>
    /// <param name="bindings">The bound variables; a variable of the manifest that is absent is unbound.</param>
    /// <param name="patterns">The patterns of unknown data.</param>
    /// <returns>The outcome: a compile error, an evaluation error, a value, or the unknown trails.</returns>
    SideOutcome EvaluatePartial(string expression, IReadOnlyDictionary<string, Value> bindings, IReadOnlyList<UnknownAttribute> patterns);
}

/// <summary>
/// A partial side seen as a side of the comparison: <see cref="Evaluate"/> evaluates with the patterns of the case, so the
/// minimization and the signatures work on partial evaluation as they work on the ordinary one.
/// </summary>
/// <param name="inner">The side that evaluates partially.</param>
/// <param name="patterns">The patterns of the case.</param>
internal sealed class PartialSide(IPartialSide inner, IReadOnlyList<UnknownAttribute> patterns) : ICaseSide
{
    /// <inheritdoc/>
    public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings) =>
        inner.EvaluatePartial(expression, bindings, patterns);

    /// <inheritdoc/>
    public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) =>
        throw new NotSupportedException("a partial side has no time zones");
}
