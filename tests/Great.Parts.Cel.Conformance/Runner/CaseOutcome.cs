using System;
using System.Collections.Generic;
using Cel.Expr;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>Runs a case: <c>LibraryEvaluator</c> calls the library, the tests of the runner use a scripted one.</summary>
internal interface ICaseEvaluator
{
    /// <summary>Runs the case as far as it goes.</summary>
    /// <param name="testCase">The case.</param>
    /// <returns>How far the case got and what it gave; an exception of the library is part of the outcome.</returns>
    CaseOutcome Evaluate(ConformanceCase testCase);
}

/// <summary>A stage of a case, in the order of the rule.</summary>
internal enum CaseStage
{
    /// <summary>The expression is parsed.</summary>
    Parse,

    /// <summary>The setup gets what the case declares and is built.</summary>
    Environment,

    /// <summary>The expression is checked and, for a case that is evaluated, planned.</summary>
    Check,

    /// <summary>The expression is evaluated with the bindings.</summary>
    Evaluate,
}

/// <summary>
/// What a case gave: how far it got, the errors of the failed stage, the result type, the value or the evaluation error, or an
/// exception.
/// </summary>
internal sealed record CaseOutcome
{
    private CaseOutcome(CaseStage stage, IReadOnlyList<string> errors, Exception? exception, Type? resultType, Value? value)
    {
        Stage = stage;
        Errors = errors;
        Exception = exception;
        ResultType = resultType;
        Value = value;
    }

    /// <summary>The last stage the case reached: the stage that failed or threw, or the last stage it completed.</summary>
    internal CaseStage Stage { get; }

    /// <summary>The errors of the failed stage; at <see cref="CaseStage.Evaluate"/> the evaluation error. Empty when no stage failed.</summary>
    internal IReadOnlyList<string> Errors { get; }

    /// <summary>The exception thrown in <see cref="Stage"/>, or <see langword="null"/>.</summary>
    internal Exception? Exception { get; }

    /// <summary>The result type after the check, or <see langword="null"/> when the case did not get that far.</summary>
    internal Type? ResultType { get; }

    /// <summary>The value of an evaluation without error, or <see langword="null"/>.</summary>
    internal Value? Value { get; }

    /// <summary>A stage failed with errors.</summary>
    /// <param name="stage">The stage, one before <see cref="CaseStage.Evaluate"/>.</param>
    /// <param name="errors">The errors.</param>
    internal static CaseOutcome Failed(CaseStage stage, params IReadOnlyList<string> errors) => new(stage, errors, null, null, null);

    /// <summary>A stage threw an exception.</summary>
    /// <param name="stage">The stage.</param>
    /// <param name="exception">The exception.</param>
    internal static CaseOutcome Threw(CaseStage stage, Exception exception) => new(stage, [], exception, null, null);

    /// <summary>The check passed and nothing was evaluated: a <c>check_only</c> case.</summary>
    /// <param name="resultType">The result type.</param>
    internal static CaseOutcome Checked(Type resultType) => new(CaseStage.Check, [], null, resultType, null);

    /// <summary>The evaluation gave a value.</summary>
    /// <param name="resultType">The result type.</param>
    /// <param name="value">The value.</param>
    internal static CaseOutcome Evaluated(Type resultType, Value value) => new(CaseStage.Evaluate, [], null, resultType, value);

    /// <summary>The evaluation gave an error.</summary>
    /// <param name="resultType">The result type.</param>
    /// <param name="errors">The messages of the error.</param>
    internal static CaseOutcome EvaluationFailed(Type resultType, params IReadOnlyList<string> errors) =>
        new(CaseStage.Evaluate, errors, null, resultType, null);
}
