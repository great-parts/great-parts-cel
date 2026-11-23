using System;
using System.Collections.Generic;
using System.Linq;
using Cel.Expr;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>The class of the outcome of a case on one side (docs/design/eng-difftest.md, "Outcomes and signatures").</summary>
internal enum OutcomeClass
{
    /// <summary>The expression did not compile: parse, check, or in cel-go also creating the program.</summary>
    CompileError,

    /// <summary>The evaluation returned an error.</summary>
    EvaluationError,

    /// <summary>The evaluation returned a value.</summary>
    Value,

    /// <summary>The partial evaluation returned unknown parts of the data (docs/design/eng-difftest.md, "Partial evaluation").</summary>
    Unknown,
}

/// <summary>The stage at which an expression did not compile.</summary>
internal enum CompileStage
{
    /// <summary>Parsing.</summary>
    Parse,

    /// <summary>Type checking.</summary>
    Check,

    /// <summary>Creating the program; cel-go only.</summary>
    Program,

    /// <summary>Loading the bytes of a checked expression, which only the exchange of a checked expression has.</summary>
    Load,
}

/// <summary>
/// The outcome of a case on one side: a compile error, an evaluation error, a value, or in partial evaluation the unknown parts of
/// the data. An exception from the library counts as an error of the step it escaped from.
/// </summary>
internal sealed class SideOutcome
{
    private SideOutcome(OutcomeClass outcomeClass, Value? value, CompileStage stage, string message, IReadOnlyList<UnknownAttribute> trails)
    {
        Class = outcomeClass;
        Result = value;
        Stage = stage;
        Message = message;
        Trails = trails;
    }

    /// <summary>The class of the outcome.</summary>
    internal OutcomeClass Class { get; }

    /// <summary>The value; <see langword="null"/> unless the class is <see cref="OutcomeClass.Value"/>.</summary>
    internal Value? Result { get; }

    /// <summary>The stage of a compile error; <see cref="CompileStage.Parse"/> for the other classes, where it means nothing.</summary>
    internal CompileStage Stage { get; }

    /// <summary>The message of an error; empty for a value. Messages are not compared.</summary>
    internal string Message { get; }

    /// <summary>
    /// The unknown parts of the data, each once, ordered by <see cref="UnknownAttribute.Key"/>; empty unless the class is
    /// <see cref="OutcomeClass.Unknown"/>. The ids of the nodes that became unknown are not compared.
    /// </summary>
    internal IReadOnlyList<UnknownAttribute> Trails { get; }

    /// <summary>Whether the outcome is an error of either class.</summary>
    internal bool IsError => Class is OutcomeClass.CompileError or OutcomeClass.EvaluationError;

    /// <summary>Whether the outcome is unknown parts of the data.</summary>
    internal bool IsUnknown => Class == OutcomeClass.Unknown;

    /// <summary>Whether the expression failed to parse.</summary>
    internal bool FailedToParse => Class == OutcomeClass.CompileError && Stage == CompileStage.Parse;

    /// <summary>A value.</summary>
    /// <param name="value">The value.</param>
    internal static SideOutcome Of(Value value)
    {
        return new SideOutcome(OutcomeClass.Value, value, CompileStage.Parse, string.Empty, []);
    }

    /// <summary>An evaluation error.</summary>
    /// <param name="message">The message of the error.</param>
    internal static SideOutcome EvaluationError(string message)
    {
        return new SideOutcome(OutcomeClass.EvaluationError, null, CompileStage.Parse, message, []);
    }

    /// <summary>A compile error.</summary>
    /// <param name="stage">The stage that failed.</param>
    /// <param name="message">The message of the error.</param>
    internal static SideOutcome CompileError(CompileStage stage, string message)
    {
        return new SideOutcome(OutcomeClass.CompileError, null, stage, message, []);
    }

    /// <summary>Unknown parts of the data.</summary>
    /// <param name="trails">The unknown attributes; one that repeats counts once.</param>
    internal static SideOutcome Unknown(IEnumerable<UnknownAttribute> trails)
    {
        IReadOnlyList<UnknownAttribute> ordered = [.. trails.DistinctBy(trail => trail.Key, StringComparer.Ordinal).OrderBy(trail => trail.Key, StringComparer.Ordinal)];
        return new SideOutcome(OutcomeClass.Unknown, null, CompileStage.Parse, string.Empty, ordered);
    }
}
