using System;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// An error of any stage — building an environment, parsing, type checking or evaluation — with its
/// code, stage, English message, position and, if there is one, the exception that caused it.
/// </summary>
/// <remarks>
/// <para>
/// The library returns errors as part of the result of a call — the build, compile and evaluation
/// results — and does not throw them. It throws exceptions only when the conditions of a call that
/// its documentation describes are violated, and <see cref="OperationCanceledException"/> when the
/// evaluation is canceled. An exception thrown by application code during evaluation does not escape
/// either: it becomes an error with the code <see cref="CelErrorCode.ApplicationException"/>, and the
/// original exception is in <see cref="Exception"/>.
/// </para>
/// <para>
/// Compilation returns every error it finds, each with its own position. Evaluation returns one error:
/// when several parts of an expression fail, the language definition does not specify which error
/// is reported.
/// </para>
/// <para>
/// Instances are created by the library only. They are immutable and can be shared between threads.
/// </para>
/// </remarks>
public sealed class CelError
{
    internal CelError(
        CelErrorCode code,
        CelErrorStage stage,
        string message,
        CelSourceLocation location,
        CelSourceLocation? sourceLocation,
        Exception? exception)
    {
        Code = code;
        Stage = stage;
        Message = message;
        Location = location;
        SourceLocation = sourceLocation;
        Exception = exception;
    }

    /// <summary>
    /// The code of the error from the published list of codes.
    /// </summary>
    /// <remarks>
    /// The code tells what went wrong in a form a program can test. Removing a code or changing its
    /// meaning is a breaking change and happens only in a major version; new codes can appear in a
    /// minor version, so a <see langword="switch"/> over codes needs a default branch.
    /// </remarks>
    public CelErrorCode Code { get; }

    /// <summary>
    /// The stage at which the error was found.
    /// </summary>
    /// <remarks>
    /// For codes 9000–9999, which occur at any stage, this is the only indication of the stage.
    /// For a one-step evaluation it tells whether compilation or evaluation failed.
    /// </remarks>
    public CelErrorStage Stage { get; }

    /// <summary>
    /// A description of the error in English, with the names and values involved: the variable, field,
    /// function, overload identifier, type, extension or time zone, and, for a build conflict, both
    /// conflicting sources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text does not depend on the current culture of the process.
    /// </para>
    /// <para>
    /// The text is for people and is not part of the compatibility contract. The contract of an error
    /// is the number, name, meaning and stage of its <see cref="Code"/>. The text can be refined in a
    /// minor version; such changes are listed in the release notes. Applications should not parse the
    /// text: test <see cref="Code"/> and <see cref="Stage"/>, and take positions from
    /// <see cref="Location"/> and <see cref="SourceLocation"/>.
    /// </para>
    /// </remarks>
    public string Message { get; }

    /// <summary>
    /// The position of the error in the expression text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a parse or check error it is the token or the part of the expression where the error was
    /// found; for an evaluation error it is the node that produced the error: a call, a field
    /// selection, an index, or the root of the expression when the result cannot be converted to the
    /// requested .NET type.
    /// </para>
    /// <para>
    /// Errors of the <see cref="CelErrorStage.Build"/> stage have no position in an expression: the
    /// value is <c>default(CelSourceLocation)</c>, whose <see cref="CelSourceLocation.Line"/> is 0.
    /// The position of an error in an environment description read from JSON is in
    /// <see cref="SourceLocation"/>.
    /// </para>
    /// </remarks>
    public CelSourceLocation Location { get; }

    /// <summary>
    /// The position of the error in the source document, or <see langword="null"/> when the error has
    /// no such position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the compilation options name the source document of the expression and the position at
    /// which the expression starts in it, errors of that expression — compilation errors and
    /// evaluation errors of its program — carry both positions: <see cref="Location"/> in the
    /// expression and this position in the document, named by <see cref="CelSourceLocation.SourceName"/>.
    /// </para>
    /// <para>
    /// For an error found while reading an environment description from JSON, this is the position in
    /// the JSON text, in the same units as a position in an expression; the source name names the
    /// description.
    /// </para>
    /// </remarks>
    public CelSourceLocation? SourceLocation { get; }

    /// <summary>
    /// The exception that caused the error, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For <see cref="CelErrorCode.ApplicationException"/> it is the exception thrown by application code
    /// called during evaluation: a function implementation, a member getter of an application object,
    /// a delegate of a type described at run time. For <see cref="CelErrorCode.InternalError"/> it is the
    /// unexpected exception inside the library. For other codes it is <see langword="null"/>.
    /// </para>
    /// <para>
    /// <see cref="OperationCanceledException"/> of the canceled token of the call and
    /// <see cref="OutOfMemoryException"/> are never turned into errors: they propagate to the caller.
    /// </para>
    /// </remarks>
    public Exception? Exception { get; }
}
