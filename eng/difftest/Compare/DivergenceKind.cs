using System;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>The kind of a divergence of the outcomes of a case (docs/design/eng-difftest.md, "Outcomes and signatures").</summary>
internal enum DivergenceKind
{
    /// <summary><c>value-mismatch</c>: both sides give values, and the values differ.</summary>
    ValueMismatch,

    /// <summary><c>we-value</c>: the library gives a value, cel-go an error.</summary>
    WeValue,

    /// <summary><c>we-error</c>: the library gives an error, cel-go a value.</summary>
    WeError,

    /// <summary><c>stage-mismatch</c>: one side gives a compile error, the other an evaluation error.</summary>
    StageMismatch,
}

/// <summary>The severity of a divergence: S1 is silent, S2 is loud.</summary>
internal enum DivergenceSeverity
{
    /// <summary>A silent divergence: the library gives a value where cel-go gives another value or an error.</summary>
    S1,

    /// <summary>A loud divergence: the library gives an error.</summary>
    S2,
}

/// <summary>The names and severities of the kinds, and the comparison of two outcomes.</summary>
internal static class DivergenceKinds
{
    /// <summary>The name of a kind in a signature: <c>value-mismatch</c>, <c>we-value</c>, <c>we-error</c>, <c>stage-mismatch</c>.</summary>
    /// <param name="kind">The kind.</param>
    internal static string Name(this DivergenceKind kind) => kind switch
    {
        DivergenceKind.ValueMismatch => "value-mismatch",
        DivergenceKind.WeValue => "we-value",
        DivergenceKind.WeError => "we-error",
        DivergenceKind.StageMismatch => "stage-mismatch",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of divergence."),
    };

    /// <summary>The severity of a kind: <c>value-mismatch</c> and <c>we-value</c> are S1, <c>we-error</c> and <c>stage-mismatch</c> S2.</summary>
    /// <param name="kind">The kind.</param>
    internal static DivergenceSeverity Severity(this DivergenceKind kind) =>
        kind is DivergenceKind.ValueMismatch or DivergenceKind.WeValue ? DivergenceSeverity.S1 : DivergenceSeverity.S2;

    /// <summary>
    /// Compares the outcomes of a case. Equal values agree, and so do two errors of one class; error messages and the stages of
    /// two compile errors are not compared.
    /// </summary>
    /// <param name="library">The outcome of the library.</param>
    /// <param name="oracle">The outcome of cel-go.</param>
    /// <returns>The kind of the divergence, or <see langword="null"/> when the outcomes agree.</returns>
    internal static DivergenceKind? Compare(SideOutcome library, SideOutcome oracle)
    {
        return (library.IsError, oracle.IsError) switch
        {
            (false, false) => ValueEquality.Equal(library.Result!, oracle.Result!) ? null : DivergenceKind.ValueMismatch,
            (false, true) => DivergenceKind.WeValue,
            (true, false) => DivergenceKind.WeError,
            _ => library.Class == oracle.Class ? null : DivergenceKind.StageMismatch,
        };
    }
}
