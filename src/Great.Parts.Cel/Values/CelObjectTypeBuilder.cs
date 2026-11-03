using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// Describes an object type at run time over a dictionary carrier
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> of <see cref="string"/> and nullable
/// <see cref="object"/>: a field is the entry with the field name as its key.
/// </summary>
/// <remarks>
/// <para>
/// This is the form of
/// <see cref="CelObjectTypeBuilder{TCarrier}"/> with ready-made delegates, and the only form of a type described at run time that data can express without code: an environment description in JSON creates its object types this way. A field is present when the dictionary has its key. The value of the entry is converted by the input value rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
/// as for a member of the .NET type <see cref="object"/>, and checked against the declared type of the field: a value
/// of another type is an error <see cref="CelErrorCode.FieldTypeMismatch"/>, and reading a missing key is an error
/// <see cref="CelErrorCode.FieldNotSet"/>. A boxed <see cref="CelValue"/>, a <see cref="CelList"/> or a
/// <see cref="CelMap"/> in the dictionary is taken as it is, which is how a dictionary holds a list or a map.
/// </para>
/// <para>
/// The values are boxed in the dictionary and checked at each read; the generic builder over a typed
/// carrier avoids both. The builder is not thread-safe; the descriptions it builds are immutable and can
/// be shared between threads.
/// </para>
/// </remarks>
public sealed class CelObjectTypeBuilder
{
    internal CelObjectTypeBuilder(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Starts the description of a type over a dictionary carrier.
    /// </summary>
    /// <param name="name">The full CEL name of the type, identifiers separated by dots, such as <c>acme.crm.Contact</c>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a full CEL name.</exception>
    public static CelObjectTypeBuilder ForDictionary(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds a field read from the dictionary entry whose key is <paramref name="name"/>. Its number is the
    /// number of fields added before it.
    /// </summary>
    /// <param name="name">The field name and the key of the entry: an identifier.</param>
    /// <param name="type">The declared CEL type of the field.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is not an identifier or is the name of a field added before, or
    /// <paramref name="type"/> is a type parameter.
    /// </exception>
    public CelObjectTypeBuilder AddField(string name, CelType type)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the description from the fields added so far. The builder can be used further; later
    /// fields do not change descriptions built before.
    /// </summary>
    /// <returns>The description, with <see cref="CelObjectType.ClrType"/> <see langword="null"/>.</returns>
    public CelObjectType Build()
    {
        throw new NotImplementedException();
    }
}
