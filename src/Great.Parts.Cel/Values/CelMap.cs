using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A CEL <c>map</c>: entries of keys and values, with keys of the kinds <c>int</c>, <c>uint</c>,
/// <c>bool</c> and <c>string</c>.
/// </summary>
/// <remarks>
/// <para>
/// A map is either a table of entries, as created by a map literal or by <see cref="Create"/>, or a view
/// over a dictionary of the application or a JSON object that reads values on access without copying
/// them. Only the library creates maps, so the class cannot be derived from outside it.
/// </para>
/// <para>
/// Keys compare by CEL equality: <c>int</c>, <c>uint</c> and <c>double</c> keys meet on one number line,
/// so a lookup with <c>1u</c> or <c>1.0</c> finds the key <c>1</c>. The order of enumeration never
/// depends on hash codes: a table keeps the order in which its entries were added; the order of a view
/// over a dictionary is given in the type mapping table of the documentation.
/// </para>
/// <para>
/// Reading a value of a view can fail, for example when a getter of the application throws. Such a value
/// is a value of the kind <see cref="CelValueKind.Error"/>, not an exception. A view over a JSON object
/// enumerates and counts every property, including repeated names; a lookup of a repeated name gives an
/// error value with the code <see cref="Diagnostics.CelErrorCode.DuplicateMapKey"/>.
/// </para>
/// <para>
/// Keys of a CEL map are unique, and a JSON object can repeat a name, so evaluation does not take
/// <see cref="Count"/> or the enumeration of such a view as the CEL view of the object. The operations
/// <c>size</c>, iteration in a macro, the <c>in</c> operator and equality check all names for repeats
/// first and evaluate to the error <see cref="Diagnostics.CelErrorCode.DuplicateMapKey"/> when any name
/// repeats; selecting, indexing or testing with <c>has()</c> one key fails only when that key repeats.
/// </para>
/// <para>
/// A map is immutable and can be read from any number of threads. A view over application data reflects
/// that data; if the application changes it while the map is in use, the result is undefined.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "The name is the CEL type name 'map'.")]
public abstract partial class CelMap : IReadOnlyCollection<KeyValuePair<CelValue, CelValue>>
{
    private protected CelMap()
    {
    }

    /// <summary>
    /// The number of entries. Known without reading the values.
    /// </summary>
    /// <remarks>
    /// For a view over a JSON object that repeats a name, this counts every property, repeats included;
    /// <c>size</c> in an expression gives an error for such an object instead.
    /// </remarks>
    public int Count => throw new NotImplementedException();

    /// <summary>
    /// Creates a map of <paramref name="entries"/>; enumeration keeps their order.
    /// The entries are copied, so a later change of the source does not change the map.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A key is not of the kind <see cref="CelValueKind.Int"/>, <see cref="CelValueKind.UInt"/>,
    /// <see cref="CelValueKind.Bool"/> or <see cref="CelValueKind.String"/>; two keys are equal by CEL
    /// equality, such as <c>1</c> and <c>1u</c>; or a value is of the kind <see cref="CelValueKind.Error"/>.
    /// </exception>
    public static CelMap Create(IEnumerable<KeyValuePair<CelValue, CelValue>> entries)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Looks up the value of <paramref name="key"/> by CEL key equality.
    /// </summary>
    /// <param name="key">The key; a value of a kind that cannot be a key is simply not found.</param>
    /// <param name="value">
    /// The value when the method returns <see langword="true"/>, possibly a value of the kind
    /// <see cref="CelValueKind.Error"/> when the value of a view cannot be read; otherwise <c>null</c>.
    /// </param>
    /// <returns><see langword="true"/> when the map has the key.</returns>
    public bool TryGetValue(CelValue key, out CelValue value)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns an enumerator over the entries in the order described for the map. Values are read as
    /// with <see cref="TryGetValue"/>.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<KeyValuePair<CelValue, CelValue>> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
