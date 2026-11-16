using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG907</c>, the check of origin: <c>gh attestation verify</c> on every <c>.nupkg</c> of a directory with the flags of
/// <see cref="ProvenanceFlags"/>, on the file as it is. A package whose attestation is not visible yet is checked again. The same class
/// compares the packages that nuget.org gives with the packages of the build (<see cref="SameContent"/>): the guard of the second
/// attestation, which is issued only for a file whose content is the content that the build attested.
/// </summary>
internal sealed class ProvenanceCheck
{
    /// <summary>The entry that nuget.org adds to a package.</summary>
    internal const string SignatureEntry = ".signature.p7s";

    /// <summary>How many times a failed check is repeated.</summary>
    internal const int Repeats = 5;

    /// <summary>The wait between two tries.</summary>
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    private readonly IGh _gh;
    private readonly IReleaseClock _clock;

    /// <summary>Creates the check.</summary>
    /// <param name="gh">The GitHub CLI.</param>
    /// <param name="clock">The clock for the waits.</param>
    internal ProvenanceCheck(IGh gh, IReleaseClock clock)
    {
        _gh = gh;
        _clock = clock;
    }

    /// <summary>Checks every package of the directory.</summary>
    /// <param name="directory">The full path of the directory; its subdirectories are not read.</param>
    /// <param name="tag">The release tag.</param>
    /// <param name="repository">The repository of the origin, <c>owner/name</c>.</param>
    /// <exception cref="CheckInputException">The directory does not exist or has no package, or <c>gh</c> cannot be started.</exception>
    internal IEnumerable<Finding> Check(string directory, ReleaseTag tag, string repository)
    {
        if (!Directory.Exists(directory))
        {
            throw new CheckInputException($"the directory of the packages {directory} does not exist");
        }

        string[] packages = [.. Directory.EnumerateFiles(directory, "*.nupkg", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal)];
        if (packages.Length == 0)
        {
            throw new CheckInputException($"the directory {directory} has no .nupkg package");
        }

        List<Finding> findings = [];
        foreach (string package in packages)
        {
            string failure = Verify(package, tag, repository);
            if (failure.Length > 0)
            {
                findings.Add(Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng907, $"provenance check failed for {Path.GetFileName(package)}: {failure}"));
            }
        }

        return findings;
    }

    /// <summary>
    /// Compares each downloaded package with the package of the same file name (case ignored) in the directory of the build: the sets of
    /// entries without <c>.signature.p7s</c> are equal and each entry has the same decompressed bytes.
    /// </summary>
    /// <param name="downloaded">The full path of the directory that the packages were downloaded into.</param>
    /// <param name="built">The full path of the directory of the packages of the build.</param>
    /// <param name="tag">The release tag.</param>
    /// <exception cref="CheckInputException">The directory of the build does not exist.</exception>
    internal static IEnumerable<Finding> SameContent(string downloaded, string built, ReleaseTag tag)
    {
        if (!Directory.Exists(built))
        {
            throw new CheckInputException($"the directory of the built packages {built} does not exist");
        }

        List<Finding> findings = [];
        foreach (string id in NuGetDownload.PackageIds)
        {
            string name = $"{id}.{tag.Version}.nupkg";
            string package = Path.Combine(downloaded, name);
            if (!File.Exists(package))
            {
                continue;
            }

            string? counterpart = Directory.EnumerateFiles(built, "*.nupkg", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase));
            string difference = counterpart is null ? "the build has no package of that name" : Difference(package, counterpart);
            if (difference.Length > 0)
            {
                findings.Add(Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng907, $"content check failed for {name}: {difference}"));
            }
        }

        return findings;
    }

    // An empty text is a package with the content of the built one; otherwise the first entry, in the order of the names, that differs.
    private static string Difference(string downloaded, string built)
    {
        try
        {
            Dictionary<string, byte[]> expected = Entries(built);
            Dictionary<string, byte[]> actual = Entries(downloaded);
            foreach (string entry in expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal))
            {
                if (!actual.TryGetValue(entry, out byte[]? bytes))
                {
                    return $"the entry '{entry}' is missing";
                }

                if (!expected.TryGetValue(entry, out byte[]? original))
                {
                    return $"the entry '{entry}' is not in the built package";
                }

                if (!bytes.AsSpan().SequenceEqual(original))
                {
                    return $"the entry '{entry}' differs from the built package";
                }
            }

            return string.Empty;
        }
        catch (InvalidDataException exception)
        {
            return $"the package is not a zip archive: {exception.Message}";
        }
    }

    // The decompressed bytes of every entry of a package by its name, but for the signature of nuget.org.
    private static Dictionary<string, byte[]> Entries(string package)
    {
        using ZipArchive archive = ZipFile.OpenRead(package);
        Dictionary<string, byte[]> entries = new(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.FullName != SignatureEntry))
        {
            using Stream stream = entry.Open();
            using MemoryStream bytes = new();
            stream.CopyTo(bytes);
            entries[entry.FullName] = bytes.ToArray();
        }

        return entries;
    }

    // An empty text is a package that passed; otherwise the first line of the output of the last try.
    private string Verify(string file, ReleaseTag tag, string repository)
    {
        List<string> arguments = ["attestation", "verify", file, .. ProvenanceFlags.Arguments(repository, tag.Name)];
        GhResult result = default;
        for (int attempt = 0; attempt <= Repeats; attempt++)
        {
            if (attempt > 0)
            {
                _clock.Delay(Wait);
            }

            result = _gh.Run(arguments);
            if (result.ExitCode == 0)
            {
                return string.Empty;
            }
        }

        string first = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "no output";
        return first;
    }
}
