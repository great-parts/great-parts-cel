using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Checking;

// The result of the internal entry point CelEnvironment.Check: the checked expression, or every check
// error ordered by offset, errors at one offset in the order in which the checker found them.
internal sealed class CheckResult
{
    internal CheckResult(CheckedExpression expression)
    {
        Expression = expression;
        Errors = [];
    }

    internal CheckResult(ImmutableArray<CelError> errors)
    {
        Errors = errors;
    }

    [MemberNotNullWhen(true, nameof(Expression))]
    internal bool IsSuccess => Expression is not null;

    internal CheckedExpression? Expression { get; }

    // The errors of the stage Check; empty on success.
    internal ImmutableArray<CelError> Errors { get; }
}
