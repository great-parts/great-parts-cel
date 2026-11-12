using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// Options of a type context that decide the CEL field names the source generator gives to the members
/// of the listed .NET types.
/// </summary>
/// <remarks>
/// <para>
/// The attribute goes on a partial class derived from <see cref="CelTypeContext"/>; without it the
/// defaults apply. The name of a member is decided in this order, and the first rule that applies wins:
/// <see cref="CelNameAttribute"/>; the System.Text.Json attribute <c>[JsonPropertyName]</c>, unless
/// <see cref="IgnoreJsonAttributes"/> is set; <see cref="NamingPolicy"/>. A member marked with
/// <see cref="CelIgnoreAttribute"/>, or with <c>[JsonIgnore]</c> whose condition is
/// <c>JsonIgnoreCondition.Always</c>, its default, is hidden; <c>[JsonIgnore]</c> with another condition
/// does not hide the member. So an expression over a .NET object and over the same
/// object serialized to JSON is written the same way, and a member hidden from JSON is hidden from
/// expressions too.
/// </para>
/// <para>
/// Two members whose rules give one field name are a compiler error. Names of CEL types and of
/// enumeration constants do not depend on these options.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CelTypeContextOptionsAttribute : Attribute
{
    /// <summary>
    /// Creates the options with the defaults: <see cref="CelNamingPolicy.CamelCase"/>, and the
    /// System.Text.Json attributes taken into account.
    /// </summary>
    public CelTypeContextOptionsAttribute()
    {
        NamingPolicy = CelNamingPolicy.CamelCase;
    }

    /// <summary>
    /// The rule that turns the names of members into field names when no attribute sets the name; the
    /// default is <see cref="CelNamingPolicy.CamelCase"/>, so the member <c>TotalAmount</c> is the field
    /// <c>totalAmount</c>.
    /// </summary>
    public CelNamingPolicy NamingPolicy { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the generator does not take <c>[JsonPropertyName]</c> and
    /// <c>[JsonIgnore]</c> of System.Text.Json into account; field names then come only from
    /// <see cref="CelNameAttribute"/> and <see cref="NamingPolicy"/>. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Changing the option changes field names, and so breaks expressions that use the old names. The
    /// generator recognizes the attributes by their full type names, so the context needs no reference to
    /// a JSON package.
    /// </remarks>
    public bool IgnoreJsonAttributes { get; set; }
}
