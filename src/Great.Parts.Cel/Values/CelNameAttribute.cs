using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// Sets the CEL field name of a property or field of a .NET type, the counterpart of the cel-go struct
/// tag <c>cel:"name"</c>.
/// </summary>
/// <remarks>
/// The name takes precedence over <c>[JsonPropertyName]</c> and over the naming policy of the type
/// context. A name that is not an identifier, or a name that another member of the type also gets, is a
/// compiler error of the source generator.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CelNameAttribute : Attribute
{
    /// <summary>
    /// Sets the field name of the member to <paramref name="name"/>.
    /// </summary>
    /// <param name="name">
    /// The field name: an identifier. A reserved word of CEL is allowed; an expression writes it in
    /// backquotes, such as <c>order.`in`</c>.
    /// </param>
    public CelNameAttribute(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The field name expressions use for the member.
    /// </summary>
    public string Name => throw new NotImplementedException();
}
