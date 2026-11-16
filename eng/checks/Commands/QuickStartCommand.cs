using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Runs the steps of the quick start of <c>README.md</c> in a clean container with the .NET SDK 10 and judges them
/// (<c>docs/design/eng-release.md</c>, "eng/checks quick-start"): <c>ENG304</c> for a step that fails or an output that is not the expected
/// one, <c>ENG305</c> for a run of more than 300 seconds. The package of the commit is the one package source of the container.
/// </summary>
internal sealed class QuickStartCommand : ICheckCommand
{
    /// <summary>The option of the command line: the directory of the package built from the commit.</summary>
    internal const string PackagesOption = "--packages";

    /// <summary>The option of the command line: the image of the clean container.</summary>
    internal const string ImageOption = "--image";

    private const string ReadmePath = "README.md";

    // A stable package of the library: a version with no suffix, so that 'dotnet add package' without a version takes it. The package
    // of another ID that starts with the same words, and the symbol package, are not one.
    private static readonly Regex StablePackage = new(@"^Great\.Parts\.Cel\.\d+\.\d+\.\d+\.nupkg\z", RegexOptions.CultureInvariant);

    private readonly Func<string, string, IQuickStartContainer> _start;
    private readonly IQuickStartClock _clock;

    /// <summary>Creates the command with Docker and the stopwatch of the machine.</summary>
    internal QuickStartCommand()
        : this((image, packages) => new DockerQuickStartContainer(image, packages), new StopwatchClock())
    {
    }

    /// <summary>Creates the command with the container and the clock given; a test gives its own, so no test needs Docker.</summary>
    /// <param name="start">Starts the clean container for an image and the full path of the directory of the packages.</param>
    /// <param name="clock">The clock of the time of the steps.</param>
    internal QuickStartCommand(Func<string, string, IQuickStartContainer> start, IQuickStartClock clock)
    {
        _start = start;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name => "quick-start";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        if (!context.Options.TryGetValue(PackagesOption, out string? packages) || string.IsNullOrEmpty(packages)
            || !context.Options.TryGetValue(ImageOption, out string? image) || string.IsNullOrEmpty(image))
        {
            throw new CheckInputException($"the command {Name} needs {PackagesOption} and {ImageOption}");
        }

        string readme = Path.Combine(context.Root, ReadmePath);
        if (!File.Exists(readme))
        {
            throw new CheckInputException($"{ReadmePath} does not exist in {context.Root}");
        }

        QuickStartBlock block = QuickStartBlock.Read(ReadmePath, File.ReadAllText(readme));
        if (!Directory.Exists(packages) || !Directory.EnumerateFiles(packages, "*.nupkg").Any(file => StablePackage.IsMatch(Path.GetFileName(file))))
        {
            throw new CheckInputException($"the directory {packages} has no stable package Great.Parts.Cel (a version with no suffix)");
        }

        using IQuickStartContainer container = _start(image, packages);
        return QuickStartSession.Run(block, container, _clock);
    }
}
