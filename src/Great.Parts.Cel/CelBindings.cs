using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// The values of variables for an evaluation: bindings of names to values, independent of any environment.
/// </summary>
/// <remarks>
/// <para>
/// An application loads the data an expression needs and binds it here before the evaluation; evaluation
/// itself is synchronous and does no I/O. Each value becomes a <see cref="CelValue"/> when it is added,
/// without boxing; an object of the application is kept by reference and read lazily during evaluation,
/// not copied. The program finds its variables by name, so names the environment does not declare are
/// ignored, and one set of bindings can serve programs of different environments.
/// </para>
/// <para>
/// A variable without a value is an evaluation error <see cref="CelErrorCode.Unbound"/>, raised only when
/// the expression reads it. A value that does not have the declared type of its variable is the
/// evaluation error <see cref="CelErrorCode.BindingTypeMismatch"/> that names the variable.
/// </para>
/// <para>
/// Bindings are mutable data of a call, not part of an environment. Any number of evaluations can read
/// the same bindings at once, as long as nobody adds, sets or clears values meanwhile. The collection initializer
/// <c>new CelBindings { { "order", order } }</c> calls <see cref="Add{T}(string, T)"/>, which converts by the static type;
/// the indexer converts by the run-time type, and <see cref="Clear"/> empties the bindings for the next item of a batch.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "The name is the cel-spec term 'bindings' (docs/api-concepts.md); the type is enumerable for the collection initializer.")]
public sealed class CelBindings : IEnumerable<KeyValuePair<string, CelValue>>
{
    private readonly Dictionary<string, CelValue> _values = new(StringComparer.Ordinal);

    // Counts the changes (Add, the setter of the indexer, Clear), so that an enumerator tells a change during its run: the dictionary
    // does not, for a replaced value or a removal.
    private int _version;

    /// <summary>
    /// Creates empty bindings.
    /// </summary>
    public CelBindings()
    {
    }

    /// <summary>
    /// The bound values by name, as evaluation reads them.
    /// </summary>
    internal IReadOnlyDictionary<string, CelValue> Values => _values;

    // Binds a name to a value that is already converted, as the simple entry does for the values it has typed: no second check.
    internal void Set(string name, CelValue value)
    {
        _values[name] = value;
        _version++;
    }

    // The bindings of a call that passes none: no value is ever added to them, so one instance serves every such call.
    internal static readonly CelBindings Empty = new();

    /// <summary>
    /// Binds a name to a value, converted by the static type <typeparamref name="T"/> through the input rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The conversion follows the declared type, not the run-time type: a <c>List&lt;int&gt;</c> is a
    /// <c>list(int)</c> read through a view, an <c>IReadOnlyDictionary&lt;string, long&gt;</c> is a
    /// <c>map(string, int)</c> read through a view, a set is copied and sorted in the order of CEL, a
    /// <see cref="DateTimeOffset"/> is a <c>timestamp</c>, and a <see cref="CelValue"/> is taken as it is. A class of
    /// the application is a bare object that evaluation describes by the type sources of the environment (see
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md#objects-and-collections">Objects and collections</see>), and a
    /// <see cref="System.Text.Json.JsonElement"/> is <c>dyn</c> (see
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md#jsonelement">JsonElement</see>). Only a value of the static type <see cref="object"/>
    /// is converted by its run-time type, as a <c>dyn</c> value; a .NET collection in that place is an object that no
    /// source describes, and the node that reads it decides what it is.
    /// </para>
    /// <para>
    /// Values that a row of the type mapping rejects outright are rejected here, before any evaluation: a
    /// <see cref="DateTime"/> whose kind is not <see cref="DateTimeKind.Utc"/>, an undefined
    /// <see cref="System.Text.Json.JsonElement"/>, a <see cref="char"/> that is a lone surrogate, an
    /// <see cref="Int128"/> or a <see cref="System.Numerics.BigInteger"/> outside the range of <see cref="long"/>, a
    /// <see cref="UInt128"/> above <see cref="ulong.MaxValue"/>, and on .NET 11 a <c>Decimal128</c> that is a NaN or an infinity.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type of the value, which selects the conversion.</typeparam>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value.</param>
    /// <returns>These bindings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or already bound, <typeparamref name="T"/> has no row of the
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// a set has elements that CEL cannot sort, or <paramref name="value"/> is a value the type mapping rejects, as
    /// listed in the remarks.
    /// </exception>
    public CelBindings Add<T>(string name, T value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        CelValue converted;
        if (typeof(T) == typeof(CelValue))
        {
            converted = Unsafe.As<T, CelValue>(ref value);
        }
        else if (!ConversionRules.TryFromClr(value, out converted))
        {
            throw new ArgumentException(
                "A value of the .NET type " + typeof(T) + " has no row in the input table of the type mapping, or its elements have no CEL order.",
                nameof(value));
        }

        if (ConversionRules.IsNotFiniteDecimal<T>(converted))
        {
            throw new ArgumentException("A decimal must be finite: a NaN and an infinity are not values of CEL.", nameof(value));
        }

        if (ConversionRules.IsRefusedByBindings(converted))
        {
            throw new ArgumentException(ConversionRules.BindingsRefusalMessage(converted), nameof(value));
        }

        if (!_values.TryAdd(name, converted))
        {
            throw new ArgumentException($"The name '{name}' is already bound.", nameof(name));
        }

        _version++;
        return this;
    }

    /// <summary>
    /// Gets or sets the value bound to a name. Setting binds the name or replaces its value, converted by the run-time type
    /// of the value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The setter converts by the run-time type, unlike <see cref="Add{T}(string, T)"/>, which converts by the static type:
    /// a value of a .NET type of the input rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>
    /// becomes a value of its CEL type, lists and dictionaries with <see cref="string"/> keys are recognized by the same
    /// finite list of checks as in the simple entry <see cref="CelExpression"/>, a <see cref="CelValue"/> is taken as it is,
    /// and any other instance is an object of the application that evaluation describes by the type sources of the
    /// environment. Setting such a value always succeeds: a value that no type source describes, such as a .NET collection
    /// outside those lists and dictionaries, is an evaluation error <see cref="Diagnostics.CelErrorCode.TypeNotAccessible"/>
    /// when the expression reads it. <see langword="null"/> binds <c>null</c>. A value type is boxed: for evaluation without
    /// allocation use <see cref="Add{T}(string, T)"/> on new bindings or an input type of the environment.
    /// </para>
    /// <para>
    /// The getter returns the bound value as an evaluation result of the type <see cref="object"/> would return it, by the
    /// result rows of the type mapping.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the variable.</param>
    /// <returns>The bound value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// On get, <paramref name="name"/> is not bound. On set, <paramref name="name"/> is empty, or the value is one that
    /// <see cref="Add{T}(string, T)"/> rejects: a <see cref="DateTime"/> whose <see cref="DateTime.Kind"/> is not
    /// <see cref="DateTimeKind.Utc"/>, a lone surrogate <see cref="char"/>, an integer wider than 64 bits out of range, a
    /// <c>Decimal128</c> NaN or infinity, or a default <c>JsonElement</c>.
    /// </exception>
    public object? this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            if (!_values.TryGetValue(name, out CelValue bound))
            {
                throw new ArgumentException($"The name '{name}' is not bound.", nameof(name));
            }

            // The result rows with T = object read any value; a value that no row converts has no object to give.
            return ConversionRules.TryToResult(bound, CelType.Dyn, out object? result, out _, out _) ? result : null;
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            CelValue converted = ConversionRules.FromRuntimeValue(value);
#if NET11_0_OR_GREATER
            if (value is System.Numerics.Decimal128 && converted.Kind == CelValueKind.Error)
            {
                throw new ArgumentException("A decimal must be finite: a NaN and an infinity are not values of CEL.", nameof(value));
            }

#endif
            if (ConversionRules.IsRefusedByBindings(converted))
            {
                throw new ArgumentException(ConversionRules.BindingsRefusalMessage(converted), nameof(value));
            }

            _values[name] = converted;
            _version++;
        }
    }

    /// <summary>
    /// Removes all values, so that the same bindings serve the next item of a batch evaluation.
    /// </summary>
    /// <remarks>
    /// Do not clear bindings while an evaluation reads them: bindings are mutable data of a call.
    /// </remarks>
    public void Clear()
    {
        _values.Clear();
        _version++;
    }

    /// <summary>
    /// Returns an enumerator over the bound names and their values, as evaluation reads them; the order is not specified.
    /// </summary>
    /// <remarks>
    /// The enumerator follows the contract of <see cref="IEnumerator{T}"/>: changing the bindings during the enumeration
    /// makes the next move throw <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <returns>The enumerator.</returns>
    public IEnumerator<KeyValuePair<string, CelValue>> GetEnumerator()
    {
        int version = _version;
        foreach (KeyValuePair<string, CelValue> pair in _values)
        {
            yield return pair;
            if (version != _version)
            {
                throw new InvalidOperationException("The bindings were changed during the enumeration.");
            }
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
