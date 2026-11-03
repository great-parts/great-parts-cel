using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Values;

public abstract partial class CelMap
{
    /// <summary>
    /// Creates a view over a dictionary of the application: values are converted when they are read, and
    /// nothing is copied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keys become CEL keys by the input rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>:
    /// <see cref="string"/> and <see cref="Guid"/> (in the format <c>D</c>, lower case) become <c>string</c>; signed
    /// integer types and enumerations become <c>int</c>; unsigned integer types become <c>uint</c>; <see cref="bool"/>
    /// stays <c>bool</c>. A lookup converts the CEL key back, so it costs one lookup in <paramref name="source"/>, and
    /// keys meet on one number line: the key <c>1</c> of a dictionary over <see cref="int"/> is found by <c>1</c>,
    /// <c>1u</c> and <c>1.0</c>. A key of an enumeration over <see cref="ulong"/> above <see cref="long.MaxValue"/>
    /// cannot be a CEL key; an operation that meets it gives an error value with the code
    /// <see cref="Diagnostics.CelErrorCode.ConversionRange"/>.
    /// </para>
    /// <para>
    /// The order of enumeration never depends on hash codes, which can differ between processes. When
    /// <paramref name="keepSourceOrder"/> is <see langword="true"/>, entries are enumerated in the order of
    /// <paramref name="source"/>, which the caller guarantees not to depend on hash codes, as for the map
    /// fields of protobuf messages, which keep the order of insertion. Otherwise they are enumerated in the
    /// order of <paramref name="source"/> when it is a <see cref="Dictionary{TKey, TValue}"/>,
    /// <see cref="OrderedDictionary{TKey, TValue}"/>, <see cref="SortedDictionary{TKey, TValue}"/>,
    /// <see cref="SortedList{TKey, TValue}"/> or <c>ImmutableSortedDictionary</c>, and in ascending order of
    /// the CEL keys for any other dictionary.
    /// </para>
    /// <para>
    /// A value that cannot be converted is a value of the kind <see cref="CelValueKind.Error"/>: the
    /// converter returns such a value, or an exception of <paramref name="source"/> or of the converter
    /// becomes one with the code <see cref="Diagnostics.CelErrorCode.ApplicationException"/>. The view
    /// reflects <paramref name="source"/>; if the application changes it while the view is in use, the
    /// result is undefined.
    /// </para>
    /// </remarks>
    /// <typeparam name="TKey">
    /// The .NET type of the keys: <see cref="string"/>, <see cref="Guid"/>, <see cref="bool"/>, an integer
    /// type or an enumeration.
    /// </typeparam>
    /// <typeparam name="TValue">The .NET type of the values.</typeparam>
    /// <param name="source">
    /// The dictionary of the application. <see cref="Dictionary{TKey, TValue}"/> and the other dictionaries
    /// of .NET, and the map fields of protobuf messages, implement <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </param>
    /// <param name="valueConverter">
    /// Converts a value to a CEL value by the input rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
    /// it can return a value of the kind <see cref="CelValueKind.Error"/>. It must allow calls from many threads at
    /// once.
    /// </param>
    /// <param name="keepSourceOrder">
    /// <see langword="true"/> to enumerate entries in the order of <paramref name="source"/> whatever its
    /// type; <see langword="false"/> to let the type of <paramref name="source"/> decide, as described above.
    /// </param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueConverter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="TKey"/> is not one of the key types listed above.</exception>
    public static CelMap CreateView<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source,
        Func<TValue, CelValue> valueConverter,
        bool keepSourceOrder)
    {
        throw new NotImplementedException();
    }
}
