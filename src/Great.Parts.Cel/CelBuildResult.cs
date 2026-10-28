using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

/// <summary>
/// The result of <see cref="CelEnvironmentBuilder.Build"/>: the environment, or the build errors.
/// </summary>
/// <remarks>
/// <para>
/// Build errors are returned, not thrown. Each has the stage <see cref="CelErrorStage.Build"/> and a code
/// from 4000–4999, or <see cref="CelErrorCode.InternalError"/> for a defect of the library found while
/// building. Not every build error is a conflict: an error about a conflict names both conflicting
/// sources, such as "application" and "extension strings v5", while others, such as
/// <see cref="CelErrorCode.UnknownExtensionVersion"/> or <see cref="CelErrorCode.InvalidVariableName"/>,
/// concern one declaration. An error found in an environment description read from JSON also has its
/// position in the JSON text. Neither the environment nor the text of an error depends on the order of
/// the builder calls.
/// </para>
/// <para>
/// Read <see cref="Environment"/> only after testing <see cref="IsSuccess"/>: the compiler warns about a
/// read without the test, and with warnings as errors the build fails. Code in which a build error is a
/// programming mistake, such as an environment declared once at startup, can call
/// <see cref="GetEnvironmentOrThrow"/>.
/// </para>
/// <para>The result is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelBuildResult
{
    private CelBuildResult()
    {
    }

    /// <summary>
    /// Whether the environment was built: <see langword="true"/> when there are no errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Environment))]
    public bool IsSuccess => throw new NotImplementedException();

    /// <summary>
    /// The built environment. Read it only when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="IsSuccess"/> is <see langword="false"/>.</exception>
    public CelEnvironment? Environment => throw new NotImplementedException();

    /// <summary>
    /// All build errors found; empty when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public ImmutableArray<CelError> Errors => throw new NotImplementedException();

    /// <summary>
    /// Returns the built environment, for code in which a build error is a programming mistake.
    /// </summary>
    /// <returns>The environment.</returns>
    /// <exception cref="CelException">
    /// <see cref="IsSuccess"/> is <see langword="false"/>; <see cref="CelException.Errors"/> holds the build errors.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public CelEnvironment GetEnvironmentOrThrow()
    {
        throw new NotImplementedException();
    }
}
