using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Generator;

/// <summary>
/// The incremental source generator of the application data types. It turns a partial class derived from
/// <c>CelTypeContext</c> with <c>[CelObject]</c> attributes into the descriptions of the listed .NET types and of the
/// types their members reach, and reports a diagnostic for what it cannot describe.
/// </summary>
[Generator]
public sealed class CelTypeContextGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // One value for each class that carries the attribute. The transform reads everything about the context into values, so
        // the steps after it compare by value and an edit that changes nothing a context reads leaves them unchanged.
        IncrementalValuesProvider<ContextReadResult> reads = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WellKnown.CelObjectAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (attributeContext, cancellationToken) => ContextReader.Read(attributeContext, cancellationToken))
            .WithTrackingName("ContextRead");

        // Two outputs of one step: the places of the diagnostics move when a file above the context changes, and the model must
        // stay equal then, so that the file is not written again for a change of a line number.
        IncrementalValuesProvider<ContextModel?> models = reads
            .Select(static (result, _) => result.Model)
            .WithTrackingName("ContextModel");
        IncrementalValuesProvider<EquatableArray<DiagnosticModel>> diagnostics = reads
            .Select(static (result, _) => result.Diagnostics)
            .WithTrackingName("Diagnostics");

        context.RegisterSourceOutput(models, static (production, model) => Write(production, model));
        context.RegisterSourceOutput(
            diagnostics.Combine(context.CompilationProvider),
            static (production, pair) => DiagnosticReporter.Report(production, pair.Left, pair.Right));
    }

    private static void Write(SourceProductionContext production, ContextModel? model)
    {
        if (model is null)
        {
            return;
        }

        string? text = ContextEmitter.Write(model);
        if (text is not null)
        {
            production.AddSource(model.HintName, SourceText.From(text, Encoding.UTF8));
        }
    }
}
