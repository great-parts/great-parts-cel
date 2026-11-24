using System;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// An error of any stage — building an environment, parsing, type checking or evaluation — with its
/// code, stage, English message, position and, if there is one, the exception that caused it.
/// </summary>
/// <remarks>
/// <para>
/// The library returns errors as part of the result of a call — the build, compile, validation and evaluation
/// results — and throws them, as a <see cref="CelException"/>, only from the members whose documentation says so, such as
/// the simple entry <see cref="CelExpression"/>. Other exceptions are thrown only when the conditions of a call that
/// its documentation describes are violated, and <see cref="OperationCanceledException"/> when the
/// evaluation is canceled. An exception thrown by application code during evaluation does not escape
/// either: it becomes an error with the code <see cref="CelErrorCode.ApplicationException"/>, and the
/// original exception is in <see cref="Exception"/>.
/// </para>
/// <para>
/// Besides the code and the message, an error has the fragment of the expression it refers to, <see cref="Span"/>, and,
/// as separate values, the name it is about, <see cref="Subject"/>, and the expected and the actual CEL types,
/// <see cref="ExpectedType"/> and <see cref="ActualType"/>, as the page of error codes tells for each code.
/// Compilation returns every error it finds, each with its own position. Evaluation returns one error:
/// when several parts of an expression fail, the language definition does not specify which error
/// is reported.
/// </para>
/// <para>
/// Instances are created by the library, and by extension packages through <see cref="Create"/>. They are immutable and can be
/// shared between threads.
/// </para>
/// </remarks>
public sealed class CelError
{
    // Where the message and the position come from: the line table of the expression for an error of evaluation, the
    // given message and positions, or null for an error of evaluation that has no place yet.
    private readonly object? _source;

    // Forms the message and the parts of an error of evaluation from its parts when they are read; null when they were given.
    private readonly ErrorTemplate? _format;

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
        Exception? exception,
        int length = 0,
        string? subject = null,
        CelType? expectedType = null,
        CelType? actualType = null)
    {
        _code = (ushort)code;
        _stage = (byte)stage;
        _source = new Given(message, location, sourceLocation, exception, length, subject, expectedType, actualType);
    }

    // An error of evaluation whose message and position are formed only when they are read, so that an error value
    // that a later operator absorbs costs no text. Without a line table the error has no position.
    internal CelError(
        CelErrorCode code,
        ErrorTemplate format,
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

    /// <summary>
    /// Creates an error for a document that is not an expression — for example a policy document or a filter — so that its
    /// errors come in the same list and the same <see cref="CelException"/> as the errors of its expressions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The error has the given code, stage and message and no exception of application code. Its
    /// <see cref="Location"/>, <see cref="Span"/> start and <see cref="SourceLocation"/> are all
    /// <paramref name="location"/>: a position in the document, which names it; without a location the error has no position.
    /// The parts <see cref="Subject"/>, <see cref="ExpectedType"/> and <see cref="ActualType"/> are those given.
    /// </para>
    /// <para>
    /// The codes the library itself reports, and the parts each of them has, are listed in the documentation of
    /// <see cref="CelErrorCode"/>; an error created here with one of those codes is an error of the creator, not of the library.
    /// </para>
    /// </remarks>
    /// <param name="code">The code of the error.</param>
    /// <param name="stage">The stage the error belongs to.</param>
    /// <param name="message">The message for people, in English; not empty.</param>
    /// <param name="location">The position in the document, or <see langword="null"/> for none.</param>
    /// <param name="length">The length of the fragment in UTF-16 code units, 0 or more; 0 without a location.</param>
    /// <param name="subject">The name the error is about, or <see langword="null"/>.</param>
    /// <param name="expectedType">The expected CEL type, or <see langword="null"/>.</param>
    /// <param name="actualType">The actual CEL type, or <see langword="null"/>.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> is empty, <paramref name="code"/> or <paramref name="stage"/> is not a defined member, or
    /// <paramref name="location"/> is given without a source name.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, or positive without <paramref name="location"/>.
    /// </exception>
    public static CelError Create(
        CelErrorCode code,
        CelErrorStage stage,
        string message,
        CelSourceLocation? location = null,
        int length = 0,
        string? subject = null,
        CelType? expectedType = null,
        CelType? actualType = null)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentException("The code is not a member of CelErrorCode.", nameof(code));
        }

        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentException("The stage is not a member of CelErrorStage.", nameof(stage));
        }

        ArgumentException.ThrowIfNullOrEmpty(message);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (location is not { } place)
        {
            if (length != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "A fragment has a length only with the position it starts at.");
            }

            return new CelError(code, stage, message, default, null, null, 0, subject, expectedType, actualType);
        }

        if (place.SourceName is null)
        {
            throw new ArgumentException("The position of an error of a document must name the document.", nameof(location));
        }

        return new CelError(code, stage, message, place, place, null, length, subject, expectedType, actualType);
    }

    // The format of a message that is its first part; the error has no parts.
    internal static ErrorTemplate FirstPart { get; } = new(static error => (string)error._first!);

    // The format of a message that is its first part, with the name the error is about as its second part.
    internal static ErrorTemplate NamedMessage { get; } = new(static error => (string)error._first!, subject: static error => (string?)error._second);

    // The format of a message that is its first part, with the CEL type of the value that it is about as its second part.
    internal static ErrorTemplate MessageOfType { get; } = new(static error => (string)error._first!, actualType: static error => (CelType?)error._second);

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
    public string Message => _format is null ? ((Given)_source!).Message : _format.Text(this);

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
    public Exception? Exception => _source is Given given ? given.Exception : _first as Exception ?? (_first as CelError)?.Exception;

    /// <summary>
    /// Gives this error with its position in the document that holds the expression, which the map of the expression to its document
    /// computed from the position of the error in the expression.
    /// </summary>
    /// <param name="place">The position in the document, with its source name.</param>
    /// <returns>A new error with the same code, stage, message, position and exception.</returns>
    internal CelError AtDocument(CelSourceLocation place) =>
        new(Code, Stage, Message, Location, place, Exception, Span.Length, Subject, ExpectedType, ActualType);

    // The message and the positions of an error that is created with them.
    private sealed class Given(
        string message,
        CelSourceLocation location,
        CelSourceLocation? sourceLocation,
        Exception? exception,
        int length,
        string? subject,
        CelType? expectedType,
        CelType? actualType)
    {
        internal string Message { get; } = message;

        internal CelSourceLocation Location { get; } = location;

        internal CelSourceLocation? SourceLocation { get; } = sourceLocation;

        internal Exception? Exception { get; } = exception;

        // The length of the fragment that starts at the location, in UTF-16 code units.
        internal int Length { get; } = length;

        internal string? Subject { get; } = subject;

        internal CelType? ExpectedType { get; } = expectedType;

        internal CelType? ActualType { get; } = actualType;
    }

    /// <summary>
    /// The fragment of the expression that the error refers to: <see cref="Location"/> and the length of the fragment in
    /// UTF-16 code units.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Span.Start</c> equals <see cref="Location"/>. The fragment runs from there to the end of the node of the syntax
    /// tree that gave the error: the identifier for an undeclared reference, the call from <c>(</c> to <c>)</c> for a call
    /// without a matching overload, the operator and the operand after it for an operator, the whole expression for a result
    /// that does not convert to the requested .NET type. A parse error at the end of the text has a fragment of length 0.
    /// </para>
    /// <para>
    /// An error of the stage <see cref="CelErrorStage.Build"/> has no fragment: the value is <c>default(CelSourceSpan)</c>,
    /// whose length is 0. With the start of the expression in a source document, the fragment stays in the expression; the
    /// position in the document is in <see cref="SourceLocation"/>.
    /// </para>
    /// </remarks>
    public CelSourceSpan Span => _source switch
    {
        LineTable lines => new CelSourceSpan(lines.GetLocation(_offset), lines.GetEnd(_offset) - _offset),
        Given given when given.Location.Line != 0 => new CelSourceSpan(given.Location, given.Length),
        _ => default,
    };

    /// <summary>
    /// The name the error is about, as a separate value: a variable, a field, a function, a type, an overload identifier or a
    /// time zone; <see langword="null"/> when the code has no such part.
    /// </summary>
    /// <remarks>
    /// For each code the page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/error-codes.md">Error codes</see>
    /// tells whether the error has this part. An application takes the name from here instead of parsing
    /// <see cref="Message"/>: <c>totl</c> for <see cref="CelErrorCode.UndeclaredReference"/> in <c>totl &gt; 100</c>.
    /// </remarks>
    public string? Subject => _source is Given given ? given.Subject : _format?.Subject?.Invoke(this);

    /// <summary>
    /// The CEL type that was expected, as a separate value, such as <c>bool</c> for an expression compiled for a
    /// <see cref="bool"/> result; <see langword="null"/> when the code has no such part.
    /// </summary>
    /// <remarks>
    /// The page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/error-codes.md">Error codes</see>
    /// tells for each code whether the error has this part.
    /// </remarks>
    public CelType? ExpectedType => _source is Given given ? given.ExpectedType : _format?.ExpectedType?.Invoke(this);

    /// <summary>
    /// The CEL type that was found, as a separate value, such as <c>string</c> for a string where a <c>bool</c> was
    /// expected; <see langword="null"/> when the code has no such part.
    /// </summary>
    /// <remarks>
    /// The page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/error-codes.md">Error codes</see>
    /// tells for each code whether the error has this part.
    /// </remarks>
    public CelType? ActualType => _source is Given given ? given.ActualType : _format?.ActualType?.Invoke(this);

    /// <summary>
    /// Returns a one-line text of the error for logs: the code, the line and column, and the message, such as
    /// <c>UndeclaredReference at 1:7: undeclared reference to 'totl'</c>; without a position, the code and the message.
    /// </summary>
    /// <remarks>
    /// The text is for people, in English, and does not depend on the culture of the process. It is not part of the
    /// compatibility contract and can be refined in a minor version; applications should not parse it.
    /// </remarks>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        CelSourceLocation place = SourceLocation ?? Location;
        string code = Code.ToString();
        return place.Line == 0
            ? string.Concat(code, ": ", Message)
            : string.Concat(code, " at ", place.ToString(), ": ", Message);
    }

    /// <summary>
    /// Returns a text of the error to show to the author of the expression: the message, the line of the expression where
    /// the fragment starts, and a mark <c>^~~~</c> under the fragment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mark starts under the first code unit of the fragment and ends at its last one, or at the end of the line when
    /// the fragment continues on the next line; a fragment of length 0 is marked with one <c>^</c>. An error without a
    /// fragment, such as a build error, gives the message only.
    /// </para>
    /// <para>
    /// The text is for people, in English, and is not part of the compatibility contract.
    /// </para>
    /// </remarks>
    /// <param name="expression">The text of the expression that was compiled or evaluated, the one the error belongs to.</param>
    /// <returns>The text, with lines separated by <c>"\n"</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The fragment of the error does not fit in <paramref name="expression"/>.</exception>
    public string ToDisplayString(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelSourceSpan span = Span;
        if (span.Start.Line == 0)
        {
            return Message;
        }

        int start = span.Start.Offset;
        if ((long)start + span.Length > expression.Length)
        {
            throw new ArgumentException("The fragment of the error does not fit in the text of the expression.", nameof(expression));
        }

        int lineStart = start;
        while (lineStart > 0 && expression[lineStart - 1] is not ('\n' or '\r'))
        {
            lineStart--;
        }

        int lineEnd = start;
        while (lineEnd < expression.Length && expression[lineEnd] is not ('\n' or '\r'))
        {
            lineEnd++;
        }

        // The mark starts under the first code unit of the fragment and stops at its end or at the end of the line.
        int marked = Math.Max(1, Math.Min(span.Length, lineEnd - start));
        return string.Concat(
            Message,
            "\n",
            expression.Substring(lineStart, lineEnd - lineStart),
            "\n",
            new string(' ', start - lineStart),
            "^",
            new string('~', marked - 1));
    }
}
