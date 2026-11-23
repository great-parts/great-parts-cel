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

    private readonly ICaseSide library;
    private readonly ICaseSide oracle;
    private readonly ICaseRecorder recorder;
    private readonly KnownSignatures known;
    private readonly Channel texts;
    private readonly List<Channel> exchanges = [];
    private readonly List<NewSignature> newSignatures = [];
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
        texts = new Channel(string.Empty, library, oracle, null, "cel-go returns", "we return", library, recorder, Partial: false);
    }

    /// <summary>
    /// Adds the comparisons of the exchange of a checked expression, which a case is run through when the sides agree on the text.
    /// </summary>
    /// <param name="channels">The comparisons.</param>
    internal void AddExchanges(IEnumerable<ExchangeChannel> channels)
    {
        exchanges.AddRange(channels.Select(channel => new Channel(
            channel.Prefix, channel.Variant, channel.Reference, channel.Variant, channel.ReferencePhrase, channel.VariantPhrase, library, recorder, Partial: false)));
    }

    /// <summary>The first case of each new signature, in the order the run found them.</summary>
    internal IReadOnlyList<NewSignature> NewSignatures => newSignatures;

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
    /// when nothing diverges.
    /// </returns>
    /// <exception cref="SideFailureException">The minimized case agrees when it is run again: a side is not deterministic.</exception>
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
    /// <returns>The divergence of the minimized case, or <see langword="null"/> when the outcomes agree.</returns>
    /// <exception cref="InvalidOperationException">A side does not evaluate partially (<see cref="SupportsPartial"/>).</exception>
    /// <exception cref="SideFailureException">The minimized case agrees when it is run again: a side is not deterministic.</exception>
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
            Partial: true);
        return CheckChannel(channel, seed, number, generated);
    }

    private Divergence? CheckChannel(Channel channel, ulong seed, ulong number, GeneratedCase generated)
    {
        if (Outcomes(channel, generated).Kind is null)
        {
            return null;
        }

        GeneratedCase minimized = Minimizer.Minimize(
            generated,
            candidate => Classify(channel, candidate),
            (macro, instance) => AcceptsBodyInstance(channel, macro, instance),
            candidate => LibraryValue(channel, candidate));
        (SideOutcome variantOutcome, SideOutcome referenceOutcome, DivergenceKind? kind) = Outcomes(channel, minimized);
        if (kind is not { } divergenceKind)
        {
            string run = string.Create(CultureInfo.InvariantCulture, $"case {number} of seed {seed}");
            throw new SideFailureException(
                $"{run}: the minimized case does not diverge again: a side is not deterministic",
                $"{run}: the minimized case '{minimized.Text}' does not diverge again: a side is not deterministic");
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
        bool Partial);

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
