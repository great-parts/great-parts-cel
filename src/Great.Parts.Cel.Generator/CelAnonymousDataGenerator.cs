using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Generator;

/// <summary>
/// The incremental source generator of the call sites of <c>CelExpression.Evaluate</c>. For each anonymous type that a call passes
/// as the data it writes a file that registers the type with <c>CelAnonymousData</c> from a module initializer, so the simple entry
/// reads the properties without reflection. It warns where the static type of the data hides what the call passes, and reports an
/// error for a property the simple entry cannot read. A class, a structure or an interface of the application in a property is
/// described by one more file of the assembly, written with the code of the type context generator.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class CelAnonymousDataGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // One value for each call of the method. The transform reads the type of the data into values, so the steps after it
        // compare by value: moving a call, or editing a file that has none, leaves the shapes and the files unchanged.
        IncrementalValuesProvider<CallSite> sites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => CallSiteReader.IsCandidate(node),
                static (syntaxContext, cancellationToken) => CallSiteReader.Read(syntaxContext, cancellationToken))
            .Where(static site => site is not null)
            .Select(static (site, _) => site!)
            .WithTrackingName("CallSite");

        // The distinct shapes in the order of their first call. The place of a call is in the input of this step only: the output
        // is the shapes, which are equal when a call has moved and the order has not changed.
        IncrementalValueProvider<EquatableArray<AnonymousShape>> shapes = sites
            .Where(static site => site.Shape is not null)
            .Select(static (site, _) => new ShapeAtPlace(site.Shape!, site.Place))
            .Collect()
            .Select(static (all, _) => Distinct(all))
            .WithTrackingName("Shapes");

        IncrementalValueProvider<EquatableArray<DiagnosticModel>> diagnostics = sites
            .Select(static (site, _) => site.Diagnostics)
            .Collect()
            .Select(static (all, _) => Merge(all))
            .WithTrackingName("CallSiteDiagnostics");

        // The types that the classes in the properties reach, put together for the assembly: one description for each type whatever the
        // number of the calls that reach it. Two outputs of one step, as for a type context: the places of the diagnostics of the whole
        // move with the calls, and the model must stay equal then.
        IncrementalValueProvider<ClassDescriptionsResult> descriptions = sites
            .Where(static site => site.Shape is not null && site.Closure.Objects.Length > 0)
            .Select(static (site, _) => new ClosureAtPlace(site.Closure, site.Place))
            .Collect()
            .Select(static (all, _) => ClassDescriptions.Merge(all))
            .WithTrackingName("ClassDescriptions");
        IncrementalValueProvider<ClassDescriptionsModel?> model = descriptions
            .Select(static (result, _) => result.Model)
            .WithTrackingName("ClassDescriptionsModel");
        IncrementalValueProvider<EquatableArray<DiagnosticModel>> descriptionDiagnostics = descriptions
            .Select(static (result, _) => result.Diagnostics)
            .WithTrackingName("ClassDescriptionDiagnostics");

        // Whether the assembly sees the internal members of the library: the created overrides must match the access of the members
        // they override. The value is a flag, so an edit of the sources leaves the step unchanged.
        IncrementalValueProvider<bool> internalAccess = context.CompilationProvider
            .Select(static (compilation, _) => CompilationFacts.SeesLibraryInternals(compilation))
            .WithTrackingName("InternalAccess");

        context.RegisterSourceOutput(
            shapes.Combine(model).Combine(internalAccess),
            static (production, pair) => Write(production, pair.Left.Left, pair.Left.Right, pair.Right));
        context.RegisterSourceOutput(
            diagnostics.Combine(context.CompilationProvider),
            static (production, pair) => DiagnosticReporter.Report(production, pair.Left, pair.Right));
        context.RegisterSourceOutput(
            descriptionDiagnostics.Combine(context.CompilationProvider),
            static (production, pair) => DiagnosticReporter.Report(production, pair.Left, pair.Right));
    }

    // The shapes without repeats, ordered by the file and the position of their first call.
    private static EquatableArray<AnonymousShape> Distinct(ImmutableArray<ShapeAtPlace> all)
    {
        ShapeAtPlace[] ordered = [.. all.OrderBy(static item => item.Place.FilePath, StringComparer.Ordinal).ThenBy(static item => item.Place.Span.Start)];
        var seen = new HashSet<AnonymousShape>();
        var shapes = new List<AnonymousShape>();
        foreach (ShapeAtPlace item in ordered)
        {
            if (seen.Add(item.Shape))
            {
                shapes.Add(item.Shape);
            }
        }

        return EquatableArray.From(shapes);
    }

    // The diagnostics of all calls without repeats: the property of an anonymous type that two calls pass is one error. The language
    // version below C# 12 (GPCEL008) is one error for the compilation, at the first call by the file and the position.
    private static EquatableArray<DiagnosticModel> Merge(ImmutableArray<EquatableArray<DiagnosticModel>> all)
    {
        var seen = new HashSet<DiagnosticModel>();
        var merged = new List<DiagnosticModel>();
        DiagnosticModel? language = null;
        foreach (EquatableArray<DiagnosticModel> group in all)
        {
            foreach (DiagnosticModel model in group.Items)
            {
                if (model.Id == DiagnosticIds.LanguageVersion)
                {
                    language = language is null || IsBefore(model.Location, language.Location) ? model : language;
                }
                else if (seen.Add(model))
                {
                    merged.Add(model);
                }
            }
        }

        if (language is not null)
        {
            merged.Insert(0, language);
        }

        return EquatableArray.From(merged);
    }

    private static bool IsBefore(LocationInfo left, LocationInfo right)
    {
        int byFile = string.CompareOrdinal(left.FilePath, right.FilePath);
        return byFile != 0 ? byFile < 0 : left.Span.Start < right.Span.Start;
    }

    // A form that reaches classes needs the descriptions: when the whole has none (the limit of the closure is passed, which is an
    // error already) the form gets no file, as a form with an error gets none.
    private static void Write(SourceProductionContext production, EquatableArray<AnonymousShape> shapes, ClassDescriptionsModel? descriptions, bool internalAccess)
    {
        AnonymousShape[] items = shapes.Items;
        for (int index = 0; index < items.Length; index++)
        {
            AnonymousShape shape = items[index];
            if (descriptions is null && (shape.Objects.Length > 0 || shape.Enums.Length > 0))
            {
                continue;
            }

            int number = index + 1;
            production.AddSource(RegistrationEmitter.HintName(number), SourceText.From(RegistrationEmitter.Write(shape, number, descriptions), Encoding.UTF8));
        }

        if (descriptions is not null)
        {
            production.AddSource(CallSiteNames.HintName, SourceText.From(DescriptionEmitter.Write(descriptions, shapes, internalAccess), Encoding.UTF8));
        }
    }
}
