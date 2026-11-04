using System;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Evaluation;

// The internal signal that charging the next operation would exceed the cost limit. It is not an error
// value, so no operator absorbs it; Plan.Run turns it into CostLimitExceeded at the recorded node.
[SuppressMessage(
    "Design",
    "CA1064:Exceptions should be public",
    Justification = "The signal never leaves the evaluation: Plan.Run catches it and returns the error CostLimitExceeded.")]
internal sealed class CostLimitException : Exception
{
    public CostLimitException()
        : base("The evaluation cost limit is exceeded.")
    {
    }

    public CostLimitException(string message)
        : base(message)
    {
    }

    public CostLimitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
