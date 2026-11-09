using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;

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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(valueConverter);
        if (!ConversionRules.IsMapKeyType<TKey>())
        {
            throw new ArgumentException("The key type " + typeof(TKey) + " is not a type of a CEL map key.", nameof(source));
        }

        return new ApplicationMap<TKey, TValue>(source, valueConverter, keepSourceOrder, recordedType: null, rowsSource: null);
    }

    /// <summary>
    /// The CEL type that the input rows recorded for a view they made from a typed .NET dictionary; <see langword="null"/>
    /// for any other map.
    /// </summary>
    internal virtual CelType? RecordedType => null;

    /// <summary>
    /// The value that the input rows converted to this view, which the place rule looks through; <see langword="null"/>
    /// for a view that the application created and for any other map.
    /// </summary>
    internal virtual object? RowsSource => null;

    /// <summary>
    /// Checks the map for an error that any operation which looks at all its keys — <c>size</c>, enumeration, `in` and
    /// equality — gives before its result: a repeated name of a JSON object.
    /// </summary>
    /// <param name="meter">The meter of an evaluation with a cost limit, which the check charges property by property before each is processed; <see langword="null"/> charges nothing.</param>
    /// <param name="error">The error value, without a place, when the map has one.</param>
    /// <param name="leadingUnit">
    /// Whether the check charges the leading 1 of the row of its cost, <c>1 + Σ</c>: an operation that has no unit of its own for it,
    /// such as the range of a comprehension and the two sides of an equality; <c>size</c> and <c>in</c> have it in the unit of the call.
    /// </param>
    /// <returns><see langword="true"/> when the map has the error.</returns>
    internal virtual bool TryGetKeyError(IEvaluationMeter? meter, out CelValue error, bool leadingUnit = false)
    {
        error = default;
        return false;
    }

    /// <summary>
    /// The units of a lookup of a string key beyond the constant unit of the operation: ⌈L/10⌉ for L the length of the key,
    /// and for a JSON object, which the lookup walks whole, n · (1 + ⌈L/10⌉) for n properties.
    /// </summary>
    /// <param name="keyTenths">⌈L/10⌉ of the key.</param>
    /// <returns>The units.</returns>
    internal virtual ulong ProbeUnits(ulong keyTenths) => keyTenths;

    /// <summary>
    /// Charges the start of an enumeration that the operation makes, before its first entry: the sort of a view that is
    /// ordered by ascending key; nothing for any other map.
    /// </summary>
    /// <param name="meter">The meter of an evaluation with a cost limit.</param>
    internal virtual void ChargeEnumeration(IEvaluationMeter meter)
    {
    }

    /// <summary>
    /// The lookup of a key by an operation of an evaluation: a map over a JSON object charges the string of the value it
    /// finds before it creates it.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="readValue"><see langword="false"/> for an operation that only asks whether the key is present, such as <c>has</c> and <c>in</c>: a map over a JSON object then creates no value.</param>
    /// <param name="value">The value of the key.</param>
    /// <returns><see langword="true"/> when the map has the key.</returns>
    internal virtual bool TryRead(CelValue key, IEvaluationMeter? meter, bool readValue, out CelValue value) => TryGetValue(key, out value);

    /// <summary>
    /// The enumerator of the entries for an operation of an evaluation, as <see cref="GetEnumerator"/> gives it, with the
    /// string of a value of a JSON object charged before it is created.
    /// </summary>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="readValues"><see langword="false"/> for an operation that reads only the keys: a map over a JSON object then creates no value.</param>
    /// <returns>The enumerator.</returns>
    internal virtual IEnumerator<KeyValuePair<CelValue, CelValue>> EnumerateEntries(IEvaluationMeter? meter, bool readValues) => GetEnumerator();
}
