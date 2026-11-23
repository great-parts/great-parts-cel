using System.Collections.Generic;
using Cel.Expr;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// The result of asking a side for the checked expression of a text: its bytes, "does not compile", or a failure of the side
/// itself that the comparison reports (docs/design/ast-protobuf.md, "Exchange with cel-go").
/// </summary>
internal sealed class CheckedAttempt
{
    private CheckedAttempt(byte[]? bytes, SideOutcome? failure)
    {
        Bytes = bytes;
        Failure = failure;
    }

    /// <summary>The bytes of the <c>CheckedExpr</c> message of the check; <see langword="null"/> when the side made none.</summary>
    internal byte[]? Bytes { get; }

    /// <summary>The outcome that stands for a side that failed while it made the bytes; <see langword="null"/> otherwise.</summary>
    internal SideOutcome? Failure { get; }

    /// <summary>The text does not compile on the side: it is no case of the exchange.</summary>
    internal static CheckedAttempt NotCompiled { get; } = new(null, null);

    /// <summary>The side made the bytes of the message.</summary>
    /// <param name="bytes">The bytes.</param>
    internal static CheckedAttempt Made(byte[] bytes) => new(bytes, null);

    /// <summary>The side threw, or gave no message for a text that it compiled.</summary>
    /// <param name="outcome">The outcome that stands for it.</param>
    internal static CheckedAttempt Failed(SideOutcome outcome) => new(null, outcome);
}

/// <summary>
/// A side that gives the checked expression of a text as the bytes of the message <c>cel.expr.CheckedExpr</c> and evaluates such
/// bytes: the library with its protobuf form, the oracle with the requests <c>checked</c> (docs/design/ast-protobuf.md,
/// "Exchange with cel-go"). The fake sides of the tests do not have it, and a run with one has no exchange.
/// </summary>
internal interface ICheckedSide
{
    /// <summary>Parses and checks a text and writes its checked expression.</summary>
    /// <param name="expression">The expression as CEL source.</param>
    /// <returns>The bytes, or that the text does not compile, or the failure of the side.</returns>
    CheckedAttempt Check(string expression);

    /// <summary>Loads the bytes of a checked expression, compiles them in the environment of the side and evaluates them.</summary>
    /// <param name="checkedExpr">The bytes of a <c>CheckedExpr</c> message.</param>
    /// <param name="bindings">The bound variables; a variable of the manifest that is absent is unbound.</param>
    /// <returns>The outcome: a compile error, an evaluation error or a value.</returns>
    SideOutcome EvaluateChecked(byte[] checkedExpr, IReadOnlyDictionary<string, Value> bindings);
}
