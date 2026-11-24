using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Policy;

/// <summary>
/// The result of reading a policy from a YAML document: the policy, or all errors found.
/// </summary>
/// <remarks>
/// <para>
/// Errors are returned, not thrown. Each has the stage <see cref="CelErrorStage.Parse"/>, the code
/// <see cref="CelErrorCode.InvalidYaml"/> or <see cref="CelErrorCode.InvalidPolicy"/>, and its position in the document in
/// <see cref="CelError.SourceLocation"/>.
/// </para>
/// <para>The result is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelPolicyParseResult
{
    internal CelPolicyParseResult(CelPolicy policy)
    {
        Policy = policy;
        Errors = [];
    }

    internal CelPolicyParseResult(ImmutableArray<CelError> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// <see langword="true"/> when the document is a policy without errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Policy))]
    public bool IsSuccess => Policy is not null;

    /// <summary>
    /// The policy, or <see langword="null"/> when the result is not successful.
    /// </summary>
    public CelPolicy? Policy { get; }

    /// <summary>
    /// The errors, ordered by position in the document; empty when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public ImmutableArray<CelError> Errors { get; }

    /// <summary>
    /// Returns the policy, or throws all errors.
    /// </summary>
    /// <returns>The policy.</returns>
    /// <exception cref="CelException">The result is not successful; the exception carries all errors.</exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public CelPolicy GetPolicyOrThrow() => Policy ?? throw new CelException(Errors);
}
