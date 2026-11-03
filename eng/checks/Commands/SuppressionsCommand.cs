using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Finds every place that turns a check or a rule off or lowers its level, checks the reason next to it and counts the places
/// by key for the ratchet (<c>docs/design/eng-checks-source.md</c>, "suppressions"). The forms are read by the scanners.
/// </summary>
internal sealed class SuppressionsCommand : ICheckCommand
{
    /// <summary>
    /// The scanners of the forms, in the order of their findings. A scanner for another kind of file is added here together
    /// with its class.
    /// </summary>
    internal static IReadOnlyList<ISuppressionScanner> Scanners { get; } =
    [
        new CSharpSuppressionScanner(),
        new GeneratedFileSuppressionScanner(),
        new EditorConfigSuppressionScanner(),
        new MSBuildSuppressionScanner(),
        new LycheeSuppressionScanner(),
        new LycheeIgnoreSuppressionScanner(),
        new ExampleSuppressionScanner(),
    ];

    /// <inheritdoc/>
    public string Name => "suppressions";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context) => SuppressionScan.Of(context).Findings;
}
