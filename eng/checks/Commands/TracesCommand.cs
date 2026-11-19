using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Finds the traces of the project plans in the texts of the repository (<c>docs/design/eng-checks-source.md</c>, "traces"): the
/// codes of decision records, requirements, fitness functions, tasks and themes, the names of the documents of the specification and
/// Cyrillic letters, in the sources, the tests, the documents and the files of the packages. Every one is <c>ENG103</c>.
/// </summary>
internal sealed class TracesCommand : ICheckCommand
{
    /// <inheritdoc/>
    public string Name => "traces";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        List<Finding> findings = [];
        foreach (RepositoryFile file in context.Files.Where(file => ConceptTraces.IsScanned(file.Path)))
        {
            findings.AddRange(ConceptTraces.Scan(file.Path, file.ReadText()));
        }

        return findings;
    }
}
