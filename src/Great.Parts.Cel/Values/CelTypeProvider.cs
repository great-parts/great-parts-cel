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
/// A provider is immutable and can be shared between threads and environments. The rules of types described
/// at run time and the build errors of the sources are on the page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/runtime-types.md">Types described at run time</see>.
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
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        foreach (CelObjectType objectType in objectTypes)
        {
            ArgumentNullException.ThrowIfNull(objectType, nameof(objectTypes));
        }

        foreach (CelEnumType enumType in enumTypes)
        {
            ArgumentNullException.ThrowIfNull(enumType, nameof(enumTypes));
        }

        SourceName = sourceName;
        ObjectTypes = [.. objectTypes];
        EnumTypes = [.. enumTypes];
    }

    /// <summary>
    /// The name by which build errors refer to this source, for example the name of a type context class
    /// or the name given to <see cref="Create"/>.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// The descriptions of object types this source contributes to an environment.
    /// </summary>
    public ImmutableArray<CelObjectType> ObjectTypes { get; }

    /// <summary>
    /// The descriptions of enumeration types this source contributes to an environment; their constants
    /// become identifiers of the environment.
    /// </summary>
    public ImmutableArray<CelEnumType> EnumTypes { get; }

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
    public static CelTypeProvider Create(string sourceName, params ReadOnlySpan<CelObjectType> objectTypes) =>
        new RuntimeTypeProvider(sourceName, objectTypes);

    // A source of types described at run time, without enumerations.
    private sealed class RuntimeTypeProvider(string sourceName, ReadOnlySpan<CelObjectType> objectTypes)
        : CelTypeProvider(sourceName, objectTypes, []);
}
