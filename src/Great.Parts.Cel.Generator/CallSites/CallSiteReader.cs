using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Great.Parts.Cel.Generator;

// Reads a call of CelExpression.Evaluate<T>(string, object, ...) into values: the predicate keeps the syntax that can be such a call,
// and the transform keeps the calls whose symbol is that method, so a full name, using static and an alias are found and a method
// Evaluate of another type is not. The data argument is read by its static type, not by the type of the parameter, which is object.
internal static class CallSiteReader
{
    private const string AdviceTypeSource = "pass it as a property of an anonymous object, such as new { order }, or build an environment with a type source and UseInput<T>";

    private const string AdviceAnonymousObject = "pass the anonymous object at the call or pass a dictionary";

    private const string AdviceDictionary = "pass a dictionary with string keys and values of the list, or an anonymous object";

    private const string AdviceTypeSourceForProperty = "; build an environment with a type source";

    // The name of a property of an anonymous object is chosen by the author of the call, and a type source does not correct it.
    private const string AdviceRename = "; rename the property";

    // What the created files of this generator are, in the message of GPCEL008: there is no type context here.
    private const string LanguageSubject = "CelExpression.Evaluate";

    // Syntax only: a call whose name is Evaluate, as a simple name, a generic name or the name of a member access, with at least the
    // two arguments of the expression and the data.
    internal static bool IsCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: >= 2 } invocation
        && invocation.Expression switch
        {
            SimpleNameSyntax name => name.Identifier.ValueText == "Evaluate",
            MemberAccessExpressionSyntax { Name: var name } => name.Identifier.ValueText == "Evaluate",
            _ => false,
        };

    internal static CallSite? Read(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        SemanticModel model = context.SemanticModel;
        if (model.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation || !IsSimpleEntry(operation.TargetMethod))
        {
            return null;
        }

        ArgumentSyntax? argument = DataArgument(operation);
        if (argument is null)
        {
            return null;
        }

        ITypeSymbol? type = model.GetTypeInfo(argument.Expression, cancellationToken).Type;
        if (type is null || type.TypeKind is TypeKind.Error or TypeKind.Dynamic)
        {
            return null;
        }

        LocationInfo place = LocationInfo.From(argument.Expression.GetLocation());
        if (type.IsAnonymousType)
        {
            return ReadAnonymous(model.Compilation, type, place, LocationInfo.From(invocation.GetLocation()), CompilationFacts.LanguageOf(invocation.SyntaxTree), cancellationToken);
        }

        if (PropertyRules.IsListedDictionary(type))
        {
            return null;
        }

        string advice = type.TypeKind == TypeKind.TypeParameter || type.SpecialType == SpecialType.System_Object
            ? AdviceAnonymousObject
            : IsDictionary(type) ? AdviceDictionary : AdviceTypeSource;
        var warning = new DiagnosticModel(DiagnosticIds.SimpleEntryDataType, place, new EquatableArray<string>([TypeNames.Display(type), advice]));
        return new CallSite(null, place, new EquatableArray<DiagnosticModel>([warning]), ClassClosure.Empty);
    }

    // CelExpression.Evaluate<T>(string expression, object data, ...) of the library: the overload with the object, not the one with a dictionary.
    private static bool IsSimpleEntry(IMethodSymbol method) =>
        method.Name == "Evaluate"
        && method.Parameters.Length >= 2
        && method.Parameters[1].Type.SpecialType == SpecialType.System_Object
        && method.ContainingType is { Name: "CelExpression" } type
        && type.ContainingAssembly?.Name == WellKnown.CelLibraryAssembly
        && TypeNames.NamespaceName(type.ContainingNamespace) == WellKnown.CelLibraryAssembly;

    // The argument that the second parameter takes, by position or by the name data.
    private static ArgumentSyntax? DataArgument(IInvocationOperation operation)
    {
        foreach (IArgumentOperation argument in operation.Arguments)
        {
            if (argument.Parameter?.Ordinal == 1 && argument.ArgumentKind == ArgumentKind.Explicit && argument.Syntax is ArgumentSyntax syntax)
            {
                return syntax;
            }
        }

        return null;
    }

    private static bool IsDictionary(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol own && IsDictionaryContract(own))
        {
            return true;
        }

        foreach (INamedTypeSymbol contract in type.AllInterfaces)
        {
            if (IsDictionaryContract(contract))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDictionaryContract(INamedTypeSymbol type)
    {
        string name = TypeNames.FullName(type);
        return name == WellKnown.ReadOnlyDictionary || name == WellKnown.Dictionary;
    }

    // The shape of an anonymous type, the errors of its properties and, when the project's language version is below C# 12, GPCEL008
    // at the call and no shape: the created file needs file-local classes. The generator reports it once for the compilation, at the
    // first call, from the diagnostics of all calls.
    private static CallSite ReadAnonymous(Compilation compilation, ITypeSymbol type, LocationInfo place, LocationInfo call, int language, CancellationToken cancellationToken)
    {
        var properties = new List<PropertyModel>();
        var diagnostics = new List<DiagnosticModel>();
        ClassProperties? classes = null;
        bool failed = false;
        foreach (ISymbol member in type.GetMembers())
        {
            if (member is not IPropertySymbol property)
            {
                continue;
            }

            LocationInfo where = PropertyPlace(property, place);
            PropertyModel? model = ReadProperty(compilation, property, where, ref classes, cancellationToken, out string? reason);
            if (model is not null)
            {
                properties.Add(model);
                continue;
            }

            failed = true;
            if (reason is not null)
            {
                string advice = reason == PropertyRules.ReasonNotIdentifier ? AdviceRename : AdviceTypeSourceForProperty;
                diagnostics.Add(new DiagnosticModel(
                    DiagnosticIds.SimpleEntryPropertyType,
                    where,
                    new EquatableArray<string>([property.Name, TypeNames.Display(property.Type), reason + advice])));
            }
        }

        if (language < CompilationFacts.MinimumLanguage)
        {
            failed = true;
            diagnostics.Add(new DiagnosticModel(
                DiagnosticIds.LanguageVersion,
                call,
                new EquatableArray<string>([LanguageSubject, CompilationFacts.TextOf(language)])));
        }

        // The types that the classes reach are read for the whole object: a call with an error gives no file, so no description.
        ClassClosure closure = ClassClosure.Empty;
        EquatableArray<string> objects = default;
        EquatableArray<string> enums = default;
        if (classes is not null)
        {
            if (!failed)
            {
                (closure, objects, enums) = classes.Close();
            }

            diagnostics.AddRange(classes.Diagnostics.Items);
        }

        AnonymousShape? shape = failed ? null : new AnonymousShape(EquatableArray.From(properties), objects, enums);
        return new CallSite(shape, place, EquatableArray.From(diagnostics), closure);
    }

    // One property against the rules: the list of the values first, then the classes of the application. The model is null when the
    // property is refused; reason is then the text of GPCEL011, or null when a diagnostic of the type context was reported instead.
    private static PropertyModel? ReadProperty(
        Compilation compilation,
        IPropertySymbol property,
        LocationInfo where,
        ref ClassProperties? classes,
        CancellationToken cancellationToken,
        out string? reason)
    {
        reason = null;
        PropertyModel? model = null;
        PropertyRules.ListedRead listed = PropertyRules.TryRead(compilation, property.Type, out string csharpType, out string typeReason);
        if (listed == PropertyRules.ListedRead.Read)
        {
            DirectKind direct = PropertyRules.DirectOf(property.Type, out bool nullable);
            model = new PropertyModel(property.Name, csharpType, direct, nullable);
        }
        else if (listed == PropertyRules.ListedRead.Refused)
        {
            reason = typeReason;
        }
        else
        {
            classes ??= new ClassProperties(compilation.Assembly, cancellationToken);
            ClassReading reading = classes.Read(property.Type, where);
            switch (reading.Outcome)
            {
                case ClassOutcome.Single:
                    model = new PropertyModel(property.Name, csharpType);
                    break;
                case ClassOutcome.Collection:
                    model = new PropertyModel(property.Name, csharpType, Collection: reading.Collection);
                    break;
                case ClassOutcome.Refused:
                    reason = reading.Reason;
                    break;
                case ClassOutcome.NotClass:
                    reason = typeReason;
                    break;
                default:
                    // Reported: a diagnostic of the type context stands for the property, so the reason stays null.
                    break;
            }
        }

        if (model is not null && !TypeNames.IsIdentifier(property.Name))
        {
            model = null;
            reason = PropertyRules.ReasonNotIdentifier;
        }

        return model;
    }

    // The property in the creation expression of the anonymous object; the call when the symbol has no place in source.
    private static LocationInfo PropertyPlace(IPropertySymbol property, LocationInfo fallback)
    {
        foreach (Location location in property.Locations)
        {
            if (location.IsInSource)
            {
                return LocationInfo.From(location);
            }
        }

        return fallback;
    }
}
