using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A CEL <c>list</c>: an ordered sequence of values with a known size.
/// </summary>
/// <remarks>
/// <para>
/// A list is either an array of values, as created by a list literal or by <see cref="Create"/>, or a
/// view over a collection of the application that reads elements on access without copying them.
/// Only the library creates lists, so the class cannot be derived from outside it.
/// </para>
/// <para>
/// Reading an element of a view over application data can fail, for example when a getter of the
/// application throws or a value is out of the range of its CEL type. Such an element is a value of the
/// kind <see cref="CelValueKind.Error"/>, not an exception.
/// </para>
/// <para>
/// A list is immutable and can be read from any number of threads. A view over application data
/// reflects that data; if the application changes it while the list is in use, the result is undefined.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "The name is the CEL type name 'list'.")]
public abstract partial class CelList : IReadOnlyList<CelValue>
{
    private protected CelList()
    {
    }

    /// <summary>
    /// The number of elements. Known without reading the elements.
    /// </summary>
    public int Count => CountCore;

    /// <summary>
    /// Gets the element at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>
    /// The element; a value of the kind <see cref="CelValueKind.Error"/> when the element of a view over
    /// application data cannot be read.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or not less than <see cref="Count"/>.</exception>
    public CelValue this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, CountCore);
            return GetCore(index);
        }
    }

    /// <summary>
    /// The number of elements, for <see cref="Count"/>.
    /// </summary>
    private protected abstract int CountCore { get; }

    /// <summary>
    /// Creates a list of <paramref name="items"/> in their order. The elements are copied, so a later
    /// change of the source does not change the list.
    /// </summary>
    /// <param name="items">The elements.</param>
    /// <returns>The list.</returns>
    /// <exception cref="ArgumentException">An element is a value of the kind <see cref="CelValueKind.Error"/>; an error is not an element of a CEL list.</exception>
    public static CelList Create(params ReadOnlySpan<CelValue> items)
    {
        foreach (CelValue item in items)
        {
            if (item.Kind == CelValueKind.Error)
            {
                throw new ArgumentException("An error value is not an element of a CEL list.", nameof(items));
            }
        }

        return new ValueArrayList(items.ToArray());
    }

    /// <summary>
    /// Returns an enumerator over the elements in order. Elements are read as with the indexer.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<CelValue> GetEnumerator()
    {
        int count = CountCore;
        for (int index = 0; index < count; index++)
        {
            yield return GetCore(index);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Reads the element at <paramref name="index"/>, which is already checked against <see cref="CountCore"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The element.</returns>
    private protected abstract CelValue GetCore(int index);
}
