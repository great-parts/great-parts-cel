using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

public abstract partial class CelList
{
    /// <summary>
    /// Creates a view over a list of the application: the elements are converted when they are read, and
    /// nothing is copied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view has the size of <paramref name="source"/>, and reading an element by index calls the
    /// indexer of <paramref name="source"/> and <paramref name="elementConverter"/>, so an expression that
    /// reads one element of a long list does not convert the others. Arrays, <see cref="List{T}"/>,
    /// immutable arrays and lists, and the repeated fields of protobuf messages all implement
    /// <see cref="IReadOnlyList{T}"/>.
    /// </para>
    /// <para>
    /// An element that cannot be converted is a value of the kind <see cref="CelValueKind.Error"/>: the
    /// converter returns such a value, or an exception of <paramref name="source"/> or of the converter
    /// becomes one with the code <see cref="Diagnostics.CelErrorCode.ApplicationException"/>. The view
    /// reflects <paramref name="source"/>; if the application changes it while the view is in use, the
    /// result is undefined.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type of the elements.</typeparam>
    /// <param name="source">The list of the application.</param>
    /// <param name="elementConverter">
    /// Converts an element to a CEL value by the input rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
    /// it can return a value of the kind <see cref="CelValueKind.Error"/>. It must allow calls from many threads at
    /// once.
    /// </param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="elementConverter"/> is <see langword="null"/>.</exception>
    public static CelList CreateView<T>(IReadOnlyList<T> source, Func<T, CelValue> elementConverter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(elementConverter);
        return new ApplicationList<T>(source, elementConverter, recordedType: null, rowsSource: null);
    }

    /// <summary>
    /// Creates a list from a collection of the application that has a size but no index, such as a
    /// <see cref="Queue{T}"/>, a <see cref="Stack{T}"/> or a <see cref="LinkedList{T}"/>: the elements are
    /// converted and copied now, in the order in which <paramref name="source"/> enumerates them.
    /// </summary>
    /// <remarks>
    /// An element that cannot be converted stays in the list as a value of the kind
    /// <see cref="CelValueKind.Error"/>, as in a view; an exception of <paramref name="source"/> or of the
    /// converter becomes such a value with the code
    /// <see cref="Diagnostics.CelErrorCode.ApplicationException"/>. Later changes of
    /// <paramref name="source"/> do not change the list.
    /// </remarks>
    /// <typeparam name="T">The .NET type of the elements.</typeparam>
    /// <param name="source">The collection of the application.</param>
    /// <param name="elementConverter">Converts an element to a CEL value by the input rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.</param>
    /// <returns>The list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="elementConverter"/> is <see langword="null"/>.</exception>
    public static CelList CreateCopy<T>(IReadOnlyCollection<T> source, Func<T, CelValue> elementConverter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(elementConverter);
        return ConversionRules.CopyList(source, elementConverter);
    }

    /// <summary>
    /// Creates a list from a set of the application, such as a <see cref="HashSet{T}"/>: the elements are
    /// converted and copied now, in ascending CEL order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A CEL list has an order and a set has none: the enumeration order of a hash set depends on hash
    /// codes, which can differ between processes, while the result of an expression must not. So the
    /// elements are sorted by the CEL <c>&lt;</c> ordering of their kind.
    /// </para>
    /// <para>
    /// The elements must be of one kind that CEL orders, and CEL must order every two of them: every
    /// element converts to a value of the same one of the kinds <c>int</c>, <c>uint</c>, <c>double</c>,
    /// <c>string</c> (ordered by Unicode code points), <c>bytes</c>, <c>bool</c>, <c>timestamp</c>,
    /// <c>duration</c> and, on .NET 11, <c>decimal</c>; no element is a <c>double</c> NaN; and no two
    /// elements are equal in CEL, such as <c>0.0</c> and <c>-0.0</c>. A <c>null</c>, an error value
    /// returned by the converter, elements of two kinds, a NaN or two equal elements break this condition,
    /// and the method throws <see cref="ArgumentException"/>: such elements have no CEL order, and their
    /// place in the list would depend on the enumeration order of <paramref name="source"/>.
    /// </para>
    /// <para>
    /// An exception of <paramref name="source"/> or of the converter is not caught: it reaches the caller
    /// unchanged, and during evaluation it becomes an error with the code
    /// <see cref="Diagnostics.CelErrorCode.ApplicationException"/>, as for the field reading members of
    /// <see cref="CelObjectType"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type of the elements.</typeparam>
    /// <param name="source">The set of the application.</param>
    /// <param name="elementConverter">Converts an element to a CEL value by the input rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.</param>
    /// <returns>The list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="elementConverter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The elements are not of one kind that CEL orders, or CEL does not order two of them: a <c>null</c>, an
    /// error value returned by the converter, elements of two kinds, a NaN, or two elements equal in CEL.
    /// </exception>
    public static CelList CreateSorted<T>(IReadOnlyCollection<T> source, Func<T, CelValue> elementConverter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(elementConverter);
        return ConversionRules.SortList(source, elementConverter);
    }

    /// <summary>
    /// The CEL type that the input rows recorded for a view they made from a typed .NET collection, so that the check
    /// against a declared type does not descend into a view that already fits it; <see langword="null"/> for any other
    /// list.
    /// </summary>
    internal virtual CelType? RecordedType => null;

    /// <summary>
    /// The value that the input rows converted to this view, which the place rule looks through; <see langword="null"/>
    /// for a view that the application created and for any other list.
    /// </summary>
    internal virtual object? RowsSource => null;

    /// <summary>
    /// The units that an index of this list charges beyond the constant unit: n for a view over a JSON array, which the document
    /// may walk to the element; 0 for any other list.
    /// </summary>
    internal virtual ulong IndexUnits => 0;

    /// <summary>
    /// The element at an index for an operation of an evaluation: a list over a JSON array charges the string of the element
    /// before it creates it.
    /// </summary>
    /// <param name="index">The index, already checked against the size.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <returns>The element.</returns>
    internal virtual CelValue Read(int index, IEvaluationMeter? meter) => this[index];
}
