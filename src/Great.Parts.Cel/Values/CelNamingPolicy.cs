namespace Great.Parts.Cel.Values;

/// <summary>
/// The rule that turns the name of a member of a .NET type into the CEL field name, when no attribute
/// sets the name; set it with <see cref="CelTypeContextOptionsAttribute.NamingPolicy"/>.
/// </summary>
/// <remarks>
/// The rules give the same results as the naming policies of System.Text.Json with the same names, so the
/// field names of an object match its property names in JSON written with that policy. The policy does
/// not change names of CEL types or of enumeration constants. The numeric values are part of the public
/// contract and do not change.
/// </remarks>
public enum CelNamingPolicy
{
    /// <summary>
    /// Camel case, as <c>JsonNamingPolicy.CamelCase</c>: <c>TotalAmount</c> becomes <c>totalAmount</c>.
    /// The default.
    /// </summary>
    CamelCase = 0,

    /// <summary>
    /// The name of the member as written in C#: <c>TotalAmount</c> stays <c>TotalAmount</c>.
    /// </summary>
    AsIs = 1,

    /// <summary>
    /// Lower snake case, as <c>JsonNamingPolicy.SnakeCaseLower</c>: <c>TotalAmount</c> becomes
    /// <c>total_amount</c>.
    /// </summary>
    SnakeCaseLower = 2,
}
