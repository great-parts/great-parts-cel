using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Checking;

// The result of the internal entry point CelEnvironment.Check: the checked expression, or every check
// error ordered by offset, errors at one offset in the order in which the checker found them.
internal sealed class CheckResult
{
    private static readonly Dictionary<int, Reference> NoReferences = [];

    private readonly IReadOnlyDictionary<int, Reference>? _resolved;

    internal CheckResult(CheckedExpression expression)
    {
        Expression = expression;
        Errors = [];
    }

    // The errors, and the references that the check resolved before it ended: the lists of the validation of an expression
    // that has errors.
    internal CheckResult(ImmutableArray<CelError> errors, IReadOnlyDictionary<int, Reference>? resolved = null)
    {
        Errors = errors;
        _resolved = resolved;
    }

    [MemberNotNullWhen(true, nameof(Expression))]
    internal bool IsSuccess => Expression is not null;

    internal CheckedExpression? Expression { get; }

    // The errors of the stage Check; empty on success.
    internal ImmutableArray<CelError> Errors { get; }

    // The references by node id: all of a checked expression, and those resolved before the first error of a failed check; the
    // reference of a node that failed to resolve is not among them.
    internal IReadOnlyDictionary<int, Reference> References => Expression?.References ?? _resolved ?? NoReferences;
}
