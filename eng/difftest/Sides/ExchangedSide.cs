using System;
using System.Collections.Generic;
using Cel.Expr;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// One of the two exchanges of a checked expression between the sides (docs/design/ast-protobuf.md, "Exchange with cel-go"): the
/// <c>maker</c> checks the text and writes the bytes of its <c>CheckedExpr</c>, the <c>evaluator</c> loads them and evaluates
/// them. The outcome is that of the evaluator for the bytes, to be compared with its outcome for the text, the reference. The
/// library as the maker and the oracle as the evaluator is the result of cel-go from our bytes; the oracle as the maker and the
/// library as the evaluator is our result from the bytes of cel-go. It is a side for the search and the signature only: it has no
/// time zones.
/// </summary>
/// <remarks>
/// A text that does not compile on the evaluator, or on the maker, is no case of the exchange, and the outcome is that of the
/// reference, so the two agree and the descent of the minimization drops it as it drops any agreeing node.
/// </remarks>
internal sealed class ExchangedSide : ICaseSide
{
    private readonly ICheckedSide maker;
    private readonly ICheckedSide evaluator;
    private readonly ICaseSide reference;

    /// <summary>Creates an exchange.</summary>
    /// <param name="maker">The side that writes the bytes.</param>
    /// <param name="evaluator">The side that loads and evaluates them.</param>
    /// <param name="reference">The evaluator of the text, the same side as <paramref name="evaluator"/>.</param>
    internal ExchangedSide(ICheckedSide maker, ICheckedSide evaluator, ICaseSide reference)
    {
        this.maker = maker;
        this.evaluator = evaluator;
        this.reference = reference;
    }

    /// <inheritdoc/>
    public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings) =>
        Evaluate(expression, bindings, reference.Evaluate(expression, bindings));

    /// <summary>The outcome of the exchange when the outcome of the evaluator for the text is already known.</summary>
    /// <param name="expression">The expression as CEL source.</param>
    /// <param name="bindings">The bound variables.</param>
    /// <param name="referenceOutcome">The outcome of the evaluator for the text.</param>
    internal SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings, SideOutcome referenceOutcome)
    {
        if (referenceOutcome.Class == OutcomeClass.CompileError)
        {
            return referenceOutcome;
        }

        CheckedAttempt made = maker.Check(expression);
        if (made.Failure is { } failure)
        {
            return failure;
        }

        return made.Bytes is { } bytes ? evaluator.EvaluateChecked(bytes, bindings) : referenceOutcome;
    }

    /// <summary>The exchange compares outcomes of expressions only.</summary>
    /// <param name="zone">The name of a zone.</param>
    /// <param name="instant">The instant.</param>
    /// <exception cref="NotSupportedException">Always: the offsets of the zones are compared between the sides themselves.</exception>
    public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) =>
        throw new NotSupportedException("an exchange of a checked expression has no time zones");
}
