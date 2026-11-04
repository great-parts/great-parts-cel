namespace Great.Parts.Cel.Values;

/// <summary>
/// The result of comparing two values by CEL ordering.
/// </summary>
internal enum ValueOrdering
{
    /// <summary>The left value is less.</summary>
    Less,

    /// <summary>The values are equal.</summary>
    Equal,

    /// <summary>The left value is greater.</summary>
    Greater,

    /// <summary>A NaN takes part: every relation is <c>false</c>.</summary>
    Unordered,

    /// <summary>The kinds have no ordering between them; no overload of a relation takes them.</summary>
    None,
}
