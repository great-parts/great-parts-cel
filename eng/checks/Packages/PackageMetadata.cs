using System;
using System.Collections.Generic;
using System.IO;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Checks the metadata of a package and its symbol package (<c>docs/design/eng-checks-artifacts.md</c>, "packages"): the description is
/// not the default of the SDK, the authors are set, the repository URL is there, and a <c>.snupkg</c> lies next to the <c>.nupkg</c>.
/// Every problem is one <c>ENG208</c> at the path of the <c>.nupkg</c>.
/// </summary>
internal static class PackageMetadata
{
    /// <summary>The description that the SDK writes when the project sets none.</summary>
    internal const string DefaultDescription = "Package Description";

    /// <summary>Checks one package.</summary>
    /// <param name="package">The package.</param>
    /// <param name="fullPath">The full path of the <c>.nupkg</c>; the <c>.snupkg</c> is looked for next to it.</param>
    internal static IEnumerable<Finding> Check(PackageContent package, string fullPath)
    {
        if (package.Description.Length == 0 || string.Equals(package.Description, DefaultDescription, StringComparison.Ordinal))
        {
            yield return Problem(package, "the description is missing or is the default of the SDK");
        }

        // The SDK writes the package ID as the authors when the project sets none.
        if (package.Authors.Length == 0 || string.Equals(package.Authors, package.Id, StringComparison.OrdinalIgnoreCase))
        {
            yield return Problem(package, "the authors are missing or are the default of the SDK");
        }

        if (package.RepositoryUrl.Length == 0)
        {
            yield return Problem(package, "the package has no repository url");
        }

        if (!File.Exists(Path.ChangeExtension(fullPath, ".snupkg")))
        {
            yield return Problem(package, "the .snupkg next to the package is missing");
        }
    }

    private static Finding Problem(PackageContent package, string message) =>
        new(package.Path, 1, 1, FindingSeverity.Error, EngCodes.Eng208, $"{message} ({package.Id})");
}
