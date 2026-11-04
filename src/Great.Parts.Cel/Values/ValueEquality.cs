namespace Great.Parts.Cel.Values;

/// <summary>
/// The result of comparing two values by CEL equality.
/// </summary>
internal enum ValueEquality
{
    /// <summary>The values are not equal.</summary>
    NotEqual,

    /// <summary>The values are equal.</summary>
    Equal,

    /// <summary>An operand, or an element, key or value compared on the way, is an error value; it is the result.</summary>
    Error,

    /// <summary>The values are nested too deeply for the stack of the thread.</summary>
    InsufficientStack,
}
