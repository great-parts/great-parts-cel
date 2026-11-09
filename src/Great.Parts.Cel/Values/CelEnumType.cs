using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The description of an enumeration type: its full CEL name and the names and values of its constants.
/// Expressions read enumeration values as <c>int</c> and refer to a constant by its full name.
/// </summary>
/// <remarks>
/// <para>
/// CEL reads enumerations as protobuf does: a value of an enumeration is an <c>int</c>, and the constant
/// <c>Approved</c> of the type <c>acme.sales.Status</c> is the identifier <c>acme.sales.Status.Approved</c>,
/// whose value is the number of the constant. .NET enumerations of a type context and protobuf
/// enumerations are described the same way. The naming policy of a type context does not change the
/// names of constants.
/// </para>
/// <para>
/// Instances are immutable and can be shared between threads.
/// </para>
/// </remarks>
public sealed class CelEnumType
{
    /// <summary>
    /// Creates the description of an enumeration type.
    /// </summary>
    /// <param name="name">The full CEL name of the type, identifiers separated by dots, such as <c>acme.sales.Status</c>.</param>
    /// <param name="constants">
    /// The constants: name and value. Two constants can share a value, as aliases in protobuf and in .NET
    /// enumerations do.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or the name of a constant is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is not a full CEL name, the name of a constant is not an identifier, or two
    /// constants have the same name.
    /// </exception>
    public CelEnumType(string name, ReadOnlySpan<KeyValuePair<string, long>> constants)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!DeclarationNames.IsQualifiedIdentifier(name))
        {
            throw new ArgumentException("The type name '" + name + "' is not a full CEL name.", nameof(name));
        }

        var names = new HashSet<string>(constants.Length, StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> constant in constants)
        {
            ArgumentNullException.ThrowIfNull(constant.Key, nameof(constants));
            if (!DeclarationNames.IsWordName(constant.Key) || !DeclarationNames.IsQualifiedIdentifier(name + "." + constant.Key))
            {
                throw new ArgumentException("The constant name '" + constant.Key + "' of '" + name + "' is not an identifier.", nameof(constants));
            }

            if (!names.Add(constant.Key))
            {
                throw new ArgumentException("The constant '" + constant.Key + "' of '" + name + "' is given twice.", nameof(constants));
            }
        }

        Name = name;
        Constants = [.. constants];
    }

    /// <summary>
    /// The full CEL name of the type, such as <c>acme.sales.Status</c>: the prefix of the full names of its
    /// constants.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The constants in the order of the constructor: the name relative to the type and the <c>int</c>
    /// value an expression gets for it.
    /// </summary>
    public ImmutableArray<KeyValuePair<string, long>> Constants { get; }
}
