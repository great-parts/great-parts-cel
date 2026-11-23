using System;
using System.Linq;
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

    /// <summary><c>unknown-mismatch</c>: both sides give unknown parts of the data, and the sets of trails differ.</summary>
    UnknownMismatch,

    /// <summary><c>we-unknown</c>: the library gives unknown parts, cel-go a value or an error.</summary>
    WeUnknown,

    /// <summary><c>we-known</c>: cel-go gives unknown parts, the library a value or an error.</summary>
    WeKnown,
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
    /// <summary>
    /// The name of a kind in a signature: <c>value-mismatch</c>, <c>we-value</c>, <c>we-error</c>, <c>stage-mismatch</c>, and of
    /// partial evaluation <c>unknown-mismatch</c>, <c>we-unknown</c>, <c>we-known</c>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    internal static string Name(this DivergenceKind kind) => kind switch
    {
        DivergenceKind.ValueMismatch => "value-mismatch",
        DivergenceKind.WeValue => "we-value",
        DivergenceKind.WeError => "we-error",
        DivergenceKind.StageMismatch => "stage-mismatch",
        DivergenceKind.UnknownMismatch => "unknown-mismatch",
        DivergenceKind.WeUnknown => "we-unknown",
        DivergenceKind.WeKnown => "we-known",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of divergence."),
    };

    /// <summary>
    /// The severity of a kind: <c>value-mismatch</c> and <c>we-value</c> are S1, the others S2. For <c>we-known</c> this is the
    /// severity when the library gives an error; with a value it is S1 (the overload that takes the outcome of the library).
    /// </summary>
    /// <param name="kind">The kind.</param>
    internal static DivergenceSeverity Severity(this DivergenceKind kind) =>
        kind is DivergenceKind.ValueMismatch or DivergenceKind.WeValue ? DivergenceSeverity.S1 : DivergenceSeverity.S2;

    /// <summary>
    /// The severity of a divergence of a case: that of its kind, except that <c>we-known</c> is S1 when the library gives a value
    /// where cel-go gives unknown parts, since the library then claims to know what is not known.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="library">The outcome of the library.</param>
    internal static DivergenceSeverity Severity(this DivergenceKind kind, SideOutcome library) =>
        kind == DivergenceKind.WeKnown && library.Class == OutcomeClass.Value ? DivergenceSeverity.S1 : kind.Severity();

    /// <summary>
    /// Compares the outcomes of a case. Equal values agree, and so do two errors of one class; error messages and the stages of
    /// two compile errors are not compared. Two unknown outcomes agree when they name the same unknown parts, without the ids of the
    /// nodes. An unknown outcome against another class is a divergence.
    /// </summary>
    /// <param name="library">The outcome of the library.</param>
    /// <param name="oracle">The outcome of cel-go.</param>
    /// <returns>The kind of the divergence, or <see langword="null"/> when the outcomes agree.</returns>
    internal static DivergenceKind? Compare(SideOutcome library, SideOutcome oracle)
    {
        if (library.IsUnknown || oracle.IsUnknown)
        {
            return (library.IsUnknown, oracle.IsUnknown) switch
            {
                (true, true) => library.Trails.Select(trail => trail.Key).SequenceEqual(oracle.Trails.Select(trail => trail.Key), StringComparer.Ordinal)
                    ? null
                    : DivergenceKind.UnknownMismatch,
                (true, false) => DivergenceKind.WeUnknown,
                _ => DivergenceKind.WeKnown,
            };
        }

        return (library.IsError, oracle.IsError) switch
        {
            (false, false) => ValueEquality.Equal(library.Result!, oracle.Result!) ? null : DivergenceKind.ValueMismatch,
            (false, true) => DivergenceKind.WeValue,
            (true, false) => DivergenceKind.WeError,
            _ => library.Class == oracle.Class ? null : DivergenceKind.StageMismatch,
        };
    }
}
