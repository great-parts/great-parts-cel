using System;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Values;

/// <summary>
/// Lists a .NET type, which can come from any assembly the context can see, as readable by expressions
/// of environments that add the type context this attribute is on.
/// </summary>
/// <remarks>
/// <para>
/// The attribute goes on a partial class derived from <see cref="CelTypeContext"/>, once for each type the
/// application names. The source generator describes the type and every type its members reach, so a
/// type that is only the type of a member needs no attribute of its own.
/// </para>
/// <para>
/// The generator reads classes, records, structures and interfaces through their public instance
/// properties and fields. A type of a kind it cannot read — such as a <see langword="ref"/> structure, an
/// open generic type, a delegate or a stream — is a compiler error on the attribute; a member of such a
/// kind is left out with a compiler warning, and selecting it in an expression is a type checking error
/// that gives the reason. The supported kinds are listed in the type mapping table of the documentation.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CelObjectAttribute : Attribute
{
    /// <summary>
    /// Lists <paramref name="type"/> as readable by expressions.
    /// </summary>
    /// <param name="type">A closed .NET type that the assembly of the context can see.</param>
    public CelObjectAttribute(Type type)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The listed .NET type.
    /// </summary>
    public Type Type => throw new NotImplementedException();

    /// <summary>
    /// The full CEL name of the type, identifiers separated by dots; <see langword="null"/> for the default.
    /// </summary>
    /// <remarks>
    /// The default is the full .NET name of the type with the <c>+</c> of a nested type replaced by a dot,
    /// such as <c>Acme.Sales.Order</c>. A closed generic type gets a name such as
    /// <c>Acme.Page&lt;Acme.Order&gt;</c> by default: its values can be read, but an expression cannot write
    /// that name, so set a name when an expression needs one. Names of fields and enumeration constants are
    /// not affected.
    /// </remarks>
    public string? Name
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    /// <summary>
    /// The names of members of the type, as written in C#, that expressions must not see; for a type of
    /// another assembly, which cannot carry <see cref="CelIgnoreAttribute"/>.
    /// </summary>
    /// <remarks>
    /// A hidden member is not a field: selecting it is the same error as selecting a name the type does
    /// not have, and the generator gives no warning for it.
    /// </remarks>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "A named argument of an attribute can only be an array.")]
    public string[]? IgnoreMembers
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}
