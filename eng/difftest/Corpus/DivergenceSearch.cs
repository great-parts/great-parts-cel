using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Corpus;

/// <summary>The first case of a new signature.</summary>
/// <param name="Divergence">The divergence of the minimized case.</param>
/// <param name="Seed">The seed of the run.</param>
/// <param name="Number">The number of the generated case in the run.</param>
/// <param name="CaseText">The case of the regression corpus written by the oracle, in text format.</param>
internal sealed record NewSignature(Divergence Divergence, ulong Seed, ulong Number, string CaseText);

/// <summary>
/// Steps 4 and 5 of a run for one case (docs/design/eng-difftest.md, "A run", "Known signatures and the corpus"): evaluates
/// the case on both sides; a divergence is minimized and gets a signature; the first case of each new signature is written
/// by the oracle as a case of the regression corpus.
/// </summary>
internal sealed class DivergenceSearch
{
    /// <summary>The section of <c>pending.textproto</c>: the cases are named <c>pending/new/&lt;seed&gt;-&lt;n&gt;</c>.</summary>
    internal const string SectionName = "new";

    private readonly ICaseSide library;
    private readonly ICaseSide oracle;
    private readonly ICaseRecorder recorder;
    private readonly KnownSignatures known;
    private readonly List<NewSignature> newSignatures = [];
    private readonly HashSet<string> newSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> knownSeen = new(StringComparer.Ordinal);

    /// <summary>Creates the search of a run.</summary>
    /// <param name="library">The library.</param>
    /// <param name="oracle">The oracle.</param>
    /// <param name="recorder">The oracle, which writes the cases of new signatures.</param>
    /// <param name="known">The known signatures.</param>
    internal DivergenceSearch(ICaseSide library, ICaseSide oracle, ICaseRecorder recorder, KnownSignatures known)
    {
        this.library = library;
        this.oracle = oracle;
        this.recorder = recorder;
        this.known = known;
    }

    /// <summary>The first case of each new signature, in the order the run found them.</summary>
    internal IReadOnlyList<NewSignature> NewSignatures => newSignatures;

    /// <summary>The known signatures that the run found.</summary>
    internal IReadOnlyCollection<string> KnownSignaturesFound => knownSeen;

    /// <summary>Checks a case of the run.</summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="number">The number of the case.</param>
    /// <param name="generated">The case.</param>
    /// <param name="limit">How long minimization searches.</param>
    /// <returns>The divergence of the minimized case, or <see langword="null"/> when the outcomes agree.</returns>
    /// <exception cref="SideFailureException">The minimized case agrees when it is run again: a side is not deterministic.</exception>
    internal Divergence? Check(ulong seed, ulong number, GeneratedCase generated, MinimizationLimit limit)
    {
        if (Outcomes(generated).Kind is null)
        {
            return null;
        }

        GeneratedCase minimized = Minimizer.Minimize(generated, candidate => Outcomes(candidate).Kind is not null, limit);
        (SideOutcome libraryOutcome, SideOutcome oracleOutcome, DivergenceKind? kind) = Outcomes(minimized);
        if (kind is not { } divergenceKind)
        {
            string run = string.Create(CultureInfo.InvariantCulture, $"case {number} of seed {seed}");
            throw new SideFailureException(
                $"{run}: the minimized case does not diverge again: a side is not deterministic",
                $"{run}: the minimized case '{minimized.Text}' does not diverge again: a side is not deterministic");
        }

        string signature = Signature.Of(minimized, libraryOutcome, oracleOutcome, divergenceKind, library, oracle);
        Divergence divergence = new(signature, divergenceKind, minimized, libraryOutcome, oracleOutcome);
        if (known.Contains(signature))
        {
            knownSeen.Add(signature);
        }
        else if (newSeen.Add(signature))
        {
            string name = string.Create(CultureInfo.InvariantCulture, $"{seed}-{number}");
            string text = recorder.Case(name, signature, minimized.Text, minimized.Bindings);
            newSignatures.Add(new NewSignature(divergence, seed, number, text));
        }

        return divergence;
    }

    private (SideOutcome Library, SideOutcome Oracle, DivergenceKind? Kind) Outcomes(GeneratedCase generated)
    {
        string text = generated.Text;
        SideOutcome libraryOutcome = library.Evaluate(text, generated.Bindings);
        SideOutcome oracleOutcome = oracle.Evaluate(text, generated.Bindings);
        return (libraryOutcome, oracleOutcome, DivergenceKinds.Compare(libraryOutcome, oracleOutcome));
    }
}
