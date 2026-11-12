using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// Hides a property or field of a .NET type from expressions, the counterpart of the cel-go struct tag
/// <c>cel:"-"</c>.
/// </summary>
/// <remarks>
/// A hidden member is not a field of the CEL type: selecting it is the same type checking error as
/// selecting a name the type does not have, and the source generator gives no warning for it. For a type
/// of another assembly, which cannot carry this attribute, use
/// <see cref="CelObjectAttribute.IgnoreMembers"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CelIgnoreAttribute : Attribute
{
    /// <summary>
    /// Marks the member as hidden from expressions.
    /// </summary>
    public CelIgnoreAttribute()
    {
    }
}
