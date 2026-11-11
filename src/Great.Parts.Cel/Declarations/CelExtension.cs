using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Declarations;

/// <summary>
/// One version of one extension set: a named group of function declarations with their implementations
/// that an environment adds as a whole. The built-in sets, such as <c>strings</c> and <c>math</c>, come
/// from the extension catalog of the library; an application can build its own set with
/// <see cref="Create"/>.
/// </summary>
/// <remarks>
/// <para>
/// A built-in set can also bring macros, syntax and abstract types, such as the <c>.?</c> syntax and the
/// <c>optional_type</c> of the <c>optional</c> set. When cel-go, the CEL implementation in Go, has the
/// same set, the version number is its number in cel-go, so an environment description means the same
/// to both. The
/// functions of a version never change: a new function comes as a new version, and an environment keeps
/// the functions of the version it names when the library is updated. The version is always explicit.
/// </para>
/// <para>
/// When an environment is built, all its sets are checked together with the other declarations, so the
/// order in which sets are added does not matter. One set added in two versions is a build error
/// <see cref="CelErrorCode.ExtensionVersionConflict"/>. The same set — a set of the catalog in the same
/// version, or the same instance — added twice is one set; two different sets with one name and version,
/// such as a set of the catalog and a set built with <see cref="Create"/>, are a build error
/// <see cref="CelErrorCode.DuplicateExtension"/> that names both sources. A version
/// the set does not have is a build error <see cref="CelErrorCode.UnknownExtensionVersion"/>. A set whose
/// dependency is not added to the environment is a build error
/// <see cref="CelErrorCode.MissingExtensionDependency"/>: dependencies are never added implicitly. Overloads
/// of different sets that are the same or overlap are a build error
/// <see cref="CelErrorCode.OverlappingOverloads"/> that names both sets.
/// </para>
/// <para>
/// An extension set is immutable and can be shared between threads and environments. The rules that hold for every set, the
/// catalog with the versions and the checks of the build are on the page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/extensions.md">Extension sets</see>.
/// </para>
/// </remarks>
public sealed class CelExtension
{
    // The definition is null for a set of the catalog that has no such version; the available versions are those that the
    // catalog has under the name, for the error UnknownExtensionVersion.
    internal CelExtension(string name, uint version, ExtensionDefinition? definition, ImmutableArray<uint> availableVersions)
    {
        Name = name;
        Version = version;
        Definition = definition;
        AvailableVersions = availableVersions;
    }

    /// <summary>
    /// The name of the set, such as <c>strings</c>, which build errors show together with the version.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The version of the set. Version numbers are unsigned, as in cel-go.
    /// </summary>
    public uint Version { get; }

    /// <summary>
    /// Builds an extension set of the application, for example a group of functions shared by several
    /// environments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The functions of the set are declarations of the application: their names follow the rules of
    /// <see cref="CelFunctionDecl"/>, and each overload needs an implementation registered here under its
    /// identifier. An overload without an implementation is a build error
    /// <see cref="CelErrorCode.MissingImplementation"/>; an implementation whose identifier no declared
    /// overload has is a build error <see cref="CelErrorCode.UnusedImplementation"/>. The name of a set of
    /// the catalog, such as <c>strings</c>, is allowed, but a set built here is a different set: in an
    /// environment that also adds a set with the same name and version — a set of the catalog or another
    /// built set — it is a build error <see cref="CelErrorCode.DuplicateExtension"/>; adding the same instance
    /// twice is one set. These errors and the conflicts are reported when an environment with the set is
    /// built, not by this method.
    /// </para>
    /// <para>
    /// The implementations run synchronously inside evaluation. Do not block in them on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of
    /// ASP.NET Core and hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the
    /// bindings; see <see cref="CelFunctionImplementation"/>.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the set.</param>
    /// <param name="version">The version of the set. Keep the functions of a published version unchanged and add a new version for new functions.</param>
    /// <param name="functions">The function declarations of the set.</param>
    /// <param name="implementations">The implementations of the overloads of <paramref name="functions"/>, by overload identifier.</param>
    /// <param name="dependencies">The names of the extension sets this set requires; an environment with this set must add them too.</param>
    /// <returns>The extension set.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="implementations"/> is <see langword="null"/>, or an element of
    /// <paramref name="functions"/>, a value of <paramref name="implementations"/> or an element of
    /// <paramref name="dependencies"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or an element of <paramref name="dependencies"/> is empty.</exception>
    public static CelExtension Create(
        string name,
        uint version,
        ReadOnlySpan<CelFunctionDecl> functions,
        IReadOnlyDictionary<string, CelFunctionImplementation> implementations,
        params ReadOnlySpan<string> dependencies)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementations);
        foreach (CelFunctionDecl function in functions)
        {
            ArgumentNullException.ThrowIfNull(function, nameof(functions));
        }

        foreach (KeyValuePair<string, CelFunctionImplementation> implementation in implementations)
        {
            ArgumentNullException.ThrowIfNull(implementation.Value, nameof(implementations));
        }

        foreach (string dependency in dependencies)
        {
            ArgumentException.ThrowIfNullOrEmpty(dependency, nameof(dependencies));
        }

        return new CelExtension(name, version, ApplicationExtension.Create(name, version, functions, implementations, dependencies), []);
    }

    // What the set is made of; null for a set of the catalog whose version the catalog lacks, which Build reports.
    internal ExtensionDefinition? Definition { get; }

    // The versions of the name that the catalog has, ascending; empty for a set of the application.
    internal ImmutableArray<uint> AvailableVersions { get; }

    // Whether the set was built with Create, not taken from the catalog.
    internal bool IsApplication => Definition is ApplicationExtension;

    // Two sets are the same set when they hold the same definition, or when both come from the catalog and have the same
    // name and version, a missing definition included. A set of the application is the same only as itself.
    internal bool IsSameSetAs(CelExtension other)
    {
        if (ReferenceEquals(this, other) || (Definition is not null && ReferenceEquals(Definition, other.Definition)))
        {
            return true;
        }

        return !IsApplication
            && !other.IsApplication
            && Version == other.Version
            && string.Equals(Name, other.Name, StringComparison.Ordinal);
    }
}
