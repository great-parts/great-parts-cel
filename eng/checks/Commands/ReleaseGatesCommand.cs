using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The gates of a release and the check of origin of its packages (<c>docs/design/eng-release.md</c>, "eng/checks release-gates"). Without
/// a mode option all gates run on the tag: <c>ENG901</c>–<c>ENG906</c>, <c>ENG908</c> and <c>ENG803</c>. <c>--provenance</c> runs the check of origin
/// (<c>ENG907</c>) on the packages of a directory, <c>--nuget</c> downloads the packages of the version from nuget.org into a directory
/// and, with <c>--same-content-as</c>, compares their content with the packages of the build.
/// </summary>
internal sealed class ReleaseGatesCommand : ICheckCommand
{
    /// <summary>The option of the command line: the commit to check instead of the commit of the tag.</summary>
    internal const string CommitOption = "--commit";

    /// <summary>The option of the command line: the date of the release, <c>yyyy-MM-dd</c>.</summary>
    internal const string DateOption = "--date";

    /// <summary>The option of the command line: the address or the path of the repository of cel-spec.</summary>
    internal const string CelSpecOption = "--cel-spec";

    /// <summary>The option of the command line: the repository of the workflow runs and of the origin, <c>owner/name</c>.</summary>
    internal const string RepositoryOption = "--repository";

    /// <summary>The option of the command line: the address of the API of GitHub.</summary>
    internal const string ApiOption = "--api";

    /// <summary>The option of the command line: the directory of the packages for the check of origin.</summary>
    internal const string ProvenanceOption = "--provenance";

    /// <summary>The option of the command line: with <c>--nuget</c>, the directory of the packages of the build that the downloaded ones are compared with.</summary>
    internal const string SameContentOption = "--same-content-as";

    /// <summary>The option of the command line: the directory that the packages of the version are downloaded into.</summary>
    internal const string NuGetOption = "--nuget";

    private const string DefaultRepository = "great-parts/great-parts-cel";
    private const string DefaultApi = "https://api.github.com";
    private const string DefaultCelSpec = "https://github.com/cel-expr/cel-spec.git";

    private static readonly Regex RepositoryName = new("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\\z", RegexOptions.CultureInvariant);
    private static readonly Regex CommitHash = new("^[0-9A-Fa-f]{7,40}\\z", RegexOptions.CultureInvariant);

    private readonly Func<string, IGitHubApi> _api;
    private readonly IGh _gh;
    private readonly IPackageSource _source;
    private readonly IReleaseClock _clock;

    /// <summary>Creates the command with the API of GitHub, <c>gh</c>, nuget.org and the clock of the machine.</summary>
    internal ReleaseGatesCommand()
        : this(
            address => new HttpGitHubApi(address, Environment.GetEnvironmentVariable("GITHUB_TOKEN")),
            new GhProgram(),
            new HttpPackageSource(),
            new SystemReleaseClock())
    {
    }

    /// <summary>Creates the command with the outer world given; a test gives its own, so no check goes to the network.</summary>
    /// <param name="api">Gives the access to the API of GitHub for an address.</param>
    /// <param name="gh">The GitHub CLI.</param>
    /// <param name="source">nuget.org.</param>
    /// <param name="clock">The clock.</param>
    internal ReleaseGatesCommand(Func<string, IGitHubApi> api, IGh gh, IPackageSource source, IReleaseClock clock)
    {
        _api = api;
        _gh = gh;
        _source = source;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name => "release-gates";

    /// <inheritdoc/>
    public string? ArgumentName => "<tag>";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        if (!context.Arguments.TryGetValue(Name, out string? name))
        {
            throw new CheckInputException($"the command {Name} needs {ArgumentName}");
        }

        ReleaseTag tag = ReleaseTag.Parse(name);
        string repository = Repository(context);
        bool provenance = context.Options.TryGetValue(ProvenanceOption, out string? packages);
        bool download = context.Options.TryGetValue(NuGetOption, out string? target);
        if (provenance && download)
        {
            throw new CheckInputException($"the options {ProvenanceOption} and {NuGetOption} are two modes: give one");
        }

        bool compare = context.Options.TryGetValue(SameContentOption, out string? built);
        if (compare && !download)
        {
            throw new CheckInputException($"the option {SameContentOption} needs the option {NuGetOption}");
        }

        if (provenance)
        {
            return new ProvenanceCheck(_gh, _clock).Check(packages!, tag, repository);
        }

        return download ? Download(target!, built, tag) : Gates(context, tag, repository);
    }

    // The packages of the version from nuget.org and, with the directory of the build, the comparison of their content with it.
    private List<Finding> Download(string target, string? built, ReleaseTag tag)
    {
        List<Finding> findings = [.. new NuGetDownload(_source, _clock).Download(target, tag)];
        if (built is not null)
        {
            findings.AddRange(ProvenanceCheck.SameContent(target, built, tag));
        }

        return findings;
    }

    private List<Finding> Gates(CheckContext context, ReleaseTag tag, string repository)
    {
        string root = context.Root;
        string commit = Commit(context, tag);
        IGitHubApi api = _api(context.Options.TryGetValue(ApiOption, out string? address) ? address : DefaultApi);
        List<Finding> findings = [];
        findings.AddRange(ChangelogGate.Check(root, tag));
        findings.AddRange(MainAncestryGate.Check(root, tag, commit));
        findings.AddRange(RegressionListGate.Check(root, tag));
        findings.AddRange(LongRunGate.Check(tag, commit, repository, api));
        findings.AddRange(WorkflowsGreenGate.Check(tag, commit, repository, api));
        findings.AddRange(CelSpecPinGate.Check(root, Date(context), CelSpec(context)));
        findings.AddRange(SkipReasonGate.Check(root));
        findings.AddRange(SkipReferenceGate.Check(root, tag));
        return findings;
    }

    private static string Repository(CheckContext context)
    {
        string? repository;
        if (!context.Options.TryGetValue(RepositoryOption, out repository))
        {
            repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        }

        if (string.IsNullOrEmpty(repository))
        {
            repository = DefaultRepository;
        }

        return RepositoryName.IsMatch(repository)
            ? repository
            : throw new CheckInputException($"the repository '{repository}' is not owner/name");
    }

    // The full hash of the commit of the option, or of the commit of the tag.
    private static string Commit(CheckContext context, ReleaseTag tag)
    {
        string revision = "refs/tags/" + tag.Name;
        if (context.Options.TryGetValue(CommitOption, out string? given))
        {
            revision = CommitHash.IsMatch(given) ? given : throw new CheckInputException($"the commit '{given}' is not a hash of 7 to 40 hexadecimal digits");
        }

        GitResult result = Git.Run(context.Root, "rev-parse", "--verify", "--quiet", revision + "^{commit}");
        return result.Succeeded && result.Output.Trim().Length == 40
            ? result.Output.Trim()
            : throw new CheckInputException($"the commit of {revision} cannot be found in the repository; for a run before the tag give {CommitOption}");
    }

    private DateOnly Date(CheckContext context)
    {
        if (!context.Options.TryGetValue(DateOption, out string? given))
        {
            return DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        }

        return DateOnly.TryParseExact(given, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : throw new CheckInputException($"the date '{given}' is not yyyy-MM-dd");
    }

    // A relative path is taken from the working directory; an address is used as it is.
    private static string CelSpec(CheckContext context)
    {
        if (!context.Options.TryGetValue(CelSpecOption, out string? given))
        {
            return DefaultCelSpec;
        }

        return given.Contains("://", StringComparison.Ordinal) || given.StartsWith("git@", StringComparison.Ordinal)
            ? given
            : Path.GetFullPath(given);
    }
}
