using System;

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
public readonly struct CelOptional<T> : IEquatable<CelOptional<T>>
{
    /// <summary>
    /// Creates an optional that holds <paramref name="value"/>, as <c>optional.of(value)</c> does.
    /// </summary>
    /// <param name="value">The held value; <see langword="null"/> is a value, not "empty".</param>
    public CelOptional(T value)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// <see langword="true"/> when the optional holds a value, which can be <see langword="null"/>;
    /// <see langword="false"/> when it is empty.
    /// </summary>
    public bool HasValue => throw new NotImplementedException();

    /// <summary>
    /// The held value.
    /// </summary>
    /// <exception cref="InvalidOperationException">The optional is empty; check <see cref="HasValue"/> first.</exception>
    public T Value => throw new NotImplementedException();

    /// <summary>
    /// Compares two optionals: both are empty, or both hold values that are equal by
    /// <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <param name="other">The optional to compare with.</param>
    /// <returns><see langword="true"/> when the optionals are equal.</returns>
    public bool Equals(CelOptional<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Compares this optional with an object, as <see cref="Equals(CelOptional{T})"/> does when the object is a <see cref="CelOptional{T}"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal optional.</returns>
    public override bool Equals(object? obj)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelOptional{T})"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether two optionals are equal, as <see cref="Equals(CelOptional{T})"/> does.
    /// </summary>
    /// <param name="left">The first optional.</param>
    /// <param name="right">The second optional.</param>
    /// <returns><see langword="true"/> when the optionals are equal.</returns>
    public static bool operator ==(CelOptional<T> left, CelOptional<T> right)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether two optionals differ, as the negation of <see cref="Equals(CelOptional{T})"/>.
    /// </summary>
    /// <param name="left">The first optional.</param>
    /// <param name="right">The second optional.</param>
    /// <returns><see langword="true"/> when the optionals differ.</returns>
    public static bool operator !=(CelOptional<T> left, CelOptional<T> right)
    {
        throw new NotImplementedException();
    }
}
