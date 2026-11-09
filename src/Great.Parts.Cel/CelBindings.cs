using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
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
/// the same bindings at once, as long as nobody adds to them meanwhile.
/// </para>
/// </remarks>
public sealed class CelBindings
{
    private readonly Dictionary<string, CelValue> _values = new(StringComparer.Ordinal);

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
    /// <see cref="System.Text.Json.JsonElement"/>, and on .NET 11 a <c>Decimal128</c> that is a NaN or an infinity.
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
            // Optional values and decimals come with their parts.
            throw ConversionRules.IsOfLaterPart<T>()
                ? new NotImplementedException()
                : new ArgumentException(
                    "A value of the .NET type " + typeof(T) + " has no row in the input table of the type mapping, or its elements have no CEL order.",
                    nameof(value));
        }

        if (ConversionRules.IsRefusedByBindings(converted))
        {
            throw new ArgumentException(ConversionRules.BindingsRefusalMessage(converted), nameof(value));
        }

        if (!_values.TryAdd(name, converted))
        {
            throw new ArgumentException($"The name '{name}' is already bound.", nameof(name));
        }

        return this;
    }
}
