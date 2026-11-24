using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Finds repeated code after renaming: runs of tokens of the C# sources that are equal when every identifier and every literal is
/// replaced by its kind (<c>docs/design/eng-checks-source.md</c>, "duplicates"). The search is made by <see cref="DuplicateScan"/>.
/// </summary>
internal sealed class DuplicatesCommand : ICheckCommand
{
    /// <summary>The length of the window of the search in tokens; the shortest repeat that is a pair.</summary>
    internal const int WindowTokens = 100;

    /// <summary>
    /// The directory under the root whose files are not searched and form no pair with other files. A sample is copied whole into another
    /// project, so every sample carries its own introduction, and the samples repeat each other on purpose.
    /// </summary>
    internal const string ExcludedDirectory = "samples/";

    /// <inheritdoc/>
    public string Name => "duplicates";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context) => DuplicateScan.Of(context).Findings;
}
