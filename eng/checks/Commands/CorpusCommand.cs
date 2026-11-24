using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Checks the publication line of the corpus of the filtering package (<c>docs/design/filtering-corpus.md</c>, "Publication"): the README
/// of the package and the page of conformance each have the line <c>AIP-160: own test corpus, N cases, run on yyyy-MM-dd</c>, with N the
/// number of the cases of <c>tests/filtering/testdata</c> and no percentage. A line that is missing, differs in form, counts another
/// number of cases or has a percentage is <c>ENG307</c>.
/// </summary>
internal sealed class CorpusCommand : ICheckCommand
{
    /// <inheritdoc/>
    public string Name => "corpus";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        int cases = CorpusPublication.CountCases(context.Files);
        if (cases == 0)
        {
            throw new CheckInputException($"the directory {CorpusPublication.DataDirectory} has no case of the corpus");
        }

        List<Finding> findings = [];
        foreach (string page in CorpusPublication.Pages)
        {
            RepositoryFile file = context.Files.FirstOrDefault(candidate => string.Equals(candidate.Path, page, StringComparison.Ordinal))
                ?? throw new CheckInputException($"the page {page} does not exist");
            findings.AddRange(CorpusPublication.Check(page, file.ReadText(), cases));
        }

        return findings;
    }
}
