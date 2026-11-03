using System;
using System.Collections.Generic;
using System.IO;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>What a failed link is: the code and level of the finding, its message, and the texts that find the link in its file.</summary>
/// <param name="Code">The code, <c>ENG401</c> to <c>ENG404</c> or <c>ENG409</c>.</param>
/// <param name="Severity">An error, a warning for <c>ENG409</c>.</param>
/// <param name="Message">The message: English, one line.</param>
/// <param name="Needles">The texts to search for when the report gives no span, the most exact first.</param>
internal sealed record LinkVerdict(string Code, FindingSeverity Severity, string Message, IReadOnlyList<string> Needles);

/// <summary>
/// Gives the code to a failed link of the lychee report by the kind of its failure (<c>docs/design/eng-docs.md</c>, "links"): a missing
/// anchor is <c>ENG404</c>; a missing file or a link that cannot be resolved is <c>ENG403</c>; an external link that answered 404 or 410
/// is <c>ENG401</c>, one whose domain does not exist is <c>ENG402</c>; any other failure of an external link is the warning <c>ENG409</c>.
/// </summary>
internal sealed class LinkClassifier
{
    private const string FragmentText = "Cannot find fragment";
    private const string FileNotFoundText = "File not found";
    private const string ConnectionFailedText = "Connection failed";
    private const string StatusPrefix = "Rejected status code: ";
    private const string UnresolvedUrl = "error:";
    private const int NotFound = 404;
    private const int Gone = 410;

    private readonly string _root;
    private readonly IHostResolver _resolver;
    private readonly Dictionary<string, bool> _unknownHosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the classifier.</summary>
    /// <param name="root">The full path of the repository root, the working directory of lychee: the paths of the files are relative to it.</param>
    /// <param name="resolver">Tells whether a domain exists, for the connections that failed.</param>
    internal LinkClassifier(string root, IHostResolver resolver)
    {
        _root = root;
        _resolver = resolver;
    }

    /// <summary>Classifies a failed link.</summary>
    /// <param name="link">The failed link of the report.</param>
    /// <param name="sourcePath">The path of its input file, relative to the root.</param>
    internal LinkVerdict Classify(ReportedLink link, string sourcePath)
    {
        Uri.TryCreate(link.Url, UriKind.Absolute, out Uri? uri);
        if (link.Details.Contains(FragmentText, StringComparison.Ordinal) && uri is not null)
        {
            return Fragment(uri, sourcePath);
        }

        if (link.Url.StartsWith(UnresolvedUrl, StringComparison.Ordinal))
        {
            return Error(EngCodes.Eng403, $"link cannot be resolved: {link.Details}", [Quoted(link.Text)]);
        }

        if (uri is { IsFile: true })
        {
            return MissingFile(link, uri, sourcePath);
        }

        return External(link, uri);
    }

    private LinkVerdict Fragment(Uri uri, string sourcePath)
    {
        string fragment = uri.Fragment;
        if (uri.IsFile)
        {
            string target = Relative(uri.LocalPath);
            return Error(EngCodes.Eng404, $"fragment '{fragment}' not found in {target}", [FromDirectory(uri, sourcePath) + fragment, target + fragment, fragment]);
        }

        string page = uri.GetLeftPart(UriPartial.Query);
        return Error(EngCodes.Eng404, $"fragment '{fragment}' not found in {page}", [uri.OriginalString, page, fragment]);
    }

    private LinkVerdict MissingFile(ReportedLink link, Uri uri, string sourcePath)
    {
        string target = Relative(uri.LocalPath);
        string message = link.Text.StartsWith(FileNotFoundText, StringComparison.Ordinal) ? $"file '{target}' not found" : $"{OneLine(link.Details)}: {target}";
        return Error(EngCodes.Eng403, message, [FromDirectory(uri, sourcePath), target, Path.GetFileName(target)]);
    }

    private LinkVerdict External(ReportedLink link, Uri? uri)
    {
        string address = link.Url;
        List<string> needles = [address, address.TrimEnd('/')];
        if (link.StatusCode is { } code)
        {
            string reason = ReasonOf(link, code);
            bool dead = code is NotFound or Gone;
            return new LinkVerdict(dead ? EngCodes.Eng401 : EngCodes.Eng409, dead ? FindingSeverity.Error : FindingSeverity.Warning, $"{reason}: {address}", needles);
        }

        if (!link.TimedOut && uri is { Host.Length: > 0 } && link.Details.StartsWith(ConnectionFailedText, StringComparison.Ordinal) && IsUnknown(uri.Host))
        {
            return new LinkVerdict(EngCodes.Eng402, FindingSeverity.Error, $"the domain {uri.Host} does not exist: {address}", needles);
        }

        return new LinkVerdict(EngCodes.Eng409, FindingSeverity.Warning, $"{OneLine(link.Details)}: {address}", needles);
    }

    // "Rejected status code: 404 Not Found" is "404 Not Found"; a status text of another form is the code alone.
    private static string ReasonOf(ReportedLink link, int code) =>
        link.Text.StartsWith(StatusPrefix, StringComparison.Ordinal) ? OneLine(link.Text[StatusPrefix.Length..]) : code.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private bool IsUnknown(string host)
    {
        if (!_unknownHosts.TryGetValue(host, out bool unknown))
        {
            _unknownHosts[host] = unknown = _resolver.IsUnknown(host);
        }

        return unknown;
    }

    private string Relative(string fullPath)
    {
        string relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
        return relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? fullPath.Replace('\\', '/') : relative;
    }

    // The path of the target as the link may have it: relative to the directory of the file that holds the link.
    private string FromDirectory(Uri uri, string sourcePath) =>
        Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(sourcePath, _root)) ?? _root, uri.LocalPath).Replace('\\', '/');

    // lychee names the link as written in the text of an error that has no address: Error building URL for "/docs/b.md" (…).
    private static string Quoted(string text)
    {
        int start = text.IndexOf('"', StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf('"', start + 1);
        return end < 0 ? string.Empty : text[(start + 1)..end];
    }

    private static string OneLine(string text) => text.ReplaceLineEndings(" ");

    private static LinkVerdict Error(string code, string message, IReadOnlyList<string> needles) =>
        new(code, FindingSeverity.Error, message, needles);
}
