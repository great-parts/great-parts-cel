using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// The exception that carries all errors of an unsuccessful build, compilation or evaluation, for code in which such an
/// error is a programming mistake.
/// </summary>
/// <remarks>
/// <para>
/// Results report errors without exceptions. The library throws this exception only from the members that its
/// documentation names: the <c>Get…OrThrow</c> methods of the results; <see cref="CelBuildResult.Environment"/>,
/// <see cref="CelCompileResult{T}.Program"/> and <see cref="CelEvalResult{T}.Value"/> of an unsuccessful result; the
/// simple entry <see cref="CelExpression"/>; and <see cref="CelEnvironment.Create(Action{CelEnvironmentBuilder})"/> with
/// its overload that takes a <see cref="CelStandardLibrary"/>. Other
/// failures of a call are <see cref="ArgumentException"/> and its derived types for arguments,
/// <see cref="InvalidOperationException"/> for the state of an object, and <see cref="OperationCanceledException"/> for
/// cancellation.
/// </para>
/// <para>
/// <see cref="Exception.Message"/> lists the errors. <see cref="Exception.InnerException"/> is the
/// <see cref="CelError.Exception"/> of the first error in <see cref="Errors"/> that has one — for example the exception
/// of a function of the application — so logs that follow the chain of inner exceptions show it; it is
/// <see langword="null"/> when no error has an exception. Every original exception stays in <see cref="Errors"/>.
/// Instances are created by the library only.
/// </para>
/// <para>
/// Like <see cref="CelError.Message"/>, the text of <see cref="Exception.Message"/> is for people and is
/// not part of the compatibility contract. The contract of each error is the number, name, meaning and
/// stage of its <see cref="CelError.Code"/>. The text can be refined in a minor version; such changes
/// are listed in the release notes. Applications should not parse the text: read <see cref="Errors"/>.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "The exception exists only to carry the errors of an unsuccessful result; a constructor without errors would create it without its data.")]
public sealed class CelException : Exception
{
    private CelException(ImmutableArray<CelError> errors, Exception? inner)
        : base(FormatMessage(errors), inner)
    {
        Errors = errors;
    }

    /// <summary>
    /// Creates the exception of an unsuccessful result: the one way the library makes it, so that every member that throws it
    /// gives the same exception for the same errors.
    /// </summary>
    /// <param name="errors">The errors of the result, not empty.</param>
    /// <returns>The exception; its inner exception is that of the first error that has one, or <see langword="null"/>.</returns>
    internal static CelException FromErrors(ImmutableArray<CelError> errors)
    {
        Exception? inner = null;
        foreach (CelError error in errors)
        {
            if (error.Exception is { } exception)
            {
                inner = exception;
                break;
            }
        }

        return new CelException(errors, inner);
    }

    /// <summary>
    /// The errors of the unsuccessful result, in the order of the result: all compilation errors, or
    /// the one evaluation error, or all build errors. The array is never empty.
    /// </summary>
    public ImmutableArray<CelError> Errors { get; }

    private static string FormatMessage(ImmutableArray<CelError> errors)
    {
        StringBuilder text = new("The result is not successful:");
        foreach (CelError error in errors)
        {
            CelSourceLocation location = error.Location;
            text.Append('\n').Append(error.Code.ToString());
            if (location.Line > 0)
            {
                text.Append(CultureInfo.InvariantCulture, $" at line {location.Line}, column {location.Column}");
            }

            text.Append(": ").Append(error.Message);
        }

        return text.ToString();
    }
}
