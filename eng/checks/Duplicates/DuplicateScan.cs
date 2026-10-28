using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The result of the search for repeats: the pairs with their places, which the ratchet counts and names, and the findings
/// with their level. The scan is made once per run and kept in <see cref="CheckContext"/>, so <c>duplicates</c> and
/// <c>ratchet</c> of one run share it.
/// </summary>
internal sealed class DuplicateScan
{
    private const string ScanName = "duplicates";

    private DuplicateScan(IReadOnlyList<DuplicatePair> pairs, IReadOnlyList<Finding> findings)
    {
        Pairs = pairs;
        Findings = findings;
    }

    /// <summary>Every pair, sorted by the first place and then by the second one.</summary>
    internal IReadOnlyList<DuplicatePair> Pairs { get; }

    /// <summary><c>ENG601</c>, one per pair at its first place: a warning while the number of pairs is within the baseline, an error above it.</summary>
    internal IReadOnlyList<Finding> Findings { get; }

    /// <summary>Returns the scan of the run, making it on the first call.</summary>
    /// <param name="context">The context of the run.</param>
    internal static DuplicateScan Of(CheckContext context) =>
        context.Scan(ScanName, () => Run(context, DuplicatesCommand.WindowTokens));

    /// <summary>Searches the C# sources for repeats of at least <paramref name="window"/> tokens.</summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="window">The length of the window in tokens.</param>
    internal static DuplicateScan Run(CheckContext context, int window)
    {
        CSharpSources sources = context.Sources;
        IReadOnlyList<RawDuplicate> joined = DuplicateMerge.Join(SearchBySet(sources, window));
        List<DuplicatePair> pairs = [.. joined.Select(pair => Resolve(sources, pair))];
        FindingSeverity severity = pairs.Count <= RatchetFile.Of(context).Duplicates ? FindingSeverity.Warning : FindingSeverity.Error;
        List<Finding> findings = [.. pairs.Select(pair => new Finding(
            pair.FirstPath,
            pair.First.Line,
            pair.First.Column,
            severity,
            EngCodes.Eng601,
            $"{pair.Tokens} tokens duplicated at {pair.SecondPath}({pair.Second.Line},{pair.Second.Column})"))];
        return new DuplicateScan(pairs, findings);
    }

    // One search per symbol set over the tokens of that set. A set that reads every file as an earlier one does has the same
    // pairs and is not searched again.
    private static List<IReadOnlyList<RawDuplicate>> SearchBySet(CSharpSources sources, int window)
    {
        List<TokenStream> streams = [];
        List<IReadOnlyList<RawDuplicate>> bySet = [];
        for (int set = 0; set < sources.Sets.Count; set++)
        {
            TokenStream stream = TokenStream.Build(sources.Files, set);
            if (!streams.Any(stream.SameAs))
            {
                streams.Add(stream);
                bySet.Add(DuplicateSearch.Find(stream, window));
            }
        }

        return bySet;
    }

    private static DuplicatePair Resolve(CSharpSources sources, RawDuplicate pair)
    {
        CSharpFile first = sources.Files[pair.First.File];
        CSharpFile second = sources.Files[pair.Second.File];
        return new DuplicatePair(
            first.Path,
            first.Lines.Locate(pair.First.Start),
            second.Path,
            second.Lines.Locate(pair.Second.Start),
            pair.Tokens);
    }
}
