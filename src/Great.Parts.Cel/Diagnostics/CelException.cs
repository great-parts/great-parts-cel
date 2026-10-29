using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// The exception thrown by the <c>Get…OrThrow</c> methods of the build, compile and evaluation results
/// when the result is not successful. It carries the errors of that result.
/// </summary>
/// <remarks>
/// <para>
/// Results report errors without exceptions; the <c>Get…OrThrow</c> methods exist for code in which an
/// error is a programming mistake, such as an environment built from declarations fixed in the source
/// code. Calling such a method on an unsuccessful result violates its documented condition, and this
/// exception reports the violation together with the errors. No other member of the library throws it.
/// </para>
/// <para>
/// <see cref="Exception.Message"/> lists the errors. Instances are created by the library only.
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
    internal CelException(ImmutableArray<CelError> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
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
