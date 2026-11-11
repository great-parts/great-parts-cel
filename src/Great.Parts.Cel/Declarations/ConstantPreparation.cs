namespace Great.Parts.Cel.Declarations;

// The outcome of the preparation of a constant argument: the prepared object, immutable and safe to share between evaluations and
// threads, or the complete text of the error. Exactly one of them is not null.
internal readonly struct ConstantPreparation
{
    private ConstantPreparation(object? prepared, string? error)
    {
        Prepared = prepared;
        Error = error;
    }

    // The prepared object; null on an error.
    internal object? Prepared { get; }

    // The complete text of the error; null on success.
    internal string? Error { get; }

    internal static ConstantPreparation Success(object prepared) => new(prepared, null);

    internal static ConstantPreparation Failure(string error) => new(null, error);
}
