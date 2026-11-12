using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A CEL <c>optional_type(T)</c> value of the <c>optional</c> extension: either empty
/// (<c>optional.none()</c>) or holding a value (<c>optional.of(x)</c>), where the held value can itself
/// be <see langword="null"/>.
/// </summary>
/// <typeparam name="T">
/// The .NET type of the held value:
/// <see cref="CelValue"/> inside function implementations, or the type requested for the result of an expression of type <c>optional_type(T)</c> by the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
/// </typeparam>
/// <remarks>
/// <para>
/// Unlike a nullable value, an optional distinguishes "no value" from "the value <c>null</c>":
/// <c>optional.none()</c> and <c>optional.of(null)</c> are different values. <c>default(CelOptional&lt;T&gt;)</c>
/// is empty.
/// </para>
/// <para>The structure is immutable; whether it can be shared between threads depends on the held value.</para>
/// </remarks>
public readonly struct CelOptional<T> : IEquatable<CelOptional<T>>, IOptionalSource
{
    private readonly T _value;
    private readonly bool _hasValue;

    /// <summary>
    /// Creates an optional that holds <paramref name="value"/>, as <c>optional.of(value)</c> does.
    /// </summary>
    /// <param name="value">The held value; <see langword="null"/> is a value, not "empty".</param>
    public CelOptional(T value)
    {
        _value = value;
        _hasValue = true;
    }

    /// <summary>
    /// <see langword="true"/> when the optional holds a value, which can be <see langword="null"/>;
    /// <see langword="false"/> when it is empty.
    /// </summary>
    public bool HasValue => _hasValue;

    /// <summary>
    /// The held value.
    /// </summary>
    /// <exception cref="InvalidOperationException">The optional is empty; check <see cref="HasValue"/> first.</exception>
    public T Value => _hasValue ? _value : throw new InvalidOperationException("The optional is empty.");

    /// <summary>
    /// Compares two optionals: both are empty, or both hold values that are equal by
    /// <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <param name="other">The optional to compare with.</param>
    /// <returns><see langword="true"/> when the optionals are equal.</returns>
    public bool Equals(CelOptional<T> other)
    {
        return _hasValue == other._hasValue && (!_hasValue || EqualityComparer<T>.Default.Equals(_value, other._value));
    }

    /// <summary>
    /// Compares this optional with an object, as <see cref="Equals(CelOptional{T})"/> does when the object is a <see cref="CelOptional{T}"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal optional.</returns>
    public override bool Equals(object? obj)
    {
        return obj is CelOptional<T> other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelOptional{T})"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return _hasValue ? HashCode.Combine(1, _value) : 0;
    }

    /// <summary>
    /// Tells whether two optionals are equal, as <see cref="Equals(CelOptional{T})"/> does.
    /// </summary>
    /// <param name="left">The first optional.</param>
    /// <param name="right">The second optional.</param>
    /// <returns><see langword="true"/> when the optionals are equal.</returns>
    public static bool operator ==(CelOptional<T> left, CelOptional<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Tells whether two optionals differ, as the negation of <see cref="Equals(CelOptional{T})"/>.
    /// </summary>
    /// <param name="left">The first optional.</param>
    /// <param name="right">The second optional.</param>
    /// <returns><see langword="true"/> when the optionals differ.</returns>
    public static bool operator !=(CelOptional<T> left, CelOptional<T> right)
    {
        return !left.Equals(right);
    }

    // The CEL value of the optional by the input rows: the empty optional, or one that holds the converted value.
    bool IOptionalSource.TryToCelValue(out CelValue result)
    {
        if (!_hasValue)
        {
            result = CelValue.EmptyOptional;
            return true;
        }

        if (!ConversionRules.TryFromClr(_value, out CelValue held))
        {
            result = default;
            return false;
        }

        // An error value that the conversion gave is the value of the read, never the content of an optional.
        result = held.Kind == CelValueKind.Error ? held : CelValue.OptionalOfValue(held);
        return true;
    }
}
