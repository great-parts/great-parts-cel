using System;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Values;

public readonly partial struct CelValue
{
    /// <summary>
    /// Creates an object value that holds <paramref name="instance"/> together with its description, for
    /// a place of type <c>dyn</c> that the declarations of the environment do not describe, such as the
    /// result of a function implementation declared as <c>dyn</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Where a value enters an expression through a declared object type — a variable, a field, an element
    /// of a typed collection — the description comes from the declaration, and the application passes
    /// the instance itself. In a <c>dyn</c> place the description is otherwise found by the exact .NET type
    /// of the instance, then by its base classes; a type described at run time has no .NET type, so its
    /// carrier needs this method there, as does an instance whose exact type the environment describes
    /// differently. In a place declared with an object type the value must belong to that type.
    /// </para>
    /// <para>
    /// The value refers to the instance without copying it and allocates a small pair of the description
    /// and the instance.
    /// </para>
    /// </remarks>
    /// <param name="type">The description of the object type.</param>
    /// <param name="instance">The instance: of the .NET type of <paramref name="type"/>, or of the carrier class of a type described at run time.</param>
    /// <returns>The object value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="instance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="instance"/> does not belong to <paramref name="type"/>.</exception>
    public static CelValue FromObject(CelObjectType type, object instance)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads an object value: the instance of an application type, the carrier of a type described at run
    /// time, or a protobuf message — the same instance the application passed, not a copy.
    /// </summary>
    /// <param name="instance">The instance when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Object"/>.</returns>
    public bool TryGetObject([NotNullWhen(true)] out object? instance)
    {
        throw new NotImplementedException();
    }
}
