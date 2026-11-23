using System.Collections.Generic;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Corpus;

/// <summary>A side of the run that evaluates a text and also writes and loads checked expressions.</summary>
/// <param name="Text">The side as the evaluator of a text.</param>
/// <param name="Checked">The side as the writer and the loader of checked expressions.</param>
internal sealed record ExchangeParty(ICaseSide Text, ICheckedSide Checked);

/// <summary>
/// One of the two comparisons of the exchange of a checked expression (docs/design/ast-protobuf.md, "Exchange with cel-go"): the
/// outcome of the exchange against the outcome of the same side for the text. A divergence of it has its own signature, with the
/// prefix, so it is told from the divergence of the sides on the text.
/// </summary>
/// <param name="Prefix">The start of the signature of a divergence found by the comparison.</param>
/// <param name="ReferencePhrase">Who gives the reference outcome, with the verb, for the message of a finding.</param>
/// <param name="VariantPhrase">Who gives the outcome of the exchange, with the verb, for the message of a finding.</param>
/// <param name="Reference">The side that evaluates the text.</param>
/// <param name="Variant">The exchange: the same side evaluates the bytes that the other side wrote.</param>
internal sealed record ExchangeChannel(
    string Prefix,
    string ReferencePhrase,
    string VariantPhrase,
    ICaseSide Reference,
    ExchangedSide Variant)
{
    /// <summary>
    /// The two comparisons of a run: the result of cel-go from our bytes against the result of cel-go from the text, and our result
    /// from the bytes of cel-go against our result from the text.
    /// </summary>
    /// <param name="library">The library side.</param>
    /// <param name="oracle">The oracle.</param>
    internal static IReadOnlyList<ExchangeChannel> Both(ExchangeParty library, ExchangeParty oracle) =>
    [
        new ExchangeChannel(
            "checked-to-cel-go: ",
            "cel-go from the text returns",
            "cel-go from our bytes returns",
            oracle.Text,
            new ExchangedSide(library.Checked, oracle.Checked, oracle.Text)),
        new ExchangeChannel(
            "checked-from-cel-go: ",
            "the library from the text returns",
            "the library from the bytes of cel-go returns",
            library.Text,
            new ExchangedSide(oracle.Checked, library.Checked, library.Text)),
    ];
}
