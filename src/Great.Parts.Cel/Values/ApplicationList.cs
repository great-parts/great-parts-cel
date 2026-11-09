using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelList"/> over a list of the application: an element is read from the source and converted at each
/// access, and nothing is copied. Its decisions are in <see cref="ConversionRules"/>.
/// </summary>
/// <typeparam name="T">The .NET type of the elements.</typeparam>
internal sealed class ApplicationList<T> : CelList
{
    private readonly IReadOnlyList<T> _source;
    private readonly Func<T, CelValue> _converter;
    private readonly CelType? _recordedType;
    private readonly object? _rowsSource;

    /// <summary>
    /// Creates the view.
    /// </summary>
    /// <param name="source">The list of the application.</param>
    /// <param name="converter">Converts an element.</param>
    /// <param name="recordedType">The CEL type that the input rows gave the .NET type of the list, or <see langword="null"/> for a view of the application.</param>
    /// <param name="rowsSource">The value that the input rows converted, or <see langword="null"/> for a view of the application.</param>
    internal ApplicationList(IReadOnlyList<T> source, Func<T, CelValue> converter, CelType? recordedType, object? rowsSource)
    {
        _source = source;
        _converter = converter;
        _recordedType = recordedType;
        _rowsSource = rowsSource;
    }

    internal override CelType? RecordedType => _recordedType;

    internal override object? RowsSource => _rowsSource;

    private protected override int CountCore => _source.Count;

    private protected override CelValue GetCore(int index) => ConversionRules.ReadElement(_source, index, _converter);
}
