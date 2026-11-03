using System;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A field of an object type: the name an expression selects, the CEL type of its values, and the number
/// by which evaluation reads it.
/// </summary>
/// <remarks>
/// <para>
/// The number is the position of the field in <see cref="CelObjectType.Fields"/>. The program binds a
/// field selection to the number when it is planned, so evaluation reads the field through
/// <c>CelObjectType.GetField</c> without looking up the name.
/// </para>
/// <para>
/// Type checking uses the type of the field: a member of the .NET type
/// <see cref="string"/> annotated as not nullable is <c>string</c>, a nullable one is the wrapper type of <c>string</c>, and a member of the .NET type <see cref="object"/> is <c>dyn</c>, as the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>
/// gives. Instances are immutable and can be shared between threads.
/// </para>
/// </remarks>
public sealed class CelObjectField
{
    /// <summary>
    /// Creates a field.
    /// </summary>
    /// <param name="number">The number of the field: its position among the fields of its type, from 0.</param>
    /// <param name="name">
    /// The name an expression selects: an identifier, such as <c>total</c>, or, for an extension field of
    /// a protobuf message, the full name of the extension. A reserved word of CEL is allowed; an
    /// expression writes it in backquotes, such as <c>order.`in`</c>.
    /// </param>
    /// <param name="type">The CEL type of the values of the field.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is neither an identifier nor a dotted name of identifiers, or
    /// <paramref name="type"/> is a type parameter, which no value has.
    /// </exception>
    public CelObjectField(int number, string name, CelType type)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The number of the field, its position in <see cref="CelObjectType.Fields"/>; the argument that
    /// <c>CelObjectType.GetField</c> and <c>CelObjectType.HasField</c> receive.
    /// </summary>
    public int Number => throw new NotImplementedException();

    /// <summary>
    /// The name an expression selects, such as <c>total</c> in <c>order.total</c>. For a .NET member it is
    /// the name after <see cref="CelNameAttribute"/>, the System.Text.Json attributes and the naming policy
    /// of the type context, so it can differ from the name of the member in C#.
    /// </summary>
    public string Name => throw new NotImplementedException();

    /// <summary>
    /// The CEL type of the values of the field, which type checking uses for <c>e.f</c>, such as
    /// <c>double</c>, <c>list(acme.sales.Line)</c> or <c>dyn</c>.
    /// </summary>
    public CelType Type => throw new NotImplementedException();
}
