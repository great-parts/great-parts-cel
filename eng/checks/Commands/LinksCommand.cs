using System.Collections.Generic;
using System.IO;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Turns the JSON report of lychee into findings (<c>docs/design/eng-docs.md</c>, "links"). Every failed link is a finding in its input
/// file: a missing file is <c>ENG403</c>, a missing anchor <c>ENG404</c>, an external link that answered 404 or 410 <c>ENG401</c>,
/// one whose domain does not exist <c>ENG402</c>, and any other failure of an external link the warning <c>ENG409</c>. The line and the
/// column are those of the report; when it gives none, the first place of the address in the file does. The report is given by
/// <c>--report</c>; it is the offline report of <c>eng/docs</c> or the report of the external links of CI.
/// </summary>
internal sealed class LinksCommand : ICheckCommand
{
    /// <summary>The option of the command line: the JSON report of lychee.</summary>
    internal const string ReportOption = MutationCommand.ReportOption;

    private readonly IHostResolver _resolver;

    /// <summary>Creates the command with the resolver of the machine.</summary>
    internal LinksCommand()
        : this(new DnsHostResolver())
    {
    }

    /// <summary>Creates the command with a resolver; a test gives its own, so no check goes to the network.</summary>
    /// <param name="resolver">Tells whether a domain exists, for the connections that failed.</param>
    internal LinksCommand(IHostResolver resolver) => _resolver = resolver;

    /// <inheritdoc/>
    public string Name => "links";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        if (!context.Options.TryGetValue(ReportOption, out string? given))
        {
            throw new CheckInputException($"the command {Name} needs the option {ReportOption}");
        }

        if (!File.Exists(given))
        {
            throw new CheckInputException($"the lychee report {given} does not exist");
        }

        LycheeReport report = LycheeReport.Read(given);
        LinkSources sources = new(context.Root);
        LinkClassifier classifier = new(context.Root, _resolver);
        List<Finding> findings = [];
        foreach (ReportedLink link in report.Failures)
        {
            string path = sources.RelativePath(link.File);
            LinkVerdict verdict = classifier.Classify(link, path);
            Position place = sources.Locate(path, link.Span, verdict.Needles);
            findings.Add(new Finding(path, place.Line, place.Column, verdict.Severity, verdict.Code, verdict.Message));
        }

        return findings;
    }
}
