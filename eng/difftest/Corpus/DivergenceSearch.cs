using System;
using System.Collections.Generic;
using System.Globalization;
using Cel.Expr;
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
    /// <returns>The divergence of the minimized case, or <see langword="null"/> when the outcomes agree.</returns>
    /// <exception cref="SideFailureException">The minimized case agrees when it is run again: a side is not deterministic.</exception>
    internal Divergence? Check(ulong seed, ulong number, GeneratedCase generated)
    {
        if (Outcomes(generated).Kind is null)
        {
            return null;
        }

        GeneratedCase minimized = Minimizer.Minimize(generated, Classify, AcceptsBodyInstance, LibraryValue);
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

    // How the outcomes of a node on the way down agree: they diverge, or agree because both sides give an error, or agree
    // otherwise. A node is evaluated once on each side, whatever the verdict is used for.
    private NodeVerdict Classify(GeneratedCase generated)
    {
        (SideOutcome libraryOutcome, SideOutcome oracleOutcome, _) = Outcomes(generated);
        return VerdictOf(libraryOutcome, oracleOutcome);
    }

    /// <summary>
    /// The verdict on a node of the descent: the outcomes diverge, or agree because both sides give an error, whatever its
    /// cause, or agree otherwise.
    /// </summary>
    /// <param name="library">The outcome of the node on the library.</param>
    /// <param name="oracle">The outcome of the node on cel-go.</param>
    internal static NodeVerdict VerdictOf(SideOutcome library, SideOutcome oracle) =>
        DivergenceKinds.Compare(library, oracle) is not null ? NodeVerdict.Diverges : library.IsError ? NodeVerdict.AgreesInError : NodeVerdict.Agrees;

    // A minimized instance of a body of a macro replaces the macro only when its signature is the signature of the macro or a
    // known one: a divergence under a macro that nobody knows stays the signature of the macro.
    private bool AcceptsBodyInstance(GeneratedCase macro, GeneratedCase instance) =>
        SignatureOf(instance) is { } signature && (known.Contains(signature) || signature == SignatureOf(macro));

    // The value of a case on the library, which gives the elements of a macro range that is not a literal; the range agrees on
    // both sides, since the descent has compared it as a child before.
    private Value? LibraryValue(GeneratedCase generated) => library.Evaluate(generated.Text, generated.Bindings).Result;

    private string? SignatureOf(GeneratedCase generated)
    {
        (SideOutcome libraryOutcome, SideOutcome oracleOutcome, DivergenceKind? kind) = Outcomes(generated);
        return kind is { } divergenceKind ? Signature.Of(generated, libraryOutcome, oracleOutcome, divergenceKind, library, oracle) : null;
    }

    private (SideOutcome Library, SideOutcome Oracle, DivergenceKind? Kind) Outcomes(GeneratedCase generated)
    {
        string text = generated.Text;
        SideOutcome libraryOutcome = library.Evaluate(text, generated.Bindings);
        SideOutcome oracleOutcome = oracle.Evaluate(text, generated.Bindings);
        return (libraryOutcome, oracleOutcome, DivergenceKinds.Compare(libraryOutcome, oracleOutcome));
    }
}
