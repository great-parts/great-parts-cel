using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The base class of a type context: a partial class on which the application lists, with
/// <see cref="CelObjectAttribute"/>, the .NET types that expressions can read, and which the source
/// generator completes at build time with descriptions of those types.
/// </summary>
/// <remarks>
/// <para>
/// A context is declared as a non-generic <see langword="partial"/> class that derives from this class
/// and is not nested in a generic type, with one <see cref="CelObjectAttribute"/> for each type the
/// application names; a type from another assembly is named the same way as its own types. The
/// generator follows the types of the members and describes them too, so nested types need no
/// attribute. It creates a private constructor and a static <c>Default</c> property that holds the
/// single instance; the environment adds the context through that instance, and its class name is the
/// source name in build errors. The generator reports a context it cannot complete as a compiler error.
/// </para>
/// <para>
/// The descriptions read members directly, without reflection, so the context works under trimming and
/// Native AOT. <see cref="CelTypeContextOptionsAttribute"/> on the context sets the naming policy of
/// fields. The context is immutable and can be shared between threads and environments.
/// </para>
/// </remarks>
public abstract class CelTypeContext : CelTypeProvider
{
    /// <summary>
    /// Initializes the context. Called by the constructor that the source generator creates in the
    /// partial class of the application; application code does not call it.
    /// </summary>
    /// <param name="sourceName">The name of the context class, which build errors show.</param>
    /// <param name="objectTypes">The descriptions of the listed .NET types and of the types their members reach.</param>
    /// <param name="enumTypes">The descriptions of the enumeration types the members reach.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> or an element of <paramref name="objectTypes"/> or <paramref name="enumTypes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty.</exception>
    protected CelTypeContext(string sourceName, ReadOnlySpan<CelObjectType> objectTypes, ReadOnlySpan<CelEnumType> enumTypes)
        : base(sourceName, objectTypes, enumTypes)
    {
    }
}
