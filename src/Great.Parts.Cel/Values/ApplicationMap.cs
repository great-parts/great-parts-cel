using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelMap"/> over a dictionary of the application: a lookup converts the CEL key back to the key of the
/// dictionary and makes one lookup in it, and a value is converted at each read. Its decisions are in
/// <see cref="ConversionRules"/>.
/// </summary>
/// <typeparam name="TKey">The .NET type of the keys.</typeparam>
/// <typeparam name="TValue">The .NET type of the values.</typeparam>
internal sealed class ApplicationMap<TKey, TValue> : CelMap
{
    private readonly IReadOnlyDictionary<TKey, TValue> _source;
    private readonly Func<TValue, CelValue> _converter;
    private readonly bool _inSourceOrder;
    private readonly CelType? _recordedType;
    private readonly object? _rowsSource;

    /// <summary>
    /// Creates the view.
    /// </summary>
    /// <param name="source">The dictionary of the application.</param>
    /// <param name="converter">Converts a value.</param>
    /// <param name="keepSourceOrder">Whether to enumerate in the order of <paramref name="source"/> whatever its type.</param>
    /// <param name="recordedType">The CEL type that the input rows gave the .NET type of the dictionary, or <see langword="null"/> for a view of the application.</param>
    /// <param name="rowsSource">The value that the input rows converted, or <see langword="null"/> for a view of the application.</param>
    internal ApplicationMap(
        IReadOnlyDictionary<TKey, TValue> source,
        Func<TValue, CelValue> converter,
        bool keepSourceOrder,
        CelType? recordedType,
        object? rowsSource)
    {
        _source = source;
        _converter = converter;
        _inSourceOrder = keepSourceOrder || ConversionRules.EnumeratesInOrder(source);
        _recordedType = recordedType;
        _rowsSource = rowsSource;
    }

    internal override CelType? RecordedType => _recordedType;

    internal override object? RowsSource => _rowsSource;

    private protected override int CountCore => _source.Count;

    internal override void ChargeEnumeration(IEvaluationMeter meter)
    {
        if (!_inSourceOrder)
        {
            ConversionRules.ChargeSortedKeys(_source, meter);
        }
    }

    private protected override bool TryGetValueCore(CelValue key, out CelValue value) =>
        ConversionRules.TryFind(_source, key, _converter, out value);

    private protected override IEnumerator<KeyValuePair<CelValue, CelValue>> GetEnumeratorCore() =>
        ConversionRules.Enumerate(_source, _converter, _inSourceOrder);
}
