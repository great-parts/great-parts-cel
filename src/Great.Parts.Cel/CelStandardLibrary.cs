using System;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

/// <summary>
/// A version of the CEL standard library: the type identifiers, macros, functions and overloads of the
/// CEL language definition that every environment has. An environment names its version explicitly in
/// the constructor of <see cref="CelEnvironmentBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// The declarations of a version never change, so updating the library does not change how an
/// environment is built or which overload a call selects. Functions that a later revision of the
/// language definition adds come as a new version in a minor release; behavior fixes for conformance
/// with the language definition apply to every version.
/// </para>
/// <para>
/// An application can add overloads to a standard function when they do not overlap the standard
/// overloads. An overload that is the same as or overlaps a standard one, a variable or type with the name
/// of a standard identifier, and a function with the name and the form of a standard macro are a build
/// error <see cref="CelErrorCode.StandardLibraryConflict"/>: without it, the application's declaration
/// would silently replace or shadow the standard one.
/// </para>
/// <para>The list of declarations of each version is in the standard library table of the documentation.</para>
/// </remarks>
public sealed class CelStandardLibrary
{
    private CelStandardLibrary()
    {
    }

    /// <summary>
    /// Version 1: the standard definitions of the CEL language definition, including the functions on
    /// <c>timestamp</c> and <c>duration</c>.
    /// </summary>
    public static CelStandardLibrary V1 { get; } = new();

    /// <summary>
    /// The version number, which build errors show, for example "standard library v1".
    /// </summary>
    public uint Version => throw new NotImplementedException();
}
