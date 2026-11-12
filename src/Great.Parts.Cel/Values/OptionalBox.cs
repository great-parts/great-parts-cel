namespace Great.Parts.Cel.Values;

// The content of a non-empty value of the kind Optional: the held value, which is any value but an error value. The empty
// optional has no box. A box is immutable, so any number of threads share it, as they share the value it holds.
internal sealed class OptionalBox(CelValue value)
{
    internal CelValue Value { get; } = value;
}
