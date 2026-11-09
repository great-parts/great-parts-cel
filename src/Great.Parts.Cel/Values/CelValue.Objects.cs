using System;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Values;

public readonly partial struct CelValue
{
    /// <summary>
    /// Creates an object value that holds <paramref name="instance"/> together with its description, for
    /// a place where the description cannot come from a declaration: a place of type <c>dyn</c>, such as
    /// the result of a function implementation declared as <c>dyn</c>, an element of a collection, or an
    /// argument or a result of a function implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the top level of a variable or a field declared with an object type takes the description from
    /// the declaration, so the application can bind the instance itself there. Everywhere else the
    /// description of a bare instance is found by the exact .NET type of the instance, then by its base
    /// classes. A type described at run time has no .NET type, so its carrier needs this method in those
    /// places — in a list, in a <c>dyn</c> binding, as the result of a function — as does an instance whose
    /// exact type the environment describes differently. A read delegate of
    /// <see cref="CelObjectTypeBuilder{TCarrier}"/> that returns values created once with this method
    /// avoids a pair per read. In a place declared with an object type the value fits only when
    /// <paramref name="type"/> is the description that the environment has for that type.
    /// </para>
    /// <para>
    /// The value refers to the instance without copying it and allocates a small pair of the description
    /// and the instance. The rules for the type of an object in each place are on the page
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md#the-type-of-an-object">Type mapping</see>.
    /// </para>
    /// </remarks>
    /// <param name="type">The description of the object type.</param>
    /// <param name="instance">The instance: of the .NET type of <paramref name="type"/>, or of the carrier class of a type described at run time.</param>
    /// <returns>The object value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> or <paramref name="instance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="instance"/> does not belong to <paramref name="type"/>.</exception>
    public static CelValue FromObject(CelObjectType type, object instance)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(instance);
        if (!type.Belongs(instance))
        {
            throw new ArgumentException(
                "The instance of the .NET type '" + instance.GetType() + "' does not belong to the object type '" + type.Name + "'.",
                nameof(instance));
        }

        return FromPair(type, instance);
    }

    /// <summary>
    /// Reads an object value: the instance of an application type, the carrier of a type described at run
    /// time, or a protobuf message — the same instance the application passed, not a copy.
    /// </summary>
    /// <param name="instance">The instance when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Object"/>.</returns>
    public bool TryGetObject([NotNullWhen(true)] out object? instance)
    {
        if (_kind != CelValueKind.Object)
        {
            instance = null;
            return false;
        }

        instance = _ref is ObjectPair pair ? pair.Instance : _ref!;
        return true;
    }

    // The pair of a description and an instance that belongs to it.
    internal static CelValue FromPair(CelObjectType type, object instance) => new(CelValueKind.Object, new ObjectPair(type, instance), 0, 0);

    // An object value that holds the instance without a description: a bare instance, which allocates nothing.
    internal static CelValue FromBareObject(object instance) => new(CelValueKind.Object, instance, 0, 0);

    // The description of a pair; null for a bare instance and for a value of another kind.
    internal CelObjectType? PairedType => _kind == CelValueKind.Object ? (_ref as ObjectPair)?.Type : null;

    // The pair of a description and an instance that CelValue.FromObject creates: 32 bytes.
    private sealed class ObjectPair(CelObjectType type, object instance)
    {
        internal CelObjectType Type { get; } = type;

        internal object Instance { get; } = instance;
    }
}
