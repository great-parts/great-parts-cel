using System;
using Great.Parts.Cel.Protobuf;

namespace Great.Parts.Cel.Conformance.FilterCorpus;

/// <summary>
/// The environment of the cases of the corpus: the fields of <see cref="Item"/>, described by the protobuf provider of the test
/// messages, are the variables, and a few functions are what the filters call.
/// </summary>
internal static class FilterEnvironment
{
    /// <summary>The full name of the enumeration of the field <c>color</c>, as the provider names it.</summary>
    internal const string ColorType = "greatparts.filtering.test.Item.Color";

    /// <summary>The provider of the messages of the schema of the corpus.</summary>
    internal static CelProtobufTypes Types { get; } = new CelProtobufTypesBuilder("filter-corpus")
        .AddFile(FilterTestReflection.Descriptor)
        .Build();

    /// <summary>
    /// The environment: <c>UseInput&lt;Item&gt;()</c> and the functions <c>lower</c>, <c>isRecent</c>, <c>add</c>, <c>seven</c> and
    /// <c>acme.shout</c>.
    /// </summary>
    internal static CelEnvironment Value { get; } = CelEnvironment.Create(builder => builder
        .AddTypes(Types)
        .UseInput<Item>()
        .AddFunction<string, string>("lower", Lower)
        .AddFunction<DateTimeOffset, bool>("isRecent", static published => published.Year >= 2000)
        .AddFunction<long, long, long>("add", static (left, right) => left + right)
        .AddFunction<long>("seven", static () => 7L)
        .AddFunction<string, string>("acme.shout", Shout));

    private static string Lower(string text) => string.Create(text.Length, text, static (lower, source) => source.AsSpan().ToLowerInvariant(lower));

    private static string Shout(string text) => string.Create(text.Length, text, static (upper, source) => source.AsSpan().ToUpperInvariant(upper));
}
