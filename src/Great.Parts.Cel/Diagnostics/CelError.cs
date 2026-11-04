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
    // Where the message and the position come from: the line table of the expression for an error of evaluation, the
    // given message and positions, or null for an error of evaluation that has no place yet.
    private readonly object? _source;

    // Forms the message of an error of evaluation from its parts when it is read; null when the message was given.
    private readonly Func<CelError, string>? _format;

    // The parts of the message of an error of evaluation: references to objects that already exist, a number and a
    // byte, so that creating the error allocates this object only. An exception that caused the error is the first part.
    private readonly object? _first;
    private readonly object? _second;
    private readonly long _number;
    private readonly byte _small;

    // The offset of the node of an error of evaluation, for its line table.
    private readonly int _offset;
    private readonly ushort _code;
    private readonly byte _stage;

    internal CelError(
        CelErrorCode code,
        CelErrorStage stage,
        string message,
        CelSourceLocation location,
        CelSourceLocation? sourceLocation,
        Exception? exception)
    {
        _code = (ushort)code;
        _stage = (byte)stage;
        _source = new Given(message, location, sourceLocation, exception);
    }

    // An error of evaluation whose message and position are formed only when they are read, so that an error value
    // that a later operator absorbs costs no text. Without a line table the error has no position.
    internal CelError(
        CelErrorCode code,
        Func<CelError, string> format,
        LineTable? lines,
        int offset,
        object? first = null,
        object? second = null,
        long number = 0,
        byte small = 0)
    {
        _code = (ushort)code;
        _stage = (byte)CelErrorStage.Evaluate;
        _source = lines;
        _format = format;
        _offset = offset;
        _first = first;
        _second = second;
        _number = number;
        _small = small;
    }

    // The format of a message that is its first part.
    internal static Func<CelError, string> FirstPart { get; } = static error => (string)error._first!;

    // The parts of the message, for the format.
    internal object? First => _first;

    internal object? Second => _second;

    internal long Number => _number;

    internal byte Small => _small;

    /// <summary>
    /// The code of the error from the published list of codes.
    /// </summary>
    /// <remarks>
    /// The code tells what went wrong in a form a program can test. Removing a code or changing its
    /// meaning is a breaking change and happens only in a major version; new codes can appear in a
    /// minor version, so a <see langword="switch"/> over codes needs a default branch.
    /// </remarks>
    public CelErrorCode Code => (CelErrorCode)_code;

    /// <summary>
    /// The stage at which the error was found.
    /// </summary>
    /// <remarks>
    /// For codes 9000–9999, which occur at any stage, this is the only indication of the stage.
    /// For a one-step evaluation it tells whether compilation or evaluation failed.
    /// </remarks>
    public CelErrorStage Stage => (CelErrorStage)_stage;

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
    public string Message => _format is null ? ((Given)_source!).Message : _format(this);

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
    public CelSourceLocation Location => _source switch
    {
        LineTable lines => lines.GetLocation(_offset),
        Given given => given.Location,
        _ => default,
    };

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
    public CelSourceLocation? SourceLocation => (_source as Given)?.SourceLocation;

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
    public Exception? Exception => _source is Given given ? given.Exception : _first as Exception;

    /// <summary>
    /// Gives this error with its position in the document that holds the expression, from the position at which the
    /// expression starts there: the offset of the start plus the offset, the line of the start plus the line minus 1,
    /// and on the first line of the expression the column of the start plus the column minus 1, on the other lines the
    /// column itself.
    /// </summary>
    /// <param name="start">The position of the start of the expression in the document, with its source name.</param>
    /// <returns>A new error with the same code, stage, message, position and exception.</returns>
    internal CelError InDocument(CelSourceLocation start)
    {
        int column = Location.Line == 1 ? Add(start.Column, Location.Column - 1) : Location.Column;
        var place = new CelSourceLocation(
            start.SourceName!,
            Add(start.Offset, Location.Offset),
            Add(start.Line, Location.Line - 1),
            column);
        return new CelError(Code, Stage, Message, Location, place, Exception);
    }

    // The start is any position the application gives; a sum past int.MaxValue stays at int.MaxValue.
    private static int Add(int start, int value) => (int)Math.Min((long)start + value, int.MaxValue);

    // The message and the positions of an error that is created with them.
    private sealed class Given(string message, CelSourceLocation location, CelSourceLocation? sourceLocation, Exception? exception)
    {
        internal string Message { get; } = message;

        internal CelSourceLocation Location { get; } = location;

        internal CelSourceLocation? SourceLocation { get; } = sourceLocation;

        internal Exception? Exception { get; } = exception;
    }
}
