using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A named source of object and enumeration types that an application adds to an environment: a type
/// context described by the source generator, a set of types described at run time, or the protobuf
/// message types of the protobuf package.
/// </summary>
/// <remarks>
/// <para>
/// An environment takes any number of sources, each added the same way; the order of adding does not
/// matter. When the environment is built, all sources are checked together: two different descriptions
/// with one CEL name, or one .NET type described by two sources, are a build error
/// <see cref="CelErrorCode.DuplicateType"/> whose message names both sources by
/// <see cref="SourceName"/>. Identical descriptions of one .NET type from two generated type contexts —
/// a type that both contexts reached through the types of their members — are one declaration, not an
/// error. A variable declared with a .NET type that no source describes is a build error
/// <see cref="CelErrorCode.TypeNotDeclared"/>.
/// </para>
/// <para>
/// A provider is immutable and can be shared between threads and environments.
/// </para>
/// </remarks>
public abstract class CelTypeProvider
{
    /// <summary>
    /// Initializes a provider with its name and its types.
    /// </summary>
    /// <param name="sourceName">The name of the source, which build errors show, such as the name of a type context class.</param>
    /// <param name="objectTypes">The descriptions of object types.</param>
    /// <param name="enumTypes">The descriptions of enumeration types.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> or an element of <paramref name="objectTypes"/> or <paramref name="enumTypes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty.</exception>
    protected CelTypeProvider(string sourceName, ReadOnlySpan<CelObjectType> objectTypes, ReadOnlySpan<CelEnumType> enumTypes)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The name by which build errors refer to this source, for example the name of a type context class
    /// or the name given to <see cref="Create"/>.
    /// </summary>
    public string SourceName => throw new NotImplementedException();

    /// <summary>
    /// The descriptions of object types this source contributes to an environment.
    /// </summary>
    public ImmutableArray<CelObjectType> ObjectTypes => throw new NotImplementedException();

    /// <summary>
    /// The descriptions of enumeration types this source contributes to an environment; their constants
    /// become identifiers of the environment.
    /// </summary>
    public ImmutableArray<CelEnumType> EnumTypes => throw new NotImplementedException();

    /// <summary>
    /// Creates a source of types described at run time, for example with
    /// <see cref="CelObjectTypeBuilder{TCarrier}"/> from the metadata of the application.
    /// </summary>
    /// <remarks>
    /// The descriptions are checked when an environment is built, together with all other sources: two
    /// descriptions with one CEL name are a build error <see cref="CelErrorCode.DuplicateType"/>, not an
    /// exception of this method.
    /// </remarks>
    /// <param name="sourceName">The name of the source, which build errors show.</param>
    /// <param name="objectTypes">The descriptions of object types.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> or an element of <paramref name="objectTypes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty.</exception>
    public static CelTypeProvider Create(string sourceName, params ReadOnlySpan<CelObjectType> objectTypes)
    {
        throw new NotImplementedException();
    }
}
