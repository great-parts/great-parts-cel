using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Downloads the six packages of the version of the tag from nuget.org as it gives them, with the signature of the repository inside.
/// It asks again every 30 seconds for up to 15 minutes while nuget.org does not give a package yet; a package that does not appear is
/// <c>ENG907</c>.
/// </summary>
internal sealed class NuGetDownload
{
    /// <summary>The ids of the packages of a release, one list for the workflow and the checks.</summary>
    internal static readonly IReadOnlyList<string> PackageIds =
        [
            "Great.Parts.Cel",
            "Great.Parts.Cel.Protobuf",
            "Great.Parts.Cel.DependencyInjection",
            "Great.Parts.Cel.AspNetCore",
            "Great.Parts.Cel.Policy",
            "Great.Parts.Cel.Filtering",
        ];

    /// <summary>The wait between two asks.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>How long nuget.org may take to give the packages after the publication.</summary>
    internal static readonly TimeSpan Limit = TimeSpan.FromMinutes(15);

    private const string Flat = "https://api.nuget.org/v3-flatcontainer/";

    private readonly IPackageSource _source;
    private readonly IReleaseClock _clock;

    /// <summary>Creates the download.</summary>
    /// <param name="source">Where the packages come from.</param>
    /// <param name="clock">The clock for the waits and the limit.</param>
    internal NuGetDownload(IPackageSource source, IReleaseClock clock)
    {
        _source = source;
        _clock = clock;
    }

    /// <summary>The address of a package in the flat container of nuget.org: the id and the version in lower case.</summary>
    /// <param name="id">The id of the package.</param>
    /// <param name="version">The version of the release.</param>
    internal static Uri AddressOf(string id, string version)
    {
        string lowerId = Lower(id);
        string lowerVersion = Lower(version);
        return new Uri($"{Flat}{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg", UriKind.Absolute);
    }

    /// <summary>Downloads the packages into the directory.</summary>
    /// <param name="directory">The full path of the directory; it is created when it does not exist.</param>
    /// <param name="tag">The release tag.</param>
    internal IEnumerable<Finding> Download(string directory, ReleaseTag tag)
    {
        Directory.CreateDirectory(directory);
        DateTimeOffset deadline = _clock.UtcNow + Limit;
        List<Finding> findings = [];
        foreach (string id in PackageIds)
        {
            string name = $"{id}.{tag.Version}.nupkg";
            PackageAnswer answer = Fetch(AddressOf(id, tag.Version), deadline);
            if (answer.Found)
            {
                File.WriteAllBytes(Path.Combine(directory, name), answer.Content);
            }
            else
            {
                string minutes = ((int)Limit.TotalMinutes).ToString(CultureInfo.InvariantCulture);
                findings.Add(Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng907, $"{name} is not on nuget.org after {minutes} minutes: {answer.Reason}"));
            }
        }

        return findings;
    }

    // Asks until the package comes or the deadline has passed; a package is asked for at least once.
    private PackageAnswer Fetch(Uri address, DateTimeOffset deadline)
    {
        PackageAnswer answer = _source.Fetch(address);
        while (!answer.Found && _clock.UtcNow < deadline)
        {
            _clock.Delay(Interval);
            answer = _source.Fetch(address);
        }

        return answer;
    }

    private static string Lower(string text) => string.Concat(text.Select(char.ToLowerInvariant));
}
