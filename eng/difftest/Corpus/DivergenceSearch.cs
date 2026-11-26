using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Corpus;

/// <summary>The first case of a new signature.</summary>
/// <param name="Divergence">The divergence of the minimized case.</param>
/// <param name="Seed">The seed of the run.</param>
/// <param name="Number">The number of the generated case in the run.</param>
/// <param name="CaseText">The case of the regression corpus written by the oracle, in text format; for a partial case the expression and the unknown data in one line.</param>
/// <param name="Partial">Whether the divergence is one of partial evaluation: its case is no case of the regression corpus and is not written to <c>pending.textproto</c>.</param>
internal sealed record NewSignature(Divergence Divergence, ulong Seed, ulong Number, string CaseText, bool Partial = false);

/// <summary>The side of a comparison that gave another outcome when a case was run again.</summary>
internal enum UnstableSide
{
    /// <summary>No side did: the variation did not come back in the repeated runs.</summary>
    None,

    /// <summary>Only the library did.</summary>
    Library,

    /// <summary>Only the oracle did.</summary>
    Oracle,

    /// <summary>Both did.</summary>
    Both,
}

/// <summary>
/// A case that diverged when it was first run and whose minimized case agrees when it is run again: a side gave another outcome for
/// the same text and bindings, so it is not deterministic. The run goes on with the next case and ends with exit code 1.
/// </summary>
/// <param name="Seed">The seed of the run.</param>
/// <param name="Number">The number of the generated case in the run.</param>
/// <param name="Comparison">Which comparison found it: empty for the sides on the text, else <c>partial evaluation</c> or the prefix of an exchange without its colon.</param>
/// <param name="MinimizedText">The text of the minimized case, which agrees when it is run again.</param>
/// <param name="Side">The side that gave another outcome when the original and the minimized text were each run again.</param>
internal sealed record UnstableCase(ulong Seed, ulong Number, string Comparison, string MinimizedText, UnstableSide Side)
{
    /// <summary>The name of the side in the journal line and in the message: <c>library</c>, <c>oracle</c>, <c>both</c> or <c>none</c>.</summary>
    internal string SideName => Side switch
    {
        UnstableSide.Library => "library",
        UnstableSide.Oracle => "oracle",
        UnstableSide.Both => "both",
        _ => "none",
    };

    /// <summary>The message about the case, in one line, with the minimized case quoted; a finding of a run that shows the cases.</summary>
    internal string Message
    {
        get
        {
            string where = Comparison.Length == 0 ? string.Empty : " (" + Comparison + ")";
            return string.Create(
                CultureInfo.InvariantCulture,
                $"case {Number} of seed {Seed}{where}: the minimized case '{MinimizedText}' does not diverge again: a side is not deterministic; varied: {SideName}");
        }
    }
}

/// <summary>
/// Steps 4 and 5 of a run for one case (docs/design/eng-difftest.md, "A run", "Known signatures and the corpus"): evaluates
/// the case on both sides; a divergence is minimized and gets a signature; the first case of each new signature is written
/// by the oracle as a case of the regression corpus. A case on which the sides agree is also run through the two exchanges of a
/// checked expression, when the run has them (docs/design/ast-protobuf.md, "Exchange with cel-go"): each is a comparison with
/// its own signatures, searched, minimized and recorded as the first one is. The same search checks the partial evaluation of a
/// case (<see cref="CheckPartial"/>): the known and the new signatures are shared with the ordinary check.
/// </summary>
internal sealed class DivergenceSearch
{
    /// <summary>The section of <c>pending.textproto</c>: the cases are named <c>pending/new/&lt;seed&gt;-&lt;n&gt;</c>.</summary>
    internal const string SectionName = "new";

    /// <summary>How many times each side evaluates the original and the minimized text of an unstable case again to tell which side varies.</summary>
    internal const int RepeatsOfUnstableCase = 32;

    private readonly ICaseSide library;
    private readonly ICaseSide oracle;
    private readonly ICaseRecorder recorder;
    private readonly KnownSignatures known;
    private readonly Channel texts;
    private readonly List<Channel> exchanges = [];
    private readonly List<NewSignature> newSignatures = [];
    private readonly List<UnstableCase> unstableCases = [];
    private readonly HashSet<string> newSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> knownSeen = new(StringComparer.Ordinal);

    /// <summary>Creates the search of a run, without the exchange of a checked expression.</summary>
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
        texts = new Channel(
            string.Empty, library, oracle, null, "cel-go returns", "we return", library, recorder, Partial: false, UnstableSide.Library, UnstableSide.Oracle);
    }

    /// <summary>
    /// Adds the comparisons of the exchange of a checked expression, which a case is run through when the sides agree on the text.
    /// </summary>
    /// <param name="channels">The comparisons.</param>
    internal void AddExchanges(IEnumerable<ExchangeChannel> channels)
    {
        exchanges.AddRange(channels.Select(channel => new Channel(
            channel.Prefix,
            channel.Variant,
            channel.Reference,
            channel.Variant,
            channel.ReferencePhrase,
            channel.VariantPhrase,
            library,
            recorder,
            Partial: false,
            channel.VariantSide,
            channel.ReferenceSide)));
    }

    /// <summary>The first case of each new signature, in the order the run found them.</summary>
    internal IReadOnlyList<NewSignature> NewSignatures => newSignatures;

    /// <summary>The cases whose minimized case agrees when it is run again, in the order the run found them.</summary>
    internal IReadOnlyList<UnstableCase> UnstableCases => unstableCases;

    /// <summary>The known signatures that the run found.</summary>
    internal IReadOnlyCollection<string> KnownSignaturesFound => knownSeen;

    /// <summary>Whether both sides evaluate partially, so that <see cref="CheckPartial"/> can run.</summary>
    internal bool SupportsPartial => library is IPartialSide && oracle is IPartialSide;

    /// <summary>Checks a case of the run.</summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="number">The number of the case.</param>
    /// <param name="generated">The case.</param>
    /// <returns>
    /// The divergence of the minimized case; when the sides agree, the first divergence of the exchanges; <see langword="null"/>
    /// when nothing diverges, or when the minimized case agrees when it is run again (<see cref="UnstableCases"/>).
    /// </returns>
    internal Divergence? Check(ulong seed, ulong number, GeneratedCase generated)
    {
        // A case on which the sides diverge is spent: the exchanges would only repeat its divergence.
        Divergence? found = CheckChannel(texts, seed, number, generated);
        if (found is not null)
        {
            return found;
        }

        // Both exchanges are checked: a divergence of each is a finding of its own.
        foreach (Channel exchange in exchanges)
        {
            Divergence? divergence = CheckChannel(exchange, seed, number, generated);
            found ??= divergence;
        }

        return found;
    }

    /// <summary>
    /// Checks the partial evaluation of a case (docs/design/eng-difftest.md, "Partial evaluation"): both sides evaluate the case
    /// with the patterns as unknown data, and the minimization and the signature work on that evaluation.
    /// </summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="number">The number of the case.</param>
    /// <param name="generated">The case.</param>
    /// <param name="patterns">The patterns of unknown data.</param>
    /// <returns>
    /// The divergence of the minimized case, or <see langword="null"/> when the outcomes agree or the minimized case agrees when it is
    /// run again (<see cref="UnstableCases"/>).
    /// </returns>
    /// <exception cref="InvalidOperationException">A side does not evaluate partially (<see cref="SupportsPartial"/>).</exception>
    internal Divergence? CheckPartial(ulong seed, ulong number, GeneratedCase generated, IReadOnlyList<UnknownAttribute> patterns)
    {
        if (library is not IPartialSide partialLibrary || oracle is not IPartialSide partialOracle)
        {
            throw new InvalidOperationException("a side does not evaluate partially");
        }

        var libraryOnUnknowns = new PartialSide(partialLibrary, patterns);
        var channel = new Channel(
            string.Empty,
            libraryOnUnknowns,
            new PartialSide(partialOracle, patterns),
            null,
            "cel-go returns",
            "we return",
            libraryOnUnknowns,
            new PartialRecorder(patterns),
            Partial: true,
            UnstableSide.Library,
            UnstableSide.Oracle);
        return CheckChannel(channel, seed, number, generated);
    }

    private Divergence? CheckChannel(Channel channel, ulong seed, ulong number, GeneratedCase generated)
    {
        (SideOutcome firstVariant, SideOutcome firstReference, DivergenceKind? firstKind) = Outcomes(channel, generated);
        if (firstKind is null)
        {
            return null;
        }

        GeneratedCase minimized = Minimizer.Minimize(
            generated,
            candidate => Classify(channel, candidate),
            (macro, instance) => AcceptsBodyInstance(channel, macro, instance),
            candidate => LibraryValue(channel, candidate),
            candidate => StepKindOf(channel, candidate));
        (SideOutcome variantOutcome, SideOutcome referenceOutcome, DivergenceKind? kind) = Outcomes(channel, minimized);
        if (kind is not { } divergenceKind)
        {
            // The case diverged and the descent kept only nodes that diverged, so a side gave another outcome for the same text and
            // bindings. That is a finding of the run and not a reason to end it: the run goes on with the next case, so that a long run
            // is not lost to one case, and the number of the case, which --case reproduces, is in the findings and the journal line.
            // The rare case is run again, so that the finding names the side that varies, which is all that tells the priority of it.
            UnstableSide side = VariedSide(channel, generated, firstVariant, firstReference, minimized, variantOutcome, referenceOutcome);
            unstableCases.Add(new UnstableCase(seed, number, ComparisonName(channel), minimized.Text, side));
            return null;
        }

        string signature = channel.Prefix + Signature.Of(minimized, variantOutcome, referenceOutcome, divergenceKind, channel.Variant, channel.Reference);
        Divergence divergence = new(signature, divergenceKind, minimized, variantOutcome, referenceOutcome, channel.ReferencePhrase, channel.VariantPhrase);
        if (known.Contains(signature))
        {
            knownSeen.Add(signature);
        }
        else if (newSeen.Add(signature))
        {
            string name = string.Create(CultureInfo.InvariantCulture, $"{seed}-{number}");
            string text = channel.Recorder.Case(name, signature, minimized.Text, minimized.Bindings);
            newSignatures.Add(new NewSignature(divergence, seed, number, text, channel.Partial));
        }

        return divergence;
    }

    // Which side of the comparison varies: each side evaluates the original text and the minimized text again, with the same bindings,
    // RepeatsOfUnstableCase times, and varies when one of the runs gives an outcome that differs from the outcome the side gave when the
    // text was checked. A side that gave no other outcome in the runs is not named; none is named when the variation did not come back.
    private static UnstableSide VariedSide(
        Channel channel,
        GeneratedCase generated,
        SideOutcome generatedVariant,
        SideOutcome generatedReference,
        GeneratedCase minimized,
        SideOutcome minimizedVariant,
        SideOutcome minimizedReference)
    {
        bool variantVaries = Varies(channel, generated, generatedVariant, generatedReference, ofVariant: true)
            || Varies(channel, minimized, minimizedVariant, minimizedReference, ofVariant: true);
        bool referenceVaries = Varies(channel, generated, generatedVariant, generatedReference, ofVariant: false)
            || Varies(channel, minimized, minimizedVariant, minimizedReference, ofVariant: false);
        if (variantVaries && referenceVaries)
        {
            return channel.VariantSide == channel.ReferenceSide ? channel.VariantSide : UnstableSide.Both;
        }

        if (variantVaries)
        {
            return channel.VariantSide;
        }

        return referenceVaries ? channel.ReferenceSide : UnstableSide.None;
    }

    // Whether the variant or the reference of the comparison gives, in one of the repeated runs of a text, an outcome that differs from
    // the one it gave when the text was checked. The exchange takes the outcome of its reference as it was, so that only the variant
    // is evaluated again when the variant is asked.
    private static bool Varies(Channel channel, GeneratedCase generated, SideOutcome variantOutcome, SideOutcome referenceOutcome, bool ofVariant)
    {
        for (int run = 0; run < RepeatsOfUnstableCase; run++)
        {
            SideOutcome again;
            SideOutcome before;
            if (!ofVariant)
            {
                again = channel.Reference.Evaluate(generated.Text, generated.Bindings);
                before = referenceOutcome;
            }
            else if (channel.Exchange is { } exchange)
            {
                again = exchange.Evaluate(generated.Text, generated.Bindings, referenceOutcome);
                before = variantOutcome;
            }
            else
            {
                again = channel.Variant.Evaluate(generated.Text, generated.Bindings);
                before = variantOutcome;
            }

            if (DivergenceKinds.Compare(before, again) is not null)
            {
                return true;
            }
        }

        return false;
    }

    // The name of a comparison for the message about an unstable case: empty for the sides on the text, else the kind of the comparison.
    private static string ComparisonName(Channel channel) => channel.Partial ? "partial evaluation" : channel.Prefix.TrimEnd(':', ' ');

    // How the outcomes of a node on the way down agree: they diverge, or agree because both sides give an error, or agree
    // otherwise. A node is evaluated once on each side, whatever the verdict is used for.
    private static NodeVerdict Classify(Channel channel, GeneratedCase generated)
    {
        (SideOutcome variantOutcome, SideOutcome referenceOutcome, _) = Outcomes(channel, generated);
        return VerdictOf(variantOutcome, referenceOutcome);
    }

    /// <summary>
    /// The verdict on a node of the descent: the outcomes diverge, or agree because both sides give an error or unknown parts,
    /// whatever its cause, or agree otherwise.
    /// </summary>
    /// <param name="library">The outcome of the node on the library.</param>
    /// <param name="oracle">The outcome of the node on cel-go.</param>
    internal static NodeVerdict VerdictOf(SideOutcome library, SideOutcome oracle) =>
        DivergenceKinds.Compare(library, oracle) is not null ? NodeVerdict.Diverges
        : library.IsError || library.IsUnknown ? NodeVerdict.AgreesInError
        : NodeVerdict.Agrees;

    // A minimized instance of a body of a macro replaces the macro only when its signature is the signature of the macro or a
    // known one: a divergence under a macro that nobody knows stays the signature of the macro.
    private bool AcceptsBodyInstance(Channel channel, GeneratedCase macro, GeneratedCase instance) =>
        SignatureOf(channel, instance) is { } signature && (known.Contains(signature) || signature == SignatureOf(channel, macro));

    // The value of a case on the library, which gives the elements of a macro range that is not a literal; the range agrees on
    // both sides, since the descent has compared it as a child before. In partial evaluation it is the value on the unknown data.
    private static Value? LibraryValue(Channel channel, GeneratedCase generated) => channel.Values.Evaluate(generated.Text, generated.Bindings).Result;

    // What an instance of the transform of map, transformList or transformMapEntry, or of the key of sortBy, gives, for the step of the fold: a value, unknown parts or an error
    // of evaluation that the two sides give alike, else other (Minimizer.FoldStep). Unknown parts come only in partial evaluation, so
    // the ordinary check builds no step.
    private static StepKind StepKindOf(Channel channel, GeneratedCase generated)
    {
        (SideOutcome variantOutcome, _, DivergenceKind? kind) = Outcomes(channel, generated);
        if (kind is not null)
        {
            return StepKind.Other;
        }

        return variantOutcome.Class switch
        {
            OutcomeClass.Value => StepKind.Value,
            OutcomeClass.Unknown => StepKind.Unknown,
            OutcomeClass.EvaluationError => StepKind.Error,
            _ => StepKind.Other,
        };
    }

    private static string? SignatureOf(Channel channel, GeneratedCase generated)
    {
        (SideOutcome variantOutcome, SideOutcome referenceOutcome, DivergenceKind? kind) = Outcomes(channel, generated);
        return kind is { } divergenceKind
            ? channel.Prefix + Signature.Of(generated, variantOutcome, referenceOutcome, divergenceKind, channel.Variant, channel.Reference)
            : null;
    }

    private static (SideOutcome Variant, SideOutcome Reference, DivergenceKind? Kind) Outcomes(Channel channel, GeneratedCase generated)
    {
        string text = generated.Text;
        SideOutcome variantOutcome;
        SideOutcome referenceOutcome;
        if (channel.Exchange is { } exchange)
        {
            // The exchange takes the outcome of its side for the text, so that it is evaluated once.
            referenceOutcome = channel.Reference.Evaluate(text, generated.Bindings);
            variantOutcome = exchange.Evaluate(text, generated.Bindings, referenceOutcome);
        }
        else
        {
            variantOutcome = channel.Variant.Evaluate(text, generated.Bindings);
            referenceOutcome = channel.Reference.Evaluate(text, generated.Bindings);
        }

        return (variantOutcome, referenceOutcome, DivergenceKinds.Compare(variantOutcome, referenceOutcome));
    }

    // A comparison of the run: the outcome to be compared with the reference, the wording of a finding, the side whose values give the
    // elements of a macro range, and what writes the case of a new signature. The sides on the text are the first channel, the library
    // against cel-go; an exchange is the same side on the text and on the bytes of the other; the partial evaluation of a case is the
    // two sides with the patterns as unknown data, and its case is one line, since it is no case of the regression corpus.
    private sealed record Channel(
        string Prefix,
        ICaseSide Variant,
        ICaseSide Reference,
        ExchangedSide? Exchange,
        string ReferencePhrase,
        string VariantPhrase,
        ICaseSide Values,
        ICaseRecorder Recorder,
        bool Partial,
        UnstableSide VariantSide,
        UnstableSide ReferenceSide);

    // The case of a new partial signature in one line, since it is no case of the regression corpus: the expression, quoted with the
    // escapes of JSON so that it stays on one line, and the unknown data; a finding message carries it, and the bindings come with
    // `--case` of the seed.
    private sealed class PartialRecorder(IReadOnlyList<UnknownAttribute> patterns) : ICaseRecorder
    {
        private static readonly JsonSerializerOptions QuoteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public string Case(string name, string description, string expression, IReadOnlyDictionary<string, Value> bindings) =>
            "expression " + JsonSerializer.Serialize(expression, QuoteOptions) + " with unknown data " + string.Join(", ", patterns);
    }
}
