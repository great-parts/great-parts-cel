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
}

/// <summary>
/// The outcome of a case on one side: a compile error, an evaluation error or a value. An exception from the library counts as
/// an error of the step it escaped from.
/// </summary>
internal sealed class SideOutcome
{
    private SideOutcome(OutcomeClass outcomeClass, Value? value, CompileStage stage, string message)
    {
        Class = outcomeClass;
        Result = value;
        Stage = stage;
        Message = message;
    }

    /// <summary>The class of the outcome.</summary>
    internal OutcomeClass Class { get; }

    /// <summary>The value; <see langword="null"/> unless the class is <see cref="OutcomeClass.Value"/>.</summary>
    internal Value? Result { get; }

    /// <summary>The stage of a compile error; <see cref="CompileStage.Parse"/> for the other classes, where it means nothing.</summary>
    internal CompileStage Stage { get; }

    /// <summary>The message of an error; empty for a value. Messages are not compared.</summary>
    internal string Message { get; }

    /// <summary>Whether the outcome is an error of either class.</summary>
    internal bool IsError => Class != OutcomeClass.Value;

    /// <summary>Whether the expression failed to parse.</summary>
    internal bool FailedToParse => Class == OutcomeClass.CompileError && Stage == CompileStage.Parse;

    /// <summary>A value.</summary>
    /// <param name="value">The value.</param>
    internal static SideOutcome Of(Value value)
    {
        return new SideOutcome(OutcomeClass.Value, value, CompileStage.Parse, string.Empty);
    }

    /// <summary>An evaluation error.</summary>
    /// <param name="message">The message of the error.</param>
    internal static SideOutcome EvaluationError(string message)
    {
        return new SideOutcome(OutcomeClass.EvaluationError, null, CompileStage.Parse, message);
    }

    /// <summary>A compile error.</summary>
    /// <param name="stage">The stage that failed.</param>
    /// <param name="message">The message of the error.</param>
    internal static SideOutcome CompileError(CompileStage stage, string message)
    {
        return new SideOutcome(OutcomeClass.CompileError, null, stage, message);
    }
}
